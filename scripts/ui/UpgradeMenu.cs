using Godot;

namespace HeroArena
{
    /// <summary>
    /// Level-up perk selection menu.
    /// Displays 3 random perks; player clicks to select one, then gameplay resumes.
    /// </summary>
    public partial class UpgradeMenu : Control
    {
        [Export] public Button Perk1Button { get; set; } = null!;
        [Export] public Button Perk2Button { get; set; } = null!;
        [Export] public Button Perk3Button { get; set; } = null!;

        private PerkType[] _currentPerks = new PerkType[3];
        private LevelProgression _progression = null!;

        private System.Action<int>? _onLevelUpHandler;

        public override void _Ready()
        {
            _progression = GameManager.Instance.EnsureLevelProgression();

            _onLevelUpHandler = _ => ShowMenu();
            EventBus.Instance.OnLevelUp += _onLevelUpHandler;

            Perk1Button.Pressed += () => SelectPerk(0);
            Perk2Button.Pressed += () => SelectPerk(1);
            Perk3Button.Pressed += () => SelectPerk(2);

            Visible = false;
        }

        private void ShowMenu()
        {
            _currentPerks = _progression.GetRandomPerks(3);
            Perk1Button.Text = PerkDisplayName(_currentPerks[0]);
            Perk2Button.Text = PerkDisplayName(_currentPerks[1]);
            Perk3Button.Text = PerkDisplayName(_currentPerks[2]);
            Visible = true;
            GameManager.Instance.PauseGame();
        }

        private void SelectPerk(int index)
        {
            ApplyPerk(_currentPerks[index]);
            EventBus.Instance.EmitPerkChosen(_currentPerks[index]);
            Visible = false;
            GameManager.Instance.ResumeGame();
        }

        private static void ApplyPerk(PerkType perk)
        {
            var hero = GameManager.Instance.ActiveHero;
            if (hero == null) return;

            // MaxHealthUp needs the health ratio before MaxHealth changes.
            float healthRatio = perk == PerkType.MaxHealthUp
                ? hero.CurrentHealth / hero.MaxHealth
                : 1f;

            PerkSystem.ApplyPerk(perk, hero.Stats);

            if (perk == PerkType.MaxHealthUp)
                hero.SetCurrentHealth(hero.MaxHealth * healthRatio);
        }

        private static string PerkDisplayName(PerkType perk) => PerkSystem.PerkName(perk);

        /// <summary>Explicit one-line perk effect, for display on perk cards.</summary>
        public static string GetPerkDescription(PerkType perk) => PerkSystem.PerkDescription(perk);

        /// <summary>Synergy tag for a perk, e.g. "Damage", "Survival", "AoE".</summary>
        public static string GetPerkSynergyTag(PerkType perk) => PerkSystem.PerkSynergyTag(perk);

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnLevelUp -= _onLevelUpHandler;
        }
    }
}
