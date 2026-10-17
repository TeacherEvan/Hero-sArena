# Investigation — Interactive & Destructive Environment Illusions (PM pass)

Hero's Arena · Surgical-Investigation · **Project-Manager mode** (investigate → build directly, no report) · 2026-10-17 · **full-sweep** strategy

## Scope

- **Target:** current workspace — Godot 4.3 + C# (.NET 8), top-down arena shooter
- **Mission (asked):** make surroundings more interactive and destructive using special art techniques — shading, sprite, particle and light manipulation — as effect illusions
- **Deliverable:** 3 recommendations on that theme, each implemented in-repo and locally verified

## Mission verification (stated vs actual)

The README states environmental destruction "escalates threat via the Collateral Karma system". The **mechanical** half existed (`CollateralKarma` → `DestructionTracker` → threat levels). The **visual** half did not:

| Layer | State before this pass |
|---|---|
| Decals | ✅ `DecalSystem` (pooled sprites; `CraterMark` emitted on cover destruction) |
| Screen shake / hit-stop / hit flash | ✅ `ScreenShake`, `HitStop`, `HitFlash` |
| Lights (Light2D, CanvasModulate) | ❌ none anywhere in the repo |
| Particles (CPU/GPU) | ❌ none |
| Shaders | ❌ **zero** `.gdshader` files in the project |
| Debris / shatter illusion | ❌ none — destroyed cover simply vanished and left a decal |

So the arena's "escalating destruction" had no perceptible feedback beyond decals and a red flash. The event bus already exposed the needed hooks (`OnProjectileHit`, `OnEnvironmentDestroyed`, `OnHeroDamaged`, `OnThreatLevelChanged`, `OnWaveIncoming`, `OnEnemyKilled`) — several had no or minimal consumers. The build below adds consumers rather than new gameplay systems: pure illusion layers.

## External research (full-sweep)

Saved under `docs/external-research/` (Tier-tagged, per strategy):

- `2d-destruction-illusions.md` — technique survey (one-shot bursts, additive flash rings, dissolve shaders, decal memory, reactive light energy, threat vignette; source tiers incl. Godot docs Tier 1, GDC/blog Tier 3, community Tier 4)
- `godot-43-2d-lighting.md` — Light2D/CanvasModulate specifics, code-generated radial light textures, 16-lights-per-CanvasItem cap, glow-inert finding (`GlowBloom=0` + threshold 1.0 + LDR canvas ⇒ current glow is a no-op), `FillEnum.Radial` / `BlendModeEnum.Add` exact names
- `godot-43-screen-shaders.md` — `hint_screen_texture` syntax, world→UV denominator (`CanvasTransform / GetVisibleRect().Size` — matches implementation), array-uniform traps (`Vector4[]` does not marshal; `float[32]` does), BackBufferCopy conditions (not needed: only screen-reader in frame), idle-hide requirement
- `godot-43-2d-particles.md` — C# names reflection-verified from the local GodotSharp 4.3.0.0 (`CpuParticles2D` not `CPUParticles2D`, `FillEnum.Radial`, `BlendModeEnum.Add`; `Finished` is a parameterless `Action`), one-shot re-trigger must use `Restart()` — never `Emitting` toggling (engine-source race: `set_emitting()` early-returns on unchanged value), CPU-vs-GPU verdict (CPU for many small 2D bursts; trails/sub-emitters are GPU-only and unavailable on Compatibility), official 4.3 demo tuning recipes, pool arithmetic (idle CPU emitters are ~free). Confirms the shipped R-FX3 choices: `Restart()`-based re-trigger, idle-then-restart pooled emitters, exact enum names

## The 3 recommendations (all implemented this pass)

### R-FX1 — "Living Light": the arena is lit by the fight (light manipulation)

- **What:** `CanvasModulate` veil over the world + a pool of 12 reused `PointLight2D` flashes with code-generated radial textures (zero new art). Impact strobes take the damage-type palette (`HitFlashPalette`), environmental destruction flares warm, hero damage pulses red, enemy kills shed pale soul-light; ambient glow + deterministic flicker escalate with threat level and boss waves.
- **Why:** light is the cheapest illusion of "the environment reacts to you". The dark-godlike theme (void background, gold/cyan palette) was already committed to; this makes the world visibly *carved out of* that void by combat.
- **Event hooks:** `OnProjectileHit`, `OnEnvironmentDestroyed`, `OnHeroDamaged`, `OnEnemyKilled`, `OnThreatLevelChanged`, `OnWaveIncoming`.
- **Rate policy:** ≥50 ms between small flashes; explosions/hero-damage bypass. Pool never exceeds 12 lights (research cap: 16 per CanvasItem).
- **Effort:** ~1 day. **Status:** ✅ implemented — `scripts/core/LightFXState.cs` + `scripts/vfx/LivingLightRig.cs` + `LightFXStateTests` (18 tests).

### R-FX2 — "Shockwave air": screen-space refraction + karma vignette (shading)

- **What:** full-screen `canvas_item` shader (`shaders/screen_distortion.gdshader`) on a `CanvasLayer` + `ColorRect`; up to 8 simultaneous ripple rings bend `SCREEN_UV` around explosions/impacts (refraction illusion) with a faint sheen; the karma vignette mixes screen edges toward deep crimson as threat rises (with a pulse on hero damage). HUD stays crisp — the layer draws before HUD/HitFlash canvases.
- **Why:** air with physical presence — explosions stop being sprites on a plane and start bending the frame. The vignette ties "how much you destroyed" (CollateralKarma) directly to how the screen *feels*.
- **Controls (exported/tunable):** strengths/lifetimes/radii are named constants; ripple policy (rate limit, duplicate coalescing, bypass strength) lives in the tested state class.
- **Effort:** ~1 day. **Status:** ✅ implemented — `scripts/core/ScreenRippleState.cs` + `scripts/vfx/ScreenDistortionFX.cs` + `ScreenRippleStateTests` (17 tests) + shader.

### R-FX3 — "Shatter & fade": debris + smoke + sparks + ground ring (sprites + particles)

- **What:** on `OnEnvironmentDestroyed`: 4–12 spinning chunk sprites burst with deterministic per-position spread (same cover always shatters the same way), ballistic arcs, one damped bounce, then fade; a two-slot smoke `CpuParticles2D` pass; a two-slot additive spark burst; and an expanding additive ground-ring flash that fakes light propagation across the floor. All textures generated in code (soft square chunks, radial dots, ring band).
- **Why:** the illusion of mass. A crate that bursts into tumbling chunks reads as *destructible* even though nothing physical changed; embers + smoke sell heat; the ground ring sells the blast wave.
- **Event hook:** `OnEnvironmentDestroyed` (already emitted by `DestructibleObject`). Pools are pre-allocated (64 shard sprites, 4 rings, 2+2 emitters) matching the repo's zero-runtime-instantiation pattern.
- **Effort:** ~1 day. **Status:** ✅ implemented — `scripts/core/DebrisBurstPlan.cs` + `scripts/vfx/DestructionDrama.cs` + `DebrisBurstPlanTests` (20 tests).

## Files added / changed

**Added (all new — no user in-flight file was edited):**
- `scripts/core/LightFXState.cs`, `scripts/core/ScreenRippleState.cs`, `scripts/core/DebrisBurstPlan.cs` (Godot-free logic cores, per AGENTS.md extraction pattern)
- `scripts/vfx/LivingLightRig.cs`, `scripts/vfx/ScreenDistortionFX.cs`, `scripts/vfx/DestructionDrama.cs` (node wrappers; delegate 1:1)
- `shaders/screen_distortion.gdshader`
- `tests/HeroArena.Tests/Systems/LightFXStateTests.cs`, `ScreenRippleStateTests.cs`, `DebrisBurstPlanTests.cs`
- `docs/external-research/*` (4 files)

**Changed (one shared file, minimal edit):**
- `scenes/Main.tscn` — 3 new nodes + 3 ext_resource entries, `load_steps` 17→20: `LivingLightRig` (after Map), `DestructionDrama` (after DecalLayer), `ScreenDistortionFX` (before HUD, so the distortion samples the world only).

## Verification evidence (2026-10-17, ~02:40 +0700)

| Gate | Result |
|---|---|
| `dotnet build -c Release` | ✅ 0 errors, 3 warnings (pre-existing `WaveManager` CS8602, present at baseline) |
| `dotnet test` | ✅ **231/231 pass** (baseline 176 + 55 new) |
| TODO/FIXME quality gate | ✅ pass |
| Roslynator analyze | ✅ 0 diagnostics in any new file (20 pre-existing/user-work diagnostics remain elsewhere: 16× CA1822 infos, 3× CS8602, 1× CA1016 — same classes as committed code; CI has never run, issue #23) |
| Godot 4.3 engine, headless | ✅ `Main.tscn` parses (14 nodes), all 3 FX nodes present, `screen_distortion.gdshader` resource loads (GDScript `SceneTree` check — the one engine path that works in this workspace) |
| tscn structural validator | ✅ 19 ext resources, all `ExtResource` references resolve, `load_steps=20` consistent |

## Not verified this pass (honest list)

- **GPU/visual runtime behavior** — shader compilation on a real GPU, light interplay, particle look, ripple feel. The Godot mono binary in this workspace cannot execute C# (documented in README/AGENTS.md); everything above is compile-level, logic-level and parse-level verified.
- **Sandbox.tscn** not wired (in-flight user file — deliberately untouched); FX nodes currently live in `Main.tscn` only.
- `project.godot` untouched — no autoloads added; all three systems are scene-wired nodes.

## Live-edit caveat

Another process was editing the working tree during this investigation (`scripts/heroes/Synapse.cs` changed at 02:40:09; one Roslynator run caught a transient mid-edit `CS0019` that the next build no longer saw). All verification reflects the tree as of the timestamps above; none of my changes touch files that process is editing.

## Follow-ups (identified, not built)

1. **HDR 2D glow** — flashes can bloom by enabling `rendering/viewport/hdr_2d` + lowering the glow HDR threshold (research: `godot-43-2d-lighting.md`; current glow is a documented no-op).
2. **Debris trails** — `GpuParticles2D` `trail_enabled` chunks as an upgrade path if CPU bursts show up hot; note trails/sub-emitters are Forward+/Mobile only (fine on the project's default renderer, unavailable on Compatibility) and GPU has a high per-emitter base cost + `VisibilityRect` culling (research: `godot-43-2d-particles.md` §3, §6).
3. **Decal memory layer** — CanvasGroup-wrapped scorch accumulation so repeated destruction visibly scars the ground (research: `2d-destruction-illusions.md`).
4. **Wire the Sandbox scene** with the three nodes once its owner finishes the in-flight work.
