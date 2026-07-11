using Godot;
using System;
using System.Diagnostics;

public partial class FrameTimeBenchmark : SceneTree
{
    public override void _Initialize()
    {
        var scene = new PackedScene();
        var enemyNode = new Node2D();
        enemyNode.Name = "EnemyMock";
        scene.Pack(enemyNode);

        long maxFrameTimeSync = 0;
        long maxFrameTimeAsync = 0;

        // Baseline: 1 spike of 2000 spawns
        var root1 = new Node();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
        {
            var inst = scene.Instantiate<Node2D>();
            root1.AddChild(inst);
        }
        sw.Stop();
        maxFrameTimeSync = sw.ElapsedMilliseconds;
        root1.QueueFree();

        // Optimized: max per frame
        var root2 = new Node();
        int pending = 2000;
        while (pending > 0)
        {
            int toSpawn = Math.Min(pending, 20);
            sw.Restart();
            for (int i = 0; i < toSpawn; i++)
            {
                var inst = scene.Instantiate<Node2D>();
                root2.AddChild(inst);
            }
            sw.Stop();
            maxFrameTimeAsync = Math.Max(maxFrameTimeAsync, sw.ElapsedMilliseconds);
            pending -= toSpawn;
        }
        root2.QueueFree();

        GD.Print($"BASELINE Max Frame Time (Spike): {maxFrameTimeSync} ms");
        GD.Print($"OPTIMIZED Max Frame Time (Time-Sliced): {maxFrameTimeAsync} ms");
        Quit();
    }
}
