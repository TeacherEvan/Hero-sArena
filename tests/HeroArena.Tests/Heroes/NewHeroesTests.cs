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
}
