using System;
using Xunit;
using FluentAssertions;
using HeroArena;
using Godot;
using System.Runtime.Serialization;

namespace HeroArena.Tests.Core;

// Bypasses the Godot Node constructor since we are running outside the engine.
// This works because EventBus is mostly a pure C# class and its event system
// doesn't heavily depend on the engine internals.
public class EventBusTests : IDisposable
{
    private readonly EventBus _eventBus;

    public EventBusTests()
    {
#pragma warning disable SYSLIB0050
        _eventBus = (EventBus)FormatterServices.GetUninitializedObject(typeof(EventBus));
#pragma warning restore SYSLIB0050

        // Ensure static instance is null before tests
        if (EventBus.Instance != null)
        {
            _eventBus._ExitTree();
        }
    }

    public void Dispose()
    {
        // Clean up events and static instances to avoid leaking between tests
        _eventBus._ExitTree();
    }

    [Fact]
    public void Ready_SetsSingletonInstance()
    {
        _eventBus._Ready();
        EventBus.Instance.Should().Be(_eventBus);
    }

    [Fact]
    public void ExitTree_ClearsSingletonInstance()
    {
        _eventBus._Ready();
        _eventBus._ExitTree();
        EventBus.Instance.Should().BeNull();
    }

    [Fact]
    public void EmitEnemyKilled_InvokesEvent()
    {
        bool eventFired = false;
        EnemyBase? passedEnemy = null;
        _eventBus.OnEnemyKilled += (e) => {
            eventFired = true;
            passedEnemy = e;
        };

        _eventBus.EmitEnemyKilled(null!); // Assuming we can pass null for test

        eventFired.Should().BeTrue();
        passedEnemy.Should().BeNull();
    }

    [Fact]
    public void EmitHeroDamaged_InvokesEvent()
    {
        bool eventFired = false;
        float? passedDamage = null;
        _eventBus.OnHeroDamaged += (damage) => {
            eventFired = true;
            passedDamage = damage;
        };

        _eventBus.EmitHeroDamaged(15.5f);

        eventFired.Should().BeTrue();
        passedDamage.Should().Be(15.5f);
    }

    [Fact]
    public void EmitWaveStarted_InvokesEvent()
    {
        bool eventFired = false;
        int? passedWave = null;
        _eventBus.OnWaveStarted += (wave) => {
            eventFired = true;
            passedWave = wave;
        };

        _eventBus.EmitWaveStarted(3);

        eventFired.Should().BeTrue();
        passedWave.Should().Be(3);
    }

    [Fact]
    public void EmitWaveCompleted_InvokesEvent()
    {
        bool eventFired = false;
        int? passedWave = null;
        _eventBus.OnWaveCompleted += (wave) => {
            eventFired = true;
            passedWave = wave;
        };

        _eventBus.EmitWaveCompleted(5);

        eventFired.Should().BeTrue();
        passedWave.Should().Be(5);
    }

    [Fact]
    public void EmitLevelUp_InvokesEvent()
    {
        bool eventFired = false;
        int? passedLevel = null;
        _eventBus.OnLevelUp += (level) => {
            eventFired = true;
            passedLevel = level;
        };

        _eventBus.EmitLevelUp(10);

        eventFired.Should().BeTrue();
        passedLevel.Should().Be(10);
    }

    [Fact]
    public void EmitPowerupCollected_InvokesEvent()
    {
        bool eventFired = false;
        string? passedType = null;
        _eventBus.OnPowerupCollected += (type) => {
            eventFired = true;
            passedType = type;
        };

        _eventBus.EmitPowerupCollected("Health");

        eventFired.Should().BeTrue();
        passedType.Should().Be("Health");
    }

    [Fact]
    public void EmitEnvironmentDestroyed_InvokesEvent()
    {
        bool eventFired = false;
        Vector2? passedPos = null;
        float? passedRadius = null;
        _eventBus.OnEnvironmentDestroyed += (pos, radius) => {
            eventFired = true;
            passedPos = pos;
            passedRadius = radius;
        };

        var expectedPos = new Vector2(10, 20);
        _eventBus.EmitEnvironmentDestroyed(expectedPos, 5f);

        eventFired.Should().BeTrue();
        passedPos.Should().Be(expectedPos);
        passedRadius.Should().Be(5f);
    }

    [Fact]
    public void EmitThreatLevelChanged_InvokesEvent()
    {
        bool eventFired = false;
        int? passedLevel = null;
        _eventBus.OnThreatLevelChanged += (level) => {
            eventFired = true;
            passedLevel = level;
        };

        _eventBus.EmitThreatLevelChanged(2);

        eventFired.Should().BeTrue();
        passedLevel.Should().Be(2);
    }

    [Fact]
    public void EmitProjectileHit_InvokesEvent()
    {
        bool eventFired = false;
        Vector2? passedPos = null;
        DamageType? passedType = null;
        _eventBus.OnProjectileHit += (pos, type) => {
            eventFired = true;
            passedPos = pos;
            passedType = type;
        };

        var expectedPos = new Vector2(50, 50);
        _eventBus.EmitProjectileHit(expectedPos, DamageType.Energy);

        eventFired.Should().BeTrue();
        passedPos.Should().Be(expectedPos);
        passedType.Should().Be(DamageType.Energy);
    }

    [Fact]
    public void EmitDecalRequested_InvokesEvent()
    {
        bool eventFired = false;
        Vector2? passedPos = null;
        DecalType? passedType = null;
        float? passedSize = null;
        _eventBus.OnDecalRequested += (pos, type, size) => {
            eventFired = true;
            passedPos = pos;
            passedType = type;
            passedSize = size;
        };

        var expectedPos = new Vector2(100, 100);
        _eventBus.EmitDecalRequested(expectedPos, DecalType.ScorchMark, 2f);

        eventFired.Should().BeTrue();
        passedPos.Should().Be(expectedPos);
        passedType.Should().Be(DecalType.ScorchMark);
        passedSize.Should().Be(2f);
    }
}
