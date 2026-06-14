using System;
using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

public class ObjectPoolManagerTests
{
    [Fact]
    public void Constants_AreExpectedValues()
    {
        ObjectPoolManager.MAX_PROJECTILES.Should().Be(5000);
        ObjectPoolManager.MAX_DECALS.Should().Be(10000);
    }

    [Fact]
    public void ObjectPoolManager_Constants_ArePowersOfReasonableSizes()
    {
        // Verify reasonable pool sizes for performance
        ObjectPoolManager.MAX_PROJECTILES.Should().BeGreaterThan(1000);
        ObjectPoolManager.MAX_DECALS.Should().BeGreaterThan(ObjectPoolManager.MAX_PROJECTILES);
    }
}

// Note: ObjectPoolManager requires Godot scene tree for full testing
// These tests verify the pure C# logic. Integration tests would need Godot headless.