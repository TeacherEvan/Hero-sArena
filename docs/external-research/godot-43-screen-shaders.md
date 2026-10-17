# Godot 4.3 — `canvas_item` screen-space shaders for full-screen 2D post-processing

- **Date:** 2026-10-17
- **Scope:** Godot 4.3 stable, .NET 8 C#, top-down 2D arena shooter (Hero's Arena).
- **Project context:** 1920×1080 base, `stretch/mode="canvas_items"`, `stretch/aspect="expand"` (verified in `project.godot`); no renderer override, so desktop defaults to Forward+ (Forward+ is Godot's default rendering method).
- **Repo state at time of writing:** an implementation already exists (`shaders/screen_distortion.gdshader`, `scripts/vfx/ScreenDistortionFX.cs`, `scripts/core/ScreenRippleState.cs`) using **8 packed `vec4` uniforms** (`u_ripple0..7`) plus a karma vignette. This document validates that approach against official sources and documents the **array-uniform alternative** (`uniform vec2 u_centers[8]`, etc.) requested in the research brief.
- **Not executed in-engine:** no builds/runs were performed (per task constraints; the mono Godot binary hangs headlessly in this workspace per `AGENTS.md`). All claims below are doc/source-derived. Treat code as compile-checked-by-inspection, not runtime-verified.

## Source tiers

| Tier | Meaning | Used here |
| --- | --- | --- |
| **T1** | Official docs / official engine source / official demo project | Godot 4.3 docs, `godotengine/godot` 4.3-stable source, `godot-demo-projects` |
| **T2** | Official issue tracker / merged PRs | godotengine/godot issues & PRs |
| **T3** | Blogs / tutorials | (none used — nothing load-bearing) |
| **T4** | Community (godotshaders.com, official forums, Reddit) | Shader formulas and field-tested quirks |

## Queries run

Web searches:

1. `godotshaders.com shockwave distortion screen texture canvas_item 2D`
2. `Godot 4 hint_screen_texture canvas_item SCREEN_TEXTURE SCREEN_UV textureLod post-processing`
3. `Godot 4 shader uniform array "uniform vec2" array size limit set_shader_parameter C#`
4. `Godot 4 shader uniform array PackedVector2Array set_shader_parameter not working github issue`
5. `Godot 4.3 hint_screen_texture canvas_items stretch Compatibility renderer bug`
6. `Godot 4.4 shader "unsized" uniform array "[]" support release notes`

Primary sources fetched (full list with URLs in “Sources”):

- Godot 4.3 docs: screen-reading shaders, canvas_item shader reference, shading language, multiple resolutions, size & anchors, `Control`, `Viewport` class refs.
- Godot 4.3-stable engine source: `scene/main/viewport.cpp`, `scene/main/window.cpp`, `scene/main/viewport.h`, `modules/mono/.../Variant.cs`, `servers/rendering/renderer_rd/storage_rd/material_storage.cpp`, `drivers/gles3/storage/material_storage.cpp`.
- GitHub: issue #97728, PR #74937, issue #83106.
- Community shaders: godotshaders.com “Distortion/Shockwave Shader” (mrsir8433, CC0), “Positioned Shockwave” (pekopekorhino, MIT); Godot forum thread on array-element updates.
- Official demo: `godot-demo-projects/2d/screen_space_shaders`.

---

## 1. `hint_screen_texture` syntax and sampling (Godot 4.x)

**Exact declaration (works verbatim in 4.3)** [T1 — screen-reading shaders doc, main example]:

```glsl
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
```

- The Godot 3 built-in `SCREEN_TEXTURE` **was removed in Godot 4**; the canvas_item reference explicitly says “Removed in Godot 4. Use a `sampler2D` with `hint_screen_texture` instead.” [T1 — canvas_item shader ref, fragment built-ins table]. Any Godot 3 snippet that still writes `texture(SCREEN_TEXTURE, SCREEN_UV)` must be ported.
- Sample it with `SCREEN_UV` (a built-in varying: “Screen UV coordinate for current pixel”) [T1 — canvas_item ref]:

```glsl
COLOR = textureLod(screen_texture, SCREEN_UV, 0.0);
```

- `textureLod(..., 0.0)` is used because we want the base mip. **Warning (official):** if the filter mode is not a mipmap filter (`filter_nearest_mipmap`, `filter_linear_mipmap`, or the anisotropic mipmap variants), an LOD argument > 0.0 has the same appearance as 0.0. So cheap “blurred screen” tricks require switching the hint to a mipmap filter [T1 — screen-reading shaders doc]. The official demo does exactly this for a blur pass: `uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;` then `textureLod(screen_texture, SCREEN_UV, amount)` [T1 — `2d/screen_space_shaders/shaders/blur.gdshader`].
- You may declare **multiple** screen-texture uniforms with different filter/repeat flags on one shader (e.g. nearest for color, linear for roughness reads) [T1 — screen-reading shaders doc, “Redefining screen textures”].
- The same shader can keep `texture()` (implicit LOD 0) when the filter has no mipmaps; `textureLod(..., 0.0)` is the documented spelling. Both are equivalent for a non-mipmap filter. The repo’s current shader uses `texture(screen_texture, clamp(uv, ...))` with `filter_linear` — that is consistent with the docs.

## 2. The standard full-screen post-process pattern (Godot 4.3)

**Pattern:** `CanvasLayer` (high layer) → child `ColorRect` with full-rect anchors → `ShaderMaterial` with a `canvas_item` shader that reads `hint_screen_texture`.

- **Does it automatically get the screen texture on desktop? Yes.** In 2D, when `hint_screen_texture` is first found in a node that is about to be drawn, Godot performs a full-screen copy to a back buffer; the shader then samples that copy. The documented minimal fragment shader “results in an invisible object, because it only shows what lies behind” [T1 — screen-reading shaders doc, “Screen texture” + “Behind the scenes”].
- **BackBufferCopy:** *not needed for a single full-screen overlay.* It becomes necessary when (a) two screen-reading shaders overlap — the second reuses the copy taken before the first, so the first “disappears” unless a `BackBufferCopy` node is placed between them, or (b) any other screen-reading node draws earlier in the frame (e.g. a water shader in the world) — the overlay would then sample that earlier, stale copy. In that case place a `BackBufferCopy` immediately before the overlay with copy mode = **Viewport** (whole screen) [T1 — screen-reading shaders doc, “Behind the scenes” + “Back-buffer logic”; T4 — godotshaders comment describing the same fix for multiple shockwaves].
- **CanvasLayer ordering:** `hint_screen_texture` triggers the copy at the first screen-reading node in *draw order*; everything drawn after that node in the same frame (including higher CanvasLayers) is **not** in the copy. Consequences: the overlay must sit above the world to warp it, and anything above the overlay (HUD on a higher layer) stays crisp. The repo’s `Main.tscn` ordering (world → distortion layer → HUD/HitFlash layers) is exactly this behavior [T1 docs + repo comments].
- **Full-rect anchors:** a `Control` whose parent is not a `Control` (here: `ColorRect` directly under a `CanvasLayer`) anchors/offsets relative to the **viewport** [T1 — “Size and anchors” doc: “relative to the top-left corner of the parent control or (in case there is no parent control) the viewport”]. So `ColorRect` + `LayoutPreset.FullRect` (or `AnchorRight/AnchorBottom = 1`) fills the viewport and follows window resizes.
- **Renderer differences (2D):**
  - `hint_screen_texture` works in Forward+, Mobile and Compatibility; only `hint_normal_roughness_texture` is restricted (Forward+ only), and that is not a canvas_item concern [T1 — screen-reading shaders doc note].
  - The official “Screen Space Shaders” demo (several full-screen 2D effects) states **Renderer: Compatibility** and still uses `hint_screen_texture` throughout [T1 — demo README + shaders].
  - **Compatibility-specific bugs to be aware of on 4.3:** issue #97728 (confirmed, milestone 4.6) reports `hint_screen_texture` turning black after window resizes when a `hint_depth_texture` shader is also present, and black screen textures for viewports smaller than ~41×41 px (reported on 4.4.1); it does not reproduce on all setups and the 2D-only path was fine in reporter projects. Desktop Forward+ (this project’s default) is unaffected by that issue [T2].
- **`use_hdr_2d`:** off by default. If enabled, 2D renders into an HDR framebuffer (RGBA16 on Forward+, RGB10_A2 on Mobile) in **linear** space and is converted to sRGB only at blit; Compatibility always renders LDR. For a pass-through distortion this changes nothing; it matters only if you do color math expecting display-space values [T1 — `Viewport.use_hdr_2d` class docs].

## 3. UV distortion / refraction for shockwave & explosion rings

Community formulas converge on the same shape (all 2D screen-space, animated from outside by pushing `radius`/`center`):

- **mrsir8433, “Distortion/Shockwave Shader”** (godotshaders.com, CC0, updated for Godot 4.1.1): computes an aspect-corrected distance, builds a ring `mask` from two `smoothstep`s around `radius` (`feather`, `width`), then `vec2 offset = normalize(dist_center) * strength * mask;` and samples `texture(SCREEN_TEXTURE, biased_st)`; adds optional chromatic aberration by sampling R/B at `±offset * aberration`. Its own docs say: add it to a **ColorRect above all nodes in a CanvasLayer**, normalize the center to 0–1, and animate `radius`/`center` from outside [T4].
- **pekopekorhino, “Positioned Shockwave”** (godotshaders.com, MIT, based on Nolkaloid’s tutorial; originally Godot 3): same ring mask
  `mask = (1 - smoothstep(size - 0.1, size, len)) * smoothstep(size - thickness - 0.1, size - thickness, len)`
  and `COLOR = texture(SCREEN_TEXTURE, SCREEN_UV - disp)` where `disp = normalize(SCREEN_UV - center) * force * mask` [T4].
- The aspect-ratio correction is a known pitfall: the 0–1 UV space is not square, so distances must be computed in a space where x is scaled by `width/height`; a commenter on the distortion shader documents the “center not where you expect” bug and the fix (apply the same x-scaling to the center) [T4]. In the shader, `SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x` = `width/height` [T1 — `SCREEN_PIXEL_SIZE` = inverse resolution].

**Canonical ring-wave formulation used in this doc** (derived from the above, verified against the repo’s shipped shader):

```glsl
float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x; // width/height
vec2 delta = uv - center;                                  // UV space
float d = length(vec2(delta.x * aspect, delta.y));         // square-pixel distance
float wave = 1.0 - smoothstep(0.0, width, abs(d - radius));// ring, peaks at the wavefront
uv -= (delta / length(delta)) * wave * strength;           // refract away from the front
```

Notes:
- `1.0 - smoothstep(0.0, width, abs(d - radius))` is a symmetric ring falloff; the two-`smoothstep` product used by the community shaders produces the same visual result (a band around `radius`) and lets you set an asymmetric inner/outer edge.
- Expanding `radius` (ease-out) and decaying `strength` over the ripple lifetime gives the classic shockwave; the repo’s `ScreenRippleState` already does quadratic amplitude decay + ease-out expansion [repo + T4 pattern].
- Displacement is in UV units (resolution-independent). `0.02–0.06` is roughly 20–65 px at 1080p; the repo scales amplitude by `0.05` inside the shader [repo].

## 4. Passing multiple simultaneous ripples

Two working designs; both were found in the wild and both are supported by 4.3.

### A. Array uniforms (the requested form)

```glsl
const int MAX_RIPPLES = 8;
uniform int   u_ripple_count = 0;
uniform vec2  u_centers[8];    // SCREEN_UV-space centers
uniform float u_radii[8];      // current ring radius (UV units, y axis)
uniform float u_widths[8];     // ring thickness
uniform float u_strengths[8];  // max UV displacement
```

- Fixed-size uniform arrays are the documented form: the shading-language reference shows `uniform vec3 colors[3];` and sets it from script with a whole array [T1 — shading language, “Uniforms”]. Declare the size explicitly; do not rely on unsized uniform arrays (unsized arrays are only documented for locals/`const` globals in 4.3) [T1].
- **Whole-array updates only.** `set_shader_parameter("u_centers[0]", v)` does not work; the forum thread “Updating single element from uniform array via script” confirms per-element updates fail and the workaround is repacking/whole-array assignment [T4]. (The engine’s uniform storage is written per uniform, not per element — consistent with `material_storage.cpp` writing `p_array_size` elements at once [T1 source].)
- **Uniform budget:** desktop ~65536 bytes / 4096 `vec4`s, mobile ~16384 / 1024; `vec2`/`vec3` are padded to `vec4`, and arrays count fully toward the budget. `vec2[8] + float[8]×3` ≈ 8×16 + 24×16 = 512 bytes — negligible. If you ever need huge arrays, pack data into a texture instead [T1 — shading language, uniform size note].
- **Indexing safety:** out-of-range dynamic indexing “will crash and break rendering”; gate the loop with the count uniform and a constant array size (and clamp the count on the C# side) [T1 — shading language arrays note]. `break` in loops is supported (the docs’ `while` example uses it) [T1].
- **Quirk (4.3):** the editor can mis-infer types for integer-vector uniform arrays (issue #85763, open); irrelevant for `vec2`/`float` but worth knowing [T2].

### B. Packed `vec4` uniforms (what the repo currently ships)

`u_ripple0..7` as separate `uniform vec4` (x, y, amplitude, radius). No arrays, no marshaling: 8 `SetShaderParameter(name, Vector4)` calls per frame. This sidesteps array-uniform conversion entirely and is what the existing code does [repo]. It is a legitimate, equally-supported design.

### C. Packed `vec4` **array** (middle ground)

`uniform vec4 u_ripples[8];` accepts a single `float[32]` from C# (`PackedFloat32Array`), 4 floats per element — verified in the 4.3 engine’s uniform writer (`TYPE_VEC4` array branch converts `PackedFloat32Array` via `convert_array_std140<Vector4, float>`) [T1 source]. The repo’s `ScreenRippleState.WritePacked(float[32])` already produces exactly this layout, so `material.SetShaderParameter("u_ripples", packed)` is a one-call alternative to eight calls [repo + T1 source]. Note: if the uniform is declared `: source_color`, `Color` values get sRGB→linear converted (Vulkan); omit `source_color` for raw data [T1 source, `p_linear_color`].

### C# API for array uniforms (exact)

`ShaderMaterial.SetShaderParameter(StringName param, Variant value)` — pass the **whole array**. In Godot 4.3 C#, `Variant` has implicit conversions from `Vector2[]`, `Vector3[]`, `float[]`, `int[]`, `Color[]`, etc., which convert to the matching packed array type (`Vector2[]` → `PackedVector2Array`, `float[]` → `PackedFloat32Array`) [T1 — `Variant.cs` 4.3-stable implicit operators; `VariantUtils` packed conversion]. The engine then converts the packed array into the shader’s uniform array (PR #74937, merged for 4.1, fixed the conversion matrix and a crash when too little data was supplied) [T2].

```csharp
// C# — all three of these work for the array-uniform shader in section 5:
material.SetShaderParameter("u_ripple_count", n);            // int
material.SetShaderParameter("u_centers", centersVector2Array); // Vector2[8]
material.SetShaderParameter("u_radii", radiiFloatArray);       // float[8]

// Alternatives that also work:
// - Godot.Collections.Array<Vector2>  (derives from Godot.Collections.Array)
// - float[] for uniform vec4 arr[N] with 4 floats per element (section C above)
```

Known C# gotchas:

- **`Vector4[]` cannot be passed** — it has no packed counterpart, so there is no `Variant` conversion; the compiler rejects it. Issue #83106: use `Godot.Collections.Array<Vector4>`, `Color[]`, or flatten to `float[]` [T2]. (Directly relevant if packing into `vec4` arrays from C#.)
- Set the **full-length** arrays. The writer loops over the declared array size and the conversion reads as many source elements as it needs; keeping arrays exactly the declared length (pad unused slots with zeros) avoids any reliance on short-array behavior [T1 source]. The example below always uploads 8 entries and uses `u_ripple_count` to gate the loop.
- Setting arrays every frame is fine for 8 elements; there is no “dirty” tracking, so re-pushing an unchanged array just re-uploads it [T1 source behavior].

## 5. Complete minimal example

### `res://shaders/screen_ripple.gdshader`

```glsl
shader_type canvas_item;

// Full-screen refraction ripples. Runs on a full-rect ColorRect in a high
// CanvasLayer; samples the already-rendered frame and bends SCREEN_UV around
// up to eight simultaneous ring waves.

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;

const int MAX_RIPPLES = 8;

uniform int   u_ripple_count = 0;
uniform vec2  u_centers[8];    // ripple centers in SCREEN_UV space
uniform float u_radii[8];      // current ring radius in UV units (y-axis scale)
uniform float u_widths[8];     // ring half-thickness in UV units
uniform float u_strengths[8];  // max UV displacement

void fragment() {
    vec2 uv = SCREEN_UV;

    // 1/w : 1/h -> aspect = width / height (screen is non-square in UV space).
    float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;

    for (int i = 0; i < MAX_RIPPLES; i++) {
        if (i >= u_ripple_count) {
            break; // constant bound + dynamic count: safe on all renderers
        }

        vec2 delta = uv - u_centers[i];
        float dlen = length(delta);
        if (dlen > 0.0001) {
            // Distance measured with square pixels so rings stay circular.
            float d = length(vec2(delta.x * aspect, delta.y));

            // Ring mask: 1.0 exactly at the wavefront, smooth falloff over width.
            float wave = 1.0 - smoothstep(0.0, max(u_widths[i], 0.0001), abs(d - u_radii[i]));

            // Refract: sample "behind" the wave front.
            uv -= (delta / dlen) * wave * u_strengths[i];
        }
    }

    // repeat_disable clamps out-of-range UVs; one sample for all ripples.
    COLOR = vec4(textureLod(screen_texture, uv, 0.0).rgb, 1.0);
}
```

Design notes:
- **One texture fetch per pixel** regardless of ripple count: the loop only accumulates the UV offset; the screen is sampled once at the end. This is the same structure the shipped repo shader uses [repo].
- `filter_linear` smooths sub-pixel refraction; see gotcha #4 for `filter_nearest` trade-offs.
- `repeat_disable` makes out-of-range samples clamp to the screen edge (no wrap ghosting); the repo shader additionally `clamp()`s explicitly, which is harmless.
- Output alpha is forced to `1.0` so the overlay fully replaces the frame (`blend_mix` default). Keeping the sampled alpha can cause blending artifacts if the back buffer has non-opaque alpha.

### C# setup — CanvasLayer + ColorRect + full-rect anchors + ignore mouse

```csharp
using Godot;

namespace HeroArena
{
    /// <summary>Full-screen ripple overlay: CanvasLayer + ColorRect + ShaderMaterial.</summary>
    public partial class RippleOverlay : CanvasLayer
    {
        private const int MaxRipples = 8;

        private ShaderMaterial _material = null!;
        private ColorRect _rect = null!;

        // Uploaded as packed arrays via Variant's implicit conversions.
        private readonly Vector2[] _centers   = new Vector2[MaxRipples];
        private readonly float[]   _radii     = new float[MaxRipples];
        private readonly float[]   _widths    = new float[MaxRipples];
        private readonly float[]   _strengths = new float[MaxRipples];

        public override void _Ready()
        {
            Layer = 100; // above world (layer 0) and HUD (e.g. 10); lower layers stay crisp

            var shader = GD.Load<Shader>("res://shaders/screen_ripple.gdshader");
            if (shader == null)
            {
                GD.PrintErr("RippleOverlay: shader missing; overlay disabled.");
                return;
            }
            _material = new ShaderMaterial { Shader = shader };

            _rect = new ColorRect
            {
                Name = "RippleQuad",
                Color = Colors.White,
                Material = _material,
                MouseFilter = Control.MouseFilterEnum.Ignore, // do not eat gameplay input
                Visible = false, // idle: skips the full-screen backbuffer copy AND the draw
            };
            AddChild(_rect);
            // Parent is a CanvasLayer (not a Control) -> anchors are viewport-relative.
            _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);

            // Push full-length arrays once so array uniforms are never uninitialized.
            _material.SetShaderParameter("u_ripple_count", 0);
            _material.SetShaderParameter("u_centers", _centers);
            _material.SetShaderParameter("u_radii", _radii);
            _material.SetShaderParameter("u_widths", _widths);
            _material.SetShaderParameter("u_strengths", _strengths);
        }

        /// <summary>Call once per frame while ripples are alive.</summary>
        public void Sync(System.Collections.Generic.IReadOnlyList<Ripple> ripples)
        {
            int n = Mathf.Min(ripples.Count, MaxRipples);
            for (int i = 0; i < n; i++)
            {
                Ripple r = ripples[i];
                _centers[i]   = WorldToScreenUv(r.Position);
                _radii[i]     = r.Radius;    // UV units (see note below)
                _widths[i]    = r.Width;     // e.g. 0.02
                _strengths[i] = r.Strength;  // e.g. 0.02 .. 0.06
            }

            _material.SetShaderParameter("u_ripple_count", n);
            _material.SetShaderParameter("u_centers", _centers);
            _material.SetShaderParameter("u_radii", _radii);
            _material.SetShaderParameter("u_widths", _widths);
            _material.SetShaderParameter("u_strengths", _strengths);

            _rect.Visible = n > 0; // hidden => zero cost (no copy, no draw)
        }

        /// <summary>
        /// World position -> SCREEN_UV. Correct under canvas_items + expand:
        /// CanvasTransform maps world to pre-stretch ("logical") viewport coords,
        /// and GetVisibleRect().Size is exactly that logical size, so the ratio is
        /// the normalized render-target UV the shader sees as SCREEN_UV.
        /// Do NOT divide by GetViewportRect().Size (render-target pixels) here —
        /// that mismatches whenever window size != logical size.
        /// </summary>
        private Vector2 WorldToScreenUv(Vector2 world)
        {
            Viewport vp = GetViewport();
            Vector2 logical = vp.GetCanvasTransform() * world;
            Vector2 visible = vp.GetVisibleRect().Size;
            return visible.X <= 0f || visible.Y <= 0f ? Vector2.Zero : logical / visible;
        }

        public struct Ripple
        {
            public Vector2 Position; // world position of the impact
            public float Radius;     // 0 .. ~1, in UV units (y-axis scale)
            public float Width;
            public float Strength;
        }
    }
}
```

Radius conversion note: `radius_uv = worldRadius * CanvasTransform.Scale.Y / VisibleRect.Size.Y` converts a world-space radius to UV units (world → logical pixels via the canvas scale, logical pixels → normalized via the visible height). For the array variant, keep the aspect correction in the shader (section 5) and store `Radius` in y-normalized UV units.

Alternative to code-built UI: create the `CanvasLayer` + `ColorRect` in the scene with anchors preset **Full Rect** and `mouse_filter = Ignore`, assign the `ShaderMaterial`, and keep the C# script only for pushing uniforms. The repo already does the code-built version.

---

## 6. Gotchas (ranked by likelihood of biting this project)

1. **World→UV under `canvas_items` + `expand`: divide `CanvasTransform * world` by `GetVisibleRect().Size`, not `GetViewportRect().Size`.**
   In `canvas_items` mode the render target is the **window size** (engine source: `final_size = screen_size` in `Window::_update_viewport_size`), while the camera/canvas transform works in the pre-stretch logical space; the stretch is applied as `stretch_transform = render_size / size_2d_override` where `size_2d_override` (returned by `GetVisibleRect().Size`) is the logical/base size adjusted by the expand aspect. Therefore `(CanvasTransform * world) / GetVisibleRect().Size` equals `SCREEN_UV`, while pairing `CanvasTransform` with `GetViewportRect().Size` is off by the stretch factor whenever the window is not exactly base size (e.g. a 1280×720 window would place a center ripple at 0.75, 0.75). Verified in 4.3-stable source (`window.cpp`, `viewport.cpp`, `viewport.h`). The repo’s existing `WorldToUv` already uses the correct denominator [T1 source].
2. **`mouse_filter` defaults to `MOUSE_FILTER_STOP` (value 0) on `Control`/`ColorRect`** — a full-rect overlay would swallow all mouse input (aiming/clicks) for the whole game. Set `MouseFilter = Control.MouseFilterEnum.Ignore`. The repo already does [T1 — `Control` class ref default table].
3. **Array uniforms are all-or-nothing.** Set the whole array every time; `u_centers[0]` as a parameter name is silently ignored. From C#, `Vector2[]` and `float[]` convert implicitly to `Variant` (→ packed arrays), but `Vector4[]` does not compile as a Variant — use `Godot.Collections.Array<Vector4>`, `Color[]`, or flattened `float[]` for `vec4` arrays [T4 forum + T2 #83106 + T1 source].
4. **Filter choice for distortion:** `filter_nearest` resamples the distorted image at the nearest texel, so continuous UV offsets make the refraction “snap”/shimmer pixel by pixel; `filter_linear` is the smooth choice for shockwaves/heat haze. The cost difference is negligible (same single fetch; filtering happens in the TMU). If you want `textureLod` LOD > 0 blurs, you **must** switch to a mipmap filter or the LOD is ignored [T4 practice + T1 docs warning]. The official demo uses `filter_linear_mipmap`; the docs’ basic example uses `filter_nearest` [T1].
5. **A ColorRect smaller than the viewport produces visible seams.** Inside the rect the shader replaces the frame with the distorted resample; outside, the original remains — the boundary is a hard discontinuity, and distorted UVs can pull content from outside the rect (the screen texture copy is whole-screen). Always use full-rect anchors. Also keep `Color = White` and alpha 1 so nothing tints/blends the output [T1 copy semantics + reasoning].
6. **Disable cheaply when idle:** hide the `ColorRect` (`Visible = false`). The screen copy is triggered only when a node using `hint_screen_texture` is about to be drawn, so hiding skips both the full-screen copy and the full-screen draw. A shader `u_ripple_count == 0` early-out alone does **not** skip the copy. The repo’s `AnyWork` → `_overlay.Visible = false` pattern is the recommended shape [T1 backbuffer logic + repo].
7. **Stale back-buffer if anything else reads the screen earlier.** Only the first screen-reading node triggers the copy per frame; later screen-reading nodes reuse it (documented “first sphere disappears” example). If a water/warp shader already reads the screen earlier in the frame, put a `BackBufferCopy` (copy mode = Viewport) immediately before the overlay to force a fresh copy at that point [T1 docs].
8. **`TEXTURE_PIXEL_SIZE` is meaningless on a `ColorRect`.** It describes the node’s default 2D texture; a `ColorRect` draws a rect with the engine’s default (white, 1×1) texture, not the screen. Use `SCREEN_PIXEL_SIZE` (= 1 / viewport size) for any screen-space pixel math. `SCREEN_PIXEL_SIZE` is the **render-target** size: in `canvas_items` mode that is the window size (e.g. 1280×720 window ⇒ `(1/1280, 1/720)`), not the 1920×1080 base — resolution-independent effects should work in UV units [T1 built-in definitions + T1 multiple-resolutions “rendered directly at the target resolution”].
9. **sRGB/color space:** a pass-through resample needs no conversion — the documented minimal shader is “an invisible object”, proving the sampled values match what is on screen. The brightness/contrast example operates directly in the sampled space with no gamma step [T1]. If `Viewport.use_hdr_2d` is enabled, the 2D buffer is linear HDR (RGBA16 on Forward+) and converted at blit; pass-through distortion is still unaffected [T1 `Viewport` docs].
10. **Compatibility renderer:** 2D `hint_screen_texture` works, but 4.3-era Compatibility has confirmed edge-case bugs (black screen textures after resize when a depth-texture shader exists; sub-41px viewports; fixed in 4.6) [T2 #97728]. Desktop Forward+ (this project) is not affected. `hint_normal_roughness_texture` is Forward+ only, irrelevant for canvas_item [T1].
11. **Aspect/expand math:** with `aspect=expand`, the visible rect grows in one axis with the window aspect (engine source computes `viewport_size` from the desired/base aspect vs video-mode aspect). UV-space ring distances must be aspect-corrected in the shader (`delta.x *= width/height`), and centers must come from the world→UV mapping rather than hardcoded 1920×1080 coordinates [T1 multiple resolutions + engine source + T4 aspect-fix comment].
12. **Performance:** while visible, the overlay adds one full-screen backbuffer copy + one full-screen draw; the per-pixel cost is one screen fetch plus a few ALU ops per active ripple. Sample once after accumulating offsets (both the repo shader and section 5 do). Nothing here needs optimization on desktop; hiding while idle (gotcha 6) removes the fixed cost entirely.

## 7. Assessment of the existing repo implementation

The shipped `screen_distortion.gdshader` + `ScreenDistortionFX.cs` conform to the researched guidance:

- `uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;` — valid 4.3 syntax, sensible filter for distortion [T1/T4].
- CanvasLayer + full-rect ColorRect + `MouseFilterEnum.Ignore` + `Visible = false` while idle [T1].
- Correct world→UV mapping (`CanvasTransform / GetVisibleRect().Size`) [T1 source].
- 8 packed `vec4` uniforms instead of arrays: no marshaling pitfalls, 8 cheap setters; equivalent to the array approach for 8 ripples [T1 source + T2 #74937].
- Aspect correction via `u_aspect` (pushed from `GetVisibleRect().Size`) matches the shader-side `SCREEN_PIXEL_SIZE` ratio; both are valid. The pushed uniform keeps the shader independent of the viewport and is already covered by `ScreenRippleStateTests`.

Possible (optional) future simplification, not required: fold `u_ripple0..7` into `uniform vec4 u_ripples[8]` fed by the existing `WritePacked(float[32])` in one `SetShaderParameter` call (verified supported by the 4.3 uniform writer) [T1 source].

## 8. Verification checklist (for whoever integrates next)

- [ ] Smoke test in-editor on Forward+ (Godot cannot run headless in this workspace): spawn a ripple at a world position, confirm the ring is centered and circular at 1920×1080.
- [ ] Resize the window (or run at 1280×720 / 21:9) and confirm ripple centers still land on the world position (validates gotcha 1) and rings stay circular (validates aspect term).
- [ ] Confirm `ColorRect` does not eat mouse clicks (`MouseFilter = Ignore`) and that HUD layers above the overlay stay undistorted.
- [ ] Confirm no measurable frame cost when idle (`Visible = false`).
- [ ] If a second screen-reading shader is ever added, add `BackBufferCopy` (Viewport mode) before the overlay and re-test.
- [ ] If migrating to array uniforms: verify `SetShaderParameter` with `Vector2[]`/`float[]` on the 4.3 mono build (expected per source, not executed here).

## Sources

**Tier 1 — official docs & source (Godot 4.3 / 4.3-stable):**

- Screen-reading shaders — https://docs.godotengine.org/en/4.3/tutorials/shaders/screen-reading_shaders.html
- CanvasItem shaders (built-ins; `SCREEN_TEXTURE` removed in Godot 4) — https://docs.godotengine.org/en/4.3/tutorials/shaders/shader_reference/canvas_item_shader.html
- Shading language (uniforms, `uniform vec3 colors[3]`, uniform size limit 65536/16384 bytes, arrays, loops, out-of-range indexing crash) — https://docs.godotengine.org/en/4.3/tutorials/shaders/shader_reference/shading_language.html
- Multiple resolutions (`canvas_items` = “rendered directly at the target resolution”; expand) — https://docs.godotengine.org/en/4.3/tutorials/rendering/multiple_resolutions.html
- Size and anchors (anchors relative to viewport when no parent Control) — https://docs.godotengine.org/en/4.3/tutorials/ui/size_and_anchors.html
- `Control` class ref (`mouse_filter` default `0` = STOP) — https://docs.godotengine.org/en/4.3/classes/class_control.html
- `Viewport` class ref (`use_hdr_2d`) — https://docs.godotengine.org/en/4.3/classes/class_viewport.html
- Engine source 4.3-stable: `scene/main/window.cpp` (`final_size = screen_size` for canvas_items), `scene/main/viewport.cpp` (`get_visible_rect`, `_set_size`/`stretch_transform`), `scene/main/viewport.h` (`is_size_2d_override_stretch_enabled() == true`), `modules/mono/glue/GodotSharp/GodotSharp/Core/Variant.cs` (`implicit operator Variant(Vector2[])`, `float[]`, …), `servers/rendering/renderer_rd/storage_rd/material_storage.cpp` and `drivers/gles3/storage/material_storage.cpp` (uniform array conversion) — https://github.com/godotengine/godot/tree/4.3-stable
- Official demo: Screen Space Shaders (full-screen 2D effects, `hint_screen_texture`, `filter_linear_mipmap`, Compatibility) — https://github.com/godotengine/godot-demo-projects/tree/master/2d/screen_space_shaders

**Tier 2 — official issues / merged PRs:**

- Issue #97728 — “When changing screen resolution, having a shader that use `hint_depth_texture` will cause all shader that use `hint_screen_texture` to turn black” (confirmed; Compatibility; fixed in 4.6; also reports sub-41×41 viewports) — https://github.com/godotengine/godot/issues/97728
- PR #74937 — “Fix shader uniform storage conversions and crash” (4.1; packed-array/`Vector2[]` conversions for uniform arrays, bounds-check crash fix) — https://github.com/godotengine/godot/pull/74937
- Issue #83106 — “Shader uniform vec4 array does not accept C# Vector4 array when setting” (no `PackedVector4Array`; use `Godot.Collections.Array<T>`, `Color[]`, or flattened `float[]`) — https://github.com/godotengine/godot/issues/83106
- Issue #85763 — editor mis-infers integer-vector uniform array types (4.x, open) — https://github.com/godotengine/godot/issues/85763

**Tier 4 — community (formulas & field-tested quirks):**

- mrsir8433, “Distortion/Shockwave Shader” (godotshaders.com, CC0) — https://godotshaders.com/shader/distortion/
- pekopekorhino, “Positioned Shockwave” (godotshaders.com, MIT) — https://godotshaders.com/shader/positioned-shockwave/
- Godot forum, “Updating single element from uniform array via script” — https://forum.godotengine.org/t/updating-single-element-from-uniform-array-via-script/69544
