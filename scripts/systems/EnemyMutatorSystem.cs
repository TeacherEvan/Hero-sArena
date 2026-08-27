using Godot;
using System;

namespace HeroArena
{
    public enum MutatorType { SpeedBoost, HealthBoost, DamageBoost, Shielded, Enraged }

    /// <summary>
    /// Applies stacking random mutators to enemies based on current ThreatLevel.
    /// Higher threat = more mutators with stronger effects.
    /// </summary>
    public partial class EnemyMutatorSystem : Node
    {
        public static EnemyMutatorSystem? Instance { get; private set; }

        private readonly RandomNumberGenerator _rng = new();
        private static readonly MutatorType[] AllMutators = (MutatorType[])Enum.GetValues(typeof(MutatorType));

        public override void _Ready()
        {
            Instance = this;
            _rng.Randomize();
            EventBus.Instance.OnThreatLevelChanged += OnThreatChanged;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnThreatLevelChanged -= OnThreatChanged;
            if (Instance == this) Instance = null;
            base._ExitTree();
        }

        private void OnThreatChanged(int threatLevel) { /* future: re-apply scaling to live enemies */ }

        public void ApplyMutators(EnemyBase enemy, int threatLevel)
        {
            int mutatorCount = Mathf.Min(threatLevel, AllMutators.Length);

            Span<MutatorType> pool = stackalloc MutatorType[AllMutators.Length];
            AllMutators.AsSpan().CopyTo(pool);
            int poolCount = pool.Length;

            for (int i = 0; i < mutatorCount; i++)
            {
                int idx = _rng.RandiRange(0, poolCount - 1);
                ApplySingleMutator(enemy, pool[idx], threatLevel);

                pool[idx] = pool[poolCount - 1];
                poolCount--;
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
                case MutatorType.Shielded:
                    enemy.AddShieldCharges(Mathf.Max(1, threatLevel / 2));
                    break;
                case MutatorType.Enraged:
                    enemy.MoveSpeed *= 1.5f;
                    enemy.Damage *= 1.5f;
                    break;
                // Splitting requires a new scene; not implemented yet.
            }
        }
    }
}
