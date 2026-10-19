using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Leaderboard service autoload. Persists run scores to
    /// <c>user://leaderboard.cfg</c> and exposes global / co-op / difficulty
    /// filtered rankings — Crimsonland's global, friends, and co-op
    /// leaderboards for survival modes.
    ///
    /// Pure ranking logic lives in <see cref="LeaderboardManager"/> so it is
    /// headlessly testable; this Node keeps only engine wiring (file I/O,
    /// SceneTree lifetime).
    ///
    /// To go online, wire <see cref="SubmitOnline"/> to an HTTP endpoint; the
    /// local save remains the fallback.
    /// </summary>
    public partial class LeaderboardService : Node
    {
        public static LeaderboardService Instance { get; private set; } = null!;

        private const string SavePath = "user://leaderboard.cfg";
        public const int MaxEntries = 100;

        public LeaderboardManager Manager { get; } = new();

        public override void _Ready()
        {
            Instance = this;
            Load();
        }

        /// <summary>Record a completed run's score. Called from GameOver / the
        /// run-end flow. Persists to disk immediately.</summary>
        public void SubmitScore(long score, int waves, int kills, int destructionCount,
            Difficulty difficulty, bool isCoop = false, int playerCount = 1,
            string playerName = "Player")
        {
            var entry = new LeaderboardEntry
            {
                PlayerName = playerName,
                Score = score,
                Waves = waves,
                Kills = kills,
                DestructionCount = destructionCount,
                Difficulty = difficulty,
                IsCoop = isCoop,
                PlayerCount = playerCount,
            };
            Manager.Add(entry);
            Trim();
            Save();
            GD.Print($"Leaderboard: submitted {score:N0} pts (wave {waves}, {kills} kills)");
        }

        /// <summary>Top <paramref name="count"/> entries overall.</summary>
        public List<LeaderboardEntry> Top(int count = 10) => Manager.Top(count);

        /// <summary>Top co-op or solo entries.</summary>
        public List<LeaderboardEntry> TopCoop(bool coopOnly, int count = 10) =>
            Manager.TopCoop(coopOnly, count);

        /// <summary>Top entries for <paramref name="difficulty"/>.</summary>
        public List<LeaderboardEntry> TopDifficulty(Difficulty difficulty, int count = 10) =>
            Manager.TopDifficulty(difficulty, count);

        /// <summary>1-based rank of <paramref name="entry"/>, or null.</summary>
        public int? RankOf(LeaderboardEntry entry) => Manager.RankOf(entry);

        /// <summary>Placeholder: submit to an online endpoint when a backend
        /// is wired up. Returns false until implemented.</summary>
        public static bool SubmitOnline(LeaderboardEntry entry)
        {
            // No backend configured yet — local save is the source of truth.
            // Wire an HTTPClient / WebSocket here when online leaderboards are
            // required (Crimsonland has global + friends + co-op leaderboards).
            return false;
        }

        private void Trim()
        {
            if (Manager.Count <= MaxEntries) return;
            var top = Manager.Top(MaxEntries);
            Manager.Clear();
            foreach (var e in top) Manager.Add(e);
        }

        // ── Persistence ────────────────────────────────────────────────────

        private void Save()
        {
            var cfg = new ConfigFile();
            cfg.SetValue("leaderboard", "count", Manager.Count);
            int i = 0;
            foreach (var e in Manager.Entries)
            {
                var section = $"entry_{i++}";
                cfg.SetValue(section, "name", e.PlayerName);
                cfg.SetValue(section, "score", e.Score);
                cfg.SetValue(section, "waves", e.Waves);
                cfg.SetValue(section, "kills", e.Kills);
                cfg.SetValue(section, "destruction", e.DestructionCount);
                cfg.SetValue(section, "difficulty", (int)e.Difficulty);
                cfg.SetValue(section, "coop", e.IsCoop);
                cfg.SetValue(section, "players", e.PlayerCount);
                cfg.SetValue(section, "timestamp", e.TimestampMs);
            }
            cfg.Save(SavePath);
        }

        private void Load()
        {
            var cfg = new ConfigFile();
            if (cfg.Load(SavePath) != Error.Ok) return;
            int count = cfg.GetValue("leaderboard", "count", 0).AsInt32();
            for (int i = 0; i < count; i++)
            {
                var section = $"entry_{i}";
                Manager.Add(new LeaderboardEntry
                {
                    PlayerName = cfg.GetValue(section, "name", "Player").AsString(),
                    Score = cfg.GetValue(section, "score", 0L).AsInt64(),
                    Waves = cfg.GetValue(section, "waves", 0).AsInt32(),
                    Kills = cfg.GetValue(section, "kills", 0).AsInt32(),
                    DestructionCount = cfg.GetValue(section, "destruction", 0).AsInt32(),
                    Difficulty = (Difficulty)cfg.GetValue(section, "difficulty", 1).AsInt32(),
                    IsCoop = cfg.GetValue(section, "coop", false).AsBool(),
                    PlayerCount = cfg.GetValue(section, "players", 1).AsInt32(),
                    TimestampMs = cfg.GetValue(section, "timestamp", 0L).AsInt64(),
                });
            }
        }

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null!;
            base._ExitTree();
        }
    }
}
