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
        private System.Action<int, bool>? _onWaveIncoming;

        private long _lastScore = -1;
        private int _lastEnemyCount = -1;
        private float _lastMaxHealth = -1f;
        private bool _lastHealthLow = false;

        private readonly Label[] _powerupLabels = new Label[PowerupBannerSlots.MaxBanners];
        private readonly float[] _powerupTimers = new float[PowerupBannerSlots.MaxBanners];

        private Label _waveWarningLabel = null!;
        private ColorRect _lowHealthVignette = null!;
        private Label _dodgeCooldownLabel = null!;
        private Label _abilityCooldownLabel = null!;
        private float _waveWarningTimer;

        private const float LOW_HEALTH_THRESHOLD = 0.30f;
        private const float VIGNETTE_MAX_ALPHA = 0.30f;
        private const float WAVE_WARNING_DURATION = 2.5f;

        public override void _Ready()
        {
            for (int i = 0; i < PowerupBannerSlots.MaxBanners; i++)
            {
                var lbl = new Label { Visible = false };
                PowerupList.AddChild(lbl);
                _powerupLabels[i] = lbl;
                _powerupTimers[i] = 0f;
            }

            _onHeroDamaged = _ => RefreshHealth();
            _onWaveStarted = w => { WaveLabel.Text = $"Wave {w}"; };
            _onLevelUp = l => { LevelLabel.Text = $"Lv {l}"; };
            _onThreatChanged = t => { ThreatLabel.Text = $"Threat {t}"; };

            EventBus.Instance.OnHeroDamaged += _onHeroDamaged;
            EventBus.Instance.OnWaveStarted += _onWaveStarted;
            EventBus.Instance.OnLevelUp += _onLevelUp;
            EventBus.Instance.OnThreatLevelChanged += _onThreatChanged;
            EventBus.Instance.OnPowerupCollected += ShowPowerup;
            ApplyTheme();

            _onWaveIncoming = (wave, isBoss) => ShowWaveWarning(wave, isBoss);
            EventBus.Instance.OnWaveIncoming += _onWaveIncoming;

            // Top-center wave warning banner.
            _waveWarningLabel = new Label
            {
                Visible = false,
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                AnchorTop = 0.15f,
                AnchorBottom = 0.15f,
                OffsetLeft = -220,
                OffsetRight = 220,
                OffsetTop = -20,
                OffsetBottom = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            VisualTheme.StyleLabel(_waveWarningLabel, VisualTheme.Gold);
            _waveWarningLabel.AddThemeFontSizeOverride("font_size", 30);
            AddChild(_waveWarningLabel);

            // Fullscreen low-health vignette (sits behind UI).
            _lowHealthVignette = new ColorRect
            {
                Color = new Color(0.5f, 0f, 0f, 0f),
                AnchorRight = 1f,
                AnchorBottom = 1f,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddChild(_lowHealthVignette);
            MoveChild(_lowHealthVignette, 0);

            // Floating Combat Damage Numbers Canvas
            AddChild(new DamageNumberCanvas());

            // Procedural Audio Engine
            if (AudioManager.Instance == null)
            {
                AddChild(new AudioManager());
            }

            // Bottom-center Cooldown Bar
            var cooldownContainer = new HBoxContainer
            {
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                AnchorTop = 0.94f,
                AnchorBottom = 0.94f,
                OffsetLeft = -220,
                OffsetRight = 220,
                OffsetTop = -32,
                OffsetBottom = 0,
                Alignment = BoxContainer.AlignmentMode.Center
            };
            cooldownContainer.AddThemeConstantOverride("separation", 36);

            _dodgeCooldownLabel = new Label { Text = "DODGE [SPACE]: READY" };
            _abilityCooldownLabel = new Label { Text = "ABILITY [E]: READY" };
            VisualTheme.StyleLabel(_dodgeCooldownLabel, VisualTheme.Cyan);
            VisualTheme.StyleLabel(_abilityCooldownLabel, VisualTheme.Gold);
            _dodgeCooldownLabel.AddThemeFontSizeOverride("font_size", 16);
            _abilityCooldownLabel.AddThemeFontSizeOverride("font_size", 16);

            cooldownContainer.AddChild(_dodgeCooldownLabel);
            cooldownContainer.AddChild(_abilityCooldownLabel);
            AddChild(cooldownContainer);
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

                // Cooldown indicators
                if (_dodgeCooldownLabel != null)
                {
                    float dRemain = hero.DodgeCooldownRemaining;
                    if (dRemain <= 0.05f)
                    {
                        _dodgeCooldownLabel.Text = "DODGE: READY";
                        _dodgeCooldownLabel.Modulate = VisualTheme.Cyan;
                    }
                    else
                    {
                        _dodgeCooldownLabel.Text = $"DODGE: {dRemain:0.0}s";
                        _dodgeCooldownLabel.Modulate = VisualTheme.TextMuted;
                    }
                }

                if (_abilityCooldownLabel != null)
                {
                    float aRemain = hero.AbilityCooldownRemaining;
                    if (aRemain <= 0.05f)
                    {
                        _abilityCooldownLabel.Text = "ABILITY: READY";
                        _abilityCooldownLabel.Modulate = VisualTheme.Gold;
                    }
                    else
                    {
                        _abilityCooldownLabel.Text = $"ABILITY: {aRemain:0.0}s";
                        _abilityCooldownLabel.Modulate = VisualTheme.TextMuted;
                    }
                }

                // Low-health state: vignette + health bar shifts gold → red.
                if (hero.MaxHealth > 0f)
                {
                    var healthPct = hero.CurrentHealth / hero.MaxHealth;
                    var low = healthPct <= LOW_HEALTH_THRESHOLD;
                    if (low != _lastHealthLow)
                    {
                        _lastHealthLow = low;
                        var fillColor = low ? VisualTheme.LowHealth : VisualTheme.Gold;
                        HealthBar.AddThemeStyleboxOverride("fill", VisualTheme.PanelStyle(fillColor, VisualTheme.Border));
                    }
                    if (low)
                    {
                        var t = 1f - healthPct / LOW_HEALTH_THRESHOLD; // 1 at 0%, 0 at 30%
                        _lowHealthVignette.Color = new Color(0.5f, 0f, 0f, VIGNETTE_MAX_ALPHA * t);
                    }
                    else
                    {
                        _lowHealthVignette.Color = new Color(0.5f, 0f, 0f, 0f);
                    }
                }
            }

            for (int i = 0; i < PowerupBannerSlots.MaxBanners; i++)
            {
                if (_powerupTimers[i] > 0f)
                {
                    _powerupTimers[i] -= (float)delta;
                    if (_powerupTimers[i] <= 0f)
                    {
                        _powerupLabels[i].Visible = false;
                    }
                }
            }

            // Wave warning banner countdown.
            if (_waveWarningTimer > 0f)
            {
                _waveWarningTimer -= (float)delta;
                if (_waveWarningTimer <= 0f)
                    _waveWarningLabel.Visible = false;
            }
        }

        private void ShowWaveWarning(int wave, bool isBoss)
        {
            _waveWarningLabel.Text = isBoss ? $"BOSS WAVE {wave} INCOMING" : $"WAVE {wave} INCOMING";
            _waveWarningLabel.Visible = true;
            _waveWarningTimer = WAVE_WARNING_DURATION;
            SetProcess(true);
        }

        private void RefreshHealth()
        {
            var hero = GameManager.Instance.ActiveHero;
            if (hero != null) HealthBar.Value = hero.CurrentHealth;
        }

        private void ShowPowerup(string type)
        {
            if (!IsInsideTree()) return;

            // Slot selection (first expired banner, else the oldest active one)
            // lives in PowerupBannerSlots so the rule is unit-tested headlessly.
            int targetIdx = PowerupBannerSlots.SelectSlot(_powerupTimers);

            var lbl = _powerupLabels[targetIdx];
            lbl.Text = type;
            lbl.Visible = true;
            _powerupTimers[targetIdx] = PowerupBannerSlots.Lifetime;
            PowerupList.MoveChild(lbl, -1);
        }

        private void ApplyTheme()
        {
            VisualTheme.StylePanel(GetNode<VBoxContainer>("VBoxContainer"));
            VisualTheme.StylePanel(PowerupList);

            VisualTheme.StyleLabel(LevelLabel, VisualTheme.Gold);
            VisualTheme.StyleLabel(WaveLabel, VisualTheme.Gold);
            VisualTheme.StyleLabel(ScoreLabel, VisualTheme.Text);
            VisualTheme.StyleLabel(EnemyCountLabel, VisualTheme.Text);
            VisualTheme.StyleLabel(ThreatLabel, VisualTheme.Text);

            VisualTheme.StyleProgressBar(HealthBar, VisualTheme.Gold);
            VisualTheme.StyleProgressBar(XpBar, VisualTheme.Cyan);

            for (int i = 0; i < _powerupLabels.Length; i++)
                VisualTheme.StyleLabel(_powerupLabels[i], VisualTheme.TextMuted);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnHeroDamaged -= _onHeroDamaged;
            EventBus.Instance.OnWaveStarted -= _onWaveStarted;
            EventBus.Instance.OnLevelUp -= _onLevelUp;
            EventBus.Instance.OnThreatLevelChanged -= _onThreatChanged;
            EventBus.Instance.OnPowerupCollected -= ShowPowerup;
            if (EventBus.Instance != null)
                EventBus.Instance.OnWaveIncoming -= _onWaveIncoming;
        }
    }
}
