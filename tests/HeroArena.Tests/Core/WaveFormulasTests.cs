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
    public void ArenaPastCapacity_YieldsNonPositiveCount_NeverThrows()
    {
        // BASE used Mathf.Clamp, which throws when the ceiling drops below
        // the floor (reachable near MaxEnemies active enemies mid-spawn).
        // An over-full arena now yields a non-positive count so the
        // caller's spawn loop simply runs zero times.
        WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 2000)
            .Should().BeLessThanOrEqualTo(0);

        WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 2500)
            .Should().BeLessThanOrEqualTo(0);
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

    [Fact]
    public void MaxEnemies_Cap_Is2000()
    {
        // The 2,000 concurrent-enemy ceiling moved here from WaveManager
        // (whose MAX_ENEMIES now delegates to this constant, and whose
        // spawn loop and bench headers reference it). Pin the raw value so
        // a silent retune of the ceiling is a visible test change.
        WaveFormulas.MaxEnemies.Should().Be(2000);
    }

    // ── Difficulty multiplier (R2) ──────────────────────────────────────────

    [Fact]
    public void CalculateWaveEnemyCount_EasyMultiplier_ReducesSpawnCount()
    {
        // Easy = 0.6 multiplier; wave 10 with no active enemies.
        int normal = WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 0, enemyCountMultiplier: 1f);
        int easy   = WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 0, enemyCountMultiplier: 0.6f);
        easy.Should().BeLessThan(normal);
    }

    [Fact]
    public void CalculateWaveEnemyCount_HardMultiplier_IncreasesSpawnCount()
    {
        // Hard = 1.4 multiplier; wave 10 with no active enemies.
        int normal = WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 0, enemyCountMultiplier: 1f);
        int hard   = WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 0, enemyCountMultiplier: 1.4f);
        hard.Should().BeGreaterThan(normal);
    }

    [Fact]
    public void CalculateWaveEnemyCount_Multiplier_StillFloorsAtMinimum()
    {
        // Even with a huge multiplier, the floor still applies.
        WaveFormulas.CalculateWaveEnemyCount(0, activeEnemyCount: 0, enemyCountMultiplier: 5f)
            .Should().Be(WaveFormulas.MinWaveEnemies);
    }

    [Fact]
    public void CalculateWaveEnemyCount_Multiplier_StillCeilsAtCap()
    {
        // With the arena nearly full, even a multiplier is capped by free slots.
        WaveFormulas.CalculateWaveEnemyCount(10, activeEnemyCount: 1990, enemyCountMultiplier: 5f)
            .Should().Be(10);
    }
}
