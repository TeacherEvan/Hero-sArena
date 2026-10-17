# Hero's Arena — Gameplay-Mechanic Research Findings

Tier 1-2 external research grounding recommendations for a Godot 4.3 C# top-down arena shooter / wave-survival game (4 hero classes, perks, Collateral Karma environmental destruction, Weibull wave spawning).

**Tier key:** Tier 1 = official docs / vendor / RFC. Tier 2 = conference talk / official repo issue / accepted Stack Overflow / canonical wiki of record containing exact game data. Sources are prioritized Tier 1–2; marginal returns have diminished, so research stops here.

---

## Area 1 — Wave survival / horde shooter difficulty curves that do NOT decay late-game

### 1.1 Vampire Survivors — uncapped per-minute enemy stat scaling + Endless Mode overtime
**[Tier 2: Vampire Survivors Wiki (Stages)]** https://vampire.survivors.wiki/w/Stages

**Key fact / quote:** Challenge stages apply per-minute scaling "without a cap":
- Bone Zone: "Every minute, the enemies' health and speed scale by 15% and 2.5% respectively, without a cap."
- Astral Stair / Bat Country / Tiny Bridge: "Every minute, the enemies' health and speed scale by 10% and 1.25% respectively, without a cap."
- Endless Mode (post time-limit): "Enemies gain +100% of their base HP, their spawn frequency and quantity increase by 50%, their damage dealt increases by 25%, and the player's maximum incoming damage cap decreases by 1" per cycle. The Reaper no longer spawns at the time limit; waves restart from minute 0 and run indefinitely.

**Relevance to Hero's Arena:** Direct template for a difficulty curve that never decays. An uncapped per-minute HP/speed multiplier (tunable per stage) keeps late-game waves escalating; an "Endless Mode" post-30-minute cycle with compounding HP/spawn/damage and a shrinking player damage cap guarantees run termination — a natural fit for the existing Weibull wave spawning and a late-game "overtime" boss wave.

### 1.2 Brotato — exponential "Endless Factor" difficulty formula with hard caps
**[Tier 2: Brotato Wiki (Endless Mode)]** https://brotato.wiki.spellsandguns.com/Endless_Mode

**Key fact / quote:** After Wave 20, an `Endless Factor` grows (starts at 0, accelerates after Wave 35):
`Endless_wave = max(0, current_wave − 20)`
`Endless_mult = 2.0 + max(0, (current_wave − 35) × 0.2)`
`Endless_factor = max(0, ((endless_wave × (endless_wave + 1)) / 100)) × endless_mult`
This single factor multiplies enemy damage (+100% of factor), enemy Max HP (+225% of factor), enemy speed (capped at +175%), shop prices, reroll cost, and *reduces* player iframe duration by `1 / max(1, factor)`. Health-percent damage items lose effectiveness after Wave 39. Wiki note: "This ensures that all runs come to an end."

**Relevance to Hero's Arena:** A clean mathematical model for an "overtime" escalation that cannot be out-statted. Shows two useful levers: (a) cap player defensive stats (iframes) so the late game stays dangerous, and (b) deliberately nullify %-of-health damage so Collateral Karma / %-damage perks don't trivialize high-HP late waves.

### 1.3 Risk of Rain 2 — scaling difficulty coefficient, exponential loop jumps, enemy-level gating
**[Tier 2: Risk of Rain 2 Wiki (wiki.gg official host)]** https://riskofrain2.wiki.gg/wiki/Difficulty

**Key fact / quote:** "Scaling difficulty is a core mechanic of Risk of Rain 2. As a run progresses, the difficulty will steadily rise, making enemies both stronger and more numerous."
`coeff = (playerFactor + timeInMinutes × timeFactor) × stageFactor`, where `playerFactor = 1 + 0.3×(playerCount−1)`, `timeFactor = 0.0506 × difficultyValue × playerCount^0.2`, `stageFactor = 1.15^stagesCompleted`.
"Every time an environment is completed ... the difficulty will jump forward ... This increase is exponential, which means that as more Loops are completed, the game becomes exponentially harder." Enemies gain 30% HP and 20% damage per level; as coeff rises, weaker enemies stop spawning entirely and stronger enemies (minibosses, elites) spawn outside scripted events.

**Relevance to Hero's Arena:** Time-based + milestone-based difficulty that never decays, with explicit exponential jumps at wave/loop transitions — a model for difficulty spikes at Weibull wave boundaries and for "stronger enemies spawn outside scheduled events" late in a run (ties into Collateral Karma destruction spawning tougher threats).

### 1.4 Vampire Survivors — hard 30-minute run cap + Reaper overtime mechanic
**[Tier 2: GDC Festival of Gaming — "Surviving 'Vampire Survivors': Agile Community Management"]** https://www.youtube.com/watch?v=vfMhgSTVrNs

**Key fact / quote:** A run is 30 minutes; "after that the grim reaper shows up and ... rips you apart." The Reaper appears every minute after the time limit to end the run; optimized builds can break the game and survive ~34–35 minutes against Reapers. The design intentionally creates a hard time cap so even unskilled players feel progress early, while skill matters most at the 15–20 minute mark with "another spike" later.

**Relevance to Hero's Arena:** A hard run cap (e.g., 30 min) plus an overtime "Reaper wave" that escalates every minute gives late-game tension and a natural end condition — prevents late-game decay by making survival, not power, the scarce resource.

---

## Area 2 — Roguelite perk/synergy systems that create real emergent build combos

### 2.1 Hades — Duo Boons: 28 two-god combination boons with prerequisites
**[Tier 2: Hades Wiki (Duo Boons)]** https://hades.fandom.com/wiki/Duo_Boons

**Key fact / quote:** "Duo Boons are a type of Boon which combine the powers of two different gods for a unique effect. Duo Boons can only be obtained when you have the prerequisite boons ... In Hades there are 28 Duo Boons, shared between 8 different gods ... Every god that may have Duo boons shares one with each other god." Concrete examples:
- **Curse of Longing** (Aphrodite + Ares): "Your Doom effects continuously strike Weak foes."
- **Lightning Phalanx** (Athena + Zeus): "Your Cast bounces between nearby enemies. Max Bounces: 3"
- **Sea Storm** (Poseidon + Zeus): "Your knock-away effects also cause foes to be struck by lightning."
- **Heart Rend** (Aphrodite + Artemis): "Your Critical hits do more damage to Weak enemies. Bonus Critical Damage: +150%"
Duo Boons are lost on death; discovery odds can be raised via Keepsakes, Mirror of Night talent "Gods' Legacy," etc.

**Relevance to Hero's Arena:** The clearest example of non-flat-stat synergies: two single perks must both be taken to unlock a third, unique combo effect. Prerequisite boons *telegraph* the synergy (the game signals which pairs combine). Hero's Arena's perk system can adopt "combo perks" unlocked only when two related perks are present (e.g., explosion perk + knockback perk → chain-reaction Collateral Karma explosions).

### 2.2 Hades — god-themed boons telegraph synergies through consistent identities
**[Tier 2: IGN — "Hades 2 Best Boons"; Daar on Games — "Hades vs Hades II: How they approach boons"]** https://www.ign.com/wikis/hades-2/Best_Boons  https://daarongames.substack.com/p/hades-vs-hades-ii-boon-design

**Key fact / quote:** Each god has a unique, consistent gameplay identity so players can plan builds: "Zeus's boon which makes each hit produce a lightning bolt that bounces between enemies, favoring frequent attacks instead ... Dionysus's boons cause damage over time ... Aphrodite's boon makes enemies deal less damage." Concrete synergy: "if you're planning on going heavy on Zeus Boons, look to get Heavenly Strike to add Blitz to your attacks, and then go for Boons that synergize with Blitz, like Electric Overload, which fires chain lightning every time Blitz procs." Artemis's "Support Fire" adds an extra arrow per hit; stacking attack speed makes it "rain damage."

**Relevance to Hero's Arena:** Synergies are readable because each perk "school" has one mechanical theme. For Hero's Arena, give each perk tree a distinct mechanic (e.g., one tree = knockback/physics, one = DoT/burn, one = cooldown reduction) so combos are discoverable without a wiki, and name perks so the combo is telegraphed in the UI.

### 2.3 Risk of Rain 2 — stackable items create emergent build variety
**[Tier 2: Risk of Rain 2 Wiki (wiki.gg); Hopoo Games site]** https://riskofrain2.wiki.gg/wiki/  https://hopoogames.com

**Key fact / quote:** Items stack with explicit per-stack values, e.g. "Ghor's Tome — 4% (+4% on stack) chance on kill to drop a treasure ... Scales over time." Many effects "scale over time," so late-game power comes from item *count and combination*, not flat level stats. Emergent builds arise from combining crowd-control, healing-on-kill, and AoE items (documented in community build guides and the "125x Difficulty Scaling" analysis).

**Relevance to Hero's Arena:** Let perks stack (each copy adds a % or flat value) so build power scales with investment rather than hitting a flat-stat ceiling; stacking plus complementary perk pairs yields emergent combos without new code per combo.

---

## Area 3 — Meta-progression / persistent between-run systems

### 3.1 Hades — Mirror of Night: Darkness currency, permanent talents, gated unlocks
**[Tier 2: Hades Wiki (Mirror of Night)]** https://hades.fandom.com/wiki/Mirror_of_Night

**Key fact / quote:** "It is one of the main meta progression systems in Hades. Reflecting at the Mirror gives Zagreus the ability to spend Darkness gathered from the Underworld to improve his own abilities." Talents are permanent across runs; unlocking/upgrading costs Darkness (earned per run) and many are gated behind Chthonic Keys. Total to max: 35,365 Darkness. Examples:
- **Death Defiance:** "Each rank restores you for 50% ... 1 time when your Life Total is depleted" (3 ranks, +3 extra lives).
- **Greater Reflex:** "Each rank lets you chain +1 Dash before briefly recovering."
- **Shadow Presence / Fiery Presence:** conditional damage bonuses (+10%/+15% per rank).
- Boon-rarity talents: "Gods' Legacy — Each rank gives you +1% greater chance for a Boon to be Legendary or a Duo."
Spent Darkness can be refunded (1 Chthonic Key) to respec between runs.

**Relevance to Hero's Arena:** Currency awarded on each run (even on death) spent on permanent between-run power is the classic retention loop. Gating upgrades behind a second currency (keys) prevents immediate maxing and gives players meaningful choices; refundability reduces respec friction. Hero's Arena's between-run tree should follow this: run-currency → permanent nodes, gated by unlock keys, with conditional (not flat) bonuses so power grows without removing challenge.

### 3.2 Dead Cells — Runes and permanent upgrades: cells currency + blueprints + path unlocks
**[Tier 2: Dead Cells Wiki (Runes and upgrades, wiki.gg official host)]** https://deadcells.wiki.gg/wiki/Runes_and_upgrades

**Key fact / quote:** "Upgrades are general improvements purchased from the Collector for cells, and function as improvements ... They are permanent and last between runs. Most are only available after completing a certain amount of unlocks and others must be acquired as Blueprints from specific locations before they are available." Runes unlock alternate paths/biomes (Metroidvania-style gating) — e.g., Vine Rune, Teleportation Rune — permanently opening new routes between runs. Weapon blueprints unlock new items that can then spawn in future runs.

**Relevance to Hero's Arena:** Cells-on-death currency plus permanent unlock trees (new weapons, new perks, new wave types) is a proven retention model. Unlocking new *paths* or *content* (not just stats) keeps runs fresh after the power curve flattens — worth considering for Hero's Arena's class/perk unlock tree.

### 3.3 Hades — GDC Podcast with Greg Kasavin on roguelike meta-progression philosophy
**[Tier 2: GDC Podcast ep. 16 — "Roguelikes And Narrative Design With Hades Creative Director Greg Kasavin"]** https://gdconf.com/article/roguelikes-and-narrative-design-with-hades-creative-director-greg-kasavin-gdc-podcast-ep-16

**Key fact / quote:** Supergiant's goal was roguelikes "that aren't just about you know getting really far and then you know and then dying and then rage quitting" — meta-progression and narrative persistence across deaths make failure feel like progress rather than a wasted run.

**Relevance to Hero's Arena:** Between-run systems should make death feel like progress (permanent unlocks, story/hero advancement), not a reset to zero — directly supports retention in a wave-survival game where runs end in death.

---

## Area 4 — Class/hero ability design in hero shooters (cooldowns, ultimates, i-frames, dodge, balance)

### 4.1 Overwatch 2 — cooldowns and ultimates tuned as primary balance levers
**[Tier 1: Overwatch 2 Developer Blog — Blizzard Entertainment]** https://overwatch.blizzard.com/en-us/news/23824005/overwatch-2-developer-blog-tuning-hero-balance-for-launch

**Key fact / quote:** Balance is delivered primarily through cooldown and ultimate-cost tuning:
- Junker Queen Commanding Shout: cooldown 11 → 15 s; health bonus 100 → 50 HP.
- Orisa Energy Javelin cooldown 8 → 6 s (to "keep her feeling active").
- D.Va Micro Missiles cooldown 8 → 7 s; Call Mech ultimate cost −12%.
- Temporary health now grants 50% reduced ultimate charge (previously zero) so defensive cooldowns still feed the offense economy.
- All heroes refund up to 30% ultimate power on hero swap.

**Relevance to Hero's Arena:** Cooldown length is the main dial for ability power; longer cooldowns make use "more deliberate and less forgiving." Ultimates are priced separately from basic abilities and can be discounted/increased to balance a hero. For Hero's Arena's 4 hero classes, tune class ability cooldowns independently and cost ultimates in a separate charge meter — avoids one ability dominating balance.

### 4.2 Valorant — per-round ability economy, cooldowns, and ultimate charge
**[Tier 1: Valorant Patch Notes 3.0 — Riot Games]** https://playvalorant.com/en-gb/news/game-updates/valorant-patch-notes-3-0

**Key fact / quote:** "Our goal here is to create a healthier pace of abilities throughout a round and force you to think more critically about your purchase decisions." Mechanics:
- Signature abilities provide a minimum of one charge per round; charges gained from cooldowns are temporary (do not carry over).
- Cooldowns tuned per ability: Astra Nova Pulse 12 → 25 s; Recon Bolt 35 → 40 s; Omen Dark Cover 35 → 40 s.
- Ultimates cost points (Sage Resurrection 7 → 8; Raze Showstopper 7 → 8; Jett Bladestorm 6 → 7).
- Abilities also cost credits to buy each round (e.g., Barrier Orb 300 → 400 credits).

**Relevance to Hero's Arena:** A per-wave or per-round charge economy plus cooldowns prevents ability spam and forces tradeoffs. Ultimates as a separate, slowly-charged resource earned by performance (kills/objectives) fits a hero-shooter class design where each of the 4 classes has a signature ability (short cooldown) and an ultimate (long charge).

### 4.3 Team Fortress 2 — class roles differentiated by stats, weapons, and visual identity
**[Tier 2: Official TF2 Wiki (Classes)]** https://wiki.teamfortress.com/wiki/Classes

**Key fact / quote:** Nine classes split into Offensive / Defensive / Support roles. "Support classes ... cannot hold the fight on their own, but their specialized abilities can tip the balance." Each class has distinct health, speed, size, and weapon set; e.g., the Medic "offers a mobile means of healing for teammates, capable of overhealing them to 150% of their maximum." Valve designed each class "to be visually distinct, even at range."

**Relevance to Hero's Arena:** Four hero classes should be differentiated by more than damage numbers — distinct health/speed/size, a unique primary mechanic (tank/support/dps/control), and clear visual readability so players can identify a class and its threat at a glance, mirroring TF2's role-based differentiation.

---

## Summary of cross-cutting recommendations for Hero's Arena

| Design area | Proven pattern (source) | Application |
|---|---|---|
| Late-game difficulty that never decays | Uncapped per-min HP/speed scaling; Endless Mode compounding cycles (Vampire Survivors); exponential Endless Factor (Brotato); difficulty coefficient + loop jumps (Risk of Rain 2) | Weibull waves get per-minute + per-cycle HP/speed/spawn multipliers with no cap; post-cap "overtime" wave escalates until run ends |
| Preventing late-game trivialization | Cap defensive stats (iframes shrunk by Endless Factor); nullify %-of-health damage late (Brotato); shrinking player damage cap (Vampire Survivors Endless) | Cap Collateral Karma / %-damage perks at high wave counts; reduce player i-frames late |
| Emergent perk combos (not flat stats) | Duo Boons requiring two prerequisite single boons (Hades 28 combos); consistent god-themed identities (Hades) | Combo perms unlocked only when two related perks are held; each perk tree has one readable mechanic |
| Meta-progression retention | Darkness-per-run spent on permanent Mirror-of-Night talents, gated by keys, refundable (Hades); cells + blueprints + path unlocks (Dead Cells) | Between-run currency earned even on death; permanent unlock tree gated by a second currency; unlock new classes/perks/wave types |
| Class ability balance | Cooldown and ultimate-cost as primary balance knobs (Overwatch 2); per-round charge economy + separate ult meter (Valorant); role + stat + visual differentiation (TF2) | Tune each hero's ability cooldowns independently; ultimates on a separate charge meter earned by performance; 4 classes = offense/defense/support/control with distinct stats & silhouettes |
