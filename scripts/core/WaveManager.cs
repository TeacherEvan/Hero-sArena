using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Manages enemy wave spawning using a modified Weibull distribution for intensity.
    /// Enforces the 2,000 concurrent enemy cap.
    /// </summary>
    public partial class WaveManager : Node
    {
        public const int MAX_ENEMIES = WaveFormulas.MaxEnemies;
        private const float BREATHER_SECONDS = 5f;
        private const float BOSS_INTERVAL_SECONDS = 300f;
        private const int MAX_SPAWNS_PER_FRAME = 20;
        private const float WAVE_WARNING_LEAD_SECONDS = 2f;

        [Export] public PackedScene[] EnemyScenes { get; set; } = Array.Empty<PackedScene>();
        [Export] public Vector2[] SpawnPoints { get; set; } = Array.Empty<Vector2>();

        private float _bossTimer = 0f;
        private bool _waveInProgress = false;
        private float _breatherTimer = 0f;
        private float _breatherSeconds = BREATHER_SECONDS;
        private int _currentWave = 0;
        private int _pendingEnemiesToSpawn = 0;
        private RandomNumberGenerator _rng = new RandomNumberGenerator();
        private bool _waveWarningEmitted;

        public override void _Ready()
        {
            _rng.Randomize();
            // Self-register so the spawner works no matter which scene hosts us.
            // (MainBootstrap also wires this; last write wins and both agree.)
            if (GameManager.Instance != null)
                GameManager.Instance.WaveManager = this;
            EventBus.Instance.OnWaveCompleted += OnWaveComplete;
        }

        public override void _PhysicsProcess(double delta)
        {
            // During Tutorial (R1), WaveManager's Weibull spawning is
            // suppressed — Tutorial.cs spawns drones one at a time instead.
            if (GameManager.Instance != null
                && GameManager.Instance.CurrentState != GameState.Playing)
                return;

            float dt = (float)delta;
            _bossTimer += dt;

            if (_bossTimer >= BOSS_INTERVAL_SECONDS)
            {
                _bossTimer = 0f;
                SpawnBoss();
            }

            if (!_waveInProgress && _breatherTimer > 0f)
            {
                _breatherTimer -= dt;

                // Warn the player once when the breather reaches the lead time
                // before the next wave spawns.
                if (!_waveWarningEmitted && _breatherTimer <= WAVE_WARNING_LEAD_SECONDS)
                {
                    _waveWarningEmitted = true;
                    var nextWave = _currentWave + 1;
                    var isBoss = _bossTimer >= BOSS_INTERVAL_SECONDS - WAVE_WARNING_LEAD_SECONDS;
                    EventBus.Instance.EmitWaveIncoming(nextWave, isBoss);
                }

                if (_breatherTimer <= 0f)
                    BeginNextWave();
            }

            if (_pendingEnemiesToSpawn > 0)
            {
                ProcessPendingSpawns();
            }

            // Emit wave completion once the wave is fully cleared (no pending spawns and no live enemies)
            if (_waveInProgress && _pendingEnemiesToSpawn == 0
                && GameManager.Instance != null
                && GameManager.Instance.ActiveEnemyCount == 0)
            {
                EventBus.Instance.EmitWaveCompleted(_currentWave);
            }
        }

        public void BeginNextWave()
        {
            _currentWave++;
            _waveInProgress = true;
            _waveWarningEmitted = false;
            EventBus.Instance.EmitWaveStarted(_currentWave);

            var gameManager = GameManager.Instance;
            var settings = gameManager?.DifficultySettings
                ?? GameManagerState.GetSettings(Difficulty.Normal);
            _breatherSeconds = settings.BreatherSeconds;

            // Weibull intensity, min floor, and the MaxEnemies clamp live in
            // WaveFormulas (covered headlessly in WaveFormulasTests).
            int totalEnemies = WaveFormulas.CalculateWaveEnemyCount(
                _currentWave, gameManager?.ActiveEnemyCount ?? 0, settings.EnemyCountMultiplier);
            SpawnWaveEnemies(totalEnemies);
        }

        private void SpawnWaveEnemies(int total)
        {
            if (EnemyScenes.Length == 0 || SpawnPoints.Length == 0) return;
            _pendingEnemiesToSpawn += total;
        }

        private void ProcessPendingSpawns()
        {
            int toSpawn = Math.Min(_pendingEnemiesToSpawn, MAX_SPAWNS_PER_FRAME);

            int spawnedThisFrame = 0;
            for (int i = 0; i < toSpawn; i++)
            {
                if (GameManager.Instance.ActiveEnemyCount >= MAX_ENEMIES)
                {
                    _pendingEnemiesToSpawn = 0;
                    return;
                }
                int sceneIdx = _rng.RandiRange(0, EnemyScenes.Length - 1);
                Vector2 spawnPos = SpawnPoints[_rng.RandiRange(0, SpawnPoints.Length - 1)];
                SpawnEnemy(EnemyScenes[sceneIdx], spawnPos);
                spawnedThisFrame++;
            }

            _pendingEnemiesToSpawn -= spawnedThisFrame;
        }

        private void SpawnEnemy(PackedScene scene, Vector2 pos)
        {
            var enemy = scene.Instantiate<EnemyBase>();
            enemy.GlobalPosition = pos;
            GetTree().CurrentScene.AddChild(enemy);
            var gameManager = GameManager.Instance;
            var settings = gameManager?.DifficultySettings
                ?? GameManagerState.GetSettings(Difficulty.Normal);
            EnemyMutatorSystem.Instance?.ApplyMutators(
                enemy, gameManager?.ThreatLevel ?? 0, settings.MutatorScale);
            gameManager?.IncrementEnemyCount();
        }

        private void SpawnBoss()
        {
            if (EnemyScenes.Length == 0 || SpawnPoints.Length == 0) return;
            // Last scene is assumed to be Apex boss
            var bossScene = EnemyScenes[EnemyScenes.Length - 1];
            var pos = SpawnPoints[0];
            SpawnEnemy(bossScene, pos);
        }

        private void OnWaveComplete(int wave)
        {
            _waveInProgress = false;
            _breatherTimer = _breatherSeconds;
            _waveWarningEmitted = false;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnWaveCompleted -= OnWaveComplete;
        }
    }
}
