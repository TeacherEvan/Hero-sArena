using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class MetaProgressionStateTests
{
    [Fact]
    public void GetUpgradeCost_ScalesWithTier_ReturnsNegativeWhenMaxed()
    {
        MetaProgressionState.GetUpgradeCost(0).Should().Be(50);
        MetaProgressionState.GetUpgradeCost(1).Should().Be((int)(50 * 1.5f));
        MetaProgressionState.GetUpgradeCost(5).Should().Be(-1); // MaxTier reached
    }

    [Fact]
    public void TryPurchaseUpgrades_DeductsScrapAndIncrementsTier()
    {
        var meta = new MetaProgressionState { Scrap = 150 };

        meta.TryPurchaseHealthUpgrade().Should().BeTrue();
        meta.HealthUpgradeTier.Should().Be(1);
        meta.Scrap.Should().Be(100);

        meta.TryPurchaseSpeedUpgrade().Should().BeTrue();
        meta.SpeedUpgradeTier.Should().Be(1);
        meta.Scrap.Should().Be(50);

        meta.TryPurchaseDamageUpgrade().Should().BeTrue();
        meta.DamageUpgradeTier.Should().Be(1);
        meta.Scrap.Should().Be(0);

        // Insufficient scrap
        meta.TryPurchaseHealthUpgrade().Should().BeFalse();
    }

    [Fact]
    public void Respec_RefundsAllSpentScrapAndResetsTiers()
    {
        var meta = new MetaProgressionState { Scrap = 300 };
        meta.TryPurchaseHealthUpgrade(); // 50
        meta.TryPurchaseHealthUpgrade(); // 75
        meta.TryPurchaseSpeedUpgrade();  // 50
        int remaining = meta.Scrap;

        meta.Respec();
        meta.HealthUpgradeTier.Should().Be(0);
        meta.SpeedUpgradeTier.Should().Be(0);
        meta.DamageUpgradeTier.Should().Be(0);
        meta.Scrap.Should().Be(300);
    }

    [Fact]
    public void ApplyToHeroStats_AddsBonusesAccurately()
    {
        var meta = new MetaProgressionState
        {
            HealthUpgradeTier = 2, // +20 HP
            SpeedUpgradeTier = 1,  // +10 Speed
            DamageUpgradeTier = 2  // +10% Damage
        };

        var stats = new HeroStats();
        meta.ApplyToHeroStats(stats);

        stats.MaxHealth.Should().Be(120f);
        stats.MoveSpeed.Should().Be(210f);
        stats.DamageMult.Should().BeApproximately(1.10f, 0.001f);
    }

    [Fact]
    public void CalculateRunScrap_ComputesPerformanceScrap()
    {
        int scrap = MetaProgressionState.CalculateRunScrap(wave: 10, kills: 50, destructions: 20);
        // (10 * 10) + (50 * 2) + (20 / 2) = 100 + 100 + 10 = 210
        scrap.Should().Be(210);
    }
}
