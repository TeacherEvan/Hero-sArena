using Xunit;
using FluentAssertions;
using HeroArena;
using Godot;
using System.Reflection;
using System.Runtime.Serialization;
using System;

namespace HeroArena.Tests.Core;

// Dummy subclass to bypass Godot engine GetTree() calls
public partial class TestableGameManager : GameManager
{
    public bool TreePausedState { get; private set; } = false;

    protected override void SetTreePaused(bool paused)
    {
        TreePausedState = paused;
    }
}

[Trait("Category", "GodotRuntime")]
public class GameManagerTests_State
{
    private TestableGameManager CreateManager()
    {
#pragma warning disable SYSLIB0050
        var gm = (TestableGameManager)FormatterServices.GetUninitializedObject(typeof(TestableGameManager));
#pragma warning restore SYSLIB0050
        typeof(GameManager).GetProperty("CurrentState", BindingFlags.Public | BindingFlags.Instance)!.SetValue(gm, GameState.MainMenu);
        typeof(GameManager).GetProperty("CurrentWave", BindingFlags.Public | BindingFlags.Instance)!.SetValue(gm, 0);
        typeof(GameManager).GetProperty("Score", BindingFlags.Public | BindingFlags.Instance)!.SetValue(gm, 0);
        typeof(GameManager).GetProperty("ThreatLevel", BindingFlags.Public | BindingFlags.Instance)!.SetValue(gm, 0);
        typeof(GameManager).GetProperty("KillCount", BindingFlags.Public | BindingFlags.Instance)!.SetValue(gm, 0);
        typeof(GameManager).GetProperty("DestructionCount", BindingFlags.Public | BindingFlags.Instance)!.SetValue(gm, 0);
        return gm;
    }

    [Fact]
    public void StartGame_SetsStateToPlaying_AndResetsMetrics()
    {
        // Arrange
        var gm = CreateManager();

        typeof(GameManager).GetProperty("CurrentState")!.SetValue(gm, GameState.GameOver);
        typeof(GameManager).GetProperty("Score")!.SetValue(gm, 100);
        typeof(GameManager).GetProperty("KillCount")!.SetValue(gm, 5);

        // Act
        gm.StartGame();

        // Assert
        gm.CurrentState.Should().Be(GameState.Playing);
        gm.Score.Should().Be(0);
        gm.KillCount.Should().Be(0);
        gm.ThreatLevel.Should().Be(0);
        gm.CurrentWave.Should().Be(0);
        gm.DestructionCount.Should().Be(0);
    }

    [Fact]
    public void PauseGame_WhenPlaying_SetsStateToPaused_AndPausesTree()
    {
        // Arrange
        var gm = CreateManager();
        typeof(GameManager).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gm, new object[] { GameState.Playing });

        // Act
        gm.PauseGame();

        // Assert
        gm.CurrentState.Should().Be(GameState.Paused);
        gm.TreePausedState.Should().BeTrue();
    }

    [Fact]
    public void PauseGame_WhenNotPlaying_DoesNothing()
    {
        // Arrange
        var gm = CreateManager();
        typeof(GameManager).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gm, new object[] { GameState.GameOver });

        // Act
        gm.PauseGame();

        // Assert
        gm.CurrentState.Should().Be(GameState.GameOver);
        gm.TreePausedState.Should().BeFalse();
    }

    [Fact]
    public void ResumeGame_WhenPaused_SetsStateToPlaying_AndUnpausesTree()
    {
        // Arrange
        var gm = CreateManager();
        typeof(GameManager).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gm, new object[] { GameState.Paused });

        // Initial setup for TreePausedState (simulate already paused)
        typeof(TestableGameManager).GetProperty("TreePausedState")!.SetValue(gm, true);

        // Act
        gm.ResumeGame();

        // Assert
        gm.CurrentState.Should().Be(GameState.Playing);
        gm.TreePausedState.Should().BeFalse();
    }

    [Fact]
    public void ResumeGame_WhenNotPaused_DoesNothing()
    {
        // Arrange
        var gm = CreateManager();
        typeof(GameManager).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gm, new object[] { GameState.MainMenu });

        // Initial setup for TreePausedState
        typeof(TestableGameManager).GetProperty("TreePausedState")!.SetValue(gm, false);

        // Act
        gm.ResumeGame();

        // Assert
        gm.CurrentState.Should().Be(GameState.MainMenu);
        gm.TreePausedState.Should().BeFalse();
    }

    [Fact]
    public void TriggerGameOver_SetsStateToGameOver()
    {
        // Arrange
        var gm = CreateManager();
        typeof(GameManager).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gm, new object[] { GameState.Playing });

        // Act
        gm.TriggerGameOver();

        // Assert
        gm.CurrentState.Should().Be(GameState.GameOver);
    }
}
