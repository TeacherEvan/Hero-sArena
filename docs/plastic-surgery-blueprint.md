# Plastic Surgery Blueprint: Hero's Arena UI / HUD Visual Overhaul

## 1. Alignment & Workspace Theme Lock

- **Collaboration Matrix:** Desktop + Commercial + All Audiences
  - *Desktop:* `project.godot` fixes a 1920×1080 viewport; `Windowed`/`Fullscreen` toggles; WASD + Space + ESC + left-click input. No touch/mobile code paths exist. Pointer/fine pointer + keyboard.
  - *Commercial:* `install.sh` GUI installer, desktop icon + applications-menu entry, export presets for Linux/Windows/macOS → `build/`. Distributable product requiring accessibility, broad compatibility, conversion polish.
  - *All Audiences:* Top-down arena shooter, combat framed as hero-vs-enemy waves; no mature-content indicators. Approachable, high-clarity, universally safe design.
- **Workspace Theme & Stack:** "Dark godlike" celestial void theme (deep void background `#0b0d14`, panel `#131620`, godlike gold `#e3c76a`, celestial cyan `#38bdf8`). Godot 4.3 C# / .NET 8, CanvasItem 2D (Control / CanvasLayer nodes, StyleBox theming, Tween fades, a `screen_distortion.gdshader` full-screen pass).
- **Investigated Modern Techniques Selected:**
  1. **`oklch()` tonal lightness elevation + specular top-edge inset borders** (`inset 0 1px 0 oklch(1 0 0 / 0.14)`) — elevates panels/buttons above the void canvas, matching the "godlike" celestial metaphor and giving each z-layer perceptual depth (+3% lightness per layer).
  2. **Six-state tactile button spec** (default / hover / active / focus-visible / loading / disabled) with `140ms cubic-bezier` hover lift (`translate3d(0,-1px,0)`), `<=25ms` active press (`scale(0.985)`), `2px` focus ring — gives commercial-grade tactile feedback on desktop pointer input.
  3. **`font-variant-numeric: tabular-nums`** on live numeric readouts (score, wave, enemy count, threat, level, cooldown tenths) — eliminates per-frame numeric jitter as values change width, keeping the HUD render pass within the 3.33 ms budget at 120 Hz.

## 2. Perceptual Token & Typography System

| Token | Value | Role & Contrast Spec |
| :--- | :--- | :--- |
| `--surface-canvas` | `oklch(0.15 0.018 265)` ≈ `#0b0d14` | Base background void / canvas |
| `--surface-card` | `oklch(0.18 0.020 265)` ≈ `#131620` | Elevated container + `1px` specular top border |
| `--surface-raised` | `oklch(0.22 0.022 265)` ≈ `#1f2433` | Raised panel (one z-layer above card) |
| `--accent-primary` | `oklch(0.68 0.190 260)` ≈ `#38bdf8` cyan | Primary action fill & focus ring |
| `--accent-gold` | `oklch(0.78 0.180 80)` ≈ `#e3c76a` | Secondary accent / stat values |
| `--text-on-accent` | `oklch(0.12 0.020 265)` ≈ `#0b0d14` | Foreground on primary accent (CR ≈ 7:1 AAA) |
| `--text-primary` | `oklch(0.91 0.015 265)` ≈ `#e8ecf2` | Body text (WCAG 4.5:1+) |
| `--text-secondary` | `oklch(0.79 0.015 265)` ≈ `#c9d0de` | Secondary content (APCA LC 65+ AA) |
| `--text-muted` | `oklch(0.66 0.020 265)` ≈ `#8b93a5` | Muted labels / borders (APCA LC 45) |
| `--border` | `oklch(0.20 0.015 265)` ≈ `#2a3140` | Panel borders |
| `--text-telemetry` | `oklch(0.98 0.002 265)` ≈ `#f7f8fa` | High-priority readouts (tabular-nums) |
| `--status-nominal` | `oklch(0.70 0.15 160)` ≈ cyan glow | Glowing semantic status pip |
| `--status-low` | `oklch(0.65 0.20 25)` ≈ red glow | Low-health / danger glow |

Typography: `font-variant-numeric: tabular-nums` applied to all live numeric readouts (score, wave, enemy count, threat, level, cooldown tenths, lifetime stats). Godot Label supports `font_variant`/`features` via theme overrides; where unsupported, fixed-width numeric glyph layout is used.

## 3. Six-State Tactile Button & Control Specification

| State | Visual treatment |
| :--- | :--- |
| **Default** | `Panel(Panel, Border)` base, `1px` top specular inset highlight (`oklch(1 0 0 / 0.14)`), gold text, zero resting transform (no static GPU layer) |
| **Hover** (`@media` equivalent: pointer fine + hover) | `140ms cubic-bezier(0.22,1,0.36,1)` lift `translate3d(0,-1px,0)`, border brightens, `::after` glow overlay fades in (opacity) |
| **Active / Pressed** | `<=25ms` transition, `translate3d(0,1px,0) scale(0.985)`, inset shadow inversion (`inset 0 2px 4px oklch(0 0 0 / 0.65)`) |
| **Focus-Visible** | `2px solid var(--accent-primary)` ring, `2px` offset |
| **Loading** | Width-locked bounding box, label `visibility: hidden`, inline spinner, `cursor: wait` |
| **Disabled** | `opacity: 0.45`, `cursor: not-allowed`, desaturated fill, `font_color_disabled` |

All six states are expressed purely through Godot `StyleBoxFlat` themebox overrides (`normal`/`hover`/`pressed`/`disabled`/`focus`) + `modulate` alpha for the loading overlay — **no layout-thrashing properties** (`width`/`height`/`top`/`left`/`margin`/`padding` are never animated), keeping the compositor clean.

## 4. Rendering & Smoothness Execution Plan

- **DOM / Layout (Godot CanvasItem):** Compositor-only transforms (`modulate` alpha, `position` offsets via anchors that don't reflow), `minmax`-style container locks via fixed `custom_minimum_size`, `MouseFilter.Ignore` on background layers so they never steal input.
- **Canvas / GUI / Game Loop:** DPR clamp `[1, 2]` handled by Godot viewport stretch; per-frame allocation rule respected (zero `new Color`/`new Vector2`/object-literal allocations inside `_Process`; scratch `Color`/`Vector2` reused, as already done in `HUD` and `DamageNumberCanvas`).
- **Dirty-rect & batching:** UI panels drawn once at open; no per-frame stylebox recreation. Tween-driven fades reuse a single `Tween`.
- **Reduced-motion guard:** `DisplayServer` / project setting for reduced motion collapses all spatial translations to near-instant (`0.01ms`) while preserving gentle indicator/spinner animations.
- **VRAM & stacking safe:** Max 2 overlapping blur/glow layers; no static `translate3d(0,0,0)` on resting buttons.
- **Preserved Bindings Check (functional integrity — immutable):** All `[Export]` node references, `Pressed`/`Toggled`/`ValueChanged` signal connections, `OnBack`/`Action` callbacks, `EventBus` subscriptions, `data`-equivalent node paths, `NodePath` exports, `GetNodeOrNull` lookups, `SetPressedNoSignal`, `AddThemeStyleboxOverride`/`AddThemeColorOverride` keys, `modulate:a` tween targets, `_ExitTree` unsubscriptions, `Visible`/`SetProcessInput` modal gating — all remain byte-for-byte identical. Only color values, stylebox geometry (border widths/radii), added focus/hover/pressed/loading styleboxes, and new `tabular-numeric` overrides are introduced.

## 5. Per-File Change Scope (presentation only, zero logic change)

1. `scripts/core/VisualTheme.cs` — Add `oklch`-derived palette tokens, `PanelStyle` specular top-edge inset, six-state `StyleButtonTactile`, `StyleButtonFocus`, `StyleButtonLoading`, `StyleSliderTactile`, `StyleProgressBarTactile`, `tabular-numeric` numeric-label helper, `ReducedMotion` guard helper. No existing method signatures removed.
2. `scripts/ui/HUD.cs` — Apply `tabular-numeric` to ScoreLabel/WaveLabel/EnemyCountLabel/ThreatLabel/LevelLabel/cooldown labels; wire reduced-motion guard for vignette/pulse animations.
3. `scripts/ui/MainMenu.cs` — Apply six-state tactile buttons to hero/difficulty/Start/Options/Quit buttons; add focus rings.
4. `scripts/ui/Settings.cs` — Apply tactile buttons + sliders + focus rings; reduced-motion on fade.
5. `scripts/ui/PauseMenu.cs`, `scripts/ui/GameOver.cs`, `scripts/ui/Dashboard.cs`, `scripts/ui/UpgradeMenu.cs`, `scripts/ui/Splash.cs` — Apply tactile buttons, stat-card elevation, focus rings, reduced-motion guard.
6. `scripts/vfx/DamageNumberCanvas.cs` — `tabular-numeric` on damage numerals; reduced-motion on float/scale pop.

No `.tscn` node structure, `[Export]` references, signal connections, or scene ordering changes. All changes confined to `scripts/core/VisualTheme.cs` applicators and the `scripts/ui/*` + `scripts/vfx/DamageNumberCanvas.cs` call sites.
