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

        /// <summary>Increments the wave counter (single source of truth via GameManagerState).</summary>
        public void IncrementWave()
        {
            _state.IncrementWave();
            CheckAchievements();
        }

        // ── System references (assigned in _Ready or by scene) ────────────────
        public WaveManager? WaveManager { get; set; }
        public FlowFieldPathfinder? FlowField { get; set; }
        public SpatialHashGrid? SpatialGrid { get; set; }
        public ObjectPoolManager? PoolManager { get; set; }
        public CollateralKarma? KarmaSystem { get; set; }
        public HeroBase? ActiveHero { get; set; }

        // ── Co-op ─────────────────────────────────────────────────────────────
        /// <summary>Per-player lives for local co-op (Crimsonland accountability).</summary>
        public LivesManager Lives { get; } = new(LivesMode.PerPlayer, startingLives: 3, playerCount: 1);

        private Action? _onAllOutHandler;
        private Action<int, int>? _onLivesChangedHandler;

        // ── Meta-progression (R3) ─────────────────────────────────────────────
        public readonly MetaProgressionState MetaProgression = new();

        // ── Achievements (Crimsonland-style local achievement system) ────────
        public readonly AchievementManager Achievements = new();
        private int _perksTaken;
        private bool _tookDamage;

        // ── Survival modes (Crimsonland: Survival, Rush, Blitz, Weapon Picker, Nukefism) ─
        public GameMode CurrentGameMode { get; private set; } = GameMode.Survival;
        public GameModeRules CurrentGameModeRules { get; private set; } = GameModeRules.SurvivalRules();
        private float _rushTimer;
        private int _blitzKillsAtModeStart;

        /// <summary>Select the survival mode for the next run.</summary>
        public void SetGameMode(GameMode mode)
        {
            CurrentGameMode = mode;
            CurrentGameModeRules = mode switch
            {
                GameMode.Rush => GameModeRules.RushRules(RushTimeLimitSeconds),
                GameMode.Blitz => GameModeRules.BlitzRules(BlitzKillTarget),
                GameMode.WeaponPicker => GameModeRules.WeaponPickerRules(),
                GameMode.Nukefism => GameModeRules.NukefismRules(),
                _ => GameModeRules.SurvivalRules(),
            };
        }

        // Rush time limit (seconds). Persisted like difficulty.
        public int RushTimeLimitSeconds { get; set; } = 180;
        // Blitz kill target.
        public int BlitzKillTarget { get; set; } = 100;

        /// <summary>Snapshot of run stats including mode info for leaderboards.</summary>
        public RunStats GetRunStats() => new(
            _state.KillCount,
            _state.CurrentWave,
            _state.DestructionCount,
            _perksTaken,
            0, // HighestChapterCleared
            0, // QuestsCompleted
            _tookDamage);

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
            EventBus.Instance.OnHeroDamaged += HandleHeroDamaged;
            EventBus.Instance.OnPerkChosen += HandlePerkChosen;

            // Co-op: game over only when every player is out of lives.
            _onAllOutHandler = () => TriggerGameOver();
            _onLivesChangedHandler = (pi, lives) =>
                EventBus.Instance?.EmitLivesChanged(pi, lives);
            Lives.OnAllOut += _onAllOutHandler;
            Lives.OnLivesChanged += _onLivesChangedHandler;

            LoadMetaProgression();

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

            // Rush mode: survive the time limit → mode complete (win).
            if (CurrentGameMode == GameMode.Rush)
            {
                _rushTimer += (float)delta;
                if (_rushTimer >= RushTimeLimitSeconds)
                    CompleteMode();
            }
        }

        // ── Public API (each 1:1 over GameManagerState) ───────────────────────
        public void StartGame()
        {
            _tookDamage = false;
            _perksTaken = 0;
            _rushTimer = 0f;
            _blitzKillsAtModeStart = _state.KillCount;
            _state.StartGame();
            WaveManager?.BeginNextWave();
        }

        public void StartTutorial()
        {
            _tookDamage = false;
            _perksTaken = 0;
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
            _state.TryTransition(GameState.GameOver);
            EventBus.Instance.EmitGameOver();
        }

        /// <summary>Called when a mode's win condition is met (Rush time limit,
        /// Blitz kill target). Ends the run as a mode completion rather than a
        /// death, so the run counts as a cleared survival mode.</summary>
        public void CompleteMode()
        {
            if (CurrentState == GameState.GameOver) return;
            _state.TryTransition(GameState.GameOver);
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

        public void SaveMetaProgression()
        {
            var cfg = new ConfigFile();
            if (cfg.Load("user://settings.cfg") != Error.Ok)
                cfg = new ConfigFile();

            var sec = MetaProgressionState.ConfigSection;
            cfg.SetValue(sec, MetaProgressionState.ScrapKey, MetaProgression.Scrap);
            cfg.SetValue(sec, MetaProgressionState.HealthTierKey, MetaProgression.HealthUpgradeTier);
            cfg.SetValue(sec, MetaProgressionState.SpeedTierKey, MetaProgression.SpeedUpgradeTier);
            cfg.SetValue(sec, MetaProgressionState.DamageTierKey, MetaProgression.DamageUpgradeTier);

            string path = "user://settings.cfg";
            string tmp = path + ".tmp";
            cfg.Save(tmp);
            System.IO.File.Move(ProjectSettings.GlobalizePath(tmp), ProjectSettings.GlobalizePath(path), overwrite: true);
        }

        private void LoadMetaProgression()
        {
            var cfg = new ConfigFile();
            if (cfg.Load("user://settings.cfg") != Error.Ok) return;

            var sec = MetaProgressionState.ConfigSection;
            MetaProgression.Scrap = MetaProgressionState.ParseInt(cfg.GetValue(sec, MetaProgressionState.ScrapKey, 0).AsString(), 0);
            MetaProgression.HealthUpgradeTier = MetaProgressionState.ParseInt(cfg.GetValue(sec, MetaProgressionState.HealthTierKey, 0).AsString(), 0);
            MetaProgression.SpeedUpgradeTier = MetaProgressionState.ParseInt(cfg.GetValue(sec, MetaProgressionState.SpeedTierKey, 0).AsString(), 0);
            MetaProgression.DamageUpgradeTier = MetaProgressionState.ParseInt(cfg.GetValue(sec, MetaProgressionState.DamageTierKey, 0).AsString(), 0);
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private void HandleEnemyKilled(EnemyBase enemy)
        {
            _state.RecordEnemyKill(enemy.ExpValue);

            // Heal on Kill perk.
            var hero = ActiveHero;
            if (hero != null && hero.Stats.HealOnKill)
                hero.Heal(5f);

            // Blitz mode: reach kill target as fast as possible → mode complete (win).
            if (CurrentGameMode == GameMode.Blitz)
            {
                int killsThisRun = _state.KillCount - _blitzKillsAtModeStart;
                if (killsThisRun >= BlitzKillTarget)
                    CompleteMode();
            }

            // Check kill/wave/destruction achievements after every kill.
            CheckAchievements();
        }

        private void HandleHeroDamaged(float _) => _tookDamage = true;

        private void HandlePerkChosen(PerkType _) => _perksTaken++;

        private void HandleEnvironmentDestroyed(Vector2 pos, float radius)
        {
            if (_state.RecordEnvironmentDestroyed())
                EventBus.Instance.EmitThreatLevelChanged(_state.ThreatLevel);
        }

        /// <summary>Evaluate all achievements against the current run stats and
        /// surface any newly unlocked ones (the HUD shows a toast for each).</summary>
        private void CheckAchievements()
        {
            var newly = Achievements.Check(GetRunStats());
            foreach (var id in newly)
                EventBus.Instance?.EmitPowerupCollected($"Achievement:{id}");
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnemyKilled -= HandleEnemyKilled;
                EventBus.Instance.OnEnvironmentDestroyed -= HandleEnvironmentDestroyed;
                EventBus.Instance.OnHeroDamaged -= HandleHeroDamaged;
                EventBus.Instance.OnPerkChosen -= HandlePerkChosen;
            }
            Lives.OnAllOut -= _onAllOutHandler;
            Lives.OnLivesChanged -= _onLivesChangedHandler;
            if (Instance == this) Instance = null!;
            base._ExitTree();
        }
    }
}
