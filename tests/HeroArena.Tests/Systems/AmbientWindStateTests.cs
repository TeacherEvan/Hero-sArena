using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// AmbientWindState: the Godot-free wind field behind the living-arena layers.
// Covers the deterministic gust clock, impulse pooling/decay, sampling
// bounds, and the shared speed clamp.
public class AmbientWindStateTests
{
    [Fact]
    public void Update_AdvancesTime_AndIgnoresNonPositiveDt()
    {
        var wind = new AmbientWindState();

        wind.Update(0.5f);
        wind.Update(0f);
        wind.Update(-1f);

        wind.Time.Should().BeApproximately(0.5f, 0.0001f);
    }

    [Fact]
    public void BaseWind_IsDeterministic_ForSameTime()
    {
        var a = new AmbientWindState();
        var b = new AmbientWindState();
        a.Update(3.7f);
        b.Update(3.7f);

        var (ax, ay) = a.BaseWind();
        var (bx, by) = b.BaseWind();

        ax.Should().Be(bx);
        ay.Should().Be(by);
    }

    [Fact]
    public void BaseWind_StaysInsideItsDocumentedEnvelope_OverLongHorizon()
    {
        var wind = new AmbientWindState();
        float max = 0f;
        for (int i = 0; i < 600; i++)
        {
            wind.Update(0.25f);
            var (x, y) = wind.BaseWind();
            max = MathF.Max(max, MathF.Sqrt(x * x + y * y));
        }

        // Alive but bounded: the base wave peaks near 48 px/s — well below the
        // 90 px/s hard cap, which only gust amplification can approach.
        max.Should().BeInRange(30f, 55f);
    }

    [Fact]
    public void GustStrength_BoundedInUnitRange_OverFullPeriod()
    {
        var wind = new AmbientWindState();
        for (int i = 0; i < 200; i++)
        {
            wind.Update(0.1f);
            wind.GustStrength().Should().BeInRange(0f, 1f);
        }
    }

    [Fact]
    public void GustStrength_RisesToPeak_ThenReleasesToZero()
    {
        var wind = new AmbientWindState();

        wind.Update(AmbientWindState.GustRise); // exactly at the peak
        wind.GustStrength().Should().BeApproximately(1f, 0.001f);

        wind.Update(AmbientWindState.GustRelease + 0.5f); // past rise+release
        wind.GustStrength().Should().Be(0f);
    }

    [Fact]
    public void GustStrength_IsPeriodic()
    {
        var wind = new AmbientWindState();
        wind.Update(1.4f);
        float first = wind.GustStrength();

        wind.Update(AmbientWindState.GustPeriod);
        wind.GustStrength().Should().BeApproximately(first, 0.0001f);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0.5f, 1.675f)]
    [InlineData(1f, 2.35f)]
    [InlineData(-1f, 1f)]   // clamped low
    [InlineData(2f, 2.35f)] // clamped high
    public void GustGain_MapsEnvelopeToMultiplier(float gust, float expected)
    {
        AmbientWindState.GustGain(gust).Should().BeApproximately(expected, 0.0001f);
    }

    [Fact]
    public void Wind_ReachesButNeverBreaksItsCap_ThroughTheWholeGust()
    {
        var wind = new AmbientWindState();
        float max = 0f;
        for (float t = 0f; t < AmbientWindState.GustPeriod; t += 0.05f)
        {
            wind.Update(0.05f);
            var (x, y) = wind.Wind();
            max = MathF.Max(max, MathF.Sqrt(x * x + y * y));
        }

        max.Should().BeLessThanOrEqualTo(AmbientWindState.MaxBaseSpeed + 0.001f,
            "the cap holds through the whole gust");
        max.Should().BeGreaterThan(AmbientWindState.MaxBaseSpeed - 2f,
            "the peak gust actually reaches the cap");
    }

    [Fact]
    public void AddImpulse_ActivatesSlot_AndReportsCount()
    {
        var wind = new AmbientWindState();

        wind.AddImpulse(100f, 100f, 120f, 300f, 0.6f);

        wind.ActiveImpulseCount.Should().Be(1);
    }

    [Fact]
    public void AddImpulse_UsesFreeSlotsBeforeOverwriting()
    {
        var wind = new AmbientWindState();
        for (int i = 0; i < AmbientWindState.MaxImpulses; i++)
            wind.AddImpulse(i, 0f, 100f, 200f, 10f);

        wind.ActiveImpulseCount.Should().Be(AmbientWindState.MaxImpulses);

        wind.AddImpulse(99f, 99f, 100f, 200f, 10f);
        wind.ActiveImpulseCount.Should().Be(AmbientWindState.MaxImpulses);
    }

    [Fact]
    public void AddImpulse_WhenFull_ReplacesTheMostConsumed_NotTheFirstSlot()
    {
        var wind = new AmbientWindState();
        for (int i = 0; i < AmbientWindState.MaxImpulses; i++)
            wind.AddImpulse(i * 1000f, 0f, 100f, 50f, i == 3 ? 1f : 100f);

        wind.Update(0.9f); // slot 3 is ~90% consumed; the rest are under 1%

        // The fresh impulse must take slot 3 — a first-slot or first-free
        // implementation would evict slot 0 (or drop the impulse) instead.
        wind.AddImpulse(900000f, 0f, 7777f, 200f, 50f);

        // The dead slot-3 impulse is gone from the field…
        var (oldX, _) = wind.ImpulseAt(3010f, 0f);
        oldX.Should().Be(0f);

        // …and the replacement pushes outward at its own epicentre.
        var (newX, _) = wind.ImpulseAt(900010f, 0f);
        newX.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void ImpulseAt_PushesAwayFromEpicentre()
    {
        var wind = new AmbientWindState();
        wind.AddImpulse(0f, 0f, 200f, 400f, 1f);

        var (rightX, rightY) = wind.ImpulseAt(80f, 0f);
        var (leftX, leftY) = wind.ImpulseAt(-80f, 0f);
        var (_, downY) = wind.ImpulseAt(0f, 80f);

        rightX.Should().BeGreaterThan(0f);
        rightY.Should().Be(0f);
        leftX.Should().BeLessThan(0f);
        downY.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void ImpulseAt_IsZero_OutsideRadius_AndAtEpicentre()
    {
        var wind = new AmbientWindState();
        wind.AddImpulse(0f, 0f, 200f, 100f, 1f);

        var (farX, farY) = wind.ImpulseAt(150f, 0f);
        var (centerX, centerY) = wind.ImpulseAt(0f, 0f);

        farX.Should().Be(0f);
        farY.Should().Be(0f);
        centerX.Should().Be(0f);
        centerY.Should().Be(0f);
    }

    [Fact]
    public void ImpulseAt_DecaysToZero_AfterLifetime()
    {
        var wind = new AmbientWindState();
        wind.AddImpulse(0f, 0f, 200f, 400f, 0.5f);

        wind.Update(0.2f);
        var (midX, _) = wind.ImpulseAt(50f, 0f);
        midX.Should().BeGreaterThan(0f);

        wind.Update(0.4f); // past the 0.5 s lifetime
        var (lateX, lateY) = wind.ImpulseAt(50f, 0f);
        lateX.Should().Be(0f);
        lateY.Should().Be(0f);
        wind.ActiveImpulseCount.Should().Be(0);
    }

    [Fact]
    public void Sample_CombinesBaseWindAndImpulses()
    {
        var wind = new AmbientWindState();
        wind.Update(2f);
        wind.AddImpulse(0f, 0f, 200f, 400f, 1f);

        var (wx, wy) = wind.Wind();
        var (ix, iy) = wind.ImpulseAt(50f, 50f);
        var (sx, sy) = wind.Sample(50f, 50f);

        sx.Should().BeApproximately(wx + ix, 0.0001f);
        sy.Should().BeApproximately(wy + iy, 0.0001f);
    }

    [Fact]
    public void ClampSpeed_LeavesSmallVectorsUntouched_AndCapsLargeOnes()
    {
        var (sx, sy) = AmbientWindState.ClampSpeed(10f, 10f, 90f);
        sx.Should().Be(10f);
        sy.Should().Be(10f);

        var (bx, by) = AmbientWindState.ClampSpeed(200f, 0f, 90f);
        bx.Should().BeApproximately(90f, 0.001f);
        by.Should().Be(0f);
    }

    [Fact]
    public void ExplosionImpulse_ScalesWithBlastRadius()
    {
        var small = AmbientWindState.ExplosionImpulse(32f);
        var large = AmbientWindState.ExplosionImpulse(160f);

        small.Strength.Should().BeGreaterThan(0f);
        small.Radius.Should().BeGreaterThan(0f);
        small.Life.Should().BeGreaterThan(0f);

        large.Strength.Should().BeGreaterThan(small.Strength);
        large.Radius.Should().BeGreaterThan(small.Radius);
        large.Life.Should().BeGreaterThan(small.Life);
    }

    [Fact]
    public void ImpulseForHit_MapsBlastSchoolsOnly()
    {
        // Blast-like schools reproduce the standard explosion curve…
        AmbientWindState.ImpulseForHit(DamageType.Explosive)
            .Should().Be(AmbientWindState.ExplosionImpulse(AmbientWindState.ExplosiveHitRadius));
        AmbientWindState.ImpulseForHit(DamageType.Acid)
            .Should().Be(AmbientWindState.ExplosionImpulse(AmbientWindState.AcidHitRadius));

        // …while bullet-weight schools never shake the weather.
        AmbientWindState.ImpulseForHit(DamageType.Kinetic).Should().Be((0f, 0f, 0f));
        AmbientWindState.ImpulseForHit(DamageType.Energy).Should().Be((0f, 0f, 0f));
        AmbientWindState.ImpulseForHit(DamageType.Fire).Should().Be((0f, 0f, 0f));
        AmbientWindState.ImpulseForHit(DamageType.Lightning).Should().Be((0f, 0f, 0f));
    }

    [Fact]
    public void ExplosionImpulse_ClampsTinyBlastsToAMinimum()
    {
        var tiny = AmbientWindState.ExplosionImpulse(2f);
        var minimum = AmbientWindState.ExplosionImpulse(24f);

        tiny.Should().Be(minimum);
    }
}
