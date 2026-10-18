using System;

namespace HeroArena
{
    /// <summary>
    /// Pure (Godot-free) mutator formulas: the threat-tier scale and mutator
    /// count used by <see cref="EnemyMutatorSystem"/>. Extracted so the
    /// idempotency invariant — the scale is a total multiplier applied to
    /// spawn-time base stats, never compounded on current values — can be
    /// verified headlessly without instantiating Godot nodes (audit P1-1).
    /// </summary>
    public static class MutatorFormulas
    {
        /// <summary>
        /// Total multiplier for a threat tier: <c>1 + 0.1 * threatLevel *
        /// mutatorScale</c>. Applied to base stats (not current values) so
        /// re-application at the same tier is idempotent.
        /// </summary>
        public static float ComputeScale(int threatLevel, float mutatorScale = 1f)
            => 1f + 0.1f * threatLevel * mutatorScale;

        /// <summary>
        /// Number of mutators to apply for a threat tier, clamped to the
        /// available mutator pool size.
        /// </summary>
        public static int ComputeMutatorCount(int threatLevel, int maxMutators, float mutatorScale = 1f)
            => (int)MathF.Ceiling(MathF.Min(threatLevel, maxMutators) * mutatorScale);
    }
}
