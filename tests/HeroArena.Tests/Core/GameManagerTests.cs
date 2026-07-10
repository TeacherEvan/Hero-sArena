using System;
using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

public class GameManagerTests : IDisposable
{
    private readonly GameManager _gameManager;
    private readonly EventBus _eventBus;

    public GameManagerTests()
    {
        // Setup dependencies
        _eventBus = new EventBus();
        _eventBus._Ready(); // Initialize the singleton

        _gameManager = new GameManager();
        _gameManager._Ready();
    }

    public void Dispose()
    {
        // Clean up events to avoid leaking between tests
        _gameManager._ExitTree();
    }

    [Fact]
    public void AddScore_IncreasesScoreByPoints()
    {
        // Arrange
        _gameManager.Score.Should().Be(0);

        // Act
        _gameManager.AddScore(150);

        // Assert
        _gameManager.Score.Should().Be(150);

        // Act again
        _gameManager.AddScore(50);

        // Assert
        _gameManager.Score.Should().Be(200);
    }

    [Fact]
    public void IncrementEnemyCount_IncreasesCountByOne()
    {
        // Arrange
        _gameManager.ActiveEnemyCount.Should().Be(0);

        // Act
        _gameManager.IncrementEnemyCount();

        // Assert
        _gameManager.ActiveEnemyCount.Should().Be(1);
    }

    [Fact]
    public void DecrementEnemyCount_DecreasesCountByOne()
    {
        // Arrange
        _gameManager.IncrementEnemyCount();
        _gameManager.IncrementEnemyCount();
        _gameManager.ActiveEnemyCount.Should().Be(2);

        // Act
        _gameManager.DecrementEnemyCount();

        // Assert
        _gameManager.ActiveEnemyCount.Should().Be(1);
    }

    [Fact]
    public void DecrementEnemyCount_DoesNotGoBelowZero()
    {
        // Arrange
        _gameManager.ActiveEnemyCount.Should().Be(0);

        // Act
        _gameManager.DecrementEnemyCount();

        // Assert
        _gameManager.ActiveEnemyCount.Should().Be(0);
    }

    [Fact]
    public void SetThreatLevel_UpdatesThreatLevel_AndEmitsEvent()
    {
        // Arrange
        int emittedLevel = -1;
        bool eventFired = false;

        EventBus.Instance.OnThreatLevelChanged += (level) => {
            eventFired = true;
            emittedLevel = level;
        };

        // Act
        _gameManager.SetThreatLevel(5);

        // Assert
        _gameManager.ThreatLevel.Should().Be(5);
        eventFired.Should().BeTrue();
        emittedLevel.Should().Be(5);
    }

    [Fact]
    public void SetThreatLevel_WhenSameLevel_DoesNotUpdateOrEmit()
    {
        // Arrange
        _gameManager.SetThreatLevel(5);

        bool eventFired = false;
        EventBus.Instance.OnThreatLevelChanged += (level) => {
            eventFired = true;
        };

        // Act
        _gameManager.SetThreatLevel(5);

        // Assert
        _gameManager.ThreatLevel.Should().Be(5);
        eventFired.Should().BeFalse();
    }
}
