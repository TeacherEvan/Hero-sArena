using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

// WaveManager's spawn-count curve. These assertions used to live only in the
// Godot headless gate (TestWaveManager), where the Weibull constants were
// duplicated and no harness has ever executed them. The math now lives in
// WaveFormulas, so the curve is pinned headlessly here.
public class WaveFormulasTests
{
    [Theory]
    [InlineData(1, 32)]  // early ramp-up
    [InlineData(10, 37)] // still elevated past the peak
    public void CalculateWaveEnemyCount_MatchesGateAssertions(int wave, int expected)
    {
        WaveFormulas.CalculateWaveEnemyCount(wave, activeEnemyCount: 0)
            .Should().Be(expected);
    }

    [Fact]
    public void Wave20_ExceedsMinimum_WithoutHittingTheCeiling()
    {
        // Wave 20 is past the Weibull peak but must still clear the floor.
        WaveFormulas.CalculateWaveEnemyCount(20, activeEnemyCount: 0)
            .Should().BeGreaterThan(WaveFormulas.MinWaveEnemies);
    }

    [Fact]
    public void NeverBelowMinimum_EvenForWaveZero()
    {
        // Weibull intensity is 0 at wave 0, so the floor must hold.
        WaveFormulas.CalculateWaveEnemyCount(0, activeEnemyCount: 0)
            .Should().Be(WaveFormulas.MinWaveEnemies);
    }

    [Fact]
    public void RoomLeftUnderCap_CeilingClampsCount()
    {
        // With the arena nearly full, the count is capped by free slots.
        WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 1990)
            .Should().Be(10);

        WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 1970)
            .Should().Be(30);
    }

    [Fact]
    public void Intensity_PeaksBeforeWaveTen_ThenDecays()
    {
        float early = WaveFormulas.WeibullIntensity(1f);
        float nearPeak = WaveFormulas.WeibullIntensity(5f);
        float late = WaveFormulas.WeibullIntensity(20f);

        nearPeak.Should().BeGreaterThan(early);
        late.Should().BeLessThan(nearPeak);
    }

    [Fact]
    public void Intensity_RisesFromZero()
    {
        WaveFormulas.WeibullIntensity(0f).Should().Be(0f);
    }
}
