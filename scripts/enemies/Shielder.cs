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
            float distSq = DistanceSquaredToHero();
            _aiState = distSq < 40f * 40f ? ShielderState.Bash
                : distSq < 200f * 200f ? ShielderState.AdvanceGuarded
                : ShielderState.Reorient;

            if (_aiState == ShielderState.Bash && _bashCooldown <= 0f)
            {
                _hero.TakeDamage(Damage, DamageType.Kinetic);
                _bashCooldown = 2f;
            }
        }

        public override void TakeDamage(float amount, DamageType type = DamageType.Kinetic)
        {
            // Shielder's frontal shield blocks 90% of damage.
            // The Shielder always faces the hero (it advances head-on), so the dot-product
            // frontal-arc check is not meaningful — every attack comes from the front.
            // If a future Shielder variant strafes or sidesteps, reintroduce the arc check
            // using a stable spawn-time facing direction rather than the per-tick vector
            // to the hero (which always returned ~1.0 — see fix/audit-medium-cleanup review).
            float reduced = amount * (1f - BLOCK_REDUCTION);
            base.TakeDamage(reduced, type);
        }
    }
}
