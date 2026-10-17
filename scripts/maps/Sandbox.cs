using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Sandbox / Practice mode. No waves, no GameOver. Lets the player try any
    /// hero, spawn target dummies, toggle god mode, and apply perks freely to
    /// experiment risk-free. Recommended first step of R3 (Perk Transparency +
    /// Sandbox Mode).
    /// </summary>
    public partial class Sandbox : Node2D
    {
        [Export] public Button BtnAtlas { get; set; } = null!;
        [Export] public Button BtnZephyr { get; set; } = null!;
        [Export] public Button BtnSynapse { get; set; } = null!;
        [Export] public Button BtnVolt { get; set; } = null!;
        [Export] public Button BtnSpawnDummy { get; set; } = null!;
        [Export] public Button BtnGodMode { get; set; } = null!;
        [Export] public Button BtnApplyPerk { get; set; } = null!;
        [Export] public Button BtnResetStats { get; set; } = null!;
        [Export] public Button BtnBack { get; set; } = null!;
        [Export] public Label HeroLabel { get; set; } = null!;
        [Export] public Label PerkLabel { get; set; } = null!;
        [Export] public Marker2D HeroSpawnPoint { get; set; } = null!;
        [Export] public Node2D DummyContainer { get; set; } = null!;

        private HeroBase? _hero;
        private HeroClass _selectedHero = HeroClass.Atlas;
        private bool _godMode = false;
        private readonly List<EnemyBase> _dummies = new();
        private int _perkIndex = 0;

        public override void _Ready()
        {
            BtnAtlas.Pressed += () => SelectHero(HeroClass.Atlas);
            BtnZephyr.Pressed += () => SelectHero(HeroClass.Zephyr);
            BtnSynapse.Pressed += () => SelectHero(HeroClass.Synapse);
            BtnVolt.Pressed += () => SelectHero(HeroClass.Volt);
            BtnSpawnDummy.Pressed += SpawnDummy;
            BtnGodMode.Pressed += ToggleGodMode;
            BtnApplyPerk.Pressed += ApplyNextPerk;
            BtnResetStats.Pressed += ResetStats;
            BtnBack.Pressed += BackToMainMenu;

            SelectHero(_selectedHero);
        }

        private void SelectHero(HeroClass heroClass)
        {
            if (_hero != null && IsInstanceValid(_hero))
                _hero.QueueFree();

            _selectedHero = heroClass;
            HeroLabel.Text = $"Hero: {heroClass}";

            var scene = heroClass switch
            {
                HeroClass.Atlas    => GD.Load<PackedScene>("res://scenes/heroes/Atlas.tscn"),
                HeroClass.Zephyr   => GD.Load<PackedScene>("res://scenes/heroes/Zephyr.tscn"),
                HeroClass.Synapse  => GD.Load<PackedScene>("res://scenes/heroes/Synapse.tscn"),
                HeroClass.Volt     => GD.Load<PackedScene>("res://scenes/heroes/Volt.tscn"),
                _                  => GD.Load<PackedScene>("res://scenes/heroes/Atlas.tscn"),
            };

            var hero = scene.Instantiate<HeroBase>();
            var parent = DummyContainer?.GetParent() ?? GetTree().CurrentScene;
            parent.AddChild(hero);
            hero.GlobalPosition = HeroSpawnPoint != null ? HeroSpawnPoint.GlobalPosition : Vector2.Zero;
            GameManager.Instance.ActiveHero = hero;
            _hero = hero;
        }

        private void SpawnDummy()
        {
            var scene = GD.Load<PackedScene>("res://scenes/enemies/Drone.tscn");
            var dummy = scene.Instantiate<EnemyBase>();
            var offset = new Vector2(_dummies.Count * 60f, 0f);
            dummy.GlobalPosition = DummyContainer != null
                ? DummyContainer.GlobalPosition + offset
                : Vector2.Zero;
            dummy.BecomeDummy();
            (DummyContainer ?? GetTree().CurrentScene).AddChild(dummy);
            GameManager.Instance.IncrementEnemyCount();
            _dummies.Add(dummy);
        }

        private void ToggleGodMode()
        {
            _godMode = !_godMode;
            if (_hero != null) _hero.Invulnerable = _godMode;
            BtnGodMode.Text = $"God Mode: {(_godMode ? "On" : "Off")}";
        }

        private void ApplyNextPerk()
        {
            var perks = (PerkType[])Enum.GetValues(typeof(PerkType));
            var perk = perks[_perkIndex % perks.Length];
            _perkIndex++;

            var hero = GameManager.Instance.ActiveHero;
            if (hero == null) return;

            float healthRatio = perk == PerkType.MaxHealthUp
                ? hero.CurrentHealth / hero.MaxHealth
                : 1f;
            PerkSystem.ApplyPerk(perk, hero.Stats);
            if (perk == PerkType.MaxHealthUp)
                hero.SetCurrentHealth(hero.MaxHealth * healthRatio);

            PerkLabel.Text = $"Last perk: {PerkSystem.PerkName(perk)} — {PerkSystem.PerkDescription(perk)}";
        }

        private void ResetStats()
        {
            var hero = GameManager.Instance.ActiveHero;
            if (hero == null) return;
            hero.Stats.ResetToDefaults();
            hero.SetCurrentHealth(hero.MaxHealth);
            PerkLabel.Text = "Stats reset.";
        }

        private void BackToMainMenu()
        {
            // Runtime path load breaks the MainMenu <-> Sandbox ext_resource cycle.
            GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
        }

        public override void _ExitTree()
        {
            foreach (var dummy in _dummies)
                if (IsInstanceValid(dummy)) dummy.QueueFree();
            _dummies.Clear();
        }
    }
}
