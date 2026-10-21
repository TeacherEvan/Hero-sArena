using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Intro splash: plays the cinematic video then fades into the Main Menu.
    /// Skippable with any key or mouse click.
    /// </summary>
    public partial class Splash : Control
    {
        [Export] public VideoStreamPlayer VideoPlayer { get; set; } = null!;
        [Export] public Label TitleLabel { get; set; } = null!;
        [Export] public Label SubtitleLabel { get; set; } = null!;

        private Tween? _fadeTween;
        private bool _done;

        public override void _Ready()
        {
            Visible = true;

            VisualTheme.StyleTitle(TitleLabel, VisualTheme.Gold, 64);
            TitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            VisualTheme.StyleLabel(SubtitleLabel, VisualTheme.TextMuted);
            SubtitleLabel.HorizontalAlignment = HorizontalAlignment.Center;

            TitleLabel.Modulate = new Color(1f, 1f, 1f, 0f);
            SubtitleLabel.Modulate = new Color(1f, 1f, 1f, 0f);

            if (VideoPlayer != null && VideoPlayer.Stream == null)
            {
                try
                {
                    if (ResourceLoader.Exists("res://res/splashes/intro.mp4"))
                        VideoPlayer.Stream = GD.Load<VideoStream>("res://res/splashes/intro.mp4");
                }
                catch (Exception ex)
                {
                    GD.Print($"Splash: VideoStream load skipped: {ex.Message}");
                }
            }

            if (VideoPlayer != null && VideoPlayer.Stream != null)
            {
                VideoPlayer.Play();
                VideoPlayer.Finished += OnVideoFinished;

                // Fade the title in once the video is underway.
                _fadeTween = CreateTween();
                _fadeTween.TweenInterval(0.6f);
                var fade = VisualTheme.ReducedMotionDuration(0.75f);
                _fadeTween.TweenProperty(TitleLabel, "modulate:a", 1f, fade);
                _fadeTween.TweenProperty(SubtitleLabel, "modulate:a", 1f, fade);
            }
            else
            {
                // Video stream not available: show title immediately and proceed.
                TitleLabel.Modulate = new Color(1f, 1f, 1f, 1f);
                SubtitleLabel.Modulate = new Color(1f, 1f, 1f, 1f);
                CallDeferred(MethodName.GoToMainMenu);
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (_done) return;
            if (@event is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true })
            {
                Skip();
            }
        }

        private void OnVideoFinished()
        {
            _fadeTween?.Kill();
            GoToMainMenu();
        }

        private void Skip()
        {
            _fadeTween?.Kill();
            VideoPlayer?.Stop();
            GoToMainMenu();
        }

        private void GoToMainMenu()
        {
            if (_done) return;
            _done = true;

            var mainMenu = GD.Load<PackedScene>("res://scenes/ui/MainMenu.tscn");
            if (mainMenu != null)
                GetTree().ChangeSceneToPacked(mainMenu);
            else
                GD.PrintErr("Splash: MainMenu.tscn not found.");
        }

        public override void _ExitTree()
        {
            if (VideoPlayer != null && VideoPlayer.Stream != null)
                VideoPlayer.Finished -= OnVideoFinished;
        }
    }
}
