using System;
using Godot;
using HeroArena;
using Xunit;
using FluentAssertions;

namespace HeroArena.Tests.Core
{
    public class SpatialHashGridTests
    {
        [Fact]
        public void TestInsertAndQuery()
        {
            var grid = new SpatialHashGrid(64, 512);
            grid.Insert(1, new Vector2(10, 10), 5);

            int[] results = grid.QueryRadius(new Vector2(12, 12), 10, out int count);
            count.Should().Be(1);
            results[0].Should().Be(1);
        }

        [Fact]
        public void TestUpdateFastPath()
        {
            var grid = new SpatialHashGrid(64, 512);
            grid.Insert(1, new Vector2(10, 10), 5);

            // Move within the same cell
            grid.Update(1, new Vector2(15, 15), 5);

            int[] results = grid.QueryRadius(new Vector2(12, 12), 10, out int count);
            count.Should().Be(1);
            results[0].Should().Be(1);
        }
    }
}
