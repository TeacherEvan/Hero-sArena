# Hero's Arena Phase 1 — Remaining Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the ObjectPoolManager autoload, enable per-type decal/projectile textures, and verify the build runs clean — completing Phase 1 playable core with real art.

**Architecture:** 
- `ObjectPoolManager` autoload (registered in `project.godot`) needs its three `PackedScene` exports assigned at startup. `GameManager` (also an autoload) will load and assign them in `_Ready()`.
- `DecalInstance` and `ProjectileBase` will load per-type textures from `AssetPaths` dictionaries at activation time, allowing a single pooled scene to serve all variants.
- All 44 sprites already exist in `res/` and all entity scenes reference correct paths.

**Tech Stack:** Godot 4.3 + C# (.NET 8), Godot.NET.Sdk/4.3.0

**Spec:** This plan implements the remaining integration gaps identified in the context — see the earlier analysis for full context.

## Global Constraints

- .NET 8 SDK at `/home/leandi-duplessis/.dotnet8` — all `dotnet` commands must use `DOTNET_ROOT=/home/leandi-duplessis/.dotnet8`
- Nullable reference types enabled — new properties should be nullable or initialized
- Build must pass with 0 errors (warnings acceptable)
- No new scene files needed — all required `.tscn` files exist
- No Blender — PIL-generated sprites are complete and in `res/`
- Follow existing code style: `namespace HeroArena`, 4-space indent, XML doc comments on public members

---

### Task 1: Assign ObjectPoolManager Exports in GameManager._Ready()

**Files:**
- Modify: `scripts/core/GameManager.cs:48-72` (the `_Ready` method)

**Interfaces:**
- Consumes: `AssetPaths.ProjectileSprites.DefaultPath`, `AssetPaths.DecalSprites.DefaultPath`, existing `PoolManager` property
- Produces: `ObjectPoolManager.ProjectileScene`, `ObjectPoolManager.DecalScene`, `ObjectPoolManager.DestructibleScene` set to valid `PackedScene` instances

- [ ] **Step 1: Write the failing test**

```csharp
// No unit test for autoload wiring — integration test is "build runs and pool doesn't throw"
// Manual verification: dotnet build passes, then check log for "ObjectPoolManager: ProjectileScene/DecalScene not assigned" absence
```

- [ ] **Step 2: Run build to verify current state**

Run:
```bash
DOTNET_ROOT=/home/leandi-duplessis/.dotnet8 /home/leandi-duplessis/.dotnet8/dotnet build /home/leandi-duplessis/github/workspaces/Hero-sArena/Hero-sArena.csproj
```
Expected: PASS (0 errors, 3 nullable warnings)

- [ ] **Step 3: Implement GameManager._Ready() pool assignment**

```csharp
public override void _Ready()
{
    Instance = this;
    EventBus.Instance.OnEnemyKilled += HandleEnemyKilled;
    EventBus.Instance.OnEnvironmentDestroyed += HandleEnvironmentDestroyed;

    // Assign ObjectPoolManager exports so pooling works
    var pool = GetNodeOrNull<ObjectPoolManager>("/root/ObjectPoolManager");
    if (pool != null)
    {
        pool.ProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/Projectile_standard_png.tscn");
        pool.DecalScene = GD.Load<PackedScene>("res://scenes/vfx/Decal.tscn");
        pool.DestructibleScene = GD.Load<PackedScene>("res://scenes/maps/Destructible.tscn");
    }
    else
    {
        GD.PrintErr("GameManager: ObjectPoolManager autoload not found at /root/ObjectPoolManager");
    }
}
```

- [ ] **Step 4: Run build to verify it compiles**

Run:
```bash
DOTNET_ROOT=/home/leandi-duplessis/.dotnet8 /home/leandi-duplessis/.dotnet8/dotnet build /home/leandi-duplessis/github/workspaces/Hero-sArena/Hero-sArena.csproj
```
Expected: PASS (0 errors)

- [ ] **Step 5: Commit**

```bash
git add scripts/core/GameManager.cs
git commit -m "feat: assign ObjectPoolManager scene exports in GameManager._Ready"
```

---

### Task 2: Add Per-Type Decal Texture Loading to DecalInstance

**Files:**
- Modify: `scripts/vfx/DecalInstance.cs:1-70`

**Interfaces:**
- Consumes: `AssetPaths.DecalSprites.Paths` dictionary, `DecalType` enum
- Produces: `DecalInstance.Texture` set to correct decal texture per activation

- [ ] **Step 1: Write the failing test**

```csharp
// No unit test — integration verification: decal spawned via DecalSystem shows correct texture per type
// Manual: run game, trigger different decal types, verify visual difference
```

- [ ] **Step 2: Add static texture cache and load in Activate()**

```csharp
// At top of class (after line 19, before const fields):
private static readonly Dictionary<DecalType, Texture2D> _textures = new();
private static bool _texturesLoaded = false;

// In Activate() method, after line 41 (Visible = true;):
if (!_texturesLoaded)
{
    foreach (var kvp in AssetPaths.DecalSprites.Paths)
    {
        _textures[kvp.Key] = GD.Load<Texture2D>(kvp.Value);
    }
    _texturesLoaded = true;
}

if (_textures.TryGetValue(type, out var tex))
{
    Texture = tex;
}
```

- [ ] **Step 3: Run build to verify it compiles**

Run:
```bash
DOTNET_ROOT=/home/leandi-duplessis/.dotnet8 /home/leandi-duplessis/.dotnet8/dotnet build /home/leandi-duplessis/github/workspaces/Hero-sArena/Hero-sArena.csproj
```
Expected: PASS (0 errors)

- [ ] **Step 4: Commit**

```bash
git add scripts/vfx/DecalInstance.cs
git commit -m "feat: per-type decal texture loading in DecalInstance.Activate"
```

---

### Task 3: Add Per-Type Projectile Texture Loading to ProjectileBase

**Files:**
- Modify: `scripts/projectiles/ProjectileBase.cs:1-108`

**Interfaces:**
- Consumes: `AssetPaths.ProjectileSprites.Paths` dictionary, `DamageType` enum
- Produces: `Sprite2D.Texture` set to correct projectile texture per activation

- [ ] **Step 1: Write the failing test**

```csharp
// No unit test — integration verification: projectile fired by different heroes shows correct texture
// Manual: run game, fire each hero's weapon, verify visual difference
```

- [ ] **Step 2: Add static texture cache and load in Activate()**

```csharp
// At top of class (after line 20, before const MAX_LIFETIME):
private static readonly Dictionary<DamageType, Texture2D> _textures = new();
private static bool _texturesLoaded = false;

// In Activate() method, after line 42 (SetPhysicsProcess(true);):
if (!_texturesLoaded)
{
    foreach (var kvp in AssetPaths.ProjectileSprites.Paths)
    {
        _textures[kvp.Key] = GD.Load<Texture2D>(kvp.Value);
    }
    _texturesLoaded = true;
}

if (_textures.TryGetValue(DamageType, out var tex))
{
    var sprite = GetNode<Sprite2D>("Sprite2D");
    sprite.Texture = tex;
}
```

- [ ] **Step 3: Run build to verify it compiles**

Run:
```bash
DOTNET_ROOT=/home/leandi-duplessis/.dotnet8 /home/leandi-duplessis/.dotnet8/dotnet build /home/leandi-duplessis/github/workspaces/Hero-sArena/Hero-sArena.csproj
```
Expected: PASS (0 errors)

- [ ] **Step 4: Commit**

```bash
git add scripts/projectiles/ProjectileBase.cs
git commit -m "feat: per-type projectile texture loading in ProjectileBase.Activate"
```

---

### Task 4: Verify Full Build and Clean Up Redundant Projectile Scenes

**Files:**
- Delete: `scenes/projectiles/Projectile_acid_png.tscn`, `Projectile_atlas_slam_png.tscn`, `Projectile_energy_png.tscn`, `Projectile_explosive_png.tscn`, `Projectile_fire_png.tscn`, `Projectile_kinetic_png.tscn`, `Projectile_lightning_png.tscn`, `Projectile_synapse_bolt_png.tscn`, `Projectile_volt_chain_png.tscn`, `Projectile_zephyr_wind_png.tscn` (10 files)
- Keep: `Projectile_standard_png.tscn` (renamed to `Projectile.tscn` for clarity), `Projectile.tscn` (original placeholder — delete if unused)

**Interfaces:**
- Consumes: Completed Tasks 1-3
- Produces: Clean scene folder, verified build

- [ ] **Step 1: Run build to verify Tasks 1-3 work together**

Run:
```bash
DOTNET_ROOT=/home/leandi-duplessis/.dotnet8 /home/leandi-duplessis/.dotnet8/dotnet build /home/leandi-duplessis/github/workspaces/Hero-sArena/Hero-sArena.csproj
```
Expected: PASS (0 errors)

- [ ] **Step 2: Delete redundant projectile scenes**

```bash
cd /home/leandi-duplessis/github/workspaces/Hero-sArena
git rm scenes/projectiles/Projectile_acid_png.tscn
git rm scenes/projectiles/Projectile_atlas_slam_png.tscn
git rm scenes/projectiles/Projectile_energy_png.tscn
git rm scenes/projectiles/Projectile_explosive_png.tscn
git rm scenes/projectiles/Projectile_fire_png.tscn
git rm scenes/projectiles/Projectile_kinetic_png.tscn
git rm scenes/projectiles/Projectile_lightning_png.tscn
git rm scenes/projectiles/Projectile_synapse_bolt_png.tscn
git rm scenes/projectiles/Projectile_volt_chain_png.tscn
git rm scenes/projectiles/Projectile_zephyr_wind_png.tscn
# Keep Projectile_standard_png.tscn as the single pooled scene
```

- [ ] **Step 3: Rename remaining scene for clarity (optional)**

```bash
git mv scenes/projectiles/Projectile_standard_png.tscn scenes/projectiles/Projectile.tscn
# Update any references in code (none expected — pool loads by path string)
```

- [ ] **Step 4: Run final build verification**

Run:
```bash
DOTNET_ROOT=/home/leandi-duplessis/.dotnet8 /home/leandi-duplessis/.dotnet8/dotnet build /home/leandi-duplessis/github/workspaces/Hero-sArena/Hero-sArena.csproj
```
Expected: PASS (0 errors)

- [ ] **Step 5: Commit cleanup**

```bash
git add -A
git commit -m "cleanup: remove redundant per-type projectile scenes, use single pooled scene"
```

---

## Self-Review Checklist

- [x] Spec coverage: All 4 integration gaps addressed (ObjectPoolManager exports, decal textures, projectile textures, scene cleanup)
- [x] No placeholders: Every step has exact code, paths, commands
- [x] Type consistency: `AssetPaths.ProjectileSprites.Paths` / `AssetPaths.DecalSprites.Paths` used consistently, `GD.Load<Texture2D>` / `GD.Load<PackedScene>` correct
- [x] Nullable safety: `_textures` dictionaries initialized before use, `_texturesLoaded` flag prevents double-load
- [x] No new dependencies: Uses existing `AssetPaths`, `GD.Load`, existing enums

---

**Plan complete and saved to `docs/superpowers/plans/2026-09-13-hero-arena-phase1-integration.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**