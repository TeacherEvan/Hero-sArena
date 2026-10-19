using System;

namespace HeroArena
{
    /// <summary>
    /// Pure (Godot-free) wave-intensity math: the modified Weibull spawn
    /// curve and the per-wave enemy count. Extracted from WaveManager's
    /// CalculateWaveEnemyCount (gate-only coverage; the Weibull constants
    /// were duplicated by the gate assertions) so the xUnit suite can pin
    /// the curve headlessly; WaveManager keeps only engine wiring (spawning,
    /// timers, event subscription).
    /// </summary>
    public static class WaveFormulas
    {
        public const int MinWaveEnemies = 10;
        public const int MaxEnemies = 2000;
        private const float IntensityScaleFactor = 500f;
        private const float WeibullK = 1.5f;
        private const float WeibullLambda = 10f;

        /// <summary>
        /// Weibull intensity for <paramref name="wave"/>: a curve that rises
        /// from zero, peaks around wave 5, then decays — so early waves ramp
        /// up, mid waves are the most intense, late waves taper off.
        /// </summary>
        public static float WeibullIntensity(float wave)
            => (WeibullK / WeibullLambda)
                * MathF.Pow(wave / WeibullLambda, WeibullK - 1f)
                * MathF.Exp(-MathF.Pow(wave / WeibullLambda, WeibullK));

        /// <summary>
        /// Enemy count for <paramref name="wave"/>, floor-clamped to
        /// <see cref="MinWaveEnemies"/> and ceiling-clamped to the room left
        /// under <see cref="MaxEnemies"/> for <paramref name="activeEnemyCount"/>.
        /// <paramref name="enemyCountMultiplier"/> scales the Weibull curve
        /// (Easy=0.6, Normal=1.0, Hard=1.4) before clamping.
        /// </summary>
        public static int CalculateWaveEnemyCount(int wave, int activeEnemyCount, float enemyCountMultiplier = 1f)
        {
            // Weibull surge + sustained late-game escalation past wave 10
            int escalation = (int)(Math.Max(0, wave - 10) * 2.5f);
            int count = MinWaveEnemies + (int)((WeibullIntensity(wave) * IntensityScaleFactor + escalation) * enemyCountMultiplier);
            int max = Math.Max(0, MaxEnemies - activeEnemyCount);
            // Never return a negative count: an over-full arena yields 0, so the
            // caller's spawn loop runs zero times and _pendingEnemiesToSpawn can
            // never strand below zero (wave soft-lock). The floor still applies
            // when there is room; the ceiling clamps to free slots otherwise.
            if (count < MinWaveEnemies) return MinWaveEnemies;
            if (count > max) return max;
            return count;
        }
    }
}
