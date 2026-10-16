using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// CollateralKarma's Node-level behavior (start at 0, increment per
// OnEnvironmentDestroyed, amplifier delegates to ProgressionFormulas) — the
// part the Godot gate covered in TestCollateralKarmaBehavior and that xUnit
// could not reach without the runtime. DestructionTracker is the Godot-free
// state; the Node only forwards events into it.
public class DestructionTrackerTests
{
    [Fact]
    public void StartsAtZeroWithUnitAmplifier()
    {
        var tracker = new DestructionTracker();

        tracker.DestructionCount.Should().Be(0);
        tracker.KarmaAmplifier.Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void Record_IncrementsCount()
    {
        var tracker = new DestructionTracker();

        tracker.Record();
        tracker.Record();
        tracker.Record();

        tracker.DestructionCount.Should().Be(3);
    }

    [Fact]
    public void Amplifier_DelegatesToProgressionFormulas()
    {
        var tracker = new DestructionTracker();
        for (int i = 0; i < 7; i++) tracker.Record();

        tracker.KarmaAmplifier
            .Should().BeApproximately(ProgressionFormulas.CalcKarmaAmplifier(7), 0.0001f);
    }

    [Fact]
    public void Amplifier_IncreasesMonotonicallyWithDestructions()
    {
        var tracker = new DestructionTracker();
        var previous = tracker.KarmaAmplifier;

        for (int i = 0; i < 20; i++)
        {
            tracker.Record();
            var amplifier = tracker.KarmaAmplifier;
            amplifier.Should().BeGreaterThan(previous);
            previous = amplifier;
        }
    }

    [Fact]
    public void Reset_ReturnsToZero()
    {
        var tracker = new DestructionTracker();
        tracker.Record();
        tracker.Record();

        tracker.Reset();

        tracker.DestructionCount.Should().Be(0);
    }
}
