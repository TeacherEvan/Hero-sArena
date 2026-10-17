using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class PerkSystemTests
{
    private static HeroStats FreshStats() => new HeroStats();

    [Fact]
    public void PerkName_ReturnsExpectedForEachPerk()
    {
        PerkSystem.PerkName(PerkType.DamageUp).Should().Be("DamageUp");
        PerkSystem.PerkName(PerkType.SpeedUp).Should().Be("SpeedUp");
        PerkSystem.PerkName(PerkType.KineticDamageUp).Should().Be("KineticDamageUp");
        PerkSystem.PerkName((PerkType)999).Should().Be("999"); // unknown falls back to ToString
    }

    [Fact]
    public void PerkDescription_ReturnsNonEmptyForAll20Perks()
    {
        foreach (PerkType p in Enum.GetValues<PerkType>())
        {
            PerkSystem.PerkDescription(p).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void PerkSynergyTag_ReturnsNonEmptyForAll20Perks()
    {
        foreach (PerkType p in Enum.GetValues<PerkType>())
        {
            PerkSystem.PerkSynergyTag(p).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ApplyPerk_DamageUp_IncreasesDamageMultBy15Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.DamageUp, stats);
        stats.DamageMult.Should().BeApproximately(1.15f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_SpeedUp_IncreasesMoveSpeedBy10Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.SpeedUp, stats);
        stats.MoveSpeed.Should().BeApproximately(220f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_HealthRegen_Adds2HpPerSec()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.HealthRegen, stats);
        stats.HpRegenPerSec.Should().Be(2f);
    }

    [Fact]
    public void ApplyPerk_MaxHealthUp_IncreasesMaxHealthBy15Percent()
    {
        var stats = new HeroStats { MaxHealth = 100f };
        PerkSystem.ApplyPerk(PerkType.MaxHealthUp, stats);
        stats.MaxHealth.Should().BeApproximately(115f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_AttackSpeedUp_ReducesCooldownMultBy15Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.AttackSpeedUp, stats);
        stats.AttackCooldownMult.Should().BeApproximately(0.85f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_PiercingShots_SetsPiercingTrue()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.PiercingShots, stats);
        stats.ProjectilePiercing.Should().BeTrue();
    }

    [Fact]
    public void ApplyPerk_ExplosiveRounds_SetsExplosiveTrue()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.ExplosiveRounds, stats);
        stats.ProjectileExplosive.Should().BeTrue();
    }

    [Fact]
    public void ApplyPerk_LifeSteal_Adds3Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.LifeSteal, stats);
        stats.LifeStealPct.Should().BeApproximately(0.03f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_ShieldBurst_Adds2ShieldCharges()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.ShieldBurst, stats);
        stats.ShieldCharges.Should().Be(2);
    }

    [Fact]
    public void ApplyPerk_DodgeCooldownReduce_MultipliesBy075()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.DodgeCooldownReduce, stats);
        stats.DodgeCooldownMult.Should().BeApproximately(0.75f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_CritChanceUp_Adds10Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.CritChanceUp, stats);
        stats.CritChance.Should().BeApproximately(0.10f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_AoERadiusUp_MultipliesBy120Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.AoERadiusUp, stats);
        stats.AoeRadiusMult.Should().BeApproximately(1.20f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_ProjectileCount_AddsOne()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.ProjectileCount, stats);
        stats.ProjectileCountBonus.Should().Be(1);
    }

    [Fact]
    public void ApplyPerk_SlowOnHit_SetsTrue()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.SlowOnHit, stats);
        stats.SlowOnHit.Should().BeTrue();
    }

    [Fact]
    public void ApplyPerk_BurnOnHit_SetsTrue()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.BurnOnHit, stats);
        stats.BurnOnHit.Should().BeTrue();
    }

    [Fact]
    public void ApplyPerk_FrostAura_SetsTrue()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.FrostAura, stats);
        stats.FrostAura.Should().BeTrue();
    }

    [Fact]
    public void ApplyPerk_ThornArmor_SetsTrue()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.ThornArmor, stats);
        stats.ThornArmor.Should().BeTrue();
    }

    [Fact]
    public void ApplyPerk_DoubleJump_SetsDodgeChargesTo2()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.DoubleJump, stats);
        stats.DodgeCharges.Should().Be(2);
    }

    [Fact]
    public void ApplyPerk_EnergyDamageUp_IncreasesBy15Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.EnergyDamageUp, stats);
        stats.EnergyDamageMult.Should().BeApproximately(1.15f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_KineticDamageUp_IncreasesBy15Percent()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.KineticDamageUp, stats);
        stats.KineticDamageMult.Should().BeApproximately(1.15f, 0.0001f);
    }

    [Fact]
    public void ApplyPerk_NullStats_DoesNotThrow()
    {
        PerkSystem.ApplyPerk(PerkType.DamageUp, null!);
    }

    [Fact]
    public void ApplyPerk_StacksAcrossMultiplePicks()
    {
        var stats = FreshStats();
        PerkSystem.ApplyPerk(PerkType.DamageUp, stats);
        PerkSystem.ApplyPerk(PerkType.DamageUp, stats);
        stats.DamageMult.Should().BeApproximately(1.15f * 1.15f, 0.0001f);
    }
}
