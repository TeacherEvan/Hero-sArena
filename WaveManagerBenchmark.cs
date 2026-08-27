// F-24 fix/bench-f24-production-hot-path
//
// Audit F-24: the previous "BASELINE" reported the wall time of one
// synchronous burst of 2000 spawns, and the "OPTIMIZED" reported the
// cumulative CPU time of the throttled loop. Neither number is the one
// the production throttle exists to bound.
//
// What the throttle actually protects is the per-frame spike the player
// would feel as a hitch. The user-facing metric is therefore MAX per-frame
// time, not cumulative CPU. The previous OPTIMIZED print was misleading:
// it summed the time across all 100 frames, so the number grew with the
// total workload instead of measuring the worst single frame.
//
// What this file does now:
//   - Reports MAX per-frame wall time for both cases (the metric that
//     matters for hitches).
//   - Reports TOTAL wall time for context (so a regression that slows
//     the average per-frame time without breaking the max still shows up).
//   - Keeps the `Node2D` mock with a header note explaining the
//     under-count vs `EnemyBase` (a `CharacterBody2D` with shape, mutator
//     state, `_Ready` chain). An `EnemyBase` fixture is out of scope for
//     a micro-bench; calibrate using the in-game frame profiler instead.
//   - Notes that `WaveManager.ProcessPendingSpawns` ALSO bails out when
//     `GameManager.Instance.ActiveEnemyCount >= MAX_ENEMIES` (see
//     scripts/core/WaveManager.cs:105). The bench does not model the
//     active-enemy ceiling, so it over-estimates spawn cost in long runs.

using Godot;
using System;
using System.Diagnostics;

public partial class WaveManagerBenchmark : SceneTree
{
    private const int MaxSpawnsPerFrame = 20; // mirror of WaveManager.MAX_SPAWNS_PER_FRAME
    private const int TotalSpawns = 2000;
    private const double PhysicsStepMs = 1000.0 / 60.0;

    public override void _Initialize()
    {
        var root = new Node();
        var scene = new PackedScene();
        var enemyNode = new Node2D();
        enemyNode.Name = "EnemyMockNode2D";
        scene.Pack(enemyNode);

        // ---- BASELINE: one synchronous burst of TotalSpawns ----
        // Not a production path. Kept for the regression baseline only.
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < TotalSpawns; i++)
        {
            var inst = scene.Instantiate<Node2D>();
            root.AddChild(inst);
        }
        stopwatch.Stop();
        long baselineMaxFrameMs = stopwatch.ElapsedMilliseconds;
        long baselineTotalMs = stopwatch.ElapsedMilliseconds;
        GD.Print($"BASELINE (one burst)        Max Frame: {baselineMaxFrameMs} ms, Total: {baselineTotalMs} ms");
        root.QueueFree();

        // ---- OPTIMIZED: per-physics-frame throttle ----
        // The user-facing number is the per-frame spike. Report MAX and TOTAL.
        var root2 = new Node();
        long optimizedMaxFrameMs = 0;
        long optimizedTotalMs = 0;
        int pending = TotalSpawns;
        while (pending > 0)
        {
            int toSpawn = Math.Min(pending, MaxSpawnsPerFrame);
            var frameSw = Stopwatch.StartNew();
            for (int i = 0; i < toSpawn; i++)
            {
                var inst = scene.Instantiate<Node2D>();
                root2.AddChild(inst);
            }
            frameSw.Stop();
            optimizedMaxFrameMs = Math.Max(optimizedMaxFrameMs, frameSw.ElapsedMilliseconds);
            optimizedTotalMs += frameSw.ElapsedMilliseconds;
            pending -= toSpawn;
            int sleepMs = (int)Math.Max(0, PhysicsStepMs - frameSw.Elapsed.TotalMilliseconds);
            if (sleepMs > 0) System.Threading.Thread.Sleep(sleepMs);
        }
        GD.Print($"OPTIMIZED (throttle, 60Hz)   Max Frame: {optimizedMaxFrameMs} ms, Total: {optimizedTotalMs} ms across {TotalSpawns / MaxSpawnsPerFrame} frames");
        root2.QueueFree();

        Quit();
    }
}
