using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Dashboard: current-run + lifetime stats presented as stat cards.
    /// Reachable from MainMenu. Lifetime stats are persisted to
    /// <c>user://settings.cfg</c> across runs.
    /// </summary>
    public partial class Dashboard : Control
    {
        [Export] public Label ScoreLabel { get; set; } = null!;
        [Export] public Label KillsLabel { get; set; } = null!;
        [Export] public Label WavesLabel { get; set; } = null!;
        [Export] public Label DifficultyLabel { get; set; } = null!;
        [Export] public Label PlayTimeLabel { get; set; } = null!;
        [Export] public Label LifetimeKillsLabel { get; set; } = null!;
        [Export] public Label LifetimeScoreLabel { get; set; } = null!;
        [Export] public Label LifetimeWavesLabel { get; set; } = null!;
        [Export] public Label LifetimePlayTimeLabel { get; set; } = null!;
        [Export] public Button BackButton { get; set; } = null!;

        public Action? OnBack;

        private const string SettingsPath = "user://settings.cfg";
        private double _sessionStartTime;

        public override void _Ready()
        {
            _sessionStartTime = Time.GetUnixTimeFromSystem();
            BackButton.Pressed += () => { Close(); OnBack?.Invoke(); };
            VisualTheme.StylePanel(this);
            VisualTheme.StyleButton(BackButton);

            // Style section headers.
            var sectionSession = GetNodeOrNull<Label>("Container/SectionSession");
            var sectionLifetime = GetNodeOrNull<Label>("Container/SectionLifetime");
            if (sectionSession != null) VisualTheme.StyleSectionHeader(sectionSession);
            if (sectionLifetime != null) VisualTheme.StyleSectionHeader(sectionLifetime);

            // Style stat cards.
            var sessionCard = GetNodeOrNull<Control>("Container/SessionCard");
            var lifetimeCard = GetNodeOrNull<Control>("Container/LifetimeCard");
            if (sessionCard != null) VisualTheme.StyleStatCard(sessionCard);
            if (lifetimeCard != null) VisualTheme.StyleStatCard(lifetimeCard);

            // Style stat values/labels.
            StyleStatLabel(ScoreLabel, true);
            StyleStatLabel(KillsLabel, true);
            StyleStatLabel(WavesLabel, true);
            StyleStatLabel(DifficultyLabel, true);
            StyleStatLabel(PlayTimeLabel, true);
            StyleStatLabel(LifetimeKillsLabel, false);
            StyleStatLabel(LifetimeScoreLabel, false);
            StyleStatLabel(LifetimeWavesLabel, false);
            StyleStatLabel(LifetimePlayTimeLabel, false);

            Refresh();
        }

        private static void StyleStatLabel(Label label, bool isValue)
        {
            if (isValue)
                VisualTheme.StyleStatValue(label);
            else
                VisualTheme.StyleStatLabel(label);
        }

        public void Open()
        {
            Refresh();
            Visible = true;
        }

        private void Close()
        {
            Visible = false;
        }

        private void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                ScoreLabel.Text = gm.Score.ToString("N0");
                KillsLabel.Text = gm.KillCount.ToString("N0");
                WavesLabel.Text = gm.CurrentWave.ToString("N0");
                DifficultyLabel.Text = gm.CurrentDifficulty.ToString();
            }

            var sessionSeconds = Time.GetUnixTimeFromSystem() - _sessionStartTime;
            PlayTimeLabel.Text = FormatTime(sessionSeconds);

            var cfg = new ConfigFile();
            if (cfg.Load(SettingsPath) == Error.Ok)
            {
                var section = SettingsState.ConfigSection;
                var lifetimeKills = SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimeKills).AsString(), 0);
                var lifetimeScore = SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimeScore).AsString(), 0);
                var lifetimeWaves = SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimeWaves).AsString(), 0);
                var lifetimePlay = SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimePlaySeconds).AsString(), 0);

                var gmInstance = GameManager.Instance;
                var scrap = gmInstance?.MetaProgression.Scrap ?? 0;
                LifetimeKillsLabel.Text = $"Lifetime Kills: {lifetimeKills:N0}  |  Scrap: {scrap:N0}";
                LifetimeScoreLabel.Text = lifetimeScore.ToString("N0");
                LifetimeWavesLabel.Text = lifetimeWaves.ToString("N0");
                LifetimePlayTimeLabel.Text = FormatTime(lifetimePlay);
            }
            else
            {
                var gmInstance = GameManager.Instance;
                var scrap = gmInstance?.MetaProgression.Scrap ?? 0;
                LifetimeKillsLabel.Text = $"Lifetime Kills: 0  |  Scrap: {scrap:N0}";
                LifetimeScoreLabel.Text = "0";
                LifetimeWavesLabel.Text = "0";
                LifetimePlayTimeLabel.Text = "0m 0s";
            }
        }

        /// <summary>Call this when a run ends so lifetime stats are saved.</summary>
        public static void RecordRunEnd(int kills, long score, int waves, int playSeconds)
        {
            var cfg = new ConfigFile();
            if (cfg.Load("user://settings.cfg") != Error.Ok)
                cfg = new ConfigFile();

            var section = SettingsState.ConfigSection;
            cfg.SetValue(section, SettingsState.ConfigKeyLifetimeKills,
                SettingsState.SerializeInt(SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimeKills).AsString(), 0) + kills));
            cfg.SetValue(section, SettingsState.ConfigKeyLifetimeScore,
                SettingsState.SerializeInt(SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimeScore).AsString(), 0) + score));
            cfg.SetValue(section, SettingsState.ConfigKeyLifetimeWaves,
                SettingsState.SerializeInt(SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimeWaves).AsString(), 0) + waves));
            cfg.SetValue(section, SettingsState.ConfigKeyLifetimePlaySeconds,
                SettingsState.SerializeInt(SettingsState.ParseInt(
                    cfg.GetValue(section, SettingsState.ConfigKeyLifetimePlaySeconds).AsString(), 0) + playSeconds));
            // Atomic write: temp file + replace (avoid corrupted settings.cfg on crash/OOM).
            string path = "user://settings.cfg";
            string tmp = path + ".tmp";
            cfg.Save(tmp);
            System.IO.File.Move(tmp, path, overwrite: true);
        }

        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s"
                : $"{ts.Minutes:D2}m {ts.Seconds:D2}s";
        }
    }
}
