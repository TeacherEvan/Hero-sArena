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

### 1. xUnit — `tests/HeroArena.Tests/` (project `HeroArena.Tests.csproj`)
Run with:
```
dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj
```
These tests cover pure C# logic. **Caveat / UNVERIFIED-IN-THIS-ENV:**
tests that construct a Godot `Node` subclass (`GameManagerTests`,
`LevelProgressionTests`) call into the Godot native runtime and
**crash the test host** ("Test host process crashed") unless the Godot
native library is loaded. In a plain `dotnet test` run without the Godot
engine, the suite aborts. As of this writing, the passing classes are
`SpatialHashGridTests`, `SpatialHashGridBenchmarkTests`, `HeroBaseTests`,
`CollateralKarmaTests`, and `ObjectPoolManagerTests` (~17 tests); the
failing/crashing classes are `GameManagerTests` and `LevelProgressionTests`
(~11 tests).

The recommended fix is a design decision, not a one-line patch: either
run the full suite under Godot's own test runner, or extract the pure
logic out of `Node` subclasses so it is unit-testable headlessly.

### 2. Godot headless gate — `tests/GodotTests/CoreSystemTests.cs`
A Godot script (`Node`) that self-runs assertions for `SpatialHashGrid`,
`WaveManager`, `FlowFieldPathfinder`, and `LevelProgression`, then
quits non-zero on failure. Run with the Godot binary:
```
godot --headless -s res://tests/GodotTests/CoreSystemTests.cs
```
**UNVERIFIED-IN-THIS-ENV:** no Godot binary is currently installed in
this workspace, so this gate cannot be executed here. Its assertions
were reconciled to shipped logic in recent commits (`ab8e09b`, `1f9d573`).

## Known gaps
- The Godot-dependent test classes (`GameManagerTests`, `LevelProgressionTests`)
  are unrunnable under plain `dotnet test` and need the Godot runtime or
  a refactor (see Testing / Caveat above).
- `FlowFieldPathfinder` has no unit coverage in either harness.
