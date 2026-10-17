# Recommendations — Round 2 (post-implementation review)

> **Status update (same day):** all five recommendations are landed and
> verified locally, everything pending under the co-agent's commit flow.
> Evidence: `dotnet tool run roslynator analyze` — **0 diagnostics** (was 21:
> 17×CA1822 staticized, with CallDeferred targets documented via
> `SuppressMessage`; 3×CS8602 resolved with null-safe GameManager locals in
> `WaveManager`; 1×CA1016 suppressed in `.editorconfig` with rationale —
> Godot.NET.Sdk supplies assembly info); xUnit **352/352**; Godot gate
> **13/13, exit 0**; Release build 0 errors / 0 warnings.
> - **N1** — `tools/smoke_scenes.sh` (executable) boots Main, MainMenu,
>   Tutorial, UpgradeMenu, Sandbox: **SMOKE OK ×5**, 0 C# exception frames;
>   wired into `.github/workflows/ci.yml` `godot-verify` after the core-tests
>   step.
> - **N2** — Easy / Normal / Hard radio buttons (ButtonGroup) + persistence
>   (`user://settings.cfg`, section `[game]`, key `difficulty`; verified on
>   disk) in `MainMenu.tscn`/`MainMenu.cs`; pure logic in
>   `scripts/core/MenuDifficultyState.cs` (+11 xUnit cases). GameManager
>   state already feeds WaveManager + EnemyMutatorSystem, so the choice
>   applies to the next run.
> - **N3** — the stale-hero-ref class is fixed upstream by the co-agent
>   (`EnemyBase` `IsInstanceValid` guard + "Enemy target-disposal tests" in
>   the gate). The scripted-input interactive pass remains future work.
> - **N4** — `install.sh` renamed to the `HeroArena.x86_64` / `HeroArena.pck`
>   artifact names that `export_presets.cfg` and the CI export job produce.
> - **N5** — lint job now passes at zero diagnostics (policy: fix, not
>   baseline); CI revival itself still blocked on the GitHub account (issue
>   #23, not actionable from here).

Date: 2026-10-17 (updated after runtime verification session)
Basis: R3/R2/R1 pure-core implementation plus full runtime verification of
both local gates and the five main scenes.

## Verified today (evidence)

| Check | Result |
|---|---|
| xUnit (`dotnet test`) | **333/333 pass** |
| Godot gate (`res://tests/GodotTests/CoreSystemTests.tscn`) | **11/11 pass, ~4 s, exit code 0** |
| Build (`dotnet build`, Debug) | 0 warnings, 0 errors |
| Scene smoke — Main / MainMenu / Sandbox / Tutorial / UpgradeMenu | **0 errors, 0 C# exception frames** each (12–18 s headless boots) |

Fixed en route to reach this state:

- **Dead scene export wiring (5 scenes)** — hand-written `.tscn` files were
  missing the `node_paths=PackedStringArray(...)` node-header attribute, so
  every `[Export] Node` property loaded as **null** and `_Ready` threw NREs
  (`HUD`, `MainMenu`, `Sandbox`, `UpgradeMenu` all NRE'd on boot; the game was
  unplayable since the initial commit). Root-caused with a minimal probe and
  fixed in `HUD.tscn`, `MainMenu.tscn`, `Sandbox.tscn`, `Tutorial.tscn`,
  `UpgradeMenu.tscn`.
- **Circular scene reference** `MainMenu.tscn ↔ Sandbox.tscn` (ext_resource
  cycle → both refs failed to parse). Broken by switching Sandbox's back
  button to a runtime `ChangeSceneToFile`.
- **Wrong parent paths in Sandbox.tscn** (13 nodes) — parents must be the
  full path from the scene root (`UI/Panel/VBoxContainer`, not `VBoxContainer`).
- **Gate test bugs** (`CoreSystemTests.cs`): `GetTree().Root.AddChild()` during
  main-scene `_Ready` ("Parent node is busy setting up children") and
  `System.IO.File.Exists("res://…")` (virtual paths need
  `ProjectSettings.GlobalizePath`).
- **Pool pre-warm stall** — fixed by the parallel workstream (`2cd98dd`:
  deactivate before `AddChild`; sizes 2000/4000/300). The former "mono hangs in
  unmanaged code" verdict was a misdiagnosis of this stall.

## The five recommendations

| # | Recommendation | Priority | Effort | Unblocks |
|---|---|---|---|---|
| N1 | Land the runtime-verification fixes; add both gates + scene smoke to CI | Critical | ~1 d | Everything below |
| N2 | Ship the Difficulty selector UI (Easy/Normal/Hard) | High | ~1 d | Completes R2 |
| N3 | Interactive verification pass; fix the stale-hero-ref class | High | ~2–3 d | Completes R1/R3 |
| N4 | Fix release artifact naming (install.sh vs export presets vs CI) | Medium | ~2 h | Release integrity |
| N5 | Revive CI + resolve the 20 pre-existing lint diagnostics | Medium | ~1 d | Safe merges |

### N1 — Land the fixes; wire both gates into CI (Critical)

The gate is now fast and reliable enough for CI:

```bash
env DOTNET_ROOT=~/.dotnet8 dotnet build        # non-exported runs load the Debug assembly
env DOTNET_ROOT=~/.dotnet8 ~/.local/bin/godot-mono --headless --path . \
    res://tests/GodotTests/CoreSystemTests.tscn   # prints PASS/FAIL lines, exit 0/1, ~4 s
```

- Commit the session's fixes: `tests/GodotTests/CoreSystemTests.cs` (two test
  bugs), five `.tscn` wiring fixes, `Sandbox.cs` cycle fix.
- `godot-verify` in CI should assert `Results: 11 passed, 0 failed` and rely on
  the exit code (verified clean exit; the earlier shutdown stall no longer
  reproduces).
- Add a scene smoke script (see Appendix B): boot each main scene headless for
  ~15 s and fail on `at HeroArena` exception frames. This bug class —
  scenes that parse but are dead at runtime — cost the project its entire UI
  surface and is invisible to unit tests.

### N2 — Difficulty selector UI (High)

Backend is implemented (`GameManagerState.SetDifficulty` / `GetSettings`,
per-difficulty multipliers). Remaining:

- Add Easy/Normal/Hard buttons to `MainMenu.tscn` (wire with
  `node_paths=PackedStringArray(...)` — see Appendix A).
- Persist selection (`ConfigFile` in user://), reflect current choice in the
  menu, and surface the active difficulty in the HUD or pause screen.
- Tutorial completion should unlock Normal as designed (state exists; verify
  end-to-end with the interactive pass below).

### N3 — Interactive verification pass + stale-ref fix (High)

Boot is verified; the interactive layer is not: button → scene switch flows,
tutorial steps, sandbox toolbar, upgrade-perk selection, win/lose. Options:

- Scripted-input harness: a test scene that injects `InputEvent`s and asserts
  scene/state transitions headless (fits the existing gate pattern).
- Or a manual play pass on a machine with a display.

Known defect to fix as part of this: **enemies hold disposed hero references.**
`HeroBase.Die()` calls `QueueFree()`, but enemies' ~20 `_hero` uses
(`scripts/enemies/*.cs`) guard only `null` — after hero death every physics
tick throws:

```text
System.ObjectDisposedException: Cannot access a disposed object.
   at HeroArena.EnemyBase.Move ... / DistanceSquaredToHero ...
```

Evidence: 11,061 ObjectDisposedException in one 30 s no-input run. Fix by
centralising a validity check (`GodotObject.IsInstanceValid(_hero)`), or
clearing enemy refs via an event on hero death.

Minor: one `Can't emit non-existing signal "draw"` in Main (vfx path);
PagedAllocator / "Unreferenced static string" lines on exit are engine noise.

### N4 — Release artifact naming (Medium)

Three sources disagree on the binary name:

- `export_presets.cfg` exports `HeroArena.x86_64`
- `install.sh` and `.github/workflows/ci.yml` expect `Hero-sArena.x86_64`
- `build/linux/` currently holds stale `Hero-sArena.*` artifacts

Standardise on one name (`HeroArena`, matching the project name) in all three
places, then re-export to refresh `build/`.

### N5 — Revive CI + lint policy (Medium)

With the gate fixed the full CI order (lint → typecheck → build → test →
godot-verify → export) runs locally. Remaining blockers: GitHub account/
billing (issue #23) and the 20 pre-existing diagnostics — 1×CA1016 (assembly
metadata), 16×CA1822 (methods that can be static), 3×CS8602 in
`WaveManager.cs`. Decide: fix, baseline, or drop `roslynator analyze` from the
gate. Keep the TODO/FIXME quality gate — it passes.

## Appendix A — Correct `.tscn` wiring for C# `[Export] Node` properties

Hand-written scenes must reproduce what the editor writes, or C# receives a
raw `NodePath` variant that converts to **null**:

```
[node name="Sandbox" type="Node2D" node_paths=PackedStringArray("BtnAtlas", "HeroSpawnPoint")]
script = ExtResource("1")
BtnAtlas = NodePath("UI/Panel/VBoxContainer/BtnAtlas")
HeroSpawnPoint = NodePath("HeroSpawnPoint")
```

Rules:

1. `node_paths=PackedStringArray(...)` on the node header is **required** for
   every node-typed property assigned as `X = NodePath(...)`. Without it the
   engine sets the NodePath value directly and the C# binding stores null.
2. `parent="…"` is always the **full path from the scene root**
   (`parent="UI/Panel/VBoxContainer"`), never just the immediate parent.
3. Avoid `ext_resource` cycles between scenes (e.g. MainMenu ↔ Sandbox);
   load one side at runtime with `ChangeSceneToFile` / `GD.Load`.
4. `type=` must be a real class (`Node`, `CanvasLayer`, …) or a registered
   global class — `type="Tutorial"` yields `Cannot get class 'Tutorial'`.

## Appendix B — Scene smoke script sketch

```bash
#!/usr/bin/env bash
# tools/smoke_scenes.sh — fail if any main scene throws C# exceptions at boot
set -u
SCENES=(scenes/Main.tscn scenes/ui/MainMenu.tscn scenes/maps/Sandbox.tscn
        scenes/ui/Tutorial.tscn scenes/ui/UpgradeMenu.tscn)
fail=0
for s in "${SCENES[@]}"; do
  timeout 20 env DOTNET_ROOT=~/.dotnet8 ~/.local/bin/godot-mono --headless \
      --path . "res://$s" > /tmp/smoke.log 2>&1
  if grep -q "at HeroArena" /tmp/smoke.log; then
    echo "SMOKE FAIL: $s"; grep -A3 "at HeroArena" /tmp/smoke.log | head; fail=1
  else
    echo "SMOKE OK: $s"
  fi
done
exit $fail
```
