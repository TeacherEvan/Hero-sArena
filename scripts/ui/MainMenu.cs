using Godot;

namespace HeroArena
{
    /// <summary>Main menu. Hero selection → Start Game, Options, Quit.</summary>
    public partial class MainMenu : Control
    {
        [Export] public Button AtlasButton { get; set; } = null!;
        [Export] public Button ZephyrButton { get; set; } = null!;
        [Export] public Button SynapseButton { get; set; } = null!;
        [Export] public Button VoltButton { get; set; } = null!;
        [Export] public Button StartButton { get; set; } = null!;
        [Export] public Button OptionsButton { get; set; } = null!;
        [Export] public Button QuitButton { get; set; } = null!;
        [Export] public Label HeroDescLabel { get; set; } = null!;
        [Export] public PackedScene GameScene { get; set; } = null!;

        private HeroClass _selectedHero = HeroClass.Atlas;

        public override void _Ready()
        {
            AtlasButton.Pressed += () => SelectHero(HeroClass.Atlas);
            ZephyrButton.Pressed += () => SelectHero(HeroClass.Zephyr);
            SynapseButton.Pressed += () => SelectHero(HeroClass.Synapse);
            VoltButton.Pressed += () => SelectHero(HeroClass.Volt);
            StartButton.Pressed += StartGame;
            OptionsButton.Pressed += OpenOptions;
            QuitButton.Pressed += () => GetTree().Quit();
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

        private void StartGame()
        {
            if (GameScene != null)
                GetTree().ChangeSceneToPacked(GameScene);
            GameManager.Instance.StartGame();
        }

        private void OpenOptions()
        {
            // Options menu stub
        }
    }
}
