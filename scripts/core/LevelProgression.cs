using Godot;
using System;

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
    /// Thin engine wrapper: all math lives in <see cref="ProgressionFormulas"/>
    /// (pure C#, headlessly testable). This Node exists so scenes/autoloads
    /// can hold a progression instance in the tree.
    /// </summary>
    public partial class LevelProgression : Node
    {
        private readonly Random _rng = new();

        // ── Damage formulas ───────────────────────────────────────────────────
        /// <summary>Kinetic damage: D_k = D_0 * (1 + 0.15 * L)</summary>
        public float CalcKineticDamage(float baseDamage, int level)
            => ProgressionFormulas.CalcKineticDamage(baseDamage, level);

        /// <summary>Energy damage: D_e = D_0 * L^gamma (gamma clamped 1.2–1.4)</summary>
        public float CalcEnergyDamage(float baseDamage, int level)
            => ProgressionFormulas.CalcEnergyDamage(baseDamage, level);

        /// <summary>Collateral Karma amplifier: A = ln(e + 0.05 * K)</summary>
        public float CalcKarmaAmplifier(int destructionCount)
            => ProgressionFormulas.CalcKarmaAmplifier(destructionCount);

        // ── Perk selection ────────────────────────────────────────────────────
        /// <summary>Returns 3 distinct random perks for the upgrade menu.</summary>
        public PerkType[] GetRandomPerks(int count = 3)
            => ProgressionFormulas.GetRandomPerks(count, _rng);

        // ── XP / leveling ─────────────────────────────────────────────────────
        public int XpRequiredForLevel(int level)
            => ProgressionFormulas.XpRequiredForLevel(level);
    }
}
