# Investigation — "The Living Arena": environment scenes & wildlife (PM pass)

Hero's Arena · Surgical-Investigation · **Project-Manager mode** (investigate → plan → build) · 2026-10-17 · strategy: **codebase-first + pattern-driven + targeted-docs**

Role lens: environment-scene & wildlife frontend engineering — how the arena *place* and its *creatures* feel, not the combat rules.

## Scope

- **Target:** current workspace — Godot 4.3 + C# (.NET 8) top-down arena shooter, "dark godlike" aesthetic: a near-void arena tinted by a `CanvasModulate` veil; combat carves light out of the dark (`LivingLightRig`); screen refraction + karma vignette (`ScreenDistortionFX`); debris drama (`DestructionDrama`).
- **Mission (asked):** investigate 10 ways to blow players' minds (environment scenes + wildlife), draft a detailed plan, implement following best practices.
- **Deliverable:** 10 documented ways + this plan + the strongest coherent subset implemented in-repo and locally verified.

## Mission verification — stated vs actual

The README sells "waves of enemies across hero classes; destruction escalates threat". The *combat* loop is real. The *place* is not:

| Environment / wildlife layer | State before this pass |
|---|---|
| Arena floor | None. TileMapLayers are empty; world = void background colour. |
| Ambient weather / particles | None. |
| Ambient wildlife | None — every moving thing is hostile and spawned by WaveManager. |
| Biome identity (5 map scenes) | None — RuinedMetropolis is the only map wired; all five look identical (empty). |
| Wind | None. Nothing couples particles, ground, and creatures. |
| Reaction to combat (world level) | Light flashes + debris + screen warp exist; *living things* do not react. |
| Boss-wave drama | Light mood + banner; the world itself does not change state. |

So: the fight has drama, the **habitat has none**. Every gap above is a place where environmental storytelling can create wonder for near-zero cost, because the engine-side primitives (EventBus wave/threat/destruction signals, pool patterns, code-generated textures) are already in place.

## External research

Saved to `docs/external-research/` (Tier-tagged):

- `living-world-wildlife-patterns.md` — ambient-critter systems, scatter behaviour, boid-lite budgets, single-wind coherence, biome identity, what reviewers actually praise as "alive" (Tier 1–4 sources).
- `godot-43-ambient-life-impl.md` — Godot 4.3 constraints: particles on large emission regions, `_Draw` vs MultiMesh vs particles at 100–800 primitives, light caps, deterministic RNG, Parallax2D status, vertex-shader wind (Tier 1 docs + issues).

(Research files are written by parallel research subagents; findings are folded into the specs below. Where a finding changed the design, the plan says so.)

---

# The 10 ways to blow players' minds

Each way: what it is, why it lands, and the technique that makes it cheap enough to ship.

### 1. Ambient wildlife menagerie — the void becomes a habitat ✅ implemented
Biome-specific critters (fireflies, ember-moths, star-motes, spore-mites, dust-hoppers) wander, bob, and pulse through the arena *outside* combat. **Why it lands:** living creatures turn a box into a place — the cheapest, most universal "alive" signal in games (Hollow Knight's ambient flies, Journey's cloth creatures). **Technique:** one critter simulation (pure C#), species differ only by parameter rows (speed, size, colour, glow, altitude). All textures code-generated; zero art budget.

### 2. Scatter-and-return — your violence has an aftermath ✅ implemented
Explosions and heavy impacts punch nearby wildlife into panicked flight; when the fighting stops, calm returns and critters drift back. **Why it lands:** damage numbers tell you what you hit; fleeing birds tell you *what you've done* — an emotional read of power. Used by RDR2/BotW to sell stakes; here it sells the hero being a force of nature. **Technique:** radial panic impulse on `OnEnvironmentDestroyed` / explosive hits; panic decays → wander → home-seek.

### 3. World-holds-its-breath — the boss tell is silence ✅ implemented
When a boss wave is incoming, wildlife *hides* and the air thins; the arena's sudden emptiness is the warning, arriving before any enemy does. **Why it lands:** negative space as telegraph. Games telegraph dread with silence (Hunt: Showdown, Rain World); nothing in the current UI does this, so it reads fresh. **Technique:** mood flag on `OnWaveIncoming(wave, isBoss)`; director hides critters for the wave, restores on `OnWaveCompleted`.

### 4. One wind — one coherent force over everything ✅ implemented
A single gusting wind vector drives ground-sway, slants the rain/ash, drifts the motes, and nudges the critters; explosions punch local wind impulses. **Why it lands:** coherence is invisible but *felt* — when wind, grass and creatures agree, the scene stops being layered effects and becomes a weather system (Ghost of Tsushima's wind, BotW). **Technique:** `AmbientWindState` (pure): slow base + periodic gusts + position-falloff impulses; every consumer samples the same state.

### 5. Biome atmosphere presets — five arenas, five signatures ✅ implemented
Each map scene gets a signature ambience resolved from its scene path: neon rain (RuinedMetropolis), ash + rising embers (VolcanoCrater), drifting star-dust (OrbitalPlatform), spore bloom (SubterraneanLab), dust haze (AlienWasteland) — each with tint, particle field, wind scale, and a wildlife roster. **Why it lands:** identity without level design. The moment map selection ships, players feel five places, not one box recoloured. **Technique:** `BiomeAtmosphereState` profile table + path→biome resolver; one `BiomeAtmosphere` node configures code-generated particle fields per profile.

### 6. Living ground — the void gains a floor ✅ implemented
Deterministic cracks, pebbles and tufts scutter the floor; they sway with the wind and *catch the combat light* already flying around the arena. **Why it lands:** light only feels real when something can be lit — this is what the existing light rig was missing. The dark-godlike palette finally has a surface to carve out of the void. **Technique:** `GroundDetailPlan` (position-hashed deterministic generation, visible-cell budget, sway math) drawn by one `_Draw` node at capped redraw rate.

### 7. Curiosity encounters — wildlife notices *you* ✅ implemented
Between waves, when the hero stands still, a few critters drift toward the hero (fireflies orbiting Atlas, a dust-hopper investigating your boots); combat or damage scatters them. **Why it lands:** micro-emergent moments players screenshot — the world acknowledging the player as a presence, not a camera. **Technique:** director "calm curiosity" mode: gentle attraction with a minimum stand-off radius, only while relaxed; parameter-gated.

### 8. Disturbance memory — the ground keeps history ✅ implemented
Footprints fade over ~30 s, heavy enemies crack the floor, scorch patches linger, and wildlife avoids scarred ground for a while. **Why it lands:** memory turns the arena into a stage the player scribes. **Technique:** a fixed stamp pool (`DisturbanceMemoryState`, pure class: footprints from hero movement, cracks + scorch from `OnEnvironmentDestroyed`) rendered in one `_Draw` pass of code-generated radial brushes (`ScarLayer` node), plus a `SampleScar` hook the wildlife director uses to re-roll scarred wander targets — deviation from the planned ImageTexture decal layer (same intent, bounded cost, no per-frame image decay). Est. 2–3 days → shipped in ~1 session.

### 9. Ecosystem events — rare scheduled vignettes ⏳ planned (next pass)
A weighted event deck between waves: a flock sweeps the arena corner-to-corner; an ember storm rolls at max karma; moths circle a kill site for a minute. **Why it lands:** rarity = story. Players who see an event once tell other players about it. **Technique:** a `MomentDirector` scheduling 1-vignette-per-N-waves from a pure event table; reuses critters + particles as actors. Est. 2–3 days.

### 10. Reactive props & vignettes — set dressing that fights back ⏳ planned (next pass)
Lanterns and hanging chains bend under shockwave wind; banners flip; micro-scene props (crashed drone, spent campfire) get lit by combat flashes and disturbed by waves passing through. **Why it lands:** the props become characters — audiences read intention into anything that moves consistently. **Technique:** prop kit as pooled `Node2D` children of the map scenes + the wind state as the single driver; vertex-shader bend for hanging pieces. Est. 3–4 days.

**Priority logic:** 1–7 form one coherent "Living Arena" substrate — they multiply each other (wind feeds critter drift feeds ground sway; mood feeds critters feeds atmosphere). 8–10 depend on that substrate existing first, which is why this pass builds the substrate.

---

# Implemented this pass — "The Living Arena" substrate

## Architecture (per AGENTS.md extraction pattern)

| File | Kind | Role |
|---|---|---|
| `scripts/core/AmbientWindState.cs` | pure | Base wind + gusts + position-falloff impulses |
| `scripts/core/WildlifeDirectorState.cs` | pure | Critter pool sim: spawn schedule, wander, panic, hide, curiosity |
| `scripts/core/BiomeAtmosphereState.cs` | pure | 5 biome profiles (+default), path→biome resolution |
| `scripts/core/GroundDetailPlan.cs` | pure | Deterministic ground detail: hash, cell enumeration, sway |
| `scripts/vfx/BiomeAtmosphere.cs` | node | Per-biome particle fields; wind-driven slant; wave moods |
| `scripts/vfx/AmbientWildlife.cs` | node | Renders + drives the critter pool; subscribes combat/wave events |
| `scripts/vfx/LivingGround.cs` | node | Single-`_Draw` ground detail; capped redraw |
| `tests/HeroArena.Tests/Systems/*` | tests | xUnit coverage for all four pure classes |

All four pure classes are `System`-only (no `using Godot`), mirroring `LightFXState` / `DebrisBurstPlan`.

## Event hooks (all already emitted — no gameplay code touched)

- `OnEnvironmentDestroyed(pos, radius)` → wildlife panic; wind impulse (atmosphere + ground).
- `OnProjectileHit(pos, type)` → Explosive/Fire panic the wildlife; Explosive punches a wind impulse (atmosphere + ground).
- `OnWaveIncoming(wave, isBoss)` → boss: wildlife hides entirely + population 0 + storm mood; normal: nothing (the scatter fires on Started).
- `OnWaveStarted(wave)` → non-boss: scatter anchored on the hero (everything near the player bolts as the wave arrives); boss: keeps the breath held for the whole fight.
- `OnWaveCompleted(wave)` → calm returns; boss hold ends and wildlife fades back over the spawn schedule.
- `OnHeroDamaged(damage)` → small panic near the hero.
- `OnThreatLevelChanged(level)` → population thins up to 20% as threat rises; field intensity climbs (atmosphere).

## Verification evidence (2026-10-17, final; engine-level audit re-run the same day)

- Release build at pass time: `Build succeeded`, 0 errors, 3 pre-existing `WaveManager` CS8602 warnings. Today's Debug build matches that baseline (the 3 warnings remain in the writer's in-flight `WaveManager.cs` — their domain; zero diagnostics in any Living Arena file).
- Full suite: 328/328 at pass time → **334/334 today**.
- Roslynator: **20 diagnostics**, all pre-existing (3× CS8602 `WaveManager`, 16× CA1822 `CoreSystemTests.cs`, 1× CA1016); nothing in new files.
- TODO/FIXME gate: clean (exact AGENTS.md command).
- `Main.tscn` structural check: 17 nodes, 22 ext_resources, `load_steps=23` consistent, no dangling `ExtResource`, node order verified `Map < LivingGround < BiomeAtmosphere < AmbientWildlife < LivingLightRig`.
- **Godot gates:** CoreSystemTests.tscn **13 passed, 0 failed**, exit 0 (the writer added the R-FX surroundings smoke and enemy target-disposal regression checks; AGENTS.md updated accordingly). `Main.tscn --quit-after 600`: exit 0, **zero `ObjectDisposedException`s** — the writer's `IsInstanceValid` guards landed in `EnemyBase.cs`/`MainBootstrap.cs`, so the stale-hero storm no longer reproduces in-engine. Root cause recap: `HeroBase.Die()` never cleared `GameManager.ActiveHero`, and `hero == null` is **false** for disposed Godot wrappers. Remedy stands as documentation: clear `ActiveHero` on death and/or `IsInstanceValid` at read sites.
- New probe `tests/GodotTests/LivingArenaProbe.tscn` + `LivingArenaProbe.cs`: **6 passed, 0 failed**, exit 0. Boots all three nodes on the live tree and asserts wiring + draw order + arena centring + spawn/fade schedule + boss hold-breath + atmosphere fields + rain-gradient visibility + ground boot. Run: `env DOTNET_ROOT=~/.dotnet8 ~/.local/bin/godot-mono --headless --path . res://tests/GodotTests/LivingArenaProbe.tscn`.
- Honest limit remaining: `_Draw` **pixels** are not asserted headlessly (no visual delta); `_Draw` executes error-free in headless runs and its geometry is xUnit-covered through `GroundDetailPlan`.

**Review disposition (background review of the delivered pass; fixes are in this pass's files unless noted):**

| # | Finding | Leverage | Disposition |
|---|---------|----------|-------------|
| 1 | Arena rect read as top-left — wildlife box and rain mostly off-screen | Critical | **Fixed.** Shared `SceneAnchors` resolver reads `MapBase.ArenaSize` as centred on the map node origin (probe asserts centre = map position, half extents = ArenaSize/2). Removed duplicated resolvers from all three nodes. |
| 2 | Wave term of mood curve always zero (`GameManagerState.CurrentWave` never increments) | Required | **Fixed via workaround.** `BiomeAtmosphere` now tracks wave from `OnWaveIncoming`/`OnWaveCompleted` payloads instead of the dead `GameManager` property; reported to the `GameManagerState` owner (`CurrentWave` still never increments anywhere). |
| 3 | Boss hold never self-releases if a wave never completes | Required | **Fixed.** Safety valve `BossHoldMaxSeconds` + release on a non-boss `OnWaveStarted` while held, both funnelling through one `ReleaseBossHold()`. Probe asserts the hold empties and releases. |
| 4 | Curiosity tests can't fail for the boldness inversion they catch | Optional | **Fixed.** Same-seed wander control vs. curiosity run, assert ≥80 px separation; inverted boldness gate fails both (mutation-verified). |
| 5 | Impulse eviction test only exercises the first slot | Optional | **Fixed.** Most-consumed slot at index 3 must win the replacement; first-slot-always impl fails (mutation-verified). |
| 6 | Spawn-clearance test satisfied by the failure path | Optional | **Fixed.** Full pool (64 spawns); assert every home clears `HeroSpawnClearance`; check removed → test fails at seed-scale (mutation-verified). |
| 7 | Sway sign unverified | Optional | **Fixed.** New `Sway_LeansDownwind_AndAmplifiesWithWindStrength`; sign-flipped impl fails (mutation-verified). |
| 8 | Tautological/const-only tests | Optional | **Fixed.** Deleted `ExplosionImpulse_DerivationIsDeterministic` (self-compare) and `BossStorm_IsLouderThanOne` (const > 1); replaced with `ImpulseForHit` mapping test and pinned multiplier envelope test. |
| 9 | Duplicated biome/arena resolvers + impulse mapping across nodes | Optional | **Fixed.** `SceneAnchors` (biome + arena) and `AmbientWindState.ImpulseForHit` (blast-radius table incl. acid) are single sources. |
| 10 | `StreakTexture` gradient collapses — rain field invisible | Nit | **Fixed.** 3-point gradient (transparent → white mid → transparent); probe asserts mid-line alpha peak. |
| 11 | `Critter.Phase` dead state while render keys off slot index | Nit | **Fixed.** Render reads `c.Phase`; `PhaseStride` removed. |
| 12 | Wind doc overclaims creature-drift coherence | Nit | **Fixed.** Summary narrowed to ground sway + particle slant; wildlife blast reactions via panic only. |
| 13 | `GenerateRegion` silently truncates the bottom of large views | Nit | **Fixed.** Deterministic stride thins cells evenly when the worst case overflows the budget; test asserts even Y spread. |
| 14 | Curiosity comment (needs stillness) vs code (0.2 floor while running) | Nit | **Fixed.** Target is now `_calm * _stillness`; comment accurate. |
| 15 | Destruction path dead in shipped scene (no destructibles placed; Exploder acid emits but causes no impulse/panic) | FYI | **Accepted/documented.** Acid blasts now participate in the impulse table + wildlife panic; placing destructibles on maps is map/authoring work for their stream. |
| — | `MainMenu.tscn` ↔ `Sandbox.tscn` cycle reported earlier | — | **Struck.** Both scenes boot clean today; `Sandbox.tscn` no longer references `MainMenu.tscn`. |

**Audit findings outside this pass (reported, not touched):**
- Booting `Sandbox.tscn` standalone logs missing InputMap action errors — actions are registered at runtime by `MainBootstrap.EnsureInputMap()` (MainBootstrap.cs:73), not in `project.godot`; the Main→menu→sandbox flow inherits them process-wide.

## Implementation notes & deviations from the plan (recorded for honesty)

- **Scatter anchor:** plan said "scatter pulse from arena edges"; shipped as a hero-anchored panic (radius 1400, strength 0.55). Symmetric multi-epicentre edge panics cancel out and leave the last epicentre as the effective one — hero-anchored reads as "your zone clears out" and composes with combat panic.
- **Boss hold:** `HideAll` freezes existing critters, `TargetPopulation=0` stops mid-fight respawns; on completion `EndHide` + restored population fades them back in over the normal spawn schedule (one at a time) — the world breathes again.
- **One wind, three instances:** each node owns an `AmbientWindState` instance (no cross-node coupling, nodes work standalone); coherence is by construction — identical formulas + identical dt sequence + identical events, and impulse parameters derive from the single `AmbientWindState.ExplosionImpulse` helper via `ImpulseForHit`. The wildlife node feeds the director the base wind sampled at the hero; explosions reach the critters via `Panic` instead.
- **Accent colours live in the biome table:** `SpeciesProfile` gained `AccentR/G/B` so a new biome is still one table row — the node holds no colour table.
- **Firefly desync:** per-critter phase seeded at spawn (`c.Phase = rng * 2π`), so a respawn gets a fresh rhythm instead of inheriting its slot's.
- **Ground redraw:** item list regenerates only when the 32 px-snapped view rect changes; `_Draw` runs at 30 Hz and computes sway/impulses live, so camera-idle frames skip generation entirely. `_Draw` items are lit by default (research `godot-43-ambient-life-impl.md` §7), which is what makes the floor catch the combat light.

## Constraints & policy

- Zero new art assets — every texture is code-generated (`GradientTexture2D`, as in `DestructionDrama`).
- Pools pre-allocated; no runtime node instantiation per event.
- Wildlife/budget numbers are constants on the pure classes; nodes only map state onto engine objects.
- Node order: `Map → LivingGround → BiomeAtmosphere → AmbientWildlife → (existing FX/entities/HUD)` — ground under particles under creatures; entities keep drawing above ambient life.
- Solo-writer discipline: the four pure classes, three nodes, and tests are all-new files; the only shared file touched is `scenes/Main.tscn` (3 nodes + 3 ext_resources), re-read immediately before the edit (a concurrent writer was active in the tree during this pass).
