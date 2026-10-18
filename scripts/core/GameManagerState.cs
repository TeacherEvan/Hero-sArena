using System;
using System.Linq;

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
        public int CurrentWave { get; set; } = 0;
        public long Score { get; private set; } = 0;
        public int ThreatLevel { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;
        public int DestructionCount { get; private set; } = 0;
        public int ActiveEnemyCount { get; private set; } = 0;

        public void SetState(GameState newState) => CurrentState = newState;

        /// <summary>
        /// Validated state transition: only the documented from→to edges are
        /// allowed (MainMenu↔Tutorial↔Playing↔Paused↔LevelUp↔GameOver). Returns
        /// true when the transition was applied, false when it is illegal for
        /// the current state. Use this instead of <see cref="SetState"/> when
        /// the caller is not trusted to know the current state.
        /// </summary>
        public bool TryTransition(GameState newState)
        {
            if (CurrentState == newState) return true; // idempotent no-op
            var allowed = CurrentState switch
            {
                GameState.MainMenu => new[] { GameState.Playing, GameState.Tutorial },
                GameState.Tutorial => new[] { GameState.MainMenu },
                GameState.Playing => new[] { GameState.Paused, GameState.GameOver, GameState.LevelUp },
                GameState.Paused => new[] { GameState.Playing, GameState.GameOver },
                GameState.LevelUp => new[] { GameState.Playing },
                GameState.GameOver => new[] { GameState.MainMenu },
                _ => Array.Empty<GameState>()
            };
            if (!allowed.Contains(newState)) return false;
            CurrentState = newState;
            return true;
        }

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
            TryTransition(GameState.Playing);
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

        // ── Tutorial (R1: Guided "First Wave" onboarding) ───────────────────

        public bool InTutorial { get; private set; }

        /// <summary>
        /// Resets run metrics and drops the machine into Tutorial state.
        /// Caller (GameManager) suppresses normal Weibull wave spawning while
        /// in Tutorial and delegates spawning to Tutorial.cs.
        /// </summary>
        public void StartTutorial()
        {
            CurrentWave = 0;
            Score = 0;
            ThreatLevel = 0;
            KillCount = 0;
            DestructionCount = 0;
            ActiveEnemyCount = 0;
            InTutorial = true;
            TryTransition(GameState.Tutorial);
        }

        /// <summary>
        /// Leaves Tutorial, returns to MainMenu, and unlocks Normal mode.
        /// </summary>
        public void CompleteTutorial()
        {
            InTutorial = false;
            TryTransition(GameState.MainMenu);
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
        /// One environmental destruction. Threat steps up by one per
        /// <see cref="DifficultySettings.ThreatInterval"/> destructions — but
        /// only forward, never back: destruction below the current threat
        /// level does not lower anything. Returns true when the caller must
        /// emit EventBus.OnThreatLevelChanged.
        /// </summary>
        public bool RecordEnvironmentDestroyed()
        {
            DestructionCount++;
            var settings = GetSettings(CurrentDifficulty);
            int newThreat = DestructionCount / settings.ThreatInterval;
            if (newThreat > ThreatLevel)
            {
                ThreatLevel = newThreat;
                return true;
            }
            return false;
        }

        // ── Difficulty ────────────────────────────────────────────────────────

        public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Normal;

        public void SetDifficulty(Difficulty d)
        {
            if (CurrentDifficulty == d) return;
            CurrentDifficulty = d;
        }

        /// <summary>Increments the wave counter; single source of truth for
        /// CurrentWave so WaveManager and GameManagerState never diverge.</summary>
        public void IncrementWave()
        {
            CurrentWave++;
        }

        /// <summary>
        /// Per-difficulty settings payload applied at wave generation and by
        /// the mutator system. Pure — headlessly testable via xUnit.
        /// </summary>
        public static DifficultySettings GetSettings(Difficulty d) => d switch
        {
            Difficulty.Easy => new DifficultySettings
            {
                EnemyCountMultiplier = 0.6f,
                EnemyHealthMultiplier = 0.7f,
                EnemyDamageMultiplier = 0.7f,
                ThreatInterval = 20,
                MutatorScale = 0.5f,
                XpMultiplier = 1.5f,
                BreatherSeconds = 8f,
            },
            Difficulty.Hard => new DifficultySettings
            {
                EnemyCountMultiplier = 1.4f,
                EnemyHealthMultiplier = 1.3f,
                EnemyDamageMultiplier = 1.3f,
                ThreatInterval = 8,
                MutatorScale = 1.5f,
                XpMultiplier = 0.8f,
                BreatherSeconds = 4f,
            },
            _ => new DifficultySettings
            {
                EnemyCountMultiplier = 1.0f,
                EnemyHealthMultiplier = 1.0f,
                EnemyDamageMultiplier = 1.0f,
                ThreatInterval = 10,
                MutatorScale = 1.0f,
                XpMultiplier = 1.0f,
                BreatherSeconds = 5f,
            },
        };
    }

    /// <summary>Per-difficulty settings payload applied at wave generation
    /// and by the mutator system.</summary>
    public struct DifficultySettings
    {
        public float EnemyCountMultiplier;   // spawn curve / density lever
        public float EnemyHealthMultiplier;  // per-enemy stats lever
        public float EnemyDamageMultiplier;  // per-enemy stats lever
        public int   ThreatInterval;         // destructions per threat level
        public float MutatorScale;           // mutator count scale
        public float XpMultiplier;           // player XP gain
        public float BreatherSeconds;        // between waves
    }

    /// <summary>Run-state contract of the GameManager autoload.</summary>
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>Run-state contract of the GameManager autoload.</summary>
    public enum GameState { MainMenu, Tutorial, Playing, Paused, GameOver, LevelUp }
}
