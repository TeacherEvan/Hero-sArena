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
      (prefix `DOTNET_ROOT=~/.dotnet8`; 104 tests pass under the default filter;
      121 pass unfiltered).
    - Headless coverage: tests that merely *construct* a Godot `Node`
      subclass pass (GodotSharp types are plain managed objects; fixtures
      built on `FormatterServices.GetUninitializedObject` work, though that
      bypass is deprecated — see below). Tests that need `_Ready`,
      `_ExitTree`, or SceneTree callbacks do not run headlessly and belong
      in the Godot gate.
      PASS: SpatialHashGridTests, SpatialHashGridBenchmarkTests, HeroBaseTests,
             ObjectPoolManagerTests, ProgressionFormulasTests, KarmaAmplifierTests,
             EnemyMutatorSystemTests, EventBusTests, WaveFormulasTests, EntityRegistryTests,
             HitFlashPaletteTests, DestructionTrackerTests,
             PowerupBannerSlotsTests.
    - Godot-free classes extracted from `Node` wrappers so the suite can cover
      what used to be gate-only (each `Node` delegates 1:1):
      `scripts/core/EntityRegistry.cs` (F-1 registry),
      `scripts/core/HitFlashPalette.cs` (F-31 colors + fade),
      `scripts/core/DestructionTracker.cs` (CollateralKarma counting),
      `scripts/core/PowerupBannerSlots.cs` (HUD banner selection),
      `scripts/core/WaveFormulas.cs` (Weibull wave-intensity curve).
    - PASS under `[Trait("Category","GodotRuntime")]` filter exclusion (so under
      `dotnet test --filter "Category!=GodotRuntime"` they are skipped):
      `GameManagerTests`, `GameManagerTests_State`, `InputBufferTests`. **The
      trait is now STALE here**: those 17 tests pass headlessly when run
      unfiltered, but they still construct `Node`s via the
      `FormatterServices.GetUninitializedObject` bypass that this file records
      as DEPRECATED — remove the bypass first, then drop the trait.
    - RETIRED: `LevelProgressionTests` → `ProgressionFormulasTests` and
      `CollateralKarmaTests` (with its `RuntimeHelpers.GetUninitializedObject`
      bypass) → `KarmaAmplifierTests`. Pure math lives in
      `scripts/core/ProgressionFormulas.cs`; the `Node` wrappers delegate 1:1.
      Do NOT add new tests on Godot `Node` types via the bypass trick; extract
      pure logic or add `[Trait("Category","GodotRuntime")]` and grow the
      Godot headless gate instead.
      NOTE: the bypass is deprecated, not gone — 14 call sites remain in 7
      construction-only suites (`HeroBaseTests`, `EnemyMutatorSystemTests`,
      `EventBusTests`, `FlowFieldPathfinderTests`, `GameManagerTests`,
      `GameManagerTests_State`, `InputBufferTests`). All pass headlessly;
      none should be extended. Remove them as each suite is extracted.
    - The xUnit project is EXCLUDED from the main build via
      `<Compile Remove="tests/HeroArena.Tests/**/*.cs" />` in `Hero-sArena.csproj`.
  2. **Godot headless gate** — `tests/GodotTests/CoreSystemTests.tscn`
     (scene entry; script `CoreSystemTests.cs`, an assertions `Node` that
     runs in `_Ready` and quits non-zero on failure).
     - Correct invocation: `DOTNET_ROOT=~/.dotnet8 godot --headless res://tests/GodotTests/CoreSystemTests.tscn`
       Run the SCENE, not `-s`: Godot 4's `-s <file>` requires a
       `SceneTree`/`MainLoop` entry (the retired `GateRunner.cs` wrapper), and
       **C# scripts cannot run at all in `-s` mode on this machine** (see the
       blocked note). A scene entry exercises autoloads and `_Ready` normally.
     - Requires a .NET/mono-enabled Godot 4.3 binary (NOT installed in plain
       `dotnet` environments; locally at `~/.local/bin/godot-mono`).
     - **⚠ THIS GATE HAS NEVER RUN in this workspace — the Godot mono binary
       here cannot execute managed (C#) code.** Verified 2026-10-16; what works:
       `--headless --quit`, `--headless --import`, and a GDScript `-s` entry.
       What does not: ANY entry that instantiates a C# script — `-s
       GateRunner.cs`, `CoreSystemTests.tscn`, a minimal 3-line C# scene with
       no project dependencies, and the game's own `scenes/Main.tscn`. Every
       C# entry prints only the engine banner, never reaches `_Ready`, and the
       main thread spins at ~100% CPU inside unmanaged code while every .NET
       runtime thread idles on futexes (gdb stack available in session notes;
       no symbols). Not a missing library (`ldd` clean), not the runtime
       version (hangs identically on .NET 8.0.31 and 10.0.5), not affected by
       `DOTNET_EnableDiagnostics` / `gcServer` / `EnableEventPipe` overrides,
       and not the project's own code (minimal C# scene hangs too).
       An earlier diagnosis here blaming an engine "self-fork + SIGSEGV" was
       WRONG — that was the harness wrapper shell; remove it before re-using.
       Until the engine is fixed/replaced, xUnit is the ONLY authoritative
       gate — which is why the gate-only logic listed above now lives in
       Godot-free classes.
     - Covers SpatialHashGrid, WaveManager, FlowFieldPathfinder, LevelProgression,
       EntityRegistry (F-1 regression), CollateralKarma math + Node behavior,
       PowerupBannerSlots (HUD delegation guard; it replaced an older check that
      grepped a `scripts/ui/PowerupBannerFactory.cs` file which NEVER EXISTED —
      that check would have failed instantly had the gate ever run),
      HitFlash (F-31 consumer).

## CI (`.github/workflows/ci.yml`)
- Must use `${{ github.workspace }}` — NEVER hardcode local absolute paths.
- Jobs (run in order): `lint` → `typecheck` → `build` → `test` → `godot-verify` → `export` → `quality-gates`
- Exact commands used in CI:
  - Lint: `dotnet roslynator analyze Hero-sArena.csproj`
  - Typecheck: `dotnet build Hero-sArena.csproj --configuration Release --no-restore /p:RunAnalyzers=true`
  - Build: `dotnet build Hero-sArena.csproj --configuration Release --no-restore`
  - Test: `dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj --configuration Release --no-restore --filter "Category!=GodotRuntime"`
   - Godot verify: setup-dotnet 8.0.x, download Godot 4.3 MONO headless
     (standard binary cannot load C#), then `DOTNET_ROOT=… godot --headless res://tests/GodotTests/CoreSystemTests.tscn`
     (see the gate note above: unrunnable in this workspace today).
  - Export: `godot --headless --export-release "<preset>" <output_path>` (presets must match export_presets.cfg exactly)
  - Quality gates: must FAIL on any `TODO|FIXME` in `scripts/*.cs`, e.g.
    `grep -rni --include="*.cs" -E "TODO|FIXME" scripts/` (grep's exit
    code). The previous form — `find scripts -name "*.cs" -exec grep ...`
    — evaluated `find`'s exit code, NOT grep's, so it passed vacuously
    even with TODO/FIXME markers present.
- **Note:** GitHub account currently locked (billing); CI not running. Tracked in issue #23.

## Agent-branch hygiene
- Jules branches: `bolt/`, `palette/`, `sentinel/`. Memory in `.jules/` (not .gitignore).
- Before merging any agent branch: check `git merge-base --is-ancestor <tip> main`.
  If true (SUPERSEDED) → do NOT merge, prune instead.
  Large "deletion" counts vs main = stale base, NOT real removals. Never merge those.
- Do not re-fix already-merged work.

## Known production gaps (do not reintroduce)
- The Godot headless gate cannot run in this workspace (the mono binary
  hangs in unmanaged code before any C# `_Ready` fires — full evidence in
  the testing section above; an older "self-fork + SIGSEGV in
  libcoreclr.so" diagnosis was WRONG and has been corrected there). So
  `godot-verify` in CI would hang/fail, and nothing else executes the
  gate's content. Repair the toolchain (or replace the engine build)
  before relying on the gate for anything.
- `GameManagerTests` / `GameManagerTests_State` / `InputBufferTests` carry
  `[Trait("Category","GodotRuntime")]` and still instantiate `Node`s with the
  DEPRECATED `FormatterServices.GetUninitializedObject` bypass (14 legacy
  construction-only sites total — see the testing section), even though they
  pass headlessly in this environment. Remove the bypass first (extract state,
  as done for ProgressionFormulas/EntityRegistry/…), then drop the trait.
- `LevelProgression` / `CollateralKarma` math is DONE (pure
  `ProgressionFormulas` + delegating Nodes), and the former gate-only systems
  (EntityRegistry, HitFlash, DestructionTracker, PowerupBannerSlots, WaveFormulas) are DONE.
- The `RuntimeHelpers.GetUninitializedObject` bypass is DEPRECATED and
  must not be extended: the retired pattern was `CollateralKarmaTests` →
  `KarmaAmplifierTests`, and any NEW test that needs a `Node` subclass
  adds `[Trait("Category","GodotRuntime")]` and lives in the Godot
  headless gate. (14 construction-only call sites remain in existing
  suites — see the testing section.)
- README documents the test reality; keep it in sync.
- CI has never run (GitHub account locked, billing, issue #23). Proven
  green locally as of 2026-10-16: `dotnet build` (Debug and Release),
  `dotnet test` (filtered and unfiltered), and
  `dotnet roslynator analyze` — nothing beyond that.
- Known intermittent (2026-10): exactly one `dotnet test` failure has been
  observed twice, both times on the very first run immediately after a fresh
  build (`Failed: 1, Passed: N-1`), with no failing test name printed and no
  reproduction in ~50 subsequent runs (including 16 with forced rebuilds).
  Most likely a cold-start race between MSBuild finishing writes into the
  shared `.godot/mono/temp/bin/` output path and the test host loading the
  assembly — benign, but treat a single unexplained failure right after a
  build as "re-run before investigating".

## Audit history (read before touching anything)
- `audit_report.md` / `review_findings.md` / `fix_summary.md` are advisory artifacts
  generated by the code-review skill. They are GITIGNORED but the audit-driven
  fix branches (e.g. `fix/audit-critical-high`, `fix/audit-medium-cleanup`) are
  on origin and have PRs.
- Three CRITICAL bugs were found and fixed in `fix/audit-critical-high`
  (merged as PR #18; medium cleanup merged as PR #19):
  AoE abilities dealt zero damage, mutator system was dead code, wave-completion
  was never emitted. Read the PRs before re-fixing that work.