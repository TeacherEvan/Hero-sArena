# Godot 4.3 — 2D Lighting Research for a Dark Top-Down Arena Shooter

- **Date:** 2026-10-17
- **Target:** Godot 4.3 stable, C# / .NET 8 (`Hero-sArena`), Forward+ default renderer
- **Scope:** `CanvasModulate` + `Light2D`/`PointLight2D` workflow, code-generated light textures
  (`GradientTexture2D`), blend modes, performance/pooling gotchas, and Godot 4.3-specific caveats.
- **Related in-repo context:** `scripts/core/LightFXState.cs` (12-slot transient flash pool) and
  `scripts/core/MainBootstrap.cs::EnsureEnvironment` (current `WorldEnvironment` with Glow enabled).

**Source tiers used in this document**
- **Tier 1** — official docs (`docs.godotengine.org/en/4.3`) and official Godot project announcements.
- **Tier 2** — official repository artifacts: `godotengine/godot` issues/PRs, engine source at tag `4.3`,
  `godotengine/godot-demo-projects`.
- **Tier 4** — community posts used only as corroboration; every implementation-relevant claim below is
  backed by Tier 1/Tier 2.

---

## Exact queries and sources used

Web search queries (verbatim):

1. `Godot 4 use_hdr_2d project setting glow 2D lights energy bloom rendering/viewport/hdr_2d`
2. `Godot CanvasModulate does not affect background clear color 2D darken scene`
3. `Godot 4 PointLight2D rotate scale transform does not work light texture rotation`
4. `Godot 4.3 release notes 2D TileMapLayer lighting changes point lights`
5. `Godot 4 PointLight2D node scale not working texture_scale vs scale "light2d" scale issue github`
6. `Godot 4 PointLight2D rotate cone texture spotlight rotate node Godot Q&A`
7. `"CanvasModulate" background "clear color" not darkened godot 4`
8. `Godot C# "CanvasItem.TextureFilterEnum" Linear example`
9. `godot 4 WorldEnvironment background mode ignored 2D scene default clear color`

Direct pages fetched (Tier in parentheses):

- 2D lights and shadows — `https://docs.godotengine.org/en/4.3/tutorials/2d/2d_lights_and_shadows.html` (T1)
- GradientTexture2D — `https://docs.godotengine.org/en/4.3/classes/class_gradienttexture2d.html` (T1)
- CanvasModulate — `https://docs.godotengine.org/en/4.3/classes/class_canvasmodulate.html` (T1)
- Light2D — `https://docs.godotengine.org/en/4.3/classes/class_light2d.html` (T1)
- PointLight2D — `https://docs.godotengine.org/en/4.3/classes/class_pointlight2d.html` (T1)
- Gradient — `https://docs.godotengine.org/en/4.3/classes/class_gradient.html` (T1)
- Environment and post-processing (incl. “Using glow in 2D”) —
  `https://docs.godotengine.org/en/4.3/tutorials/3d/environment_and_post_processing.html#glow` (T1)
- Engine source at tag 4.3: `scene/resources/gradient_texture.cpp`, `scene/resources/gradient.cpp`
  (T2, canonical implementation)
- `godotengine/godot` issues #81147, #76266, #63666, #90905, #52467; PRs #90920, #75238 (T2)
- Official demos `godot-demo-projects/2d/glow` (branches `4.3` and `master`): `beach_cave.tscn`,
  `project.godot`, `README.md` (T2)
- Godot 4.2 dev snapshot blog (HDR 2D announcement) — `https://godotengine.org/article/dev-snapshot-godot-4-2-dev-3` (T1)
- Godot 4.3 release notes / dev snapshot 4.3 dev 6 (light early-exit perf change) —
  `https://godotengine.org/releases/4.3/index.html`, `https://godotengine.org/article/dev-snapshot-godot-4-3-dev-6` (T1)
- C# API ground truth checked locally against the shipped `GodotSharp` 4.3.0 assembly XML docs
  (`~/.nuget/packages/godotsharp/4.3.0/lib/net6.0/GodotSharp.xml`) — every enum/type name in the
  snippets below was verified there (T2, official binary).

---

## 1. CanvasModulate + Light2D workflow

### 1.1 The intended stack (documented order of concerns)

The 4.3 tutorial describes a complete 2D lighting setup as: **CanvasModulate** (darkens the scene) →
**PointLight2D / DirectionalLight2D** (carve light) → **LightOccluder2D** (shadows) → lit CanvasItems.
Quotes ([T1](https://docs.godotengine.org/en/4.3/tutorials/2d/2d_lights_and_shadows.html)):

- “CanvasModulate is used to darken the scene by specifying a color that will act as the base ‘ambient’
  color. This is the final lighting color in areas that are *not* reached by any 2D light.”
- “Without a CanvasModulate node, the final scene would look too bright as 2D lights would only brighten
  the existing unshaded appearance (which appears fully lit).”

So there is no strict “create light first, then modulate” requirement — the practical rule is: **the
CanvasModulate color must already be dark when lights are rendered, otherwise additive lights have
nothing to carve out.** For C# setup order, add the CanvasModulate once when the scene/arena is built,
before (or together with) creating the pooled lights; both live in the gameplay canvas.

### 1.2 Where the node goes / what it affects

CanvasModulate class reference ([T1](https://docs.godotengine.org/en/4.3/classes/class_canvasmodulate.html)):

- “CanvasModulate applies a color tint to **all nodes on a canvas**. Only one can be used to tint a
  canvas, but CanvasLayers can be used to render things independently.”
- It is a `Node2D`, but its tint is **canvas-wide** — not limited to its parent subtree, and its position
  is irrelevant. Parent/sibling placement relative to lights does not matter; what matters is the canvas
  it lands on (default canvas vs. a `CanvasLayer`).
- Because it tints a whole canvas, the standard way to keep a UI/HUD readable is to put the HUD on its own
  `CanvasLayer` — that is also the documented pattern for glow (see §1.4). Confirmed by maintainer
  comment on [issue #63666](https://github.com/godotengine/godot/issues/63666) (T2): “CanvasModulate
  affects an entire CanvasLayer, so you need to use a CanvasLayer node to prevent specific nodes from
  being modulated.”

Caveat from 4.0/4.1 era (already fixed before 4.3): CanvasModulate could update the *wrong* canvas when
the node entered the tree and appear to leak onto other layers/scenes — [issue #63666](https://github.com/godotengine/godot/issues/63666),
fixed by [PR #75238](https://github.com/godotengine/godot/pull/75238) (milestone 4.1, T2). No action
needed on 4.3, but do not be surprised by old forum posts.

### 1.3 The background/clear color is not lit (and is not a canvas node)

Tutorial note ([T1](https://docs.godotengine.org/en/4.3/tutorials/2d/2d_lights_and_shadows.html)):
“The background color does **not** receive any lighting. If you want light to be cast on the background,
you need to add a visual representation for the background, such as a Sprite2D.”

Practical consequences for Hero-sArena:

- The arena floor/backdrop must be an actual CanvasItem (e.g., a `Sprite2D`/`ColorRect`/TileMapLayer) if
  lights should reveal it; a flat viewport clear color stays flat and unlit.
- `CanvasModulate` is defined over “nodes on a canvas”; do not count on it to darken the raw viewport
  backdrop. Keep the backdrop dark by design (the project already sets a dark
  `Environment.BackgroundColor` ≈ `VisualTheme.Background`, `(0.043, 0.051, 0.078)`).

### 1.4 Energy in 4.x and the existing `WorldEnvironment` Glow

Energy ([T1](https://docs.godotengine.org/en/4.3/classes/class_light2d.html)): `energy` is a float,
default `1.0`, described as “The light’s intensity multiplier… The larger the value, the stronger the
light.” In LDR 2D rendering the composed canvas clamps to 1.0 per channel, so values above 1 only matter
when **HDR 2D** is on.

Current project state: `GlowEnabled = true`, `GlowStrength = 0.5f`, no `GlowBloom` (default is `0.0`),
default `GlowHdrThreshold` (`1.0`), and `rendering/viewport/hdr_2d` is not set (default `false`).

4.3 docs, “Glow” ([T1](https://docs.godotengine.org/en/4.3/tutorials/3d/environment_and_post_processing.html#glow)):
“By default, even if the effect is enabled, it will be weak or invisible. One of two conditions need to
happen for it to actually show: (1) The light in a pixel surpasses the **HDR Threshold** … (2) The
**Bloom** property has a value greater than `0.0`.”

In an LDR 2D canvas nothing exceeds 1.0, so **the current glow settings produce effectively no glow**.
The same docs page, “Using glow in 2D”, gives the two supported paths (paraphrased, with quotes):

1. **HDR 2D path (4.2+, Forward+/Mobile only):** “enable HDR for 2D rendering… This also allows you to
   control which objects glow using their individual Modulate or Self Modulate properties (use the RAW
   mode in the color picker).” Enable via `Rendering > Viewport > HDR 2D` and **restart the editor**.
   With this, overbright light output (`energy > 1`, or `GradientTexture2D.UseHdr` textures with
   color > 1) survives to the glow pass and blooms.
2. **No-HDR path:** “Enable glow, set the environment background mode to **Canvas** then decrease
   **Glow HDR Threshold** so that pixels that are not overbright will still glow. To prevent UI elements
   from glowing, make them children of a CanvasLayer node. You can control which layers are affected by
   glow using the Background > Canvas Max Layer property.”

Warning from the same docs: with HDR 2D enabled, the 2D renderer works in **linear color space**, so
`source_color` must be used on canvas_item shader color uniforms (relevant if arena shaders pass colors).

Official demo corroboration (T2): `godot-demo-projects/2d/glow`:
- `4.3` branch `beach_cave.tscn`: `background_mode = 3` (**Canvas**), `glow_enabled = true`,
  `glow_bloom = 0.08`, `glow_strength = 0.88`, `glow_levels/4 = 1.0`, `glow_levels/7 = 1.0`; sprite
  `modulate = Color(2, 2, 2, 1)`; `project.godot` sets a dark `default_clear_color`. (HDR 2D is *not*
  enabled on the 4.3 branch; glow comes from the Canvas background + Bloom.)
- `master` branch adds `viewport/hdr_2d=true`, `renderer/rendering_method="mobile"`, and moves the Label
  into a `CanvasLayer`.
- `README.md`: “The Label is on a separate CanvasLayer, so that it is not affected by glow.”

Glow + blend mode interaction (synthesis of Tier 1 facts): glow is a post-process over the final
composition, compared against `GlowHdrThreshold`. **Add** lights are the ones that can push pixels over
the threshold (bloom source); **Sub** lights only darken and can never bloom; **Mix** interpolates and
depends entirely on the light texture. There is no per-light “glow” flag — control comes from energy,
threshold, bloom, and Canvas Max Layer / CanvasLayer separation.

Renderer caveat ([T1](https://docs.godotengine.org/en/4.3/tutorials/3d/environment_and_post_processing.html#glow)):
on the Compatibility renderer, glow uses a different implementation and **Levels, Normalized, Strength,
Blend Mode, Mix, Map, Map Strength are unavailable**.

---

## 2. Creating Light2D textures purely from code

### 2.1 GradientTexture2D

The official tutorial explicitly recommends this route ([T1](https://docs.godotengine.org/en/4.3/tutorials/2d/2d_lights_and_shadows.html)):
“If you need different falloff, you can procedurally create a texture by assigning a New
GradientTexture2D on the light’s Texture property… set the fill mode to Radial. You will then have to
adjust the gradient itself to start from opaque white to transparent white, and move its starting
location to be in the center.”

Exact engine enum names (4.3) and verified C# names:

| Engine (docs/source)                    | C# (GodotSharp 4.3.0, verified)          |
| --------------------------------------- | ---------------------------------------- |
| `GradientTexture2D.FILL_LINEAR = 0`     | `GradientTexture2D.FillEnum.Linear`      |
| `GradientTexture2D.FILL_RADIAL = 1`     | `GradientTexture2D.FillEnum.Radial`      |
| `GradientTexture2D.FILL_SQUARE = 2`     | `GradientTexture2D.FillEnum.Square`      |
| `REPEAT_NONE / REPEAT / REPEAT_MIRROR`  | `RepeatEnum.None / .Repeat / .Mirror`    |

Note the C# **`Enum` suffix**: Godot’s C# bindings rename an enum to `<Name>Enum` when a property of the
same name exists (`Fill`, `Repeat`, `Light2D.BlendMode` → `Light2D.BlendModeEnum`,
`CanvasItem.TextureFilter` → `CanvasItem.TextureFilterEnum`). This was confirmed directly against
`GodotSharp.xml` shipped with the 4.3.0 package — do **not** write `Light2D.BlendMode.Add` or
`GradientTexture2D.Fill.Radial` in C#.

Defaults ([T1](https://docs.godotengine.org/en/4.3/classes/class_gradienttexture2d.html)):
`Width = 64`, `Height = 64`, `FillFrom = (0,0)`, `FillTo = (1,0)`, `Repeat = None`, `UseHdr = false`.

`UseHdr` ([T1](https://docs.godotengine.org/en/4.3/classes/class_gradienttexture2d.html)): “If `true`,
the generated texture will support high dynamic range (`Image.FORMAT_RGBAF`). This allows for glow
effects to work if `Environment.glow_enabled` is `true`. If `false`, … overbright colors will be clamped
(`Image.FORMAT_RGBA8`).”

### 2.2 Gradient, from source (important gotchas)

Engine source, tag `4.3` (T2) — `scene/resources/gradient.cpp`:

```cpp
Gradient::Gradient() {
    //Set initial gradient transition from black to white
    points.resize(2);
    points.write[0].color = Color(0, 0, 0, 1); points.write[0].offset = 0;
    points.write[1].color = Color(1, 1, 1, 1); points.write[1].offset = 1;
}
```

- `new Gradient()` in C# **already has exactly two points** (black-opaque @ 0 → white-opaque @ 1).
  `AddPoint(offset, color)` **appends** (`points.push_back`), so calling it on a fresh gradient leaves you
  with 3 points. To build a custom falloff, either mutate the two existing points via
  `SetColor(0, …)`/`SetColor(1, …)`, or replace the whole array via `SetOffsets(float[])` +
  `SetColors(Color[])` (both resize the point array).
- `RemovePoint` errors if only one point would remain (min 1 point; a 1-point gradient fills the texture
  with that single color).
- Texture regeneration is deferred internally (`call_deferred`), so it is safe to configure
  offsets+colors back-to-back.

### 2.3 Radial fill math, from source (tag 4.3)

`scene/resources/gradient_texture.cpp::_get_gradient_offset_at` (T2):

```cpp
} else if (fill == Fill::FILL_RADIAL) {
    ofs = (pos - fill_from).length() / (fill_to - fill_from).length();
}
...
if (repeat == Repeat::REPEAT_NONE) {
    ofs = CLAMP(ofs, 0.0, 1.0);
}
```

with `pos.x = x/(width-1)`, `pos.y = y/(height-1)`.

Consequences (implementation-critical):

1. **`fill_from` is the circle center, `fill_to` defines a point on the falloff circle**;
   `radius_UV = |fill_to − fill_from|`. For a centered falloff that reaches the texture edge horizontally:
   `FillFrom = (0.5, 0.5)`, `FillTo = (1.0, 0.5)`.
2. **`RepeatEnum.None` clamps** beyond the radius (corners of the square texture get `ofs > 1` →
   clamped to the final gradient stop). Therefore the gradient’s **last stop must be fully transparent**,
   otherwise the corners show a solid color square.
3. Because x and y are normalized independently, a **non-square texture produces an elliptical falloff**.
   Use `Width == Height` for circular lights.
4. Above the default 64×64, large `texture_scale` values interpolate a small bitmap; use 128–256 px for
   smooth large lights (perf cost is in the rendered footprint, not the texture size — see §4).

### 2.4 `texture_scale`, offset, height

From the tutorial and class refs (T1):

- “Texture Scale: The multiplier for the light’s size. Higher values will make the light extend out
  further. Larger lights have a higher performance cost as they affect more pixels on screen.”
  `PointLight2D.TextureScale` (C#: `TextureScale`) therefore gives a **world-space diameter ≈
  texture size × texture_scale** (canvas units at zoom 1).
- “Offset: The offset for the light texture. Unlike when you move the light node, changing the offset
  does *not* cause shadows to move.”
- “Height” only matters with 2D normal mapping (`0.0` default; increase if normal-mapped surfaces look
  under-lit).
- The light texture may contain alpha — explicitly useful with the **Mix** blend mode; not required for
  Add/Sub.

---

## 3. PointLight2D blend modes

`Light2D.BlendModeEnum` (C#-verified) = `Add = 0` (default), `Sub = 1`, `Mix = 2`.
Exact docs text ([T1](https://docs.godotengine.org/en/4.3/classes/class_light2d.html)):

- **Add** — “Adds the value of pixels corresponding to the Light2D to the values of pixels under it.
  This is the common behavior of a light.”
- **Sub** — “Subtracts the value of pixels… resulting in inversed light effect.”
- **Mix** — “Mix the value of pixels corresponding to the Light2D to the values of pixels under it by
  linear interpolation.”

When to use each (synthesis; flash semantics from `LightFXState`):

| Use case                    | Mode | Why |
| --------------------------- | ---- | --- |
| Ambient/arena mood glow     | `Add` (low energy) | Adds a base illumination that dark CanvasModulate areas can register; `Mix` is an option for stylized tinting but can *darken* where the texture is dark. |
| Impact strobe (hit spark)   | `Add` | Brightening is what sells a hit; short lifetime (~0.2 s in `LightFXState.ImpactFlash`). |
| Explosion / destructible flare | `Add` | Biggest halo; with HDR 2D + glow + `energy > 1` it is the natural bloom source (`LightFXState.ExplosionFlash` peak 1.6). |
| Hero-damage pulse           | `Add` (red) | Same reason; screen-space red flash is additive. |
| “Negative light” / shroud   | `Sub` | The only mode that darkens; documented as “not physically accurate… special effects”. |
| Colored biome tint / stylized “replace” lighting | `Mix` | Linear interpolation toward the texture color; alpha channel of the texture is meaningful here. |

Known quirks (T2): non-`Add` modes with normal-mapped sprites can produce inconsistent results
([#94383](https://github.com/godotengine/godot/issues/94383) — workaround: replicate the effect with an
additive/subtractive `Sprite2D` using the same texture, or a custom light shader). For this project
(no normal maps in the lighting path, flashes only) this is a non-issue as long as we stay on `Add`.

Glow interaction (see §1.4): only `Add` output that exceeds the threshold (or is caught by Bloom) will
bloom; `Sub` can never bloom.

---

## 4. Performance + gotchas for many transient lights

### 4.1 Hard limit: 16 lights per CanvasItem (and per TileMap quadrant)

Maintainer statement on [issue #81147](https://github.com/godotengine/godot/issues/81147) (clayjohn, T2):
“The 2D renderer supports **up to 16 lights per item**. When you use a large quadrant size, the entire
quadrant is rendered as one item, so the 16 light limit applies to the entire quadrant. If you are
bumping up against that limit, then you likely need to reduce the size of your quadrant, or reduce the
number of lights that you are using.”

Calinou added that the limit is baked into shader data (4 bits) and is effectively fixed. Symptoms:
some lights silently stop contributing **on TileMaps only** while sprites remain lit; Sprite2D items are
individual, so the cap is rarely hit there. For a floor built from `TileMapLayer` (4.3), keep
`rendering_quadrant_size` small enough for the local light density, or cap simultaneous overlapping
lights (the existing 12-slot flash pool + ambient stays under 16 per item only if all flashes overlap the
same item — worth an explicit budget check if more lights are added later).

### 4.2 Cost model and the 4.3 early-exit optimization

[PR #90920](https://github.com/godotengine/godot/pull/90920) (“Exit light calculation early when pixel
outside of light bounding rectangle”), merged 2024-04-26, milestone **4.3**, listed in the 4.3 dev 6
changelog (T2/T1). The PR body explains the model: for each pixel of a CanvasItem the fragment shader
loops over all lights intersecting that item; previously the light texture was sampled and the light
shader run even when the pixel was outside the light’s rectangle. Reported gains in the PR (project
test): iGPU RD **240 → 370 FPS**; iGPU Compatibility **140 → 275 FPS**.

Implementation implications:
- Cost scales with **(pixels of lit items) × (lights intersecting each item)**; a light that intersects
  nothing is ~free.
- Larger `texture_scale` enlarges the footprint and is explicitly documented as more expensive (T1).
- Culling transient lights off-item via `Enabled`/Energy 0 removes them from the loop; but since the
  4.3 opt is an early-out anyway, keeping 12 pooled lights alive with energy 0 is cheap.

### 4.3 Shadows

- `ShadowEnabled` default is `false` (T1). Keep it false for all transient flashes — flashes shouldn’t
  cast shadows anyway, and shadowed lights are much more expensive (“chances are it would be pretty slow
  on most hardware anyway — especially with shadows enabled”, Calinou, [#81147](https://github.com/godotengine/godot/issues/81147)).
- If a static “brazier” light ever gets shadows: `ShadowFilterEnum.None` is fastest;
  PCF13 “is the slowest shadow filtering mode, and should be used sparingly” (T1 Light2D docs);
  shadows require `LightOccluder2D` nodes (or occluder polygons on TileMapLayer tiles) and follow normal
  2D draw order rules (T1 tutorial).

### 4.4 Pooling

- Create the `PointLight2D` nodes once at scene build (the `LightFXState` design of 12 slots + 1 ambient
  maps directly to fixed nodes), and animate `Energy` (and optionally `TextureScale`) — never
  add/remove nodes per hit.
- **Do not tween the light node’s `Scale` to 0** — historical Light2D code paths hit singular-transform
  errors at zero scale ([issue #25986](https://github.com/godotengine/godot/issues/25986), T2, Godot 3
  era; the guidance to hide/deactivate instead still stands). `LightFXState` already grows
  `RadiusScale` as the envelope fades — map that onto `TextureScale`, not `Node2D.Scale`.

### 4.5 Texture filtering — project-specific

The project sets `textures/canvas_textures/default_texture_filter=0` (**Nearest**) in `project.godot`.
A code-generated gradient light sampled with Nearest at large `texture_scale` will show blocky rings.
Set the light node’s own filter to linear:

```csharp
light.TextureFilter = CanvasItem.TextureFilterEnum.Linear; // override the project-wide Nearest default
```

Known open issue ([#76266](https://github.com/godotengine/godot/issues/76266), T2): PointLight2D does not
respect Nearest filtering for normal-map/shadow shading — lighting is evaluated per screen pixel.
Irrelevant for smooth gradient lights; relevant only if the project later goes pixel-art + normal maps.

### 4.6 Transforms, camera zoom, and item scale/rotation

- `Light2D` is a `Node2D`/`CanvasItem`: the light texture is transformed by its node’s transform, and
  everything is in canvas space, so camera zoom scales the light like any sprite. No camera-zoom
  interaction issues are documented for `PointLight2D`.
- Documented zoom caveat applies to `DirectionalLight2D.MaxDistance`: “Camera2D zoom is not taken into
  account by Max Distance, which means that at higher zoom values, shadows will appear to fade out
  sooner” (T1 tutorial). Only relevant if a directional light is added.
- `PointLight2D.Offset` moves the texture but **not** the shadow origin (T1).
- Lights affect any CanvasItem in the same canvas whose `light_mask` matches `range_item_cull_mask`
  (both default `1`) within the Z range (`-1024..1024` default) and layer range (T1 Light2D docs).
  There is no parent/child placement requirement between lights and lit items.
- `DirectionalLight2D` ignores `range_item_cull_mask` entirely (T1 Light2D docs).

### 4.7 Tilemaps

- 4.3 replaced `TileMap` with `TileMapLayer` (T1 release notes); the light-per-item cap moves from
  `cell_quadrant_size` to `rendering_quadrant_size`. Issue #81147 remains open (T2) — treat large
  quadrants + many lights as a known risk and verify in-game.
- For very large numbers of *static* lights, maintainer advice is to fake lighting with per-tile
  `Modulate` or additive sprites instead of real lights (Calinou, #81147).

### 4.8 The documented alternative for short-lived effects

T1 tutorial, “Using additive sprites as a faster alternative to 2D lights”: “This is particularly suited
for short-lived dynamic effects, such as bullets or explosions.” Downsides (documented): inaccurate
blending (cannot light up fully dark areas), no shadows, ignores normal/specular maps. For Hero-sArena,
the planned `PointLight2D` flashes are the right call because the arena is *fully dark* and the flash is
meant to carve light out of darkness — but if profiling shows 12 pooled lights is too much on target
hardware, a hybrid (Sprite2D add flashes over already-lit areas) is a documented escape hatch.

---

## 5. Godot 4.3-specific caveats (vs 4.0–4.2)

1. **HDR 2D** is available (added in 4.2 via [GH-80215](https://github.com/godotengine/godot/pull/80215),
   announced in the [4.2 dev 3 snapshot](https://godotengine.org/article/dev-snapshot-godot-4-2-dev-3);
   T1). It only works on the **Forward+ / Mobile** renderers, requires an editor restart after enabling,
   and switches 2D to linear color space (use `source_color` on shader color uniforms — T1 4.3 docs).
2. **Light shader early-exit** ([PR #90920](https://github.com/godotengine/godot/pull/90920), milestone
   4.3) makes many-light scenes substantially faster than 4.0–4.2 — plan perf budgets against 4.3, not
   old benchmarks (T2/T1).
3. **TileMapLayer introduced in 4.3**; lighting/occluder workflows and the 16-light-per-quadrant issue
   apply to it (T1 release page, T2 #81147).
4. **2D glow without HDR requires Background Mode = Canvas + lowered HDR threshold**; with HDR off and
   default threshold/bloom, glow is invisible (T1 docs). Official 4.3 demo uses Canvas + `glow_bloom`
   (T2).
5. **Compatibility renderer glow differs** (fewer properties; T1 docs). Project uses the default
   Forward+, so the documented Forward+ behavior applies.
6. **Light masks**: `range_item_cull_mask` (which items receive light) vs
   `shadow_item_cull_mask` (which occluders cast shadows) — two separate masks, default `1` each;
   `DirectionalLight2D` ignores the light mask (T1 class docs). If the arena ever masks lights to
   specific layers, both sides must be updated.
7. **Non-Add blend + normal maps** has inconsistent results ([#94383](https://github.com/godotengine/godot/issues/94383), T2, affects 4.x).
8. **CanvasModulate across canvases** was buggy in 4.0 and fixed for 4.1+
   ([#63666](https://github.com/godotengine/godot/issues/63666) → [PR #75238](https://github.com/godotengine/godot/pull/75238), T2).

---

## Minimal C# snippets (names verified against GodotSharp 4.3.0)

### Radial light texture, generated entirely in code

```csharp
using Godot;

/// <summary>Radial falloff light texture with no image assets (GradientTexture2D).</summary>
public static GradientTexture2D CreateRadialLightTexture(int size = 256)
{
    // new Gradient() starts with exactly 2 points (Godot 4.3 source):
    //   index 0 = black opaque @ offset 0.0, index 1 = white opaque @ offset 1.0
    var gradient = new Gradient();
    gradient.SetColor(0, Colors.White);                 // hot center
    gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));    // transparent rim (required: Repeat.None clamps)

    // Optional smoother falloff — replaces the 2 default points with a 4-point curve:
    // gradient.SetOffsets(new[] { 0f, 0.35f, 0.7f, 1f });
    // gradient.SetColors(new[]
    // {
    //     Colors.White,
    //     new Color(1f, 1f, 1f, 0.65f),
    //     new Color(1f, 1f, 1f, 0.20f),
    //     new Color(1f, 1f, 1f, 0f),
    // });

    return new GradientTexture2D
    {
        Width  = size,
        Height = size,                                  // square => circular (not elliptical) falloff
        Gradient = gradient,
        Fill = GradientTexture2D.FillEnum.Radial,       // engine: FILL_RADIAL
        FillFrom = new Vector2(0.5f, 0.5f),             // circle center in UV space
        FillTo   = new Vector2(1.0f, 0.5f),             // radius = 0.5 UV -> reaches the texture edge
        Repeat = GradientTexture2D.RepeatEnum.None,     // default; clamps outside the radius
        UseHdr = true,                                  // keep >1.0 values for glow when HDR 2D is on
    };
}
```

### One pooled transient light (Add mode, no shadows, linear filter)

```csharp
using Godot;

/// <summary>Creates one reusable flash light sharing a pre-generated radial texture.</summary>
public static PointLight2D CreateFlashLight(Texture2D radialTex, float worldRadius, int textureSize = 256)
{
    // TextureScale units: world diameter = textureSize * TextureScale.
    float baseScale = (worldRadius * 2f) / textureSize;

    return new PointLight2D
    {
        Texture      = radialTex,                             // shared Resource across all pool slots
        Color        = Colors.White,                          // tint per flash via Color on spawn
        Energy       = 0f,                                    // slots pulse this up/down
        TextureScale = baseScale,
        BlendMode    = Light2D.BlendModeEnum.Add,             // C#: BlendModeEnum, not BlendMode.Add
        TextureFilter = CanvasItem.TextureFilterEnum.Linear,  // project default is Nearest
        ShadowEnabled = false,                                // transient flashes never cast shadows
        Enabled = true,                                       // keep enabled; drive visibility with Energy
    };
}
```

### CanvasModulate + environment wiring (mirrors `MainBootstrap.EnsureEnvironment`)

```csharp
using Godot;

// 1) Dark ambient base for the gameplay canvas (do this once, before/with the light pool).
//    Position does not matter; it tints EVERY node on its canvas. Keep the HUD on a CanvasLayer.
AddChild(new CanvasModulate { Color = new Color(0.10f, 0.11f, 0.16f) });

// 2) Existing environment. To make Glow actually visible in 2D, use one of:
var env = new Godot.Environment
{
    GlowEnabled  = true,
    GlowStrength = 0.5f,

    // Option A (no HDR): documented 2D path — canvas background + nonzero bloom / lower threshold.
    BackgroundMode   = Godot.Environment.BGMode.Canvas,
    GlowBloom        = 0.08f,                 // master demo value; docs require Bloom > 0 or a lower threshold
    GlowHdrThreshold = 0.9f,                  // lower so bright-but-clamped LDR pixels glow
    // GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight, // default; "works best on dark scenes"

    // Option B (HDR 2D): enable ProjectSettings rendering/viewport/hdr_2d (Forward+/Mobile, editor restart),
    // keep the light gradient's UseHdr = true and let flash Energy exceed 1.0 for true bloom.
};
AddChild(new WorldEnvironment { Environment = env });
```

---

## Key takeaways for implementation

1. **Add exactly one `CanvasModulate`** to the arena canvas (e.g., next to the `WorldEnvironment` in
   `MainBootstrap`) before/with the light pool. It tints every node on the canvas regardless of position;
   HUD on its own `CanvasLayer` stays readable. Dark modulated areas are exactly the “canvas” the flash
   lights carve out.
2. **The current glow configuration is a no-op in 2D** (`GlowBloom` 0, LDR canvas, threshold 1.0). Pick
   one: (a) low-risk: `BGMode.Canvas` + `GlowBloom ≈ 0.05–0.1` (official demo uses 0.08) and optionally a
   lower `GlowHdrThreshold`; (b) higher quality: enable the `rendering/viewport/hdr_2d` project setting
   and keep `UseHdr = true` on the gradient + flash `Energy > 1` for real bloom on explosions. HDR 2D is
   Forward+/Mobile only and needs an editor restart.
3. **One shared `GradientTexture2D` for all flash lights**: 256×256, `FillEnum.Radial`,
   `FillFrom = (0.5, 0.5)`, `FillTo = (1.0, 0.5)`, white-opaque → transparent, `RepeatEnum.None`
   (clamps — the last stop *must* be transparent), square aspect for a circular falloff.
4. **`LightFXState` → `PointLight2D` mapping**: `Energy = slot.Energy`;
   `TextureScale = slot.Radius * slot.RadiusScale / (textureSize * 0.5f)`; `BlendModeEnum.Add`;
   `ShadowEnabled = false`; recolor per event (impact = damage-type color, explosion = warm,
   hero damage = red). Ambient mood = one long-lived `Add` light at `LightFXState.AmbientEnergy`.
5. **Never animate node `Scale` on lights; animate `Energy`/`TextureScale`** (historical singular-transform
   crash at scale 0; `Scaling` a light also scales its footprint unpredictably relative to `TextureScale`).
6. **Set `TextureFilter = CanvasItem.TextureFilterEnum.Linear` on each light** because the project's
   global canvas filter is Nearest (`default_texture_filter=0`); otherwise large radial lights show
   concentric banding.
7. **Budget: ≤16 lights per CanvasItem.** The 12-slot pool + ambient fits only as long as future lights
   respect the cap per item; on TileMapLayer floors keep `rendering_quadrant_size` small, or lights will
   silently drop out (#81147 is still open in 4.3).
8. **C# API names to remember**: `GradientTexture2D.FillEnum.Radial`, `GradientTexture2D.RepeatEnum.None`,
   `Light2D.BlendModeEnum.Add/Sub/Mix`, `CanvasItem.TextureFilterEnum.Linear`,
   `Environment.BGMode.Canvas`, `Environment.GlowBlendModeEnum.*`. All verified against the
   GodotSharp 4.3.0 assembly. (`Gradient.InterpolationModeEnum` also exists if a cubic falloff is wanted.)
9. **Verify in-engine** (not resolvable from docs alone): how the flat backdrop behaves with the current
   `BGMode.Color` + dark `BackgroundColor` in a pure-2D viewport (official demos rely on
   `default_clear_color` + `BGMode.Canvas`); the actual glow look with the chosen option; and light
   density versus `rendering_quadrant_size` on the arena floor if it is a TileMapLayer.
