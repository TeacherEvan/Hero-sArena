# Godot 4.3 — 2D Particle Techniques for Hero's Arena (C#)

Research notes for implementing explosion / debris / smoke "juice" in Hero's Arena.
Engine baseline: **Godot 4.3-stable (mono)**, C#. Read-only research; no game code was modified.

**Key result up front:** for many small one-shot 2D bursts, use **`CpuParticles2D`** (note the C# class
name!) with `OneShot = true` and re-trigger it with **`Restart()`** — never by toggling `Emitting`.
Every claim below is tagged with a source tier.

## Source tiers used in this document

| Tag | Meaning |
|---|---|
| **T1** | Official Godot **4.3** documentation (`docs.godotengine.org/en/4.3/...`) |
| **T1-src** | `godotengine/godot` **4.3-stable** engine source — behavior verified at line level |
| **T1-api** | Reflection dump of the local `GodotSharp.dll` **4.3.0.0** (`~/.local/bin/godot-4.3-mono/GodotSharp/Api/Release/GodotSharp.dll`). Authoritative for exact C# names; the Debug build agrees |
| **T2** | Official extras: `godot-demo-projects` 4.3 branch, godotengine.org blog, engine issue tracker |
| **T3** | Community studies / forum posts |
| **T4** | Talks / videos (juice & game-feel material) |

Version-tagging rule: anything from `master`/`latest` docs is marked **T1-latest** and explicitly called
out as **not 4.3** where it differs.

## Query log (how this was researched)

- "Godot 4 CPUParticles2D one shot restart C#" / "Godot 4 particle re-trigger restart vs emitting"
- "Godot 4.3 GPUParticles2D vs CPUParticles2D 2D one shot compatibility renderer"
- "Godot 4 emit_particle sub emitter compatibility Forward+ Mobile"
- "Godot GradientTexture2D radial fill soft dot particle texture"
- "Godot 4 CanvasItemMaterial additive blend C# BlendModeEnum"
- "Godot CPUParticles2D performance many emitters 2D discussion"
- "Godot 4 particles physics interpolation 2D"
- "juice it or lose it" / "art of screenshake" (game-feel references)
- Direct fetches: 4.3 class docs + tutorial RST (4.3 and master branches), 4.3-stable C++ sources,
  4.3 demo project scene/README, local `GodotSharp.dll` reflection.

---

## 1. C# API names — verified (read this first)

### 1.1 The class names are not the doc names

Godot docs and GDScript use `CPUParticles2D` / `GPUParticles2D`, but the **C# classes are `CpuParticles2D`
and `GpuParticles2D`** [T1-api]. (`new CPUParticles2D()` will not compile.)

The binding generator applies `pascal_to_pascal_case()` to class names; enum names only get an `"Enum"`
suffix when they would collide with an existing PascalCase member of the class
(`modules/mono/editor/bindings_generator.cpp`, ~line 4030) [T1-src].

### 1.2 Enum names (all reflection-verified) [T1-api]

| GDScript enum | C# enum | Members (C#) |
|---|---|---|
| `CPUParticles2D.Parameter` | `CpuParticles2D.Parameter` | `InitialLinearVelocity, AngularVelocity, OrbitVelocity, LinearAccel, RadialAccel, TangentialAccel, Damping, Angle, Scale, HueVariation, AnimSpeed, AnimOffset, Max` |
| `CPUParticles2D.ParticleFlags` | `CpuParticles2D.ParticleFlags` | `AlignYToVelocity, RotateY, DisableZ, Max` |
| `CPUParticles2D.EmissionShape` | `CpuParticles2D.EmissionShapeEnum` | `Point, Sphere, SphereSurface, Rectangle, Points, DirectedPoints, Max` (2D uses `Rectangle`, 3D uses `Box`) |
| `CPUParticles2D.DrawOrder` | `CpuParticles2D.DrawOrderEnum` | `Index, Lifetime` |
| `GPUParticles2D.EmitFlags` | `GpuParticles2D.EmitFlags` | `Position, RotationScale, Velocity, Color, Custom` |
| `GPUParticles2D.DrawOrder` | `GpuParticles2D.DrawOrderEnum` | `Index, Lifetime, ReverseLifetime` |
| `CanvasItemMaterial.BlendMode` | `CanvasItemMaterial.BlendModeEnum` | `Mix, Add, Sub, Mul, PremultAlpha` |
| `GradientTexture2D.Fill` | `GradientTexture2D.FillEnum` | `Linear, Radial, Square` |
| `GradientTexture2D.Repeat` | `GradientTexture2D.RepeatEnum` | `None, Repeat, Mirror` |
| `ParticleProcessMaterial.EmissionShape` | `ParticleProcessMaterial.EmissionShapeEnum` | `Point, Sphere, SphereSurface, Box, Points, DirectedPoints, Ring, Max` |
| `ParticleProcessMaterial.SubEmitterMode` | `ParticleProcessMaterial.SubEmitterModeEnum` | `Disabled, Constant, AtEnd, AtCollision, Max` |
| `ParticleProcessMaterial.Parameter` | `ParticleProcessMaterial.Parameter` | (unsuffixed — no collision) |

### 1.3 Other C# gotchas (reflection-verified) [T1-api]

- **`Finished` is a plain `Action`** (parameterless) on both classes: `fx.Finished += OnFinished;`
- **`CpuParticles2D` has no `EmitParticle()`** — manual per-particle emission is GPU-only:
  `GpuParticles2D.EmitParticle(Transform2D xform, Vector2 velocity, Color color, Color custom, uint flags)`.
- `ParticleProcessMaterial.Direction` and `.Gravity` are **`Vector3` even in 2D** (set Z = 0).
  In 2D you should also set `ParticleFlagDisableZ = true` (per official demo README) [T1-api][T2].
- **Numeric types differ:** on `CpuParticles2D`, `Lifetime`, `Preprocess`, `SpeedScale`,
  `LifetimeRandomness` are `double`; most others are `float`. `Explosiveness` is `float` — write
  `Explosiveness = 1.0f`, not `1.0` (double literals don't implicitly convert to float).
- `Curve.AddPoint(Vector2 position, float leftTangent = 0, float rightTangent = 0, ...)`; curve limits
  via `Curve.MaxValue` / `MinValue` [T1-api].
- `Gradient.Colors` is `Color[]`, `Gradient.Offsets` is `float[]` [T1-api].
- `CpuParticles2D` has **no `VisibilityRect`** (that is `GpuParticles2D`-only) — do not port it over.
- `CanvasItem` provides `Material`, `Modulate`, and `TopLevel` (useful to detach an FX emitter from a
  moving parent) [T1-api].

---

## 2. One-shot re-trigger pattern (`CpuParticles2D`)

### The pattern

```csharp
// Once, at creation:
var fx = new CpuParticles2D
{
    OneShot = true,
    Emitting = false,   // idle until first Restart()
    // ...static config: Texture, Amount, Lifetime, Material, curves, ...
};

// Every trigger:
fx.GlobalPosition = worldPosition;
fx.Restart();           // the only reliable re-trigger
```

### Why `Restart()` and not `Emitting` toggling

- `Restart()` (4.3 source, `cpu_particles_2d.cpp:263-279`) resets `time`, `frame_remainder`, `cycle`,
  deactivates every particle, then internally calls `set_emitting(true)`. It works from any state —
  idle, mid-burst, or just-finished [T1-src].
- `Emitting = false; Emitting = true;` is unreliable: `set_emitting()` **early-returns when the value is
  unchanged** (`cpu_particles_2d.cpp:39-42`), and the docs state that while a one-shot is active,
  setting `emitting = true` "will not restart the emission cycle until after all active particles finish
  processing" [T1][T1-src].
- The docs for GPU particles say it even more directly: after `finished` there may be a short period in
  which setting `emitting = true` does nothing, and recommend calling `restart()` instead — same advice
  applies to CPU one-shots as the uniform pattern [T1].
- `Restart()` resets a GPU emitter with `active_time = lifetime * (2 - explosiveness_ratio)`
  (`gpu_particles_2d.cpp:443-455`), i.e. it is the intended "fire again" API [T1-src].

### Lifecycle facts

- `Finished` is emitted **only when `one_shot` is true** ("When `one_shot` is disabled, particles will
  process continuously, so this is never emitted") [T1]. CPU emits it when the cycle ends
  (`cpu_particles_2d.cpp:997-1000`), GPU when `time > active_time` (`gpu_particles_2d.cpp:731-735`) [T1-src].
- **Idle CPU emitters cost ~nothing.** An inactive, non-emitting `CpuParticles2D` has its internal
  processing disabled (`_update_internal` → `set_process_internal(false)`, `cpu_particles_2d.cpp:566-575`),
  and entering the tree only enables processing when `emitting` is true (`:1103-1105`) [T1-src]. A pool
  of dozens of idle emitters is fine.

---

## 3. GPU vs CPU verdict for 2D one-shots in 4.3

**Verdict: default to `CpuParticles2D` for many small, short one-shot bursts (explosions, dust, sparks).
Use `GpuParticles2D` when you need high per-burst particle counts, trails, sub-emitters, or you are
already on Forward+/Mobile and want headroom.**

Supporting facts:

- 4.3 tutorial: "GPUParticles2D is more advanced … CPUParticles2D is a CPU-driven option with
  near-feature parity with GPUParticles2D, but lower performance when using large amounts of particles.
  On the other hand, CPUParticles2D may perform better on low-end systems or in GPU-bottlenecked
  situations." [T1]
- **Sub-emitters and trails are GPU-only** in 4.3 and are the only GPU features that warn (and do not
  work) on the **Compatibility** renderer:
  "Particle trails are only available when using the Forward+ or Mobile rendering backends." /
  "Particle sub-emitters are not available when using the GL Compatibility rendering backend."
  (`gpu_particles_2d.cpp:347-353`) [T1-src]. GPU particles themselves do run in Compatibility [T1-src].
- `emit_particle()` (manual emission) is documented as Forward+/Mobile-only in later docs; the 4.3 docs
  are silent about this — treat as a forward-looking constraint [T1-latest]. Historically, GPU emission
  in Compatibility had a 4.2 regression ([#84072], fixed by [#84189]) [T2].
- GPU 2D quirks vs CPU:
  - `FixedFps` default is **30** on GPU vs **0** on CPU; `Interpolate` exists on GPU only [T1].
  - `VisibilityRect` defaults to `Rect2(-100, -100, 200, 200)` and **can cull bursts** that travel
    outside it — set it to cover the effect, or call `CaptureRect()` [T1][T1-api].
  - `amount` is allocated up front: "Higher values will increase GPU requirements, even if not all
    particles are visible at a given time or if `amount_ratio` is decreased", and "Reducing the
    `amount_ratio` has no performance benefit" [T1].
- Official blog (Oct 2023, pre-4.3 but sets direction): CPUParticles is now "a lower-end, simpler
  alternative to GPU particles, instead of a CPU-based, 1-to-1 fallback option… We recommend migrating
  your effects to GPUParticles." [T2]
- Latest docs go further: "there are no plans to add new features to CPUParticles2D… we recommend using
  GPUParticles2D unless you have an explicit reason not to." — **this sentence is not in the 4.3 docs**
  (verified absent on the 4.3 branch) [T1-latest].
- Anecdotal community study (Godot 4.2.2): a 2D test found GPU particles had a very high **base cost
  per emitter** (60→13 FPS even with amount = 5), while CPU particles degraded more gracefully for small
  amounts (60→35 FPS at amount = 5, 60→7 FPS at 500). Single-machine, no full methodology — but it
  matches the docs' guidance to prefer CPU for many small 2D bursts [T3].
- 4.3 has no physics interpolation for 2D particles (either type); disable it on particle nodes or
  accept the jitter during camera/entity interpolation [T1].

**Practical rule of thumb for Hero's Arena:** one `CpuParticles2D` per burst archetype instance,
pooled; switch a specific effect to `GpuParticles2D` only if it needs hundreds+ particles per burst,
trails, or sub-emitters.

---

## 4. Code-generated textures and additive material (C#)

### Soft dot via `GradientTexture2D` (radial fill)

```csharp
private static GradientTexture2D MakeSoftDot(int size = 32)
{
    var gradient = new Gradient
    {
        Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f) },
        Offsets = new[] { 0f, 1f },
    };

    return new GradientTexture2D
    {
        Gradient = gradient,
        Fill = GradientTexture2D.FillEnum.Radial, // Linear = 0, Radial = 1, Square = 2
        FillFrom = new Vector2(0.5f, 0.5f),       // center
        FillTo = new Vector2(1f, 0.5f),           // edge (radius direction)
        Width = size,
        Height = size,
    };
}
```

Names verified [T1-api]; radial fill semantics: gradient is mapped from `FillFrom` towards `FillTo`
[T1]. Build one texture and reuse it across emitters (cheap, but no reason to duplicate).

### Additive blending

```csharp
particles.Material = new CanvasItemMaterial
{
    BlendMode = CanvasItemMaterial.BlendModeEnum.Add, // Mix (default) | Add | Sub | Mul | PremultAlpha
};
```

`Add` is the additive glow look used by the official demo for explosion/fire/sparks; `PremultAlpha` is
for premultiplied-alpha textures (e.g. soft light), `Mix` is the default [T1-api][T2].

### Flipbook animation (optional)

Set on the same `CanvasItemMaterial`: `ParticlesAnimation = true`, `ParticlesAnimHFrames`,
`ParticlesAnimVFrames`, `ParticlesAnimLoop`; then use `AnimSpeed*` / `AnimOffset*` on the emitter. The
official demo uses a 2×2, non-looping flipbook for fire with `Add` [T1-api][T2].

---

## 5. Tuning recipes (from the official 4.3 demo) [T2]

Values below were extracted from the official `godot-demo-projects` 4.3 `2d/particles` scene
(`particles.tscn`). The demo is GPU-based, but every value maps 1:1 onto `CpuParticles2D` properties
(names in parentheses are the C# property names). Demo scenes use `preprocess` so they are mid-effect
on load — irrelevant for one-shots since `Restart()` resets the cycle.

### 5.1 Explosion puff (smoke + additive)

- `amount = 16`, `lifetime = 2.0`, `explosiveness = 1.0`, ADD blend (`CanvasItemMaterial.BlendModeEnum.Add`)
- Emission shape: sphere **surface**, radius 60 (`EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface`,
  `EmissionSphereRadius = 60f`)
- `radial_accel_curve`: 0 → ~41 (at t≈0.04) → 50 (max_value = 50) (`RadialAccelCurve`)
- `scale_curve`: 0 → 1.71 (t≈0.31) → 2.0 (max_value = 2) (`ScaleAmountCurve`)
- Color ramp (orange, fades out): `(1, 0.64, 0.28, 1)` → `(1, 0.40, 0, 1)` → dark brown → transparent
- Texture: soft smoke puff

C# translation (simplified curve points):

```csharp
var fx = new CpuParticles2D
{
    OneShot = true,
    Emitting = false,
    Amount = 16,
    Lifetime = 2.0,                 // double
    Explosiveness = 1.0f,           // float
    EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface,
    EmissionSphereRadius = 60f,
    Texture = smokeTexture,
    Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
};

var accel = new Curve { MaxValue = 50f };
accel.AddPoint(new Vector2(0f, 0f));
accel.AddPoint(new Vector2(0.04f, 41f));
accel.AddPoint(new Vector2(1f, 50f));
fx.RadialAccelCurve = accel;

var scale = new Curve { MaxValue = 2f };
scale.AddPoint(new Vector2(0f, 0f));
scale.AddPoint(new Vector2(0.31f, 1.71f), 0.287f, 0.287f);
scale.AddPoint(new Vector2(1f, 2f), 0.42f, 0f);
fx.ScaleAmountCurve = scale;
```

### 5.2 Soft smoke puff (non-additive)

- `amount = 32`, `lifetime = 2.0`, MIX (no material)
- Sphere emission r = 30; `angle_max = 360` (`AngleMax`); `radial_accel = 15`
- Scale curve: 0 → 1 (t=0.5) → 0.86
- Color ramp: white alpha 0 → white alpha 0.5 → white alpha 0

### 5.3 Sparks / debris fountain (the classic "hit" burst)

- Box emission 40×1 (`EmissionShape = …Enum.Rectangle`, `EmissionRectExtents = new Vector2(40f, 1f)`)
- `direction = (0, 1, 0)`, `spread = 5°`, `initial_velocity = 150–400`
- `gravity = (0, 400, 0)`, `damping = 20–200`
- `scale = 0.4–0.7`, `hue_variation ±0.25`, ADD blend
- Collision variant uses `collision_mode = hide on contact` — 2D particles can collide with
  `LightOccluder2D` but not physics bodies [T1]

### 5.4 Sub-emitter chain (GPU-only) — for reference

Parent: spread 25°, velocity 160, `gravity = (0, 200, 0)`, `sub_emitter_mode = AtEnd`,
`sub_emitter_amount_at_end = 1`, `sub_emitter_keep_velocity = true`; child sparks: velocity 100,
`gravity = (0, -400, 0)`, gold `(1, 0.835, 0, 1)`. In C#:
`ParticleProcessMaterial.SubEmitterModeEnum.AtEnd`, `SubEmitterAmountAtEnd = 1` [T1-api][T2].
Remember: sub-emitters do not work on Compatibility [T1-src].

### 5.5 Magic ring / fire (style references)

- Ring emission: `radius = 60`, `inner_radius = 50`, `radial_accel = -10` (inward), scale 0.51→1→0,
  rainbow ramp.
- Fire: sphere r=8, `gravity = (0, -250, 0)`, angular-velocity curve, 2×2 flipbook, ADD, loop = off.

---

## 6. Performance guidance for many simultaneous bursts

1. **Pool emitters and recycle on `Finished`.** Instantiate/free also works in 4.3, but 4.4 regressed
   scene instantiation cost for both particle node types ([#104360]: "Issue not present in Godot 4.3";
   the reporter notes it was not visible in Compatibility mode) [T2]. Pooling is future-proof and avoids
   allocation spikes.
2. **Idle emitters are ~free on CPU** [T1-src] — a pool can be generously sized; the cost is only while
   a burst is alive.
3. **Budget arithmetic:** concurrent emitters ≈ bursts per second × lifetime. E.g. 20 bursts/s × 0.8 s
   ≈ 16 emitters; at 16 particles each that's ~250 live particles — trivial for CPU. Even a chain of
   200 deaths in one second with 0.8 s lifetime ≈ 160 concurrent emitters ≈ 2.5k particles; CPU
   particles can handle this (each emitter updates a single internal MultiMesh buffer) [T1-src], but
   measure on target hardware and lower `Amount` first if needed.
4. **Don't create one GPU emitter per tiny burst.** GPU cost is allocated for the full `amount`
   regardless of visibility, and `amount_ratio` saves nothing [T1]; a per-emitter base cost was also the
   takeaway of the community study [T3]. GPU shines per effect when a single emitter covers many
   particles (trails, sub-emitters, 100s–1000s of particles).
5. **Never toggle `Emitting` per frame** — use `Restart()` (section 2) [T1][T1-src].
6. **Compatibility renderer:** CPU particles are fully safe; GPU particles work but trails/sub-emitters
   are unavailable [T1-src]. If the project targets Compatibility, this alone settles the choice for
   bursts that would otherwise want sub-emitters.
7. Keep `FixedFps` at defaults (CPU 0, GPU 30); on GPU set `VisibilityRect` generously for effects that
   move off-screen [T1].

---

## 7. Minimal pooled burst (C#)

```csharp
using System;
using System.Collections.Generic;
using Godot;

/// <summary>Spawns one-shot CpuParticles2D bursts and recycles finished emitters.</summary>
public partial class BurstFxPool : Node2D
{
    private readonly Stack<CpuParticles2D> _pool = new();

    public void SpawnBurst(Vector2 worldPosition)
    {
        CpuParticles2D fx = _pool.Count > 0 ? _pool.Pop() : CreateEmitter();
        fx.GlobalPosition = worldPosition;
        fx.Restart(); // reliable re-trigger; works on fresh, finished, or active emitters
    }

    private CpuParticles2D CreateEmitter()
    {
        var fx = new CpuParticles2D
        {
            OneShot = true,
            Emitting = false, // stays idle (and free) until Restart()
            Amount = 16,
            Lifetime = 0.8,
            Explosiveness = 1.0f,
            Texture = MakeSoftDot(),
            Material = new CanvasItemMaterial
            {
                BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
            },
        };

        fx.Finished += () => _pool.Push(fx); // fires once per burst because OneShot = true
        AddChild(fx);
        return fx;
    }

    private static GradientTexture2D MakeSoftDot(int size = 32)
    {
        var gradient = new Gradient
        {
            Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f) },
            Offsets = new[] { 0f, 1f },
        };

        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = size,
            Height = size,
        };
    }
}
```

Notes:

- All member names above are reflection-verified [T1-api]; `Finished` is `Action`, so the lambda needs
  no parameters.
- Keep the pool under a static/world-space parent, or set `TopLevel = true` on emitters if the parent
  moves (e.g. camera-relative FX layers) [T1-api].
- If you later switch an archetype to `GpuParticles2D`, the same pool shape works — just note
  `FixedFps`/`VisibilityRect` defaults and the post-`finished` restart delay [T1].

---

## 8. References

### T1 — official 4.3 docs
- CPUParticles2D — <https://docs.godotengine.org/en/4.3/classes/class_cpuparticles2d.html>
- GPUParticles2D — <https://docs.godotengine.org/en/4.3/classes/class_gpuparticles2d.html>
- ParticleProcessMaterial — <https://docs.godotengine.org/en/4.3/classes/class_particleprocessmaterial.html>
- CanvasItemMaterial — <https://docs.godotengine.org/en/4.3/classes/class_canvasitemmaterial.html>
- GradientTexture2D — <https://docs.godotengine.org/en/4.3/classes/class_gradienttexture2d.html>
- Particle systems (2D) tutorial — <https://docs.godotengine.org/en/4.3/tutorials/2d/particle_systems_2d.html>

### T1-src — 4.3-stable engine source
- `scene/2d/cpu_particles_2d.cpp` — <https://github.com/godotengine/godot/blob/4.3-stable/scene/2d/cpu_particles_2d.cpp>
  (set_emitting :39, restart :263, idle disable :566, finished :997)
- `scene/2d/gpu_particles_2d.cpp` — <https://github.com/godotengine/godot/blob/4.3-stable/scene/2d/gpu_particles_2d.cpp>
  (Compatibility warnings :347, restart :443, finished :731)
- `modules/mono/editor/bindings_generator.cpp` — <https://github.com/godotengine/godot/blob/4.3-stable/modules/mono/editor/bindings_generator.cpp>
  (enum `Enum` suffix rule, ~:4030)

### T1-api — local reflection dump
- `GodotSharp.dll` 4.3.0.0, Release (+ Debug) API assemblies shipped with
  `Godot_v4.3-stable_mono_linux.x86_64`; all C# class/enum/member names in this document were read
  from it via `System.Reflection`.

### T2 — official extras
- 2D Particles demo (4.3 branch) — <https://github.com/godotengine/godot-demo-projects/tree/4.3/2d/particles>
  (`particles.tscn` recipes, `README.md`: "when used in 2D, the 'Disable Z' flag should be enabled")
- "State of particles and future updates" (godotengine.org blog, 2023-10-23) —
  <https://godotengine.org/article/progress-report-state-of-particles>
- Slow instantiation of many particle scenes in 4.4 (not in 4.3) — <https://github.com/godotengine/godot/issues/104360>
- GPU emission in Compatibility regression (4.2) — <https://github.com/godotengine/godot/issues/84072>,
  fixed by <https://github.com/godotengine/godot/pull/84189>

### T3 — community
- "CPU vs GPU Particles 2D Performance Study — Careful, GPU Particles seem to have a HIGH base cost!"
  (forum, 2024-06-20, Godot 4.2.2) —
  <https://forum.godotengine.org/t/cpu-vs-gpu-particles-2d-performance-study-careful-gpu-particles-seem-to-have-a-high-base-cost/67850>

### T4 — talks (game feel / juice)
- "Juice it or lose it" — Martin Jonasson & Petri Purho —
  <https://www.youtube.com/watch?v=Fy0aCDmgnxg> (GDC Vault: <https://www.gdcvault.com/play/1016487/juice-it-or-lose>)
- "The art of screenshake" — Jan Willem Nijman (Vlambeer) — <https://youtu.be/AJdEqssNZ-U>

---

## 9. Unknowns / not fully verified

- Snippets were **not compiled or run** in this workspace (research-only task; the Godot mono gate is
  unavailable locally per repo AGENTS.md). Names were verified by reflection, behavior by source — but
  a first compile in the project is still the real check.
- The 4.3 docs do not state `emit_particle()`'s Forward+/Mobile-only restriction; that appears in later
  docs and is tagged accordingly.
- The community performance study [T3] is a single-machine anecdote with no methodology details.
- `ParticleProcessMaterial.SubEmitterMode`/`CollisionMode` C# member names beyond the ones listed were
  not all exercised; only the listed members are verified.
