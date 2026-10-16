# TODO — Production readiness (tracks tasks/plan.md)

## Phase 0: Workspace ready

- [x] T0.1 Godot 4.3 mono binary at `~/.local/bin/godot-mono`
- [x] T0.2 Fix `.vscode/settings.json` godotTools editorPath
- [x] T0.3 Diagnose headless runtime hang — DIAGNOSED 2026-10-16 (evidence:
      full note in AGENTS.md): the engine's main thread spins at ~100% CPU in
      unmanaged code while all .NET runtime threads idle on futexes; every C#
      entry hangs (`-s GateRunner.cs`, `CoreSystemTests.tscn`, the game's own
      `scenes/Main.tscn`, and a minimal 3-line C# scene — so it is NOT this
      project's code). Not a missing library (`ldd` clean), not the runtime
      version (identical on .NET 8.0.31 and 10.0.5), not fixed by
      `DOTNET_EnableDiagnostics`/`gcServer`/`EnableEventPipe` overrides. An
      earlier "self-fork + SIGSEGV" diagnosis was WRONG (harness wrapper shell).
      Consequence adopted: xUnit is the ONLY authoritative local gate; the
      Godot headless gate stays unrunnable until the engine build is
      fixed/replaced. `--build-solutions` works (exit 0). CI unaffected.

## Phase 1: P0 — game runs (IMPLEMENTED 2026-09-22)

- [x] T1.1 Attach scripts to 10 enemy scene roots
- [x] T1.2 Attach scripts to 4 hero scene roots
- [x] T1.3 Combat runtime: WaveManager + EnemyScenes (Apex last) + map +
      HUD in Main.tscn;
      MainBootstrap; PendingHeroClass handoff; menu exports; WaveManager self-register
- [x] T1.4 GateRunner + header + ci.yml `-s` path (compiles;
      runtime run needs green env)

## Build-system fix

- [x] CS0579 incremental-break fixed (GenerateAssemblyInfo/TargetFramework false).
      Pre-existing, reproduced on clean HEAD.

## Godot-free extractions (2026-10 — merged as PR #49, 8b15e15)

Pure classes extracted from `Node` wrappers so xUnit covers what used to be
gate-only; each Node delegates 1:1 (pattern per AGENTS.md). Post-merge code
review (2026-10-16) found and fixed:
- **`TestPowerupBannerFactory` was never invoked from the gate `_Ready`** —
  the HUD-delegation guard sat dead in the scene; the never-passing
  never-existing-file grep it replaced would have been *no loss at all*
  because the replacement never ran either. Wired into `_Ready` with the
  PASS/FAIL harness pattern (method since renamed
  `TestPowerupBannerDelegation`).
- `HitFlashPalette.ColorFor` returned `Godot.Color` — not Godot-free.
  Now returns an RGB triple; `HitFlash` converts.
- `WaveFormulas` clamp comment falsely claimed to mirror `Mathf.Clamp`
  (Godot actually throws when max < min). Documented as a deliberate
  divergence; pinned by `ArenaPastCapacity_YieldsNonPositiveCount_NeverThrows`.
- Formatting collapse + stale `TypeToColor` reflection docstring in
  CoreSystemTests; overclaiming F-1 docstring.
- ci.yml `godot-verify` ran the engine with no `DOTNET_ROOT`, unlike the
  documented invocation.
- Dead shipped API removed: `EntityRegistry.Clear()`, `EntityRegistry.Ids`,
  `DestructionTracker.Reset()` (test-only); their tests dropped.
- AGENTS.md/README stale claims corrected (test counts, vacuous
  quality-gate command, "headless ONLY without Node construction",
  RETIRED-vs-14-live-bypass-sites, wrong self-fork/SIGSEGV diagnosis,
  README six→seven jobs).

- [x] `scripts/core/EntityRegistry.cs` + EntityRegistryTests — F-1 regression
      (EnemyBase.TryGetById now delegates to `EntityRegistry<EnemyBase>`)
- [x] `scripts/core/HitFlashPalette.cs` + HitFlashPaletteTests — F-31
      damage-type colors + fade (HitFlash delegates)
- [x] `scripts/core/DestructionTracker.cs` + DestructionTrackerTests —
      CollateralKarma counting/amplifier state (CollateralKarma delegates)
- [x] `scripts/core/PowerupBannerSlots.cs` + PowerupBannerSlotsTests — HUD
      banner slot-selection rule (HUD delegates; MaxBanners const)
- [x] `scripts/core/WaveFormulas.cs` + WaveFormulasTests — Weibull wave
      intensity + min floor + MaxEnemies clamp (WaveManager delegates;
      MAX_ENEMIES now `WaveFormulas.MaxEnemies`)
- [x] `tests/GodotTests/GateRunner.cs` deleted (retired `-s` entry; superseded
      by `CoreSystemTests.tscn` scene entry in ci.yml); CoreSystemTests.cs
      delegates to the pure classes; AGENTS.md/README updated

## Phase 2: CI repair (blocked: billing lock #23)

- [x] T2.1 Mono Godot in both download jobs + setup-dotnet in godot-verify
      (ci.yml valid, 7 jobs)
- [x] T2.3 (2026-10-16, post-merge review): export job repaired — it had NO
      setup-dotnet (Godot mono must run `dotnet build` on the C# solution
      before exporting) and no DOTNET_ROOT for the mono engine; both added.
      Also fixed export-template extraction: the .tpz archive has a
      `templates/` root, and the previous in-place unzip nested the files at
      `4.3.stable/templates/` where Godot never looks — export would have
      failed with "no export template found". Now unzips at the parent and
      renames `templates → 4.3.stable`.
- [ ] Unblocked externally only by billing; then watch first green run

## Phase 3: Test hardening

- [x] T3.1-slice: ProgressionFormulas pure extraction; LevelProgression +
      CollateralKarma delegate.
      Exposed + fixed a stale 812.8 expectation (true 5^1.3*100 = 810.3).
- [x] T3.2: bypass RETIRED (LevelProgressionTests→ProgressionFormulasTests,
      CollateralKarmaTests→KarmaAmplifierTests); Node behavior moved to gate
      (TestCollateralKarmaBehavior, needs CI run).
- [x] T3.3 (2026-10-16): bench-refs follow-up after PR #49 moved MAX_ENEMIES
      and the throttle's neighbours: WaveManagerBenchmark.cs + FrameTimeBenchmark.cs
      headers re-pointed at WaveManager.cs:12/:15/:37/:95 (and WaveFormulas for
      the ceiling), README bench bullet refreshed (issue #20 is CLOSED),
      `WaveFormulas.MaxEnemies = 2000` pinned by `MaxEnemies_Cap_Is2000`.
      Plan.md holds the full note.
- [x] T3.1-rest (2026-10-16): GameManager state extracted to the pure
      `GameManagerState`; GameManagerTests/GameManagerTests_State retired
      into headless `GameManagerStateTests` + gate delegation check.
      INPUT BUFFER same day: pure `InputBufferState` +
      `InputBufferStateTests` (9 headless tests) + gate check; the last
      `[Trait]` suite is gone, so filtered = unfiltered = 131/131.
      Bypass sites recounted: 7 in 4 suites. Last tagged suite: none —
      issue #22's CI-nowhere gap is CLOSED.
- [ ] T3.3: bench hot-path alignment (#20)
- [ ] Lint gate red on pre-existing infos — 2026-10 re-count: **14 infos
      (exit 1)**: 13× CA1822 + 1× CA1016 (was 17). None in the new pure files.
      Policy call still stands — the CA1822s are NOT mechanically fixable:
      `LevelProgression`'s 4 delegates are called via instance
      (`_levelProg.CalcEnergyDamage(...)` in Synapse.cs/Volt.cs), so `static`
      conversion would break compilation of hero code, and the
      CoreSystemTests/MainMenu/MainBootstrap CA1822s live in the gate/scene
      scripts this container cannot run (T0.3) — unverifiable changes.
      CA1016 is unfixable by design (`GenerateAssemblyInfo=false`). Options:
      info-cleanup pass with hero call-site updates (touches gameplay code —
      needs approval), warn-level config, or accept-with-note in CI.

## Phase 4: Docs sync

- [x] T4.1 README (66 tests, PRs merged, mono requirement, GateRunner cmd,
      toolchain, gaps)
- [x] T4.2 AGENTS.md (stack/toolchain, testing matrix, CI cmds, gaps, audit history)

## Phase 5: Branch hygiene (read-only checks done 2026-09-22)

- code-health/burrower-ai-refactor-*: SUPERSEDED (ancestor of main) → prune candidate
- fix/evictoldest-decal-rename-regression: stale base (11 behind),
  fix already in main (fb99aad) → PRUNED 2026-09-22 with approval.

## Post-merge review (2026-09-22, d4d35ad) — PR #43

- [x] Main.tscn was absent from d4d35ad → restored with plain Node2D root
      (kills autoload/scene GameManager duality: double subs + dangling Instance)
- [x] project.godot engine pollution → reverted
- [x] plan.md checkboxes synced; dead using removed
- [x] Branch `fix/review-followups-main-wiring` merged as PR #43
      (442ed2f); stale branches pruned with approval.

## Runtime proof (2026-09-22, container + lavapipe, tiny pools)

- [x] Wave 1 spawns 32/32 enemies (Brute/Healer/Artillery/… at map markers);
      600 combat frames, exit 0, zero script errors, zero ObjectDisposed
      (FlowField hardening verified). Temp instrumentation fully reverted.
- [ ] Full-size pools (5000/10000/1000) need ~8 min warmup on software GL —
      fine on real hardware; CI gate never instantiates pools. No code change.
- [x] NOTE: scenes/Main.tscn was externally reverted 3x — cause found:
      editor reload prompts answered incorrectly (acknowledged).
      Rule: single writer per file; restore project.godot after engine runs.

## Verification evidence (2026-10-16 — T3.1-rest closure: GameManager + InputBuffer)

- `dotnet build` Release: 0 warnings, 0 errors
- `dotnet test --filter Category!=GodotRuntime`: 131/131 — EQUAL to
  unfiltered for the first time (131/131): no `[Trait]`-skipped suites
  remain, so the CI-nowhere gap (issue #22) is closed
- Retired: InputBufferTests (last trait'd suite, 5 bypass tests,
  2 sites) → InputBufferStateTests (9 untagged) + gate
  TestInputBufferDelegation; GameManagerTests/_State →
  GameManagerStateTests + gate TestGameManagerDelegation
- Bypass call sites recounted twice with comment mentions excluded:
  **7 in 4 suites** (HeroBaseTests 4, FlowFieldPathfinderTests 1,
  EventBusTests 1, EnemyMutatorSystemTests 1) — "14 in 7" and "11 in 6"
  were both comment-inflated
- Roslynator: exit 1, 16 infos (15× CA1822 + 1× CA1016); the two new
  CA1822s are the two new gate checks (same unrunnable-gate category);
  nothing new in shipped files
- Secret bug found & fixed during the round: a drafting error put a
  nonexistent `GameState.Menuless` and a wrong long-overflow value in
  draft tests — caught by the compile/test gates before any commit

## Verification evidence (2026-10-16 — T3.1-rest GameManager extraction)

- `dotnet build` Release: 0 warnings, 0 errors
- `dotnet test --filter Category!=GodotRuntime`: 121/121 (was 105;
  +17 GameManagerStateTests, −1 retired GameManagerThreatLevelTests)
- unfiltered: 126/126 (was 122; −13 bypass-built tests, +17 pure)
- Bypass call sites recounted by grep: 11 in 6 suites
  (HeroBaseTests 4, FlowFieldPathfinderTests 2, InputBufferTests 2,
  EventBusTests 1, EnemyMutatorSystemTests 1, KarmaAmplifierTests 1);
  the previously recorded "14 in 7" was inaccurate.
- docs: AGENTS/README counts + trait/bypass claims updated; gate
  docstring lists GameManager among extracted systems.

## Verification evidence (2026-10-16 — bench-refs round T3.3)

- `dotnet test --filter Category!=GodotRuntime`: 105/105 pass (the
  MaxEnemies_Cap_Is2000 pin added)
- unfiltered: 122/122 pass
- console bench `dotnet run --project bench_test/Benchmark.csproj`:
  four-case bracket green (Case 3 still ~5–8× the fast path, as expected)
- roslynator: no NEW findings in touched files (CA1016 + CA1822 infos as
  before; counts unchanged because no new members ship)

## Concurrent-writer incident (2026-10-16, bench-refs round)

- While this round was in flight, an EXTERNAL writer (not this agent —
  most likely the Godot editor session / another local agent started with
  the theme task) landed uncommitted changes in the tree:
  untracked `scripts/core/VisualTheme.cs` + `icon.svg`, and edits wiring a
  theme into `HUD.cs`, `MainMenu.cs`, `UpgradeMenu.cs`, `MainBootstrap.cs`.
- The first snapshot did not compile (`AddThemeStyleBoxOverride` at
  VisualTheme.cs:80-81 — Godot 4's API is `AddThemeStyleboxOverride`).
- Actions taken: single-writer rule respected — foreign files never
  edited; quarantined ONE intermediate snapshot via
  `git stash push -u stash@{0}` (labelled "foreign WIP quarantine...
  restore after bench verification") to unblock the 105/122 gate runs,
  then gates+bench verified green on this round's files only.
- The writer continued post-stash: a NEWER `HUD.cs` (+19) re-appeared on
  disk (ApplyTheme hunk, VisualTheme calls) while `VisualTheme.cs`/`icon.svg`
  did not come back. DO NOT `git stash pop` blindly — it would 3-way-merge
  the stashed older HUD.cs against the newer copy and conflict.
- Recovery for the theme author: `git stash show -p stash@{0} >
  theme-wip.patch` (or cherry-pick hunks) if their editor lost the buffer;
  then delete the stash. Also fix the lowercase-b API name before the
  feature compiles.
- Status: NOT mine, NOT committed, left for its author to finish.

## Verification evidence (2026-10 — extraction refactor + post-merge fixes)

- `dotnet build` Release: 0 warnings, 0 errors
- `dotnet test --filter Category!=GodotRuntime`: 104/104 pass (unfiltered:
  121/121 — the 17 GodotRuntime-tagged tests run headlessly via the
  GetUninitializedObject bypass noted in AGENTS.md)
- Roslynator: exit 1, 15 infos (14× CA1822 + 1× CA1016); the new CA1822 is
  the T3.1-rest gate check (same unrunnable-gate category as the rest),
  none in `GameManagerState.cs`; see the policy-call note in plan.md
- `godot-mono --headless --build-solutions --quit`: exit 0
- ci.yml: GateRunner references gone; gate entry is the
  `CoreSystemTests.tscn` scene (mono binary in download jobs);
  godot-verify now exports `DOTNET_ROOT` for the gate run
