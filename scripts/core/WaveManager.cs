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
        public const int MAX_ENEMIES = 2000;
        private const float WEIBULL_K = 1.5f;
        private const float WEIBULL_LAMBDA = 10f;
        private const float BREATHER_SECONDS = 5f;
        private const float BOSS_INTERVAL_SECONDS = 300f;

        [Export] public PackedScene[] EnemyScenes { get; set; } = Array.Empty<PackedScene>();
        [Export] public Vector2[] SpawnPoints { get; set; } = Array.Empty<Vector2>();

        private float _bossTimer = 0f;
        private bool _waveInProgress = false;
        private float _breatherTimer = 0f;
        private int _currentWave = 0;

        public override void _Ready()
        {
            EventBus.Instance.OnWaveCompleted += OnWaveComplete;
        }

        public override void _PhysicsProcess(double delta)
        {
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
                if (_breatherTimer <= 0f)
                    BeginNextWave();
            }
        }

        public void BeginNextWave()
        {
            _currentWave++;
            _waveInProgress = true;
            EventBus.Instance.EmitWaveStarted(_currentWave);

            int totalEnemies = CalculateWaveEnemyCount(_currentWave);
            SpawnWaveEnemies(totalEnemies);
        }

        private int CalculateWaveEnemyCount(int wave)
        {
            const int MinWaveEnemies = 10;
            const float IntensityScaleFactor = 500f;

            // Weibull intensity scaled to spawn count
            float t = wave;
            float intensity = (WEIBULL_K / WEIBULL_LAMBDA)
                * MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K - 1f)
                * MathF.Exp(-MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K));
            int count = MinWaveEnemies + (int)(intensity * IntensityScaleFactor);
            count = Mathf.Clamp(count, MinWaveEnemies, MAX_ENEMIES - GameManager.Instance.ActiveEnemyCount);
            return count;
        }

        private void SpawnWaveEnemies(int total)
        {
            if (EnemyScenes.Length == 0 || SpawnPoints.Length == 0) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();

            for (int i = 0; i < total; i++)
            {
                if (GameManager.Instance.ActiveEnemyCount >= MAX_ENEMIES) break;
                int sceneIdx = rng.RandiRange(0, EnemyScenes.Length - 1);
                Vector2 spawnPos = SpawnPoints[rng.RandiRange(0, SpawnPoints.Length - 1)];
                SpawnEnemy(EnemyScenes[sceneIdx], spawnPos);
            }
        }

        private void SpawnEnemy(PackedScene scene, Vector2 pos)
        {
            var enemy = scene.Instantiate<EnemyBase>();
            GetTree().CurrentScene.AddChild(enemy);
            enemy.GlobalPosition = pos;
            GameManager.Instance.IncrementEnemyCount();
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
            _breatherTimer = BREATHER_SECONDS;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnWaveCompleted -= OnWaveComplete;
        }
    }
}
