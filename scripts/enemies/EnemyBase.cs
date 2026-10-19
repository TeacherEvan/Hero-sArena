using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    public enum EnemyAIState { Idle, Chase, Attack, Flee, Dead }

    /// <summary>
    /// Abstract base for all enemies. AI updates at 10 Hz via timer.
    /// Registers self with the SpatialHashGrid on spawn.
    /// </summary>
    public abstract partial class EnemyBase : CharacterBody2D
    {
        // F-1 regression site: entity-id → enemy lookup. The pure registry is
        // EntityRegistry<T> (covered headlessly in EntityRegistryTests); this
        // class keeps only the engine wiring (_Ready/_ExitTree).
        private static readonly EntityRegistry<EnemyBase> Registry = new();

        public static bool TryGetById(int entityId, out EnemyBase? enemy)
            => Registry.TryGet(entityId, out enemy);

        /// <summary>Returns all registered enemies that are not dead.</summary>
        public static IEnumerable<EnemyBase> GetAllAlive()
        {
            foreach (var enemy in Registry.GetAll())
            {
                if (enemy != null && enemy.State != EnemyAIState.Dead)
                    yield return enemy;
            }
        }

        [Export] public float MaxHealth { get; set; } = 50f;
        [Export] public float MoveSpeed { get; set; } = 100f;
        [Export] public float Damage { get; set; } = 10f;
        [Export] public int ExpValue { get; set; } = 10;

        public float CurrentHealth { get; protected set; }

        // ── Spawn-time base stats (mutator scaling applies to these, not to
        // ── current values, so repeated threat-tier applications are
        // ── idempotent instead of compounding exponentially). Cached in _Ready.
        public float BaseMoveSpeed { get; private set; } = 100f;
        public float BaseMaxHealth { get; private set; } = 50f;
        public float BaseDamage { get; private set; } = 10f;
        public EnemyAIState State { get; protected set; } = EnemyAIState.Idle;
        public int ShieldCharges { get; private set; } = 0;

        protected HeroBase? _hero;
        protected int _entityId;

        private float _aiTimer = 0f;
        private const float AI_INTERVAL = 0.1f; // 10 Hz
        private static readonly Random _dropRng = new();

        // ── Mind control ────────────────────────────────────────────
        // While mind-controlled (Synapse Overclock), the enemy hunts other
        // enemies instead of the hero, its normal AI is suspended, and a blue
        // Modulate tint signals the state. Timer/cooldown rules live in the
        // Godot-free MindControlState (headlessly covered in tests).
        private readonly MindControlState _mindControl = new();
        private EnemyBase? _mindControlTarget;
        private float _mindControlRetargetTimer = 0f;
        private Color _preMindControlModulate = new(1f, 1f, 1f, 1f);
        private bool _modulateStored = false;

        /// <summary>Seconds between target re-acquisitions while mind-controlled
        /// (prevents a spatial-grid query every physics frame).</summary>
        private const float MIND_CONTROL_RETARGET_INTERVAL = 0.5f;
        private const float MIND_CONTROL_HUNT_RADIUS = 600f;

        public bool IsMindControlled => _mindControl.IsControlled;

        private StatusEffectState _status = new();
        public bool IsSlowed => _status.IsSlowed;
        public bool IsBurning => _status.IsBurning;

        public void ApplySlow(float duration, float ratio = 0.30f) => _status.ApplySlow(duration, ratio);
        public void ApplyBurn(float duration, float dps = 5f) => _status.ApplyBurn(duration, dps);

        private Vector2 _lastGridPos;
        protected CharacterJuiceState _juice = new();
        protected Sprite2D? _sprite;
        private Vector2 _baseSpriteScale = Vector2.One;

        public override void _Ready()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            CurrentHealth = MaxHealth;
            // Cache spawn-time base stats so mutator scaling is idempotent.
            BaseMaxHealth = MaxHealth;
            BaseMoveSpeed = MoveSpeed;
            BaseDamage = Damage;
            _entityId = (int)(GetInstanceId() & 0x7FFFFFFF);

            var grid = gm.SpatialGrid;
            grid?.Insert(_entityId, GlobalPosition, 16f);

            Registry.Register(_entityId, this);
            _lastGridPos = GlobalPosition;

            _hero = gm.ActiveHero;
            _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            if (_sprite != null)
                _baseSpriteScale = _sprite.Scale;
            OnSpawn();
        }

        public override void _ExitTree()
        {
            GameManager.Instance?.SpatialGrid?.Remove(_entityId);
            Registry.Remove(_entityId);
            base._ExitTree();
        }

        protected virtual void OnSpawn() { }

        /// <summary>Turns this enemy into a stationary target dummy (no chasing, no AI updates).</summary>
        public void BecomeDummy()
        {
            State = EnemyAIState.Idle;
            _hero = null;
        }

        /// <summary>Mind-controls this enemy for <paramref name="duration"/> seconds.
        /// While mind-controlled, the enemy hunts other enemies instead of the
        /// hero and its normal AI is suspended.</summary>
        public void ApplyMindControl(float duration)
        {
            if (!_mindControl.IsControlled)
            {
                _preMindControlModulate = Modulate;
                _modulateStored = true;
            }
            _mindControl.Apply(duration);
            _mindControlTarget = null;
            _mindControlRetargetTimer = 0f;
            Modulate = new Color(0f, 0.5f, 1f, 1f); // Blue tint = mind-controlled
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Death frees nodes (the hero on game over, enemies when corpses
            // expire). A cached reference to a freed object is NOT null in C#,
            // so drop invalid references before the AI/movement code touches
            // them — otherwise every frame floods ObjectDisposedException.
            if (_hero != null && !IsInstanceValid(_hero)) _hero = null;
            if (_mindControlTarget != null && !IsInstanceValid(_mindControlTarget)) _mindControlTarget = null;

            if (_mindControl.Tick(dt) && _modulateStored)
            {
                Modulate = _preMindControlModulate; // Revert tint when control expires
                _modulateStored = false;
                _mindControlTarget = null;
            }

            // Normal AI is suspended while mind-controlled so hostile abilities
            // (Artillery mortars, Exploder proximity, Shielder bash, Apex
            // bullet-hell, ...) cannot keep damaging the hero.
            if (!IsMindControlled)
            {
                _aiTimer += dt;
                if (_aiTimer >= AI_INTERVAL)
                {
                    _aiTimer -= AI_INTERVAL;
                    UpdateAI();
                }
            }
            else
            {
                _aiTimer = 0f;
            }

            if (State != EnemyAIState.Dead)
            {
                // Status effects tick (burn damage + slow decay)
                float burnDmg = _status.Update(dt);
                if (burnDmg > 0f)
                {
                    ApplyHealth(CurrentHealth - burnDmg);
                    EventBus.Instance?.EmitDamageNumber(GlobalPosition, burnDmg, DamageType.Energy, false);
                }

                if (IsMindControlled)
                    MoveMindControlled(dt);
                else
                    Move(dt);
            }

            // Update spatial grid
            if (GlobalPosition.DistanceSquaredTo(_lastGridPos) > 1.0f)
            {
                GameManager.Instance?.SpatialGrid?.Update(_entityId, GlobalPosition, 16f);
                _lastGridPos = GlobalPosition;
            }

            UpdateJuice(dt);
        }

        private void UpdateJuice(float dt)
        {
            if (_sprite == null || !IsInstanceValid(_sprite)) return;

            bool isMoving = Velocity.LengthSquared() > 10f;
            float speedRatio = MoveSpeed > 0f ? Velocity.Length() / MoveSpeed : 0f;
            _juice.Update(dt, isMoving, speedRatio);

            // Facing direction: align with movement velocity or hero target
            Vector2 faceDir = Velocity.LengthSquared() > 10f
                ? Velocity
                : (_hero != null && IsInstanceValid(_hero) ? _hero.GlobalPosition - GlobalPosition : Vector2.Zero);

            if (faceDir != Vector2.Zero)
            {
                float targetAngle = faceDir.Angle();
                _sprite.Rotation = Mathf.LerpAngle(_sprite.Rotation, targetAngle + _juice.TiltAngle, Math.Min(1f, dt * 14f));
            }

            var scale = _baseSpriteScale;
            scale.X *= _juice.ScaleX;
            scale.Y *= _juice.ScaleY;
            _sprite.Scale = scale;
            _sprite.Position = new Vector2(0f, _juice.WalkBob);
        }

        protected abstract void UpdateAI();

        protected virtual void Move(float dt)
        {
            if (_hero == null || State == EnemyAIState.Dead) return;
            var flow = GameManager.Instance?.FlowField;
            Vector2 dir = flow != null
                ? flow.GetFlowDirection(GlobalPosition)
                : (_hero.GlobalPosition - GlobalPosition).Normalized();

            float speedMult = _status.GetSpeedMultiplier();
            if (_hero.Stats.FrostAura && HeroCombatMechanics.IsInFrostAura(DistanceSquaredToHero(), true))
            {
                speedMult = MathF.Min(speedMult, 1f - HeroCombatMechanics.FROST_AURA_SLOW_RATIO);
            }

            Velocity = dir * (MoveSpeed * speedMult);
            MoveAndSlide();
        }

        /// <summary>Mind-controlled movement: hunt the nearest uncontrolled
        /// enemy and land contact hits on a melee-style cooldown. Target
        /// re-acquisition is throttled — never a grid query per frame.</summary>
        private void MoveMindControlled(float dt)
        {
            _mindControlRetargetTimer -= dt;
            if (_mindControlTarget == null
                || _mindControlTarget.State == EnemyAIState.Dead
                || _mindControlRetargetTimer <= 0f)
            {
                AcquireMindControlTarget();
            }

            var target = _mindControlTarget;
            if (target == null)
            {
                Velocity = Vector2.Zero;
                MoveAndSlide();
                return;
            }

            Vector2 toTarget = target.GlobalPosition - GlobalPosition;
            float distance = toTarget.Length();
            if (distance > 0.001f)
            {
                if (_mindControl.TryConsumeContactHit(distance))
                    target.TakeDamage(Damage, DamageType.Kinetic);

                Velocity = toTarget / distance * MoveSpeed;
            }
            else
            {
                Velocity = Vector2.Zero;
            }

            MoveAndSlide();
        }

        /// <summary>Re-selects the nearest living, uncontrolled enemy within the
        /// hunt radius and restarts the retarget throttle.</summary>
        private void AcquireMindControlTarget()
        {
            _mindControlRetargetTimer = MIND_CONTROL_RETARGET_INTERVAL;
            _mindControlTarget = null;

            var grid = GameManager.Instance?.SpatialGrid;
            if (grid == null) return;

            int[] hits = grid.QueryRadius(GlobalPosition, MIND_CONTROL_HUNT_RADIUS, out int count, out bool _);
            float nearestDistSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (EnemyBase.TryGetById(hits[i], out var candidate) && candidate != null
                    && candidate != this
                    && candidate.State != EnemyAIState.Dead
                    && !candidate.IsMindControlled)
                {
                    float distSq = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        _mindControlTarget = candidate;
                    }
                }
            }
        }

        public virtual void TakeDamage(float amount, DamageType type = DamageType.Kinetic)
        {
            if (ShieldCharges > 0)
            {
                ShieldCharges--;
                return;
            }
            _juice.ApplyImpactFlinch();
            ApplyHealth(CurrentHealth - amount);
            EventBus.Instance?.EmitDamageNumber(GlobalPosition, amount, type, false);
        }

        public void AddShieldCharges(int charges) => ShieldCharges += charges;

        public void SetCurrentHealth(float value)
        {
            ApplyHealth(value);
        }

        private void ApplyHealth(float newValue)
        {
            CurrentHealth = Mathf.Clamp(newValue, 0f, MaxHealth);
            if (CurrentHealth <= 0f) Die();
        }

        protected virtual void Die()
        {
            if (State == EnemyAIState.Dead) return;
            State = EnemyAIState.Dead;
            GameManager.Instance?.SpatialGrid?.Remove(_entityId);
            EventBus.Instance?.EmitEnemyKilled(this);
            TryDropWeapon();
            QueueFree();
        }

        /// <summary>Apply an instantaneous knockback impulse (used by the
        /// level-up ring and explosive weapons). Applied to Velocity so the
        /// next MoveAndSlide carries the enemy away.</summary>
        public void ApplyKnockback(Vector2 direction, float force)
        {
            Velocity = direction * force;
        }

        /// <summary>Crimsonland weapon drop: enemies occasionally drop a
        /// weapon pickup on the ground. The hero walks over it to equip
        /// (one weapon at a time; re-equipping tops off ammo).</summary>
        private void TryDropWeapon()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            // Weapon Picker / Nukefism: no weapon pickups drop.
            if (gm.CurrentGameModeRules.WeaponPickupsDisabled) return;
            if (_dropRng.NextDouble() > 0.06) return; // 6% drop chance per kill

            var candidates = new List<WeaponId>
            {
                WeaponId.SubmachineGun, WeaponId.Shotgun, WeaponId.AssaultRifle,
                WeaponId.SniperRifle, WeaponId.PlasmaRifle, WeaponId.Railgun,
                WeaponId.GrenadeLauncher, WeaponId.Minigun
            };
            WeaponId drop = candidates[_dropRng.Next(candidates.Count)];

            var pickupScene = GD.Load<PackedScene>("res://scenes/items/PickupItem.tscn");
            if (pickupScene == null) return;
            var pickup = pickupScene.Instantiate<PickupItem>();
            pickup.Weapon = drop;
            pickup.GlobalPosition = GlobalPosition;
            GetTree().CurrentScene?.AddChild(pickup);
        }

        protected float DistanceSquaredToHero()
        {
            if (_hero == null) return float.MaxValue;
            return GlobalPosition.DistanceSquaredTo(_hero.GlobalPosition);
        }
    }
}
