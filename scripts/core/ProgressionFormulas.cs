using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Pure (Godot-free) progression math: damage formulas, karma amplifier,
    /// XP curve, and perk selection. Extracted from LevelProgression so the
    /// xUnit suite can cover it headlessly; the LevelProgression Node and
    /// CollateralKarma Node delegate here and add only engine wiring
    /// (RNG seeding, event subscription).
    /// </summary>
    public static class ProgressionFormulas
    {
        private const float GAMMA = 1.3f;
        private const float GAMMA_MIN = 1.2f;
        private const float GAMMA_MAX = 1.4f;

        private static readonly PerkType[] AllPerks = (PerkType[])Enum.GetValues(typeof(PerkType));

        /// <summary>Kinetic damage: D_k = D_0 * (1 + 0.15 * L)</summary>
        public static float CalcKineticDamage(float baseDamage, int level)
            => baseDamage * (1f + 0.15f * level);

        /// <summary>Energy damage: D_e = D_0 * L^gamma (gamma clamped 1.2–1.4)</summary>
        public static float CalcEnergyDamage(float baseDamage, int level)
        {
            float g = Math.Clamp(GAMMA, GAMMA_MIN, GAMMA_MAX);
            return baseDamage * MathF.Pow(level, g);
        }

        /// <summary>Collateral Karma amplifier: A = ln(e + 0.05 * K)</summary>
        public static float CalcKarmaAmplifier(int destructionCount)
            => MathF.Log(MathF.E + 0.05f * destructionCount);

        /// <summary>XP required to go from <paramref name="level"/> to next.</summary>
        public static int XpRequiredForLevel(int level)
            => 100 + (level - 1) * 50 + (level - 1) * (level - 1) * 10;

        /// <summary>Returns <paramref name="count"/> distinct random perks.</summary>
        public static PerkType[] GetRandomPerks(int count = 3, Random? rng = null)
        {
            rng ??= Random.Shared;
            var pool = new List<PerkType>(AllPerks);
            var result = new List<PerkType>(Math.Min(count, pool.Count));
            while (result.Count < count && pool.Count > 0)
            {
                int idx = rng.Next(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result.ToArray();
        }
    }
}
