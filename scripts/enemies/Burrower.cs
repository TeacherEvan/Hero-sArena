using Godot;

namespace HeroArena
{
    public enum BurrowerState { SubmergedTrack, Erupt, Attack, Submerge }

    /// <summary>Burrower – underground tracker, erupts under player. Ignores flow field.</summary>
    public partial class Burrower : EnemyBase
    {
        private const float ERUPT_RANGE = 80f;
        private const float ATTACK_RANGE = 60f;
        private const float SUBMERGE_COOLDOWN = 4f;

        private BurrowerState _aiState = BurrowerState.SubmergedTrack;
        private float _submergeCooldown = 0f;

        protected override void OnSpawn()
        {
            MaxHealth = 75f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 140f;
            Damage = 30f;
            ExpValue = 35;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            if (_submergeCooldown > 0f) _submergeCooldown -= (float)delta;
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            float dist = DistanceToHero();
            switch (_aiState)
            {
                case BurrowerState.SubmergedTrack:
                    if (dist < ERUPT_RANGE) _aiState = BurrowerState.Erupt;
                    break;
                case BurrowerState.Erupt:
                    // After erupting, immediately attack
                    _aiState = BurrowerState.Attack;
                    break;
                case BurrowerState.Attack:
                    if (dist < ATTACK_RANGE)
                    {
                        _hero.TakeDamage(Damage, DamageType.Kinetic);
                        _aiState = BurrowerState.Submerge;
                        _submergeCooldown = SUBMERGE_COOLDOWN;
                    }
                    break;
                case BurrowerState.Submerge:
                    if (_submergeCooldown <= 0f) _aiState = BurrowerState.SubmergedTrack;
                    break;
            }
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead || _hero == null) return;
            if (_aiState == BurrowerState.Submerge) return;
            // Burrowers ignore flow field - direct tracking underground
            Vector2 dir = (_hero.GlobalPosition - GlobalPosition).Normalized();
            Velocity = dir * MoveSpeed;
            MoveAndSlide();
        }
    }
}
