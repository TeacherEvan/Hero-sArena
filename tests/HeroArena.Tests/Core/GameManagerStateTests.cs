using Xunit;
using FluentAssertions;
using HeroArena;
using System;

namespace HeroArena.Tests.Core;

// T3.1-rest: the GameManager run-state machine, extracted to the Godot-free
// GameManagerState so the whole transition/guard/threat surface runs
// headlessly. Replaces the retired GameManagerTests + GameManagerTests_State
// (which built managers via the deprecated FormatterServices.GetUninitializedObject
// bypass); every assertion those suites made lives here against the pure class.
// GameManager's remaining Node behavior (EventBus wiring, SceneTree.Paused,
// instance registration) is guarded by the Godot gate's
// TestGameManagerDelegation in CoreSystemTests.
public class GameManagerStateTests
{
    private GameManagerState NewState() => new();

    // ── Score ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddScore_AccumulatesPoints()
    {
        var state = NewState();

        state.AddScore(150);
        state.AddScore(50);

        state.Score.Should().Be(200);
    }

    [Fact]
    public void StartGame_ResetsScore_AndMetrics()
    {
        var state = NewState();
        state.SetState(GameState.GameOver);
        state.AddScore(999);
        state.SetThreatLevel(5);

        state.StartGame();

        state.CurrentState.Should().Be(GameState.Playing);
        state.Score.Should().Be(0);
        state.ThreatLevel.Should().Be(0);
        state.CurrentWave.Should().Be(0);
        state.KillCount.Should().Be(0);
        state.DestructionCount.Should().Be(0);
    }

    [Fact]
    public void StartGame_DoesNotResetActiveEnemyCount()
    {
        // Preserved quirk from the pre-extraction GameManager (docs: pin
        // sought-with no change): StartGame resets run metrics but not the
        // live enemy counter — a fresh game inherits stragglers until the
        // WaveManager cap check trims them.
        var state = NewState();
        state.IncrementEnemyCount();
        state.IncrementEnemyCount();
        state.IncrementEnemyCount();

        state.StartGame();

        state.ActiveEnemyCount.Should().Be(3);
    }

    // ── Live enemy count ─────────────────────────────────────────────────

    [Fact]
    public void IncrementEnemyCount_RoundsUpByOne()
    {
        var state = NewState();

        state.IncrementEnemyCount();

        state.ActiveEnemyCount.Should().Be(1);
    }

    [Fact]
    public void DecrementEnemyCount_DecreasesByOne()
    {
        var state = NewState();
        state.IncrementEnemyCount();
        state.IncrementEnemyCount();

        state.DecrementEnemyCount();

        state.ActiveEnemyCount.Should().Be(1);
    }

    [Fact]
    public void DecrementEnemyCount_FloorsAtZero()
    {
        var state = NewState();

        state.DecrementEnemyCount();
        state.DecrementEnemyCount();

        state.ActiveEnemyCount.Should().Be(0);
    }

    // ── Transitions & guards ─────────────────────────────────────────────

    [Fact]
    public void RequestPause_FromPlaying_MovesToPaused_AndSignalsTree()
    {
        var state = NewState();
        state.SetState(GameState.Playing);

        bool treePause = state.RequestPause();

        treePause.Should().BeTrue();
        state.CurrentState.Should().Be(GameState.Paused);
    }

    [Fact]
    public void RequestPause_FromAnyOtherState_IsNoOp()
    {
        foreach (var from in new[] { GameState.MainMenu, GameState.GameOver, GameState.LevelUp })
        {
            var state = NewState();
            state.SetState(from);

            bool treePause = state.RequestPause();

            treePause.Should().BeFalse();
            state.CurrentState.Should().Be(from);
        }
    }

    [Fact]
    public void RequestResume_FromPaused_MovesToPlaying_AndSignalsTree()
    {
        var state = NewState();
        state.SetState(GameState.Paused);

        bool treeUnpause = state.RequestResume();

        treeUnpause.Should().BeTrue();
        state.CurrentState.Should().Be(GameState.Playing);
    }

    [Fact]
    public void RequestResume_FromAnyOtherState_IsNoOp()
    {
        foreach (var from in new[] { GameState.MainMenu, GameState.Playing, GameState.GameOver, GameState.LevelUp })
        {
            var state = NewState();
            state.SetState(from);

            bool treeUnpause = state.RequestResume();

            treeUnpause.Should().BeFalse();
            state.CurrentState.Should().Be(from);
        }
    }

    [Fact]
    public void SetState_GameOver_MovesToGameOver_Unconditionally()
    {
        // GameManager.TriggerGameOver() delegates here bare — no from-state
        // guard (reached from Playing via hero death, or any state via boss).
        var state = NewState();
        state.SetState(GameState.Playing);

        state.SetState(GameState.GameOver);

        state.CurrentState.Should().Be(GameState.GameOver);
    }

    // ── Threat level ─────────────────────────────────────────────────────

    [Fact]
    public void SetThreatLevel_NewLevel_UpdatesAndSignalsEvent()
    {
        var state = NewState();

        bool emit = state.SetThreatLevel(5);

        emit.Should().BeTrue();
        state.ThreatLevel.Should().Be(5);
    }

    [Fact]
    public void SetThreatLevel_SameLevel_IsNoOpWithoutEvent()
    {
        var state = NewState();
        state.SetThreatLevel(5);

        bool emit = state.SetThreatLevel(5);

        emit.Should().BeFalse();
        state.ThreatLevel.Should().Be(5);
    }

    [Fact]
    public void RecordEnvironmentDestroyed_ThreatStepsUpEveryTen()
    {
        // Mirrors the retired GameManagerThreatLevelTests reflection run:
        // 1st-9th no signal, 10th signals + step, 11th-19th nothing,
        // 20th signals + step.
        var state = NewState();

        for (int i = 0; i < 9; i++)
            state.RecordEnvironmentDestroyed().Should().BeFalse();
        state.DestructionCount.Should().Be(9);
        state.ThreatLevel.Should().Be(0);

        state.RecordEnvironmentDestroyed().Should().BeTrue();
        state.DestructionCount.Should().Be(10);
        state.ThreatLevel.Should().Be(1);

        for (int i = 0; i < 9; i++)
            state.RecordEnvironmentDestroyed().Should().BeFalse();
        state.DestructionCount.Should().Be(19);
        state.ThreatLevel.Should().Be(1);

        state.RecordEnvironmentDestroyed().Should().BeTrue();
        state.DestructionCount.Should().Be(20);
        state.ThreatLevel.Should().Be(2);
    }

    // ── Enemy kills ──────────────────────────────────────────────────────

    [Fact]
    public void RecordEnemyKill_ScoresTenPerExp_CountsKills_AndFreesSlot()
    {
        var state = NewState();
        state.IncrementEnemyCount();
        state.IncrementEnemyCount();

        state.RecordEnemyKill(expValue: 50); // Brute

        state.KillCount.Should().Be(1);
        state.Score.Should().Be(500);
        state.ActiveEnemyCount.Should().Be(1);
    }

    [Fact]
    public void RecordEnemyKill_WhenArenaEmpty_FloorsCountAtZero()
    {
        var state = NewState();

        state.RecordEnemyKill(expValue: 25); // Shielder

        state.KillCount.Should().Be(1);
        state.Score.Should().Be(250);
        state.ActiveEnemyCount.Should().Be(0);
    }

    [Fact]
    public void RecordEnemyKill_ScoreIsLong_NoOverflowAtIntExpBounds()
    {
        var state = NewState();

        // Apex is the biggest windfall; repeated kills stay in long range.
        for (int i = 0; i < 10_000; i++)
            state.RecordEnemyKill(expValue: 500);

        state.Score.Should().Be(10_000L * 500 * 10); // 50_000_000 — long, not int
        state.KillCount.Should().Be(10_000);
    }
}
