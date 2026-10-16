using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

// F-1 regression guard, formerly Godot-gated: EnemyBase's static entity-id
// registry. The dictionary lives in the Godot-free EntityRegistry<T>, so the
// registry semantics are coverable headlessly: unknown ids and negative ids
// must never resolve, and registration/removal must be reflected
// immediately. EnemyBase's _ExitTree → Remove wiring itself is SceneTree-
// dependent and is exercised (actually or by construction) in the Godot gate.
public class EntityRegistryTests
{
    [Fact]
    public void TryGet_UnknownId_ReturnsFalseAndNull()
    {
        var registry = new EntityRegistry<string>();

        bool found = registry.TryGet(int.MaxValue, out string? enemy);

        found.Should().BeFalse();
        enemy.Should().BeNull();
    }

    [Fact]
    public void TryGet_NegativeId_ReturnsFalseAndNull()
    {
        var registry = new EntityRegistry<string>();
        registry.Register(7, "enemy");

        bool found = registry.TryGet(-1, out string? enemy);

        found.Should().BeFalse();
        enemy.Should().BeNull();
    }

    [Fact]
    public void Register_Then_TryGet_Resolves()
    {
        var registry = new EntityRegistry<string>();

        registry.Register(42, "enemy-42");

        registry.TryGet(42, out string? found).Should().BeTrue();
        found.Should().Be("enemy-42");
        registry.Get(42).Should().Be("enemy-42");
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_IsReflectedImmediately()
    {
        var registry = new EntityRegistry<string>();
        registry.Register(1, "a");
        registry.Register(2, "b");

        registry.Remove(1).Should().BeTrue();

        registry.TryGet(1, out string? removed).Should().BeFalse();
        removed.Should().BeNull();
        registry.TryGet(2, out string? survivor).Should().BeTrue();
        survivor.Should().Be("b");
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_UnknownId_ReturnsFalse()
    {
        var registry = new EntityRegistry<string>();

        registry.Remove(999).Should().BeFalse();
    }

    [Fact]
    public void Register_SameIdTwice_LastWriteWins()
    {
        var registry = new EntityRegistry<string>();

        registry.Register(5, "old");
        registry.Register(5, "new");

        registry.Get(5).Should().Be("new");
        registry.Count.Should().Be(1);
    }
}
