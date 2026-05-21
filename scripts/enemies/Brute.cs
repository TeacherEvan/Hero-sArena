using Godot;

namespace HeroArena
{
    /// <summary>Brute – high HP, slow, immune to knockback. Has a charge attack.</summary>
    public partial class Brute : EnemyBase
    {
        private const float CHARGE_RANGE = 300f;
        private const float CHARGE_SPEED = 400f;
        private const float CHARGE_COOLDOWN = 4f;

        private float _chargeCooldown = 0f;
        private bool _isCharging = false;
        private float _chargeTimer = 0f;

        protected override void OnSpawn()
        {
            MaxHealth = 400f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 70f;
            Damage = 40f;
            ExpValue = 50;
            State = EnemyAIState.Chase;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_chargeCooldown > 0f) _chargeCooldown -= dt;
            if (_chargeTimer > 0f)
            {
                _chargeTimer -= dt;
                if (_chargeTimer <= 0f) _isCharging = false;
            }
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            float dist = DistanceToHero();
            if (dist < CHARGE_RANGE && _chargeCooldown <= 0f)
                StartCharge();
        }

        private void StartCharge()
        {
            _isCharging = true;
            _chargeTimer = 0.5f;
            _chargeCooldown = CHARGE_COOLDOWN;
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead || _hero == null) return;
            if (_isCharging)
            {
                Vector2 dir = (_hero.GlobalPosition - GlobalPosition).Normalized();
                Velocity = dir * CHARGE_SPEED;
            }
            else
            {
                var flow = GameManager.Instance.FlowField;
                Vector2 dir = flow != null
                    ? flow.GetFlowDirection(GlobalPosition)
                    : (_hero.GlobalPosition - GlobalPosition).Normalized();
                Velocity = dir * MoveSpeed;
            }
            MoveAndSlide();
        }

        // Brutes are immune to knockback - override if knockback logic added
        public override void TakeDamage(float amount, DamageType type = DamageType.Kinetic)
        {
            base.TakeDamage(amount, type); // no knockback modifier here
        }
    }
}
