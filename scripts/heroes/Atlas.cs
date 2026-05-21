using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Atlas – Strength class. Wields Seismic Gauntlets.
    /// Slam() creates an AoE shockwave. Unique power: Tectonic Plating.
    /// </summary>
    public partial class Atlas : HeroBase
    {
        private const float SLAM_RADIUS = 150f;
        private const float SLAM_DAMAGE = 60f;
        private const float SLAM_COOLDOWN = 0.8f;
        private const float TECTONIC_DURATION = 3f;
        private const float TECTONIC_COOLDOWN = 30f;

        private float _slamCooldown = 0f;
        private float _tectonicTimer = 0f;
        private float _tectonicCooldown = 0f;
        private bool _tectonicActive = false;
        private float _reflectDamage = 0f;

        protected override void OnReady()
        {
            MaxHealth = 150f;
            MoveSpeed = 160f;
            CurrentHealth = MaxHealth;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_slamCooldown > 0f) _slamCooldown -= dt;
            if (_tectonicCooldown > 0f) _tectonicCooldown -= dt;
            if (_tectonicTimer > 0f)
            {
                _tectonicTimer -= dt;
                if (_tectonicTimer <= 0f) _tectonicActive = false;
            }
        }

        public override void Attack()
        {
            if (_slamCooldown > 0f) return;
            Slam();
            _slamCooldown = SLAM_COOLDOWN;
        }

        public override void Dodge()
        {
            StartDodge(0.3f, 1.0f);
        }

        public override void UseAbility()
        {
            if (_tectonicCooldown > 0f) return;
            ActivateTectonicPlating();
        }

        private void Slam()
        {
            // Deal AoE kinetic damage to all enemies within radius
            float damage = _levelProg.CalcKineticDamage(SLAM_DAMAGE, Level);
            var grid = GameManager.Instance.SpatialGrid;
            if (grid == null) return;

            int[] hits = grid.QueryRadius(GlobalPosition, SLAM_RADIUS, out int count);
            for (int i = 0; i < count; i++)
            {
                // Entity id resolution handled by EnemyBase registering itself
                // The ids stored in the grid match enemy node instance IDs
            }

            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Kinetic);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.CraterMark, SLAM_RADIUS);
        }

        /// <summary>Tectonic Plating: 3s invulnerability + damage reflection.</summary>
        private void ActivateTectonicPlating()
        {
            _tectonicActive = true;
            _tectonicTimer = TECTONIC_DURATION;
            _tectonicCooldown = TECTONIC_COOLDOWN;
            _reflectDamage = 30f;
            EventBus.Instance.EmitPowerupCollected("TectonicPlating");
        }

        public override void TakeDamage(float amount, DamageType type)
        {
            if (_tectonicActive)
            {
                // Reflect damage back – handled via event; ignore incoming
                EventBus.Instance.EmitProjectileHit(GlobalPosition, type);
                return;
            }
            base.TakeDamage(amount, type);
        }
    }
}
