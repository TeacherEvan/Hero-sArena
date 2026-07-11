using Godot;
using System;
using System.Diagnostics;

public partial class WaveManagerBenchmark : SceneTree
{
    public override void _Initialize()
    {
        var root = new Node();
        var scene = new PackedScene();
        var enemyNode = new Node2D();
        enemyNode.Name = "EnemyMock";
        scene.Pack(enemyNode);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Simulate spawning 2000 enemies at once
        for (int i = 0; i < 2000; i++)
        {
            var inst = scene.Instantiate<Node2D>();
            root.AddChild(inst);
        }

        stopwatch.Stop();
        GD.Print($"BASELINE: Instantiating 2000 enemies synchronously took: {stopwatch.ElapsedMilliseconds} ms");

        // Clean up
        root.QueueFree();

        // Now simulate time-slicing (e.g., 20 per frame)
        var stopwatchAsm = new Stopwatch();
        stopwatchAsm.Start();
        int pending = 2000;
        int maxPerFrame = 20;

        while (pending > 0)
        {
            var frameRoot = new Node();
            int toSpawn = Math.Min(pending, maxPerFrame);
            for(int i = 0; i < toSpawn; i++)
            {
                var inst = scene.Instantiate<Node2D>();
                frameRoot.AddChild(inst);
            }
            pending -= toSpawn;
            frameRoot.QueueFree();
        }

        stopwatchAsm.Stop();
        GD.Print($"OPTIMIZED: Instantiating 2000 enemies time-sliced took: {stopwatchAsm.ElapsedMilliseconds} ms (total CPU time)");

        Quit();
    }
}
