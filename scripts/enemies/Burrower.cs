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
            float distSq = DistanceSquaredToHero();
            switch (_aiState)
            {
                case BurrowerState.SubmergedTrack:
                    DoSubmergedTrack(distSq);
                    break;
                case BurrowerState.Erupt:
                    DoErupt();
                    break;
                case BurrowerState.Attack:
                    DoAttack(distSq);
                    break;
                case BurrowerState.Submerge:
                    DoSubmerge();
                    break;
            }
        }

        private void DoSubmergedTrack(float distSq)
        {
            if (distSq < ERUPT_RANGE * ERUPT_RANGE) _aiState = BurrowerState.Erupt;
        }

        private void DoErupt()
        {
            // After erupting, immediately attack
            _aiState = BurrowerState.Attack;
        }

        private void DoAttack(float distSq)
        {
            if (distSq < ATTACK_RANGE * ATTACK_RANGE)
            {
                _hero?.TakeDamage(Damage, DamageType.Kinetic);
                _aiState = BurrowerState.Submerge;
                _submergeCooldown = SUBMERGE_COOLDOWN;
            }
        }

        private void DoSubmerge()
        {
            if (_submergeCooldown <= 0f) _aiState = BurrowerState.SubmergedTrack;
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead || _hero is not { } hero) return;
            if (_aiState == BurrowerState.Submerge) return;
            // Burrowers ignore flow field - direct tracking underground
            Vector2 dir = (hero.GlobalPosition - GlobalPosition).Normalized();
            Velocity = dir * MoveSpeed;
            MoveAndSlide();
        }
    }
}
