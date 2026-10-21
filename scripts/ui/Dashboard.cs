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
            var container = GetNodeOrNull<Control>("Container");
            if (container != null)
            {
                VisualTheme.StylePanel(container);
                // Elevated surface behind the dashboard column.
                VisualTheme.FitColumnHeight(container);
                VisualTheme.AttachSurface(container, 24);
            }
            VisualTheme.StyleButtonTactile(BackButton);
            VisualTheme.StyleButtonFocus(BackButton);

            // Style section headers.
            var sectionSession = GetNodeOrNull<Label>("Container/SectionSession");
            var sectionLifetime = GetNodeOrNull<Label>("Container/SectionLifetime");
            if (sectionSession != null) VisualTheme.StyleSectionHeader(sectionSession);
            if (sectionLifetime != null) VisualTheme.StyleSectionHeader(sectionLifetime);

            // Typography: the dashboard title is the headline.
            var title = GetNodeOrNull<Label>("Container/TitleLabel");
            if (title != null)
            {
                VisualTheme.StyleTitle(title, VisualTheme.Gold, 34);
                title.HorizontalAlignment = HorizontalAlignment.Center;
            }

            // Stat cards: the rows carry the chip plates (a VBoxContainer never
            // draws a panel stylebox, so the card would stay invisible).
            var sessionCard = GetNodeOrNull<Control>("Container/SessionCard");
            var lifetimeCard = GetNodeOrNull<Control>("Container/LifetimeCard");
            if (sessionCard != null) VisualTheme.StyleStatCard(sessionCard);
            if (lifetimeCard != null) VisualTheme.StyleStatCard(lifetimeCard);

            // Style stat values/labels: each row is a chip plate.
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
                VisualTheme.StyleStatRow(label, true);
            else
                VisualTheme.StyleStatRow(label, false);
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
                ScoreLabel.Text = $"Score: {gm.Score:N0}";
                KillsLabel.Text = $"Kills: {gm.KillCount:N0}";
                WavesLabel.Text = $"Waves Survived: {gm.CurrentWave:N0}";
                DifficultyLabel.Text = $"Difficulty: {gm.CurrentDifficulty}";
            }

            var sessionSeconds = Time.GetUnixTimeFromSystem() - _sessionStartTime;
            PlayTimeLabel.Text = $"Play Time: {FormatTime(sessionSeconds)}";

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
                LifetimeScoreLabel.Text = $"Lifetime Score: {lifetimeScore:N0}";
                LifetimeWavesLabel.Text = $"Lifetime Waves: {lifetimeWaves:N0}";
                LifetimePlayTimeLabel.Text = $"Lifetime Play Time: {FormatTime(lifetimePlay)}";
            }
            else
            {
                var gmInstance = GameManager.Instance;
                var scrap = gmInstance?.MetaProgression.Scrap ?? 0;
                LifetimeKillsLabel.Text = $"Lifetime Kills: 0  |  Scrap: {scrap:N0}";
                LifetimeScoreLabel.Text = "Lifetime Score: 0";
                LifetimeWavesLabel.Text = "Lifetime Waves: 0";
                LifetimePlayTimeLabel.Text = "Lifetime Play Time: 0m 0s";
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
