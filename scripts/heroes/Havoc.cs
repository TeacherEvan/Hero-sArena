using Godot;

namespace HeroArena
{
    /// <summary>
    /// Havoc – Demolitionist class. Wields Cluster Rocket (heavy ordnance explosive blasts).
    /// Unique power: Orbital Barrage (heavy artillery carpet bomb on target location).
    /// </summary>
    public partial class Havoc : HeroBase
    {
        private const float ROCKET_DAMAGE = 65f;
        private const float ROCKET_SPEED = 380f;
        private const float ROCKET_COOLDOWN = 0.55f;
        private const float BARRAGE_DAMAGE = 160f;
        private const float BARRAGE_RADIUS = 260f;
        private const float BARRAGE_COOLDOWN = 30f;

        private float _rocketCooldown = 0f;
        private float _barrageCooldown = 0f;

        protected override void OnReady()
        {
            MaxHealth = 135f;
            MoveSpeed = 180f;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_rocketCooldown > 0f) _rocketCooldown -= dt;
            if (_barrageCooldown > 0f) _barrageCooldown -= dt;
        }

        public override void Attack()
        {
            if (_rocketCooldown > 0f) return;
            FireRocket();
            _rocketCooldown = ROCKET_COOLDOWN * Stats.AttackCooldownMult;
        }

        public override void Dodge()
        {
            StartDodge(0.28f, 0.9f * Stats.DodgeCooldownMult);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ExplosionScorch, 70f);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Explosive);
        }

        public override void UseAbility()
        {
            if (_barrageCooldown > 0f) return;
            ActivateOrbitalBarrage();
        }

        private void FireRocket()
        {
            var pool = GameManager.Instance.PoolManager;
            if (pool == null) return;

            Vector2 baseDir = GetAimDirection();
            float dmg = LevelProgression.CalcKineticDamage(ROCKET_DAMAGE, Level) * Stats.DamageMult;
            var data = new ProjectileData(GlobalPosition, baseDir, ROCKET_SPEED, dmg, DamageType.Explosive);
            var p = pool.GetProjectile(data);
            if (p != null)
            {
                p.PiercingCount = Stats.ProjectilePiercing ? 3 : 0;
            }

            int extraRockets = Stats.ProjectileCountBonus;
            for (int i = 0; i < extraRockets; i++)
            {
                float spreadAngle = ((i + 1) % 2 == 1 ? 1 : -1) * 0.15f * ((i / 2) + 1);
                Vector2 spreadDir = baseDir.Rotated(spreadAngle);
                var spreadData = new ProjectileData(GlobalPosition, spreadDir, ROCKET_SPEED, dmg, DamageType.Explosive);
                var extra = pool.GetProjectile(spreadData);
                if (extra != null)
                {
                    extra.PiercingCount = Stats.ProjectilePiercing ? 3 : 0;
                }
            }

            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Explosive);
        }

        private void ActivateOrbitalBarrage()
        {
            _barrageCooldown = BARRAGE_COOLDOWN;
            Vector2 targetPos = GetGlobalMousePosition();
            float damage = LevelProgression.CalcKineticDamage(BARRAGE_DAMAGE, Level)
                * Stats.DamageMult * Stats.AoeRadiusMult;
            float radius = BARRAGE_RADIUS * Stats.AoeRadiusMult;

            var grid = GameManager.Instance.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(targetPos, radius, out int count);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(damage, DamageType.Explosive);
                    }
                }
            }

            EventBus.Instance.EmitProjectileHit(targetPos, DamageType.Explosive);
            EventBus.Instance.EmitDecalRequested(targetPos, DecalType.CraterMark, radius);
            EventBus.Instance.EmitDecalRequested(targetPos, DecalType.ExplosionScorch, radius * 0.8f);
            EventBus.Instance.EmitPowerupCollected("OrbitalBarrage");
        }
    }
}
