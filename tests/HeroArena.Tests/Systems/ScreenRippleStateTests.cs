using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// ScreenRippleState: the Godot-free core of the ScreenDistortionFX layer.
// Covers the ripple ring buffer, rate limiting / duplicate coalescing and
// the karma vignette (baseline + damage pulse + threat saturation curve).
public class ScreenRippleStateTests
{
    [Fact]
    public void TryAdd_AddsRipple_AndReportsActiveCount()
    {
        var state = new ScreenRippleState();

        bool added = state.TryAdd(0.5f, 0.5f, 0.3f, 0.25f, 0.6f, timeSinceLastAdd: 1f);

        added.Should().BeTrue();
        state.ActiveCount.Should().Be(1);
        var r = state.GetRipple(0);
        r.Active.Should().BeTrue();
        r.U.Should().Be(0.5f);
        r.V.Should().Be(0.5f);
        r.Strength.Should().Be(0.3f);
    }

    [Fact]
    public void TryAdd_RejectsSmallRipple_WhenLastAddWasTooRecent()
    {
        var state = new ScreenRippleState();

        bool added = state.TryAdd(0.1f, 0.1f, 0.2f, 0.2f, 0.5f, timeSinceLastAdd: 0f);

        added.Should().BeFalse();
        state.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void TryAdd_AcceptsSmallRipple_AfterRateLimitWindow()
    {
        var state = new ScreenRippleState();

        bool added = state.TryAdd(0.1f, 0.1f, 0.2f, 0.2f, 0.5f, timeSinceLastAdd: ScreenRippleState.MinAddInterval);

        added.Should().BeTrue();
    }

    [Fact]
    public void TryAdd_BigRipple_BypassesRateLimit()
    {
        var state = new ScreenRippleState();

        bool added = state.TryAdd(0.2f, 0.2f, ScreenRippleState.BypassStrength, 0.4f, 0.6f, timeSinceLastAdd: 0f);

        added.Should().BeTrue();
    }

    [Fact]
    public void TryAdd_SuppressesWeakerDuplicate_NearFreshRipple()
    {
        var state = new ScreenRippleState();
        state.TryAdd(0.5f, 0.5f, 0.4f, 0.3f, 0.6f, timeSinceLastAdd: 1f);

        // Same spot, fresh ripple, weaker new one: rejected despite elapsed time.
        bool added = state.TryAdd(0.51f, 0.505f, 0.2f, 0.3f, 0.6f, timeSinceLastAdd: 1f);

        added.Should().BeFalse();
        state.ActiveCount.Should().Be(1);
    }

    [Fact]
    public void TryAdd_AcceptsStrongerRipple_AtSameSpot()
    {
        var state = new ScreenRippleState();
        state.TryAdd(0.5f, 0.5f, 0.2f, 0.3f, 0.6f, timeSinceLastAdd: 1f);

        bool added = state.TryAdd(0.51f, 0.505f, 0.4f, 0.3f, 0.6f, timeSinceLastAdd: 1f);

        added.Should().BeTrue();
        state.ActiveCount.Should().Be(2);
    }

    [Fact]
    public void TryAdd_AcceptsFarRipple_EvenWhenOneIsFresh()
    {
        var state = new ScreenRippleState();
        state.TryAdd(0.2f, 0.2f, 0.4f, 0.3f, 0.6f, timeSinceLastAdd: 1f);

        bool added = state.TryAdd(0.8f, 0.8f, 0.2f, 0.3f, 0.6f, timeSinceLastAdd: 1f);

        added.Should().BeTrue();
    }

    [Fact]
    public void TryAdd_NeverExceedsCapacity()
    {
        var state = new ScreenRippleState();

        for (int i = 0; i < 30; i++)
            state.TryAdd(i / 30f, 0.5f, 0.9f, 0.3f, 0.6f, timeSinceLastAdd: 0f);

        state.ActiveCount.Should().BeLessThanOrEqualTo(ScreenRippleState.MaxRipples);
    }

    [Fact]
    public void Update_ExpiresRipples()
    {
        var state = new ScreenRippleState();
        state.TryAdd(0.5f, 0.5f, 0.3f, 0.3f, 0.2f, timeSinceLastAdd: 1f);

        state.Update(0.1f);
        state.ActiveCount.Should().Be(1);

        state.Update(0.1f);
        state.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Ripple_AmplitudeDecays_AndRadiusExpands()
    {
        var state = new ScreenRippleState();
        state.TryAdd(0.4f, 0.6f, 0.4f, 0.5f, 1f, timeSinceLastAdd: 1f);

        var start = state.GetRipple(0);
        start.Amplitude.Should().BeApproximately(0.4f, 0.001f);
        start.RingRadius01.Should().BeApproximately(0f, 0.001f);

        state.Update(0.5f);
        var mid = state.GetRipple(0);
        mid.Amplitude.Should().BeApproximately(0.1f, 0.001f); // (1-0.5)^2 = 0.25
        mid.RingRadius01.Should().BeApproximately(0.375f, 0.001f); // ease-out of 0.5
        mid.RingRadius01.Should().BeGreaterThan(start.RingRadius01);
    }

    [Fact]
    public void Vignette_BaselineConvergesTowardTarget_WithoutOvershoot()
    {
        var state = new ScreenRippleState();
        state.SetVignetteTarget(0.4f);

        state.Update(0.5f);

        state.BaseVignette.Should().BeGreaterThan(0f);
        state.BaseVignette.Should().BeLessThan(0.4f);

        state.Update(5f);
        state.BaseVignette.Should().BeApproximately(0.4f, 0.01f);
    }

    [Fact]
    public void Vignette_PulseRaisesValue_ThenDecays()
    {
        var state = new ScreenRippleState();
        state.PulseVignette(0.5f);
        state.Vignette.Should().BeApproximately(0.5f, 0.001f);

        state.Update(1f);

        state.VignetteBoost.Should().BeLessThan(0.5f);
        state.VignetteBoost.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Vignette_ClampsAtOne()
    {
        var state = new ScreenRippleState();
        state.SetVignetteTarget(1f);
        state.PulseVignette(0.9f);

        state.Vignette.Should().BeLessThanOrEqualTo(1f);
        state.PulseVignette(5f);
        state.Vignette.Should().Be(1f);
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(60, 0.45f)]
    public void ThreatVignette_IsZeroAtZeroAndSaturatesAtCap(int threat, float expected)
    {
        ScreenRippleState.ThreatVignette(threat).Should().BeApproximately(expected, 0.001f);
    }

    [Fact]
    public void ThreatVignette_IsMonotonicAndBounded()
    {
        float prev = 0f;
        for (int threat = 0; threat <= 30; threat++)
        {
            float v = ScreenRippleState.ThreatVignette(threat);
            v.Should().BeGreaterThanOrEqualTo(prev - 0.0001f);
            v.Should().BeLessThanOrEqualTo(0.451f);
            prev = v;
        }
    }

    [Fact]
    public void AnyWork_IsFalseWhenIdle_AndTrueWhileRippling()
    {
        var state = new ScreenRippleState();
        state.AnyWork.Should().BeFalse();

        state.TryAdd(0.5f, 0.5f, 0.3f, 0.3f, 0.2f, timeSinceLastAdd: 1f);
        state.AnyWork.Should().BeTrue();

        state.Update(0.5f); // ripple expires; nothing else moving
        state.AnyWork.Should().BeFalse();
    }

    [Fact]
    public void AnyWork_StaysTrue_UntilPulseAndBaselineSettle()
    {
        var state = new ScreenRippleState();
        state.SetVignetteTarget(0.3f);
        state.PulseVignette(0.4f);

        state.Update(0.1f);
        state.AnyWork.Should().BeTrue();

        for (int i = 0; i < 200; i++) state.Update(0.1f); // 20s: everything settles
        state.AnyWork.Should().BeFalse();
    }

    [Fact]
    public void ShouldRender_IsFalseWhenFullyIdle_TrueWhilePulsing()
    {
        var state = new ScreenRippleState();

        state.ShouldRender.Should().BeFalse(); // nothing to show: overlay hides

        state.PulseVignette(0.2f);
        state.ShouldRender.Should().BeTrue(); // damage pulse keeps it visible
    }

    [Fact]
    public void ShouldRender_StaysTrue_AfterVignetteSettles_SoTheMoodDoesNotPop()
    {
        var state = new ScreenRippleState();
        state.SetVignetteTarget(0.3f);

        for (int i = 0; i < 200; i++) state.Update(0.1f); // baseline settles

        state.AnyWork.Should().BeFalse();       // no per-frame work left
        state.ShouldRender.Should().BeTrue();   // but the settled vignette still shows
        state.Vignette.Should().BeApproximately(0.3f, 0.01f);
    }

    [Fact]
    public void TryAdd_ClampsTinyLife_ToMinimum()
    {
        var state = new ScreenRippleState();

        state.TryAdd(0.5f, 0.5f, 0.3f, 0.3f, life: 0.001f, timeSinceLastAdd: 1f);

        state.GetRipple(0).Life.Should().BeApproximately(0.05f, 0.0001f);
    }
}
