using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

/// <summary>
/// DisturbanceMemoryState: the Godot-free scar memory behind ScarLayer.
/// Covers stamping, fade envelopes, radii, overlap clamping, lifetimes,
/// bounded capacity and determinism.
/// </summary>
public class DisturbanceMemoryStateTests
{
    private static DisturbanceMemoryState Make(int capacity = DisturbanceMemoryState.DefaultCapacity)
        => new(capacity);

    [Fact]
    public void Stamp_IsInvisibleUntilFadedIn_ThenFullStrength_ThenDies()
    {
        var m = Make();
        m.Stamp(DisturbanceMemoryState.ScarKind.Scorch, 100f, 100f, 40f, 0.9f);

        m.SampleScar(100f, 100f).Should().Be(0f); // age 0: fade-in not complete
        m.Update(2f);
        m.SampleScar(100f, 100f).Should().BeApproximately(0.9f, 0.01f); // faded in, full strength at centre
        m.Update(148f); // past its 150 s lifetime
        m.SampleScar(100f, 100f).Should().Be(0f);
        m.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void OutsideRadius_IsClean()
    {
        var m = Make();
        m.Stamp(DisturbanceMemoryState.ScarKind.Scorch, 0f, 0f, 10f, 1f);
        m.Update(2f);
        m.SampleScar(20f, 0f).Should().Be(0f);
        m.SampleScar(9.99f, 0f).Should().BeGreaterThan(0f);
    }

    [Fact]
    public void OverlappingStamps_SumAndClampToOne()
    {
        var m = Make();
        m.Stamp(DisturbanceMemoryState.ScarKind.Scorch, 0f, 0f, 40f, 0.9f);
        m.Stamp(DisturbanceMemoryState.ScarKind.Scorch, 0f, 0f, 40f, 0.9f);
        m.Update(2f);
        m.SampleScar(0f, 0f).Should().BeApproximately(1f, 0.001f); // clamped at saturation
    }

    [Fact]
    public void KindLifetimes_AreOrdered()
    {
        DisturbanceMemoryState.LifetimeFor(DisturbanceMemoryState.ScarKind.Print)
            .Should().BeLessThan(DisturbanceMemoryState.LifetimeFor(DisturbanceMemoryState.ScarKind.Crack));
        DisturbanceMemoryState.LifetimeFor(DisturbanceMemoryState.ScarKind.Crack)
            .Should().BeLessThan(DisturbanceMemoryState.LifetimeFor(DisturbanceMemoryState.ScarKind.Scorch));
    }

    [Fact]
    public void Envelope_IsZeroAtEndpoints_AndOneInTheMiddle()
    {
        var life = DisturbanceMemoryState.ScorchLifetime;
        DisturbanceMemoryState.EnvelopeFor(0f, life).Should().Be(0f);
        DisturbanceMemoryState.EnvelopeFor(life * 0.5f, life).Should().BeApproximately(1f, 0.001f);
        DisturbanceMemoryState.EnvelopeFor(life, life).Should().Be(0f);
    }

    [Fact]
    public void Capacity_IsBounded()
    {
        var m = new DisturbanceMemoryState(12);
        for (int i = 0; i < 100; i++)
            m.Stamp(DisturbanceMemoryState.ScarKind.Print, i, 0f, 3f, 0.3f);
        m.ActiveCount.Should().BeLessThanOrEqualTo(12);
    }

    [Fact]
    public void IsDeterministic()
    {
        var a = Make();
        var b = Make();
        for (int i = 0; i < 40; i++)
        {
            var kind = (DisturbanceMemoryState.ScarKind)(i % 3);
            a.Stamp(kind, i * 7f, i * 3f, 12f + i, 0.4f + i * 0.01f, i);
            b.Stamp(kind, i * 7f, i * 3f, 12f + i, 0.4f + i * 0.01f, i);
        }
        a.Update(5f);
        b.Update(5f);
        for (int i = 0; i < DisturbanceMemoryState.DefaultCapacity; i++)
        {
            var sa = a.GetScar(i);
            var sb = b.GetScar(i);
            sa.Active.Should().Be(sb.Active);
            if (sa.Active)
            {
                sa.X.Should().Be(sb.X);
                sa.Y.Should().Be(sb.Y);
                sa.Age.Should().Be(sb.Age);
                sa.Heading.Should().Be(sb.Heading);
            }
        }
        a.SampleScar(100f, 100f).Should().Be(b.SampleScar(100f, 100f));
    }
}
