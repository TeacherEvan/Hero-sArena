# Recommendations — Attract Amateur to Novice Players (Expanded)
Hero's Arena · Surgical-Investigation · Escalation pass · 2026-10-17

These 3 recommendations target the onboarding barrier: the game assumes the player already knows the horde-shooter genre. Each recommendation now includes **concrete C# endpoint/control signatures, payload structures, UI widget specs, effort estimates, and tier-weighted source citations** (Tier 1 = dev/GDC/interview; Tier 2 = press/UI reference; Tier 3 = community wiki).

## Implementation Status (all 3 complete ✅)

| ID | Recommendation | Impact | Effort | Status |
|----|---------------|--------|--------|--------|
| R1 | Guided Tutorial / "First Wave" Onboarding | High | ~3–4 days | **Implemented** — `Tutorial.cs`, `Tutorial.tscn`, MainMenu button, WaveManager suppression, MainBootstrap handoff |
| R2 | Difficulty Selector (Easy / Normal / Hard) | High | ~2–3 days | **Implemented** — `Difficulty` enum, `GetSettings()`, WaveManager + EnemyMutatorSystem wiring |
| R3 | Perk Transparency + Practice/Sandbox Mode | Critical | ~4–5 days | **Implemented** — perk stats wired to all 4 heroes, `Invulnerable` property, `Sandbox.cs`, `Sandbox.tscn`, MainMenu button |

---

## R1 · Guided Tutorial / "First Wave" Onboarding Mode (Impact: High)

### Problem
New players are dropped into Wave 1 with no instruction on controls (twin-stick move + aim, attack, dodge, ability), the level-up/perk flow, or the threat/escalation loop. Wave 1 already spawns mixed enemies at full speed. The game has no `GameState.Tutorial`.

### What to add (per external grounding)
Research across Hades, Vampire Survivors, Brotato, and HoloCure converges on **no dedicated tutorial level** — instead, **Wave 1 is the guided first experience** (learn-by-doing with minimal UI). Key patterns:
- Hades: "very little preamble," "low accessibility barrier," player figures gameplay out immediately ([GDC, Tier 1]).
- Vampire Survivors: first run teaches Move → Kill → Collect XP; first level-up at ~5 XP, roughly 30–60s in ([VS Wiki, Tier 3]; [RPS interview, Tier 2]).
- Brotato (Godot-built): first wave is the tutorial, then shop/level-up between rounds ([Superjump, Tier 2]).

Recommended design for your game:
1. **Start directly in Wave 1** (no tutorial level — Hades/Vs philosophy).
2. **Wave 1 is "soft":** slow telegraphed enemies (1–3 Drones, one at a time), plenty of kiting space, hero's starting weapon auto-attacks from frame 1.
3. **Teach 3 verbs in the first 60s:** Move/dodge, Attack (auto), Collect XP gems. Show XP bar filling on first gem pickup; pause automatically at first level-up (~40–60s, 5 XP) so the player engages with the perk system ([VS Wiki Tier 3: 5 XP to level 2]).
4. **On-screen prompts** for each verb as it first matters ("Move → WASD / left stick", "Attack → X", "Dodge → Space", "Ability → Q").
5. **Perk choice presentation:** icon + name + one explicit effect line; 3 options (Galante's 3–4 sweet spot; [Android Police Tier 2]); pause the game; forced choice (must pick to resume; [VS Wiki Tier 3]).

### Concrete endpoint / control spec
**`scripts/core/GameManagerState.cs`**
```csharp
public enum GameState { MainMenu, Tutorial, Playing, Paused, GameOver, LevelUp }

public bool InTutorial { get; private set; }

public void StartTutorial()
{
    CurrentWave = 0; Score = 0; ThreatLevel = 0;
    KillCount = 0; DestructionCount = 0;
    InTutorial = true;
    SetState(GameState.Tutorial);
}

public void CompleteTutorial()
{
    InTutorial = false;
    SetState(GameState.MainMenu); // return to menu; Normal mode now unlocked
}
```

**`scripts/core/GameManager.cs`** (additions)
```csharp
public bool PendingTutorial { get; set; } = false;

public void StartTutorial() { _state.StartTutorial(); /* suppress normal wave spawning */ }
public void CompleteTutorial() { _state.CompleteTutorial(); }
// _PhysicsProcess: in Tutorial state, delegate spawning to Tutorial.cs; skip Weibull wave logic
```

**`scripts/ui/MainMenu.cs`** (additions)
```csharp
[Export] public Button TutorialButton { get; set; } = null!;
// _Ready: TutorialButton.Pressed += StartTutorial;

private void StartTutorial()
{
    GameManager.Instance.PendingStartAfterSceneChange = true;
    GameManager.Instance.PendingTutorial = true;
    GetTree().ChangeSceneToPacked(GameScene);
    CallDeferred(MethodName.OnSceneChanged);
}
```

**`scripts/ui/Tutorial.cs`** (NEW) — step machine
```csharp
public partial class Tutorial : Node
{
    public enum Step { Move, Attack, Dodge, Ability, LevelUp, Done }
    public Step Current { get; private set; } = Step.Move;

    [Export] public Label PromptLabel { get; set; } = null!;
    [Export] public PackedScene DroneScene { get; set; } = null!;
    [Export] public Vector2[] TutorialSpawnPoints { get; set; } = Array.Empty<Vector2>();

    private float _spawnTimer;
    private int _dronesRemaining;

    public override void _Ready()
    {
        EventBus.Instance.OnLevelUp += OnLevelUp;
        ShowPrompt(Step.Move);
    }

    private void ShowPrompt(Step s)
    {
        PromptLabel.Text = s switch
        {
            Step.Move => "MOVE → WASD / left stick",
            Step.Attack => "ATTACK → X / action (weapon fires automatically)",
            Step.Dodge => "DODGE → Space / right trigger",
            Step.Ability => "ABILITY → Q / left shoulder",
            Step.LevelUp => "LEVEL UP → pick a perk (pause)",
            _ => ""
        };
        PromptLabel.Visible = true;
    }

    // Spawn 1 slow Drone at a time; only after previous is killed
    public void SpawnNextDrone()
    {
        if (DroneScene == null || TutorialSpawnPoints.Length == 0) return;
        var drone = DroneScene.Instantiate<EnemyBase>();
        drone.GlobalPosition = TutorialSpawnPoints[_rng.RandiRange(0, TutorialSpawnPoints.Length - 1)];
        GetTree().CurrentScene.AddChild(drone);
        EnemyMutatorSystem.Instance?.ApplyMutators(drone, 0); // no mutators in tutorial
        GameManager.Instance.IncrementEnemyCount();
        _dronesRemaining++;
    }

    private void OnLevelUp(int level)
    {
        Current = Step.LevelUp;
        ShowPrompt(Step.LevelUp);
        GameManager.Instance.PauseGame(); // UpgradeMenu opens via EventBus
    }

    public void MarkStepDone(Step s) { /* advance to next prompt */ }
}
```

**`scenes/ui/Tutorial.tscn`** (NEW): CanvasLayer with center-screen gold prompt label (font_size 30, outline), subtle highlight around the relevant action (e.g. flash the Dodge key icon), progress dots "Step 1 of 5".

**Files to touch:** `scripts/core/GameManagerState.cs`, `scripts/core/GameManager.cs`, `scripts/ui/MainMenu.cs`, `scripts/ui/Tutorial.cs` (new), `scenes/ui/Tutorial.tscn` (new).

**Effort:** ~3–4 days (1 dev).

**Gate:** New `GameState.Tutorial`; tutorial-complete flag unlocks Normal mode in save data.

**Sources:** [Tier 1] GDC/Hades Kasavin interview — https://www.youtube.com/watch?v=9LykrwmUu8I ; [Tier 2] Android Police VS interview — https://www.androidpolice.com/vampire-survivors-developer-interview ; [Tier 2] Superjump Brotato — https://www.superjumpmagazine.com/brotato-is-a-tasty-roguelike-snack ; [Tier 3] VS Wiki Level up — https://vampire.survivors.wiki/w/Level_up

---

## R2 · Difficulty Selector (Easy / Normal / Hard) (Impact: High)

### Problem
A single fixed difficulty curve (Weibull wave intensity + CollateralKarma threat every 10 destructions + stacking EnemyMutatorSystem mutators) punishes novices. Destroying the environment (core to the genre) raises threat and strengthens enemies — novices snowball into an unwinnable state within minutes.

### What to add (per external grounding)
Research shows difficulty is best implemented as **two orthogonal levers**: (a) spawn/density curve (how many/how fast) and (b) per-enemy stats (HP/damage) — Brotato and Vampire Survivors both do both ([Brotato Wiki Tier 3-4]; [VS Nexus Tier 4]). Selection should be a **main-menu dropdown persisted to save**, with an optional Hades-style "God Mode" player damage-resistance toggle (20% → cap 80%, +2%/death) that does **not** block progression/achievements ([Inverse Tier 2]; [Access-Ability Tier 2]).

### Concrete endpoint / control spec
**`scripts/core/GameManagerState.cs`**
```csharp
public enum Difficulty { Easy, Normal, Hard }
public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Normal;

public void SetDifficulty(Difficulty d)
{
    if (CurrentDifficulty == d) return;
    CurrentDifficulty = d;
    // persist to save data here
}

// DifficultySettings payload (applied at wave generation):
public struct DifficultySettings
{
    public float EnemyCountMultiplier;   // spawn curve / density lever
    public float EnemyHealthMultiplier;  // per-enemy stats lever
    public float EnemyDamageMultiplier;  // per-enemy stats lever
    public int   ThreatInterval;         // destructions per threat level (Easy=20, Normal=10, Hard=8)
    public float MutatorScale;           // mutator count scale
    public float XpMultiplier;           // player XP gain
    public float BreatherSeconds;        // between waves
    public float PlayerDamageResistance; // Hades-style God Mode, 0.2 → 0.8 cap
}

public DifficultySettings GetSettings(Difficulty d) => d switch
{
    Difficulty.Easy   => new() { EnemyCountMultiplier = 0.6f, EnemyHealthMultiplier = 0.7f,
                                 EnemyDamageMultiplier = 0.7f, ThreatInterval = 20,
                                 MutatorScale = 0.5f, XpMultiplier = 1.5f, BreatherSeconds = 8f },
    Difficulty.Hard   => new() { EnemyCountMultiplier = 1.4f, EnemyHealthMultiplier = 1.3f,
                                 EnemyDamageMultiplier = 1.3f, ThreatInterval = 8,
                                 MutatorScale = 1.5f, XpMultiplier = 0.8f, BreatherSeconds = 4f },
    _                 => new() { EnemyCountMultiplier = 1.0f, EnemyHealthMultiplier = 1.0f,
                                 EnemyDamageMultiplier = 1.0f, ThreatInterval = 10,
                                 MutatorScale = 1.0f, XpMultiplier = 1.0f, BreatherSeconds = 5f },
};
```

**`scripts/core/WaveFormulas.cs`** (signature change)
```csharp
public static int CalculateWaveEnemyCount(int wave, int activeEnemyCount, float enemyCountMultiplier = 1f)
{
    int count = MinWaveEnemies + (int)(WeibullIntensity(wave) * IntensityScaleFactor * enemyCountMultiplier);
    int max = MaxEnemies - activeEnemyCount;
    if (count < MinWaveEnemies) return MinWaveEnemies;
    if (count > max) return max;
    return count;
}
```

**`scripts/core/WaveManager.cs`** (additions)
```csharp
private float _breatherSeconds = BREATHER_SECONDS; // set from difficulty

public void BeginNextWave()
{
    ...
    var settings = GameManager.Instance?.DifficultySettings ?? GameManagerState.GetSettings(Difficulty.Normal);
    _breatherSeconds = settings.BreatherSeconds;
    int totalEnemies = WaveFormulas.CalculateWaveEnemyCount(
        _currentWave, GameManager.Instance.ActiveEnemyCount, settings.EnemyCountMultiplier);
    SpawnWaveEnemies(totalEnemies);
}
```

**`scripts/systems/EnemyMutatorSystem.cs`** (signature change)
```csharp
public void ApplyMutators(EnemyBase enemy, int threatLevel, float mutatorScale = 1f)
{
    int mutatorCount = Mathf.CeilToInt(Mathf.Min(threatLevel, AllMutators.Length) * mutatorScale);
    // ... existing pool logic, but draw mutatorCount mutators
}
```
Call site in `WaveManager.SpawnEnemy`: `EnemyMutatorSystem.Instance?.ApplyMutators(enemy, GameManager.Instance.ThreatLevel, settings.MutatorScale);`

**`scripts/core/GameManager.cs`** (additions)
```csharp
public DifficultySettings DifficultySettings => _state.GetSettings(_state.CurrentDifficulty);
public void SetDifficulty(Difficulty d) => _state.SetDifficulty(d);
```

**`scripts/ui/MainMenu.cs`** (additions)
```csharp
[Export] public Button EasyButton, NormalButton, HardButton { get; set; } = null!;
// _Ready: each → SetDifficulty + visual highlight on selected
private void SetDifficulty(Difficulty d)
{
    GameManager.Instance.SetDifficulty(d);
    // update button visuals: highlight selected
}
```

**Files to touch:** `scripts/core/GameManagerState.cs`, `scripts/core/WaveFormulas.cs`, `scripts/core/WaveManager.cs`, `scripts/systems/EnemyMutatorSystem.cs`, `scripts/core/GameManager.cs`, `scripts/ui/MainMenu.cs`.

**Effort:** ~2–3 days (1 dev).

**Gate:** `GameManagerState.Difficulty` enum persisted; `GetSettings()` pure → headlessly testable xUnit.

**Sources:** [Tier 2] Inverse Hades God Mode interview — https://www.inverse.com/gaming/hades-god-mode-interview ; [Tier 2] Access-Ability Hades analysis — https://access-ability.uk/2022/04/25/hades-god-mode-is-a-great-approach-to-difficulty/ ; [Tier 3-4] Brotato Wiki Danger Levels — https://brotato.fandom.com/wiki/Danger_Levels ; [Tier 4] VS Nexus curse — https://www.nexusmods.com/vampiresurvivors/mods/120

---

## R3 · Perk Transparency + Practice / Sandbox Mode (Impact: Critical)

### Problem (two parts)
1. **Dead perks:** `UpgradeMenu.ApplyPerk()` implements only DamageUp (commented no-op), SpeedUp, and MaxHealthUp. The other 17 perks (`break;`) do nothing. A novice picks "Piercing Shots" or "Life Steal" and feels no effect — breaks trust.
2. **No safe experimentation:** Permadeath GameOver on first hero death gives novices no room to learn heroes/abilities/perk combos.

### What to add (per external grounding)
Research shows: perk offer screens present **icon + name + explicit effect line** ([VS Wiki Tier 3]; [Hades 2 Wiki Tier 3]), the count should be **3** (Galante's sweet spot, [Android Police Tier 2]), choice is **forced by default** with skip/reroll gated behind currency ([VS Wiki Tier 3]; [Digital Trends Tier 2]), and synergies are surfaced via tags/prerequisite badges, not spelled-out optimal builds ([Hades fandom Tier 3]; [kokutech Tier 3]).

### Concrete endpoint / control spec
**`scripts/ui/UpgradeMenu.cs`** (rewrite `ApplyPerk` + add descriptions)
```csharp
// Perk metadata: explicit one-line effect shown under each perk name
private static readonly (string Name, string Description, string SynergyTag)[] PerkMeta =
{
    ("DamageUp", "+15% Damage", "Damage"),
    ("SpeedUp", "+10% Move Speed", "Movement"),
    ("HealthRegen", "+2 HP/s while not taking damage", "Survival"),
    ("MaxHealthUp", "+15% Max Health (heals to preserve %)", "Survival"),
    ("AttackSpeedUp", "-15% Attack cooldown", "Damage"),
    ("PiercingShots", "Projectiles pass through enemies", "AoE"),
    ("ExplosiveRounds", "Projectiles explode on impact (AoE)", "AoE"),
    ("LifeSteal", "+3% of damage dealt restored as HP", "Survival"),
    ("ShieldBurst", "Gain 2 shield charges on level up", "Defense"),
    ("DodgeCooldownReduce", "-25% Dodge cooldown", "Movement"),
    ("CritChanceUp", "+10% Critical hit chance", "Damage"),
    ("AoERadiusUp", "+20% AoE radius", "AoE"),
    ("ProjectileCount", "+1 Projectile per shot", "Damage"),
    ("SlowOnHit", "Enemies slowed 30% for 1s on hit", "Control"),
    ("BurnOnHit", "Enemies burn for 5 HP/s over 3s", "Damage"),
    ("FrostAura", "Nearby enemies slowed 20%", "Control"),
    ("ThornArmor", "Reflect 10% of melee damage", "Defense"),
    ("DoubleJump", "Second dodge charge", "Movement"),
    ("EnergyDamageUp", "+15% Energy damage", "Damage"),
    ("KineticDamageUp", "+15% Kinetic damage", "Damage"),
};

private static string PerkDescription(PerkType p)
{
    var i = (int)p;
    return (uint)i < (uint)PerkMeta.Length ? PerkMeta[i].Description : "";
}
private static string PerkSynergyTag(PerkType p) => ...;

private static void ApplyPerk(PerkType perk)
{
    var hero = GameManager.Instance.ActiveHero;
    if (hero == null) return;
    switch (perk)
    {
        case PerkType.DamageUp:        hero.DamageMult *= 1.15f; break;
        case PerkType.SpeedUp:         hero.MoveSpeed *= 1.10f; break;
        case PerkType.HealthRegen:     hero.HpRegenPerSec += 2f; break;
        case PerkType.MaxHealthUp:
            float ratio = hero.CurrentHealth / hero.MaxHealth;
            hero.MaxHealth *= 1.15f;
            hero.SetCurrentHealth(hero.MaxHealth * ratio);
            break;
        case PerkType.AttackSpeedUp:   hero.AttackCooldownMult *= 0.85f; break;
        case PerkType.PiercingShots:   hero.ProjectilePiercing = true; break;
        case PerkType.ExplosiveRounds: hero.ProjectileExplosive = true; break;
        case PerkType.LifeSteal:       hero.LifeStealPct += 0.03f; break;
        case PerkType.ShieldBurst:     hero.AddShieldCharges(2); break;
        case PerkType.DodgeCooldownReduce: hero.DodgeCooldownMult *= 0.75f; break;
        case PerkType.CritChanceUp:    hero.CritChance += 0.10f; break;
        case PerkType.AoERadiusUp:     hero.AoeRadiusMult *= 1.20f; break;
        case PerkType.ProjectileCount: hero.ProjectileCountBonus += 1; break;
        case PerkType.SlowOnHit:       hero.SlowOnHit = true; break;
        case PerkType.BurnOnHit:       hero.BurnOnHit = true; break;
        case PerkType.FrostAura:       hero.FrostAura = true; break;
        case PerkType.ThornArmor:      hero.ThornArmor = true; break;
        case PerkType.DoubleJump:      hero.DodgeCharges = 2; break;
        case PerkType.EnergyDamageUp:  hero.EnergyDamageMult *= 1.15f; break;
        case PerkType.KineticDamageUp: hero.KineticDamageMult *= 1.15f; break;
    }
    hero.OnPerkApplied(perk); // hero subclasses hook ability changes
}
```

**UI widget spec** (UpgradeMenu): each perk card = [Icon] [Name: gold] + [Description: TextMuted, one line] + optional [SynergyTag badge: cyan] when player already holds a prerequisite perk. 3 cards centered; game paused; forced choice (must pick to resume). Add brief invulnerability on pick ([VS Wiki Tier 3]).

**`scripts/maps/Sandbox.cs`** (NEW)
```csharp
public partial class Sandbox : Node
{
    public bool GodMode { get; private set; }
    [Export] public PackedScene DummyScene { get; set; } = null!;
    [Export] public Vector2[] DummySpawnPoints { get; set; } = Array.Empty<Vector2>();

    public void SpawnDummy(Vector2? pos = null)
    {
        if (DummyScene == null) return;
        var dummy = DummyScene.Instantiate<EnemyBase>();
        dummy.GlobalPosition = pos ?? DummySpawnPoints[_rng.RandiRange(0, DummySpawnPoints.Length - 1)];
        GetTree().CurrentScene.AddChild(dummy);
    }

    public void SetGodMode(bool on)
    {
        GodMode = on;
        var hero = GameManager.Instance.ActiveHero;
        if (hero != null) hero.Invulnerable = on;
        EventBus.Instance.EmitPowerupCollected(on ? "GodMode ON" : "GodMode OFF");
    }

    public void ApplyPerk(PerkType perk) => UpgradeMenu.ApplyPerk(perk); // reuse implementation

    public void ResetHero()
    {
        var hero = GameManager.Instance.ActiveHero;
        if (hero == null) return;
        hero.Level = 1; hero.Experience = 0;
        hero.CurrentHealth = hero.MaxHealth;
        // reset perk multipliers to defaults (store defaults on hero)
        hero.ResetStats();
    }
}
```

**`scenes/maps/Sandbox.tscn`** (NEW): empty arena, hero spawn, toolbar buttons: "Spawn Dummy", "Toggle God Mode", "Apply Perk (dropdown)", "Reset Hero", "Back to Menu". No waves, no GameOver.

**`scripts/ui/MainMenu.cs`** (addition)
```csharp
[Export] public Button SandboxButton { get; set; } = null!;
// _Ready: SandboxButton.Pressed += OpenSandbox;
private void OpenSandbox() => GetTree().ChangeSceneToPacked(SandboxScene);
```

**Files to touch:** `scripts/ui/UpgradeMenu.cs`, `scripts/maps/Sandbox.cs` (new), `scenes/maps/Sandbox.tscn` (new), `scripts/ui/MainMenu.cs`. Optional stat fields on `HeroBase`/`Atlas`/`Zephyr`/`Synapse`/`Volt` for the new multipliers.

**Effort:** ~4–5 days (1 dev).

**Gate:** `ApplyPerk` all 20 perks → each has a testable effect; `PerkDescription()` pure → headlessly testable xUnit; Sandbox scenes verified on real hardware.

**Sources:** [Tier 2] Android Police VS interview (3–4 options) — https://www.androidpolice.com/vampire-survivors-developer-interview ; [Tier 3] VS Wiki Level up / Powerup — https://vampire.survivors.wiki/w/Level_up ; [Tier 2] Digital Trends Brotato hands-on — https://www.digitaltrends.com/gaming/brotato-hands-on-impressions-pc/ ; [Tier 3] Hades 2 Wiki Boons — https://hades2.wiki.fextralife.com/Boons ; [Tier 3] kokutech Hades design — https://www.kokutech.com/blog/gamedev/design-patterns/power-fantasy/hades

---

## Prioritization & Total Estimate

| Order | ID | Recommendation | Impact | Effort |
|-------|----|----------------|--------|--------|
| 1 | R3 | Perk transparency + Sandbox mode | Critical | ~4–5 days |
| 2 | R1 | Guided "First Wave" tutorial | High | ~3–4 days |
| 3 | R2 | Difficulty selector Easy/Normal/Hard | High | ~2–3 days |

**Estimated total: ~9–12 days (1 dev).**

**Recommended first step: R3** — it fixes a silent bug (17 dead perks) that undermines every run, and gives novices the immediate payoff of "progression feels real." All pure logic follows the AGENTS.md extraction pattern (Godot-free classes + xUnit tests headless); UI scenes verified on real hardware once the Godot mono toolchain is repaired.

**IMPLEMENT button:** wire this report's IMPLEMENT control to begin with R3 (fix `ApplyPerk()` + add `PerkDescription()`), which is fully headlessly testable and unblocks the other two.

---

# Recommendations — Interactive & Destructive Environment Illusions (PM pass, 2026-10-17)

Scope: make the surroundings more interactive and destructive through art-technique illusions (shading, sprites, particles, light). **All 3 recommendations implemented and locally verified in this pass** (Project-Manager mode: no report — built directly from findings).

| ID | Recommendation | Impact | Effort | Status |
|----|---------------|--------|--------|--------|
| R-FX1 | "Living Light" — arena lit by the fight (Light2D + CanvasModulate + threat mood) | High | ~1 day | **Implemented** — `LightFXState` + `LivingLightRig` + 18 tests |
| R-FX2 | "Shockwave air" — screen-space refraction ripples + karma vignette (shader) | High | ~1 day | **Implemented** — `ScreenRippleState` + `ScreenDistortionFX` + `screen_distortion.gdshader` + 17 tests |
| R-FX3 | "Shatter & fade" — deterministic debris chunks + smoke/sparks + ground ring (sprites + particles) | High | ~1 day | **Implemented** — `DebrisBurstPlan` + `DestructionDrama` + 20 tests |

### R-FX1 · "Living Light" (Impact: High)

- **What:** `CanvasModulate` veil + 12 pooled `PointLight2D` flashes (code-generated radial textures). Damage-type impact strobes, warm destruction flares, red hero-damage pulse, pale kill sparks; ambient energy + flicker escalate with threat/boss waves.
- **Why:** light makes the environment feel like it *reacts to the player* — the core of "interactive surroundings".
- **Event hooks:** `EventBus.OnProjectileHit / OnEnvironmentDestroyed / OnHeroDamaged / OnEnemyKilled / OnThreatLevelChanged / OnWaveIncoming`.
- **Controls:** exported `VeilColor`; rate policy ≥50 ms for small flashes, bypass for explosions/hero hits; pool ≤12 (engine cap: 16 lights per CanvasItem).
- **Files:** `scripts/core/LightFXState.cs`, `scripts/vfx/LivingLightRig.cs`, `tests/HeroArena.Tests/Systems/LightFXStateTests.cs`.
- **Sources:** [Tier 1] Godot 4.3 2D lights docs; [Tier 3] NT/Gungeon reactive-light patterns (see `docs/external-research/godot-43-2d-lighting.md`, `2d-destruction-illusions.md`).

### R-FX2 · "Shockwave air" (Impact: High)

- **What:** full-screen `hint_screen_texture` shader — up to 8 refraction rings bend the frame around explosions/impacts; karma vignette tightens screen edges with threat; red pulse on hero damage. Draws before HUD/HitFlash so the UI stays crisp.
- **Why:** refraction + vignette are the two cheapest "the world has physical pressure" illusions; the vignette ties destruction count (CollateralKarma) directly to screen feel.
- **Event hooks:** same bus events; rate-limited + duplicate-coalesced in `ScreenRippleState`.
- **Files:** `scripts/core/ScreenRippleState.cs`, `scripts/vfx/ScreenDistortionFX.cs`, `shaders/screen_distortion.gdshader`, tests.
- **Sources:** [Tier 1] Godot screen-reading shaders docs; [Tier 3] per-scene grade/vignette case studies (see `docs/external-research/godot-43-screen-shaders.md`).

### R-FX3 · "Shatter & fade" (Impact: High)

- **What:** deterministic debris bursts (4–12 chunks, ballistic + damped bounce + fade), smoke + additive spark particle passes, expanding ground-ring flash. All textures code-generated; pools pre-allocated (64 shards, 4 rings, 2+2 emitters).
- **Why:** mass illusion — destroyed props read as heavy and fragmenting; the ground ring fakes blast-light propagation.
- **Event hook:** `EventBus.OnEnvironmentDestroyed` (already emitted by `DestructibleObject`).
- **Files:** `scripts/core/DebrisBurstPlan.cs`, `scripts/vfx/DestructionDrama.cs`, tests.
- **Sources:** [Tier 1] Godot particles docs; [Tier 3] Vlambeer NT article / juice literature (see `docs/external-research/2d-destruction-illusions.md`).

**Verification:** 231/231 xUnit tests (55 new), Release build 0 errors, TODO gate pass, 0 Roslynator diagnostics in new files, Godot 4.3 headless parse of `Main.tscn` + shader load. GPU-visual behavior remains unverifiable in this workspace (mono C# runtime broken — see AGENTS.md).

**Follow-ups:** HDR-2D glow enablement, GPU-particle trails for chunks, CanvasGroup decal-memory layer, Sandbox scene wiring (details in `docs/investigation-notes/2026-10-17-illusion-fx-systems.md`).

---

# Recommendations — The Living Arena: environment identity & wildlife (Environment/Wildlife pass, 2026-10-17)

Scope: make the arena feel alive — biome identity, weather, wildlife with emergent behaviour, and a detailed floor — all with **zero new art assets** (every texture code-generated) and full respect for the dark-godlike void aesthetic. **Substrate (ideas 1–7) implemented and locally verified in this pass; ideas 8–10 planned as the next pass.** Full plan: `docs/investigation-notes/2026-10-17-living-arena-wildlife.md`.

| ID | Recommendation | Impact | Effort | Status |
|----|---------------|--------|--------|--------|
| R-LA1 | One wind through everything — base drift + gusts + explosion impulses | High | ~0.5 day | **Implemented** — `AmbientWindState` + 23 tests |
| R-LA2 | Biome identity table — 5 profiles resolved from the map scene path | High | ~0.5 day | **Implemented** — `BiomeAtmosphereState` + tests |
| R-LA3 | Signature biome weather — neon rain / ash / embers / star-dust / spores / haze | High | ~0.5 day | **Implemented** — `BiomeAtmosphere` node (wave/boss moods) |
| R-LA4 | Ambient wildlife — wander, panic, boss hold-breath, calm curiosity | Very High | ~1.5 days | **Implemented** — `WildlifeDirectorState` + `AmbientWildlife` + tests |
| R-LA5 | Living ground — deterministic tufts/pebbles/cracks that sway and catch the combat light | High | ~1 day | **Implemented** — `GroundDetailPlan` + `LivingGround` + tests |
| R-LA6 | Disturbance memory — footprints, cracks, scorch patches; wildlife avoids scars | High | ~2–3 days | Planned (next pass) |
| R-LA7 | Ecosystem events — rare vignettes (flock sweep, ember storm, moth vigil) | High | ~2–3 days | Planned (next pass) |
| R-LA8 | Reactive props — lanterns/chains/banners bend under shockwind; vignette props | Medium | ~3–4 days | Planned (next pass) |

### R-LA4 · "Calm curiosity" — the flagship moment (Impact: Very High)

- **What:** between waves, when the hero stands still, bold critters drift toward the player and hold a 140 px stand-off; combat or damage scatters them. A boss wave hides all wildlife ("the world holds its breath") until it is cleared.
- **Why:** micro-emergent moments players screenshot — the world acknowledging the player as a presence, not a camera. Costs nothing at runtime (64-slot pool, one O(n²) separation pass at trivially small n).
- **Firefly pattern:** glowing species are dark most of the time — one short flash per ~4.4 s (real-firefly rhythm), golden-ratio phase desync across slots; all wildlife stays below projectile brightness.
- **Files:** `scripts/core/WildlifeDirectorState.cs`, `scripts/core/AmbientWindState.cs`, `scripts/vfx/AmbientWildlife.cs`, `tests/HeroArena.Tests/Systems/{WildlifeDirectorState,AmbientWindState,GroundDetailPlan,BiomeAtmosphereState}Tests.cs`.
- **Sources:** [T1] Godot 4.3 docs (custom drawing, lights on `_Draw`, particles) + [T3] firefly/osmosis patterns — see `docs/external-research/living-world-wildlife-patterns.md` and `godot-43-ambient-life-impl.md`.

**Verification:** 328/328 xUnit at pass time → **334/334** today; Release build 0 errors at pass time (today's Debug matches that baseline: 3 pre-existing `WaveManager` CS8602 warnings remain in the writer's in-flight file — zero diagnostics in any Living Arena file); 0 Roslynator diagnostics in new files; TODO gate pass; `Main.tscn` structural check (17 nodes / 22 ext_resources / `load_steps` consistent / node order verified). **Engine-level evidence added in the audit re-run** (the pass-time "mono cannot execute managed code" limit was a misdiagnosis of the `ObjectPoolManager` pre-warm stall, fixed in `2cd98dd`): CoreSystemTests gate **13/13** (per AGENTS.md), `Main.tscn --quit-after 600` **exit 0 with zero errors and zero ObjectDisposedExceptions**, and the new `tests/GodotTests/LivingArenaProbe.tscn` drives all three nodes on the live SceneTree — wiring + spawning + boss hold-breath — at **6/6 PASS** (includes arena-resolution and rain-gradient checks). Residual honest limit: `_Draw` pixels cannot be asserted headlessly.

**Follow-ups:** ecosystem event deck (`MomentDirector`) and reactive prop kit — designed in the investigation doc; disturbance memory shipped (scar layer + director avoidance, R-LA6; see implementation notes for the deviation from the planned ImageTexture layer).

**Audit notes (same-day re-run):** the engine probes surfaced a pre-existing stale-hero bug outside this pass — hero death (`HeroBase.Die()` → `QueueFree()`) never clears `GameManager.ActiveHero`, and cached readers guard with `hero == null`, which is false for a disposed Godot wrapper → ~9.5k `ObjectDisposedException`s per 10 s of headless run in enemies and camera follow. Reported for the `HeroBase`/`EnemyBase`/`MainBootstrap` owner; evidence in the investigation doc. A transient `MainMenu.tscn` ↔ `Sandbox.tscn` cross-reference seen mid-session no longer reproduces (the sandbox writer's edit removed it).
