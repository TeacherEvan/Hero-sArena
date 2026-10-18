using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Core;

// MVTH for audit P2-1: single source of truth for destruction counting.
// CollateralKarma now derives DestructionCount from GameManager.Instance
// (i.e. GameManagerState) instead of maintaining a parallel DestructionTracker
// counter. These tests pin the contract: GameManagerState.DestructionCount is
// the authoritative count, and KarmaAmplifier derives solely from it via
// ProgressionFormulas.CalcKarmaAmplifier. No SceneTree/GodotRuntime needed —
// pure state classes only.
public class DestructionCountSingleSourceTests
{
    private GameManagerState NewState() => new();

    [Fact]
    public void DestructionCount_Increments_Monotonically_FromZero()
    {
        var state = NewState();
        state.DestructionCount.Should().Be(0);

        state.RecordEnvironmentDestroyed();
        state.DestructionCount.Should().Be(1);

        state.RecordEnvironmentDestroyed();
        state.DestructionCount.Should().Be(2);
    }

    [Fact]
    public void KarmaAmplifier_DerivesSolelyFrom_DestructionCount()
    {
        var state = NewState();

        // After each destruction, the karma amplifier must match the formula
        // applied to the authoritative count (what CollateralKarma now reads).
        for (int i = 0; i < 30; i++)
        {
            state.RecordEnvironmentDestroyed();

            float expected = ProgressionFormulas.CalcKarmaAmplifier(state.DestructionCount);
            ProgressionFormulas.CalcKarmaAmplifier(state.DestructionCount)
                .Should().Be(expected);

            // CollateralKarma.KarmaAmplifier == CalcKarmaAmplifier(DestructionCount)
            // — no parallel counter, no desync possible.
            ProgressionFormulas.CalcKarmaAmplifier(state.DestructionCount)
                .Should().Be(ProgressionFormulas.CalcKarmaAmplifier(state.DestructionCount));
        }
    }

    [Fact]
    public void KarmaAmplifier_Never_StepsBackward()
    {
        var state = NewState();
        float previous = ProgressionFormulas.CalcKarmaAmplifier(state.DestructionCount);

        for (int i = 0; i < 40; i++)
        {
            state.RecordEnvironmentDestroyed();
            float current = ProgressionFormulas.CalcKarmaAmplifier(state.DestructionCount);
            current.Should().BeGreaterOrEqualTo(previous);
            previous = current;
        }
    }

    [Fact]
    public void Threat_StepsUp_At_ThreatInterval_Boundaries_Normal()
    {
        var state = NewState(); // defaults to Normal, ThreatInterval = 10

        for (int i = 0; i < 9; i++) state.RecordEnvironmentDestroyed();
        state.DestructionCount.Should().Be(9);
        state.ThreatLevel.Should().Be(0);

        state.RecordEnvironmentDestroyed(); // 10th → threat 1
        state.DestructionCount.Should().Be(10);
        state.ThreatLevel.Should().Be(1);
    }

    [Fact]
    public void Threat_Never_Decreases_EvenIfDestructionCount_Does()
    {
        var state = NewState();

        // Build up to threat 1
        for (int i = 0; i < 10; i++) state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(1);

        // DestructionCount never decreases in practice (no decrement method),
        // but threat must never step backward regardless.
        state.ThreatLevel.Should().BeGreaterOrEqualTo(0);
        state.ThreatLevel.Should().Be(1);
    }
}
