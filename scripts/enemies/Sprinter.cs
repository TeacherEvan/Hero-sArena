using Godot;

namespace HeroArena
{
    public enum SprinterState { Spawning, SwarmApproach, FlankManeuver, Lunge }

    /// <summary>Sprinter – fast flanking enemy with lunge attack.</summary>
    public partial class Sprinter : EnemyBase
    {
        private SprinterState _aiState = SprinterState.Spawning;
        private float _stateTimer = 0f;
        private Vector2 _flankTarget = Vector2.Zero;

        protected override void OnSpawn()
        {
            MaxHealth = 35f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 220f;
            Damage = 15f;
            ExpValue = 15;
            _stateTimer = 0.5f; // spawning delay
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            _stateTimer -= 0.1f;
            float dist = DistanceToHero();

            switch (_aiState)
            {
                case SprinterState.Spawning:
                    if (_stateTimer <= 0f) _aiState = SprinterState.SwarmApproach;
                    break;
                case SprinterState.SwarmApproach:
                    if (dist < 250f) { _aiState = SprinterState.FlankManeuver; SetFlankTarget(); }
                    break;
                case SprinterState.FlankManeuver:
                    if (dist < 80f) _aiState = SprinterState.Lunge;
                    if (dist > 350f) _aiState = SprinterState.SwarmApproach;
                    break;
                case SprinterState.Lunge:
                    if (dist > 120f) _aiState = SprinterState.SwarmApproach;
                    break;
            }
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead || _hero == null) return;
            Vector2 dir = _aiState switch
            {
                SprinterState.FlankManeuver => (_flankTarget - GlobalPosition).Normalized(),
                SprinterState.Lunge => (_hero.GlobalPosition - GlobalPosition).Normalized(),
                _ => GameManager.Instance.FlowField?.GetFlowDirection(GlobalPosition)
                     ?? (_hero.GlobalPosition - GlobalPosition).Normalized()
            };
            float speed = _aiState == SprinterState.Lunge ? MoveSpeed * 2f : MoveSpeed;
            Velocity = dir * speed;
            MoveAndSlide();
        }

        private void SetFlankTarget()
        {
            if (_hero == null) return;
            Vector2 perp = (_hero.GlobalPosition - GlobalPosition).Normalized().Rotated(Mathf.Pi * 0.5f);
            _flankTarget = _hero.GlobalPosition + perp * 100f;
        }
    }
}
