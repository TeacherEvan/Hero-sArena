using Godot;

namespace HeroArena
{
    /// <summary>
    /// Ignis – Pyromancer class. Wields Flame Stream (rapid fire stream of incendiary bolts).
    /// Unique power: Inferno Nova (massive 360-degree thermal shockwave).
    /// </summary>
    public partial class Ignis : HeroBase
    {
        private const float FLAME_DAMAGE = 18f;
        private const float FLAME_SPEED = 480f;
        private const float FIRE_COOLDOWN = 0.14f;
        private const float NOVA_DAMAGE = 100f;
        private const float NOVA_RADIUS = 280f;
        private const float NOVA_COOLDOWN = 22f;

        private float _fireCooldown = 0f;
        private float _novaCooldown = 0f;

        protected override void OnReady()
        {
            MaxHealth = 110f;
            MoveSpeed = 220f;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_fireCooldown > 0f) _fireCooldown -= dt;
            if (_novaCooldown > 0f) _novaCooldown -= dt;
        }

        public override void Attack()
        {
            if (_fireCooldown > 0f) return;
            FireStream();
            _fireCooldown = FIRE_COOLDOWN * Stats.AttackCooldownMult;
        }

        public override void Dodge()
        {
            StartDodge(0.22f, 0.7f * Stats.DodgeCooldownMult);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ScorchMark, 60f);
        }

        public override void UseAbility()
        {
            if (_novaCooldown > 0f) return;
            ActivateInfernoNova();
        }

        private void FireStream()
        {
            var pool = GameManager.Instance.PoolManager;
            if (pool == null) return;

            Vector2 baseDir = GetAimDirection();
            float dmg = LevelProgression.CalcKineticDamage(FLAME_DAMAGE, Level) * Stats.DamageMult;
            var data = new ProjectileData(GlobalPosition, baseDir, FLAME_SPEED, dmg, DamageType.Fire);
            var p = pool.GetProjectile(data);
            if (p != null)
            {
                p.PiercingCount = Stats.ProjectilePiercing ? 2 : 0;
            }

            int extraBolts = Stats.ProjectileCountBonus;
            for (int i = 0; i < extraBolts; i++)
            {
                float spreadAngle = ((i + 1) % 2 == 1 ? 1 : -1) * 0.08f * ((i / 2) + 1);
                Vector2 spreadDir = baseDir.Rotated(spreadAngle);
                var spreadData = new ProjectileData(GlobalPosition, spreadDir, FLAME_SPEED, dmg, DamageType.Fire);
                var extra = pool.GetProjectile(spreadData);
                if (extra != null)
                {
                    extra.PiercingCount = Stats.ProjectilePiercing ? 2 : 0;
                }
            }

            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Fire);
        }

        private void ActivateInfernoNova()
        {
            _novaCooldown = NOVA_COOLDOWN;
            float damage = LevelProgression.CalcKineticDamage(NOVA_DAMAGE, Level)
                * Stats.DamageMult * Stats.AoeRadiusMult;
            float radius = NOVA_RADIUS * Stats.AoeRadiusMult;

            var grid = GameManager.Instance.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition, radius, out int count);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(damage, DamageType.Fire);
                    }
                }
            }

            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Fire);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ScorchMark, radius);
            EventBus.Instance.EmitPowerupCollected("InfernoNova");
        }
    }
}
