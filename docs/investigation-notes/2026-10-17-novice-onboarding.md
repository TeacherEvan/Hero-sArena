# Investigation Notes — Novice/Amateur Onboarding (Hero's Arena)
Date: 2026-10-17
Mode: Investigator (codebase-first)
Strategy: codebase-first
Target: /home/leandi-duplessis/github/workspaces/Hero-sArena

## Stated mission (README)
"A top-down arena shooter built with Godot 4.3 and C# (.NET 8). Fight waves of enemies across 4 hero classes; environmental destruction escalates threat via the Collateral Karma system."

## Actual state
- Genre: horde / top-down arena shooter (Vampire Survivors / Brotato / Hades-like).
- 4 heroes (Atlas, Zephyr, Synapse, Volt), 10 enemy types, wave-based spawning via Weibull intensity curve (WaveFormulas), 2000 enemy cap, 5s breather between waves, boss every 300s.
- Progression: XP → level up → UpgradeMenu offers 3 random perks from 20 PerkType values.
- Systems: EventBus, GameManager/GameManagerState, WaveManager, LevelProgression/ProgressionFormulas, CollateralKarma/DestructionTracker, EnemyMutatorSystem, ObjectPoolManager, SpatialHashGrid, FlowFieldPathfinder, InputBuffer/InputBufferState.
- Autoloads (project.godot): EventBus, GameManager, ObjectPoolManager.
- GameState: MainMenu, Playing, Paused, GameOver, LevelUp. (No Tutorial state.)

## Confirmed gaps for amateur → novice players
1. NO tutorial / onboarding. New players are dropped into Wave 1 with 4 heroes they don't understand and 20 perks with cryptic names. No control hints, no guided first wave.
2. NO difficulty selector. Single fixed curve (Weibull + CollateralKarma threat escalation + stacking mutators). Novices cannot soften the challenge.
3. NO perk transparency. UpgradeMenu shows only perk *names* ("+15% Damage") with no description of what they do or synergies. Worse: `UpgradeMenu.ApplyPerk()` only implements 3 of 20 perks (DamageUp is a no-op comment; SpeedUp and MaxHealthUp work; the other 17 do nothing). A novice picks a perk expecting an effect and gets nothing — erodes trust.
4. NO practice / sandbox mode. No way to try heroes, abilities, or perks without committing to a full run that ends in permadeath GameOver.
5. XP bar shows `Experience` but no `MaxValue`, so progress-to-next-level is unreadable.

## What already exists (do not duplicate)
- docs/ux-feedback-spec.md — combat-feedback juice spec (damage numbers, wave warnings, power-up feedback, low-health, pause/upgrade UX). Does NOT address onboarding/novice accessibility.
- AGENTS.md — comprehensive, accurate; extraction pattern (pure logic from Node wrappers) is the established convention for new testable logic.
- xUnit suite: 131 tests pass headlessly; pure-logic extraction pattern is the house style.

## Sources (internal, Tier 1)
- scripts/core/GameManager.cs, GameManagerState.cs
- scripts/core/WaveManager.cs, WaveFormulas.cs
- scripts/core/LevelProgression.cs, ProgressionFormulas.cs
- scripts/ui/UpgradeMenu.cs, HUD.cs, MainMenu.cs
- scripts/heroes/HeroBase.cs + Atlas.cs, scripts/enemies/EnemyBase.cs + Brute.cs
- scripts/systems/EnemyMutatorSystem.cs, CollateralKarma.cs
- project.godot (autoloads: EventBus, GameManager, ObjectPoolManager)
- docs/ux-feedback-spec.md (T1 genre refs: Vampire Survivors, Brotato, Hades, HoloCure)

## Recommendations (see docs/recommendations.md)
R1: Guided Tutorial / "First Wave" onboarding mode.
R2: Difficulty selector (Easy / Normal / Hard).
R3: Perk transparency + practice/sandbox mode (fix ApplyPerk to implement all 20 perks, add perk descriptions, sandbox for risk-free experimentation).

## Implementation Status (2026-10-17 — all 3 complete ✅)

**R3 — Perk stats wired to gameplay** (was the #1 silent bug: 17 of 20 perks did nothing)
- `PerkSystem.ApplyPerk()` already implemented all 20 perks with `HeroStats` container (pre-existing)
- Wired `Stats.DamageMult`, `Stats.KineticDamageMult/EnergyDamageMult`, `Stats.AoeRadiusMult`, `Stats.AttackCooldownMult`, `Stats.ProjectilePiercing`, `Stats.ProjectileCountBonus`, `Stats.DodgeCooldownMult` into all 4 heroes' Attack/Dodge methods
- Added `HeroBase.Invulnerable` property (god mode support, checked before `_isDodging` in `TakeDamage`)
- Added `EnemyBase.BecomeDummy()` (stationary target for Sandbox)
- Added `HeroStats.ResetToDefaults()`

**Sandbox / Practice mode** (new files)
- `scripts/maps/Sandbox.cs` — hero selection (4 heroes), spawn target dummies, toggle god mode, apply any perk, reset stats, back to menu
- `scenes/maps/Sandbox.tscn` — UI panel with all buttons wired
- `MainMenu.cs` + `MainMenu.tscn` — "Sandbox / Practice" button added

**R2 — Difficulty selector fully wired** (was partially implemented in GameManagerState)
- `WaveManager.BeginNextWave()` now reads `GameManager.Instance.DifficultySettings` and passes `EnemyCountMultiplier` to `WaveFormulas.CalculateWaveEnemyCount`
- `WaveManager` uses `BreatherSeconds` per difficulty
- `WaveManager.SpawnEnemy()` passes `MutatorScale` to `EnemyMutatorSystem.ApplyMutators(enemy, threatLevel, mutatorScale)`
- `EnemyMutatorSystem.ApplyMutators()` accepts `mutatorScale` parameter (Easy=0.5×, Normal=1.0×, Hard=1.5×)
- `WaveManager._PhysicsProcess` guarded to skip spawning when `CurrentState != GameState.Playing`

**R1 — Guided Tutorial / "First Wave" onboarding** (new files)
- `scripts/ui/Tutorial.cs` — step machine (Move→Attack→Dodge→CollectXP→LevelUp→Done), controlled slow drone spawns (1–3, one at a time), bonus XP for fast level-up, on-screen prompts
- `scenes/ui/Tutorial.tscn` — CanvasLayer with prompt and progress labels
- `scripts/core/EventBus.cs` — added `OnPerkChosen` event + `EmitPerkChosen()`
- `scripts/ui/UpgradeMenu.cs` — `SelectPerk()` emits `OnPerkChosen`
- `scripts/ui/MainMenu.cs` + `scenes/ui/MainMenu.tscn` — "Tutorial / First Wave" button, `StartTutorial()`, `OnSceneChanged()` handles `PendingTutorial`
- `scripts/core/MainBootstrap.cs` — adds Tutorial node when `PendingTutorial || CurrentState == Tutorial`
- `GameState.Tutorial`, `InTutorial`, `StartTutorial()`, `CompleteTutorial()` already existed in GameManagerState/GameManager
