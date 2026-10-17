using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Synapse – Intelligence class. Wields Neural Lasers (chaining beams, up to 5 targets).
    /// Unique power: Overclock (mind-control 50 nearest enemies for 10s).
    /// </summary>
    public partial class Synapse : HeroBase
    {
        private const float LASER_DAMAGE = 35f;
        private const float LASER_RANGE = 400f;
        private const int MAX_CHAIN = 5;
        private const float FIRE_COOLDOWN = 0.3f;
        private const float OVERCLOCK_DURATION = 10f;
        private const float OVERCLOCK_COOLDOWN = 45f;
        private const int MAX_MIND_CONTROL = 50;
        private const float OVERCLOCK_RADIUS = 1200f;

        private float _fireCooldown = 0f;
        private float _overclockTimer = 0f;
        private float _overclockCooldown = 0f;

        protected override void OnReady()
        {
            MaxHealth = 90f;
            MoveSpeed = 200f;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_fireCooldown > 0f) _fireCooldown -= dt;
            if (_overclockCooldown > 0f) _overclockCooldown -= dt;
            if (_overclockTimer > 0f) _overclockTimer -= dt;
        }

        public override void Attack()
        {
            if (_fireCooldown > 0f) return;
            FireChainLaser();
            _fireCooldown = FIRE_COOLDOWN * Stats.AttackCooldownMult;
        }

        public override void Dodge()
        {
            StartDodge(0.25f, 0.8f * Stats.DodgeCooldownMult);
        }

        public override void UseAbility()
        {
            if (_overclockCooldown > 0f) return;
            ActivateOverclock();
        }

        private void FireChainLaser()
        {
            var grid = GameManager.Instance.SpatialGrid;
            if (grid == null) return;

            float dmg = LevelProgression.CalcEnergyDamage(LASER_DAMAGE, Level)
                * Stats.DamageMult * Stats.EnergyDamageMult;
            int[] hits = grid.QueryRadius(GlobalPosition, LASER_RANGE * Stats.AoeRadiusMult, out int count);
            int chained = 0;
            int chainLimit = MAX_CHAIN + Stats.ProjectileCountBonus;
            for (int i = 0; i < count && chained < chainLimit; i++)
            {
                if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                    && enemy.State != EnemyAIState.Dead)
                {
                    enemy.TakeDamage(dmg, DamageType.Energy);
                    chained++;
                }
            }
            if (chained > 0)
            {
                EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Energy);
            }
        }

        private void ActivateOverclock()
        {
            _overclockTimer = OVERCLOCK_DURATION;
            _overclockCooldown = OVERCLOCK_COOLDOWN;
            EventBus.Instance.EmitPowerupCollected("Overclock");

            var grid = GameManager.Instance.SpatialGrid;
            if (grid == null) return;

            // Candidate screening happens here (live enemies only); the
            // nearest-N ordering and cap rules live in the pure
            // MindControlTargets helper (covered headlessly in tests).
            int[] hits = grid.QueryRadius(GlobalPosition, OVERCLOCK_RADIUS, out int count);
            var candidates = new List<EnemyBase>();
            var distancesSq = new List<float>();
            for (int i = 0; i < count; i++)
            {
                if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                    && enemy.State != EnemyAIState.Dead)
                {
                    candidates.Add(enemy);
                    distancesSq.Add(GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition));
                }
            }

            foreach (int index in MindControlTargets.SelectNearest(distancesSq, MAX_MIND_CONTROL))
            {
                candidates[index].ApplyMindControl(OVERCLOCK_DURATION);
            }
        }
    }
}
