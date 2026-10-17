using System;

namespace HeroArena
{
    public enum CombustionStage
    {
        Normal,
        Blazing,
        Overheat
    }

    /// <summary>
    /// Pure logic state machine for Ignis's Combustion Overheat Engine.
    /// Free of Godot Node dependencies; tested headlessly via xUnit.
    /// </summary>
    public class IgnisCombustionState
    {
        public const float MAX_HEAT = 100f;
        public const float BLAZING_THRESHOLD = 50f;
        public const float OVERHEAT_THRESHOLD = 98f;
        public const float DEFAULT_COOL_RATE = 12f;

        public float CurrentHeat { get; private set; }

        public CombustionStage Stage => CurrentHeat switch
        {
            >= OVERHEAT_THRESHOLD => CombustionStage.Overheat,
            >= BLAZING_THRESHOLD => CombustionStage.Blazing,
            _ => CombustionStage.Normal
        };

        public void AddHeat(float amount)
        {
            CurrentHeat = Math.Clamp(CurrentHeat + amount, 0f, MAX_HEAT);
        }

        public void CoolDown(float dt, float coolRate = DEFAULT_COOL_RATE)
        {
            if (dt <= 0f) return;
            CurrentHeat = Math.Clamp(CurrentHeat - (coolRate * dt), 0f, MAX_HEAT);
        }

        public float VentHeat(float amount = 30f)
        {
            float vented = Math.Min(CurrentHeat, amount);
            CurrentHeat -= vented;
            return vented;
        }

        public float ConsumeAllHeat()
        {
            float consumed = CurrentHeat;
            CurrentHeat = 0f;
            return consumed;
        }

        public float GetAttackCooldownMultiplier()
        {
            return 1.0f - (CurrentHeat / MAX_HEAT * 0.38f);
        }

        public static float CalcNovaDamage(float baseDamage, float heat)
        {
            float heatFactor = Math.Clamp(heat / MAX_HEAT, 0f, 1f);
            return baseDamage * (1.0f + (heatFactor * 1.5f));
        }

        public static float CalcNovaRadius(float baseRadius, float heat)
        {
            float heatFactor = Math.Clamp(heat / MAX_HEAT, 0f, 1f);
            return baseRadius + (heatFactor * 120f);
        }
    }
}
