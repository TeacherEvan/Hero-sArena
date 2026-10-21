using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Settings menu: audio volumes, graphics toggles, difficulty.
    /// Every change is persisted immediately to <c>user://settings.cfg</c>.
    ///
    /// Presented as a modal overlay with fade-in/fade-out transitions.
    /// The host calls <see cref="Open"/> and listens to <see cref="OnBack"/> to close it.
    /// </summary>
    public partial class Settings : Control
    {
        [Export] public HSlider MasterSlider { get; set; } = null!;
        [Export] public HSlider SfxSlider { get; set; } = null!;
        [Export] public HSlider MusicSlider { get; set; } = null!;
        [Export] public CheckButton FullscreenToggle { get; set; } = null!;
        [Export] public CheckButton VSyncToggle { get; set; } = null!;
        [Export] public Button EasyButton { get; set; } = null!;
        [Export] public Button NormalButton { get; set; } = null!;
        [Export] public Button HardButton { get; set; } = null!;
        [Export] public Button BackButton { get; set; } = null!;
        [Export] public Label MasterValueLabel { get; set; } = null!;
        [Export] public Label SfxValueLabel { get; set; } = null!;
        [Export] public Label MusicValueLabel { get; set; } = null!;

        public Action? OnBack;

        private const string SettingsPath = "user://settings.cfg";
        private Tween? _fadeTween;

        public override void _Ready()
        {
            LoadSettings();

            MasterSlider.ValueChanged += OnMasterChanged;
            SfxSlider.ValueChanged += OnSfxChanged;
            MusicSlider.ValueChanged += OnMusicChanged;
            FullscreenToggle.Toggled += OnFullscreenToggled;
            VSyncToggle.Toggled += OnVSyncToggled;
            EasyButton.Pressed += () => SelectDifficulty(Difficulty.Easy);
            NormalButton.Pressed += () => SelectDifficulty(Difficulty.Normal);
            HardButton.Pressed += () => SelectDifficulty(Difficulty.Hard);
            BackButton.Pressed += Close;

            // Modal overlay: eat input while open so nothing behind is activated.
            MouseFilter = Control.MouseFilterEnum.Stop;

            VisualTheme.StylePanel(this);

            // Style section headers.
            var sectionAudio = GetNodeOrNull<Label>("Container/SectionAudio");
            var sectionGraphics = GetNodeOrNull<Label>("Container/SectionGraphics");
            var sectionDifficulty = GetNodeOrNull<Label>("Container/SectionDifficulty");
            if (sectionAudio != null) VisualTheme.StyleSectionHeader(sectionAudio);
            if (sectionGraphics != null) VisualTheme.StyleSectionHeader(sectionGraphics);
            if (sectionDifficulty != null) VisualTheme.StyleSectionHeader(sectionDifficulty);

            // Style buttons: difficulty as primary when selected, plain otherwise.
            VisualTheme.StyleButtonTactileSegment(EasyButton);
            VisualTheme.StyleButtonTactileSegment(NormalButton);
            VisualTheme.StyleButtonTactileSegment(HardButton);
            VisualTheme.StyleButtonTactile(BackButton);
            foreach (var seg in new[] { EasyButton, NormalButton, HardButton })
                seg.CustomMinimumSize = new Vector2(100, 32);
            VisualTheme.StyleSliderValueTactile(MasterValueLabel);
            VisualTheme.StyleSliderValueTactile(SfxValueLabel);
            VisualTheme.StyleSliderValueTactile(MusicValueLabel);

            // Sliders: dark track + celestial fill instead of the stock widget.
            // Expand so the value read-out is pushed to the row's right edge.
            foreach (var slider in new[] { MasterSlider, SfxSlider, MusicSlider })
            {
                VisualTheme.StyleSlider(slider);
                slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            }
            VisualTheme.StyleCheckButton(FullscreenToggle);
            VisualTheme.StyleCheckButton(VSyncToggle);

            // Elevated surface behind the settings column (containers never
            // draw a panel stylebox of their own).
            var column = GetNodeOrNull<Control>("Container");
            if (column != null)
            {
                VisualTheme.FitColumnHeight(column);
                VisualTheme.AttachSurface(column, 24);
            }

            // The Back button is an action, not a bar: keep it compact and
            // centred instead of stretching across the whole column.
            BackButton.CustomMinimumSize = new Vector2(180, 34);
            BackButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

            // Typography: title is the headline, section headers stay gold.
            var title = GetNodeOrNull<Label>("Container/TitleLabel");
            if (title != null)
                VisualTheme.StyleTitle(title, VisualTheme.Gold, 34);

            RefreshDifficultyButtons();
            RefreshVolumeLabels();

            Visible = false;
            SetProcessInput(false);
        }

        public void Open()
        {
            LoadSettings();
            Visible = true;
            SetProcessInput(true);
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 1f, VisualTheme.ReducedMotionDuration(0.2f));
        }

        private void Close()
        {
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 0f, VisualTheme.ReducedMotionDuration(0.2f));
            _fadeTween.TweenCallback(Callable.From(() =>
            {
                Visible = false;
                SetProcessInput(false);
                OnBack?.Invoke();
            }));
        }

        private void LoadSettings()
        {
            var cfg = new ConfigFile();
            if (cfg.Load(SettingsPath) != Error.Ok) return;

            MasterSlider.Value = SettingsState.ParseVolume(
                cfg.GetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyMaster).AsString(),
                SettingsState.DefaultVolume) * 100f;
            SfxSlider.Value = SettingsState.ParseVolume(
                cfg.GetValue(SettingsState.ConfigSection, SettingsState.ConfigKeySfx).AsString(),
                SettingsState.DefaultVolume) * 100f;
            MusicSlider.Value = SettingsState.ParseVolume(
                cfg.GetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyMusic).AsString(),
                SettingsState.DefaultVolume) * 100f;
            FullscreenToggle.ButtonPressed = SettingsState.ParseBool(
                cfg.GetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyFullscreen).AsString(),
                false);
            VSyncToggle.ButtonPressed = SettingsState.ParseBool(
                cfg.GetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyVSync).AsString(),
                true);
            SelectDifficulty(SettingsState.ParseDifficulty(
                cfg.GetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyDifficulty).AsString()));
        }

        private void SaveSettings()
        {
            var cfg = new ConfigFile();
            cfg.SetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyMaster,
                SettingsState.SerializeFloat((float)MasterSlider.Value / 100f));
            cfg.SetValue(SettingsState.ConfigSection, SettingsState.ConfigKeySfx,
                SettingsState.SerializeFloat((float)SfxSlider.Value / 100f));
            cfg.SetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyMusic,
                SettingsState.SerializeFloat((float)MusicSlider.Value / 100f));
            cfg.SetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyFullscreen,
                SettingsState.SerializeBool(FullscreenToggle.ButtonPressed));
            cfg.SetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyVSync,
                SettingsState.SerializeBool(VSyncToggle.ButtonPressed));
            cfg.SetValue(SettingsState.ConfigSection, SettingsState.ConfigKeyDifficulty,
                SettingsState.SerializeDifficulty(GameManager.Instance?.CurrentDifficulty ?? Difficulty.Normal));
            // Atomic write: temp file + replace (avoid corrupted settings.cfg on crash/OOM).
            string tmp = SettingsPath + ".tmp";
            cfg.Save(tmp);
            System.IO.File.Move(ProjectSettings.GlobalizePath(tmp), ProjectSettings.GlobalizePath(SettingsPath), overwrite: true);
        }

        private void OnMasterChanged(double v) => SaveAndRefresh(MasterValueLabel, MasterSlider, (float)v);
        private void OnSfxChanged(double v) => SaveAndRefresh(SfxValueLabel, SfxSlider, (float)v);
        private void OnMusicChanged(double v) => SaveAndRefresh(MusicValueLabel, MusicSlider, (float)v);

        private void SaveAndRefresh(Label label, HSlider slider, float value)
        {
            SaveSettings();
            label.Text = $"{value:F0}%";
        }

        private void OnFullscreenToggled(bool pressed)
        {
            SaveSettings();
            DisplayServer.WindowSetMode(pressed
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
        }

        private void OnVSyncToggled(bool pressed)
        {
            SaveSettings();
            DisplayServer.WindowSetVsyncMode(pressed
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);
        }

        private void SelectDifficulty(Difficulty difficulty)
        {
            GameManager.Instance?.SetDifficulty(difficulty);
            SaveSettings();
            RefreshDifficultyButtons();
        }

        private void RefreshDifficultyButtons()
        {
            var current = GameManager.Instance?.CurrentDifficulty ?? Difficulty.Normal;

            // Segmented control: the selected segment is the pressed one, so it
            // lights up gold on its own (colour is never the only cue - the
            // pressed geometry differs too).
            VisualTheme.StyleButtonTactileSegment(EasyButton);
            VisualTheme.StyleButtonTactileSegment(NormalButton);
            VisualTheme.StyleButtonTactileSegment(HardButton);

            if (current == Difficulty.Easy)
                HighlightDifficultyButton(EasyButton);
            else if (current == Difficulty.Normal)
                HighlightDifficultyButton(NormalButton);
            else
                HighlightDifficultyButton(HardButton);

            EasyButton.SetPressedNoSignal(current == Difficulty.Easy);
            NormalButton.SetPressedNoSignal(current == Difficulty.Normal);
            HardButton.SetPressedNoSignal(current == Difficulty.Hard);
        }

        private static void HighlightDifficultyButton(Button button)
        {
            button.AddThemeStyleboxOverride("normal",
                VisualTheme.PanelStyleSpecular(VisualTheme.Gold, new Color(1f, 1f, 1f, 0.30f)));
            button.AddThemeColorOverride("font_color", VisualTheme.SurfaceCanvas);
        }

        private void RefreshVolumeLabels()
        {
            MasterValueLabel.Text = $"{MasterSlider.Value:F0}%";
            SfxValueLabel.Text = $"{SfxSlider.Value:F0}%";
            MusicValueLabel.Text = $"{MusicSlider.Value:F0}%";
        }
    }
}
