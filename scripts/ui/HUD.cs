using Godot;

namespace HeroArena
{
    /// <summary>In-game HUD. Binds to event bus and game manager for live updates.</summary>
    public partial class HUD : CanvasLayer
    {
        [Export] public ProgressBar HealthBar { get; set; } = null!;
        [Export] public ProgressBar XpBar { get; set; } = null!;
        [Export] public Label WaveLabel { get; set; } = null!;
        [Export] public Label ScoreLabel { get; set; } = null!;
        [Export] public Label EnemyCountLabel { get; set; } = null!;
        [Export] public Label ThreatLabel { get; set; } = null!;
        [Export] public Label LevelLabel { get; set; } = null!;
        [Export] public VBoxContainer PowerupList { get; set; } = null!;

        // Store delegates so they can be properly unsubscribed in _ExitTree
        private System.Action<float>? _onHeroDamaged;
        private System.Action<int>? _onWaveStarted;
        private System.Action<int>? _onLevelUp;
        private System.Action<int>? _onThreatChanged;

        private const int MAX_POWERUP_BANNERS = 5;
        private long _lastScore = -1;
        private int _lastEnemyCount = -1;
        private float _lastMaxHealth = -1f;

        public override void _Ready()
        {
            _onHeroDamaged = _ => RefreshHealth();
            _onWaveStarted = w => { WaveLabel.Text = $"Wave {w}"; };
            _onLevelUp = l => { LevelLabel.Text = $"Lv {l}"; };
            _onThreatChanged = t => { ThreatLabel.Text = $"Threat {t}"; };

            EventBus.Instance.OnHeroDamaged += _onHeroDamaged;
            EventBus.Instance.OnWaveStarted += _onWaveStarted;
            EventBus.Instance.OnLevelUp += _onLevelUp;
            EventBus.Instance.OnThreatLevelChanged += _onThreatChanged;
            EventBus.Instance.OnPowerupCollected += ShowPowerup;
        }

        public override void _Process(double delta)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing) return;

            if (gm.Score != _lastScore)
            {
                ScoreLabel.Text = $"Score: {gm.Score}";
                _lastScore = gm.Score;
            }
            if (gm.ActiveEnemyCount != _lastEnemyCount)
            {
                EnemyCountLabel.Text = $"Enemies: {gm.ActiveEnemyCount}";
                _lastEnemyCount = gm.ActiveEnemyCount;
            }

            var hero = gm.ActiveHero;
            if (hero != null)
            {
                if (!Mathf.IsEqualApprox(hero.MaxHealth, _lastMaxHealth))
                {
                    HealthBar.MaxValue = hero.MaxHealth;
                    _lastMaxHealth = hero.MaxHealth;
                }
                HealthBar.Value = hero.CurrentHealth;
                XpBar.Value = hero.Experience;
            }
        }

        private void RefreshHealth()
        {
            var hero = GameManager.Instance.ActiveHero;
            if (hero != null) HealthBar.Value = hero.CurrentHealth;
        }

        private void ShowPowerup(string type)
        {
            if (!IsInsideTree()) return;

            // Cap the visible list to MAX_POWERUP_BANNERS (oldest first out)
            while (PowerupList.GetChildCount() >= MAX_POWERUP_BANNERS)
            {
                var oldest = PowerupList.GetChild(0);
                PowerupList.RemoveChild(oldest);
                oldest.QueueFree();
            }

            // The actual banner+auto-cleanup logic is in PowerupBannerFactory
            // so it can be tested without an [Export]-wired HUD scene.
            // BUG-GUARD (regression for c9fed16): the factory's timer callback
            // must free BOTH the banner and the timer on every return path,
            // including the early-return when the banner has been evicted.
            PowerupBannerFactory.Spawn(this, PowerupList, type);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnHeroDamaged -= _onHeroDamaged;
            EventBus.Instance.OnWaveStarted -= _onWaveStarted;
            EventBus.Instance.OnLevelUp -= _onLevelUp;
            EventBus.Instance.OnThreatLevelChanged -= _onThreatChanged;
            EventBus.Instance.OnPowerupCollected -= ShowPowerup;
        }
    }
}
