using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free lives tracking for local co-op. Modeled on Crimsonland's
    /// per-player accountability: each player has N lives; one player can be
    /// "out" while others continue; game over only when all players exhaust
    /// lives. Headlessly testable via xUnit.
    /// </summary>
    public enum LivesMode { SharedPool, PerPlayer }

    public sealed class LivesManager
    {
        public LivesMode Mode { get; }
        public int SharedLives { get; private set; }

        /// <summary>playerIndex → lives remaining (PerPlayer mode).</summary>
        public Dictionary<int, int> PerPlayerLives { get; } = new();

        public event Action<int, int>? OnLivesChanged;      // (playerIndex, lives)
        public event Action<int>? OnPlayerOut;              // playerIndex
        public event Action? OnAllOut;                      // game over

        public LivesManager(LivesMode mode, int startingLives = 3, int playerCount = 1)
        {
            Mode = mode;
            if (mode == LivesMode.SharedPool)
                SharedLives = startingLives;
            else
                for (int i = 0; i < playerCount; i++)
                    PerPlayerLives[i] = startingLives;
        }

        /// <summary>True when <paramref name="playerIndex"/> can respawn.</summary>
        public bool HasLives(int playerIndex)
        {
            if (Mode == LivesMode.SharedPool) return SharedLives > 0;
            return PerPlayerLives.TryGetValue(playerIndex, out var l) && l > 0;
        }

        /// <summary>Call when a player dies. Returns true if a respawn is
        /// allowed (lives remain), false when the player is out.</summary>
        public bool SpendLife(int playerIndex)
        {
            if (Mode == LivesMode.SharedPool)
            {
                if (SharedLives <= 0) return false;
                SharedLives--;
                OnLivesChanged?.Invoke(-1, SharedLives);
            }
            else
            {
                if (!PerPlayerLives.TryGetValue(playerIndex, out var l) || l <= 0)
                    return false;
                PerPlayerLives[playerIndex] = l - 1;
                OnLivesChanged?.Invoke(playerIndex, PerPlayerLives[playerIndex]);
                if (PerPlayerLives[playerIndex] == 0)
                    OnPlayerOut?.Invoke(playerIndex);
            }

            if (IsEveryoneOut()) OnAllOut?.Invoke();
            return true;
        }

        public bool IsEveryoneOut()
        {
            if (Mode == LivesMode.SharedPool) return SharedLives <= 0;
            foreach (var kvp in PerPlayerLives)
                if (kvp.Value > 0) return false;
            return true;
        }

        public void AddLife(int playerIndex, int amount = 1)
        {
            if (Mode == LivesMode.SharedPool)
                SharedLives += amount;
            else if (PerPlayerLives.ContainsKey(playerIndex))
                PerPlayerLives[playerIndex] += amount;
            OnLivesChanged?.Invoke(playerIndex,
                Mode == LivesMode.SharedPool ? SharedLives : PerPlayerLives[playerIndex]);
        }
    }
}
