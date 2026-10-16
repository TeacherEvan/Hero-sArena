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

## Installing (one click → "Play HeroArena" desktop icon)
Run the included GUI installer:

```
./install.sh
```

It opens a single window: **Install** → choose basic settings (install location, desktop icon, applications-menu entry, launch after install) → **Install**. When it finishes you get a **"Play HeroArena"** icon on your desktop and in your applications menu. Double-click it to play.

- Installer: `install.sh` (uses `zenity`; requires the pre-built `build/linux/Hero-sArena.x86_64` + `.pck`)
- Uninstall: `./install.sh --uninstall` (removes the game and all icons cleanly)
- Install log: `~/.local/share/HeroArena/install.log`
The xUnit test project is excluded from the main build via
`<Compile Remove="tests/HeroArena.Tests/**/*.cs" />` in `Hero-sArena.csproj`.

Local toolchain notes: use the .NET 8 SDK at `~/.dotnet8`
(`DOTNET_ROOT=~/.dotnet8`; the snap `dotnet` shim is broken and
`~/.dotnet` is v10 — do not use it). `Hero-sArena.csproj` sets
`GenerateAssemblyInfo=false` / `GenerateTargetFrameworkAttribute=false`
because `Godot.NET.Sdk` supplies both generated files; leaving the SDK
defaults on breaks incremental builds with CS0579 duplicates.

## Testing

There are two test harnesses, and they have different runtime requirements.
See `AGENTS.md` for the canonical, up-to-date description of the test matrix
and the bypass tricks involved.

### 1. xUnit — `tests/HeroArena.Tests/` (project `HeroArena.Tests.csproj`)
Run with:
```
dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj
```
Under the default filter, **131 tests pass** — with no suite filtered
out anymore (see below). They cover pure C# logic
(`SpatialHashGridTests`,
`SpatialHashGridBenchmarkTests`, `HeroBaseTests`, `ObjectPoolManagerTests`,
`ProgressionFormulasTests`, `KarmaAmplifierTests`, `EnemyMutatorSystemTests`,
`EventBusTests`, plus regression tests), including the systems that used to
have no headless coverage: `EntityRegistryTests` (F-1 registry regression),
`HitFlashPaletteTests` (F-31 damage-type colors + fade curve),
`DestructionTrackerTests` (CollateralKarma counting state),
`PowerupBannerSlotsTests` (HUD banner selection rule),
`WaveFormulasTests` (Weibull wave-intensity curve),
`GameManagerStateTests` (the run state machine: transitions, guards, score
and threat math), and
`InputBufferStateTests` (the 6-frame input buffer: membership, expiry
window, refill-slot semantics — the T3.1-rest closure). Each of these was
extracted from the `Node` wrapper into a Godot-free class that the wrapper
delegates to, mirroring the `ProgressionFormulas` pattern.

**No suite is filtered out anymore**: the last one carrying
`[Trait("Category","GodotRuntime")]` (`InputBufferTests`, 5
bypass-built tests) retired on 2026-10-16 with the `InputBufferState`
extraction, alongside `GameManagerTests` / `GameManagerTests_State`
→ `GameManagerStateTests`. The former `LevelProgressionTests` /
`CollateralKarmaTests` retired the same way. Residual deprecated
`FormatterServices.GetUninitializedObject` construction fixtures remain
in 4 suites (7 call sites — see `AGENTS.md`), but they all run in the
default command, and the surviving Node-behavior checks
(`TestGameManagerDelegation`, `TestInputBufferDelegation`) moved to the
Godot headless gate.

### 2. Godot headless gate — `tests/GodotTests/` (`CoreSystemTests.tscn` + `CoreSystemTests.cs`)
A Godot `Node` that self-runs assertions for `SpatialHashGrid`,
`WaveManager`, `FlowFieldPathfinder`, `LevelProgression`, `EntityRegistry`
(registry regression for the F-1 fix), `CollateralKarma` (math
cross-cover + Node behavior), `PowerupBannerSlots` (HUD delegation
guard — it replaces an older check that grepped for a
`scripts/ui/PowerupBannerFactory.cs` file which never existed in the
repo), and `HitFlash` (F-31 consumer), then quits non-zero on failure.
Run the scene with a **.NET/mono-enabled** Godot 4.3 binary (the standard
headless binary cannot load `.cs` scripts), with `DOTNET_ROOT` pointing at
the 8.0 runtime so the engine finds the .NET host fxr:
```
DOTNET_ROOT=~/.dotnet8 godot --headless res://tests/GodotTests/CoreSystemTests.tscn
```
**⚠ This gate has never run in this workspace: the Godot mono binary here
cannot execute managed (C#) code.** What works: `--headless --quit`,
`--headless --import`, and a GDScript `-s` entry. What does not: any entry
that instantiates a C# script — a scene, `-s` with a `SceneTree` wrapper, a
minimal 3-line C# script with no project dependencies, and the game's own
`scenes/Main.tscn`. C# entries print only the engine banner, never reach
`_Ready`, and spin at ~100% CPU in unmanaged code while every .NET runtime
thread idles on futexes. It is not a missing library (`ldd` is clean), not
the runtime version (identical hang on .NET 8.0.31 and 10.0.5), not the
`DOTNET_EnableDiagnostics` / `gcServer` / `EnableEventPipe` overrides, and
not the project's code (a minimal C#-only scene hangs too). The CI
`godot-verify` job therefore cannot pass until the engine build is fixed or
replaced — treat the xUnit suite as the authoritative gate. An earlier note
in this file blaming an engine "self-fork + SIGSEGV" was a misread of the
test harness wrapper shell and has been removed.

## CI

`.github/workflows/ci.yml` runs seven jobs on every push and PR:
- `lint` (Roslynator) — must pass
- `typecheck` — must pass
- `build` — must pass
- `test` (xUnit, `Category!=GodotRuntime`) — must pass
- `godot-verify` (downloads Godot 4.3 mono, runs the headless gate) — must pass
- `export` (Linux/Windows/macOS) — must pass
- `quality-gates` (fails on any `TODO|FIXME` in `scripts/`) — must pass

As of `a3171a5` (PR #18) and `7968f52` (PR #19), all gates must pass
on the branch — `continue-on-error: true` was removed from the
typecheck/build/test jobs.

**Note (2026-08-27):** the GitHub account is currently locked due to a
billing issue, so CI is not running. Tracked in issue #23. The audit
fix PRs (#18, #19) are merged; local gates (`dotnet build`, `dotnet test`,
Roslynator) mirror CI meanwhile — the Godot gate does not (see above).

## Known gaps
- Closed (2026-10-16): **no xUnit class carries
  `[Trait("Category","GodotRuntime")]` anymore**, so nothing is filtered
  out of the default command, and nothing runs nowhere in CI. The frame
  buffer became the pure `InputBufferState` (the `InputBuffer` Node keeps
  the StringName boundary and tick→expire ordering;
  `TestInputBufferDelegation` guards it in the headless gate) and the run
  state machine became the pure `GameManagerState`
  (`TestGameManagerDelegation` likewise). Residual deprecated
  `FormatterServices.GetUninitializedObject` construction fixtures still
  exist in 4 suites (7 call sites — `HeroBaseTests`,
  `FlowFieldPathfinderTests`, `EventBusTests`,
  `EnemyMutatorSystemTests`), but they headlessly PASS inside the default
  command; extract those states the same way as they surface. Tracked in
  issue #22.
- Bench harnesses were realigned with the production hot path (dfb7acd,
  issue #20 closed): the SceneTree benches report max per-frame wall time
  and model the 60Hz drain of the `MAX_SPAWNS_PER_FRAME` throttle, and the
  console bench brackets `SpatialHashGrid.Update` across four cell-crossing
  cases. Documented residual divergences (the `Node2D` spawn mocks
  under-count vs an `EnemyBase` `CharacterBody2D`; the active-enemy
  ceiling is not modeled) live in each bench header — calibrate against
  the in-game frame profiler.
- `OnProjectileHit` is consumed by `HitFlash` (F-31 fix); audio cues
  and post-processing shaders can subscribe as VFX/SFX plugins.
  Tracked in issue #21.
