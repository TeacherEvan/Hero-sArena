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
        [Export] public Button IgnisButton { get; set; } = null!;
        [Export] public Button ToxisButton { get; set; } = null!;
        [Export] public Button HavocButton { get; set; } = null!;
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
        [Export] public Button DashboardButton { get; set; } = null!;
        [Export] public PackedScene SettingsScene { get; set; } = null!;
        [Export] public PackedScene DashboardScene { get; set; } = null!;

        private HeroClass _selectedHero = HeroClass.Atlas;
        private Difficulty _difficulty = Difficulty.Normal;
        private const string SettingsPath = "user://settings.cfg";
        private Settings? _settingsOverlay;
        private Dashboard? _dashboardOverlay;

        public override void _Ready()
        {
            AtlasButton.Pressed += () => SelectHero(HeroClass.Atlas);
            ZephyrButton.Pressed += () => SelectHero(HeroClass.Zephyr);
            SynapseButton.Pressed += () => SelectHero(HeroClass.Synapse);
            VoltButton.Pressed += () => SelectHero(HeroClass.Volt);
            IgnisButton.Pressed += () => SelectHero(HeroClass.Ignis);
            ToxisButton.Pressed += () => SelectHero(HeroClass.Toxis);
            HavocButton.Pressed += () => SelectHero(HeroClass.Havoc);
            StartButton.Pressed += StartGame;
            TutorialButton.Pressed += StartTutorial;
            OptionsButton.Pressed += OpenSettings;
            DashboardButton.Pressed += OpenDashboard;
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
                HeroClass.Ignis => "Ignis – Pyromancer\nFlame Stream: rapid fire streams\nUnique: Inferno Nova (360° thermal shockwave)",
                HeroClass.Toxis => "Toxis – Alchemist\nAcid Lob: corrosive piercing canisters\nUnique: Caustic Eruption (12-way burst + chemical field)",
                HeroClass.Havoc => "Havoc – Demolitionist\nCluster Rocket: high-impact explosive ordnance\nUnique: Orbital Barrage (heavy artillery strike)",
                _ => ""
            };
        }

        // ── Difficulty (R2) — delegates to GameManagerState via GameManager ──

        private void SelectDifficulty(Difficulty difficulty)
        {
            _difficulty = difficulty;
            GameManager.Instance?.SetDifficulty(difficulty);
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
            // Read-modify-write atomically: load existing settings, update only
            // the difficulty key, then save via temp file + replace. Prevents
            // wiping audio/graphics settings (audit: split-brain settings
            // persistence between MainMenu and Settings) and config corruption
            // on crash/OOM (non-atomic direct save).
            var cfg = new ConfigFile();
            if (cfg.Load(SettingsPath) == Error.Ok)
            {
                // existing keys (master_volume, sfx_volume, ...) are preserved
            }
            cfg.SetValue(MenuDifficultyState.ConfigSection, MenuDifficultyState.ConfigKey,
                MenuDifficultyState.Serialize(_difficulty));
            string tmp = SettingsPath + ".tmp";
            cfg.Save(tmp);
            System.IO.File.Move(ProjectSettings.GlobalizePath(tmp), ProjectSettings.GlobalizePath(SettingsPath), overwrite: true);
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
            // Flag the run so MainBootstrap (which lives in Main.tscn and survives
            // the scene change) can start it after wiring WaveManager/SpatialGrid.
            // We deliberately do NOT CallDeferred(OnSceneChanged) here: the
            // MainMenu node is freed by ChangeSceneToPacked, so a deferred call on
            // it would be dropped and the run would never start (no waves, no
            // enemies). MainBootstrap is the single owner of starting the run.
            GameManager.Instance.PendingStartAfterSceneChange = true;
            GameManager.Instance.PendingHeroClass = _selectedHero;
            GetTree().ChangeSceneToPacked(GameScene);
        }

        private void StartTutorial()
        {
            if (GameScene == null) return;
            GameManager.Instance.PendingStartAfterSceneChange = true;
            GameManager.Instance.PendingTutorial = true;
            GameManager.Instance.StartTutorial(); // sets state = Tutorial on the autoload
            GetTree().ChangeSceneToPacked(GameScene);
        }

        private void OpenSettings()
        {
            if (SettingsScene == null) return;
            _settingsOverlay = SettingsScene.Instantiate<Settings>();
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

        private void OpenDashboard()
        {
            if (DashboardScene == null) return;
            _dashboardOverlay = DashboardScene.Instantiate<Dashboard>();
            _dashboardOverlay.OnBack += CloseDashboard;
            AddChild(_dashboardOverlay);
            _dashboardOverlay.Open();
        }

        private void CloseDashboard()
        {
            if (_dashboardOverlay != null)
            {
                RemoveChild(_dashboardOverlay);
                _dashboardOverlay.QueueFree();
                _dashboardOverlay = null;
            }
        }

        private void OpenSandbox()
        {
            if (SandboxScene == null) return;
            GetTree().ChangeSceneToPacked(SandboxScene);
        }
    }
}
