using System;
using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Core;

// Nearest-N selection for Synapse's Overclock cast, extracted from the hero
// Node into a pure helper so the ordering and cap rules run headlessly.
public class MindControlTargetsTests
{
    [Fact]
    public void SelectNearest_EmptyInput_ReturnsEmpty()
    {
        var result = MindControlTargets.SelectNearest(Array.Empty<float>(), 50);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SelectNearest_NonPositiveCap_ReturnsEmpty(int cap)
    {
        var result = MindControlTargets.SelectNearest(new[] { 1f, 2f }, cap);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SelectNearest_FewerThanCap_ReturnsAllOrderedByDistance()
    {
        // index 0 far, index 1 near, index 2 middle
        var result = MindControlTargets.SelectNearest(new[] { 9f, 1f, 4f }, 50);

        result.Should().Equal(1, 2, 0);
    }

    [Fact]
    public void SelectNearest_MoreThanCap_ReturnsOnlyTheNearest()
    {
        var result = MindControlTargets.SelectNearest(new[] { 5f, 1f, 4f, 2f, 3f }, 3);

        result.Should().Equal(1, 3, 4);
    }

    [Fact]
    public void SelectNearest_ExactCap_ReturnsAll()
    {
        var result = MindControlTargets.SelectNearest(new[] { 3f, 1f, 2f }, 3);

        result.Should().Equal(1, 2, 0);
    }

    [Fact]
    public void SelectNearest_DistanceTies_KeepLowerIndexFirst()
    {
        var result = MindControlTargets.SelectNearest(new[] { 2f, 2f, 0f, 2f }, 4);

        result.Should().Equal(2, 0, 1, 3);
    }
}
