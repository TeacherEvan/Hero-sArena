using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class HeroCombatMechanicsTests
{
    [Fact]
    public void ProcessRegen_RestoresHealthClampedToMax()
    {
        float currentHp = 80f;
        float maxHp = 100f;
        float regenPerSec = 5f;
        float dt = 2f;

        float result = HeroCombatMechanics.ProcessRegen(currentHp, maxHp, regenPerSec, dt);
        result.Should().Be(90f);

        // Clamping check
        float overRegen = HeroCombatMechanics.ProcessRegen(95f, maxHp, regenPerSec, 2f);
        overRegen.Should().Be(100f);
    }

    [Fact]
    public void ProcessRegen_ZeroOrNegativeDt_ReturnsUnchanged()
    {
        HeroCombatMechanics.ProcessRegen(50f, 100f, 5f, 0f).Should().Be(50f);
        HeroCombatMechanics.ProcessRegen(50f, 100f, 0f, 1f).Should().Be(50f);
    }

    [Fact]
    public void CalculateLifeSteal_ComputesProportionalHealing()
    {
        float heal = HeroCombatMechanics.CalculateLifeSteal(100f, 0.05f);
        heal.Should().BeApproximately(5f, 0.001f);

        HeroCombatMechanics.CalculateLifeSteal(100f, 0f).Should().Be(0f);
        HeroCombatMechanics.CalculateLifeSteal(0f, 0.05f).Should().Be(0f);
    }

    [Fact]
    public void CalculateThornReflection_ReflectsTenPercentWhenActive()
    {
        HeroCombatMechanics.CalculateThornReflection(50f, true).Should().Be(5f);
        HeroCombatMechanics.CalculateThornReflection(50f, false).Should().Be(0f);
    }

    [Fact]
    public void IsInFrostAura_ChecksDistanceAccurately()
    {
        float insideDistSq = 100f * 100f; // 100 <= 180
        float outsideDistSq = 200f * 200f; // 200 > 180

        HeroCombatMechanics.IsInFrostAura(insideDistSq, true).Should().BeTrue();
        HeroCombatMechanics.IsInFrostAura(outsideDistSq, true).Should().BeFalse();
        HeroCombatMechanics.IsInFrostAura(insideDistSq, false).Should().BeFalse();
    }

    [Fact]
    public void StatusEffectState_TracksSlowAndSpeedMultiplier()
    {
        var status = new StatusEffectState();
        status.IsSlowed.Should().BeFalse();
        status.GetSpeedMultiplier().Should().Be(1f);

        status.ApplySlow(2f, 0.30f);
        status.IsSlowed.Should().BeTrue();
        status.GetSpeedMultiplier().Should().BeApproximately(0.70f, 0.001f);

        status.Update(1f);
        status.IsSlowed.Should().BeTrue();
        status.SlowTimer.Should().Be(1f);

        status.Update(1.5f);
        status.IsSlowed.Should().BeFalse();
        status.GetSpeedMultiplier().Should().Be(1f);
    }

    [Fact]
    public void StatusEffectState_TracksBurnDamageOverTime()
    {
        var status = new StatusEffectState();
        status.IsBurning.Should().BeFalse();

        status.ApplyBurn(3f, 10f); // 10 dps for 3s
        status.IsBurning.Should().BeTrue();

        float tick1 = status.Update(1f);
        tick1.Should().BeApproximately(10f, 0.001f);

        float tick2 = status.Update(2f);
        tick2.Should().BeApproximately(20f, 0.001f);

        status.IsBurning.Should().BeFalse();
        status.Update(1f).Should().Be(0f);
    }

    [Fact]
    public void PerkSynergies_ApplySynergyBonuses()
    {
        var stats = new HeroStats();
        float baseAoe = stats.AoeRadiusMult;
        float baseDmg = stats.DamageMult;

        var perks = new HashSet<PerkType> { PerkType.FrostAura, PerkType.SlowOnHit };
        PerkSystem.ApplySynergies(perks, stats);

        stats.AoeRadiusMult.Should().BeApproximately(baseAoe * 1.25f, 0.001f);
        stats.DamageMult.Should().Be(baseDmg); // Firestorm not unlocked

        perks.Add(PerkType.BurnOnHit);
        perks.Add(PerkType.ExplosiveRounds);
        PerkSystem.ApplySynergies(perks, stats);

        stats.DamageMult.Should().BeApproximately(baseDmg * 1.20f, 0.001f);
    }
}
