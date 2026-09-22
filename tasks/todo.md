# TODO — Production readiness (tracks tasks/plan.md)

## Phase 0: Workspace ready

- [x] T0.1 Godot 4.3 mono binary at `~/.local/bin/godot-mono`
- [x] T0.2 Fix `.vscode/settings.json` godotTools editorPath
- [ ] T0.3 Diagnose headless runtime hang (engine stalls after .NET init;
      evidence: /tmp/gatev.log). Pre-existing container issue. CI unaffected.
      `--build-solutions` works (exit 0).

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

## Phase 2: CI repair (blocked: billing lock #23)

- [x] T2.1 Mono Godot in both download jobs + setup-dotnet in godot-verify
      (ci.yml valid, 7 jobs)
- [ ] Unblocked externally only by billing; then watch first green run

## Phase 3: Test hardening

- [x] T3.1-slice: ProgressionFormulas pure extraction; LevelProgression +
      CollateralKarma delegate.
      Exposed + fixed a stale 812.8 expectation (true 5^1.3*100 = 810.3).
- [x] T3.2: bypass RETIRED (LevelProgressionTests→ProgressionFormulasTests,
      CollateralKarmaTests→KarmaAmplifierTests); Node behavior moved to gate
      (TestCollateralKarmaBehavior, needs CI run).
- [ ] T3.1-rest: GameManager state-machine extraction (deferred, tree-coupled)
- [ ] T3.3: bench hot-path alignment (#20)
- [ ] Lint gate red on 17 pre-existing infos (exit 1, predates session; +4 by-design
      wrapper infos). Needs policy call: info-cleanup pass or warn-level config.

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

## Verification evidence (2026-09-22)

- `dotnet build` Release: 0 warnings, 0 errors (consecutive incremental builds)
- `dotnet test --filter Category!=GodotRuntime`: 66/66 pass (was 51)
- Roslynator: 17 infos, none in new files
  (ProgressionFormulas/MainBootstrap/GateRunner clean)
- `godot-mono --headless --build-solutions --quit`: exit 0
- ci.yml: valid YAML, 7 jobs intact
