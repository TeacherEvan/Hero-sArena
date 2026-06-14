using System;
using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

public class LevelProgressionTests
{
    private readonly LevelProgression _levelProgression = new();

    [Theory]
    [InlineData(1, 100f, 115f)]
    [InlineData(2, 100f, 130f)]
    [InlineData(5, 100f, 175f)]
    [InlineData(10, 100f, 250f)]
    public void CalcKineticDamage_LinearScaling_ComputesCorrectly(int level, float baseDamage, float expected)
    {
        // Act
        var result = _levelProgression.CalcKineticDamage(baseDamage, level);

        // Assert
        result.Should().BeApproximately(expected, 0.001f);
    }

    [Theory]
    [InlineData(1, 100f, 100f)] // 1^1.3 = 1
    [InlineData(2, 100f, 246.2f)] // 2^1.3 * 100
    [InlineData(5, 100f, 812.8f)] // 5^1.3 * 100
    [InlineData(10, 100f, 1995.3f)] // 10^1.3 * 100
    public void CalcEnergyDamage_PowerLaw_ComputesCorrectly(int level, float baseDamage, float expected)
    {
        // Act
        var result = _levelProgression.CalcEnergyDamage(baseDamage, level);

        // Assert - allow slightly wider tolerance for floating point
        result.Should().BeApproximately(expected, 1f);
    }

    [Fact]
    public void CalcKarmaAmplifier_LogarithmicGrowth_IncreasesWithDestruction()
    {
        // Act
        var result0 = _levelProgression.CalcKarmaAmplifier(0);
        var result10 = _levelProgression.CalcKarmaAmplifier(10);
        var result100 = _levelProgression.CalcKarmaAmplifier(100);

        // Assert
        result0.Should().Be(MathF.Log(MathF.E)); // ln(e + 0) = 1
        result10.Should().BeGreaterThan(result0);
        result100.Should().BeGreaterThan(result10);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 160)] // 100 + 1*50 + 1*10
    [InlineData(5, 450)] // 100 + 4*50 + 16*10
    [InlineData(10, 1450)] // 100 + 9*50 + 81*10
    public void XpRequiredForLevel_QuadraticFormula_ComputesCorrectly(int level, int expected)
    {
        // Act
        var result = _levelProgression.XpRequiredForLevel(level);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetRandomPerks_ReturnsDistinctPerks_CountMatchesRequest()
    {
        // Act
        var perks = _levelProgression.GetRandomPerks(3);

        // Assert
        perks.Should().HaveCount(3);
        perks.Should().OnlyHaveUniqueItems();
        perks.Should().AllSatisfy(p => Enum.IsDefined(typeof(PerkType), p).Should().BeTrue());
    }

    [Fact]
    public void GetRandomPerks_WhenCountExceedsAvailable_ReturnsAllAvailable()
    {
        // This acts as a boundary test - if we somehow request more than available
        var perks = _levelProgression.GetRandomPerks(50);

        // Should return at most all available perks
        perks.Length.Should().BeLessOrEqualTo(Enum.GetValues(typeof(PerkType)).Length);
    }
}
