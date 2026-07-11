using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using HeroArena;
using Godot;

namespace HeroArena.Tests.Core;

public class SpatialHashGridBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public SpatialHashGridBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BenchmarkGridUpdate()
    {
        var grid = new SpatialHashGrid(64, 1024);
        int numEnemies = 1000;
        Vector2[] positions = new Vector2[numEnemies];

        for (int i=0; i<numEnemies; i++) {
            positions[i] = new Vector2(i * 10, i * 10);
            grid.Insert(i, positions[i], 16f);
        }

        var sw = Stopwatch.StartNew();
        for (int frames=0; frames<1000; frames++) {
            for (int i=0; i<numEnemies; i++) {
                grid.Update(i, positions[i], 16f);
            }
        }
        sw.Stop();
        _output.WriteLine($"Baseline Update 1000 enemies for 1000 frames: {sw.ElapsedMilliseconds} ms");

        // Now with check
        var lastPositions = new Vector2[numEnemies];
        Array.Copy(positions, lastPositions, numEnemies);

        sw.Restart();
        for (int frames=0; frames<1000; frames++) {
            for (int i=0; i<numEnemies; i++) {
                if (positions[i] != lastPositions[i]) {
                    grid.Update(i, positions[i], 16f);
                    lastPositions[i] = positions[i];
                }
            }
        }
        sw.Stop();
        _output.WriteLine($"Optimized Update (no movement): {sw.ElapsedMilliseconds} ms");
    }
}
