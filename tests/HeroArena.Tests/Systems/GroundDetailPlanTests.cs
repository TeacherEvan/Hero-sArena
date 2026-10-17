using System;
using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// GroundDetailPlan: the Godot-free generator behind the living ground.
// Covers position-hash determinism, per-cell and region budgets, cell math
// (including negative coordinates), and the bounded wind-sway function.
public class GroundDetailPlanTests
{
    private static GroundDetailPlan.Item[] Buffer(int size) => new GroundDetailPlan.Item[size];

    [Fact]
    public void Hash_IsDeterministic_AndSensitiveToInputs()
    {
        uint a = GroundDetailPlan.Hash(3, -7, 123u);
        uint b = GroundDetailPlan.Hash(3, -7, 123u);
        uint c = GroundDetailPlan.Hash(3, -7, 124u);
        uint d = GroundDetailPlan.Hash(4, -7, 123u);

        a.Should().Be(b);
        a.Should().NotBe(c);
        a.Should().NotBe(d);
    }

    [Fact]
    public void Hash01_IsTheTopBitsOfHash_MappedToUnitRange()
    {
        for (int x = -50; x < 50; x += 7)
        {
            for (int y = -50; y < 50; y += 9)
            {
                float v = GroundDetailPlan.Hash01(x, y, 0xBEEFu);
                v.Should().BeInRange(0f, 1f);
                // Pins the derivation: 24 significant bits scaled to [0, 1).
                v.Should().Be((GroundDetailPlan.Hash(x, y, 0xBEEFu) & 0xFFFFFFu) / 16777216f);
            }
        }
    }

    [Fact]
    public void Generate_IsDeterministic_ForSameCellAndSeed()
    {
        var first = Buffer(GroundDetailPlan.MaxItemsPerCell);
        var second = Buffer(GroundDetailPlan.MaxItemsPerCell);

        int n1 = GroundDetailPlan.Generate(12, -3, 777u, first, first.Length);
        int n2 = GroundDetailPlan.Generate(12, -3, 777u, second, second.Length);

        n1.Should().Be(n2).And.BeGreaterThan(0);
        for (int i = 0; i < n1; i++)
        {
            first[i].X.Should().Be(second[i].X);
            first[i].Y.Should().Be(second[i].Y);
            first[i].Kind.Should().Be(second[i].Kind);
            first[i].Size.Should().Be(second[i].Size);
        }

        // A different seed must diverge…
        var other = Buffer(GroundDetailPlan.MaxItemsPerCell);
        int nOther = GroundDetailPlan.Generate(12, -3, 778u, other, other.Length);
        bool differs = nOther != n1;
        for (int i = 0; i < Math.Min(n1, nOther) && !differs; i++)
            differs = first[i].X != other[i].X || first[i].Kind != other[i].Kind;
        differs.Should().BeTrue("a different seed must grow different detail");

        // …and the function is stateless: an unrelated call in between must not
        // change the answer (catches a hidden shared RNG).
        var afterOtherWork = Buffer(GroundDetailPlan.MaxItemsPerCell);
        GroundDetailPlan.Generate(99, 99, 555u, afterOtherWork, afterOtherWork.Length);
        int n3 = GroundDetailPlan.Generate(12, -3, 777u, afterOtherWork, afterOtherWork.Length);
        n3.Should().Be(n1);
        for (int i = 0; i < n1; i++)
            afterOtherWork[i].X.Should().Be(first[i].X);
    }

    [Fact]
    public void Generate_DifferentCells_ProduceDifferentDetail()
    {
        var a = Buffer(GroundDetailPlan.MaxItemsPerCell);
        var b = Buffer(GroundDetailPlan.MaxItemsPerCell);

        int na = GroundDetailPlan.Generate(0, 0, 777u, a, a.Length);
        int nb = GroundDetailPlan.Generate(1, 0, 777u, b, b.Length);

        bool anyDifference = na != nb;
        for (int i = 0; i < Math.Min(na, nb) && !anyDifference; i++)
            anyDifference = MathF.Abs(a[i].X - b[i].X) > 0.001f || a[i].Kind != b[i].Kind;

        anyDifference.Should().BeTrue();
    }

    [Fact]
    public void Generate_ItemsStayInsideTheCell_InsetByMargin()
    {
        var buffer = Buffer(GroundDetailPlan.MaxItemsPerCell);

        for (int cx = -3; cx <= 3; cx++)
        {
            for (int cy = -3; cy <= 3; cy++)
            {
                int n = GroundDetailPlan.Generate(cx, cy, 42u, buffer, buffer.Length);
                n.Should().BeInRange(1, GroundDetailPlan.MaxItemsPerCell);
                for (int i = 0; i < n; i++)
                {
                    buffer[i].X.Should().BeInRange(cx * (float)GroundDetailPlan.CellSize + GroundDetailPlan.CellMargin - 0.01f,
                        (cx + 1) * (float)GroundDetailPlan.CellSize - GroundDetailPlan.CellMargin + 0.01f);
                    buffer[i].Y.Should().BeInRange(cy * (float)GroundDetailPlan.CellSize + GroundDetailPlan.CellMargin - 0.01f,
                        (cy + 1) * (float)GroundDetailPlan.CellSize - GroundDetailPlan.CellMargin + 0.01f);
                }
            }
        }
    }

    [Fact]
    public void Generate_RespectsBudgetAndBufferLength()
    {
        var small = Buffer(2);

        int n = GroundDetailPlan.Generate(5, 5, 1u, small, 999);

        n.Should().BeLessThanOrEqualTo(2);

        GroundDetailPlan.Generate(5, 5, 1u, Buffer(0), 10).Should().Be(0);
        GroundDetailPlan.Generate(5, 5, 1u, small, 0).Should().Be(0);
    }

    [Fact]
    public void Generate_NullBuffer_ReturnsZero()
    {
        GroundDetailPlan.Generate(0, 0, 1u, null!, 4).Should().Be(0);
    }

    [Fact]
    public void GenerateRegion_FillsFromCellsIntersectingRect_WithinBudget()
    {
        var buffer = Buffer(200);

        int n = GroundDetailPlan.GenerateRegion(-320f, -160f, 480f, 320f, 99u, buffer, 120);

        n.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(120);

        // Rect spans cells x: -2..3, y: -1..2 → world x in [-320, 640), y in [-160, 480).
        for (int i = 0; i < n; i++)
        {
            buffer[i].X.Should().BeInRange(-320f, 640f);
            buffer[i].Y.Should().BeInRange(-160f, 480f);
        }
    }

    [Fact]
    public void GenerateRegion_OverflowingBudget_ThinsEvenlyInsteadOfTruncating()
    {
        // 8×8 cells worst-case 384 items into a 60-item budget: the scan must
        // stride so detail spreads over the whole region instead of filling
        // the top rows and stopping.
        var buffer = Buffer(60);
        float side = GroundDetailPlan.CellSize * 8f - 1f;
        int n = GroundDetailPlan.GenerateRegion(0f, 0f, side, side, 7u, buffer, 60);

        n.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(60);

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            minY = MathF.Min(minY, buffer[i].Y);
            maxY = MathF.Max(maxY, buffer[i].Y);
        }

        (maxY - minY).Should().BeGreaterThan(side * 0.4f, "detail must reach the far half of the region");
    }

    [Fact]
    public void GenerateRegion_IsDeterministic()
    {
        var a = Buffer(100);
        var b = Buffer(100);

        int na = GroundDetailPlan.GenerateRegion(0f, 0f, 640f, 480f, 5u, a, 100);
        int nb = GroundDetailPlan.GenerateRegion(0f, 0f, 640f, 480f, 5u, b, 100);

        na.Should().Be(nb).And.BeGreaterThan(0);
        for (int i = 0; i < na; i++)
        {
            a[i].X.Should().Be(b[i].X);
            a[i].Kind.Should().Be(b[i].Kind);
        }
    }

    [Fact]
    public void GenerateRegion_ReversedRect_ReturnsZero()
    {
        var buffer = Buffer(50);

        GroundDetailPlan.GenerateRegion(100f, 0f, -100f, 50f, 1u, buffer, 50).Should().Be(0);
        GroundDetailPlan.GenerateRegion(0f, 100f, 50f, -100f, 1u, buffer, 50).Should().Be(0);
    }

    [Fact]
    public void GenerateRegion_PointRect_GrowsTheCellThatContainsIt()
    {
        // A zero-area rect still names the cell the point falls in (the node
        // always passes real view rects; this pins the floor-division contract).
        int n = GroundDetailPlan.GenerateRegion(40f, 40f, 40f, 40f, 1u, Buffer(50), 50);

        n.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(GroundDetailPlan.MaxItemsPerCell);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(-1f, -1f)]
    [InlineData(-160f, -1f)]
    [InlineData(-161f, -2f)]
    [InlineData(319f, 1f)]
    [InlineData(320f, 2f)]
    public void CellRangeFor_UsesFloorDivision_WithNegatives(float coordinate, int expectedCell)
    {
        GroundDetailPlan.CellRangeFor(coordinate, coordinate, coordinate, coordinate, out int x0, out int y0, out int x1, out int y1);

        x0.Should().Be(expectedCell);
        x1.Should().Be(expectedCell);
        y0.Should().Be(expectedCell);
        y1.Should().Be(expectedCell);
    }

    [Fact]
    public void Sway_IsDeterministic_AndBounded_EvenUnderExtremeWind()
    {
        GroundDetailPlan.Sway(1.23f, 60f, -20f, 500.25f, -200.75f, out float sx, out float sy);
        GroundDetailPlan.Sway(1.23f, 60f, -20f, 500.25f, -200.75f, out float sx2, out float sy2);

        sx.Should().Be(sx2);
        sy.Should().Be(sy2);
        sx.Should().BeInRange(-GroundDetailPlan.MaxSway, GroundDetailPlan.MaxSway);
        sy.Should().BeInRange(-GroundDetailPlan.MaxSway, GroundDetailPlan.MaxSway);

        GroundDetailPlan.Sway(9f, 100000f, 100000f, 0f, 0f, out float hx, out float hy);
        hx.Should().BeInRange(-GroundDetailPlan.MaxSway, GroundDetailPlan.MaxSway);
        hy.Should().BeInRange(-GroundDetailPlan.MaxSway, GroundDetailPlan.MaxSway);
    }

    [Fact]
    public void Sway_FluttersEvenWithoutWind()
    {
        GroundDetailPlan.Sway(0f, 0f, 0f, 10f, 10f, out float aX, out float aY);
        GroundDetailPlan.Sway(1.1f, 0f, 0f, 10f, 10f, out float bX, out float bY);

        (aX != bX || aY != bY).Should().BeTrue("tufts breathe even in dead air");
    }

    [Fact]
    public void Sway_LeansDownwind_AndAmplifiesWithWindStrength()
    {
        GroundDetailPlan.Sway(0.7f, 60f, 0f, 10f, 10f, out float gentle, out _);
        GroundDetailPlan.Sway(0.7f, 120f, 0f, 10f, 10f, out float strong, out _);

        gentle.Should().BeGreaterThan(0f, "tufts lean the way the wind blows");
        strong.Should().BeGreaterThan(gentle, "stronger wind must sway further");
        strong.Should().BeLessThanOrEqualTo(GroundDetailPlan.MaxSway);
    }

    [Fact]
    public void Tufts_DominateTheDetailMix_ButAllKindsAppear()
    {
        int tufts = 0, pebbles = 0, cracks = 0;
        var buffer = Buffer(GroundDetailPlan.MaxItemsPerCell);
        for (int cx = 0; cx < 20; cx++)
        {
            for (int cy = 0; cy < 20; cy++)
            {
                int n = GroundDetailPlan.Generate(cx, cy, 3u, buffer, buffer.Length);
                for (int i = 0; i < n; i++)
                {
                    switch (buffer[i].Kind)
                    {
                        case GroundDetailPlan.ItemKind.Tuft: tufts++; break;
                        case GroundDetailPlan.ItemKind.Pebble: pebbles++; break;
                        default: cracks++; break;
                    }
                }
            }
        }

        tufts.Should().BeGreaterThan(pebbles);
        tufts.Should().BeGreaterThan(cracks);
        pebbles.Should().BeGreaterThan(0);
        cracks.Should().BeGreaterThan(0);
    }
}
