using Godot;
using System;

namespace HeroArena
{
    /// <summary>Main menu. Hero selection → Start Game, Tutorial, Sandbox, Options, Quit.</summary>
    public partial class MainMenu : Control
    {
        [Export] public Button AtlasButton { get; set; } = null!;
        [Export] public Button ZephyrButton { get; set; } = null!;
        [Export] public Button SynapseButton { get; set; } = null!;
        [Export] public Button VoltButton { get; set; } = null!;
        [Export] public Button StartButton { get; set; } = null!;
        [Export] public Button OptionsButton { get; set; } = null!;
        [Export] public Button QuitButton { get; set; } = null!;
        [Export] public Button SandboxButton { get; set; } = null!;
        [Export] public Button TutorialButton { get; set; } = null!;
        [Export] public Label HeroDescLabel { get; set; } = null!;
        [Export] public PackedScene GameScene { get; set; } = null!;
        [Export] public PackedScene SandboxScene { get; set; } = null!;
        [Export] public Button EasyButton { get; set; } = null!;
        [Export] public Button NormalButton { get; set; } = null!;
        [Export] public Button HardButton { get; set; } = null!;
        [Export] public Label DifficultyLabel { get; set; } = null!;

        private HeroClass _selectedHero = HeroClass.Atlas;
        private Difficulty _difficulty = Difficulty.Normal;
        private const string SettingsPath = "user://settings.cfg";

        public override void _Ready()
        {
            AtlasButton.Pressed += () => SelectHero(HeroClass.Atlas);
            ZephyrButton.Pressed += () => SelectHero(HeroClass.Zephyr);
            SynapseButton.Pressed += () => SelectHero(HeroClass.Synapse);
            VoltButton.Pressed += () => SelectHero(HeroClass.Volt);
            StartButton.Pressed += StartGame;
            TutorialButton.Pressed += StartTutorial;
            OptionsButton.Pressed += OpenOptions;
            SandboxButton.Pressed += OpenSandbox;
            QuitButton.Pressed += () => GetTree().Quit();

            // Difficulty (R2): radio-style trio backed by the GameManager
            // state machine, seeded from the persisted user:// choice.
            EasyButton.Pressed += () => SelectDifficulty(Difficulty.Easy);
            NormalButton.Pressed += () => SelectDifficulty(Difficulty.Normal);
            HardButton.Pressed += () => SelectDifficulty(Difficulty.Hard);
            SelectDifficulty(LoadSavedDifficulty());
        }

        private void SelectHero(HeroClass hero)
        {
            _selectedHero = hero;
            HeroDescLabel.Text = hero switch
            {
                HeroClass.Atlas => "Atlas – Strength\nSeismic Gauntlets: AoE slam\nUnique: Tectonic Plating (3s invulnerability + reflect)",
                HeroClass.Zephyr => "Zephyr – Agility\nPlasma Blades: piercing projectiles\nUnique: Hyper-Dash (0.3× enemy time, 300% speed)",
                HeroClass.Synapse => "Synapse – Intelligence\nNeural Lasers: chaining beams (5 targets)\nUnique: Overclock (mind-control 50 enemies)",
                HeroClass.Volt => "Volt – Speed\nArc Lightning: forks to 8 enemies\nUnique: Static Field (EMP zone, stun 3s)",
                _ => ""
            };
        }

        // ── Difficulty (R2) — delegates to GameManagerState via GameManager ──

        private void SelectDifficulty(Difficulty difficulty)
        {
            _difficulty = difficulty;
            GameManager.Instance.SetDifficulty(difficulty);
            RefreshDifficultyButtons();
            PersistDifficulty();
        }

        private static Difficulty LoadSavedDifficulty()
        {
            var cfg = new ConfigFile();
            if (cfg.Load(SettingsPath) != Error.Ok)
                return Difficulty.Normal;
            return MenuDifficultyState.Parse(
                cfg.GetValue(MenuDifficultyState.ConfigSection, MenuDifficultyState.ConfigKey, "Normal").AsString());
        }

        private void PersistDifficulty()
        {
            var cfg = new ConfigFile();
            cfg.SetValue(MenuDifficultyState.ConfigSection, MenuDifficultyState.ConfigKey,
                MenuDifficultyState.Serialize(_difficulty));
            cfg.Save(SettingsPath);
        }

        private void RefreshDifficultyButtons()
        {
            EasyButton.SetPressedNoSignal(MenuDifficultyState.IsSelected(_difficulty, Difficulty.Easy));
            NormalButton.SetPressedNoSignal(MenuDifficultyState.IsSelected(_difficulty, Difficulty.Normal));
            HardButton.SetPressedNoSignal(MenuDifficultyState.IsSelected(_difficulty, Difficulty.Hard));
            DifficultyLabel.Text = $"Difficulty: {MenuDifficultyState.Serialize(_difficulty)}";
        }

        private void StartGame()
        {
            if (GameScene == null) return;
            // Defer the actual StartGame() until after the new scene is mounted and
            // its WaveManager / SpatialGrid are reachable. ChangeSceneToPacked runs on
            // the next frame; CallDeferred puts us one frame later still, which is
            // when the new scene's _Ready has fired and GameManager.WaveManager
            // (assigned by the scene) is non-null.
            GameManager.Instance.PendingStartAfterSceneChange = true;
            GameManager.Instance.PendingHeroClass = _selectedHero;
            GetTree().ChangeSceneToPacked(GameScene);
            CallDeferred(MethodName.OnSceneChanged);
        }

        private void StartTutorial()
        {
            if (GameScene == null) return;
            GameManager.Instance.PendingStartAfterSceneChange = true;
            GameManager.Instance.PendingTutorial = true;
            GameManager.Instance.StartTutorial(); // sets state = Tutorial on the autoload
            GetTree().ChangeSceneToPacked(GameScene);
            CallDeferred(MethodName.OnSceneChanged);
        }

        // CallDeferred target: Godot invokes script methods on the node
        // instance, so it must remain non-static even though it reads no
        // instance state (CA1822 exception, documented in lieu of static).
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style", "CA1822:Mark members as static",
            Justification = "CallDeferred dispatch target; Godot bridges instance methods only")]
        private void OnSceneChanged()
        {
            if (GameManager.Instance.PendingStartAfterSceneChange)
            {
                GameManager.Instance.PendingStartAfterSceneChange = false;
                // StartTutorial() already called GameManager.StartTutorial() before
                // the scene change; StartGame() would override to Playing, so skip it.
                if (!GameManager.Instance.PendingTutorial)
                    GameManager.Instance.StartGame();
                GameManager.Instance.PendingTutorial = false;
            }
        }

        private void OpenOptions()
        {
            // Options menu stub
        }

        private void OpenSandbox()
        {
            if (SandboxScene == null) return;
            GetTree().ChangeSceneToPacked(SandboxScene);
        }
    }
}
