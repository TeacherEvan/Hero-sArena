using System;
using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Heroes;

public class NewHeroesTests
{
    [Fact]
    public void HeroClass_ContainsAllSevenHeroes()
    {
        var values = Enum.GetNames<HeroClass>();
        values.Should().Contain(new[] { "Atlas", "Zephyr", "Synapse", "Volt", "Ignis", "Toxis", "Havoc" });
        values.Length.Should().Be(7);
    }

    [Fact]
    public void HeroStats_DefaultsAreValidForCombat()
    {
        var stats = new HeroStats();
        stats.MaxHealth.Should().Be(100f);
        stats.MoveSpeed.Should().Be(200f);
        stats.DamageMult.Should().Be(1f);
        stats.AttackCooldownMult.Should().Be(1f);
        stats.DodgeCooldownMult.Should().Be(1f);
        stats.AoeRadiusMult.Should().Be(1f);
    }

    [Theory]
    [InlineData(18f, 1, 20.7f)] // 18 * (1 + 0.15 * 1) = 20.7
    [InlineData(30f, 1, 34.5f)] // 30 * (1 + 0.15 * 1) = 34.5
    [InlineData(65f, 1, 74.75f)] // 65 * (1 + 0.15 * 1) = 74.75
    [InlineData(100f, 5, 175f)] // 100 * (1 + 0.15 * 5) = 175
    public void LevelProgression_ScalesNewHeroDamagesCorrectly(float baseDmg, int level, float expectedDmg)
    {
        float actual = LevelProgression.CalcKineticDamage(baseDmg, level);
        actual.Should().BeApproximately(expectedDmg, 0.01f);
    }

    [Fact]
    public void HeroStats_CooldownMultipliers_AffectTimersPredictably()
    {
        var stats = new HeroStats
        {
            AttackCooldownMult = 0.5f,
            DodgeCooldownMult = 0.8f,
            AoeRadiusMult = 1.5f,
            DamageMult = 2.0f
        };

        float baseFireCooldown = 0.14f;
        float baseDodgeCooldown = 0.7f;
        float baseAoeRadius = 280f;

        float effectiveFireCooldown = baseFireCooldown * stats.AttackCooldownMult;
        float effectiveDodgeCooldown = baseDodgeCooldown * stats.DodgeCooldownMult;
        float effectiveAoeRadius = baseAoeRadius * stats.AoeRadiusMult;

        effectiveFireCooldown.Should().BeApproximately(0.07f, 0.001f);
        effectiveDodgeCooldown.Should().BeApproximately(0.56f, 0.001f);
        effectiveAoeRadius.Should().BeApproximately(420f, 0.001f);
    }

    [Theory]
    [InlineData(DamageType.Fire, 1f, 0.5f, 0.1f)]
    [InlineData(DamageType.Acid, 0.4f, 1f, 0.4f)]
    [InlineData(DamageType.Explosive, 1f, 1f, 1f)]
    public void HitFlashPalette_MapsNewHeroDamageTypes(DamageType type, float r, float g, float b)
    {
        var color = HitFlashPalette.ColorFor(type);
        color.R.Should().Be(r);
        color.G.Should().Be(g);
        color.B.Should().Be(b);
    }

    [Fact]
    public void IgnisCombustionState_HeatBuildsAndTransitionsStages()
    {
        var state = new IgnisCombustionState();
        state.CurrentHeat.Should().Be(0f);
        state.Stage.Should().Be(CombustionStage.Normal);

        state.AddHeat(55f);
        state.CurrentHeat.Should().Be(55f);
        state.Stage.Should().Be(CombustionStage.Blazing);

        state.AddHeat(50f);
        state.CurrentHeat.Should().Be(100f);
        state.Stage.Should().Be(CombustionStage.Overheat);

        float vented = state.VentHeat(30f);
        vented.Should().Be(30f);
        state.CurrentHeat.Should().Be(70f);
        state.Stage.Should().Be(CombustionStage.Blazing);

        float consumed = state.ConsumeAllHeat();
        consumed.Should().Be(70f);
        state.CurrentHeat.Should().Be(0f);
        state.Stage.Should().Be(CombustionStage.Normal);
    }

    [Fact]
    public void IgnisCombustionState_CalculatesNovaScalingAccurately()
    {
        float zeroHeatDmg = IgnisCombustionState.CalcNovaDamage(100f, 0f);
        float fullHeatDmg = IgnisCombustionState.CalcNovaDamage(100f, 100f);
        float fullHeatRadius = IgnisCombustionState.CalcNovaRadius(280f, 100f);

        zeroHeatDmg.Should().Be(100f);
        fullHeatDmg.Should().Be(250f);
        fullHeatRadius.Should().Be(400f);
    }

    [Fact]
    public void ToxisCatalystState_StacksCorrosionAndCalculatesAmplification()
    {
        var state = new ToxisCatalystState();
        state.GetCorrosion(101).Should().Be(0);

        state.AddCorrosion(101, 2);
        state.GetCorrosion(101).Should().Be(2);
        state.GetDamageAmplification(101).Should().BeApproximately(1.20f, 0.001f);
        state.GetSpeedMultiplier(101).Should().BeApproximately(0.84f, 0.001f);

        state.AddCorrosion(101, 10);
        state.GetCorrosion(101).Should().Be(5);
        state.GetDamageAmplification(101).Should().BeApproximately(1.50f, 0.001f);

        var detonated = state.DetonateAll();
        detonated.Should().HaveCount(1);
        detonated[0].Key.Should().Be(101);
        detonated[0].Value.Should().Be(5);
        state.GetCorrosion(101).Should().Be(0);

        float ruptureDmg = ToxisCatalystState.CalcRuptureDamage(80f, 5);
        ruptureDmg.Should().Be(205f);
        float siphon = ToxisCatalystState.CalcSiphonHealing(5);
        siphon.Should().Be(15f);
    }

    [Fact]
    public void HavocArtilleryState_CalculatesRecoilAndClusterDirections()
    {
        var aimDir = new Godot.Vector2(1f, 0f);
        var recoil = HavocArtilleryState.CalcRecoilImpulse(aimDir, 300f);
        recoil.X.Should().Be(-300f);
        recoil.Y.Should().Be(0f);

        var clusters = HavocArtilleryState.CalcClusterSpreadDirections(aimDir, 3, 0.4f);
        clusters.Should().HaveCount(3);

        var strikes = HavocArtilleryState.GetSequentialStrikes();
        strikes.Should().HaveCount(3);
        strikes[2].DamageMultiplier.Should().Be(1.4f);
    }
}
