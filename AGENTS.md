# AGENTS.md

Project conventions for autonomous agents working on Hero's Arena (Godot 4.3 + C#).

## Stack & local toolchain (read first — easy to get wrong)
- Engine: Godot 4.3. Language: C# / .NET 8 (`TargetFramework=net8.0`, `Nullable=enable`). SDK: `Godot.NET.Sdk/4.3.0`. Single csproj (`Hero-sArena.csproj`), no `.sln`. Root namespace `HeroArena`.
- **`dotnet` on PATH is .NET 10 (broken snap shim) and `~/.dotnet` is also v10 — never use either.** Prefix every dotnet command with `DOTNET_ROOT=~/.dotnet8` (.NET 8 SDK at `~/.dotnet8`).
- **Godot:** use `~/.local/bin/godot-mono` (C#-enabled mono build). The standard/non-mono Godot cannot load `.cs` scripts — never use it to run or test.
- `Hero-sArena.csproj` sets `GenerateAssemblyInfo=false` and `GenerateTargetFrameworkAttribute=false` because `Godot.NET.Sdk` supplies both. Do NOT re-enable — incremental builds duplicate them (CS0579).
- Excluded from the shipped build (via `<Compile Remove>` in `Hero-sArena.csproj`): `tests/HeroArena.Tests/**/*.cs`, `bench_test/**/*.cs`, `CheckProcessMode.cs`, `FindPhysics.cs`, `FrameTimeBenchmark.cs`, `WaveManagerBenchmark.cs`.

## Repo layout
- Game code: `scripts/**/*.cs` (Godot `Node` subclasses plus pure C# classes in `scripts/core/`).
- Autoloads (registered in `project.godot`): `EventBus`, `GameManager`, `ObjectPoolManager`.
- Exports: `export_presets.cfg` presets "Linux/X11", "Windows Desktop", "macOS" → `build/`. Preset names must match exactly: `godot --headless --export-release "<preset>" <out>`.

## Commands (CI order: lint → typecheck → build → test → godot-verify → export → quality-gates)
All dotnet commands need `DOTNET_ROOT=~/.dotnet8`.

- Lint: `dotnet tool run roslynator analyze Hero-sArena.csproj` (config `.config/dotnet-tools.json`; restore tools with `dotnet tool restore`).
- Typecheck: `dotnet build Hero-sArena.csproj --configuration Release --no-restore /p:RunAnalyzers=true`
- Build: `dotnet build Hero-sArena.csproj --configuration Release --no-restore`
- Test: `dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj` (default filter — no suites are filtered out). The test project references the main csproj, so a main-project build failure fails the test run.

## Testing
- **Both gates run locally.** xUnit via `dotnet test`; the Godot headless gate via `env DOTNET_ROOT=~/.dotnet8 ~/.local/bin/godot-mono --headless --path . res://tests/GodotTests/CoreSystemTests.tscn` (prints PASS/FAIL lines, exits non-zero on failure, ~5s). Run `dotnet build` first — non-exported runs load the **Debug** assembly. The former "mono hangs in unmanaged code" note was a misdiagnosis of the ObjectPoolManager pre-warm stall (fixed in `2cd98dd`); the gate passes 13/13 (the R-FX surroundings smoke and the enemy target-disposal regression were added to it).
- Convention: extract pure logic out of `Node` wrappers into Godot-free classes in `scripts/core/` (e.g. `GameManagerState`, `InputBufferState`, `ProgressionFormulas`, `WaveFormulas`, `EntityRegistry`, `HitFlashPalette`, `DestructionTracker`, `PowerupBannerSlots`); the `Node` delegates 1:1.
- Do NOT add new tests on Godot `Node` types via `RuntimeHelpers.GetUninitializedObject` — that bypass is deprecated. Extract pure logic or add `[Trait("Category","GodotRuntime")]` and grow the Godot gate.
- Residual deprecated bypass fixtures exist in `HeroBaseTests`, `FlowFieldPathfinderTests`, `EventBusTests`, `EnemyMutatorSystemTests` — they pass headlessly; do not extend them, remove them as each suite is extracted.
- `[Trait("Category","GodotRuntime")]` is a no-op kept for git history; no xUnit suite carries it anymore. CI still passes `--filter "Category!=GodotRuntime"` harmlessly.
- Node-dependent coverage (e.g. `ProjectileBase` : Area2D, `DestructionDrama`/`AmbientWildlife` : Node2D, `ObjectPoolManager` : Node) belongs in the Godot headless gate (`tests/GodotTests/CoreSystemTests.tscn`), not in xUnit via deprecated bypasses. Grow that gate when adding behavior to `Node` subclasses; keep xUnit for pure `scripts/core/` logic only.
- `Godot.Vector2.Zero.Normalized()` returns `Vector2.Zero` (not `NaN`) in Godot 4 — do not invent a zero-direction `Normalize` guard; it is not a defect here (evidenced by `tests/HeroArena.Tests/Systems/AuditRegressionTests.cs`).

## Quality gates
- Must FAIL on any `TODO|FIXME` in `scripts/*.cs`:
  `if grep -rni --include="*.cs" -E "TODO|FIXME" scripts/; then echo "Quality gate failed: TODO/FIXME markers found"; exit 1; fi`
  Do not use `find ... -exec grep` — that returns `find`'s exit code and passes vacuously even with markers present.

## Branch hygiene
- Jules branches: `bolt/`, `palette/`, `sentinel/`; agent memory lives in `.jules/` (not `.gitignore`d).
- Before merging any agent branch: `git merge-base --is-ancestor <tip> main`. If true (branch is SUPERSEDED by main) → prune, do NOT merge. Large deletion counts vs main = stale base, not real removals.
- Do not re-fix already-merged work.

## Audit history (read before touching core systems)
- PRs #18 (`fix/audit-critical-high`) and #19 (`fix/audit-medium-cleanup`) fixed three CRITICAL bugs: AoE abilities dealt zero damage, the mutator system was dead code, wave-completion was never emitted. Read those PRs before re-fighting that work.

## Known gotchas
- CI has never run (GitHub account locked, billing; issue #23). Verify locally with `dotnet build` + `dotnet test` + the Godot gate scene (see Testing) + `dotnet roslynator analyze`.
- Pool pre-warm is order-sensitive: pooled nodes must be deactivated BEFORE `AddChild` (`ObjectPoolManager`) and `ProjectileBase.Deactivate()` must keep parking its `CollisionShape2D` + `Monitoring`/`Monitorable`. Misordering re-creates the super-linear (hours) boot stall (fixed in `2cd98dd`).
- Pool exhaustion contract: `ObjectPoolManager.GetProjectile()` returns `null` when all `MAX_PROJECTILES` (5000) are active; callers must null-check before `Activate`. Exhaustion is effectively unreachable in normal play but the contract is documented.
- Per-frame allocation rule: zero `new Color`/`new Vector2`/object-literal allocations inside `_Process`/`_Render` loops (60–120 Hz). Cache per-entity tints/values at spawn and mutate alpha only each frame; per-frame GC churn can blow the 16.67ms (60Hz) / 8.33ms (120Hz) frame budget.
- Intermittent: a single unexplained `dotnet test` failure has occurred on the very first run right after a fresh build, with no failing test name and no reproduction on re-run (likely a cold-start MSBuild/test-host race). Re-run before investigating.
