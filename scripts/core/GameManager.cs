using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Central game manager singleton (Autoload). Tracks state, score, and wave data.
    /// The run state machine (transitions, guards, score/threat math, counters)
    /// lives in the Godot-free <see cref="GameManagerState"/> (plan T3.1-rest);
    /// this Node keeps only the engine wiring and delegates 1:1.
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; } = null!;

        // ── Extracted state machine ──────────────────────────────────────────
        private readonly GameManagerState _state = new();

        // ── Public state (delegates 1:1 to GameManagerState) ─────────────────
        public GameState CurrentState => _state.CurrentState;
        public int CurrentWave => _state.CurrentWave;
        public long Score => _state.Score;
        public int ThreatLevel => _state.ThreatLevel;
        public int KillCount => _state.KillCount;
        public int DestructionCount => _state.DestructionCount;
        public int ActiveEnemyCount => _state.ActiveEnemyCount;
        public Difficulty CurrentDifficulty => _state.CurrentDifficulty;
        public bool InTutorial => _state.InTutorial;

        // ── Difficulty (delegates 1:1 to GameManagerState) ────────────────────
        public DifficultySettings DifficultySettings => GameManagerState.GetSettings(_state.CurrentDifficulty);
        public void SetDifficulty(Difficulty d) => _state.SetDifficulty(d);

        // ── System references (assigned in _Ready or by scene) ────────────────
        public WaveManager? WaveManager { get; set; }
        public FlowFieldPathfinder? FlowField { get; set; }
        public SpatialHashGrid? SpatialGrid { get; set; }
        public ObjectPoolManager? PoolManager { get; set; }
        public CollateralKarma? KarmaSystem { get; set; }
        public HeroBase? ActiveHero { get; set; }

        // Set by MainMenu.StartGame; consumed by MainMenu.OnSceneChanged after
        // the new scene's _Ready has wired WaveManager/SpatialGrid.
        public bool PendingStartAfterSceneChange { get; set; } = false;

        // Set by MainMenu.StartGame from the hero picker; consumed by
        // MainBootstrap when the game scene loads. Defaults to Atlas so a
        // direct boot into Main.tscn still spawns a playable hero.
        public HeroClass PendingHeroClass { get; set; } = HeroClass.Atlas;

        // Set by MainMenu.StartTutorial; consumed by MainBootstrap so the
        // game scene boots directly into the guided first wave.
        public bool PendingTutorial { get; set; } = false;

        private LevelProgression? _levelProgression;

        public LevelProgression EnsureLevelProgression()
        {
            if (_levelProgression == null || !IsInstanceValid(_levelProgression))
            {
                _levelProgression = new LevelProgression { Name = "LevelProgression" };
                AddChild(_levelProgression);
            }
            return _levelProgression;
        }

        public override void _Ready()
        {
            Instance = this;
            EventBus.Instance.OnEnemyKilled += HandleEnemyKilled;
            EventBus.Instance.OnEnvironmentDestroyed += HandleEnvironmentDestroyed;

            // Assign ObjectPoolManager exports so pooling works
            var pool = GetNodeOrNull<ObjectPoolManager>("/root/ObjectPoolManager");
            if (pool != null)
            {
                pool.ProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/Projectile.tscn");
                pool.DecalScene = GD.Load<PackedScene>("res://scenes/vfx/Decal.tscn");
                pool.DestructibleScene = GD.Load<PackedScene>("res://scenes/maps/Destructible.tscn");
            }
            else
            {
                GD.PrintErr("GameManager: ObjectPoolManager autoload not found at /root/ObjectPoolManager");
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (CurrentState != GameState.Playing) return;
        }

        // ── Public API (each 1:1 over GameManagerState) ───────────────────────
        public void StartGame()
        {
            _state.StartGame();
            WaveManager?.BeginNextWave();
        }

        public void StartTutorial()
        {
            _state.StartTutorial();
            PendingTutorial = true;
        }

        public void CompleteTutorial()
        {
            _state.CompleteTutorial();
            PendingTutorial = false;
        }

        protected virtual void SetTreePaused(bool paused)
        {
            var tree = GetTree();
            if (tree != null)
            {
                tree.Paused = paused;
            }
        }

        public void PauseGame()
        {
            if (_state.RequestPause())
                SetTreePaused(true);
        }

        public void ResumeGame()
        {
            if (_state.RequestResume())
                SetTreePaused(false);
        }

        public void TriggerGameOver()
        {
            _state.SetState(GameState.GameOver);
            EventBus.Instance.EmitGameOver();
        }

        public void AddScore(long points)
        {
            _state.AddScore(points);
        }

        public void IncrementEnemyCount() => _state.IncrementEnemyCount();
        public void DecrementEnemyCount() => _state.DecrementEnemyCount();

        public void SetThreatLevel(int level)
        {
            if (_state.SetThreatLevel(level))
                EventBus.Instance.EmitThreatLevelChanged(level);
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private void HandleEnemyKilled(EnemyBase enemy)
        {
            _state.RecordEnemyKill(enemy.ExpValue);
        }

        private void HandleEnvironmentDestroyed(Vector2 pos, float radius)
        {
            if (_state.RecordEnvironmentDestroyed())
                EventBus.Instance.EmitThreatLevelChanged(_state.ThreatLevel);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnemyKilled -= HandleEnemyKilled;
                EventBus.Instance.OnEnvironmentDestroyed -= HandleEnvironmentDestroyed;
            }
            if (Instance == this) Instance = null!;
            base._ExitTree();
        }
    }
}
