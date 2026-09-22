# AGENTS.md

Project conventions for autonomous agents (Jules / Copilot / Codex).

## Stack
- Godot 4.3 + C# (.NET 8). `dotnet` 8 in CI; local SDK may be newer (10) — do not pin to 10.
- Root namespace `HeroArena`. Nullable enabled.
- Godot SDK: `Godot.NET.Sdk/4.3.0` (no `.sln` file; single csproj).
- Lint: Roslynator via `dotnet tool run roslynator` (config in `.config/dotnet-tools.json`).
- Local toolchain: .NET 8 SDK at `~/.dotnet8` — prefix every dotnet command
  with `DOTNET_ROOT=~/.dotnet8` (snap `dotnet` shim is broken; `~/.dotnet`
  is v10, never use it). C#-capable Godot at `~/.local/bin/godot-mono`;
  the standard (non-mono) binary cannot load `.cs` — never use it for
  run/test, only the mono build.
- `Hero-sArena.csproj` sets `GenerateAssemblyInfo=false` and
  `GenerateTargetFrameworkAttribute=false`: Godot.NET.Sdk supplies both
  generated files, and the SDK defaults duplicate them on incremental
  builds (CS0579). Do NOT re-enable.

## Repo layout
- Game code: `scripts/**/*.cs` (Godot `Node` subclasses + a few pure C# classes).
- Autoloads (project.godot): `EventBus`, `GameManager`, `ObjectPoolManager`.
- Benchmarks: `bench_test/`, `FrameTimeBenchmark.cs`, `WaveManagerBenchmark.cs` (NOT part of shipped build).
- Exports: `export_presets.cfg` presets "Linux/X11", "Windows Desktop", "macOS".

## Testing — TWO harnesses, different runtimes (READ CAREFULLY)
1. **xUnit** — `tests/HeroArena.Tests/` (project `HeroArena.Tests.csproj`).
   - Run locally: `dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj`
     (prefix `DOTNET_ROOT=~/.dotnet8`; 66 tests pass under the default filter).
   - Works headlessly ONLY for tests that do NOT construct a Godot `Node` subclass.
     PASS: SpatialHashGridTests, SpatialHashGridBenchmarkTests, HeroBaseTests,
            ObjectPoolManagerTests, ProgressionFormulasTests, KarmaAmplifierTests,
            EnemyMutatorSystemTests, EventBusTests.
   - PASS under `[Trait("Category","GodotRuntime")]` filter exclusion (so under
     `dotnet test --filter "Category!=GodotRuntime"` they are skipped):
     `GameManagerTests`, `GameManagerTests_State`, `InputBufferTests` — they `new`
     a `Node` subclass, which requires the Godot native runtime.
   - RETIRED: `LevelProgressionTests` → `ProgressionFormulasTests` and
     `CollateralKarmaTests` (with its `RuntimeHelpers.GetUninitializedObject`
     bypass) → `KarmaAmplifierTests`. Pure math lives in
     `scripts/core/ProgressionFormulas.cs`; the `Node` wrappers delegate 1:1.
     Do NOT add new tests on Godot `Node` types via the bypass trick; extract
     pure logic or add `[Trait("Category","GodotRuntime")]` and grow the
     Godot headless gate instead.
   - The xUnit project is EXCLUDED from the main build via
     `<Compile Remove="tests/HeroArena.Tests/**/*.cs" />` in `Hero-sArena.csproj`.
2. **Godot headless gate** — `tests/GodotTests/GateRunner.cs` (SceneTree entry)
   + `CoreSystemTests.cs` (assertions `Node`, quits non-zero on failure).
   - Correct invocation: `godot --headless -s res://tests/GodotTests/GateRunner.cs`
     (`-s CoreSystemTests.cs` directly can NEVER work — Godot 4 requires the
     entry script to inherit SceneTree/MainLoop).
   - Requires a .NET/mono-enabled Godot 4.3 binary (NOT installed in plain
     `dotnet` environments; locally at `~/.local/bin/godot-mono`).
   - Covers SpatialHashGrid, WaveManager, FlowFieldPathfinder, LevelProgression,
     EntityRegistry (F-1 regression), CollateralKarma math + Node behavior,
     PowerupBannerFactory (timer-leak guard), HitFlash (F-31 consumer).

## CI (`.github/workflows/ci.yml`)
- Must use `${{ github.workspace }}` — NEVER hardcode local absolute paths.
- Jobs (run in order): `lint` → `typecheck` → `build` → `test` → `godot-verify` → `export` → `quality-gates`
- Exact commands used in CI:
  - Lint: `dotnet roslynator analyze Hero-sArena.csproj`
  - Typecheck: `dotnet build Hero-sArena.csproj --configuration Release --no-restore /p:RunAnalyzers=true`
  - Build: `dotnet build Hero-sArena.csproj --configuration Release --no-restore`
  - Test: `dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj --configuration Release --no-restore --filter "Category!=GodotRuntime"`
  - Godot verify: setup-dotnet 8.0.x, download Godot 4.3 MONO headless
    (standard binary cannot load C#), then `godot --headless -s res://tests/GodotTests/GateRunner.cs`
  - Export: `godot --headless --export-release "<preset>" <output_path>` (presets must match export_presets.cfg exactly)
  - Quality gates: `find scripts -name "*.cs" -exec grep -n -i "TODO\|FIXME" {} \;`
- **Note:** GitHub account currently locked (billing); CI not running. Tracked in issue #23.

## Agent-branch hygiene
- Jules branches: `bolt/`, `palette/`, `sentinel/`. Memory in `.jules/` (not .gitignore).
- Before merging any agent branch: check `git merge-base --is-ancestor <tip> main`.
  If true (SUPERSEDED) → do NOT merge, prune instead.
  Large "deletion" counts vs main = stale base, NOT real removals. Never merge those.
- Do not re-fix already-merged work.

## Known production gaps (do not reintroduce)
- xUnit GameManager tests need the Godot runtime (`GameManagerTests`,
  `GameManagerTests_State` carry the trait). `LevelProgression` /
  `CollateralKarma` are DONE (pure `ProgressionFormulas` + delegating
  Nodes). Preferred fix for the remainder: extract pure logic out of
  the `Node` subclasses so `dotnet test` passes headlessly.
- The `RuntimeHelpers.GetUninitializedObject` bypass is RETIRED
  (`CollateralKarmaTests` → `KarmaAmplifierTests`). Never reintroduce
  it: any new test that needs a `Node` subclass adds
  `[Trait("Category","GodotRuntime")]` and lives in the Godot headless
  gate.
- README documents the test reality; keep it in sync.

## Audit history (read before touching anything)
- `audit_report.md` / `review_findings.md` / `fix_summary.md` are advisory artifacts
  generated by the code-review skill. They are GITIGNORED but the audit-driven
  fix branches (e.g. `fix/audit-critical-high`, `fix/audit-medium-cleanup`) are
  on origin and have PRs.
- Three CRITICAL bugs were found and fixed in `fix/audit-critical-high`
  (merged as PR #18; medium cleanup merged as PR #19):
  AoE abilities dealt zero damage, mutator system was dead code, wave-completion
  was never emitted. Read the PRs before re-fixing that work.