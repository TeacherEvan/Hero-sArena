using Godot;

namespace HeroArena
{
    /// <summary>
    /// Toxis – Alchemist class. Wields Acid Lob (corrosive piercing projectiles).
    /// Unique power: Caustic Eruption (radial 12-way burst + chemical saturation).
    /// </summary>
    public partial class Toxis : HeroBase
    {
        private const float ACID_DAMAGE = 30f;
        private const float ACID_SPEED = 420f;
        private const float ACID_COOLDOWN = 0.22f;
        private const float ERUPTION_DAMAGE = 80f;
        private const float ERUPTION_RADIUS = 240f;
        private const float ERUPTION_COOLDOWN = 25f;

        private float _acidCooldown = 0f;
        private float _eruptionCooldown = 0f;

        protected override void OnReady()
        {
            MaxHealth = 95f;
            MoveSpeed = 240f;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_acidCooldown > 0f) _acidCooldown -= dt;
            if (_eruptionCooldown > 0f) _eruptionCooldown -= dt;
        }

        public override void Attack()
        {
            if (_acidCooldown > 0f) return;
            LobAcid();
            _acidCooldown = ACID_COOLDOWN * Stats.AttackCooldownMult;
        }

        public override void Dodge()
        {
            StartDodge(0.25f, 0.75f * Stats.DodgeCooldownMult);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.AcidPool, 80f);
        }

        public override void UseAbility()
        {
            if (_eruptionCooldown > 0f) return;
            ActivateCausticEruption();
        }

        private void LobAcid()
        {
            var pool = GameManager.Instance.PoolManager;
            if (pool == null) return;

            Vector2 baseDir = GetAimDirection();
            float dmg = LevelProgression.CalcKineticDamage(ACID_DAMAGE, Level) * Stats.DamageMult;
            var data = new ProjectileData(GlobalPosition, baseDir, ACID_SPEED, dmg, DamageType.Acid);
            var p = pool.GetProjectile(data);
            if (p != null)
            {
                p.PiercingCount = Stats.ProjectilePiercing ? 4 : 2;
            }

            int extraBolts = Stats.ProjectileCountBonus;
            for (int i = 0; i < extraBolts; i++)
            {
                float spreadAngle = ((i + 1) % 2 == 1 ? 1 : -1) * 0.12f * ((i / 2) + 1);
                Vector2 spreadDir = baseDir.Rotated(spreadAngle);
                var spreadData = new ProjectileData(GlobalPosition, spreadDir, ACID_SPEED, dmg, DamageType.Acid);
                var extra = pool.GetProjectile(spreadData);
                if (extra != null)
                {
                    extra.PiercingCount = Stats.ProjectilePiercing ? 4 : 2;
                }
            }

            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Acid);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.AcidPool, 40f);
        }

        private void ActivateCausticEruption()
        {
            _eruptionCooldown = ERUPTION_COOLDOWN;
            float damage = LevelProgression.CalcKineticDamage(ERUPTION_DAMAGE, Level)
                * Stats.DamageMult * Stats.AoeRadiusMult;
            float radius = ERUPTION_RADIUS * Stats.AoeRadiusMult;

            var pool = GameManager.Instance.PoolManager;
            if (pool != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = Mathf.DegToRad(i * 30f);
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var data = new ProjectileData(GlobalPosition, dir, ACID_SPEED * 0.9f, damage * 0.5f, DamageType.Acid);
                    var p = pool.GetProjectile(data);
                    if (p != null) p.PiercingCount = 3;
                }
            }

            var grid = GameManager.Instance.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition, radius, out int count);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(damage, DamageType.Acid);
                    }
                }
            }

            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Acid);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.AcidPool, radius);
            EventBus.Instance.EmitPowerupCollected("CausticEruption");
        }
    }
}
