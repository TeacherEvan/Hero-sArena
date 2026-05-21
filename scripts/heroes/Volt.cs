using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Volt – Speed class. Wields Arc Lightning (forks to 8 enemies).
    /// Unique power: Static Field (EMP zone, vaporizes projectiles, stuns 3s).
    /// </summary>
    public partial class Volt : HeroBase
    {
        private const float LIGHTNING_DAMAGE = 20f;
        private const float LIGHTNING_RANGE = 350f;
        private const int MAX_FORKS = 8;
        private const float FIRE_COOLDOWN = 0.15f;
        private const float STATIC_FIELD_RADIUS = 250f;
        private const float STATIC_FIELD_DURATION = 3f;
        private const float STATIC_FIELD_COOLDOWN = 25f;

        private float _fireCooldown = 0f;
        private float _staticFieldCooldown = 0f;

        protected override void OnReady()
        {
            MaxHealth = 85f;
            MoveSpeed = 300f;
            CurrentHealth = MaxHealth;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_fireCooldown > 0f) _fireCooldown -= dt;
            if (_staticFieldCooldown > 0f) _staticFieldCooldown -= dt;
        }

        public override void Attack()
        {
            if (_fireCooldown > 0f) return;
            FireArcLightning();
            _fireCooldown = FIRE_COOLDOWN;
        }

        public override void Dodge()
        {
            StartDodge(0.18f, 0.5f);
        }

        public override void UseAbility()
        {
            if (_staticFieldCooldown > 0f) return;
            ActivateStaticField();
        }

        private void FireArcLightning()
        {
            var grid = GameManager.Instance.SpatialGrid;
            if (grid == null) return;

            float dmg = _levelProg.CalcEnergyDamage(LIGHTNING_DAMAGE, Level);
            int[] hits = grid.QueryRadius(GlobalPosition, LIGHTNING_RANGE, out int count);
            int forked = 0;
            for (int i = 0; i < count && forked < MAX_FORKS; i++)
            {
                forked++;
                EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Lightning);
            }
        }

        private void ActivateStaticField()
        {
            _staticFieldCooldown = STATIC_FIELD_COOLDOWN;
            // Emit event so enemy/projectile systems can react
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Lightning);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ScorchMark, STATIC_FIELD_RADIUS);
            EventBus.Instance.EmitPowerupCollected("StaticField");
        }
    }
}
