using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Systems;

public class CollateralKarmaTests
{
    private CollateralKarma CreateKarmaSystem(int destructionCount = 0)
    {
        // Bypass Godot Node constructor since we are not running inside the engine
#pragma warning disable SYSLIB0050
        var karma = (CollateralKarma)RuntimeHelpers.GetUninitializedObject(typeof(CollateralKarma));
#pragma warning restore SYSLIB0050

        var prop = typeof(CollateralKarma).GetProperty("DestructionCount", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(karma, destructionCount);

        return karma;
    }

    [Fact]
    public void InitialState_DestructionCountIsZero()
    {
        var karma = CreateKarmaSystem();
        karma.DestructionCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 1.0f)]
    [InlineData(10, 1.168f)]
    [InlineData(50, 1.652f)]
    public void KarmaAmplifier_ScalesLogarithmicallyWithDestructionCount(int count, float expected)
    {
        var karma = CreateKarmaSystem(count);

        karma.KarmaAmplifier.Should().BeApproximately(expected, 0.005f);
    }

    [Fact]
    public void OnEnvironmentDestroyed_IncrementsDestructionCount()
    {
        var karma = CreateKarmaSystem(0);

        // Use reflection to invoke private method
        var method = typeof(CollateralKarma).GetMethod("OnEnvironmentDestroyed", BindingFlags.NonPublic | BindingFlags.Instance);

        method?.Invoke(karma, new object[] { Godot.Vector2.Zero, 10f });

        karma.DestructionCount.Should().Be(1);
    }
}
