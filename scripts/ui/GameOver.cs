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
            if (container != null) VisualTheme.StylePanel(container);
            VisualTheme.StyleButtonPrimary(RestartButton);
            VisualTheme.StyleButton(MainMenuButton);

            // Style title and stat labels.
            var title = GetNodeOrNull<Label>("Container/TitleLabel");
            if (title != null) VisualTheme.StyleLabel(title);
            VisualTheme.StyleStatValue(ScoreLabel);
            VisualTheme.StyleStatLabel(KillsLabel);
            VisualTheme.StyleStatLabel(WavesLabel);
            VisualTheme.StyleStatLabel(DifficultyLabel);
            VisualTheme.StyleStatLabel(PlayTimeLabel);

            RestartButton.Pressed += Restart;
            MainMenuButton.Pressed += ToMainMenu;
        }

        /// <summary>Show the game over screen with the given run stats.</summary>
        public void Show(int kills, long score, int waves, int runSeconds, Difficulty difficulty)
        {
            _runSeconds = runSeconds;

            ScoreLabel.Text = score.ToString("N0");
            KillsLabel.Text = kills.ToString("N0");
            WavesLabel.Text = waves.ToString("N0");
            DifficultyLabel.Text = difficulty.ToString();
            PlayTimeLabel.Text = FormatTime(runSeconds);

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
            _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.3f);
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
            _fadeTween.TweenProperty(this, "modulate:a", 0f, 0.3f);
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
