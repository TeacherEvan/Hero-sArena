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
- [ ] T0.2: Fix `.vscode/settings.json` editorPath → mono binary path
- [ ] T0.3: Diagnose headless SIGSEGV (repro: `godot-mono --headless --verbose --quit`)

### Checkpoint: Workspace
- [ ] `dotnet build` green via `~/.dotnet8` (verified: 0 warn, 0 err)
- [ ] `dotnet test --filter Category!=GodotRuntime` green (verified: 51/51)
- [ ] Godot-tools connects to 4.3 mono in Cursor

### Phase 1: P0 — make the game run (firewall fixes)
- [ ] T1.1: Attach scripts to 10 enemy scene roots (`script = ExtResource("1")`)
  Files: `scenes/enemies/*.tscn`. Scope: XS.
  Verify: `grep -L "script = ExtResource" scenes/enemies/*.tscn` → empty.
- [ ] T1.2: Attach scripts to 4 hero scene roots. Files: `scenes/heroes/*.tscn`. Scope: XS.
- [ ] T1.3: Wire combat runtime — WaveManager node + `EnemyScenes` + `SpawnPoints`
  + hero spawn + map load. OPEN DESIGN Q: Main.tscn nodes vs GameManager code.
  Verify: headless spawn smoke test instantiates non-null `EnemyBase`. Scope: M.
- [ ] T1.4: Fix headless gate runner for Godot 4 (`-s` requires SceneTree/MainLoop;
  CoreSystemTests extends Node). Options: extend SceneTree or add wrapper scene.
  Verify: `godot-mono --headless -s <gate>` prints `Results: 7 passed, 0 failed`, exit 0. Scope: S.

### Checkpoint: Playable
- [ ] Gate green under mono binary; first enemy spawns without NullReferenceException

### Phase 2: CI repair (blocked externally by billing lock, issue #23)
- [ ] T2.1: Switch both Godot download steps to `Godot_v4.3-stable_mono_linux_x86_64.zip`,
  set `DOTNET_ROOT`/PATH to .NET 8 before build/test steps. Scope: S.
- [ ] T2.2: Use corrected gate invocation from T1.4 in `godot-verify`. Scope: XS.

### Phase 3: Test hardening (issues #20, #22)
- [ ] T3.1: Extract pure logic out of GameManager/LevelProgression Node subclasses
  so `dotnet test` covers them headlessly; drop `GodotRuntime` trait need. Scope: L → split per class.
- [ ] T3.2: Retire `RuntimeHelpers.GetUninitializedObject` bypass in CollateralKarmaTests. Scope: S.
- [ ] T3.3: Align bench harnesses with production hot path (follow-up to dfb7acd). Scope: M.

### Phase 4: Docs sync
- [ ] T4.1: README — 51 tests (not 19), audit PRs #18/#19 merged, mono-binary
  requirement, corrected gate invocation. Scope: XS.
- [ ] T4.2: AGENTS.md — same + headless SIGSEGV note. Scope: XS.

### Phase 5: Branch hygiene
- [ ] T5.1: `git merge-base --is-ancestor` check on unmerged branches
  (`code-health/burrower-ai-refactor-*`, `fix/evictoldest-decal-rename-regression`);
  prune superseded, merge live. Scope: S.

## Risks and Mitigations
| Risk | Impact | Mitigation |
|------|--------|------------|
| GitHub billing lock (CI dark) | High | Local gates mirror CI: build + test + mono gate |
| Headless SIGSEGV in container | Med | T0.3 diagnosis; editor-run via Cursor/Godot-tools as fallback |
| T1.3 design choice (scene vs code wiring) | Med | Ask user before implementing (see Open Questions) |
| Export templates (~1 GB) not installed | Low | Download only when export is actually needed |

## Open Questions
- Where should WaveManager/hero/map wiring live: Main.tscn nodes or GameManager code?
- Which map is the default (RuinedMetropolis?)?
- Export priority when CI returns: Linux first?
