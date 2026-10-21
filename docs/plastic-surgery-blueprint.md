# Plastic Surgery Blueprint: Hero's Arena UI / HUD Visual Overhaul (Fix Pass)

> **Status:** applied and verified. This document supersedes the first-pass
> blueprint (commit `03e7334`); the sections marked **FIX PASS** record the
> defects that pass left behind, how they were found, and what changed.

## 1. Alignment & Workspace Theme Lock

- **Collaboration Matrix:** Desktop + Commercial + All Audiences
  - *Desktop:* `project.godot` fixes a 1920×1080 viewport; `Windowed`/`Fullscreen` toggles; WASD + Space + ESC + left-click input. No touch/mobile code paths exist. Pointer/fine pointer + keyboard.
  - *Commercial:* `install.sh` GUI installer, desktop icon + applications-menu entry, export presets for Linux/Windows/macOS → `build/`. Distributable product requiring accessibility, broad compatibility, conversion polish.
  - *All Audiences:* Top-down arena shooter, combat framed as hero-vs-enemy waves; no mature-content indicators. Approachable, high-clarity, universally safe design.
- **Workspace Theme & Stack:** "Dark godlike" celestial void theme (deep void background `#0b0d14`, panel `#131620`, godlike gold `#e3c76a`, celestial cyan `#38bdf8`). Godot 4.3 C# / .NET 8, CanvasItem 2D (Control / CanvasLayer nodes, StyleBox theming, Tween fades, a `screen_distortion.gdshader` full-screen pass).
- **Investigated Modern Techniques Selected:**
  1. **Perceptual tonal elevation + rim/specular surfaces** — every raised surface is built from one recipe: 8px radius, 1px light rim, 2px specular top edge, short drop shadow. Only colours change per role, never geometry, so nothing "jumps" between states.
  2. **Six-state tactile button spec** (default / hover / active / focus-visible / loading / disabled) expressed through StyleBox state overrides — no layout-thrashing property is animated.
  3. **Real surfaces behind menu columns** — a card plate that frames each overlay, plus per-row chips for readouts that live inside a container.

---

## 2. FIX PASS — Pre-Op Defect Catalog (evidence-based)

The first pass was verified by rendering every screen offscreen and reading the
actual pixels (`tools/ui_capture.gd`, see §6). The following defects were
present and are now fixed.

| # | Defect | Where | Root cause |
| :--- | :--- | :--- | :--- |
| D1 | **Focused primary CTA lost its gold fill** — the focused "Start Game" button rendered as a dark slab | `VisualTheme.StyleButtonFocus` | Godot draws the `focus` stylebox *over* the state stylebox. The override used a filled `StyleBoxFlat` (`DrawCenter = true`), so it painted over the button fill. Now a border-only ring (`DrawCenter = false`, 2px, 2px expand margin). |
| D2 | **No panel was ever drawn** — every "elevated surface" in the game was invisible | `StylePanel` on VBoxContainer / plain Control | Godot only draws the `panel` stylebox on `Panel`/`PanelContainer`; containers and plain `Control`s ignore it. Surfaces are now attached as a sibling `Panel` behind the content (`VisualTheme.AttachSurface`) or expressed as row chips (`VisualTheme.StyleChip`). |
| D3 | **Progress bars were never restyled** — the HUD still used the stock Godot bar with a "66%" label painted over the fill | `StyleProgressBarTactile` | Godot 4 themes the track as `background`; the pass overrode `bg`, which is silently ignored. Now `background` + `fill`, flat 3px radius, percentage text off, and a dedicated `Health: 100/150` read-out row. |
| D4 | **Stock Godot widgets leaked into the theme** — light-grey sliders and white toggle switches on the Settings screen | `scripts/ui/Settings.cs` | `HSlider` / `CheckButton` were never themed. Now: dark track + celestial fill, and a procedurally drawn celestial switch icon. |
| D5 | **Settings value read-outs sat next to the sliders** instead of at the row's right edge | `scripts/ui/Settings.cs` | The sliders did not expand (`expand = true` is not a `HSlider` property). Now `SizeFlagsHorizontal = ExpandFill`. |
| D6 | **HUD stat stack was ragged** (labels forced right-aligned by a "tabular" helper) | `VisualTheme.StyleLabelTabular` | The helper right-aligned every label and forced a font size. Verified with a runtime probe: **Godot's bundled font already ships tabular figures** (digits `0` and `1` measure identically), so the right-align hack only broke the layout. Labels are left-aligned and keep their own size. |
| D7 | **Achievement / power-up toasts clipped off the right screen edge** | `scripts/ui/HUD.cs` | The toast strip was 210px wide and the text overflowed the viewport. The strip is now 340px, the labels are right-aligned so long toasts grow leftwards, and the font is 14px. |
| D8 | **Loading state let clicks through** | `VisualTheme.StyleButtonLoading` | `MouseFilter = Pass` meant a click on a "loading" button fell through to whatever was behind it. The button is now disabled + dimmed. |
| D9 | **Reduced-motion guard was dead code** | `VisualTheme.ReducedMotionDuration` | It read a project setting that did not exist, and no fade used it. Added `accessibility/reduced_motion` to `project.godot` and wired it into every UI fade (Settings / Pause / Game Over / Splash). |
| D10 | **Stat cards were columns of bare numbers** ("0", "0", "0", "Normal") with no labels | `GameOver.Show`, `Dashboard.Refresh` | The refresh code overwrote the "Name: value" captions with bare values. Captions restored (display strings only — no data contract, save file or binding touched). |
| D11 | **Perk cards showed only a name** — the player could not tell what a perk did | `scripts/ui/UpgradeMenu.cs` | `PerkSystem.PerkDescription` was never used. Cards now show `name + description` with autowrap. |
| D12 | **Menus floated on the void** — no card, no hierarchy, 16px title | `MainMenu`, `Settings`, `Pause`, `Game Over`, `Dashboard` | The scenes never had an elevated surface and the titles used body text size. Now: card surface, gold display title, section hierarchy, 34px comfortable button rows. |
| D13 | **Cards were half empty** — columns sized to a fixed 600px box | scene offsets | `VisualTheme.FitColumnHeight` lets a column hug its content while staying centred on its anchor. |

## 3. Perceptual Token & Typography System

| Token | Value | Role & Contrast Spec |
| :--- | :--- | :--- |
| `--surface-canvas` | `oklch(0.15 0.018 265)` ≈ `#0b0d14` | Base background void / canvas |
| `--surface-card` | `oklch(0.18 0.020 265)` ≈ `#131620` | Elevated container (menu / HUD card) |
| `--surface-raised` | `oklch(0.22 0.022 265)` ≈ `#1f2433` | Raised control (hover state) |
| `--rim-soft` | `rgba(255,255,255,0.10)` | 1px light rim that lifts a surface off the void |
| `--rim-bright` | `rgba(255,255,255,0.22)` | Hover rim |
| `--accent-primary` | `oklch(0.68 0.190 260)` ≈ `#38bdf8` | Cyan: focus ring, slider fill, switch-on, cooldown text |
| `--accent-gold` | `oklch(0.78 0.180 80)` ≈ `#e3c76a` | Titles, primary CTA fill, health bar, level/wave read-outs |
| `--text-on-accent` | `oklch(0.12 0.020 265)` ≈ `#0b0d14` | Foreground on the gold CTA (CR ≈ 7:1 AAA) |
| `--text-primary` | `oklch(0.91 0.015 265)` ≈ `#e8ecf2` | Body text (WCAG 4.5:1+) |
| `--text-secondary` | `oklch(0.79 0.015 265)` ≈ `#c9d0de` | Secondary content |
| `--text-muted` | `oklch(0.66 0.020 265)` ≈ `#8b93a5` | Muted labels / borders (APCA LC 45) |
| `--status-low` | `oklch(0.65 0.20 25)` ≈ red | Game-over title / danger button |

Typography: display title 34–64px gold with a soft shadow, section headers
14px gold, body 16px, stat rows 20px (14px for lifetime rows), toasts 14px.
Digits are tabular by font construction (verified), so live read-outs never
change width as their values change.

## 4. Six-State Tactile Button & Control Specification

| State | Visual treatment |
| :--- | :--- |
| **Default** | `SurfaceCard` fill, 8px radius, 1px `--rim-soft` rim, 2px specular top edge, 3px drop shadow |
| **Hover** | `SurfaceRaised` fill, `--rim-bright` rim (fill + rim only — geometry never changes) |
| **Active / Pressed** | Specular top edge collapses 2px → 1px and the drop shadow is dropped: the control reads as pushed *into* the surface (a depression, not a colour swap) |
| **Focus-Visible** | 2px `--accent-primary` ring with a 2px offset, `DrawCenter = false` so it never hides the button fill |
| **Loading** | Width-locked bounding box, dimmed to 55%, input disabled (never click-through) |
| **Disabled** | Desaturated fill, muted rim, no shadow, muted label |

Segmented variant (difficulty pickers): compact 5px radius, the selected
segment lights gold and its geometry differs, so the selection is not carried
by colour alone.

## 5. Rendering & Smoothness Execution Plan

- **DOM / Layout (Godot CanvasItem):** compositor-only changes; `width`/`height`/`margin`/`padding` are never animated. Containers keep their layout; the surface panel is a sibling that follows the target's live rect through the `resized` signal.
- **Canvas / GUI / Game Loop:** DPR clamp handled by Godot's viewport stretch; the per-frame allocation rule is respected — the new health read-out rebuilds its string only when the value changes, and the switch icons are generated once and cached.
- **Dirty-rect & batching:** no per-frame stylebox creation; surfaces are created once at `_Ready`.
- **Reduced-motion guard:** `accessibility/reduced_motion` collapses every UI fade to 0.01s.
- **VRAM & stacking safe:** no static transform layers, no blur layers, at most one extra `Panel` per screen.
- **Preserved Bindings Check (functional integrity — immutable):** all `[Export]` node references, `Pressed`/`Toggled`/`ValueChanged` signal connections, `OnBack`/`Action` callbacks, `EventBus` subscriptions, `NodePath` exports, `GetNodeOrNull` lookups, `SetPressedNoSignal`, `AddThemeStyleboxOverride`/`AddThemeColorOverride` keys, `modulate:a` tween targets, `_ExitTree` unsubscriptions, `Visible`/`SetProcessInput` modal gating — all unchanged. Only colours, stylebox geometry, focus/hover/pressed/loading styleboxes, new optional theme overrides and display strings changed. No `.tscn` file was modified.

## 6. Visual Verification Harness

`tools/ui_capture.gd` renders a scene offscreen and writes a PNG so UI work is
verified with real pixels instead of assumptions:

```bash
xvfb-run -a ~/.local/bin/godot-mono --path . --resolution 1920x1080 \
  --rendering-driver opengl3 --script res://tools/ui_capture.gd -- \
  --scene res://scenes/ui/MainMenu.tscn --out /tmp/mainmenu.png --wait 40
```

Options: `--act <frame>` (when to press/hover/focus), `--press <path>` (emit a
real button press), `--child <scene>` (instantiate a second scene, e.g. the
Settings overlay), `--visible` (force a hidden overlay on screen).

## 7. Verification (all green after the fix pass)

| Gate | Result |
| :--- | :--- |
| `dotnet build --configuration Release` | 0 warnings, 0 errors |
| `dotnet tool run roslynator analyze` | 0 diagnostics |
| `dotnet test tests/HeroArena.Tests` | 468 passed / 0 failed |
| Godot headless gate `tests/GodotTests/CoreSystemTests.tscn` | 14 passed, 0 failed |
| TODO/FIXME quality gate over `scripts/` | clean |
| Offscreen renders of MainMenu / Settings / Pause / GameOver / Dashboard / UpgradeMenu / HUD | captured and reviewed |
