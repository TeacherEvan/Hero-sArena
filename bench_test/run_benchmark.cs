using System;
using System.Diagnostics;
using Godot;
using HeroArena;

class Program {
    static void Main() {
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
        Console.WriteLine($"Baseline Update 1000 enemies for 1000 frames: {sw.ElapsedMilliseconds} ms");

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
        Console.WriteLine($"Optimized Update (no movement): {sw.ElapsedMilliseconds} ms");

        // Let's also do a mixed case where some enemies move.
        // Assuming 10% move every frame.
        sw.Restart();
        var random = new Random(42);
        for (int frames=0; frames<1000; frames++) {
            for (int i=0; i<numEnemies; i++) {
                if (random.NextDouble() < 0.1) {
                    positions[i] += new Vector2(1, 1);
                }
                if (positions[i] != lastPositions[i]) {
                    grid.Update(i, positions[i], 16f);
                    lastPositions[i] = positions[i];
                }
            }
        }
        sw.Stop();
        Console.WriteLine($"Optimized Update (10% movement): {sw.ElapsedMilliseconds} ms");
    }
}
