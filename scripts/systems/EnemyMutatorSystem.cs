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

        private void OnThreatChanged(int threatLevel)
        {
            // Re-apply mutator scaling to all live enemies so higher threat
            // strengthens existing foes, not just newly spawned ones.
            if (GameManager.Instance == null) return;
            foreach (var enemy in EnemyBase.GetAllAlive())
            {
                if (enemy.State == EnemyAIState.Dead || enemy.IsMindControlled)
                    continue;
                ApplyMutators(enemy, threatLevel, 1f);
            }
        }

        public void ApplyMutators(EnemyBase enemy, int threatLevel, float mutatorScale = 1f)
        {
            // Total multiplier applied to spawn-time base stats (MutatorFormulas,
            // covered headlessly). Because the multiplier is computed from base
            // (not current) values, re-apply on every threat tier is idempotent:
            // tier N always yields the same stats regardless of how many times
            // it is re-applied (audit P1-1).
            float scale = MutatorFormulas.ComputeScale(threatLevel, mutatorScale);
            int mutatorCount = MutatorFormulas.ComputeMutatorCount(threatLevel, AllMutators.Length, mutatorScale);

            Span<MutatorType> pool = stackalloc MutatorType[AllMutators.Length];
            AllMutators.AsSpan().CopyTo(pool);
            int poolCount = pool.Length;

            for (int i = 0; i < mutatorCount; i++)
            {
                int idx = _rng.RandiRange(0, poolCount - 1);
                ApplySingleMutator(enemy, pool[idx], threatLevel, scale);

                pool[idx] = pool[poolCount - 1];
                poolCount--;
            }
        }

        private static void ApplySingleMutator(EnemyBase enemy, MutatorType mutator, int threatLevel, float scale)
        {
            switch (mutator)
            {
                case MutatorType.SpeedBoost:
                    enemy.MoveSpeed = enemy.BaseMoveSpeed * scale;
                    break;
                case MutatorType.HealthBoost:
                    float healthPct = enemy.MaxHealth > 0f ? enemy.CurrentHealth / enemy.MaxHealth : 1f;
                    enemy.MaxHealth = enemy.BaseMaxHealth * scale;
                    enemy.SetCurrentHealth(enemy.MaxHealth * healthPct);
                    break;
                case MutatorType.DamageBoost:
                    enemy.Damage = enemy.BaseDamage * scale;
                    break;
                case MutatorType.Shielded:
                    // Additive charge count (not multiplicative): keep based on
                    // threat level so it scales linearly, not exponentially.
                    enemy.AddShieldCharges(Mathf.Max(1, threatLevel / 2));
                    break;
                case MutatorType.Enraged:
                    enemy.MoveSpeed = enemy.BaseMoveSpeed * 1.5f;
                    enemy.Damage = enemy.BaseDamage * 1.5f;
                    break;
                // Splitting requires a new scene; not implemented yet.
            }
        }
    }
}
