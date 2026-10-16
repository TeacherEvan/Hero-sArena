using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// Powerup banner slot selection, extracted from HUD.ShowPowerup. The old
// Godot gate guarded this via a source-text grep against a file that never
// existed (PowerupBannerFactory.cs), so it could never pass; the selection
// rule itself is pure and now covered headlessly.
public class PowerupBannerSlotsTests
{
    [Fact]
    public void SelectSlot_AllExpired_ReturnsFirst()
    {
        float[] timers = { 0f, 0f, 0f, 0f, 0f };

        PowerupBannerSlots.SelectSlot(timers).Should().Be(0);
    }

    [Fact]
    public void SelectSlot_FirstExpiredSlotWins()
    {
        float[] timers = { 5f, 0f, 2f, 0f, 1f };

        PowerupBannerSlots.SelectSlot(timers).Should().Be(1);
    }

    [Fact]
    public void SelectSlot_AllActive_ReturnsOldestBanner()
    {
        float[] timers = { 5f, 4f, 3f, 2f, 1f };

        PowerupBannerSlots.SelectSlot(timers).Should().Be(4);
    }

    [Fact]
    public void SelectSlot_NegativeTimersCountAsExpired()
    {
        float[] timers = { 5f, 5f, -1f, 5f, 5f };

        PowerupBannerSlots.SelectSlot(timers).Should().Be(2);
    }

    [Fact]
    public void SelectSlot_ReturnsValidIndexForEveryBannerCount()
    {
        for (int n = 1; n <= PowerupBannerSlots.MaxBanners; n++)
        {
            float[] timers = new float[n];
            PowerupBannerSlots.SelectSlot(timers).Should().Be(0);
        }
    }

    [Fact]
    public void Constants_MatchHudDefaults()
    {
        PowerupBannerSlots.MaxBanners.Should().Be(5);
        PowerupBannerSlots.Lifetime.Should().Be(5f);
    }
}
