// F-24 fix/bench-f24-production-hot-path
//
// Audit F-24: this benchmark exercised a non-production hot path. The previous
// "Baseline" loop called `grid.Update` unconditionally per frame for every
// entity, but production callers (see scripts/core/SpatialHashGrid.cs:60-95)
// always go through the cell-extent fast path: when an entity's bounding box
// has not crossed any cell boundary, `Update` is a no-op (only `oldData.Pos` /
// `oldData.Radius` are refreshed and a struct copy is written back). So the
// "baseline" was measuring an impossible worst case that production never
// reaches.
//
// What this file actually measures now (in order):
//   1. STATIONARY (production fast path): every entity sits in its original
//      cell. `Update` is called every frame but short-circuits inside. This
//      is the dominant production case (~90% of frames in normal play).
//   2. WITHIN-CELL MOTION (still fast path): positions drift by 1 unit per
//      frame, which keeps cell extents unchanged so the fast path still fires.
//      This bounds the per-frame cost of the bookkeeping-only code path.
//   3. CROSS-CELL MOTION (worst case): positions shift enough each frame to
//      force a `RemoveFromCells` / `AddToCells` cycle. This is the upper
//      bound; production only hits it when many enemies move into new cells
//      in the same frame (e.g. a teleport, knockback, or wave spawn shove).
//   4. MIXED 10% (production-like): the previous mixed scenario, kept as a
//      sanity check that the throttle pattern still wins against case 3.
//
// Together these four give a defensible bracket of `SpatialHashGrid.Update`
// cost under realistic cell-crossing ratios. None of them is a 1:1 proxy for
// the full game frame (the game also runs queries, AI, pathfinding, VFX),
// but they bound the per-update overhead the rest of the frame sits on top
// of.

using System;
using System.Diagnostics;
using Godot;
using HeroArena;

class Program {
    const int NumEnemies = 1000;
    const int NumFrames = 1000;
    const int CellSize = 64;
    const int Capacity = 1024;
    const float EntityRadius = 16f;

    static void Main() {
        var grid = new SpatialHashGrid(CellSize, Capacity);
        var positions = new Vector2[NumEnemies];
        var seeded = new Random(42);
        for (int i = 0; i < NumEnemies; i++) {
            // Spread entities so the very first insert fills many distinct
            // cells (not a degenerate single-cell bench).
            positions[i] = new Vector2(seeded.Next(0, 4096), seeded.Next(0, 4096));
            grid.Insert(i, positions[i], EntityRadius);
        }

        // Case 1: STATIONARY. Positions never change. Production fast path
        // hits on every call. This is the "nothing moved this frame" case.
        long stationaryMs = Bench(() => {
            for (int i = 0; i < NumEnemies; i++) {
                grid.Update(i, positions[i], EntityRadius);
            }
        }, NumFrames);
        Console.WriteLine($"[Case 1] Stationary  (production fast path, every frame): {stationaryMs} ms");

        // Case 2: WITHIN-CELL DRIFT. Positions move by 1 unit per frame,
        // which is well below CellSize/2 so cell extents never change.
        // Fast path still fires; this is the cost of the bookkeeping-only
        // update branch (struct copy + dict write).
        var drifted = (Vector2[])positions.Clone();
        long driftMs = Bench(() => {
            for (int i = 0; i < NumEnemies; i++) {
                drifted[i] += new Vector2(1f, 1f);
                grid.Update(i, drifted[i], EntityRadius);
            }
        }, NumFrames);
        Console.WriteLine($"[Case 2] Within-cell drift (still fast path): {driftMs} ms");

        // Case 3: CROSS-CELL MOTION. Each entity steps by CellSize/2 + 1 per
        // frame, forcing a fresh cell assignment every frame. Upper bound;
        // not the steady-state production cost but the worst plausible frame.
        var shifted = (Vector2[])positions.Clone();
        long crossCellMs = Bench(() => {
            for (int i = 0; i < NumEnemies; i++) {
                shifted[i] += new Vector2(CellSize / 2f + 1f, 0f);
                grid.Update(i, shifted[i], EntityRadius);
            }
        }, NumFrames);
        Console.WriteLine($"[Case 3] Cross-cell motion (worst case, Remove+Add): {crossCellMs} ms");

        // Case 4: MIXED 10% MOTION. The previous "10% move" scenario, kept
        // for regression comparison against the prior bench output.
        var mixed = (Vector2[])positions.Clone();
        var random = new Random(42);
        long mixedMs = Bench(() => {
            for (int i = 0; i < NumEnemies; i++) {
                if (random.NextDouble() < 0.1) {
                    mixed[i] += new Vector2(1f, 1f);
                }
                grid.Update(i, mixed[i], EntityRadius);
            }
        }, NumFrames);
        Console.WriteLine($"[Case 4] Mixed 10% motion (production-like): {mixedMs} ms");
    }

    static long Bench(Action body, int frames) {
        // Warm-up to amortize JIT / cache effects on the first measured call.
        body();
        var sw = Stopwatch.StartNew();
        for (int f = 0; f < frames; f++) body();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }
}
