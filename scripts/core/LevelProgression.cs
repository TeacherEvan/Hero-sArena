using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    public enum PerkType
    {
        DamageUp, SpeedUp, HealthRegen, MaxHealthUp, AttackSpeedUp,
        PiercingShots, ExplosiveRounds, LifeSteal, ShieldBurst, DodgeCooldownReduce,
        CritChanceUp, AoERadiusUp, ProjectileCount, SlowOnHit, BurnOnHit,
        FrostAura, ThornArmor, DoubleJump, EnergyDamageUp, KineticDamageUp
    }

    /// <summary>
    /// Manages hero level progression, damage scaling formulas, and perk offers.
    /// </summary>
    public partial class LevelProgression : Node
    {
        private const float GAMMA = 1.3f;
        private const float GAMMA_MIN = 1.2f;
        private const float GAMMA_MAX = 1.4f;

        private static readonly PerkType[] AllPerks = (PerkType[])Enum.GetValues(typeof(PerkType));

        private readonly RandomNumberGenerator _rng = new();

        public override void _Ready()
        {
            _rng.Randomize();
        }

        // ── Damage formulas ───────────────────────────────────────────────────
        /// <summary>Kinetic damage: D_k = D_0 * (1 + 0.15 * L)</summary>
        public float CalcKineticDamage(float baseDamage, int level)
            => baseDamage * (1f + 0.15f * level);

        /// <summary>Energy damage: D_e = D_0 * L^gamma (gamma clamped 1.2–1.4)</summary>
        public float CalcEnergyDamage(float baseDamage, int level)
        {
            float g = Mathf.Clamp(GAMMA, GAMMA_MIN, GAMMA_MAX);
            return baseDamage * MathF.Pow(level, g);
        }

        /// <summary>Collateral Karma amplifier: A = ln(e + 0.05 * K)</summary>
        public float CalcKarmaAmplifier(int destructionCount)
            => MathF.Log(MathF.E + 0.05f * destructionCount);

        // ── Perk selection ────────────────────────────────────────────────────
        /// <summary>Returns 3 distinct random perks for the upgrade menu.</summary>
        public PerkType[] GetRandomPerks(int count = 3)
        {
            var pool = new List<PerkType>(AllPerks);
            var result = new PerkType[count];
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = _rng.RandiRange(0, pool.Count - 1);
                result[i] = pool[idx];
                pool.RemoveAt(idx);
            }
            return result;
        }

        // ── XP / leveling ─────────────────────────────────────────────────────
        public int XpRequiredForLevel(int level)
            => 100 + (level - 1) * 50 + (level - 1) * (level - 1) * 10;
    }
}
