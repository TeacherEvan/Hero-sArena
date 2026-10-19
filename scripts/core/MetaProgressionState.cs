using System;

namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free meta-progression state container.
    /// Manages persistent run currency (scrap), stat upgrade tiers, and starting bonuses.
    /// Headlessly testable via xUnit with zero scene tree dependencies.
    /// </summary>
    public class MetaProgressionState
    {
        public const string ConfigSection = "meta";
        public const string ScrapKey = "scrap";
        public const string HealthTierKey = "health_tier";
        public const string SpeedTierKey = "speed_tier";
        public const string DamageTierKey = "damage_tier";

        public const int MaxTier = 5;
        public const int BaseUpgradeCost = 50;
        public const float CostMultiplier = 1.5f;

        public int Scrap { get; set; } = 0;
        public int HealthUpgradeTier { get; set; } = 0;
        public int SpeedUpgradeTier { get; set; } = 0;
        public int DamageUpgradeTier { get; set; } = 0;

        public static int GetUpgradeCost(int currentTier)
        {
            if (currentTier >= MaxTier) return -1;
            return (int)(BaseUpgradeCost * MathF.Pow(CostMultiplier, currentTier));
        }

        public bool TryPurchaseHealthUpgrade()
        {
            int cost = GetUpgradeCost(HealthUpgradeTier);
            if (cost < 0 || Scrap < cost) return false;
            Scrap -= cost;
            HealthUpgradeTier++;
            return true;
        }

        public bool TryPurchaseSpeedUpgrade()
        {
            int cost = GetUpgradeCost(SpeedUpgradeTier);
            if (cost < 0 || Scrap < cost) return false;
            Scrap -= cost;
            SpeedUpgradeTier++;
            return true;
        }

        public bool TryPurchaseDamageUpgrade()
        {
            int cost = GetUpgradeCost(DamageUpgradeTier);
            if (cost < 0 || Scrap < cost) return false;
            Scrap -= cost;
            DamageUpgradeTier++;
            return true;
        }

        public void Respec()
        {
            int totalRefund = 0;
            for (int t = 0; t < HealthUpgradeTier; t++) totalRefund += GetUpgradeCost(t);
            for (int t = 0; t < SpeedUpgradeTier; t++) totalRefund += GetUpgradeCost(t);
            for (int t = 0; t < DamageUpgradeTier; t++) totalRefund += GetUpgradeCost(t);

            Scrap += totalRefund;
            HealthUpgradeTier = 0;
            SpeedUpgradeTier = 0;
            DamageUpgradeTier = 0;
        }

        public float GetBonusMaxHealth() => HealthUpgradeTier * 10f; // +10 HP per tier
        public float GetBonusMoveSpeed() => SpeedUpgradeTier * 10f; // +10 Speed per tier
        public float GetBonusDamageMultiplier() => 1f + (DamageUpgradeTier * 0.05f); // +5% Damage per tier

        public void ApplyToHeroStats(HeroStats stats)
        {
            if (stats == null) return;
            stats.MaxHealth += GetBonusMaxHealth();
            stats.MoveSpeed += GetBonusMoveSpeed();
            stats.DamageMult *= GetBonusDamageMultiplier();
        }

        /// <summary>
        /// Calculates scrap rewarded for run performance.
        /// </summary>
        public static int CalculateRunScrap(int wave, int kills, int destructions)
        {
            return Math.Max(0, (wave * 10) + (kills * 2) + (destructions / 2));
        }

        public static int ParseInt(string? raw, int defaultValue) =>
            int.TryParse(raw, out var v) ? v : defaultValue;
    }
}
