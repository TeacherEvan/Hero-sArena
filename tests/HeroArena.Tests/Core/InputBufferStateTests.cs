using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

// T3.1-rest final extraction: the InputBuffer logic, pulled out of the Node
// so the last #[Trait("Category","GodotRuntime")] suite retires. Replaces
// InputBufferTests (which built the Node via the deprecated
// FormatterServices.GetUninitializedObject bypass and faked the StringName
// key the same way). Every assertion those tests made lives here against
// the pure state; the Node's StringName boundary and its tick→expire
// ordering are guarded by TestInputBufferDelegation in the Godot gate.
public class InputBufferStateTests
{
    // ── Buffer membership ─────────────────────────────────────────────────

    [Fact]
    public void BufferAction_AddsActionToBuffer()
    {
        var buffer = new InputBufferState();

        buffer.BufferAction("attack");

        buffer.IsBuffered("attack").Should().BeTrue();
        buffer.BufferedCount.Should().Be(1);
    }

    [Fact]
    public void ConsumeAction_RemovesEntry_AndFreesTheSlot()
    {
        var buffer = new InputBufferState();
        buffer.BufferAction("dash");
        buffer.BufferAction("attack");

        buffer.ConsumeAction("dash").Should().BeTrue();

        buffer.BufferedCount.Should().Be(1);
        buffer.IsBuffered("dash").Should().BeFalse();
        buffer.IsBuffered("attack").Should().BeTrue();
    }

    [Fact]
    public void ConsumeAction_WhenNotBuffered_ReturnsFalse()
    {
        new InputBufferState().ConsumeAction("dash").Should().BeFalse();
    }

    [Fact]
    public void IsBuffered_DoesNotConsume()
    {
        var buffer = new InputBufferState();
        buffer.BufferAction("attack");

        buffer.IsBuffered("attack").Should().BeTrue();
        buffer.IsBuffered("attack").Should().BeTrue();
        buffer.BufferedCount.Should().Be(1);
    }

    [Fact]
    public void NullKey_MatchesNullEntry_PreservingStringNameNullBranch()
    {
        // Preserved from the pre-extraction code (the `null == null` branch
        // the StringName fakes relied on): a stored null and a null key
        // count as a match, on both IsBuffered and ConsumeAction.
        var buffer = new InputBufferState();

        buffer.BufferAction(null!);

        buffer.IsBuffered(null!).Should().BeTrue();
        buffer.ConsumeAction(null!).Should().BeTrue();
        buffer.BufferedCount.Should().Be(0);
    }

    // ── Expiry (frame-driven, reflection-free) ────────────────────────────

    [Fact]
    public void ProcessBuffer_RemovesEntriesOlderThanBufferFrames()
    {
        var buffer = new InputBufferState();
        buffer.BufferAction("attack");
        buffer.IsBuffered("attack").Should().BeTrue();

        for (int frame = 0; frame < InputBufferState.BufferFrames; frame++)
            buffer.TickFrame();

        buffer.ProcessBuffer();

        buffer.IsBuffered("attack").Should().BeFalse();
        buffer.BufferedCount.Should().Be(0);
    }

    [Fact]
    public void EntriesSurvive_BufferFramesMinusOne()
    {
        // Boundary: expiring happens at the 6th frame AFTER stamping — the
        // buffer window is inclusive of BufferFrames - 1 elapsed ticks.
        var buffer = new InputBufferState();
        buffer.BufferAction("attack");

        for (int frame = 0; frame < InputBufferState.BufferFrames - 1; frame++)
            buffer.TickFrame();

        buffer.ProcessBuffer();

        buffer.IsBuffered("attack").Should().BeTrue();
    }

    [Fact]
    public void FreshEntries_StayInsideWindow_WhileStaleOnesExpire()
    {
        // Spawn-buffer realism: an entry far past its window dies, while a
        // recent one stamped mid-clock still has room before expiry. With
        // BufferFrames=6: "old" (stamp 0), 30 ticks, "fresh" (stamp 30), 5
        // ticks → elapsed 5 < 6 for "fresh", 35 ≥ 6 for "old".
        var buffer = new InputBufferState();
        buffer.BufferAction("old");

        for (int frame = 0; frame < 30; frame++)
        {
            buffer.TickFrame();
        }
        buffer.BufferAction("fresh");

        for (int frame = 0; frame < InputBufferState.BufferFrames - 1; frame++)
            buffer.TickFrame();

        buffer.ProcessBuffer();

        buffer.IsBuffered("old").Should().BeFalse();
        buffer.IsBuffered("fresh").Should().BeTrue();
    }

    // ── Fixed capacity ────────────────────────────────────────────────────

    [Fact]
    public void BufferFill_FillsToCapacity_ThenDropsTheRest()
    {
        var buffer = new InputBufferState();

        for (int i = 0; i < InputBufferState.BufferCapacity; i++)
            buffer.BufferAction("attack");
        buffer.BufferedCount.Should().Be(InputBufferState.BufferCapacity);

        // 33rd insert: silently dropped (capacity is fixed, no growth).
        Action addOverLimit = () => buffer.BufferAction("attack");
        addOverLimit.Should().NotThrow();
        buffer.BufferedCount.Should().Be(InputBufferState.BufferCapacity);
    }

    [Fact]
    public void BufferCapacity_ConsumedSlotsAreReusable_AfterConsumeOrExpiry()
    {
        var buffer = new InputBufferState();
        for (int i = 0; i < InputBufferState.BufferCapacity; i++)
            buffer.BufferAction("attack");

        buffer.ConsumeAction("attack").Should().BeTrue();
        buffer.BufferedCount.Should().Be(InputBufferState.BufferCapacity - 1);

        // Exactly one slot freed — filling to capacity again works.
        buffer.BufferAction("dash");
        buffer.BufferedCount.Should().Be(InputBufferState.BufferCapacity);
        buffer.IsBuffered("dash").Should().BeTrue();
    }
}
