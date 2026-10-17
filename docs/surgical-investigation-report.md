# Surgical Investigation Report — Hero's Arena (Novice Onboarding)

**Mode:** Investigator (findings for review)  
**Strategy:** codebase-first  
**Date:** 2026-10-17  
**Project:** Hero's Arena (Godot 4.3 + C# .NET 8)  
**Gate:** xUnit (131 tests pass headlessly); Godot headless gate cannot run C# in this workspace

---

## 1. Mission — Stated vs Actual

**Stated (README):** "A top-down arena shooter built with Godot 4.3 and C# (.NET 8). Fight waves of enemies across 4 hero classes; environmental destruction escalates threat via the Collateral Karma system."

**Actual:** A horde/top-down arena shooter (Vampire Survivors / Brotato / Hades-like) with 4 heroes, 10 enemy types, Weibull wave-spawning curve, 2000-enemy cap, 5s breather between waves, boss every 300s, XP-driven level-up into a 20-perk upgrade menu, and Collateral Karma threat escalation.

**Gap:** No tutorial/onboarding, no difficulty selector, no perk transparency (17 of 20 perks are no-ops), no practice/sandbox mode, XP bar has no MaxValue.

---

## 2. Best-Practices Assessment

| Dimension | Rating | Finding |
|-----------|--------|---------|
| Code quality | good | Clean style; pure logic extracted from Node wrappers (AGENTS.md pattern). |
| Architecture | good | EventBus decoupling, object pooling, SpatialHashGrid O(1), FlowFieldPathfinder on background thread. |
| Testing | good | 131 xUnit tests pass headlessly. Godot C# gate unrunnable here (toolchain gap). |
| Security | good | No external services, secrets, or network deps. |
| Performance | good | Object pooling, spatial hash grid, capped spawns/frame, 2000-enemy cap. |
| Documentation | warn | AGENTS.md/README accurate; ux-feedback-spec.md covers combat juice, not onboarding. |
| Gameplay completeness | bad | UpgradeMenu.ApplyPerk() implements only 3 of 20 perks; the other 17 are no-ops. |

---

## 3. Recommendations (recommended order: R3 → R1 → R2)

### R3 — Perk Transparency + Practice/Sandbox Mode (Critical)
**Why:** 17 of 20 perks do nothing (silent bug); no safe place to experiment under permadeath.
**What:** Implement all 20 perks in `ApplyPerk()`; add perk descriptions + synergy hints in `UpgradeMenu`; add Sandbox mode (no waves/GameOver, spawn dummies, god mode, try all heroes/perks).
**Endpoint/control:** `MainMenu.OpenSandbox()` → `Sandbox.SpawnDummy()/SetGodMode()/ApplyPerk()/ResetHero()`. New `scripts/maps/Sandbox.cs` + `scenes/maps/Sandbox.tscn`.
**Effort:** ~4–5 days (1 dev).

### R1 — Guided Tutorial / "First Wave" Onboarding Mode (High)
**Why:** New players receive no instruction; Wave 1 spawns mixed enemies at full speed.
**What:** `GameState.Tutorial`, controlled slow spawns, on-screen control prompts, guaranteed level-up opening `UpgradeMenu` with 3 beginner perks, then unlock Normal mode.
**Endpoint/control:** `MainMenu.StartTutorial()` → `GameManager.StartTutorial()` → `GameState.Tutorial`. New `scripts/ui/Tutorial.cs` + `scenes/ui/Tutorial.tscn`.
**Effort:** ~3–4 days (1 dev).

### R2 — Difficulty Selector (Easy / Normal / Hard) (High)
**Why:** Single fixed Weibull + karma + mutator curve punishes novices; destroying environment raises threat.
**What:** `Difficulty` enum in `GameManagerState`. Easy: ×0.6 enemies, threat/20 destructions, ×0.5 mutators, ×1.5 XP, 8s breather. Normal: current. Hard: ×1.4 enemies, threat/8, ×1.5 mutators, ×0.8 XP.
**Endpoint/control:** `GameManagerState.SetDifficulty(Difficulty)`; `WaveManager.BeginNextWave()` reads `GameManager.Instance.Difficulty`; `EnemyMutatorSystem.ApplyMutators(enemy, threatLevel, difficulty)`.
**Effort:** ~2–3 days (1 dev).

**Estimated total:** ~9–12 days (1 dev).

---

## 4. Sources (all internal, Tier 1)

`scripts/core/GameManager.cs`, `GameManagerState.cs`; `scripts/core/WaveManager.cs`, `WaveFormulas.cs`; `scripts/core/LevelProgression.cs`, `ProgressionFormulas.cs`; `scripts/ui/UpgradeMenu.cs` (only 3/20 perks implemented); `scripts/ui/MainMenu.cs`, `HUD.cs`; `scripts/heroes/HeroBase.cs` + `Atlas.cs`; `scripts/enemies/EnemyBase.cs` + `Brute.cs`; `scripts/systems/EnemyMutatorSystem.cs`, `CollateralKarma.cs`; `project.godot` (autoloads: EventBus, GameManager, ObjectPoolManager); `docs/ux-feedback-spec.md` (combat-feedback juice spec, not onboarding); `AGENTS.md`, `README.md`.

**Not checked:** Godot headless gate cannot execute C# in this workspace. External web research not required (codebase-first).

---

## Deliverables

- `docs/surgical-investigation-report.html` — full interactive report (this summary)
- `docs/surgical-investigation-report.json` — machine-readable findings
- `docs/backend-architecture.mmd`, `docs/frontend-architecture.mmd`, `docs/research-findings.mmd`, `docs/entire-project.mmd` — Mermaid diagrams
- `docs/investigation-notes/2026-10-17-novice-onboarding.md` — full notes
- `docs/recommendations.md` — cumulative recommendations log
