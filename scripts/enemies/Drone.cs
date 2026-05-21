using Godot;

namespace HeroArena
{
    /// <summary>Drone – low HP, direct-line chaser, basic melee.</summary>
    public partial class Drone : EnemyBase
    {
        protected override void OnSpawn()
        {
            MaxHealth = 20f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 130f;
            Damage = 8f;
            ExpValue = 5;
            State = EnemyAIState.Chase;
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            State = DistanceToHero() < 24f ? EnemyAIState.Attack : EnemyAIState.Chase;
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead || _hero == null) return;
            // Drones ignore flow field - move in direct lines
            Vector2 dir = (_hero.GlobalPosition - GlobalPosition).Normalized();
            Velocity = dir * MoveSpeed;
            MoveAndSlide();
        }
    }
}
