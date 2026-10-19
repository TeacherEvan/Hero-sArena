using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Local achievement system modeled on Crimsonland's Steam achievements.
    /// Pure, Godot-free state: achievement definitions, unlock tracking, and
    /// progress. The owning Node (GameManager / HUD) persists unlocked ids to
    /// <c>user://achievements.cfg</c> and shows a toast when
    /// <see cref="NewlyUnlocked"/> is non-empty. Every trigger check is
    /// headlessly testable via xUnit.
    /// </summary>
    public sealed class AchievementManager
    {
        public const int TotalAchievements = 10;

        private static readonly AchievementDef[] _all = BuildAchievements();

        public static ReadOnlySpan<AchievementDef> All => _all;

        /// <summary>Achievement ids unlocked so far (persisted between runs).</summary>
        public HashSet<string> Unlocked { get; } = new();

        /// <summary>Ids unlocked by the most recent <see cref="Check"/> batch;
        /// cleared by <see cref="ClearNewlyUnlocked"/>. The HUD reads this to
        /// show toast notifications.</summary>
        public List<string> NewlyUnlocked { get; } = new();

        /// <summary>True when every achievement is unlocked.</summary>
        public bool IsComplete => Unlocked.Count >= TotalAchievements;

        /// <summary>Check all achievements against <paramref name="stats"/>
        /// and unlock any whose threshold is met. Returns the ids unlocked
        /// this call (also appended to <see cref="NewlyUnlocked"/>).</summary>
        public IReadOnlyList<string> Check(in RunStats stats)
        {
            NewlyUnlocked.Clear();
            foreach (var def in _all)
            {
                if (Unlocked.Contains(def.Id)) continue;
                if (def.MetBy(stats))
                {
                    Unlocked.Add(def.Id);
                    NewlyUnlocked.Add(def.Id);
                }
            }
            return NewlyUnlocked;
        }

        /// <summary>Call after reading the toast; clears the newly-unlocked list.</summary>
        public void ClearNewlyUnlocked() => NewlyUnlocked.Clear();

        /// <summary>Directly unlock an achievement (e.g. loaded from save).</summary>
        public void Unlock(string id)
        {
            if (id != null && !Unlocked.Contains(id)) Unlocked.Add(id);
        }

        /// <summary>True when <paramref name="id"/> is unlocked.</summary>
        public bool IsUnlocked(string id) => Unlocked.Contains(id);

        // ── Definitions ────────────────────────────────────────────────────

        private static AchievementDef[] BuildAchievements() => new[]
        {
            new AchievementDef { Id = "first_blood", Name = "First Blood", Description = "Kill your first enemy.", Trigger = AchievementTrigger.KillCount, Target = 1 },
            new AchievementDef { Id = "horde_slayer", Name = "Horde Slayer", Description = "Kill 100 enemies in a single run.", Trigger = AchievementTrigger.KillCount, Target = 100 },
            new AchievementDef { Id = "army_of_one", Name = "Army of One", Description = "Kill 500 enemies in a single run.", Trigger = AchievementTrigger.KillCount, Target = 500 },
            new AchievementDef { Id = "wave_survivor", Name = "Wave Survivor", Description = "Reach wave 10.", Trigger = AchievementTrigger.WaveReached, Target = 10 },
            new AchievementDef { Id = "endurance", Name = "Endurance", Description = "Reach wave 30.", Trigger = AchievementTrigger.WaveReached, Target = 30 },
            new AchievementDef { Id = "demolition_man", Name = "Demolition Man", Description = "Destroy 50 environmental objects.", Trigger = AchievementTrigger.DestructionCount, Target = 50 },
            new AchievementDef { Id = "perk_collector", Name = "Perk Collector", Description = "Take 10 perks in a single run.", Trigger = AchievementTrigger.PerksTaken, Target = 10 },
            new AchievementDef { Id = "chapter_one", Name = "Chapter One", Description = "Complete Chapter 1 of Quest mode.", Trigger = AchievementTrigger.ChapterCleared, Target = 1 },
            new AchievementDef { Id = "quest_master", Name = "Quest Master", Description = "Complete all 60 quests.", Trigger = AchievementTrigger.QuestsCompleted, Target = 60 },
            new AchievementDef { Id = "god_like", Name = "God Like", Description = "Complete a run without taking damage.", Trigger = AchievementTrigger.NoDamageRun, Target = 1 },
        };
    }

    /// <summary>Which run statistic an achievement keys off.</summary>
    public enum AchievementTrigger
    {
        KillCount,
        WaveReached,
        DestructionCount,
        PerksTaken,
        ChapterCleared,
        QuestsCompleted,
        NoDamageRun,
    }

    /// <summary>
    /// Snapshot of the run statistics achievements evaluate against. Populated
    /// by GameManager / the HUD each frame and handed to
    /// <see cref="AchievementManager.Check"/>.
    /// </summary>
    public readonly struct RunStats
    {
        public readonly int KillCount;
        public readonly int Wave;
        public readonly int DestructionCount;
        public readonly int PerksTaken;
        public readonly int HighestChapterCleared;
        public readonly int QuestsCompleted;
        public readonly bool TookDamage;

        public RunStats(int killCount, int wave, int destructionCount, int perksTaken,
            int highestChapterCleared, int questsCompleted, bool tookDamage)
        {
            KillCount = killCount;
            Wave = wave;
            DestructionCount = destructionCount;
            PerksTaken = perksTaken;
            HighestChapterCleared = highestChapterCleared;
            QuestsCompleted = questsCompleted;
            TookDamage = tookDamage;
        }
    }

    public sealed class AchievementDef
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public AchievementTrigger Trigger { get; init; }
        public int Target { get; init; }

        internal bool MetBy(in RunStats s) => Trigger switch
        {
            AchievementTrigger.KillCount => s.KillCount >= Target,
            AchievementTrigger.WaveReached => s.Wave >= Target,
            AchievementTrigger.DestructionCount => s.DestructionCount >= Target,
            AchievementTrigger.PerksTaken => s.PerksTaken >= Target,
            AchievementTrigger.ChapterCleared => s.HighestChapterCleared >= Target,
            AchievementTrigger.QuestsCompleted => s.QuestsCompleted >= Target,
            AchievementTrigger.NoDamageRun => !s.TookDamage,
            _ => false,
        };
    }
}
