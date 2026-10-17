using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Core;

// Synapse Overclock mind-control rules, extracted from EnemyBase into a pure
// state class (same pattern as InputBufferState / PowerupBannerSlots).
// EnemyBase delegates 1:1; the tint/target Node wiring stays on the Node,
// which the headless Godot gate cannot cover in this workspace (AGENTS.md).
public class MindControlStateTests
{
    private static MindControlState Controlled(float duration = 10f)
    {
        var state = new MindControlState();
        state.Apply(duration);
        return state;
    }

    // ── Timer ───────────────────────────────────────────────────────────────

    [Fact]
    public void NewState_IsNotControlled()
    {
        var state = new MindControlState();

        state.IsControlled.Should().BeFalse();
        state.Remaining.Should().Be(0f);
    }

    [Fact]
    public void Apply_StartsControl()
    {
        var state = Controlled();

        state.IsControlled.Should().BeTrue();
        state.Remaining.Should().Be(10f);
    }

    [Fact]
    public void Tick_BeforeExpiry_StaysControlled()
    {
        var state = Controlled();

        state.Tick(4f).Should().BeFalse();
        state.IsControlled.Should().BeTrue();
        state.Remaining.Should().Be(6f);
    }

    [Fact]
    public void Tick_AtExpiry_ReportsOnceAndClears()
    {
        var state = Controlled();

        state.Tick(10f).Should().BeTrue();
        state.IsControlled.Should().BeFalse();
        state.Remaining.Should().Be(0f);
        state.Tick(1f).Should().BeFalse(); // never reports a second expiry
    }

    [Fact]
    public void Apply_WhileControlled_RefreshesDuration()
    {
        var state = Controlled();
        state.Tick(4f);

        state.Apply(10f);

        state.IsControlled.Should().BeTrue();
        state.Remaining.Should().Be(10f);
    }

    // ── Contact hits ────────────────────────────────────────────────────────

    [Fact]
    public void ContactHit_RequiresControl()
    {
        var state = new MindControlState();

        state.TryConsumeContactHit(0f).Should().BeFalse();
    }

    [Fact]
    public void ContactHit_OutOfRange_IsDenied()
    {
        var state = Controlled();

        state.TryConsumeContactHit(MindControlState.ContactHitRange + 1f).Should().BeFalse();
    }

    [Fact]
    public void ContactHit_AtRangeBoundary_IsAllowed()
    {
        var state = Controlled();

        state.TryConsumeContactHit(MindControlState.ContactHitRange).Should().BeTrue();
    }

    [Fact]
    public void ContactHit_InRange_IsAllowedThenGatedByCooldown()
    {
        var state = Controlled();

        state.TryConsumeContactHit(10f).Should().BeTrue();
        state.TryConsumeContactHit(10f).Should().BeFalse(); // still on cooldown

        state.Tick(MindControlState.ContactHitCooldown);

        state.TryConsumeContactHit(10f).Should().BeTrue();
    }

    [Fact]
    public void Apply_ReArmsContactCooldown()
    {
        var state = Controlled();
        state.TryConsumeContactHit(10f).Should().BeTrue();

        state.Apply(10f); // re-cast mid-fight

        state.TryConsumeContactHit(10f).Should().BeTrue();
    }
}
