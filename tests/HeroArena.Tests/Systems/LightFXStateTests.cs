using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// LightFXState: the Godot-free core of the LivingLightRig. Covers slot
// recycling, the flash decay envelope, the rate-limit policy and ambient
// mood curves. (The karma vignette curve lives with its consumer:
// ScreenRippleStateTests.)
public class LightFXStateTests
{
    [Fact]
    public void Spawn_ActivatesSlot_AndReportsActiveCount()
    {
        var state = new LightFXState();

        int idx = state.Spawn(10f, 20f, 1f, 96f, 0.3f);

        idx.Should().BeInRange(0, LightFXState.SlotCount - 1);
        state.ActiveCount.Should().Be(1);
        var slot = state.GetSlot(idx);
        slot.Active.Should().BeTrue();
        slot.X.Should().Be(10f);
        slot.Y.Should().Be(20f);
        slot.Energy.Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void Spawn_UsesAllFreeSlots_BeforeOverwriting()
    {
        var state = new LightFXState();
        for (int i = 0; i < LightFXState.SlotCount; i++)
            state.Spawn(i, 0f, 0.5f, 64f, 10f);

        state.ActiveCount.Should().Be(LightFXState.SlotCount);

        // Pool full: the new flash overwrites the most-consumed slot; a fresh
        // same-age pool keeps the count at the cap.
        state.Spawn(99f, 99f, 1f, 64f, 10f);
        state.ActiveCount.Should().Be(LightFXState.SlotCount);
    }

    [Fact]
    public void Spawn_WhenFull_ReplacesMostConsumedSlot()
    {
        var state = new LightFXState();
        int first = state.Spawn(1f, 1f, 0.5f, 64f, 1f);
        for (int i = 1; i < LightFXState.SlotCount; i++)
            state.Spawn(i, 0f, 0.5f, 64f, 100f);
        // Age the first flash almost to its end so it is the most consumed.
        state.Update(0.9f);

        int idx = state.Spawn(2f, 2f, 0.5f, 64f, 1f);

        idx.Should().Be(first);
        state.GetSlot(idx).X.Should().Be(2f);
    }

    [Fact]
    public void Update_ExpiresSlotsAtEndOfLife()
    {
        var state = new LightFXState();
        state.Spawn(0f, 0f, 1f, 64f, 0.2f);

        state.Update(0.1f);
        state.ActiveCount.Should().Be(1);

        state.Update(0.1f);
        state.ActiveCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0.5f, 0.25f)]
    [InlineData(0.9f, 0.01f)]
    [InlineData(1f, 0f)]
    public void Envelope_IsQuadraticEaseOut(float ageFraction, float expected)
    {
        LightFXState.Envelope(ageFraction, 1f).Should().BeApproximately(expected, 0.001f);
    }

    [Fact]
    public void Envelope_ZeroLife_IsZero()
    {
        LightFXState.Envelope(0f, 0f).Should().Be(0f);
    }

    [Fact]
    public void Flicker_StaysWithinFifteenPercentBand()
    {
        for (int i = 0; i < 200; i++)
        {
            float f = LightFXState.Flicker(i * 0.017f, i * 0.37f);
            f.Should().BeInRange(0.85f, 1.15f);
        }
    }

    [Fact]
    public void Flicker_IsDeterministic()
    {
        LightFXState.Flicker(1.23f, 0.5f).Should().Be(LightFXState.Flicker(1.23f, 0.5f));
    }

    [Fact]
    public void Flicker_VariesWithTime()
    {
        // Guards against a degenerate constant flicker (the glow must breathe).
        LightFXState.Flicker(0f, 1f).Should().NotBe(LightFXState.Flicker(0.37f, 1f));
    }

    [Fact]
    public void Spawn_ClampsTinyLife_ToMinimum()
    {
        var state = new LightFXState();

        int idx = state.Spawn(10f, 20f, 1f, 96f, life: 0f);

        state.GetSlot(idx).Life.Should().BeApproximately(0.01f, 0.0001f);
    }

    [Fact]
    public void AllowsFlash_RateLimitsSmallFlashes_ButNotBypasses()
    {
        // Too soon after the last small flash: suppressed.
        LightFXState.AllowsFlash(0f, 0.7f).Should().BeFalse();
        // Enough time passed: allowed.
        LightFXState.AllowsFlash(LightFXState.MinFlashInterval, 0.7f).Should().BeTrue();
        // Explosion-sized flash ignores the interval entirely.
        LightFXState.AllowsFlash(0f, LightFXState.BypassEnergy).Should().BeTrue();
    }

    [Fact]
    public void MoodTarget_RisesWithThreat_AndBossAddsMore()
    {
        float calm = LightFXState.MoodTarget(false, 0);
        float hot = LightFXState.MoodTarget(false, 5);
        float boss = LightFXState.MoodTarget(true, 5);

        hot.Should().BeGreaterThan(calm);
        boss.Should().BeGreaterThan(hot);
        boss.Should().BeLessThanOrEqualTo(1.4f);
        calm.Should().BeGreaterThanOrEqualTo(0.6f);
    }

    [Fact]
    public void Ambient_ApproachesTarget_WithoutOvershoot()
    {
        var state = new LightFXState();
        state.SetAmbientTarget(1.0f);

        state.UpdateAmbient(1f);

        state.AmbientEnergy.Should().BeGreaterThan(0.9f);
        state.AmbientEnergy.Should().BeLessThan(1.0f);

        state.UpdateAmbient(10f);
        state.AmbientEnergy.Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void ImpactFlash_AllDamageTypesReturnPositiveValues_AndExplosivePunchesHardest()
    {
        foreach (DamageType type in System.Enum.GetValues<DamageType>())
        {
            var (energy, radius, life) = LightFXState.ImpactFlash(type);
            energy.Should().BeGreaterThan(0f);
            radius.Should().BeGreaterThan(0f);
            life.Should().BeGreaterThan(0f);
        }

        LightFXState.ImpactFlash(DamageType.Explosive).Energy
            .Should().BeGreaterThan(LightFXState.ImpactFlash(DamageType.Acid).Energy);
        // Explosive impacts bypass the rate limit by design.
        LightFXState.ImpactFlash(DamageType.Explosive).Energy
            .Should().BeGreaterThanOrEqualTo(LightFXState.BypassEnergy);
    }

    [Theory]
    [InlineData(10f, 96f)]
    [InlineData(500f, 320f)]
    public void ExplosionFlash_ClampsRadius(float blastRadius, float expectedRadius)
    {
        LightFXState.ExplosionFlash(blastRadius).Radius.Should().BeApproximately(expectedRadius, 0.001f);
    }

    [Fact]
    public void ExplosionAndHeroFlashes_BypassRateLimit()
    {
        LightFXState.ExplosionFlash(64f).Energy.Should().BeGreaterThanOrEqualTo(LightFXState.BypassEnergy);
        LightFXState.HeroDamageFlash().Energy.Should().BeGreaterThanOrEqualTo(LightFXState.BypassEnergy);
    }

    [Fact]
    public void Slot_RadiusScale_GrowsAsFlashFades()
    {
        var state = new LightFXState();
        int idx = state.Spawn(0f, 0f, 1f, 64f, 1f);

        state.GetSlot(idx).RadiusScale.Should().BeApproximately(1f, 0.001f);
        state.Update(0.75f);
        state.GetSlot(idx).RadiusScale.Should().BeGreaterThan(1f);
    }

    [Fact]
    public void GetSlot_CopyDoesNotAffectState()
    {
        var state = new LightFXState();
        int idx = state.Spawn(1f, 2f, 1f, 64f, 1f);

        var copy = state.GetSlot(idx);
        copy.X = 999f;

        state.GetSlot(idx).X.Should().Be(1f);
    }
}
