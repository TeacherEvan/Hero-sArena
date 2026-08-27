# Hero's Arena

A top-down arena shooter built with **Godot 4.3** and **C# (.NET 8)**.
Fight waves of enemies across 4 hero classes; environmental destruction
escalates threat via the Collateral Karma system.

## Tech stack
- Engine: Godot 4.3 (`config/features=PackedStringArray("4.3","C#","2D")`)
- Language: C# / .NET 8 (`TargetFramework=net8.0`, `Nullable=enable`)
- Build SDK: `Godot.NET.Sdk/4.3.0`

## Architecture

### Autoloads (registered in `project.godot`)
| Autoload | File | Role |
|----------|------|------|
| `EventBus` | `scripts/core/EventBus.cs` | Decoupled event hub (enemy killed, wave started/completed, threat changed, environment destroyed, etc.) |
| `GameManager` | `scripts/core/GameManager.cs` | Singleton state: score, wave, threat, kill/destruction counts, game state machine |
| `ObjectPoolManager` | `scripts/core/ObjectPoolManager.cs` | Pre-allocates 5000 projectiles + 10000 decals at startup (zero runtime instantiation) |

### Core systems (`scripts/core/`)
- `SpatialHashGrid.cs` — O(1) average-case collision/spatial queries with a pre-allocated result buffer.
- `FlowFieldPathfinder.cs` — 128×128 flow-field pathfinding on a background worker thread (~4 Hz), double-buffered for thread-safe reads.
- `WaveManager.cs` — Spawns enemies via a Weibull intensity curve; enforces a 2000 concurrent-enemy cap; 5s breather between waves; boss every 300s.
- `LevelProgression.cs` — Kinetic (`D*(1+0.15L)`), energy (`D*L^gamma`), and karma (`ln(e+0.05K)`) formulas; XP curve; random perk offers.
- `InputBuffer.cs` — 6-frame (100ms @ 60Hz) action buffer for responsive input.
- `CollateralKarma.cs` — Tracks environmental destruction; feeds the karma amplifier.

### Content
- Heroes (4): `Atlas`, `Zephyr`, `Synapse`, `Volt` (`scripts/heroes/`, base `HeroBase.cs`).
- Enemies (10): `Drone`, `Brute`, `Sprinter`, `Artillery`, `Shielder`, `Healer`, `Exploder`, `Burrower`, `Parasite`, `Apex` (`scripts/enemies/`, base `EnemyBase.cs`).
- VFX: `DecalSystem`, `HitStop`, `ScreenShake` (`scripts/vfx/`).

## Building
Open `project.godot` in the Godot 4.3 editor, or build the C# project:
```
dotnet build Hero-sArena.csproj
```
The xUnit test project is excluded from the main build via
`<Compile Remove="tests/HeroArena.Tests/**/*.cs" />` in `Hero-sArena.csproj`.

## Testing

There are two test harnesses, and they have different runtime requirements.
See `AGENTS.md` for the canonical, up-to-date description of the test matrix
and the bypass tricks involved.

### 1. xUnit — `tests/HeroArena.Tests/` (project `HeroArena.Tests.csproj`)
Run with:
```
dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj
```
Under the default filter (`Category!=GodotRuntime`), **19 tests pass**
covering pure C# logic (`SpatialHashGridTests`, `SpatialHashGridBenchmarkTests`,
`HeroBaseTests`, `ObjectPoolManagerTests`, plus the SpatialHashGrid-remove
regression test).

Tests that need the Godot native runtime carry
`[Trait("Category","GodotRuntime")]` and are filtered out by the default
command above: `GameManagerTests`, `LevelProgressionTests`.
`CollateralKarmaTests` uses a `RuntimeHelpers.GetUninitializedObject` bypass
to run headlessly (see `AGENTS.md` for the trade-off). The Godot headless
gate below cross-covers its math.

### 2. Godot headless gate — `tests/GodotTests/CoreSystemTests.cs`
A Godot script (`Node`) that self-runs assertions for `SpatialHashGrid`,
`WaveManager`, `FlowFieldPathfinder`, `LevelProgression`, `EntityRegistry`
(registry regression for the F-1 fix), and `CollateralKarma` (math
cross-cover), then quits non-zero on failure. Run with the Godot binary:
```
godot --headless -s res://tests/GodotTests/CoreSystemTests.cs
```
**Note:** no Godot binary is currently installed in this workspace, so
this gate cannot be executed here. The CI `godot-verify` job downloads
Godot 4.3 headless and runs it on every push and PR.

## CI

`.github/workflows/ci.yml` runs six jobs on every push and PR:
- `lint` (Roslynator) — must pass
- `typecheck` — must pass
- `build` — must pass
- `test` (xUnit, `Category!=GodotRuntime`) — must pass
- `godot-verify` (downloads Godot 4.3, runs the headless gate) — must pass
- `export` (Linux/Windows/macOS) — must pass

As of `a3171a5` (PR #18) and `7968f52` (PR #19), all gates must pass
on the branch — `continue-on-error: true` was removed from the
typecheck/build/test jobs.

**Note (2026-08-27):** the GitHub account is currently locked due to a
billing issue, so CI is not running. Tracked in issue #23. Both audit
fix PRs are open and ready to merge once CI is restored.

## Known gaps
- The Godot-dependent xUnit test classes (`GameManagerTests`,
  `LevelProgressionTests`) are gated behind a trait and run in the
  Godot headless gate only. The AGENTS.md-preferred fix is to extract
  the pure logic out of `Node` subclasses so headless `dotnet test`
  covers them too. Tracked in issue #22.
- Bench harnesses (`bench_test/`, `FrameTimeBenchmark.cs`,
  `WaveManagerBenchmark.cs`) do not exercise the production hot path
  (use `Node2D` mocks, miss the per-frame throttle). Tracked in
  issue #20.
- `OnProjectileHit` event has no in-tree consumer; reserved as a public
  hook for VFX/SFX plugins. Tracked in issue #21.
