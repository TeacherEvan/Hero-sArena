using Godot;
using System;

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

        private float _fireCooldown = 0f;
        private float _overclockTimer = 0f;
        private float _overclockCooldown = 0f;

        protected override void OnReady()
        {
            MaxHealth = 90f;
            MoveSpeed = 200f;
            CurrentHealth = MaxHealth;
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
            _fireCooldown = FIRE_COOLDOWN;
        }

        public override void Dodge()
        {
            StartDodge(0.25f, 0.8f);
        }

        public override void UseAbility()
        {
            if (_overclockCooldown > 0f) return;
            ActivateOverclock();
        }

        private void FireChainLaser()
        {
            // Chain laser hits up to MAX_CHAIN targets via spatial grid
            var grid = GameManager.Instance.SpatialGrid;
            if (grid == null) return;

            float dmg = _levelProg.CalcEnergyDamage(LASER_DAMAGE, Level);
            int[] hits = grid.QueryRadius(GlobalPosition, LASER_RANGE, out int count);
            int chained = 0;
            for (int i = 0; i < count && chained < MAX_CHAIN; i++)
            {
                // Damage resolution done via EnemyBase.TakeDamage once entity lookup is wired
                chained++;
                EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Energy);
            }
        }

        private void ActivateOverclock()
        {
            _overclockTimer = OVERCLOCK_DURATION;
            _overclockCooldown = OVERCLOCK_COOLDOWN;
            EventBus.Instance.EmitPowerupCollected("Overclock");
            // Mind control nearest 50 enemies - flag them via a component/signal
        }
    }
}
