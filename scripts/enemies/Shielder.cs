using Godot;

namespace HeroArena
{
    public enum ShielderState { Spawning, AdvanceGuarded, Reorient, Bash }

    /// <summary>Shielder – 90% frontal damage block. Bash attack when close.</summary>
    public partial class Shielder : EnemyBase
    {
        private const float BLOCK_REDUCTION = 0.90f;
        private ShielderState _aiState = ShielderState.Spawning;
        private float _bashCooldown = 0f;

        protected override void OnSpawn()
        {
            MaxHealth = 120f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 90f;
            Damage = 25f;
            ExpValue = 25;
            _aiState = ShielderState.Spawning;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            if (_bashCooldown > 0f) _bashCooldown -= (float)delta;
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            float dist = DistanceToHero();
            _aiState = dist < 40f ? ShielderState.Bash
                : dist < 200f ? ShielderState.AdvanceGuarded
                : ShielderState.Reorient;

            if (_aiState == ShielderState.Bash && _bashCooldown <= 0f)
            {
                _hero.TakeDamage(Damage, DamageType.Kinetic);
                _bashCooldown = 2f;
            }
        }

        public override void TakeDamage(float amount, DamageType type = DamageType.Kinetic)
        {
            if (_hero == null) return;
            // Check if damage comes from frontal arc (dot product > 0 means facing away = shield up)
            Vector2 toHero = (_hero.GlobalPosition - GlobalPosition).Normalized();
            Vector2 facing = -Transform.X; // shield faces toward player
            bool shieldBlocking = toHero.Dot(facing) > 0.5f;
            float reduced = shieldBlocking ? amount * (1f - BLOCK_REDUCTION) : amount;
            base.TakeDamage(reduced, type);
        }
    }
}
