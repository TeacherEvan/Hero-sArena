using Godot;

namespace HeroArena
{
    /// <summary>Drone – low HP, direct-line chaser, basic contact damage.</summary>
    public partial class Drone : EnemyBase
    {
        private const float MELEE_RANGE = 24f;
        private const float MELEE_COOLDOWN = 1.0f;
        private float _meleeTimer = 0f;

        protected override void OnSpawn()
        {
            MaxHealth = 20f;
            MoveSpeed = 130f;
            Damage = 8f;
            ExpValue = 5;
            State = EnemyAIState.Chase;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            if (_meleeTimer > 0f) _meleeTimer -= (float)delta;
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            State = DistanceSquaredToHero() < MELEE_RANGE * MELEE_RANGE
                ? EnemyAIState.Attack : EnemyAIState.Chase;
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead || _hero == null) return;
            if (State == EnemyAIState.Attack)
            {
                if (_meleeTimer <= 0f)
                {
                    _hero.TakeDamage(Damage, DamageType.Kinetic);
                    _meleeTimer = MELEE_COOLDOWN;
                }
                return;
            }
            // Drones ignore flow field - move in direct lines
            Vector2 dir = (_hero.GlobalPosition - GlobalPosition).Normalized();
            Velocity = dir * MoveSpeed;
            MoveAndSlide();
        }
    }
}
