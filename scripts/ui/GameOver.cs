using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Game Over screen shown when the hero dies. Displays the run summary,
    /// records lifetime stats, and offers Restart / Main Menu.
    ///
    /// Lives in Main.tscn's UI layer (hidden by default).
    /// </summary>
    public partial class GameOver : CanvasLayer
    {
        [Export] public Button RestartButton { get; set; } = null!;
        [Export] public Button MainMenuButton { get; set; } = null!;
        [Export] public Label ScoreLabel { get; set; } = null!;
        [Export] public Label KillsLabel { get; set; } = null!;
        [Export] public Label WavesLabel { get; set; } = null!;
        [Export] public Label DifficultyLabel { get; set; } = null!;
        [Export] public Label PlayTimeLabel { get; set; } = null!;

        private Tween? _fadeTween;
        private int _runSeconds;

        public override void _Ready()
        {
            Visible = false;
            SetProcessInput(false);

            var container = GetNodeOrNull<Control>("Container");
            if (container != null)
            {
                VisualTheme.StylePanel(container);
                // Elevated surface behind the summary column.
                VisualTheme.FitColumnHeight(container);
                VisualTheme.AttachSurface(container, 24);
            }
            VisualTheme.StyleButtonTactilePrimary(RestartButton);
            VisualTheme.StyleButtonTactile(MainMenuButton);
            VisualTheme.StyleButtonFocus(RestartButton);

            // Style title and stat labels.
            var title = GetNodeOrNull<Label>("Container/TitleLabel");
            if (title != null)
            {
                VisualTheme.StyleTitle(title, VisualTheme.LowHealth, 40);
                title.HorizontalAlignment = HorizontalAlignment.Center;
            }
            // Stat rows read as rows: a chip plate behind each line, with the
            // score picked out in the celestial accent.
            foreach (var stat in new[] { KillsLabel, WavesLabel, DifficultyLabel, PlayTimeLabel })
                VisualTheme.StyleStatRow(stat);
            VisualTheme.StyleStatRow(ScoreLabel, true, VisualTheme.Cyan);

            RestartButton.Pressed += Restart;
            MainMenuButton.Pressed += ToMainMenu;
        }

        /// <summary>Show the game over screen with the given run stats.</summary>
        public void Show(int kills, long score, int waves, int runSeconds, Difficulty difficulty)
        {
            _runSeconds = runSeconds;

            // Stat rows keep their "Name: value" caption so the summary is
            // self-describing (display strings only - no data contract change).
            ScoreLabel.Text = $"Score: {score:N0}";
            KillsLabel.Text = $"Kills: {kills:N0}";
            WavesLabel.Text = $"Waves Survived: {waves:N0}";
            DifficultyLabel.Text = $"Difficulty: {difficulty}";
            PlayTimeLabel.Text = $"Play Time: {FormatTime(runSeconds)}";

            // Record lifetime stats and meta scrap reward (R3)
            Dashboard.RecordRunEnd(kills, score, waves, runSeconds);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                int earnedScrap = MetaProgressionState.CalculateRunScrap(waves, kills, gm.DestructionCount);
                gm.MetaProgression.Scrap += earnedScrap;
                gm.SaveMetaProgression();

                // Submit to leaderboards (Crimsonland: global + friends + co-op).
                bool isCoop = gm.ActiveHero != null && gm.ActiveHero.PlayerInput != null
                    && LocalCoopInputManager.Instance?.PlayerCount > 1;
                int playerCount = isCoop ? LocalCoopInputManager.Instance?.PlayerCount ?? 1 : 1;
                LeaderboardService.Instance?.SubmitScore(
                    score, waves, kills, gm.DestructionCount, gm.CurrentDifficulty,
                    isCoop, playerCount);
            }

            Visible = true;
            SetProcessInput(true);
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 1f, VisualTheme.ReducedMotionDuration(0.3f));
        }

        private void Restart()
        {
            Hide();
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.PendingStartAfterSceneChange = true;
            GetTree().ReloadCurrentScene();
        }

        private void ToMainMenu()
        {
            HideGameOver();
            var mainMenu = GD.Load<PackedScene>("res://scenes/ui/MainMenu.tscn");
            if (mainMenu != null)
                GetTree().ChangeSceneToPacked(mainMenu);
        }

        private void HideGameOver()
        {
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 0f, VisualTheme.ReducedMotionDuration(0.3f));
            _fadeTween.TweenCallback(Callable.From(() =>
            {
                Visible = false;
                SetProcessInput(false);
            }));
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
