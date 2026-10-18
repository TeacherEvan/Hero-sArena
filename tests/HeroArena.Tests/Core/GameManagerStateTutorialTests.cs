using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

// R1 (Guided "First Wave" onboarding): the Tutorial state machine lives in
// the Godot-free GameManagerState so transitions, guards, and the in-tutorial
// flag run headlessly. GameManager delegates 1:1; its remaining Node wiring
// (EventBus, SceneTree, scene change) stays guarded by the Godot gate.
public class GameManagerStateTutorialTests
{
    private GameManagerState NewState() => new();

    // ── Default ─────────────────────────────────────────────────────────────

    [Fact]
    public void Default_IsNotInTutorial_MainMenu()
    {
        var state = NewState();
        state.InTutorial.Should().BeFalse();
        state.CurrentState.Should().Be(GameState.MainMenu);
    }

    // ── StartTutorial ───────────────────────────────────────────────────────

    [Fact]
    public void StartTutorial_SetsInTutorial_AndTutorialState()
    {
        var state = NewState();
        state.SetState(GameState.GameOver);
        state.TryTransition(GameState.MainMenu); // must return to MainMenu first
        state.AddScore(999);
        state.SetThreatLevel(5);

        state.StartTutorial();

        state.InTutorial.Should().BeTrue();
        state.CurrentState.Should().Be(GameState.Tutorial);
    }

    [Fact]
    public void StartTutorial_ResetsRunMetrics()
    {
        var state = NewState();
        state.AddScore(42);
        state.SetThreatLevel(3);
        state.IncrementEnemyCount();

        state.StartTutorial();

        state.Score.Should().Be(0);
        state.ThreatLevel.Should().Be(0);
        state.CurrentWave.Should().Be(0);
        state.KillCount.Should().Be(0);
        state.DestructionCount.Should().Be(0);
        state.ActiveEnemyCount.Should().Be(0);
    }

    // ── CompleteTutorial ────────────────────────────────────────────────────

    [Fact]
    public void CompleteTutorial_ClearsInTutorial_ReturnsToMainMenu()
    {
        var state = NewState();
        state.StartTutorial();
        state.InTutorial.Should().BeTrue();
        state.CurrentState.Should().Be(GameState.Tutorial);

        state.CompleteTutorial();

        state.InTutorial.Should().BeFalse();
        state.CurrentState.Should().Be(GameState.MainMenu);
    }

    [Fact]
    public void CompleteTutorial_WhenNotInTutorial_IsNoOp()
    {
        var state = NewState();
        state.CurrentState.Should().Be(GameState.MainMenu);

        state.CompleteTutorial();

        state.InTutorial.Should().BeFalse();
        state.CurrentState.Should().Be(GameState.MainMenu);
    }

    // ── Tutorial guards normal wave spawning ────────────────────────────────

    [Fact]
    public void InTutorial_True_OnlyDuringTutorialState()
    {
        var state = NewState();
        state.StartTutorial();
        state.InTutorial.Should().BeTrue();

        state.CompleteTutorial();
        state.InTutorial.Should().BeFalse();
    }
}
