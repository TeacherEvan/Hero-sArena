using System;
using Xunit;
using FluentAssertions;
using HeroArena;
using Godot;
using System.Reflection;
using System.Runtime.Serialization;

namespace HeroArena.Tests.Core;

public class GameManagerTests
{
    [Fact]
    public void HandleEnvironmentDestroyed_IncreasesThreatLevel_AtThresholds()
    {
        // Setup mock EventBus via UninitializedObject to prevent Godot crash
#pragma warning disable SYSLIB0050
        var eventBus = (EventBus)FormatterServices.GetUninitializedObject(typeof(EventBus));
#pragma warning restore SYSLIB0050
        typeof(EventBus).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.SetValue(null, eventBus);

#pragma warning disable SYSLIB0050
        var gm = (GameManager)FormatterServices.GetUninitializedObject(typeof(GameManager));
#pragma warning restore SYSLIB0050

        // Manually initialize properties
        typeof(GameManager).GetProperty("ThreatLevel")!.SetValue(gm, 0);
        typeof(GameManager).GetProperty("DestructionCount")!.SetValue(gm, 0);

        var method = typeof(GameManager).GetMethod("HandleEnvironmentDestroyed", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // 1st to 9th
        for(int i = 0; i < 9; i++)
        {
            method.Invoke(gm, new object[] { default(Vector2), 10f });
        }
        gm.DestructionCount.Should().Be(9);
        gm.ThreatLevel.Should().Be(0);

        // 10th
        method.Invoke(gm, new object[] { default(Vector2), 10f });
        gm.DestructionCount.Should().Be(10);
        gm.ThreatLevel.Should().Be(1);

        // 11th to 19th
        for(int i = 0; i < 9; i++)
        {
            method.Invoke(gm, new object[] { default(Vector2), 10f });
        }
        gm.DestructionCount.Should().Be(19);
        gm.ThreatLevel.Should().Be(1);

        // 20th
        method.Invoke(gm, new object[] { default(Vector2), 10f });
        gm.DestructionCount.Should().Be(20);
        gm.ThreatLevel.Should().Be(2);

        // Clean up static state
        typeof(EventBus).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.SetValue(null, null);
    }
}
