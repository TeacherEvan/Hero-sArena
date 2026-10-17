# Project Plan — Hero's Arena

Tracks the state of the codebase, completed work, and outstanding items. Synced with `AGENTS.md`, `README.md`, `tasks/plan.md`, `tasks/todo.md`, and `docs/recommendations.md` (cross-refs at end of this file).

## Stack & local toolchain

- Engine: Godot 4.3. Language: C# / .NET 8 (`TargetFramework=net8.0`, `Nullable=enable`). SDK: `Godot.NET.Sdk/4.3.0`. Single csproj (`Hero-sArena.csproj`), no `.sln`. Root namespace `HeroArena`.
- `dotnet` on PATH is .NET 10 (broken snap shim) and `~/.dotnet` is also v10 — never use either. Prefix every dotnet command with `DOTNET_ROOT=~/.dotnet8` (.NET 8 SDK at `~/.dotnet8`).
- Godot: `~/.local/bin/godot-mono` (C#-enabled mono build). The standard/non-mono Godot cannot load `.cs` scripts — never use it to run or test.
- `Hero-sArena.csproj` sets `GenerateAssemblyInfo=false` / `GenerateTargetFrameworkAttribute=false` (Godot.NET.Sdk supplies both). Do NOT re-enable — incremental builds duplicate them (CS0579).
- Excluded from shipped build (`<Compile Remove>`): `tests/HeroArena.Tests/**/*.cs`, `bench_test/**/*.cs`, `CheckProcessMode.cs`, `FindPhysics.cs`, `FrameTimeBenchmark.cs`, `WaveManagerBenchmark.cs`.

## Repo layout

- Game code: `scripts/**/*.cs` (Godot `Node` subclasses plus pure C# classes in `scripts/core/`).
- Autoloads (registered in `project.godot`): `EventBus`, `GameManager`, `ObjectPoolManager`.
- Exports: `export_presets.cfg` presets "Linux/X11", "Windows Desktop", "macOS" → `build/`. Preset names must match exactly: `godot --headless --export-release "<preset>" <out>`.

## Commands (CI order: lint → typecheck → build → test → godot-verify → export → quality-gates)

All dotnet commands need `DOTNET_ROOT=~/.dotnet8`.

- Lint: `dotnet tool run roslynator analyze Hero-sArena.csproj`
- Typecheck: `dotnet build Hero-sArena.csproj --configuration Release --no-restore /p:RunAnalyzers=true`
- Build: `dotnet build Hero-sArena.csproj --configuration Release --no-restore`
- Test: `dotnet test tests/HeroArena.Tests/HeroArena.Tests.csproj` (default filter — no suites are filtered out)
- Godot headless gate: `env DOTNET_ROOT=~/.dotnet8 ~/.local/bin/godot-mono --headless --path . res://tests/GodotTests/CoreSystemTests.tscn`
- Quality gates: must FAIL on any `TODO|FIXME` in `scripts/*.cs`

## Testing

- **Both gates run locally.** xUnit via `dotnet test`; the Godot headless gate via the mono binary (~5s).
- Convention: extract pure logic out of `Node` wrappers into Godot-free classes in `scripts/core/`; the `Node` delegates 1:1.
- Do NOT add new tests on Godot `Node` types via `RuntimeHelpers.GetUninitializedObject` — that bypass is deprecated.
- Residual deprecated bypass fixtures exist in `HeroBaseTests`, `FlowFieldPathfinderTests`, `EventBusTests`, `EnemyMutatorSystemTests` — pass headlessly; do not extend, remove as each suite is extracted.
- No xUnit suite carries `[Trait("Category","GodotRuntime")]` anymore; the trait is a no-op kept for git history.

## Completed work (by feature)

| Feature | ID | Description | Status | Verified |
|---------|----|-------------|--------|----------|
| Run-state machine extraction | T3.1-rest | `GameManagerState` (transitions, guards, score/threat math, kill math); `GameManager` Node delegates 1:1 | Done | xUnit `GameManagerStateTests` headless |
| Input buffer extraction | T3.1b | `InputBufferState` (6-frame expiry window); `InputBuffer` Node delegates | Done | xUnit `InputBufferStateTests` headless |
| Tutorial / "First Wave" onboarding | R1 | `GameState.Tutorial`, `StartTutorial`/`CompleteTutorial`, `PendingTutorial`, `Tutorial.cs`, `Tutorial.tscn`; WaveManager suppresses Weibull spawning in Tutorial | Done | xUnit `GameManagerStateTutorialTests` headless |
| Difficulty selector | R2 | `Difficulty` enum (Easy/Normal/Hard), `DifficultySettings` payload, persisted to `user://settings.cfg`; `WaveFormulas.CalculateWaveEnemyCount(..., enemyCountMultiplier)`; `EnemyMutatorSystem.ApplyMutators(..., mutatorScale)`; `MainMenu` radio buttons | Done | xUnit `GameManagerStateDifficultyTests`, `MenuDifficultyStateTests` headless |
| Perk transparency | R3 | `PerkSystem` (all 20 perks with names/descriptions/synergy tags), `HeroStats` stat container, `UpgradeMenu` delegates to `PerkSystem.ApplyPerk`, `EventBus.OnPerkChosen` | Done | xUnit `PerkSystemTests` headless |
| Mind Control (Synapse Overclock) | — | `MindControlState` (control timer + contact-hit cooldown), `MindControlTargets` (nearest-target selection); `EnemyBase`/`Synapse` delegate 1:1 | Done | xUnit `MindControlStateTests`, `MindControlTargetsTests` headless |
| FX surroundings (R-FX1/2/3) | — | `LightFXState`/`LivingLightRig`, `ScreenRippleState`/`ScreenDistortionFX`, `DebrisBurstPlan`/`DestructionDrama` | Done | xUnit + gate 11/11 |
| Living arena / wildlife | R-LA1..5 | `AmbientWindState`, `BiomeAtmosphereState`, `GroundDetailPlan`, `WildlifeDirectorState`, `AmbientWildlife` | Done | xUnit + gate 5/5 probe |
| Sandbox mode | R3 | `Sandbox.cs`/`Sandbox.tscn` (dummy spawn, God mode, perk apply, reset) | Done | Real hardware |
| Bench hot-path alignment | T3.3 | `WaveFormulas.MaxEnemies=2000` pinned; bench headers re-pointed | Done | xUnit + console bench green |

## Known gaps / open items

- Godot headless gate **passes 13/13, exit 0** (verified 2026-10-17 via `godot-mono --headless res://tests/GodotTests/CoreSystemTests.tscn`). Previously unrunnable in this container; the mono C# gate now works. CI `godot-verify` still blocked externally (issue #23, billing lock) but the local gate is green.
- Lint: **clean** — `roslynator analyze` exit 0, 0 diagnostics (16 pre-existing compile errors fixed in `SettingsState.cs`, `PauseMenu.cs`, `Settings.cs` on 2026-10-17; no longer a policy call).
- Bench harnesses (T3.3) partially stale post-extraction; headers re-pointed, README bullet refreshed.
- In-tree theme-feature WIP (VisualTheme/HUD/MainMenu/UpgradeMenu/MainBootstrap/icon.svg) being iterated by a separate writer — quarantined, not merged. Do not `git stash pop` blindly.
- Pre-existing stale-hero bug **fixed** (2026-10-17): `HeroBase.Die()` now sets `GameManager.Instance.ActiveHero = null` before `QueueFree()`, preventing `ObjectDisposedException`s at the 16 `ActiveHero` call sites.

## Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Billing lock (CI dark) | High | Local gates mirror CI (`dotnet build` + `dotnet test` + Roslynator + Godot headless gate) |
| Headless hang in container | Resolved | Godot headless gate passes 13/13 locally (2026-10-17) |
| Lint infos (exit 1) | Resolved | `roslynator analyze` exit 0, 0 diagnostics (2026-10-17) |

## Cross-references

- `AGENTS.md` — agent conventions, stack, commands, testing, quality gates, audit history, known gotchas (this plan's operational mirror)
- `README.md` — user-facing overview, tech stack, architecture, building, testing, CI, known gaps
- `tasks/plan.md` + `tasks/todo.md` — task-level tracking (T0.x–T5.x, R1–R3, phase checkpoints)
- `docs/recommendations.md` — R1 (Tutorial), R2 (Difficulty selector), R3 (Perk transparency + Sandbox), R-FX1–3 (FX), R-LA1–5 (Living Arena): endpoint/control specs, file maps, effort estimates, sources
- `docs/ux-feedback-spec.md` — combat-feedback juice spec (damage numbers, wave banners, power-up feedback, low-health state, pause/upgrade UX): EventBus hooks, C# signatures, file map
- `docs/investigation-notes/2026-10-17-*` — per-pass investigation findings (novice onboarding, illusion FX, living arena wildlife)
- `docs/external-research/*` — grounded source material (Godot 4.3 2D lighting/shaders/particles, living-world wildlife, 2D destruction illusions)
- `docs/surgical-investigation-report.*` — investigation report artifacts (html/md/json/sarif)
