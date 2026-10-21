using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Pause overlay shown during gameplay: Resume / Restart / Settings /
    /// Main Menu / Quit. Hooks ESC to toggle pause/resume.
    ///
    /// Lives in Main.tscn's UI layer (hidden by default). ESC is handled by
    /// MainBootstrap which has PauseMode.Always so it works while the tree
    /// is paused.
    /// </summary>
    public partial class PauseMenu : CanvasLayer
    {
        [Export] public Button ResumeButton { get; set; } = null!;
        [Export] public Button RestartButton { get; set; } = null!;
        [Export] public Button SettingsButton { get; set; } = null!;
        [Export] public Button MainMenuButton { get; set; } = null!;
        [Export] public Button QuitButton { get; set; } = null!;

        private Settings? _settingsOverlay;
        private PackedScene _settingsScene = null!;
        private Tween? _fadeTween;

        public override void _Ready()
        {
            Visible = false;
            SetProcessInput(false);

            ResumeButton.Pressed += Resume;
            RestartButton.Pressed += Restart;
            SettingsButton.Pressed += OpenSettings;
            MainMenuButton.Pressed += ToMainMenu;
            QuitButton.Pressed += () => GetTree().Quit();

            _settingsScene = GD.Load<PackedScene>("res://scenes/ui/Settings.tscn");

            var container = GetNodeOrNull<Control>("Container");
            if (container != null)
            {
                VisualTheme.StylePanel(container);
                // Elevated surface behind the pause column (containers never
                // draw a panel stylebox of their own).
                VisualTheme.FitColumnHeight(container);
                VisualTheme.AttachSurface(container, 24);
            }
            VisualTheme.StyleButtonTactilePrimary(ResumeButton);
            VisualTheme.StyleButtonTactile(RestartButton);
            VisualTheme.StyleButtonTactile(SettingsButton);
            VisualTheme.StyleButtonTactile(MainMenuButton);
            VisualTheme.StyleButtonTactileDanger(QuitButton);
            VisualTheme.StyleButtonFocus(ResumeButton);

            // Style title.
            var title = GetNodeOrNull<Label>("Container/TitleLabel");
            if (title != null)
            {
                VisualTheme.StyleTitle(title, VisualTheme.Gold, 34);
                title.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }

        /// <summary>Toggle pause/resume. Called by MainBootstrap on ESC.</summary>
        public void Toggle()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentState == GameState.Playing)
            {
                gm.PauseGame();      // Playing → Paused, pauses the tree
                ShowMenu();
            }
            else if (gm.CurrentState == GameState.Paused)
            {
                gm.ResumeGame();     // Paused → Playing, unpauses the tree
                HideMenu();
            }
        }

        private void ShowMenu()
        {
            Visible = true;
            SetProcessInput(true);
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 1f, VisualTheme.ReducedMotionDuration(0.2f));
        }

        private void HideMenu()
        {
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 0f, VisualTheme.ReducedMotionDuration(0.2f));
            _fadeTween.TweenCallback(Callable.From(() =>
            {
                Visible = false;
                SetProcessInput(false);
                CloseSettings();
            }));
        }

        private void Resume() => HideMenu();

        private void Restart()
        {
            HideMenu();
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.PendingStartAfterSceneChange = true;
            GetTree().ReloadCurrentScene();
        }

        private void OpenSettings()
        {
            if (_settingsScene == null) return;
            _settingsOverlay = _settingsScene.Instantiate<Settings>();
            _settingsOverlay.OnBack += CloseSettings;
            AddChild(_settingsOverlay);
            _settingsOverlay.Open();
        }

        private void CloseSettings()
        {
            if (_settingsOverlay != null)
            {
                RemoveChild(_settingsOverlay);
                _settingsOverlay.QueueFree();
                _settingsOverlay = null;
            }
        }

        private void ToMainMenu()
        {
            HideMenu();
            var mainMenu = GD.Load<PackedScene>("res://scenes/ui/MainMenu.tscn");
            if (mainMenu != null)
                GetTree().ChangeSceneToPacked(mainMenu);
        }

        public override void _ExitTree()
        {
            CloseSettings();
        }
    }
}
