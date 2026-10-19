using System;

namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free status container tracking Slow and Burn effects on an entity.
    /// Headlessly testable with zero scene tree dependencies.
    /// </summary>
    public struct StatusEffectState
    {
        public float SlowTimer;
        public float SlowRatio;
        public float BurnTimer;
        public float BurnDps;

        public readonly bool IsSlowed => SlowTimer > 0f;
        public readonly bool IsBurning => BurnTimer > 0f;

        public void ApplySlow(float duration, float slowRatio = 0.30f)
        {
            SlowTimer = MathF.Max(SlowTimer, duration);
            SlowRatio = MathF.Max(SlowRatio, slowRatio);
        }

        public void ApplyBurn(float duration, float dps = 5f)
        {
            BurnTimer = MathF.Max(BurnTimer, duration);
            BurnDps = MathF.Max(BurnDps, dps);
        }

        /// <summary>
        /// Updates status timers by dt and returns the burn damage dealt this frame.
        /// </summary>
        public float Update(float dt)
        {
            float burnDamage = 0f;
            if (BurnTimer > 0f)
            {
                float tickDt = MathF.Min(dt, BurnTimer);
                burnDamage = BurnDps * tickDt;
                BurnTimer -= dt;
                if (BurnTimer <= 0f)
                {
                    BurnTimer = 0f;
                    BurnDps = 0f;
                }
            }

            if (SlowTimer > 0f)
            {
                SlowTimer -= dt;
                if (SlowTimer <= 0f)
                {
                    SlowTimer = 0f;
                    SlowRatio = 0f;
                }
            }

            return burnDamage;
        }

        public readonly float GetSpeedMultiplier()
        {
            if (SlowTimer > 0f)
                return MathF.Max(0.1f, 1f - SlowRatio);
            return 1f;
        }
    }

    /// <summary>
    /// Pure (Godot-free) combat mechanics formulas: health regen, lifesteal,
    /// thorn reflection, frost aura distance check, and explosive radius.
    /// Extracted to keep all combat interactions 100% testable via xUnit.
    /// </summary>
    public static class HeroCombatMechanics
    {
        public const float FROST_AURA_RADIUS = 180f;
        public const float FROST_AURA_SLOW_RATIO = 0.20f;
        public const float EXPLOSIVE_ROUNDS_RADIUS = 80f;
        public const float EXPLOSIVE_ROUNDS_DAMAGE_RATIO = 0.50f;
        public const float THORN_ARMOR_REFLECT_RATIO = 0.10f;

        /// <summary>
        /// Calculates regenerated health for a frame, clamped to MaxHealth.
        /// </summary>
        public static float ProcessRegen(float currentHealth, float maxHealth, float hpRegenPerSec, float dt)
        {
            if (hpRegenPerSec <= 0f || currentHealth >= maxHealth || dt <= 0f)
                return currentHealth;

            return MathF.Min(maxHealth, currentHealth + (hpRegenPerSec * dt));
        }

        /// <summary>
        /// Computes lifesteal heal amount from damage dealt.
        /// </summary>
        public static float CalculateLifeSteal(float damageDealt, float lifeStealPct)
        {
            if (lifeStealPct <= 0f || damageDealt <= 0f) return 0f;
            return damageDealt * lifeStealPct;
        }

        /// <summary>
        /// Computes reflected thorn damage when hero is struck by melee/contact.
        /// </summary>
        public static float CalculateThornReflection(float damageTaken, bool hasThornArmor)
        {
            if (!hasThornArmor || damageTaken <= 0f) return 0f;
            return damageTaken * THORN_ARMOR_REFLECT_RATIO;
        }

        /// <summary>
        /// Checks if an entity at distSq is within Frost Aura radius.
        /// </summary>
        public static bool IsInFrostAura(float distanceSquared, bool hasFrostAura)
        {
            if (!hasFrostAura) return false;
            return distanceSquared <= (FROST_AURA_RADIUS * FROST_AURA_RADIUS);
        }
    }
}
