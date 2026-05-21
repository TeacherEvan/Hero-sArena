using Godot;

namespace HeroArena
{
    /// <summary>Parasite – tiny, erratic swarm movement. Attaches to player to slow them.</summary>
    public partial class Parasite : EnemyBase
    {
        private const float ATTACH_RANGE = 20f;
        private const float SLOW_AMOUNT = 0.4f; // 40% slow
        private const float ERRATIC_STRENGTH = 80f;

        private bool _attached = false;
        private float _erraticTimer = 0f;
        private Vector2 _erraticDir = Vector2.Right;
        private readonly RandomNumberGenerator _rng = new();

        protected override void OnSpawn()
        {
            MaxHealth = 10f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 180f;
            Damage = 5f;
            ExpValue = 8;
            _rng.Randomize();
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            if (!_attached && DistanceToHero() < ATTACH_RANGE)
                Attach();
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead) return;

            if (_attached && _hero != null)
            {
                GlobalPosition = _hero.GlobalPosition;
                return;
            }

            // Erratic approach
            _erraticTimer -= dt;
            if (_erraticTimer <= 0f)
            {
                _erraticTimer = _rng.RandfRange(0.1f, 0.3f);
                Vector2 toHero = _hero != null
                    ? (_hero.GlobalPosition - GlobalPosition).Normalized()
                    : Vector2.Zero;
                _erraticDir = (toHero + new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f))).Normalized();
            }

            Velocity = _erraticDir * MoveSpeed;
            MoveAndSlide();
        }

        private void Attach()
        {
            _attached = true;
            // Apply slow to hero - in a full impl, hero would expose a speed multiplier
        }

        protected override void Die()
        {
            // Remove slow effect if attached
            base.Die();
        }
    }
}
