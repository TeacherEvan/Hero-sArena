# Production-Readiness Plan: Hero's Arena

## Overview

Get the codebase from "builds + unit tests green" to "game actually runs and ships".
Investigation (2026-09-22) found the C# build is clean (0 warnings, 0 errors) and
51/51 headless-safe xUnit tests pass, but the game cannot spawn a single enemy:
all 14 entity scenes lack root script attachments, nothing instantiates
WaveManager/hero/map, the documented headless gate invocation is invalid on
Godot 4, and CI downloads a Godot binary that cannot run C# at all.

## Environment facts (verified this session)

- Build SDK: `~/.dotnet8/dotnet` = 8.0.425. Always run with
  `DOTNET_ROOT=~/.dotnet8 PATH=~/.dotnet8:$PATH`. The snap `dotnet` shim is
  broken (demands sudo). `~/.dotnet` (10.0.201) must NOT be used (AGENTS.md).
- Godot 4.3 mono (C#-capable): `~/.local/bin/godot-mono`
  (`Godot_v4.3-stable_mono_linux.x86_64`). Standard (non-mono) binary at
  `~/.local/bin/godot-4.3` cannot load `.cs` resources — proven by probe.
- Cursor has `geequlim.godot-tools` installed. `.vscode/settings.json`
  `godotTools.editorPath.godot4` currently points at a `project.godot` file;
  must point at the mono binary (fixed in T0.2).
- Headless `godot-mono --quit` segfaults (SIGSEGV) in this container after .NET
  init, before scene load. Evidence: `/tmp/godotprobe.log`. Undiagnosed (T0.3).

## Task List

### Phase 0: Workspace ready

- [x] T0.1: Install Godot 4.3 mono binary (done: `~/.local/bin/godot-mono`)
- [x] T0.2: Fix `.vscode/settings.json` editorPath → mono binary path
- [ ] T0.3: Diagnose headless runtime hang (engine stalls after .NET init;
  evidence: /tmp/gatev.log). `--build-solutions` exits 0; `-s` script runs
  hang. Pre-existing container issue; CI unaffected.

### Checkpoint: Workspace

- [x] `dotnet build` green via `~/.dotnet8` (0 warn, 0 err, consecutive runs)
- [x] `dotnet test --filter Category!=GodotRuntime` green (66/66)
- [ ] Godot-tools connects to 4.3 mono in Cursor
  (editorPath fixed; needs editor session)

### Phase 1: P0 — make the game run (IMPLEMENTED 2026-09-22)

- [x] T1.1: Attach scripts to 10 enemy scene roots (`script = ExtResource("1")`)
  Verify: `grep -L "script = ExtResource" scenes/enemies/*.tscn` → empty.
- [x] T1.2: Attach scripts to 4 hero scene roots.
- [x] T1.3: Combat runtime in Main.tscn (WaveManager + EnemyScenes + map + HUD
  - Bootstrap). Decision: Main.tscn nodes. Main root is plain Node2D — the
  GameManager autoload is the single Instance (scene-root script removed to
  avoid double event subscription + dangling Instance on scene change).
- [x] T1.4: GateRunner (SceneTree entry) hosts CoreSystemTests. Compiles clean;
  runtime run needs a green env (T0.3).

### Checkpoint: Playable

- [ ] Gate green under mono binary; first enemy spawns without NullReferenceException

### Phase 2: CI repair (blocked externally by billing lock, issue #23)

- [x] T2.1: Both Godot downloads → mono build; setup-dotnet in godot-verify.
- [x] T2.2: Corrected gate invocation (`GateRunner.cs`) in `godot-verify`.

### Phase 3: Test hardening (issues #20, #22)

- [x] T3.1-slice: `ProgressionFormulas` pure extraction (LevelProgression +
  CollateralKarma delegate). Exposed + fixed stale 812.8 expectation.
  REMAINDER: GameManager state-machine extraction (deferred, tree-coupled).
- [x] T3.2: Bypass RETIRED (tests renamed to ProgressionFormulasTests /
  KarmaAmplifierTests; Node behavior in gate TestCollateralKarmaBehavior).
- [ ] T3.3: Align bench harnesses with production hot path
  (follow-up to dfb7acd). Scope: M.

### Phase 4: Docs sync

- [x] T4.1: README synced (66 tests, merged PRs, mono, GateRunner).
- [x] T4.2: AGENTS.md synced (stack, test matrix, CI cmds, gaps).

### Phase 5: Branch hygiene

- [x] T5.1: merge-base checks done; both stale branches pruned with approval
  (burrower superseded, evictoldest content already in main).

## Risks and Mitigations

| Risk | Impact | Mitigation |
| ------ | -------- | ------------ |
| Billing lock (CI dark) | High | Local gates mirror CI |
| Headless hang in container | Med | See T0.3; Godot-tools fallback |
| T1.3 design choice | Done | Main.tscn nodes; plain Node2D root |
| Export templates not installed | Low | Download when export is needed |

## Resolved decisions (were Open Questions)

- WaveManager/hero/map wiring lives in Main.tscn nodes.
- Default map: RuinedMetropolis (markers feed SpawnPoints).
- Export priority when CI returns: Linux first.
