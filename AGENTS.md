# AGENTS.md

Project conventions for autonomous agents (Jules / Copilot / Codex).

## Stack
- Godot 4.3 + C# (.NET 8). `dotnet` 8 in CI; local SDK may be newer (10) — do not pin to 10.
- Root namespace `HeroArena`. Nullable enabled.

## Repo layout
- Game code: `scripts/**/*.cs` (Godot `Node` subclasses + a few pure C# classes).
- Autoloads (project.godot): `EventBus`, `GameManager`, `ObjectPoolManager`.
- Benchmarks: `bench_test/`, `FrameTimeBenchmark.cs`, `WaveManagerBenchmark.cs` (NOT part of shipped build).

## Testing — TWO harnesses, different runtimes (READ CAREFULLY)
1. **xUnit** — `tests/HeroArena.Tests/` (project `HeroArena.Tests.csproj`).
   - Run locally: `dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj`
   - Works headlessly ONLY for tests that do NOT construct a Godot `Node` subclass.
     PASS: SpatialHashGridTests, SpatialHashGridBenchmarkTests, HeroBaseTests,
            CollateralKarmaTests, ObjectPoolManagerTests.
   - CRASHES ("Test host process crashed") under plain `dotnet test` for:
     `GameManagerTests`, `LevelProgressionTests` — they `new` a `Node` subclass,
     which requires the Godot native runtime.
   - The xUnit project is EXCLUDED from the main build via
     `<Compile Remove="tests/HeroArena.Tests/**/*.cs" />` in `Hero-sArena.csproj`.
2. **Godot headless gate** — `tests/GodotTests/CoreSystemTests.cs` (autoload-style `Node`,
   self-runs assertions in `_Ready()`, quits non-zero on failure).
   - Correct invocation: `godot --headless -s res://tests/GodotTests/CoreSystemTests.cs`
   - Requires the Godot 4.3 binary (NOT installed in plain `dotnet` environments).
   - Covers SpatialHashGrid, WaveManager, FlowFieldPathfinder, LevelProgression.

## CI (` .github/workflows/ci.yml`)
- Must use `${{ github.workspace }}` — NEVER hardcode local absolute paths.
- The `test` job must actually RUN `dotnet test`, not just build.
- Godot jobs must download Godot 4.3 headless and run the headless gate.
- Export presets live in `export_presets.cfg`; preset names must match the
  `--export-release` args exactly ("Linux/X11", "Windows Desktop", "macOS").

## Agent-branch hygiene
- Jules branches: `bolt/`, `palette/`, `sentinel/`. Memory in `.jules/` (not .gitignore).
- Before merging any agent branch: check `git merge-base --is-ancestor <tip> main`.
  If true (SUPERSEDED) → do NOT merge, prune instead.
  Large "deletion" counts vs main = stale base, NOT real removals. Never merge those.
- Do not re-fix already-merged work.

## Known production gaps (do not reintroduce)
- xUnit GameManager/LevelProgression tests need the Godot runtime. Preferred fix:
  extract pure logic out of the `Node` subclasses so `dotnet test` passes headlessly.
- README documents the test reality; keep it in sync.
