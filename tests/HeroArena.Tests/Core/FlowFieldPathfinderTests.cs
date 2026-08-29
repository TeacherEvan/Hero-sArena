using System;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using FluentAssertions;
using HeroArena;
using Godot;

namespace HeroArena.Tests.Core;

// Using the bypass trick to run purely C# logic of a Godot Node headlessly
public class FlowFieldPathfinderTests
{
    private FlowFieldPathfinder CreatePathfinder()
    {
#pragma warning disable SYSLIB0050
        var pathfinder = (FlowFieldPathfinder)FormatterServices.GetUninitializedObject(typeof(FlowFieldPathfinder));
#pragma warning restore SYSLIB0050

        // Initialize fields that are skipped by GetUninitializedObject
        typeof(FlowFieldPathfinder).GetField("_bufA", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pathfinder, new byte[FlowFieldPathfinder.CELL_COUNT]);
        typeof(FlowFieldPathfinder).GetField("_bufB", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pathfinder, new byte[FlowFieldPathfinder.CELL_COUNT]);

        var bufA = (byte[])typeof(FlowFieldPathfinder).GetField("_bufA", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pathfinder)!;
        var bufB = (byte[])typeof(FlowFieldPathfinder).GetField("_bufB", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pathfinder)!;

        typeof(FlowFieldPathfinder).GetField("_readBuf", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pathfinder, bufA);
        typeof(FlowFieldPathfinder).GetField("_writeBuf", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pathfinder, bufB);
        typeof(FlowFieldPathfinder).GetField("_blocked", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pathfinder, new bool[FlowFieldPathfinder.CELL_COUNT]);
        typeof(FlowFieldPathfinder).GetField("_targetLock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pathfinder, new object());
        typeof(FlowFieldPathfinder).GetField("_bfsQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pathfinder, new int[FlowFieldPathfinder.CELL_COUNT]);

        // Properties
        pathfinder.CellWorldSize = 16f;
        pathfinder.GridOrigin = Vector2.Zero;

        return pathfinder;
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(8, 8, 0, 0)]
    [InlineData(15.9f, 15.9f, 0, 0)]
    [InlineData(16, 16, 1, 1)]
    [InlineData(32, 16, 2, 1)]
    [InlineData(160, 320, 10, 20)]
    [InlineData(-10, -10, -1, -1)]
    public void WorldToGrid_ReturnsCorrectGridPosition(float worldX, float worldY, int expectedGridX, int expectedGridY)
    {
        var pathfinder = CreatePathfinder();

        var gridPos = pathfinder.WorldToGrid(new Vector2(worldX, worldY));

        gridPos.Should().Be(new Vector2I(expectedGridX, expectedGridY));
    }

    [Theory]
    [InlineData(0, 0, 8, 8)]
    [InlineData(1, 1, 24, 24)]
    [InlineData(10, 20, 168, 328)]
    public void GridToWorld_ReturnsCorrectWorldPosition(int gridX, int gridY, float expectedWorldX, float expectedWorldY)
    {
        var pathfinder = CreatePathfinder();

        var worldPos = pathfinder.GridToWorld(new Vector2I(gridX, gridY));

        worldPos.Should().Be(new Vector2(expectedWorldX, expectedWorldY));
    }

    [Fact]
    public void SetBlocked_UpdatesBlockedState()
    {
        var pathfinder = CreatePathfinder();

        pathfinder.SetBlocked(new Vector2I(5, 5), true);

        var blockedArray = (bool[])typeof(FlowFieldPathfinder).GetField("_blocked", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pathfinder)!;

        blockedArray[5 * FlowFieldPathfinder.GRID_W + 5].Should().BeTrue();

        // Ensure other cells are still unblocked
        blockedArray[5 * FlowFieldPathfinder.GRID_W + 6].Should().BeFalse();

        // Unblock
        pathfinder.SetBlocked(new Vector2I(5, 5), false);
        blockedArray[5 * FlowFieldPathfinder.GRID_W + 5].Should().BeFalse();
    }

    [Fact]
    public void SetBlocked_OutOfBounds_IgnoresSilently()
    {
        var pathfinder = CreatePathfinder();

        // This shouldn't crash
        pathfinder.SetBlocked(new Vector2I(-1, 0), true);
        pathfinder.SetBlocked(new Vector2I(0, -1), true);
        pathfinder.SetBlocked(new Vector2I(FlowFieldPathfinder.GRID_W, 0), true);
        pathfinder.SetBlocked(new Vector2I(0, FlowFieldPathfinder.GRID_H), true);
    }

    [Fact]
    public void ComputeFlowField_ValidTarget_ComputesCorrectFlow()
    {
        var pathfinder = CreatePathfinder();

        var targetWorld = new Vector2(16 * 10 + 8, 16 * 10 + 8); // Center of grid (10, 10)

        var computeMethod = typeof(FlowFieldPathfinder).GetMethod("ComputeFlowField", BindingFlags.NonPublic | BindingFlags.Instance);
        computeMethod!.Invoke(pathfinder, new object[] { targetWorld });

        var swapMethod = typeof(FlowFieldPathfinder).GetMethod("SwapBuffers", BindingFlags.NonPublic | BindingFlags.Instance);
        swapMethod!.Invoke(pathfinder, null);

        // Cell (8, 8) -> target is at (10, 10). Should point SE (positive X, positive Y).
        // In the flow field, DirDx and DirDy map to directions.
        var sourcePos = new Vector2(16 * 8 + 8, 16 * 8 + 8);
        var dir = pathfinder.GetFlowDirection(sourcePos);

        dir.X.Should().BeGreaterThan(0);
        dir.Y.Should().BeGreaterThan(0);

        // Cell (12, 10) -> target is at (10, 10). Should point W (negative X, zero Y).
        sourcePos = new Vector2(16 * 12 + 8, 16 * 10 + 8);
        dir = pathfinder.GetFlowDirection(sourcePos);

        dir.X.Should().BeLessThan(0);
        dir.Y.Should().Be(0);

        // Target cell itself has zero vector
        dir = pathfinder.GetFlowDirection(targetWorld);
        dir.Should().Be(Vector2.Zero);
    }

    [Fact]
    public void ComputeFlowField_BlockedPath_RoutesAroundObstacle()
    {
        var pathfinder = CreatePathfinder();

        // Target at (5, 5)
        var targetWorld = new Vector2(16 * 5 + 8, 16 * 5 + 8);

        // Block a wall between (10, 5) and (5, 5). Wall at x=7.
        for (int y = 0; y <= 10; y++)
        {
            pathfinder.SetBlocked(new Vector2I(7, y), true);
        }

        var computeMethod = typeof(FlowFieldPathfinder).GetMethod("ComputeFlowField", BindingFlags.NonPublic | BindingFlags.Instance);
        computeMethod!.Invoke(pathfinder, new object[] { targetWorld });

        var swapMethod = typeof(FlowFieldPathfinder).GetMethod("SwapBuffers", BindingFlags.NonPublic | BindingFlags.Instance);
        swapMethod!.Invoke(pathfinder, null);

        // Cell at (8, 5) - target is at (5, 5). Direct path is West.
        // But West (x=7) is blocked. So it must go South (or North, depending on how BFS traverses) to go around the wall at y=10.
        // Wall extends from y=0 to y=10. South goes towards y=11 (which is unblocked).
        var sourcePos = new Vector2(16 * 8 + 8, 16 * 5 + 8);
        var dir = pathfinder.GetFlowDirection(sourcePos);

        // Expect to route South/South-East to avoid wall
        dir.Y.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetFlowDirection_OutOfBounds_ReturnsZero()
    {
        var pathfinder = CreatePathfinder();

        var dir = pathfinder.GetFlowDirection(new Vector2(-100, -100));
        dir.Should().Be(Vector2.Zero);

        dir = pathfinder.GetFlowDirection(new Vector2(9999, 9999));
        dir.Should().Be(Vector2.Zero);
    }
}
