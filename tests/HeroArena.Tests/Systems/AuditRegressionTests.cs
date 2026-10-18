using System;
using FluentAssertions;
using Godot;
using Xunit;

namespace HeroArena.Tests.Systems;

/// <summary>
/// Regression probes for the surgical-audit findings (run-003).
///
/// NOTE (AGENTS.md): Godot <c>Node</c>-dependent classes
/// (<c>ProjectileBase</c> : Area2D, <c>DestructionDrama</c> / <c>AmbientWildlife</c> :
/// Node2D, <c>ObjectPoolManager</c> : Node) cannot be instantiated headlessly in
/// xUnit — <c>RuntimeHelpers.GetUninitializedObject</c> is deprecated. Their runtime
/// behaviour is covered by the Godot headless gate
/// (<c>res://tests/GodotTests/CoreSystemTests.tscn</c>, which passes 13/13 and
/// smoke-tests DestructionDrama via <c>TestSurroundingsFx</c>).
///
/// This file therefore probes the pure, engine-independent premises behind the
/// audit defects using <c>Godot.Vector2</c> (a plain struct: no engine init required).
/// </summary>
public class AuditRegressionTests
{
    // P2-2 premise check: does Godot 4 normalize Vector2.Zero to NaN or to (0,0)?
    // This decides whether the Activate zero-direction guard is required.
    [Fact]
    public void ZeroDirection_Normalized_Premise()
    {
        var zero = Vector2.Zero;
        var normalized = zero.Normalized();

        // Document the actual engine behaviour (NaN vs (0,0)) so the guard decision
        // is evidence-based, not assumed.
        bool isNan = float.IsNaN(normalized.X) || float.IsNaN(normalized.Y);
        bool isZero = normalized == Vector2.Zero;
        // Exactly one of the two must hold; record which.
        (isNan ^ isZero).Should().BeTrue("zero normalize must be either NaN or (0,0), not both/neither");
        // If Godot 4 returns (0,0) for zero-normalize, the NaN hazard does not exist
        // and the Activate guard becomes an unwanted behaviour change.
        isZero.Should().BeTrue("Godot 4 returns (0,0) for zero Normalize -> safe, no NaN hazard");
    }

    // Guard condition logic: LengthSquared() > 1e-6f must be FALSE for Vector2.Zero
    // so the fallback branch is selected (no NaN propagation, no silent stall).
    [Fact]
    public void ZeroDirection_LengthSquared_GuardSelectsFallback()
    {
        var zero = Vector2.Zero;
        (zero.LengthSquared() > 1e-6f).Should().BeFalse();
        (zero.LengthSquared() == 0f).Should().BeTrue();
    }

    // Non-zero direction normalizes cleanly (sanity that normal path is unaffected).
    [Fact]
    public void NonZeroDirection_Normalized_IsFinite()
    {
        var dir = new Vector2(3f, 4f);
        var n = dir.Normalized();
        float.IsNaN(n.X).Should().BeFalse();
        float.IsNaN(n.Y).Should().BeFalse();
        n.Length().Should().BeApproximately(1f, 1e-4f);
    }
}
