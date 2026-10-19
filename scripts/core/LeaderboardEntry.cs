using System;

namespace HeroArena
{
    /// <summary>
    /// A single leaderboard entry. Crimsonland tracks global, friends, and
    /// co-op leaderboards for survival modes, ranked by score. Pure data —
    /// no engine dependencies — so it is headlessly testable and easy to
    /// serialize to JSON or submit to an online endpoint.
    /// </summary>
    public readonly struct LeaderboardEntry
    {
        public string PlayerName { get; init; }
        public long Score { get; init; }
        public int Waves { get; init; }
        public int Kills { get; init; }
        public int DestructionCount { get; init; }
        public Difficulty Difficulty { get; init; }
        public bool IsCoop { get; init; }
        public int PlayerCount { get; init; }
        public long TimestampMs { get; init; }

        /// <summary>Short display label for the entry.</summary>
        public string DisplayLabel =>
            $"{PlayerName}  ·  {Score:N0} pts  ·  Wave {Waves}  ·  {Kills} kills  ·  {Difficulty}";
    }
}
