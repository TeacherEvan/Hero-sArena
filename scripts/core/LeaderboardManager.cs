using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free leaderboard logic: rankings, filtering, and
    /// player-rank lookup. Supports global, co-op, and difficulty-filtered
    /// views — mirroring Crimsonland's global/friends/co-op leaderboards.
    /// Headlessly testable via xUnit.
    ///
    /// The owning Node (<see cref="LeaderboardService"/>) persists entries to
    /// <c>user://leaderboard.cfg</c> and can submit new scores to an online
    /// endpoint when networking is wired up.
    /// </summary>
    public sealed class LeaderboardManager
    {
        private readonly List<LeaderboardEntry> _entries = new();

        /// <summary>All entries (highest score first).</summary>
        public IReadOnlyList<LeaderboardEntry> Entries => _entries;

        /// <summary>Number of entries.</summary>
        public int Count => _entries.Count;

        /// <summary>Add a score to the leaderboard.</summary>
        public void Add(LeaderboardEntry entry) => _entries.Add(entry);

        /// <summary>Top <paramref name="count"/> entries, highest score first.</summary>
        public List<LeaderboardEntry> Top(int count = 10)
        {
            return _entries
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.TimestampMs)
                .Take(Math.Max(0, count))
                .ToList();
        }

        /// <summary>Top entries filtered to co-op (or solo) runs.</summary>
        public List<LeaderboardEntry> TopCoop(bool coopOnly, int count = 10)
        {
            return _entries
                .Where(e => e.IsCoop == coopOnly)
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.TimestampMs)
                .Take(Math.Max(0, count))
                .ToList();
        }

        /// <summary>Top entries for a specific difficulty.</summary>
        public List<LeaderboardEntry> TopDifficulty(Difficulty difficulty, int count = 10)
        {
            return _entries
                .Where(e => e.Difficulty == difficulty)
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.TimestampMs)
                .Take(Math.Max(0, count))
                .ToList();
        }

        /// <summary>1-based rank of <paramref name="entry"/> among all entries,
        /// or null when the entry is not present.</summary>
        public int? RankOf(LeaderboardEntry entry)
        {
            var sorted = _entries
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.TimestampMs)
                .ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].Equals(entry)) return i + 1;
            }
            return null;
        }

        /// <summary>Clear all entries (used for tests / reset).</summary>
        public void Clear() => _entries.Clear();

        /// <summary>Load entries from an enumerable (e.g. deserialized save).</summary>
        public void Load(IEnumerable<LeaderboardEntry> entries)
        {
            _entries.Clear();
            _entries.AddRange(entries);
        }
    }
}
