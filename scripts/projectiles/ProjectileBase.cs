using Godot;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Pooled projectile node. Activated/deactivated by ObjectPoolManager.
    /// Pre-allocated – no runtime instantiation.
    /// </summary>
    public partial class ProjectileBase : Area2D
    {
        public const float MAX_LIFETIME = 5f;

        public bool IsActive { get; private set; } = false;
        public Vector2 Direction { get; private set; }
        public float Speed { get; private set; }
        public float Damage { get; private set; }
        public DamageType DamageType { get; private set; }
        public int PiercingCount { get; set; } = 0;
        public int PoolIndex { get; private set; }

        // Collision shape parked while pooled so idle pool members stay out of the
        // physics broadphase (see Deactivate/Activate).
        private CollisionShape2D? _shape;

        private float _lifetime = 0f;

        // Per-type texture cache (loaded once)
        private static readonly Dictionary<DamageType, Texture2D> _textures = new();
        private static bool _texturesLoaded = false;

        public override void _Ready()
        {
            BodyEntered += OnBodyEntered;
            Visible = false;
            SetProcess(false);
            SetPhysicsProcess(false);
        }

        public void Activate(ProjectileData data, int poolIndex)
        {
            GlobalPosition = data.Position;
            // Godot 4 returns Vector2.Zero (not NaN) for Vector2.Zero.Normalized()
            // (evidenced by AuditRegressionTests.ZeroDirection_Normalized_Premise),
            // so no NaN guard is required; direction is used as-is.
            Direction = data.Direction.Normalized();
            Speed = data.Speed;
            Damage = data.Damage;
            DamageType = data.Type;
            PoolIndex = poolIndex;
            _lifetime = 0f;
            IsActive = true;
            Visible = true;
            SetPhysicsProcess(true);
            // Re-enable collision monitoring and the parked collision shape.
            Monitoring = true;
            Monitorable = true;
            if (_shape != null) _shape.Disabled = false;

            // Load per-type texture on first activation
            if (!_texturesLoaded)
            {
                foreach (var kvp in ProjectileSprites.Paths)
                {
                    _textures[kvp.Key] = GD.Load<Texture2D>(kvp.Value);
                }
                _texturesLoaded = true;
            }

            if (_textures.TryGetValue(DamageType, out var tex))
            {
                var sprite = GetNode<Sprite2D>("Sprite2D");
                sprite.Texture = tex;
            }
        }

        public void Deactivate()
        {
            IsActive = false;
            Visible = false;
            SetPhysicsProcess(false);
            Monitoring = false;
            Monitorable = false;
            // Park the collision shape: registered shapes put every pooled instance
            // into the physics broadphase, which made pre-allocation super-linear
            // and stalled the first frames after boot.
            _shape ??= GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (_shape != null) _shape.Disabled = true;
            // Reset per-shot state so a reused projectile doesn't carry the
            // previous shot's PiercingCount. (Audit finding F-51.)
            PiercingCount = 0;
        }

        /// <summary>Preloads every per-type projectile texture at boot so the first
        /// activation of each <see cref="DamageType"/> performs no synchronous GD.Load.</summary>
        public static void PreloadTextures()
        {
            if (_texturesLoaded) return;
            foreach (var kvp in ProjectileSprites.Paths)
            {
                _textures[kvp.Key] = GD.Load<Texture2D>(kvp.Value);
            }
            _texturesLoaded = true;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _lifetime += dt;
            if (_lifetime >= MAX_LIFETIME) { Expire(); return; }

            GlobalPosition += Direction * Speed * dt;
        }

        private void OnBodyEntered(Node body)
        {
            if (!IsActive) return;

            var hero = GameManager.Instance?.ActiveHero;

            if (body is EnemyBase enemy)
            {
                enemy.TakeDamage(Damage, DamageType);
                EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType);

                // On-hit status effects (R2)
                if (hero != null)
                {
                    if (hero.Stats.SlowOnHit)
                        enemy.ApplySlow(2.5f, 0.30f);
                    if (hero.Stats.BurnOnHit)
                        enemy.ApplyBurn(3.0f, 5.0f);

                    // Life steal (R2)
                    float heal = HeroCombatMechanics.CalculateLifeSteal(Damage, hero.Stats.LifeStealPct);
                    if (heal > 0f)
                        hero.Heal(heal);
                }

                // Explosive rounds AoE (R2)
                if (hero != null && hero.Stats.ProjectileExplosive)
                {
                    TriggerExplosion(hero);
                }

                if (PiercingCount > 0)
                {
                    PiercingCount--;
                    return; // Keep going
                }
                OnHit(body);
            }
            else if (body is HeroBase targetHero)
            {
                targetHero.TakeDamage(Damage, DamageType);
                EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType);
                OnHit(body);
            }
            else if (body is DestructibleObject dest)
            {
                dest.TakeDamage(Damage);
                EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType);
                if (hero != null && hero.Stats.ProjectileExplosive)
                {
                    TriggerExplosion(hero);
                }
                OnHit(body);
            }
        }

        private void TriggerExplosion(HeroBase hero)
        {
            var grid = GameManager.Instance?.SpatialGrid;
            if (grid == null) return;

            float expDmg = Damage * HeroCombatMechanics.EXPLOSIVE_ROUNDS_DAMAGE_RATIO;
            float radius = HeroCombatMechanics.EXPLOSIVE_ROUNDS_RADIUS * hero.Stats.AoeRadiusMult;
            int[] hits = grid.QueryRadius(GlobalPosition, radius, out int count, out bool _);
            for (int i = 0; i < count; i++)
            {
                if (EnemyBase.TryGetById(hits[i], out var splashEnemy) && splashEnemy != null
                    && splashEnemy.State != EnemyAIState.Dead)
                {
                    splashEnemy.TakeDamage(expDmg, DamageType.Energy);
                }
            }
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ScorchMark, radius);
        }

        private void OnHit(Node? _)
        {
            GameManager.Instance?.PoolManager?.ReturnProjectile(this);
        }

        /// <summary>Expires a projectile that reached max lifetime without hitting anything.</summary>
        private void Expire()
        {
            GameManager.Instance?.PoolManager?.ReturnProjectile(this);
        }
    }
}