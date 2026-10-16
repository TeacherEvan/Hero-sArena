using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free run-state machine extracted from the GameManager autoload,
    /// under the same pattern as ProgressionFormulas/WaveFormulas
    /// (Issue #22 / plan T3.1-rest): the xUnit suite can drive transitions,
    /// guards, and the score/threat formulas headlessly, and
    /// <see cref="GameManager"/> delegates to a state instance while keeping
    /// only the engine wiring — EventBus emissions (SetThreatLevel,
    /// RecordEnvironmentDestroyed), SceneTree.Paused (RequestPause/
    /// RequestResume), and the WaveManager kick (StartGame).
    /// Behavior is preserved 1:1 from GameManager as of 6668bc5, including
    /// quirks: StartGame does NOT reset ActiveEnemyCount (a fresh game
    /// inherits the previous run's live enemies until WaveManager's cap check
    /// trims them; pinned by GameManagerStateTests), and the tree-pause side
    /// effects stay on the Node because GetTree() is engine state.
    /// </summary>
    public class GameManagerState
    {
        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public int CurrentWave { get; private set; } = 0;
        public long Score { get; private set; } = 0;
        public int ThreatLevel { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;
        public int DestructionCount { get; private set; } = 0;
        public int ActiveEnemyCount { get; private set; } = 0;

        public void SetState(GameState newState) => CurrentState = newState;

        /// <summary>
        /// Resets the per-run metrics and drops the machine into Playing.
        /// Mirrors the GameManager.StartGame reset set exactly: wave, score,
        /// threat, kills, destruction — but NOT ActiveEnemyCount.
        /// </summary>
        public void StartGame()
        {
            CurrentWave = 0;
            Score = 0;
            ThreatLevel = 0;
            KillCount = 0;
            DestructionCount = 0;
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Playing → Paused. Returns true when the caller must pause the
        /// SceneTree; any other current state is a no-op returning false.
        /// </summary>
        public bool RequestPause()
        {
            if (CurrentState != GameState.Playing) return false;
            CurrentState = GameState.Paused;
            return true;
        }

        /// <summary>
        /// Paused → Playing. Returns true when the caller must unpause the
        /// SceneTree; any other current state is a no-op returning false.
        /// </summary>
        public bool RequestResume()
        {
            if (CurrentState != GameState.Paused) return false;
            CurrentState = GameState.Playing;
            return true;
        }

        public void AddScore(long points) => Score += points;

        public void IncrementEnemyCount() => ActiveEnemyCount++;

        /// <summary>Floors at zero — GameManager never lets the count go negative.</summary>
        public void DecrementEnemyCount()
            => ActiveEnemyCount = Math.Max(0, ActiveEnemyCount - 1);

        /// <summary>
        /// No-op when the level is unchanged. Returns true when the caller
        /// must emit EventBus.OnThreatLevelChanged (the value is this state's
        /// ThreatLevel after the call).
        /// </summary>
        public bool SetThreatLevel(int level)
        {
            if (ThreatLevel == level) return false;
            ThreatLevel = level;
            return true;
        }

        /// <summary>
        /// One enemy kill: kill counter up, score up by expValue × 10, live
        /// enemy count down. Caller passes EnemyBase.ExpValue (int) and only
        /// handles the event wiring.
        /// </summary>
        public void RecordEnemyKill(int expValue)
        {
            KillCount++;
            AddScore(expValue * 10L);
            DecrementEnemyCount();
        }

        /// <summary>
        /// One environmental destruction. Threat steps up by one per 10
        /// destructions (integer division) — but only forward, never back:
        /// destruction that happens below the current threat level does not
        /// lower anything. Returns true when the caller must emit
        /// EventBus.OnThreatLevelChanged.
        /// </summary>
        public bool RecordEnvironmentDestroyed()
        {
            DestructionCount++;
            int newThreat = DestructionCount / 10;
            if (newThreat > ThreatLevel)
            {
                ThreatLevel = newThreat;
                return true;
            }
            return false;
        }
    }

    /// <summary>Run-state contract of the GameManager autoload.</summary>
    public enum GameState { MainMenu, Playing, Paused, GameOver, LevelUp }
}
