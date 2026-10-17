# Investigation Notes — Gameplay Mechanics (External Resources)

**Date:** 2026-10-17
**Target:** Hero's Arena (Godot 4.3 + C#), top-down arena shooter, wave survival
**Mode:** Investigator (external research + gameplay recommendations; no direct build)
**Strategy:** pattern-driven + community-pulse
**Deliverable:** 3 gameplay-mechanic recommendations

## Current mechanics observed (codebase)
- Wave survival: `WaveManager` spawns via Weibull intensity (`WaveFormulas`) that peaks around wave 5 then decays; 5s breather between waves; boss every 300s; 2000 enemy cap.
- Heroes: 4 classes (Atlas, Zephyr, Synapse, Volt); twin-stick movement; Attack/Dodge/Ability per hero.
- Perks: 20 perks, flat stat multipliers (damage, speed, survival, AoE, control), each with a synergy TAG but no actual synergy mechanics (`PerkSystem.cs`).
- Collateral Karma: environmental destruction increments threat level (every 10 destructions = +1 threat).
- Progression: XP leveling within a run; perk offers on level up.
- Difficulty: Easy/Normal/Hard persisted in `user://settings.cfg`.
- No persistent meta-progression between runs identified; each run starts from baseline.

## External research executed (searches)
- "Vampire Survivors difficulty curve late game scaling design" → Tier 3 blog (Arcade Artificer, "The Secret Sauce of Vampire Survivors"); Tier 4 Reddit r/gamedesign ("The reverse difficulty curve is fundamentally bad game design").
- "roguelite perk synergy build variety game design" → Tier 3 blog (Bugnet, "How to Design a Roguelike Item and Synergy System").

## Sources fetched & key quotes
1. Arcade Artificer (Substack, Tier 3): trickling stat progression maintains pacing better than step-like curves; "the game's difficulty inevitably catch up afterwards to maintain the pressure"; meta-progression loop keeps gold between runs, eliminates "zero progress" runs, ~2.5x damage / 2x health cumulative; "you want to retry immediately with your new perks unlocked."
2. Reddit r/gamedesign (Tier 4): reverse/decaying difficulty curve flagged as bad design.
3. Bugnet (Tier 3, 2026-06-11): items should "meaningfully change runs"; synergies combine into "exciting, emergent power"; "the combinatorial possibilities of synergizing items create vast build variety."

## What I could not check
- Did not run the Godot headless gate or `dotnet build` (out of scope for a research-only investigation; no code changes proposed here).
- Did not survey hero-shooter / class-ability specific sources (second search batch returned results but the domain was covered by roguelite/arena literature above).
