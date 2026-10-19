using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Quest mode progression: 60 quests across 6 chapters of 10 levels each,
    /// modeled on Crimsonland's Quest mode (six chapters of ten levels with a
    /// fixed ending reached when a progression bar fills). Pure, Godot-free
    /// logic so every unlock/complete transition is headlessly testable.
    ///
    /// Each quest has a kill target; the in-quest progression bar fills as
    /// enemies die. Completing a quest unlocks the next one, and completing
    /// all 10 quests in a chapter unlocks the next chapter. Chapter 1 is
    /// unlocked by default; all others are locked until their prerequisite
    /// chapter is finished. Quest rewards (weapon/perk unlocks) carry between
    /// quests within a run and persist between runs.
    ///
    /// State is persisted to <c>user://quests.cfg</c> by the owning Node
    /// (GameManager); this class holds only the data model and transitions.
    /// </summary>
    public sealed class QuestProgression
    {
        public const int ChapterCount = 6;
        public const int QuestsPerChapter = 10;
        public const int TotalQuests = ChapterCount * QuestsPerChapter; // 60

        // ── Quest definitions ──────────────────────────────────────────────

        private static readonly QuestDef[] _quests = QuestBuilder.Build();

        /// <summary>All 60 quest definitions, indexed (chapter-1)*10 + (level-1).</summary>
        public static ReadOnlySpan<QuestDef> All => _quests;

        /// <summary>Definition for a specific chapter/level (1-based).</summary>
        public static QuestDef Get(int chapter, int level)
        {
            if (chapter < 1 || chapter > ChapterCount || level < 1 || level > QuestsPerChapter)
                throw new ArgumentOutOfRangeException($"Quest {chapter}-{level} is out of range.");
            return _quests[(chapter - 1) * QuestsPerChapter + (level - 1)];
        }

        /// <summary>Definition for a flat quest index 0..59.</summary>
        public static QuestDef GetByIndex(int index)
        {
            if (index < 0 || index >= TotalQuests)
                throw new ArgumentOutOfRangeException($"Quest index {index} is out of range.");
            return _quests[index];
        }

        // ── Persisted state ────────────────────────────────────────────────

        /// <summary>Highest unlocked chapter (1-based). Chapter 1 is always unlocked.</summary>
        public int HighestUnlockedChapter { get; private set; } = 1;

        /// <summary>Set of completed flat quest indices.</summary>
        public HashSet<int> CompletedQuests { get; } = new();

        /// <summary>Best kill count achieved per flat quest index (for leaderboards/stars).</summary>
        public Dictionary<int, int> BestKills { get; } = new();

        /// <summary>Weapons unlocked across runs (persisted).</summary>
        public HashSet<WeaponId> UnlockedWeapons { get; } = new() { WeaponId.Pistol };

        /// <summary>Perks unlocked across runs (persisted).</summary>
        public HashSet<PerkType> UnlockedPerks { get; } = new();

        // ── Active run state ───────────────────────────────────────────────

        /// <summary>Currently active quest, or null when not in a quest.</summary>
        public QuestDef? ActiveQuest { get; private set; }

        /// <summary>Kills accumulated in the active quest (fills the progression bar).</summary>
        public int ActiveQuestKills { get; private set; }

        /// <summary>True when the active quest's kill target is met.</summary>
        public bool ActiveQuestComplete => ActiveQuest != null && ActiveQuestKills >= ActiveQuest.TargetKills;

        // ── Transitions ────────────────────────────────────────────────────

        /// <summary>Start a quest. Returns false when the quest is locked or
        /// already completed.</summary>
        public bool StartQuest(int chapter, int level)
        {
            var def = Get(chapter, level);
            int index = FlatIndex(chapter, level);
            if (!IsUnlocked(chapter, level)) return false;
            if (CompletedQuests.Contains(index)) return false;

            ActiveQuest = def;
            ActiveQuestKills = 0;
            return true;
        }

        /// <summary>Record one kill in the active quest.</summary>
        public void RecordKill()
        {
            if (ActiveQuest == null) return;
            ActiveQuestKills++;
        }

        /// <summary>Complete the active quest (caller should verify
        /// <see cref="ActiveQuestComplete"/> first). Marks it complete,
        /// unlocks the next quest/chapter, and grants rewards. Returns the
        /// flat index of the quest just completed.</summary>
        public int CompleteActiveQuest()
        {
            if (ActiveQuest == null) return -1;
            int index = FlatIndex(ActiveQuest.Chapter, ActiveQuest.Level);
            CompletedQuests.Add(index);
            BestKills.TryGetValue(index, out int prev);
            BestKills[index] = Math.Max(prev, ActiveQuestKills);

            // Grant rewards
            if (ActiveQuest.RewardWeapon.HasValue)
                UnlockedWeapons.Add(ActiveQuest.RewardWeapon.Value);
            if (ActiveQuest.RewardPerk.HasValue)
                UnlockedPerks.Add(ActiveQuest.RewardPerk.Value);

            // Unlock the next quest in the chapter; when the chapter is fully
            // cleared, unlock the next chapter (Crimsonland chapter gating).
            int nextIndex = index + 1;
            int nextChapter = ActiveQuest.Chapter;
            int nextLevel = ActiveQuest.Level + 1;
            if (nextLevel > QuestsPerChapter)
            {
                nextLevel = 1;
                nextChapter++;
                if (nextChapter <= ChapterCount && nextChapter > HighestUnlockedChapter)
                    HighestUnlockedChapter = nextChapter;
            }
            else
            {
                // The next quest index is unlocked by virtue of being the
                // immediate successor of a completed quest.
            }

            ActiveQuest = null;
            ActiveQuestKills = 0;
            return index;
        }

        /// <summary>Abandon the active quest without completing it.</summary>
        public void AbortActiveQuest() => ActiveQuest = null;

        // ── Queries ────────────────────────────────────────────────────────

        /// <summary>True when <paramref name="chapter"/>/<paramref name="level"/>
        /// is unlocked (chapter 1 always; later chapters gated on full chapter
        /// clears; individual quests gated on prior quest completion).</summary>
        public bool IsUnlocked(int chapter, int level)
        {
            if (chapter < 1 || chapter > ChapterCount || level < 1 || level > QuestsPerChapter)
                return false;
            if (chapter == 1 && level == 1) return true;

            // Chapter gate: can only play chapter N if chapter N-1 is fully cleared.
            if (chapter > HighestUnlockedChapter) return false;

            // Within a chapter, quest N requires quest N-1 complete (except level 1).
            if (level > 1)
            {
                int prevIndex = FlatIndex(chapter, level - 1);
                if (!CompletedQuests.Contains(prevIndex)) return false;
            }
            return true;
        }

        /// <summary>Progression bar value for the active quest (0..1).</summary>
        public float ActiveQuestProgress
        {
            get
            {
                if (ActiveQuest == null) return 0f;
                float p = (float)ActiveQuestKills / ActiveQuest.TargetKills;
                return p < 0f ? 0f : (p > 1f ? 1f : p);
            }
        }

        /// <summary>Number of completed quests (0..60).</summary>
        public int CompletedCount => CompletedQuests.Count;

        /// <summary>True when all 60 quests are complete (game beaten).</summary>
        public bool IsBeaten => CompletedQuests.Count >= TotalQuests;

        private static int FlatIndex(int chapter, int level) =>
            (chapter - 1) * QuestsPerChapter + (level - 1);
    }

    /// <summary>
    /// A single quest definition: kill target, optional time limit, and
    /// rewards that carry between runs.
    /// </summary>
    public sealed class QuestDef
    {
        public int Chapter { get; init; }
        public int Level { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int TargetKills { get; init; } = 20;
        public float TimeLimitSeconds { get; init; } = 0f; // 0 = no time limit
        public int RewardXp { get; init; } = 50;
        public WeaponId? RewardWeapon { get; init; }
        public PerkType? RewardPerk { get; init; }

        /// <summary>Human-readable display label "Chapter X – Level Y: Name".</summary>
        public string DisplayLabel => $"Chapter {Chapter} – Level {Level}: {Name}";
    }

    static class QuestBuilder
    {
        public static QuestDef[] Build()
        {
            var q = new List<QuestDef>(60);

            // Helpers to reduce noise.
            WeaponId? w(WeaponId id) => id;
            PerkType? p(PerkType id) => id;

            // ── Chapter 1: The Wasteland (intro, low kill targets) ─────────
            q.Add(new() { Chapter = 1, Level = 1, Name = "First Contact", Description = "Survive the initial drone swarm.", TargetKills = 15, RewardXp = 30, RewardWeapon = w(WeaponId.Pistol) });
            q.Add(new() { Chapter = 1, Level = 2, Name = "Drone Nest", Description = "Clear the drone hatchery.", TargetKills = 20, RewardXp = 40 });
            q.Add(new() { Chapter = 1, Level = 3, Name = "Brute Force", Description = "Brutes have arrived. Put them down.", TargetKills = 18, RewardXp = 45 });
            q.Add(new() { Chapter = 1, Level = 4, Name = "Sprinter Swarm", Description = "Fast targets; don't let them close.", TargetKills = 25, RewardXp = 50 });
            q.Add(new() { Chapter = 1, Level = 5, Name = "Artillery Barrage", Description = "Outrange the artillery.", TargetKills = 20, RewardXp = 55 });
            q.Add(new() { Chapter = 1, Level = 6, Name = "Shield Wall", Description = "Shielded drones block your shots.", TargetKills = 22, RewardXp = 60 });
            q.Add(new() { Chapter = 1, Level = 7, Name = "Healer Priority", Description = "Kill the healers first.", TargetKills = 20, RewardXp = 65 });
            q.Add(new() { Chapter = 1, Level = 8, Name = "Exploder Hazard", Description = "Keep your distance from exploders.", TargetKills = 18, RewardXp = 70 });
            q.Add(new() { Chapter = 1, Level = 9, Name = "Burrower Ambush", Description = "Enemies rise from the ground.", TargetKills = 24, RewardXp = 75 });
            q.Add(new() { Chapter = 1, Level = 10, Name = "Wasteland Boss", Description = "An Apex leads the wasteland force.", TargetKills = 30, TimeLimitSeconds = 180f, RewardXp = 100, RewardPerk = p(PerkType.DamageUp) });

            // ── Chapter 2: Ruined Metropolis (moderate, mixed types) ───────
            q.Add(new() { Chapter = 2, Level = 1, Name = "Metropolis Entry", Description = "Secure the city streets.", TargetKills = 25, RewardXp = 80, RewardWeapon = w(WeaponId.SubmachineGun) });
            q.Add(new() { Chapter = 2, Level = 2, Name = "Concrete Canyon", Description = "Narrow alleys, heavy resistance.", TargetKills = 30, RewardXp = 90 });
            q.Add(new() { Chapter = 2, Level = 3, Name = "Tower Defense", Description = "Hold the rooftop position.", TargetKills = 35, RewardXp = 100 });
            q.Add(new() { Chapter = 2, Level = 4, Name = "Subway Swarm", Description = "Enemies pour from the tunnels.", TargetKills = 40, RewardXp = 110 });
            q.Add(new() { Chapter = 2, Level = 5, Name = "Skyline Snipers", Description = "Long-range threats from above.", TargetKills = 30, RewardXp = 105 });
            q.Add(new() { Chapter = 2, Level = 6, Name = "Karma Incursion", Description = "Destroy the environment to raise threat.", TargetKills = 35, RewardXp = 115 });
            q.Add(new() { Chapter = 2, Level = 7, Name = "Healer Relay", Description = "Medics keep the wave alive.", TargetKills = 40, RewardXp = 120 });
            q.Add(new() { Chapter = 2, Level = 8, Name = "Brute Crush", Description = "Heavy armor, slow advance.", TargetKills = 30, RewardXp = 110 });
            q.Add(new() { Chapter = 2, Level = 9, Name = "Parasite Hatch", Description = "Destroy parasites before they split.", TargetKills = 38, RewardXp = 125 });
            q.Add(new() { Chapter = 2, Level = 10, Name = "Metropolis Apex", Description = "The Apex commands the district.", TargetKills = 45, TimeLimitSeconds = 210f, RewardXp = 160, RewardPerk = p(PerkType.AttackSpeedUp) });

            // ── Chapter 3: Volcano Crater (high intensity, fire enemies) ───
            q.Add(new() { Chapter = 3, Level = 1, Name = "Magma Shore", Description = "Lava drones patrol the rim.", TargetKills = 35, RewardXp = 140, RewardWeapon = w(WeaponId.Shotgun) });
            q.Add(new() { Chapter = 3, Level = 2, Name = "Caldera Assault", Description = "Push into the crater.", TargetKills = 45, RewardXp = 150 });
            q.Add(new() { Chapter = 3, Level = 3, Name = "Ash Storm", Description = "Visibility reduced; sprinters stalk.", TargetKills = 50, RewardXp = 160 });
            q.Add(new() { Chapter = 3, Level = 4, Name = "Thermal Vents", Description = "Heat-seeking artillery.", TargetKills = 40, RewardXp = 155 });
            q.Add(new() { Chapter = 3, Level = 5, Name = "Ember Walkers", Description = "Fire-resistant brutes.", TargetKills = 42, RewardXp = 150 });
            q.Add(new() { Chapter = 3, Level = 6, Name = "Ignis Incursion", Description = "Pyromancer cultists summon flame.", TargetKills = 48, RewardXp = 170 });
            q.Add(new() { Chapter = 3, Level = 7, Name = "Magma Burrowers", Description = "Burrowers erupt from lava.", TargetKills = 46, RewardXp = 165 });
            q.Add(new() { Chapter = 3, Level = 8, Name = "Crater Exploders", Description = "Chain explosions in tight quarters.", TargetKills = 40, RewardXp = 150 });
            q.Add(new() { Chapter = 3, Level = 9, Name = "Volcanic Apex", Description = "An Apex guards the caldera heart.", TargetKills = 55, TimeLimitSeconds = 240f, RewardXp = 200, RewardPerk = p(PerkType.ExplosiveRounds) });
            q.Add(new() { Chapter = 3, Level = 10, Name = "Eruption Finale", Description = "Survive the volcano's wrath.", TargetKills = 70, TimeLimitSeconds = 300f, RewardXp = 250, RewardWeapon = w(WeaponId.GrenadeLauncher) });

            // ── Chapter 4: Orbital Platform (high-tech, drones + artillery) ─
            q.Add(new() { Chapter = 4, Level = 1, Name = "Airlock Breach", Description = "Drones in zero-G corridors.", TargetKills = 40, RewardXp = 190, RewardWeapon = w(WeaponId.AssaultRifle) });
            q.Add(new() { Chapter = 4, Level = 2, Name = "Data Center", Description = "Defend the server core.", TargetKills = 50, RewardXp = 200 });
            q.Add(new() { Chapter = 4, Level = 3, Name = "Solar Array", Description = "Open platforms, artillery fire.", TargetKills = 48, RewardXp = 195 });
            q.Add(new() { Chapter = 4, Level = 4, Name = "Drone Factory", Description = "Endless drones from assembly lines.", TargetKills = 60, RewardXp = 210 });
            q.Add(new() { Chapter = 4, Level = 5, Name = "Shielded Grid", Description = "Shielded drones protect the array.", TargetKills = 52, RewardXp = 205 });
            q.Add(new() { Chapter = 4, Level = 6, Name = "Healer Network", Description = "Medics link drones into squads.", TargetKills = 58, RewardXp = 220 });
            q.Add(new() { Chapter = 4, Level = 7, Name = "Platform Parasites", Description = "Infestation spreads through vents.", TargetKills = 55, RewardXp = 215 });
            q.Add(new() { Chapter = 4, Level = 8, Name = "Gravity Well", Description = "Burrowers warp through the deck.", TargetKills = 50, RewardXp = 200 });
            q.Add(new() { Chapter = 4, Level = 9, Name = "Orbital Apex", Description = "The Apex controls the platform.", TargetKills = 65, TimeLimitSeconds = 270f, RewardXp = 260, RewardPerk = p(PerkType.PiercingShots) });
            q.Add(new() { Chapter = 4, Level = 10, Name = "Station Lockdown", Description = "Clear the station or lose it.", TargetKills = 80, TimeLimitSeconds = 300f, RewardXp = 300 });

            // ── Chapter 5: Subterranean Lab (biological, parasites + healers) ─
            q.Add(new() { Chapter = 5, Level = 1, Name = "Lab Entrance", Description = "Parasites in the ventilation.", TargetKills = 45, RewardXp = 240, RewardWeapon = w(WeaponId.PlasmaRifle) });
            q.Add(new() { Chapter = 5, Level = 2, Name = "Bio-Containment", Description = "Containment breached on all levels.", TargetKills = 55, RewardXp = 250 });
            q.Add(new() { Chapter = 5, Level = 3, Name = "Mutagen Vats", Description = "Mutated brutes in the vats.", TargetKills = 50, RewardXp = 245 });
            q.Add(new() { Chapter = 5, Level = 4, Name = "Spawning Pools", Description = "Parasites hatch continuously.", TargetKills = 65, RewardXp = 260 });
            q.Add(new() { Chapter = 5, Level = 5, Name = "Healer Nest", Description = "Healers sustain the brood.", TargetKills = 58, RewardXp = 255 });
            q.Add(new() { Chapter = 5, Level = 6, Name = "Toxis Cult", Description = "Alchemists weaponize acid.", TargetKills = 62, RewardXp = 270 });
            q.Add(new() { Chapter = 5, Level = 7, Name = "Acid Flood", Description = "Acid pools spread across the floor.", TargetKills = 60, RewardXp = 265 });
            q.Add(new() { Chapter = 5, Level = 8, Name = "Lab Burrowers", Description = "Tunneling through reinforced walls.", TargetKills = 54, RewardXp = 250 });
            q.Add(new() { Chapter = 5, Level = 9, Name = "Subterranean Apex", Description = "The Apex directs the infestation.", TargetKills = 75, TimeLimitSeconds = 300f, RewardXp = 320, RewardPerk = p(PerkType.SlowOnHit) });
            q.Add(new() { Chapter = 5, Level = 10, Name = "Patient Zero", Description = "Destroy the source. No survivors.", TargetKills = 90, TimeLimitSeconds = 360f, RewardXp = 400 });

            // ── Chapter 6: The Hive (final, all enemy types, max intensity) ─
            q.Add(new() { Chapter = 6, Level = 1, Name = "Hive Gate", Description = "Every enemy type at once.", TargetKills = 60, RewardXp = 350, RewardWeapon = w(WeaponId.Railgun) });
            q.Add(new() { Chapter = 6, Level = 2, Name = "Drone Hatchery", Description = "Endless drones from the queen.", TargetKills = 80, RewardXp = 370 });
            q.Add(new() { Chapter = 6, Level = 3, Name = "Brute Line", Description = "An unbroken wall of brutes.", TargetKills = 70, RewardXp = 360 });
            q.Add(new() { Chapter = 6, Level = 4, Name = "Sprinter Storm", Description = "Too many fast targets to count.", TargetKills = 90, RewardXp = 380 });
            q.Add(new() { Chapter = 6, Level = 5, Name = "Artillery Rain", Description = "Constant bombardment.", TargetKills = 75, RewardXp = 375 });
            q.Add(new() { Chapter = 6, Level = 6, Name = "Shielded Phalanx", Description = "Shielded drones in formation.", TargetKills = 80, RewardXp = 385 });
            q.Add(new() { Chapter = 6, Level = 7, Name = "Healer Matrix", Description = "Interlinked healers.", TargetKills = 85, RewardXp = 390 });
            q.Add(new() { Chapter = 6, Level = 8, Name = "Exploder Chain", Description = "One explosion triggers them all.", TargetKills = 70, RewardXp = 380 });
            q.Add(new() { Chapter = 6, Level = 9, Name = "Parasite Wave", Description = "The brood never ends.", TargetKills = 100, RewardXp = 400 });
            q.Add(new() { Chapter = 6, Level = 10, Name = "Hive Queen", Description = "Final quest. The Apex of apexes.", TargetKills = 120, TimeLimitSeconds = 420f, RewardXp = 600, RewardPerk = p(PerkType.MaxHealthUp) });

            return q.ToArray();
        }
    }
}
