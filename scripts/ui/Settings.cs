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
            VisualTheme.StyleButtonTactile(EasyButton);
            VisualTheme.StyleButtonTactile(NormalButton);
            VisualTheme.StyleButtonTactile(HardButton);
            VisualTheme.StyleButtonTactile(BackButton);
            VisualTheme.StyleSliderValueTactile(MasterValueLabel);
            VisualTheme.StyleSliderValueTactile(SfxValueLabel);
            VisualTheme.StyleSliderValueTactile(MusicValueLabel);

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
            _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.2f);
        }

        private void Close()
        {
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 0f, 0.2f);
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

            // Base style for all three, then gold-highlight the selected one.
            VisualTheme.StyleButtonTactile(EasyButton);
            VisualTheme.StyleButtonTactile(NormalButton);
            VisualTheme.StyleButtonTactile(HardButton);

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
                VisualTheme.PanelStyle(VisualTheme.Gold, VisualTheme.Border));
            button.AddThemeColorOverride("font_color", VisualTheme.Background);
        }

        private void RefreshVolumeLabels()
        {
            MasterValueLabel.Text = $"{MasterSlider.Value:F0}%";
            SfxValueLabel.Text = $"{SfxSlider.Value:F0}%";
            MusicValueLabel.Text = $"{MusicSlider.Value:F0}%";
        }
    }
}
