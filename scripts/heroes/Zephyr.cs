using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Zephyr – Agility class. Wields Plasma Blades (piercing projectiles).
    /// Unique power: Hyper-Dash (0.3× enemy time scale, 300% speed for 2s).
    /// </summary>
    public partial class Zephyr : HeroBase
    {
        private const float BLADE_DAMAGE = 25f;
        private const float BLADE_SPEED = 500f;
        private const float FIRE_COOLDOWN = 0.12f;
        private const float HYPER_DASH_DURATION = 2f;
        private const float HYPER_DASH_COOLDOWN = 20f;

        private float _fireCooldown = 0f;
        private float _hyperDashTimer = 0f;
        private float _hyperDashCooldown = 0f;

        protected override void OnReady()
        {
            MaxHealth = 80f;
            MoveSpeed = 260f;
            CurrentHealth = MaxHealth;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_fireCooldown > 0f) _fireCooldown -= dt;
            if (_hyperDashCooldown > 0f) _hyperDashCooldown -= dt;
            if (_hyperDashTimer > 0f)
            {
                _hyperDashTimer -= dt;
                if (_hyperDashTimer <= 0f)
                {
                    Engine.TimeScale = 1.0;
                    MoveSpeed = 260f;
                }
            }
        }

        public override void Attack()
        {
            if (_fireCooldown > 0f) return;
            FireBlades();
            _fireCooldown = FIRE_COOLDOWN;
        }

        public override void Dodge()
        {
            StartDodge(0.2f, 0.6f);
        }

        public override void UseAbility()
        {
            if (_hyperDashCooldown > 0f) return;
            ActivateHyperDash();
        }

        private void FireBlades()
        {
            var pool = GameManager.Instance.PoolManager;
            if (pool == null) return;
            Vector2 dir = GetAimDirection();
            float dmg = _levelProg.CalcKineticDamage(BLADE_DAMAGE, Level);
            var p = pool.GetProjectile(GlobalPosition, dir, BLADE_SPEED, dmg, DamageType.Kinetic);
            if (p != null) p.PiercingCount = 3;
        }

        private void ActivateHyperDash()
        {
            _hyperDashTimer = HYPER_DASH_DURATION;
            _hyperDashCooldown = HYPER_DASH_COOLDOWN;
            Engine.TimeScale = 0.3;
            MoveSpeed = 260f * 3f;
            EventBus.Instance.EmitPowerupCollected("HyperDash");
        }
    }
}
