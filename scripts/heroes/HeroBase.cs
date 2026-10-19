using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Abstract base for all hero characters.
    /// Handles twin-stick movement, dodge with coyote time, leveling, and damage.
    /// </summary>
    public abstract partial class HeroBase : CharacterBody2D
    {
        // ── Stats ─────────────────────────────────────────────────────────────
        /// <summary>All modifiable hero stats, shared with <see cref="PerkSystem"/>.</summary>
        public HeroStats Stats { get; } = new HeroStats();
        public System.Collections.Generic.HashSet<PerkType> TakenPerks { get; } = new();

        public void AddPerk(PerkType perk)
        {
            TakenPerks.Add(perk);
            PerkSystem.ApplyPerk(perk, Stats);
            PerkSystem.ApplySynergies(TakenPerks, Stats);
            // Weapon stat modifiers (reload speed, ammo capacity) apply to the
            // equipped weapon so perk picks take effect immediately.
            if (_weapon.HasWeapon)
                _weapon.Equip(_weapon.CurrentWeapon, Stats.AmmoCapacityBonus, Stats.ReloadSpeedMult);
        }

        [Export] public float MaxHealth { get => Stats.MaxHealth; set => Stats.MaxHealth = value; }
        [Export] public float MoveSpeed { get => Stats.MoveSpeed; set => Stats.MoveSpeed = value; }

        public float CurrentHealth { get; protected set; }
        public int Level { get; protected set; } = 1;
        public int Experience { get; protected set; } = 0;

        // ── Coyote / dodge ────────────────────────────────────────────────────
        protected const float COYOTE_TIME = 0.15f;
        protected float _dodgeCooldown = 0f;
        protected float _dodgeTimer = 0f;
        protected bool _isDodging = false;

        public float DodgeCooldownRemaining => Mathf.Max(0f, _dodgeCooldown);
        public float DodgeCooldownTotal { get; protected set; } = 0.8f;
        public virtual float AbilityCooldownRemaining => 0f;
        public virtual float AbilityCooldownTotal => 1f;

        // ── Weapon system (Crimsonland-style one-weapon-at-a-time arsenal) ──
        protected readonly WeaponSystem _weapon = new();
        /// <summary>Weapons the hero owns this run; starts with the Pistol.</summary>
        protected readonly HashSet<WeaponId> _ownedWeapons = new() { WeaponId.Pistol };
        protected float _weaponSwitchCooldown = 0f;
        /// <summary>Brief invulnerability granted on level up (Crimsonland forcefield).</summary>
        protected float _forcefieldTimer = 0f;

        /// <summary>Id of the currently equipped weapon (<see cref="WeaponId.None"/> = class attack).</summary>
        public WeaponId CurrentWeaponId => _weapon.CurrentWeapon;
        /// <summary>Ammo in the equipped weapon's magazine.</summary>
        public int CurrentWeaponAmmo => _weapon.Ammo;
        /// <summary>True while the equipped weapon is reloading.</summary>
        public bool IsWeaponReloading => _weapon.IsReloading;

        /// <summary>True when guns are disabled (Nukefism mode). Falls back to
        /// class signature attacks.</summary>
        public static bool GunsDisabled =>
            GameManager.Instance?.CurrentGameModeRules.GunsDisabled == true;

        /// <summary>Add ammo to the equipped weapon (ammo pickup), capped at capacity.</summary>
        public void AddAmmo(int amount) => _weapon.AddAmmo(amount);

        // ── Co-op ─────────────────────────────────────────────────────────────
        /// <summary>This hero's player bindings (P1 = keyboard/mouse by default).
        /// Set by the spawner (MainBootstrap / RespawnManager). Null means the
        /// hero reads the legacy single-player Input directly (back-compat).</summary>
        public PlayerInput? PlayerInput { get; set; }

        // ── Subsystems ────────────────────────────────────────────────────────
        protected InputBuffer _inputBuffer = null!;
        protected CharacterJuiceState _juice = new();
        protected Sprite2D? _sprite;
        private Vector2 _baseSpriteScale = Vector2.One;

        /// <summary>When true, <see cref="TakeDamage"/> ignores all incoming damage (used by god mode / i-frame effects).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Current aim direction: updated from mouse every physics tick;
        /// twin-stick/touch input can override via <see cref="SetAimDirection"/>.</summary>
        protected Vector2 _aimDirection = Vector2.Right;

        /// <summary>Override the aim direction (twin-stick / touch input).</summary>
        public void SetAimDirection(Vector2 dir) => _aimDirection = dir;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            _inputBuffer = new InputBuffer();
            AddChild(_inputBuffer);
            _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            if (_sprite != null)
                _baseSpriteScale = _sprite.Scale;
            OnReady();
        }

        protected virtual void OnReady() { }

        protected int _availableDodgeCharges = 1;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // Health regeneration (R2)
            if (Stats.HpRegenPerSec > 0f && CurrentHealth < MaxHealth)
            {
                CurrentHealth = HeroCombatMechanics.ProcessRegen(CurrentHealth, MaxHealth, Stats.HpRegenPerSec, dt);
            }

            HandleInput(dt);
            HandleMovement(dt);

            if (_dodgeCooldown > 0f)
            {
                _dodgeCooldown -= dt;
                if (_dodgeCooldown <= 0f && _availableDodgeCharges < Stats.DodgeCharges)
                {
                    _availableDodgeCharges++;
                    if (_availableDodgeCharges < Stats.DodgeCharges)
                        _dodgeCooldown = DodgeCooldownTotal;
                }
            }
            if (_dodgeTimer > 0f) _dodgeTimer -= dt;
            if (_dodgeTimer <= 0f) _isDodging = false;

            // Weapon system: cooldowns, reload, forcefield timer.
            _weapon.Update(dt);
            if (_weaponSwitchCooldown > 0f) _weaponSwitchCooldown -= dt;
            if (_forcefieldTimer > 0f)
            {
                _forcefieldTimer -= dt;
                if (_forcefieldTimer <= 0f) Invulnerable = false;
            }

            UpdateJuice(dt);
        }

        private void UpdateJuice(float dt)
        {
            if (_sprite == null || !IsInstanceValid(_sprite)) return;

            bool isMoving = Velocity.LengthSquared() > 10f;
            float speedRatio = MoveSpeed > 0f ? Velocity.Length() / MoveSpeed : 0f;
            _juice.Update(dt, isMoving, speedRatio);

            // Aim rotation: face crosshair
            if (_aimDirection != Vector2.Zero)
                _sprite.Rotation = _aimDirection.Angle() + _juice.TiltAngle;

            // Apply squash, stretch, and walk-bob deformation
            var scale = _baseSpriteScale;
            scale.X *= _juice.ScaleX;
            scale.Y *= _juice.ScaleY;
            _sprite.Scale = scale;
            _sprite.Position = new Vector2(0f, _juice.WalkBob);
        }

        // ── Input ──────────────────────────────────────────────────────────────
        private void HandleInput(float dt)
        {
            var pi = PlayerInput;

            // Aim: mouse/crosshair for all players (shared cursor). Gamepad
            // right-stick aim can be wired later once the joypad axis binding
            // is confirmed in this engine binding.
            _aimDirection = (GetGlobalMousePosition() - GlobalPosition).Normalized();

            string a = pi?.Attack ?? "attack";
            string d = pi?.Dodge ?? "dodge";
            string ab = pi?.Ability ?? "ability";

            // Buffer actions on this player's own action names (legacy names when null).
            if (Input.IsActionJustPressed(a))
                _inputBuffer.BufferAction(a);
            if (Input.IsActionJustPressed(d))
                _inputBuffer.BufferAction(d);
            if (Input.IsActionJustPressed(ab))
                _inputBuffer.BufferAction(ab);

            if (_inputBuffer.ConsumeAction(a) || Input.IsActionPressed(a))
            {
                if (!_weapon.HasWeapon || GunsDisabled)
                {
                    Attack(); // class signature attack when no weapon (or guns disabled)
                }
                else
                {
                    if (_weapon.CanFire())
                        FireWeapon();
                    else if (_weapon.Ammo <= 0 && !_weapon.IsReloading)
                        _weapon.Reload(); // auto-reload when empty (Crimsonland)
                    // else: reloading or on cooldown — wait for the weapon
                }
            }
            if (_inputBuffer.ConsumeAction(d) && _availableDodgeCharges > 0) Dodge();
            if (_inputBuffer.ConsumeAction(ab)) UseAbility();
        }

        private void HandleMovement(float dt)
        {
            var pi = PlayerInput;
            string ml = pi?.MoveLeft ?? "move_left";
            string mr = pi?.MoveRight ?? "move_right";
            string mu = pi?.MoveUp ?? "move_up";
            string md = pi?.MoveDown ?? "move_down";

            Vector2 inputDir = Input.GetVector(ml, mr, mu, md);
            float speed = _isDodging ? MoveSpeed * 2.5f : MoveSpeed;
            Velocity = inputDir * speed;
            MoveAndSlide();
        }

        // ── Abstract hero actions ─────────────────────────────────────────────
        public abstract void Attack();
        public abstract void Dodge();
        public abstract void UseAbility();

        // ── Weapon actions ────────────────────────────────────────────────────

        /// <summary>Cycle owned weapons by <paramref name="direction"/> (+1 next, -1 prev).
        /// Wraps around. No-op when only the default weapon is owned.</summary>
        public void CycleWeapon(int direction)
        {
            if (_ownedWeapons.Count <= 1) return;
            var ordered = new List<WeaponId>(_ownedWeapons);
            ordered.Sort((a, b) => a.CompareTo(b));
            int idx = ordered.IndexOf(_weapon.CurrentWeapon);
            if (idx < 0) idx = 0;
            idx = (idx + direction + ordered.Count) % ordered.Count;
            EquipWeapon(ordered[idx]);
        }

        /// <summary>Equip <paramref name="weapon"/> if owned (or the default Pistol).
        /// Refills ammo; equipping None returns to the class signature attack.</summary>
        public void EquipWeapon(WeaponId weapon)
        {
            if (GunsDisabled) return; // Nukefism: no guns allowed
            if (weapon == WeaponId.None)
            {
                _weapon.Unequip();
                return;
            }
            // HashSet.Add is idempotent; no need to Contains-guard first (CA1868).
            _ownedWeapons.Add(weapon);
            _weapon.Equip(weapon, Stats.AmmoCapacityBonus, Stats.ReloadSpeedMult);
            EventBus.Instance?.EmitPowerupCollected($"Weapon:{weapon}");
        }

        /// <summary>Fire the equipped weapon, spawning projectiles through the pool.
        /// Shotguns return multiple pellets; the caller (this node) spawns each.</summary>
        private void FireWeapon()
        {
            var gm = GameManager.Instance;
            var pool = gm?.PoolManager;
            if (pool == null) return;

            Vector2 aim = GetAimDirection();
            if (aim == Vector2.Zero) aim = Vector2.Right;

            var def = _weapon.CurrentDef;
            foreach (var shot in _weapon.Fire(GlobalPosition, aim))
            {
                float damage = shot.Damage
                    * Stats.DamageMult
                    * (shot.Type == DamageType.Kinetic ? Stats.KineticDamageMult : Stats.EnergyDamageMult);
                var data = new ProjectileData(
                    shot.Position, shot.Direction, shot.Speed, damage, shot.Type);

                var projectile = pool.GetProjectile(data);
                if (projectile != null)
                {
                    if (Stats.ProjectilePiercing) projectile.PiercingCount = 1;
                    GetTree().CurrentScene.AddChild(projectile);
                }
            }

            EventBus.Instance?.EmitProjectileHit(GlobalPosition, def.DamageType);
            _juice.ApplyDashStretch(); // recoil juice
        }

        /// <summary>Grant a temporary forcefield (invulnerability) — Crimsonland's
        /// level-up forcefield that protects the player while picking a perk.</summary>
        public void GrantForcefield(float seconds = 3f)
        {
            Invulnerable = true;
            _forcefieldTimer = seconds;
            EventBus.Instance?.EmitPowerupCollected("Forcefield");
        }

        // ── Damage / death ─────────────────────────────────────────────────────
        public virtual void TakeDamage(float amount, DamageType type)
        {
            if (Invulnerable) return; // god mode / invulnerability
            if (_isDodging) return; // i-frames during dodge
            CurrentHealth -= amount;
            _juice.ApplyImpactFlinch();

            // Thorn Armor reflection (R2)
            float reflectDmg = HeroCombatMechanics.CalculateThornReflection(amount, Stats.ThornArmor);
            if (reflectDmg > 0f)
            {
                var grid = GameManager.Instance?.SpatialGrid;
                if (grid != null)
                {
                    int[] hits = grid.QueryRadius(GlobalPosition, 100f, out int count, out bool _);
                    for (int i = 0; i < count; i++)
                    {
                        if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null && enemy.State != EnemyAIState.Dead)
                        {
                            enemy.TakeDamage(reflectDmg, DamageType.Kinetic);
                        }
                    }
                }
            }

            EventBus.Instance.EmitHeroDamaged(amount);
            if (CurrentHealth <= 0f) Die();
        }

        /// <summary>Directly sets current health, clamped between 0 and MaxHealth.</summary>
        public void SetCurrentHealth(float value) => CurrentHealth = Mathf.Clamp(value, 0f, MaxHealth);

        protected virtual void Die()
        {
            var gm = GameManager.Instance;
            int playerIndex = PlayerInput?.PlayerIndex ?? 0;

            bool respawnScheduled = false;
            if (gm != null)
                respawnScheduled = RespawnManager.Instance?.RequestRespawn(playerIndex, GlobalPosition) ?? false;

            // Single-player with no lives left ends the run. Everyone-out is also
            // raised via LivesManager.OnAllOut → GameManager.TriggerGameOver.
            if (!respawnScheduled && LocalCoopInputManager.Instance?.PlayerCount == 1)
                gm?.TriggerGameOver();

            QueueFree();
        }

        // ── Leveling ──────────────────────────────────────────────────────────
        public void AddExperience(int xp)
        {
            Experience += xp;
            int required = LevelProgression.XpRequiredForLevel(Level + 1);
            while (Experience >= required)
            {
                Experience -= required;
                LevelUp();
                required = LevelProgression.XpRequiredForLevel(Level + 1);
            }
        }

        protected virtual void LevelUp()
        {
            Level++;
            EventBus.Instance.EmitLevelUp(Level);

            // Crimsonland level-up: brief forcefield (invulnerability while
            // choosing a perk) + a knockback ring that pushes nearby enemies
            // away, giving the player breathing room to pick a perk.
            GrantForcefield(3f);
            ApplyLevelUpRing();
        }

        /// <summary>Crimsonland level-up ring: push nearby enemies back for
        /// breathing room while the perk menu is open.</summary>
        protected static void ApplyLevelUpRing()
        {
            var gm = GameManager.Instance;
            var grid = gm?.SpatialGrid;
            var hero = gm?.ActiveHero;
            if (grid == null || hero == null) return;

            const float radius = 220f;
            const float force = 700f;
            int[] hits = grid.QueryRadius(hero.GlobalPosition, radius, out int count, out bool _);
            for (int i = 0; i < count; i++)
            {
                if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                    && enemy.State != EnemyAIState.Dead)
                {
                    Vector2 dir = (enemy.GlobalPosition - hero.GlobalPosition);
                    if (dir == Vector2.Zero) dir = Vector2.Right;
                    enemy.ApplyKnockback(dir.Normalized(), force);
                }
            }
        }

        // ── Utility ───────────────────────────────────────────────────────────
        protected Vector2 GetAimDirection()
        {
            return _aimDirection;
        }

        /// <summary>Heals hero by <paramref name="amount"/>, clamped to MaxHealth.</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        protected void StartDodge(float duration = 0.25f, float cooldown = 0.8f)
        {
            _isDodging = true;
            _dodgeTimer = duration;
            _dodgeCooldown = cooldown;
            DodgeCooldownTotal = cooldown;
            if (_availableDodgeCharges > 0)
                _availableDodgeCharges--;
            _juice.ApplyDashStretch();
        }
    }
}
