using Godot;
using System;

namespace HeroArena
{
    public enum MutatorType { SpeedBoost, HealthBoost, DamageBoost, Shielded, Enraged, Splitting }

    /// <summary>
    /// Applies stacking random mutators to enemies based on current ThreatLevel.
    /// Higher threat = more mutators with stronger effects.
    /// </summary>
    public partial class EnemyMutatorSystem : Node
    {
        private readonly RandomNumberGenerator _rng = new();
        private static readonly MutatorType[] AllMutators = (MutatorType[])Enum.GetValues(typeof(MutatorType));

        public override void _Ready()
        {
            _rng.Randomize();
            EventBus.Instance.OnThreatLevelChanged += OnThreatChanged;
        }

        private void OnThreatChanged(int threatLevel) { /* Re-apply to new spawns via ApplyMutators */ }

        public void ApplyMutators(EnemyBase enemy, int threatLevel)
        {
            int mutatorCount = Mathf.Min(threatLevel, AllMutators.Length);
            var pool = new System.Collections.Generic.List<MutatorType>(AllMutators);

            for (int i = 0; i < mutatorCount; i++)
            {
                int idx = _rng.RandiRange(0, pool.Count - 1);
                ApplySingleMutator(enemy, pool[idx], threatLevel);
                pool.RemoveAt(idx);
            }
        }

        private static void ApplySingleMutator(EnemyBase enemy, MutatorType mutator, int threatLevel)
        {
            float scale = 1f + 0.1f * threatLevel;
            switch (mutator)
            {
                case MutatorType.SpeedBoost:
                    enemy.MoveSpeed *= scale;
                    break;
                case MutatorType.HealthBoost:
                    enemy.MaxHealth *= scale;
                    enemy.SetCurrentHealth(enemy.MaxHealth);
                    break;
                case MutatorType.DamageBoost:
                    enemy.Damage *= scale;
                    break;
                case MutatorType.Enraged:
                    enemy.MoveSpeed *= 1.5f;
                    enemy.Damage *= 1.5f;
                    break;
                // Shielded / Splitting require additional component attachment
            }
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnThreatLevelChanged -= OnThreatChanged;
        }
    }
}
