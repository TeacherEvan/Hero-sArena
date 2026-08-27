// F-24 fix/bench-f24-production-hot-path
//
// Audit F-24: this benchmark diverged from the production hot path in three
// ways. (1) The mock scene was a bare `Node2D`, not `EnemyBase`, so it
// understated real frame cost — `EnemyBase` is a `CharacterBody2D` with shape
// registration, mutator state, and an `_Ready` chain. (2) The loop reported
// per-burst CPU time but not the per-frame spike, which is the user-facing
// metric the `MAX_SPAWNS_PER_FRAME = 20` throttle (see
// scripts/core/WaveManager.cs:17) exists to bound. (3) The "throttled" case
// was a free-running `while (pending > 0)` tight loop, not a per-physics-frame
// drain. Production calls `ProcessPendingSpawns` from `WaveManager._PhysicsProcess`
// (scripts/core/WaveManager.cs:55), so a spawn budget of 20 is realized across
// 100 separate frames at 60Hz physics — the bench was simulating the work
// happening in those 100 frames without sleeping between them.
//
// What this file does now:
//   - Keeps the `Node2D` mock (an `EnemyBase` fixture would pull in the full
//     game state — autoloads, project settings, scene tree — and is out of
//     scope for a micro-bench). The header documents that this under-counts
//     real frame cost, and points at the production call site so anyone
//     reading the numbers can calibrate.
//   - Reports MAX per-frame wall time, not cumulative CPU. This is the
//     metric the throttle exists to bound.
//   - Models the per-physics-frame drain (a `Thread.Sleep(physicsStepMs)`
//     between batches). With Godot 4.3 at 60Hz physics that's ~16.6ms per
//     step, so 2000 spawns / 20 per step = 100 steps ≈ 1660ms wall — which
//     is fine because the per-step CPU is the actual number we care about.

using Godot;
using System;
using System.Diagnostics;

public partial class FrameTimeBenchmark : SceneTree
{
    // Mirror of scripts/core/WaveManager.cs. If that constant moves, update
    // this one (the bench is intentionally not linked against the game csproj).
    private const int MaxSpawnsPerFrame = 20;
    private const int TotalSpawns = 2000;
    private const double PhysicsStepMs = 1000.0 / 60.0; // 60Hz

    public override void _Initialize()
    {
        var scene = new PackedScene();
        var enemyNode = new Node2D();
        enemyNode.Name = "EnemyMockNode2D";
        scene.Pack(enemyNode);

        // ---- BASELINE: one synchronous burst of TotalSpawns ----
        // DIVERGENCE NOTE: this is not a path production takes. Production
        // never queues 2000 spawns in a single frame; `ProcessPendingSpawns`
        // is called from `_PhysicsProcess` and drains at most MaxSpawnsPerFrame
        // per step. We keep this number as an upper bound for reference.
        var root1 = new Node();
        long baselineMaxFrameMs = 0;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < TotalSpawns; i++)
        {
            var inst = scene.Instantiate<Node2D>();
            root1.AddChild(inst);
        }
        sw.Stop();
        baselineMaxFrameMs = Math.Max(baselineMaxFrameMs, sw.ElapsedMilliseconds);
        root1.QueueFree();
        GD.Print($"BASELINE (worst case, NOT a production path) Max Frame Time: {baselineMaxFrameMs} ms");

        // ---- OPTIMIZED: production throttle, modeled at physics cadence ----
        // Each "frame" spawns up to MaxSpawnsPerFrame, then sleeps the
        // remainder of the physics step. This is what the game actually
        // experiences: a tiny CPU spike per frame spread over 100 frames.
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
            // Sleep the rest of the physics step to model real frame pacing.
            // Without this, the loop is tighter than the game ever runs.
            int sleepMs = (int)Math.Max(0, PhysicsStepMs - frameSw.Elapsed.TotalMilliseconds);
            if (sleepMs > 0) System.Threading.Thread.Sleep(sleepMs);
        }
        root2.QueueFree();
        GD.Print($"OPTIMIZED (production throttle, 60Hz physics) Max Frame Time: {optimizedMaxFrameMs} ms");
        GD.Print($"OPTIMIZED (production throttle, 60Hz physics) Total Wall:      {optimizedTotalMs} ms across {TotalSpawns / MaxSpawnsPerFrame} frames");

        // The user-facing claim that matters: under the throttle, the worst
        // single frame is at most MaxSpawnsPerFrame instantiations. If that
        // number exceeds the physics budget, the throttle needs a tighter
        // limit; if it sits well under, there is room to raise it.

        Quit();
    }
}
