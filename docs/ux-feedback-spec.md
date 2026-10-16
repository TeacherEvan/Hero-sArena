# UX Feedback Spec — Hero's Arena (horde / top-down arena shooter)

**Audience:** C# / Godot 4.3 dev implementing combat-feedback juice.
**Status:** Actionable spec. Every section lists a concrete EventBus hook + C# method signature ready to drop into `scripts/`.
**Toolchain:** `DOTNET_ROOT=~/.dotnet8`; Godot 4.3 mono at `~/.local/bin/godot-mono`. xUnit is the only authoritative local gate (headless Godot gate cannot run C# — see AGENTS.md "Known production gaps"). Prefer pure, headless-testable logic extracted from `Node` wrappers (the `PowerupBannerSlots`/`WaveFormulas` pattern).

---

## Source tier legend

| Tier | Meaning |
|------|---------|
| **T1** | Shipped genre reference (Vampire Survivors, Brotato, Hades, HoloCure) — what the player expects from the genre. |
| **T2** | Concrete implementation tutorial / engine doc — copy-pasteable approach (numbers, timings, code shape). |
| **T3** | Established UX / UI pattern guidance — interaction conventions (modals, pauses, damage indicators). |
| **T4** | Community / player-sentiment signal — e.g. "some players turn off damage numbers because of clutter" → support a toggle. |

## Sources consulted

| # | URL | Tier | Establishes |
|---|-----|------|-------------|
| S1 | https://blog.febucci.com/2026/04/how-to-create-animated-damage-numbers-in-unity/ | T2 | Damage numbers = per-label independent animation; float up + alpha fade over a `lifetime`; `floatSpeed ≈ 1.5` units/s; horizontal spread + upward direction; controller spawns labels at hit positions; pool them (hordes spawn many). |
| S2 | https://help.elderscrollsonline.com/app/answers/detail/a_id/32697 | T3 | Combat-text color convention: white outgoing dmg, yellow crit, orange DoT, red incoming, green healing, tan CC. |
| S3 | https://dev.epicgames.com/community/learning/tutorials/2KLv/unreal-engine-optimizing-combat-text-high-performance-damage-numbers-with-niagara-and-texture-atlas | T2 | Performance: combat text must be pooled/batched in horde games where dozens of numbers appear per frame. |
| S4 | https://medium.com/design-bootcamp/ux-ui-in-video-games-the-pause-menu-6f07e113e21e | T3 | Pause menu = halt game + modal overlay; clear exit path. |
| S5 | https://blog.logrocket.com/ux-design/modal-ux-design-patterns-examples-best-practices/ | T3 | Modal = focus trap, dimmed background, explicit confirm/cancel, escape key closes. |
| S6 | https://uxplanet.org/game-design-ux-best-practices-the-ultimate-guide-4a3078c32099 | T3 | Tap dim-background to close (unless it's a forced-choice/store popup — then require explicit action). |
| S7 | https://medium.com/@jasper.stephenson/a-ux-analysis-of-first-person-shooter-damage-indicators-59ac9d41caf8 | T3 | Red screen flash on hit; vignette intensifies as damage accumulates (low-health signal). |
| S8 | https://vampire-survivors.fandom.com/wiki/PowerUps | T1 | Power-ups = permanent stat improvements (duration +15%/rank, magnet +25%/rank); collected in-level. |
| S9 | https://www.youtube.com/watch?v=m7i7fGnHT6I | T1 | Vampire Survivors / Hades power-creep loop: player goes from fragile to godlike, then challenged again. |
| S10 | https://www.youtube.com/watch?v=2A3wLgQAbyw | T2 | "Game juice": non-essential VFX/Audio that amplify feel; keep shakes short (80–150 ms), avoid huge text stacks. |

---

## 1. Floating combat text / damage numbers

### Mechanic
When any entity takes damage, spawn a numeric label at the hit world position showing the amount. The label floats upward and fades out over a fixed lifetime, then returns to the pool. Critical hits and damage types are color-coded. This is the primary "did my attack land" feedback in a horde where hundreds of projectiles fly per frame.

### When to show
On every damaging hit: projectile impact, AoE ability, exploder detonation, and incoming damage to the hero. **Do not** show for zero-damage / blocked hits (show a tiny "block" or nothing — avoid clutter, S4/T4).

### Visual treatment
- **Per-label independent animation** (S1): each label owns its lifetime; a controller spawns/recycles them at the hit position.
- **Motion:** float upward at `~1.5–2.0 world-units/s` with a small random horizontal spread (`±0.3–0.6` units) so overlapping hits remain readable (S1, S10).
- **Fade:** alpha `= 1.0 − (elapsed / lifetime)`, linear; optional tiny scale "pop" at spawn (`1.0 → 1.2 → 1.0` over first `0.08s`) for impact weight.
- **Lifetime:** `1.0–1.4s` (shorter for small hits, longer for big/crit).
- **Font treatment:** bold SDF/contour outline font with dark outline + drop shadow so it reads against chaotic backgrounds; **monospaced numeric glyphs** so digits don't jitter as values change; all-caps optional. Scale scales with magnitude (crit ≈ `1.3×` base size).
- **Color coding by damage type** (extend `VisualTheme`, S2/S3 convention):
  - `Kinetic` — near-white / `Text` (#E2E8F0)
  - `Energy` — `Cyan` (#38BDF8)
  - `Fire` — orange (#F27316)
  - `Acid` — green (#22C55E)
  - `Lightning` — violet (#C084FC)
  - `Explosive` — red (#EF4444)
  - `Crit` (any type) — `Gold` (#E3C76A), larger, slight screen shake + hit stop (`HitStop.TriggerHitStop(0.05s)`)
- **Performance:** pool labels via `ObjectPoolManager` (same pattern as `ReturnProjectile`/`ReturnDecal`) and cap concurrent labels (e.g. max 12–16); oldest fades first when cap is hit (S3).

### Timing values
| Value | Default |
|-------|---------|
| Lifetime | 1.1s |
| Float speed | 1.75 units/s upward |
| Horizontal spread | ±0.4 units |
| Pop scale | 1.2× over 0.08s |
| Max concurrent pooled labels | 16 |
| Crit extra | +hit stop 0.05s, +screenshake `ScreenShake.Shake(4f, 0.1f)` |

### Audio
Short "hit" sting per number only on crit/big hits — do **not** play a sound per number (horde = audio fatigue). Use `HitFlash`/hit-stop for the impact moment instead.

### Implementation hook
**New `EventBus` member** (`scripts/core/EventBus.cs`):
```csharp
// Projectile / VFX
public event Action<Vector2, float, DamageType, bool>? OnDamageNumber;
public void EmitDamageNumber(Vector2 worldPos, float amount, DamageType type, bool critical)
    => OnDamageNumber?.Invoke(worldPos, amount, type, critical);
```
**Call sites:** `ProjectileBase` impact, `EnemyBase.TakeDamage` (incoming to hero — use `Incoming` color), `Atlas.Slam`, `Synapse.FireChainLaser`, `Volt.FireArcLightning`, `Volt.ActivateStaticField`, `Exploder.Detonate`. (Note: current `OnProjectileHit(Vector2, DamageType)` carries no amount — extend it or add the new event above.)

**New node:** `DamageNumberCanvas : CanvasLayer` (child of `HUD` or an autoload). Subscribes to `EventBus.OnDamageNumber`, pools `DamageNumber : Label` instances, animates `GlobalPosition`/`modulate.a` in `_Process`, returns to pool on expiry.
```csharp
// DamageNumberCanvas.Spawn(Vector2 pos, float amount, DamageType type, bool critical)
```
**C# method to call:** `DamageNumberCanvas.Spawn(worldPos, amount, type, critical)` — or let the canvas self-subscribe; call sites only `EventBus.Instance.EmitDamageNumber(...)`.

**Files to touch:** `scripts/core/EventBus.cs`, `scripts/vfx/` (new `DamageNumber.cs` + `DamageNumberCanvas.cs`), `scripts/core/VisualTheme.cs` (damage-type colors), call sites in `scripts/projectiles/` + `scripts/enemies/EnemyBase.cs` + hero abilities.

**Testability:** `DamageNumber` animation math (alpha over time, position over time) is pure — extract into a Godot-free class + xUnit test (per AGENTS.md extraction pattern). Canvas subscription/pooling needs the headless gate.

---

## 2. Wave incoming / boss warning banners

### Mechanic
Before a wave (and before a boss), show a fullscreen warning banner so the player is never ambushed off-screen. In a horde, enemies can spawn at map edges; the warning buys the player ~2s to reposition.

### Timing values (current WaveManager baseline: `BREATHER_SECONDS = 5f`, `BOSS_INTERVAL_SECONDS = 300f`)
| Event | Lead time before spawn | Banner hold | Notes |
|-------|------------------------|-------------|-------|
| Normal wave | 2.0s | 2.0s (covers full lead) | Fires during breather countdown |
| Boss | 4.0s | 4.0s | More dramatic; fullscreen flash + sound |
| Wave clear / breather start | — | — | No banner needed (player already won the wave) |

### Visual treatment
- **Fullscreen flash:** a fullscreen `ColorRect` tweened alpha `0 → 0.55 → 0` over `0.35s` on banner appearance (gold tint for boss, white for wave). Short, non-epileptic — single flash, not strobing (S10).
- **Banner text:** centered, large bold outline font. Normal: `"WAVE 3"` / `"WAVE APPROACHING"`. Boss: `"BOSS INCOMING"` / `"APEX APPROACHES"`, gold (`VisualTheme.Gold`), slight scale pop `1.0 → 1.15 → 1.0`.
- **Sound cue:** distinct warning sting (boss = lower-pitched, longer); optional soft heartbeat tick during the warning window (stops when wave starts).
- **Hold then fade:** banner remains for the full lead time, then fades out as the first enemies spawn (`WaveManager.BeginNextWave()`).

### Implementation hook
**New `EventBus` member** (`scripts/core/EventBus.cs`):
```csharp
// Enemy / Wave
public event Action<int, bool>? OnWaveIncoming;   // (waveNumber, isBoss)
public void EmitWaveIncoming(int wave, bool isBoss) => OnWaveIncoming?.Invoke(wave, isBoss);
```

**`WaveManager` changes** (`scripts/core/WaveManager.cs`):
- Add `public const float WAVE_WARNING_LEAD = 2f;` and `public const float BOSS_WARNING_LEAD = 4f;`.
- In `_PhysicsProcess` breather block: when `_breatherTimer <= WAVE_WARNING_LEAD` and not yet warned for this breather → `EventBus.Instance.EmitWaveIncoming(_currentWave + 1, false)`; reset the "warned" flag when `BeginNextWave()` runs.
- In boss countdown: when `_bossTimer >= BOSS_INTERVAL_SECONDS − BOSS_WARNING_LEAD` and not yet warned → `EventBus.Instance.EmitWaveIncoming(_currentWave, true)`; spawn boss `BOSS_WARNING_LEAD` seconds later (track a `_bossPending` flag + timer).

**New node:** `WarningBanner : CanvasLayer` subscribes to `EventBus.OnWaveIncoming`, shows banner (tween flash + scale + text), plays sound, hides after the lead duration.
**C# method to call:** `WarningBanner.Show(int wave, bool isBoss)`.

**Files to touch:** `scripts/core/EventBus.cs`, `scripts/core/WaveManager.cs`, `scripts/ui/` (new `WarningBanner.cs`), audio bus for sting.

**Testability:** wave/boss lead-time logic is pure timer math — extract threshold checks into a Godot-free class + xUnit test. Banner tween needs headless gate.

---

## 3. Power-up collection feedback

### Mechanic
When the hero collects a power-up (XP gem, magnet, etc.), confirm the acquisition immediately so the player feels the stat change, without cluttering the screen during dense fights.

### Banner vs toast vs HUD pulse — recommendation
Use **all three**, scoped differently:
- **HUD banner (existing):** `HUD.ShowPowerup(string type)` shows the power-up name in the `PowerupList` for `PowerupBannerSlots.Lifetime = 5s` (already implemented; `MaxBanners = 5`, slot recycling via `SelectSlot`). Good for "what did I just get?".
- **Toast / float-up (new):** a small `"+XP"` / icon text that floats up from the collection point (like a damage number but in pickup color, `VisualTheme.Cyan`), lifetime `0.9–1.2s`. Good for "I collected something right here".
- **HUD pulse (new):** a one-shot flash of the relevant bar (XP bar or Health bar) `0.2s` gold → normal. Good for "stat changed now". Do not pulse on every XP tick — pulse only on level-up or meaningful pickup.

### Stacking behavior (multiple powerups collected quickly)
- **HUD banners:** `PowerupBannerSlots.SelectSlot` already handles it — first expired slot; if all 5 active, recycle the oldest (smallest timer). Cap at 5 concurrent names; older banners fade first.
- **Float-up toasts:** pool them, cap concurrent (e.g. 8); when cap reached, drop the oldest rather than stacking infinitely (avoid clutter, S4/T4).
- **HUD pulse:** if multiple collections arrive within `0.15s`, coalesce into a single pulse rather than strobing; queue the banner names normally.

### Timing values
| Element | Duration |
|---------|----------|
| HUD banner | 5.0s (existing `PowerupBannerSlots.Lifetime`) |
| Float-up toast lifetime | 1.0s |
| Float-up speed | 1.5 units/s up |
| HUD bar pulse | 0.2s flash |
| Pulse coalescing window | 0.15s |

### Implementation hook
**Existing:** `EventBus.OnPowerupCollected(string type)` → `HUD.ShowPowerup(string type)` (already wired in `HUD._Ready`). `PowerupBannerSlots.SelectSlot(float[] timers)` is pure and xUnit-tested.

**New (toast):** `EventBus` event `OnPowerupPickedUp(Vector2 worldPos, string type)` emitted from the pickup `Area2D` `body_entered`; `FloatingPickupText : CanvasLayer` subscribes and spawns pooled float-up labels in `VisualTheme.Cyan`.
**C# method to call:** `FloatingPickupText.Spawn(worldPos, type)` (or self-subscribe via `EventBus.Instance.EmitPowerupPickedUp(...)`).

**New (pulse):** in `HUD._Process`, when `hero.Level` increases (track `_lastLevel`) or on a new `OnLevelUp` event, flash `XpBar`/`HealthBar` fill gold for `0.2s` via tween on the `fill` stylebox color.

**Files to touch:** `scripts/core/EventBus.cs`, `scripts/ui/HUD.cs` (pulse + coalescing), `scripts/ui/` (new `FloatingPickupText.cs`), pickup `Area2D` call sites.

**Testability:** `SelectSlot` is already unit-tested headlessly. Pulse coalescing timer logic is pure — extract + test.

---

## 4. Health bar low-health state

### Mechanic
When hero health drops below a threshold, signal danger progressively so the player knows to retreat / pick up healing.

### Threshold & treatment
- **Low-health threshold:** `≤ 30%` of max health (`CurrentHealth / MaxHealth <= 0.30f`). Below this, danger signals activate; at `≤ 15%` they intensify.
- **Health bar color shift:** bar fill tweens `Gold → Red` (`VisualTheme.LowHealth`) as health crosses the threshold; pulse the bar red on each hit while low.
- **Screen vignette / edge glow:** fullscreen dark-red vignette (`ColorRect`) whose alpha scales with missing health: `0.0` at 100% → `~0.30` at 30% → `~0.55` at 10%. Optional thin red edge glow. Intensifies smoothly, not a binary flash (avoid clutter during combat, S7/T3).
- **Hit flash:** on each damage hit while low, a stronger/faster red flash of the vignette + a low-health heartbeat sound (sparely — max once per second).
- **Audio:** low-health drone/heartbeat looping while below threshold; stops when healed above threshold.

### Timing values
| Value | Default |
|-------|---------|
| Low threshold | 0.30 (30%) |
| Critical threshold | 0.15 (15%) |
| Vignette alpha max | 0.55 |
| Vignette alpha per missing-health | `alpha = 0.55 * (1 − healthPct)` clamped `[0, 0.55]` |
| Vignette tween speed | 0.25s (smooth, not instant) |
| Bar flash on hit (low) | 0.15s red flash |
| Heartbeat sound cadence | once/s while low |

### Implementation hook
**Existing:** `EventBus.OnHeroDamaged(float damage)` → `HUD.RefreshHealth()` (already wired). `HUD` already tracks `hero.MaxHealth`/`CurrentHealth`.

**`HUD` additions** (`scripts/ui/HUD.cs`):
- `private const float LOW_HEALTH_PCT = 0.30f;`
- Add a fullscreen vignette `ColorRect` child of HUD (alpha tweened).
- In `_Process`, compute `healthPct = hero.CurrentHealth / hero.MaxHealth`; if `< LOW_HEALTH_PCT` → set vignette alpha `= clamp(0.55f * (1f − healthPct), 0, 0.55)`, tint `HealthBar` fill toward `VisualTheme.LowHealth`; else vignette alpha `0`, fill `Gold`.
- On `OnHeroDamaged`, if below threshold → flash vignette + bar red for `0.15s`.
- Track `_lastLevel` to pulse on level-up (see §3).
- `VisualTheme`: add `Color LowHealth = new Color(1f, 0.25f, 0.25f, 1f)`.

**C# method to call:** internal `HUD._UpdateLowHealth(float delta)` (no public call needed; driven by events + `_Process`).

**Files to touch:** `scripts/core/VisualTheme.cs`, `scripts/ui/HUD.cs`, audio bus for heartbeat.

**Testability:** vignette-alpha-from-health-percent is pure math — extract + xUnit test. Vignette tween needs headless gate.

---

## 5. Pause / upgrade menu UX

### Mechanic
Level-up (or manual pause) opens an upgrade menu that halts gameplay, dims the world, presents perk choices in a modal panel, and returns focus cleanly.

### Current baseline
`UpgradeMenu.ShowMenu()` sets `Visible = true` and calls `GameManager.Instance.PauseGame()` (sets `GameState.Paused` + `tree.Paused = true`). `SelectPerk()` hides + calls `ResumeGame()`. Buttons use `Pressed` signals; `VisualTheme.StyleButton` already gives gold text / celestial hover.

### Recommended treatment
- **Dimming background:** a fullscreen `ColorRect` (`VisualTheme.Background` @ alpha `~0.70`) behind the modal, tweened `0 → 0.70` over `0.15s` on open, `0.70 → 0` over `0.10s` on close. Gameplay remains faintly readable but de-emphasized (S4/S5).
- **Modal panel:** centered raised panel (`VisualTheme.StylePanelRaised`: `PanelHighlight` bg + `Gold` border), title `"LEVEL UP"` / `"PAUSED"`, 3 perk buttons. Blocks all input behind it (tree is already paused).
- **Button focus / selection highlight:** on open, `Perk1Button.GrabFocus()`; keep `focus_mode = FocusMode.All` and wire `focus_neighbor_right/left/up/down` for gamepad/d-pad navigation. Hover stylebox already gold (`StyleButton`). Show a subtle focus ring/outline on the focused button. First button default-selected so the player can immediately confirm.
- **Close behavior:** this is a **forced-choice** modal (must pick a perk to resume) — do **not** let tapping the dim background close it (S6: store/forced popups require explicit action). `Escape`/`Pause` input should be ignored while the upgrade choice is open. For a general pause menu (not level-up), escape closes.
- **Timing:** fade-in `0.15s`, fade-out `0.10s`; button press → apply perk → hide → `ResumeGame()` immediately (no artificial delay).
- **Audio:** menu open sound, hover tick, select confirm.

### Implementation hook
**Existing:** `EventBus.OnLevelUp` → `UpgradeMenu.ShowMenu()` (wired). `GameManager.PauseGame()` / `ResumeGame()`. `UpgradeMenu.SelectPerk(int)`.

**`UpgradeMenu` additions** (`scripts/ui/UpgradeMenu.cs`):
- Add a dim-background `ColorRect` child; tween alpha in `ShowMenu()`/`SelectPerk()`.
- Call `VisualTheme.StylePanelRaised()` on the modal container; `VisualTheme.StyleButton()` on each perk button (already available).
- `Perk1Button.GrabFocus()` in `ShowMenu()`; ensure focus neighbors set (editor or code).
- Guard `Escape`/pause input while `Visible` (ignore until a perk is chosen).

**Files to touch:** `scripts/ui/UpgradeMenu.cs`, `scripts/core/GameManager.cs` (pause input guard, optional), `scripts/core/VisualTheme.cs` (no new colors needed — uses existing `PanelHighlight`/`Gold`).

**Testability:** pause/resume state machine is `GameManager.CurrentState` — pure-ish but tree-coupled (`SetTreePaused`). Deferred per AGENTS.md (`T3.1-rest`). Focus/selection logic needs headless gate.

---

## Consolidated: new `EventBus` members to add

Add to `scripts/core/EventBus.cs`:

```csharp
// ── Combat feedback ─────────────────────────────────────────
public event Action<Vector2, float, DamageType, bool>? OnDamageNumber;
public void EmitDamageNumber(Vector2 pos, float amount, DamageType type, bool critical)
    => OnDamageNumber?.Invoke(pos, amount, type, critical);

// ── Wave warning ────────────────────────────────────────────
public event Action<int, bool>? OnWaveIncoming;   // (waveNumber, isBoss)
public void EmitWaveIncoming(int wave, bool isBoss) => OnWaveIncoming?.Invoke(wave, isBoss);

// ── Pickup feedback ─────────────────────────────────────────
public event Action<Vector2, string>? OnPowerupPickedUp;
public void EmitPowerupPickedUp(Vector2 pos, string type) => OnPowerupPickedUp?.Invoke(pos, type);
```

**Do not** change existing event signatures unless adding amount to `OnProjectileHit` — the new `OnDamageNumber` is cleaner and keeps `HitFlash` unaffected.

## File map (what to touch)

| Feature | Files |
|---------|-------|
| Damage numbers | `scripts/core/EventBus.cs`, `scripts/core/VisualTheme.cs`, `scripts/vfx/DamageNumber.cs` (new), `scripts/vfx/DamageNumberCanvas.cs` (new), call sites in `scripts/projectiles/`, `scripts/enemies/EnemyBase.cs`, hero abilities |
| Wave/boss warning | `scripts/core/EventBus.cs`, `scripts/core/WaveManager.cs`, `scripts/ui/WarningBanner.cs` (new) |
| Power-up feedback | `scripts/core/EventBus.cs`, `scripts/ui/HUD.cs` (pulse/coalesce), `scripts/ui/FloatingPickupText.cs` (new), pickup `Area2D` sites |
| Low health | `scripts/core/VisualTheme.cs`, `scripts/ui/HUD.cs` |
| Pause/upgrade | `scripts/ui/UpgradeMenu.cs` |

## Verification notes
- New pure logic (damage-number animation math, banner lead-time thresholds, `SelectSlot` stacking, vignette alpha, low-health thresholds) should be extracted into Godot-free classes + xUnit tests, following the AGENTS.md extraction pattern — these run headlessly.
- Canvas/Camera/Tree-dependent behavior (banners, modals, vignette tweens, focus) belongs in the Godot headless gate, which **cannot run C# in this workspace** (AGENTS.md known gap) — verify those manually on real hardware once the toolchain is fixed.
- Lint: keep `scripts/*.cs` free of `TODO`/`FIXME` (CI quality gate greps for them; see AGENTS.md).
