using Godot;
using System;

namespace HeroArena
{
    public enum GameState { MainMenu, Playing, Paused, GameOver, LevelUp }

    /// <summary>
    /// Central game manager singleton (Autoload). Tracks state, score, and wave data.
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; } = null!;

        // ── Public state ──────────────────────────────────────────────────────
        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public int CurrentWave { get; private set; } = 0;
        public long Score { get; private set; } = 0;
        public int ThreatLevel { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;
        public int DestructionCount { get; private set; } = 0;
        public int ActiveEnemyCount { get; private set; } = 0;

        // ── System references (assigned in _Ready or by scene) ────────────────
        public WaveManager? WaveManager { get; set; }
        public FlowFieldPathfinder? FlowField { get; set; }
        public SpatialHashGrid? SpatialGrid { get; set; }
        public ObjectPoolManager? PoolManager { get; set; }
        public CollateralKarma? KarmaSystem { get; set; }
        public HeroBase? ActiveHero { get; set; }

        public override void _Ready()
        {
            Instance = this;
            EventBus.Instance.OnEnemyKilled += HandleEnemyKilled;
            EventBus.Instance.OnEnvironmentDestroyed += HandleEnvironmentDestroyed;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (CurrentState != GameState.Playing) return;
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void StartGame()
        {
            CurrentWave = 0;
            Score = 0;
            ThreatLevel = 0;
            KillCount = 0;
            DestructionCount = 0;
            SetState(GameState.Playing);
            WaveManager?.BeginNextWave();
        }

        public void PauseGame()
        {
            if (CurrentState == GameState.Playing)
            {
                SetState(GameState.Paused);
                GetTree().Paused = true;
            }
        }

        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                SetState(GameState.Playing);
                GetTree().Paused = false;
            }
        }

        public void TriggerGameOver()
        {
            SetState(GameState.GameOver);
        }

        public void AddScore(long points)
        {
            Score += points;
        }

        public void IncrementEnemyCount() => ActiveEnemyCount++;
        public void DecrementEnemyCount() => ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);

        public void SetThreatLevel(int level)
        {
            if (ThreatLevel == level) return;
            ThreatLevel = level;
            EventBus.Instance.EmitThreatLevelChanged(ThreatLevel);
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private void SetState(GameState newState)
        {
            CurrentState = newState;
        }

        private void HandleEnemyKilled(EnemyBase enemy)
        {
            KillCount++;
            AddScore(enemy.ExpValue * 10);
            DecrementEnemyCount();
        }

        private void HandleEnvironmentDestroyed(Vector2 pos, float radius)
        {
            DestructionCount++;
            int newThreat = DestructionCount / 10;
            if (newThreat > ThreatLevel)
                SetThreatLevel(newThreat);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnemyKilled -= HandleEnemyKilled;
                EventBus.Instance.OnEnvironmentDestroyed -= HandleEnvironmentDestroyed;
            }
        }
    }
}
