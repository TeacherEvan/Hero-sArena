# 2D Destruction & Reactive-Environment Illusions — External Research

**Context:** Hero's Arena — Godot 4.3 + C# (.NET 8), top-down 2D arena shooter (destructible cover, explosions, wave combat).
**Brief:** art-illusion techniques for faking mass, light, and destruction with sprites/particles/shading/lights — with priority on techniques that need **code-generated visuals only (no new art assets)**.
**Date:** 2026-10-17.

---

## Tier legend

| Tier | Meaning | Examples used here |
|---|---|---|
| Tier 1 | Official engine documentation | Godot docs (2D lights, particle systems, class refs) |
| Tier 2 | Official issue trackers / accepted answers | Godot proposals repo (noted as open, not implemented) |
| Tier 3 | Known-author blog / developer article / conference talk / shader author | Vlambeer's own explosion article, Cyanilux, Game Anim excerpt, godotshaders authors, Unity case study, Elemental Magic |
| Tier 4 | Community (Reddit, wikis, forums) | Gungeon wiki, r/gamedev Hades breakdown, godotforums |

Claims below are tagged per tier where they rely on a source. Items marked **(suggested)** are synthesis/tuning starting points, not sourced facts.

---

## Queries used

1. `Nuclear Throne Enter the Gungeon destructible props debris particles VFX technique`
2. `Godot 4 dissolve shader noise texture burn edge godotshaders`
3. `2D fake volumetric light shafts god rays sprite additive gradient technique`
4. `Richard Williams animation principles applied to game VFX follow-through anticipation secondary motion`
5. `scorch marks decal accumulation battlefield memory game technique overdraw alpha blending issues`
6. `Vlambeer "Art of Screenshake" juice explosions game feel Jan Willem Nijman`
7. `2D game lighting mood dynamic lights flicker combat intensity Godot point light 2D`
8. `Hades Dead Cells destructible props break VFX breakdown developer`
9. `Godot 4 persistent scorch decal ground Sprite2D darken blend mode bullet marks`
10. `GDC talk VFX smoke mirrors particle effects game feel "follow through" explosions`
11. `Godot 2D bullet holes decal Viewport accumulation buffer draw texture approach reddit`
12. `2D pixel art game lighting mood reactive environment devlog "light beam" "dust motes" combat` *(no results)*
13. `VFX animation principles anticipation follow-through "secondary motion" game effects article`
14. `Hades Supergiant VFX effects particles talk technical breakdown`
15. `gamedeveloper.com lighting atmosphere 2D games mood article`
16. `"Elemental Magic" Joseph Gilland effects animation principles follow through mass energy`

---

## TL;DR — Top implementable techniques ranked by impact / effort

For a Godot 4.3 game where all visuals are generated from code (no new art). Effort assumes a competent C#/Godot dev with shader basics.

| # | Technique | Impact | Effort | Godot hook | Key source (tier) |
|---|---|---|---|---|---|
| 1 | **One-shot GPU particle bursts per destroyed prop** — nested "chunks + dust + smoke" one-shots, `explosiveness≈1`, trail on chunks for the dust streak | High | Low | `GPUParticles2D` (`one_shot`, `explosiveness`, `trail_enabled`, `sub_emitter`) | Godot docs (T1); Vlambeer article (T3) |
| 2 | **Additive flash ring / flash sprite** on every explosion — scaled outward, alpha out in ~0.15 s | High | Low | `Sprite2D`/`Polygon2D` + `CanvasItemMaterial` `BLEND_MODE_ADD` (or radial `GradientTexture2D`) | Godot docs "additive sprites" section (T1); NT technique (T3) |
| 3 | **Procedural dissolve/burn shader** to "consume" the destroyed prop, swept in the blast direction, ember/glow edge | High | Low–Med | `canvas_item` shader with in-shader fbm noise (no texture needed) | godotshaders: DimensionAlmios (T3); mreliptik (T3) |
| 4 | **Scorch decal floor layer** — dark blobs under explosions, capped and slowly fading = "battlefield memory" | High | Med | `CanvasItemMaterial` `BLEND_MODE_MUL` sprites, or one accumulated `Image`→`ImageTexture`; `CanvasGroup` to kill alpha stacking | Godot docs: CanvasGroup / CanvasItemMaterial (T1); NT update (T3) |
| 5 | **Light energy reacts to combat** — `PointLight2D.energy` driven by noise + short spikes when things explode/fire near it | Med–High | Low | `PointLight2D.energy` at runtime; `CanvasModulate` for base darkness | Godot docs 2D lights (T1) |
| 6 | **Fake god-ray shafts** — additive quad with UV-stretched gradient + scrolling procedural stripes, very low alpha | Med | Low–Med | `canvas_item` shader on a stretched sprite/ColorRect, `TIME`, additive blend | Cyanilux breakdown (T3) |
| 7 | **Threat vignette / warm color grade shift** — full-screen shader that intensifies with combat intensity | Med | Low | `CanvasLayer` + full-rect `ColorRect` shader | Tails of Iron case study (T3) |
| 8 | **Persistent micro-debris**: shell casings + a few "linger" chunks that stay on the floor for seconds | Med | Low | long-lifetime particles / small pooled `Sprite2D`s that fade after settle | "Art of Screenshake" list (T3) |

Everything above is achievable with generated textures (`GradientTexture2D`, `NoiseTexture2D`+`FastNoiseLite`, `Image`→`ImageTexture`) or pure shader math.

---

## 1. Sprite-based debris / fragmentation illusions

### 1.1 How the reference games actually do it

**Nuclear Throne — the best-documented destruction recipe.** Vlambeer's Jan Willem Nijman wrote a full public breakdown of the game's explosions (Tier 3, developer-authored):
Source: <https://ctrl500.com/game-design/explosions-in-vlambeers-nuclear-throne/> (original 403s for crawlers; archived copy: <https://web.archive.org/web/20240321225311/https://ctrl500.com/game-design/explosions-in-vlambeers-nuclear-throne/>)

- The common explosion is a **9-frame animation** (art by Paul Veer) whose readability leans on **black-and-white circle frames "rapidly creating contrast"** — high contrast frames read as energy.
- On spawn it does: **small screen shake + a ring of dust + smoke**. Dust is short-lived and "used to communicate movement"; smoke **lingers longer and is darker** because it comes from fiery things.
- **Destroyed walls spawn debris that flies and creates a nice dust trail.** (This is exactly the "chunk + trail" illusion: the trail is what sells the piece as *travelling through space*.)
- Detonating terrain or enemies **freezes the entire game for a couple of milliseconds** and adds extra screen shake — tiny hitstop = perceived power.
- Two technical cheats: the **explosion collision mask is larger than the actual sprite** ("increasing perceived power"), and **collision is active only on the second of the 9 frames** — mechanics collapse into one snappy moment and chained explosive enemies detonate in a readable cascade.
- Explosions **push each other**, so big blasts are several explosions stacked — plus smaller "tiny" explosions with slightly different timing for variety.
- Update 52 added **craters and tiny flames that disappear over time**. His stated reason is the "permanence of battles": *"it really makes sure players know about the action that went down, even after it's long gone"* — and it "made the game look better in screenshots."

**Enter the Gungeon — a per-weapon VFX taxonomy with "lingering" marks.** The community wiki (Tier 4) documents each weapon's effect slots: muzzle flash, impact-midair, impact-enemy, death-enemy, **tilemap impact** and **tilemap death** impact (separate horizontal/vertical variants for walls). Two patterns matter:
Source: <https://enterthegungeon.wiki.gg/wiki/Weapon_Visual_Effects>

- Some impacts **"linger"** (boolean column in the wiki's tilemap tables) — i.e. impact marks that stay on the floor/wall after the shot.
- Several effects **"drop as debris"** when the projectile dies, and at least one ("Barrel") is noted: **"Debris leaves water when landing"** — debris as both visual and a tiny ground state change. This is the "pre-broken variant → settled chunk" pattern in the wild.

**Hades — stylized layered sprite effects.** Supergiant shared a behind-the-scenes look centered on VFX/UI artist Josh Barnett's process for Olympian weapon effects (Tier 3): <https://80.lv/articles/a-behind-the-scenes-look-at-the-effects-in-hades>. A community breakdown (Tier 4) describes Hades effects as **pre-rendered to 2D sprites** on Supergiant's custom MonoGame-based engine, with effects built from separate layers/emitters (e.g. a "dripping" particle pass, a circular flame pass, a radial spark pass): <https://www.reddit.com/r/gamedev/comments/j7ve56/breakdown_of_the_effects_in_hades/> and <https://realtimevfx.com/t/hades-material-vfx-help/25344>. Takeaway: **good destruction FX is composed of 2–4 simple passes** (core flash, chunks, smoke, sparks), not one magic particle.

**Dead Cells:** no specific destructible-prop/VFX technical breakdown surfaced in this research pass. The commonly cited GDC 2019 talk by Sébastien Bénard is about difficulty/cheating, not VFX. Treat "how Dead Cells does it" as **unverified** here.

### 1.2 The illusion, decomposed (how to fake chunk explosions from a sprite)

The reference recipe is a 4-beat structure that any code base can reproduce:

1. **Contact/anticipation beat (~30–80 ms suggested):** on the frame of destruction, a bright shape (flash circle) at the hit point. NT's contrast circles live here; the "Art of Screenshake" talk's rule #6 is literally *"make the first bullet frame a circle"* (Tier 3, recap at <http://artificials.ch/juice-up-your-game/>, talk: <https://www.youtube.com/watch?v=AJdEqssNZU>).
2. **Burst beat:** N chunk sprites spawn at the prop centre with velocities in a random cone outward, spin, gravity. If the chunks **also trail dust**, the eye reads them as heavy fragments rather than sprites (NT rule).
3. **Settle beat:** chunks lose velocity (damping), and on ground contact you either kill them or convert a few into **static "linger" chunks** — the "chunk stays on ground briefly then fades" beat. NT ran this idea at the terrain level (craters + flames that fade); Gungeon at the projectile level ("lingers" impacts); Screenshake #29 recommends dust after explosions that *fades slowly* (Tier 3).
4. **Memory beat:** drop a floor decal (scorch/crater) — see section 2.

Two production choices for the chunk art itself:

- **Pre-broken variants** (classic): a few authored chunk sprites per prop. With a code-generated constraint, you can emulate this without new art by **cropping the prop's own texture into pieces** (e.g. 2×2 or irregular regions via `AtlasTexture` / `Sprite2D.region_enabled`) and giving each crop the prop's palette. Crops of the actual sprite preserve visual continuity ("that *was* the barrel").
- **Procedural scatter:** generate small triangles/quads in a neutral debris colour. Cheaper, reads less "authentic" but is 100 % code. Best as a supplement (dust "grit" particles) under a smaller number of crop chunks.

An extra cheap trick for props that should linger as wreckage instead of vanishing: reuse the **same sprite** with `modulate` darkened + the dissolve shader partially applied (see section 3) + a random small rotation — the silhouette stays, suggesting "broken" without any new asset.

### 1.3 Godot 4.3 mapping (Tier 1 docs)

Sources: <https://docs.godotengine.org/en/stable/tutorials/2d/particle_systems_2d.html>, <https://docs.godotengine.org/en/stable/classes/class_gpuparticles2d.html>

- `GPUParticles2D` supports everything the recipe needs:
  - `one_shot=true` + `explosiveness` **1.0 emits all particles simultaneously** (docs say mid/full values are "useful for creating explosions or sudden bursts").
  - `trail_enabled` (+ `trail_lifetime`, default `0.3`) gives the **dust trail behind flying chunks** — directly maps to NT's "debris flies and creates a nice dust trail".
  - `sub_emitter` (a `NodePath` to another `GPUParticles2D`): the docs explicitly cite "fireworks, sparks on collision, bubbles popping into water drops" as sub-emitter use cases. Note the docs caveat: **when a sub-emitter target is set, that target node no longer emits on its own**.
  - One-shot lifecycle: use the `finished` signal and call `restart()` rather than flipping `emitting` (GPU-side race documented in the class ref).
- Suggested (synthesis) particle tuning:
  - chunks: `amount 6–20`, `lifetime 0.5–0.9 s`, `explosiveness 0.9–1.0`, gravity `900–1400 px/s²`, initial velocity `120–350 px/s`, spread `~180°`, angular velocity `±300–900 °/s`, damping `0.4–1.0`.
  - dust ring: `amount 8–16`, `lifetime 0.3–0.6 s`, velocity `40–120 px/s`, no gravity influence or weak negative vertical.
  - smoke: `lifetime 1.2–2.5 s`, slow rise, `modulate` alpha fade over lifetime, dark grey; smoke must **outlive** the flash (follow-through, section 6).
- Generated textures: `GPUParticles2D.texture = null` yields **1×1 px squares** (Tier 1) — instead generate small `Image`→`ImageTexture` squares/circles at runtime, or use a radial `GradientTexture2D` for soft smoke puffs. No art assets needed.
- Perf note (Tier 1): `GPUParticles2D.emit_particle()` is **only supported on Forward+/Mobile**, not Compatibility — don't rely on it for custom debris; use regular emission + prefabs/pooling.

---

## 2. Scorch / decal accumulation as "battlefield memory"

### 2.1 The pattern and its evidence

The strongest published statement of *why* permanent marks matter is again Nuclear Throne's update 52 (Tier 3): craters + tiny flames make players remember what happened — "even after it's long gone." The Screenshake appendix makes permanence a running theme across the list (Tier 3): **#12 keep dead bodies, #21 keep ejecting shells, #29 dust that fades slowly**. Gungeon's "lingering" impact flags (Tier 4) show the same design choice at weapon granularity.

For an arena shooter this maps cleanly: scorch decals under cover that was destroyed, so a later glance at the floor tells the player *this corner was a kill zone 40 seconds ago*.

### 2.2 Known issues and their fixes

**A. Alpha stacking.** Overlapping translucent decals get progressively more opaque where they overlap — a scorch pile-up looks like an accidental black hole. Godot documents the problem and the fix directly:

> **CanvasGroup** — "Child CanvasItem nodes of a CanvasGroup are drawn as a single object. It allows to e.g. draw overlapping translucent 2D nodes without causing the overlapping sections to be more opaque than intended (set the CanvasItem.self_modulate property on the CanvasGroup to achieve this effect)."
> Tier 1: <https://docs.godotengine.org/en/stable/classes/class_canvasgroup.html>

Uses a backbuffer pass (cost), with `fit_margin`/`clear_margin` (defaults 10) to control the covered rect; **incompatible inside with `clip_children`** (docs note). One CanvasGroup per decal *layer* (not per decal) is the right granularity.

**B. Sort order ("2D z-fighting").** There is no depth fight in 2D — the equivalent failure is *ordering*: decals that land above actors, or get re-sorted by Y-sorting, will look wrong. Practical rules (synthesis): keep all decals inside one non-Y-sorted container with a **constant `z_index` band below actors** (e.g. `-5`), never parent decals into Y-sorted groups, and never mix `z_index` and Y-sort for the same content (Godot reorders children of a Y-sorted parent by Y position).

**C. Overdraw / performance.** Every decal is a full transparent quad. Godot's 2D-lights page notes that even additive *sprites* are cheaper than real lights for short-lived effects (Tier 1), but thousands of decals still cost fill-rate. Practical caps (synthesis): pool `64–128` decals, evict oldest with a short fade, keep scorch blobs small (0.8–1.3× explosion radius). If you want unlimited persistence, use the accumulation-buffer approach below.

**D. Accumulation buffer (the "surface" approach, best long-term).** Instead of many overlapping sprites, own a single `Image` (e.g. 1024×1024) for the floor-battle layer and *paint* each scorch into it: `Image.blend_rect` a generated radial-blob, but with **per-pixel darkening clamped by `max()`** so repeated hits asymptote to a chosen max (e.g. 60 % darkening) instead of stacking forever, then `ImageTexture.update()`. This is the classic GameMaker "surface" pattern (Tier 4 pointer: <https://godotforums.org/d/20372-help-with-persistent-draws> — page was unavailable at capture time, but the pattern is engine-agnostic). Benefits: zero alpha stacking, zero sort issues (one sprite), bounded fill cost; cost: CPU pixel work + texture size. For an arena, a downscaled buffer (e.g. 512×512 for a 4×4-screen arena) painted at low res is the sweet spot (synthesis).

**E. Blending choice.** Multiply-style darkening is available via `CanvasItemMaterial.blend_mode = BLEND_MODE_MUL` (enum value 3; also `ADD`, `SUB`, `PREMULT_ALPHA`) — Tier 1: <https://docs.godotengine.org/en/stable/classes/class_canvasitemmaterial.html>. Multiply is the correct mode for grill marks/scorch (it darkens what's under it and never gets a grey box halo). Note for API users: the GDScript/C# name is `CanvasItemMaterial.BlendModeEnum.Mul` in C#. Caveat (synthesis): multiplying stacks *multiplicatively* too — three 50 % decals ≈ 87 % darkening — so still cap via buffer/max, or fade earlier.

**F. Fade policy (synthesis).** Two sensible modes: (i) competitive clarity — cap ~8–16 s fade for recent scorch, or (ii) arena "chapters" — keep scorch for the whole wave, clear (fade 2–3 s) at wave transition. NT's flames "disappear over time" supports (i) as the default for readability.

---

## 3. Dissolve / burn sprite shader illusion

### 3.1 The classic noise-threshold burn (with glow edge)

**Source: "2D dissolve with burn edge" by mreliptik (godotshaders.com, 2024). CC0.** Tier 3. <https://godotshaders.com/shader/2d-dissolve-with-burn-edge/>

Technique: a `NoiseTexture2D` is sampled in the sprite's UV; the **dissolve value vs noise value** produces the cut, and a **narrow band between two smoothsteps** is recoloured to a burn colour. Full shader (short enough to archive here; CC0):

```glsl
shader_type canvas_item;

uniform sampler2D dissolve_texture : source_color;
uniform float dissolve_value : hint_range(0,1);
uniform float burn_size: hint_range(0.0, 1.0, 0.01);
uniform vec4 burn_color: source_color;

void fragment(){
    vec4 main_texture = texture(TEXTURE, UV);
    vec4 noise_texture = texture(dissolve_texture, UV);

    // avoids a leftover burn_colour dot at dissolve 0/1
    float burn_size_step = burn_size * step(0.001, dissolve_value) * step(dissolve_value, 0.999);
    float threshold = smoothstep(noise_texture.x-burn_size_step, noise_texture.x, dissolve_value);
    float border = smoothstep(noise_texture.x, noise_texture.x + burn_size_step, dissolve_value);

    COLOR.a *= threshold;
    COLOR.rgb = mix(burn_color.rgb, main_texture.rgb, border);
}
```

Author's setup notes (verbatim numbers): `NoiseTexture2D` width/height should match the sprite (too small = blurry); uses **Simplex Smooth** noise; **frequency** changes the dissolve look; **burn size `0.04` "works well usually"**; `dissolve_value` runs 0 (dissolved) → 1 (intact). A commenter (Microbe) notes the 0.001/0.999 endpoint hack can be flipped into `burn_size * (1.0-step(dissolve_value,0.0)) * (1.0-step(1.0, dissolve_value))`.

### 3.2 Fully procedural variant — no noise texture at all (best for code-only constraint)

**Source: "Dissolve with glowing burn edge" by DimensionAlmios (godotshaders.com, Sep 2026). MIT.** Tier 3. <https://godotshaders.com/shader/dissolve-with-glowing-burn-edge/>

This one **needs no texture asset**: it synthesizes fbm value-noise in the fragment shader (hash + quintic interpolation + rotated octaves to hide the grid) and domain-warps it for curly, organic burn lines. Key implementable details:

- `dissolve` 0→1 drives a threshold; AA via `fwidth`. Params: `noise_scale` (default **22**), `warp` (default **0.6**), `edge_width` (default **0.10**), `sweep`+`sweep_angle` for **directional burns** (great for "burned away from the blast point/side"), `edge_color` (ember orange default) and `edge_hot` (white-hot inner rim): *"the hot rim follows the burn line and cools to ember toward the untouched pixels."*
- Includes a spread remap `n = 0.5 + 0.5*tanh(z*0.85)` so the fbm's middle-heavy distribution doesn't make the burn pop late — a genuinely useful trick when thresholding procedural noise.
- It saves/applies vertex tint so `modulate` still works; author says to animate `dissolve` with a **Tween**; works on the Compatibility renderer.

Related community shaders worth having open while tuning (all godotshaders.com, Tier 3): "2D Burn/dissolve with direction (V 1.0)" and "…from point (V 1.0)" (enekoassets) — <https://godotshaders.com/shader/2d-burn-dissolve-with-direction-v-1-0/>, <https://godotshaders.com/shader/2d-burn-dissolve-from-point-v-1-0/>; "Fire dissolve shader" (NoiseTexture2D-based burning look) — <https://godotshaders.com/shader/fire-dissolve-shader/>.

### 3.3 Making the dissolve *feel* like destruction (synthesis)

- Fire the dissolve **at the same moment the debris particles spawn** (dissolve 0→1 over 0.25–0.5 s, debris lifetime ~0.6 s) so the solid prop "turns into" the pieces. If the prop simply vanishes on frame 1 the debris reads as unrelated confetti.
- Set `sweep_angle` to point **away from the blast** (direction of the impact, not random) — destruction has a source.
- "Glow from the dissolve edge": simplest robust trick without HDR pipeline fiddling is to make `edge_hot` colour values > 1.0 **and** put a brief additive particle burst / additive sliver sprite on the burn line at the start; a WorldEnvironment + HDR-2D glow setup can bloom it globally but is a heavier, less certain dependency (community practice; treat as optional).
- For cover that collapses rather than vanishes: dissolve to ~0.6–0.75 (not 1.0), darken `modulate`, and leave the husk as "wreckage" for the rest of the wave — silhouette continuity + battlefield memory in one.

---

## 4. Fake volumetric light shafts / god rays in 2D

### 4.1 The technique

**Source: Cyanilux, "Sun Beams / God Rays Shader Breakdown" (May 2024, updated Aug 2025).** Tier 3 (known shader educator; Unity Shader Graph implementation, technique is engine-agnostic). <https://www.cyanilux.com/tutorials/god-rays-shader-breakdown/>

Cyanilux surveyed the options and deliberately picked **billboarded quads stretched toward the light** for a top-down view (the post-process radial-blur method "only works when looking towards the light source", i.e. useless top-down; raymarching is expensive). The decomposition:

- **Stretch:** displace only the **top vertices** by multiplying position by the UV's Y component, along the light direction, by a length factor (he hardcodes **×25**).
- **Stripe variation:** a striped texture *or* procedural **gradient noise with `Tiling` set to stretch vertically (Y tiling = 0)**, offset per quad (by world position, or per-particle random) so rays don't repeat; scroll the offset with **Time × speed** for a slow shimmer.
- **Edge fades:** `smoothstep` on `abs(UV.x − 0.5)` fades both sides of the quad; same on Y fades the ends.
- **Dust motes inside the shaft:** sample a **Voronoi/Worley** texture **twice** with different scroll offsets, **multiply the two samples**, then `smoothstep(0.9, 1.0, …)` so only occasional overlap dots appear, **× ~200 brightness** so bloom catches them; a second, cheaper smoothstep adds ambient dust. All animated by scrolling the UV offset.
- **Blending:** Unlit / Transparent, **Alpha or Additive** both work; keep values low and let the stripes + dust carry the "volume".
- Depth-based fades were for 3D scene geometry; in 2D you emulate with the X/Y edge fades + sorting behind gameplay elements.

### 4.2 Godot 4.3 mapping (synthesis + Tier 1 hooks)

- One `canvas_item` shader on a stretched `Sprite2D` (or a `ColorRect` sized to the shaft) does the whole thing: use `TIME` for scroll (canvas_item shaders have `TIME`, `UV`, `SCREEN_UV`), fbm/hash stripes over UV.x, `smoothstep` edge fades, and a length mask over UV.y that fades as it leaves the source.
- Blend: `CanvasItemMaterial.blend_mode = BlendModeEnum.Add` on the node (Tier 1) — additive is the cheap, convincing choice; alternatively `mix` with low alpha (0.04–0.15 suggested) is softer and won't blow out on bright floors.
- Cheapest convincing set (synthesis): **1–3 shafts max**, alpha `0.04–0.15`, stripe count `5–9` across the shaft width, scroll `0.02–0.08 UV/s`, length `2–4×` the source light radius, slight per-shaft phase offset, fade both length ends; pair with a sparse additive dust `GPUParticles2D` (life 4–8 s, drift 5–20 px/s, alpha 0.1–0.3) confined to the shaft rect.
- Cross-reference (Tier 3): Odd Bug Studio's *Tails of Iron* case study uses a Unity "Sprite Light" — a sprite projected as the light source — for "light beams that have a textured light source … placed and rotated to show light coming from different angles" (<https://unity.com/how-to/use-2d-lights-unity-set-mood>). Same illusion: **beams are shaped sprites, not physics**.
- In Godot, real `PointLight2D`s with light-shaped textures can also project onto sprites (Tier 1 docs: light textures, blend modes, Z/Layer ranges), but for pure "shaft" looks the additive quad is cheaper and can't be occluded weirdly.

---

## 5. Reactive-environment illusion patterns

### 5.1 Lights flicker / spike with combat (Tier 1 base, synthesis for the pattern)

Godot's 2D lighting docs (Tier 1: <https://docs.godotengine.org/en/stable/tutorials/2d/2d_lights_and_shadows.html>) give the exact levers:

- `PointLight2D.energy` = "the light's intensity multiplier". Drive it from a noise: `energy = base * (1 + 0.15 * noise(t))` for ambient flicker; **spike ×2–3 for 60–120 ms** when an explosion happens within radius (synthesis numbers).
- `CanvasModulate` sets the base "ambient" colour of everything not lit — the correct node to dim the arena as threat rises (e.g. lerp ambient towards a cold blue-grey as the wave intensifies).
- `Blend Mode` `Add` (default) vs `Subtract` vs `Mix`, plus `Range > Z Min/Z Max` and `Layer Min/Max` — a "explosion light" can be restricted to the cover/decor layer so it doesn't wash out UI or the floor.
- The docs also endorse the cheap trick directly: **"Using additive sprites as a faster alternative to 2D lights" — "particularly suited for short-lived dynamic effects, such as bullets or explosions."** Downside stated in the docs: additive sprites "cannot correctly light up areas that are fully dark" and can't cast shadows — so keep `CanvasModulate` above pure black.
- NT's own "cheap fake bloom effects" (mentioned in the explosion article, not detailed) are the same family: additive brightness instead of a real bloom pipeline.

### 5.2 Explosion flash ring (the propagation illusion)

- A radial gradient sprite (code: `GradientTexture2D` with radial fill, or a 10-line canvas shader drawing a ring: `smoothstep(r_in, r_in+e, d) - smoothstep(r_out-e, r_out, d)`) that scales from ~0.6× to 2.5–3× explosion radius over **0.12–0.25 s** while alpha fades 0.5→0 (synthesis numbers). Additive blend. This is the standard "shockwave ring" and fakes *light propagating outward*.
- Turkish-window / silhouette flash: any window or doorway sprite that also flashes is read as *light reaching it*. Cheap implementation (synthesis): small `PointLight2D` behind the window sprite; on explosion, pulse `energy` after a short delay proportional to distance from blast (staged 0.03–0.08 s per distance band) — the stagger is what makes it read as propagation rather than a global effect.

### 5.3 Vignette / colour grading with threat level

- Pattern (Tier 3 evidence): *Tails of Iron* "ties the scene together with colour processing and a vignette", drawn per scene area, and swaps whole folders of lights/assets/filters for day/night states (Unity article above). For an arena: one full-screen `ColorRect` shader on a `CanvasLayer`, driven by a `threat` float — vignette tightness + a warm tint mix as threat rises; slow lerp (1–3 s) so it reads as mood, not a flash (synthesis).
- Academic pointer (Tier 3, further reading — abstract-level only, not fetched in full): "How lighting affects players in 2D video games" (J. Wu, Northeastern, 2022) cites Knez & Niedenthal on warm (reddish) vs cool (bluish) lighting affecting player performance/mood: <https://repository.library.northeastern.edu/files/neu:4f18c642/fulltext.pdf>. Treat as directional colour advice, not gospel.

---

## 6. Why these illusions work — animation principles applied to VFX

**Primary source (Tier 3): Jonathan Cooper, "The 12 principles of animation in video games" — an excerpt from *Game Anim: Video Game Animation Explained*, published by Game Developer (2019).** <https://www.gamedeveloper.com/production/the-12-principles-of-animation-in-video-games>

The principles that carry straight over to destruction/VFX (quotes paraphrased from the article):

- **Follow-through & overlapping action.** "Follow-through … describes what takes place after an action (the inverse of anticipation)… a great way to sell the weight of an object"; different parts move at different rates ("drag"). For destruction: the flash is the action; **debris, dust, smoke, and embers are the follow-through** — they must continue after the flash has died for the hit to have mass. The article's own example: a cannonball's weight is sold by animating follow-through *on the cannon that fired it*.
- **Timing = weight.** "time = distance/speed, therefore, speed = distance/time" — the *speed* of the debris arc is the weight read. Fast in, slow out: game actions typically "fast in" (responsiveness) and the **weight is added at the end in the follow-through** — exactly why chunks should decelerate and settle rather than stop dead.
- **Anticipation** "occurs … to sell the energy transfer of an action in a way the action alone cannot", but the article flags the gameplay conflict: too much anticipation "removes agency". For player-fired destruction, keep anticipation tiny (a muzzle/impact pre-flash of a frame or two); for **telegraphed enemy attacks, longer anticipation is desirable**. (NT's delayed-collision-frame trick is a correlated design pattern from the other direction: delay the *mechanic* one frame so everything snaps at once.)
- **Exaggeration.** "Real life never looks real enough… create the hyper-real" — bigger flashes, longer-held impact poses, dramatic knockback. NT's "collision mask larger than the sprite" is literal mechanical exaggeration for perceived power.
- **Secondary action** "complement[s] and emphasize[s] the primary action" — sparks, smoke wisps, screen kick: they must support, not muddy.

**Supporting source (Tier 3): Joseph Gilland, *Elemental Magic: The Technique of Special Effects Animation*** — the classic treatment of effects as their own animation discipline (energy, mass, distortion, follow-through for fire/smoke/water). Book reference; a free academic summary of the same principles is the bachelor's thesis "Mastering the Elements – Basics of 2D Effect Animation" (Theseus): <https://www.theseus.fi/bitstreams/26ee8eef-ebeb-47db-b311-eda3fc54e426/download>. Also referenced by modern research on animating dynamics from principles (e.g. PhysAnimator, SIGGRAPH-adjacent work) — effects animation principles are still the standard vocabulary.

**Developer voice (Tier 3):** Nijman's framing across both sources is the simplest statement of the "why": *"Just fill your game with love and tiny details"* (Art of Screenshake) and, on permanence, players should know "about the action that went down, even after it's long gone" (NT explosion article).

### A practical VFX checklist derived from the above (synthesis)

1. **Anticipation (short):** pre-flash/glow 30–80 ms, or a 1–2 frame contact pose.
2. **Contact:** hitstop 20–80 ms (screenshake's ~20 ms "sleep"; NT's couple-ms freeze for terrain), screen shake, high-contrast flash shape (NT's black/white circles).
3. **Follow-through:** debris with trails, smoke that outlives the flash, shells, ejecta — the "world still breathes" after the action.
4. **Overlap/drag:** dust trail lags the chunk; smoke lags dust; light flicker lags the flash; stagger window pulses by distance.
5. **Weight:** gravity on chunks; heavy settles fast, light flutters; whoever moves slow = heavy.
6. **Memory:** scorch decals, lingering chunks, shells; capped-count and slow fades keep it readable.

---

## 7. Godot 4.3-specific caveats collected along the way (all Tier 1)

- **2D particle physics interpolation is not supported in 4.3** — the docs recommend disabling `Node > Physics Interpolation` on particle nodes (or accepting the doc's workaround) to avoid jitter. (<https://docs.godotengine.org/en/stable/tutorials/2d/particle_systems_2d.html>)
- `GPUParticles2D.emit_particle()` is **Forward+/Mobile only** — not Compatibility. (class ref, above)
- One-shot restart: prefer `restart()` over toggling `emitting`; there's a documented GPU-side race window. (`finished` signal only fires for one-shot emitters.)
- Sub-emitters: setting `sub_emitter` disables the target node's own emission — keep sub-emitters as nested nodes you don't emit from directly.
- `CanvasGroup` reads the backbuffer and **does not work correctly with children that use `clip_children`**; it also has `fit_margin`/`clear_margin` to control the buffer rect.
- Pixel-art lighting: Godot computes 2D lighting/shadows at **viewport resolution**, not texture resolution — `Nearest` filtering does **not** pixelate lights/shadows. To get blocky pixel lighting you must snap `LIGHT_VERTEX`/`SHADOW_VERTEX` in a shader (`floor(v / pixel_size) * pixel_size`) — code sample in the docs.
- Normal maps: if your generated sprites are flat, lighting looks flat; Laigter (free, linked from docs) generates normal/specular maps — but this needs source textures, so lower priority under the no-new-art constraint.
- Godot's 3D `Decal` node is documented as "temporary or persistent gameplay effects such as bullet impacts and explosion scorches" (<https://docs.godotengine.org/en/stable/tutorials/3d/using_decals.html>) — there is no 2D Decal node, so the 2D scorch pattern is sprites/CanvasGroup/Image-buffer (section 2). A community proposal to add decal blend modes exists (Tier 2, open/not accepted: <https://github.com/godotengine/godot-proposals/issues/13883>) — do not plan on it.

---

## 8. Gaps / unverified items (explicitly flagged)

- **Dead Cells:** no VFX/destruction technical breakdown found in this pass; do not cite it as a technique source without more digging.
- **Hades pipeline details** (pre-rendered sprites, After Effects/Trapcode authoring) come from community threads (Tier 4), directionally plausible but not official statements.
- **Gungeon "linger" semantics:** wiki table columns (Tier 4) indicate which tilemap impacts persist; exact engine behaviour (timers vs permanent) is not documented there.
- **Godot forum "persistent draws" thread** (<https://godotforums.org/d/20372-help-with-persistent-draws>) was unavailable at fetch time (JS page); the surface/accumulation pattern is standard and stands on the CanvasGroup/Image approach in section 2.
- **Further viewing (titles only, not vetted):** GDC 2018 VFX Bootcamp "The Elementary Particles" (Mickaelle Ruckert, Arkane) — <https://www.youtube.com/watch?v=o7wFPkGmWdI>; "Can We Do It with Particles?: VFX Learnings from 'Returnal'" (Housemarque) — <https://www.youtube.com/watch?v=qbkb8ap7vts>; Jan Willem Nijman "The Art of Screenshake" (INDIGO 2013) — <https://www.youtube.com/watch?v=AJdEqssNZU>; a written recap of the 30 tricks: <http://artificials.ch/juice-up-your-game/> and <https://www.bloodirony.com/blog/the-art-of-screenshake-in-unity2d>.

---

## Source list (quick reference)

| # | Source | Tier | URL |
|---|---|---|---|
| 1 | Godot docs — 2D lights and shadows (energy, blend modes, additive sprites alternative, pixel-snap shader) | 1 | https://docs.godotengine.org/en/stable/tutorials/2d/2d_lights_and_shadows.html |
| 2 | Godot docs — 2D particle systems (explosiveness, one-shot, trails, flipbooks, 4.3 interpolation note) | 1 | https://docs.godotengine.org/en/stable/tutorials/2d/particle_systems_2d.html |
| 3 | Godot class ref — GPUParticles2D (sub_emitter, trails, finished/restart, emit_particle limits) | 1 | https://docs.godotengine.org/en/stable/classes/class_gpuparticles2d.html |
| 4 | Godot class ref — CanvasGroup (alpha-stacking fix) | 1 | https://docs.godotengine.org/en/stable/classes/class_canvasgroup.html |
| 5 | Godot class ref — CanvasItemMaterial (blend modes MIX/ADD/SUB/MUL/PREMULT) | 1 | https://docs.godotengine.org/en/stable/classes/class_canvasitemmaterial.html |
| 6 | Godot docs — Using decals (3D; "bullet impacts and explosion scorches" framing) | 1 | https://docs.godotengine.org/en/stable/tutorials/3d/using_decals.html |
| 7 | Godot proposal #13883 — decal blend modes (open, not implemented) | 2 | https://github.com/godotengine/godot-proposals/issues/13883 |
| 8 | Vlambeer (Jan Willem Nijman) — "Explosions in Vlambeer's Nuclear Throne" (archived) | 3 | https://web.archive.org/web/20240321225311/https://ctrl500.com/game-design/explosions-in-vlambeers-nuclear-throne/ |
| 9 | "Art of Screenshake" 30-tricks recap (artificials.ch) + talk | 3 | http://artificials.ch/juice-up-your-game/ ; https://www.youtube.com/watch?v=AJdEqssNZU |
| 10 | godotshaders — "2D dissolve with burn edge" (mreliptik, CC0, full code included) | 3 | https://godotshaders.com/shader/2d-dissolve-with-burn-edge/ |
| 11 | godotshaders — "Dissolve with glowing burn edge" (DimensionAlmios, MIT, procedural fbm) | 3 | https://godotshaders.com/shader/dissolve-with-glowing-burn-edge/ |
| 12 | Cyanilux — Sun Beams / God Rays shader breakdown | 3 | https://www.cyanilux.com/tutorials/god-rays-shader-breakdown/ |
| 13 | Game Developer / Jonathan Cooper — "The 12 principles of animation in video games" (Game Anim excerpt) | 3 | https://www.gamedeveloper.com/production/the-12-principles-of-animation-in-video-games |
| 14 | Unity — "How to use 2D lights to set the mood" (Tails of Iron / Odd Bug Studio case study) | 3 | https://unity.com/how-to/use-2d-lights-unity-set-mood |
| 15 | 80.lv — Behind-the-scenes of Hades effects (Josh Barnett) | 3 | https://80.lv/articles/a-behind-the-scenes-look-at-the-effects-in-hades |
| 16 | Joseph Gilland — *Elemental Magic* (book) + Theseus summary thesis | 3 | https://www.theseus.fi/bitstreams/26ee8eef-ebeb-47db-b311-eda3fc54e426/download |
| 17 | Enter the Gungeon wiki — Weapon Visual Effects (debris, "lingers" flags) | 4 | https://enterthegungeon.wiki.gg/wiki/Weapon_Visual_Effects |
| 18 | r/gamedev — Hades effects breakdown (community) | 4 | https://www.reddit.com/r/gamedev/comments/j7ve56/breakdown_of_the_effects_in_hades/ |
| 19 | godotforums — "Help with persistent draws" (surface pattern pointer; page unavailable at fetch) | 4 | https://godotforums.org/d/20372-help-with-persistent-draws |
| 20 | Northeastern thesis — "How lighting affects players in 2D video games" (further reading) | 3 | https://repository.library.northeastern.edu/files/neu:4f18c642/fulltext.pdf |
