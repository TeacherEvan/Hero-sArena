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
            if (EventBus.Instance != null)
                EventBus.Instance.OnLevelUp += _onLevelUpHandler;

            Perk1Button.Pressed += () => SelectPerk(0);
            Perk2Button.Pressed += () => SelectPerk(1);
            Perk3Button.Pressed += () => SelectPerk(2);

            // Tactile button styling for perk cards (presentation only; bindings unchanged).
            VisualTheme.StyleButtonTactile(Perk1Button);
            VisualTheme.StyleButtonTactile(Perk2Button);
            VisualTheme.StyleButtonTactile(Perk3Button);
            foreach (var perkButton in new[] { Perk1Button, Perk2Button, Perk3Button })
            {
                perkButton.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                perkButton.CustomMinimumSize = new Vector2(0, 150);
                perkButton.Alignment = HorizontalAlignment.Center;
            }

            // Elevated surface behind the perk row.
            var row = GetNodeOrNull<Control>("HBoxContainer");
            if (row != null)
            {
                VisualTheme.AttachSurface(row, 20);
                // The perk row is the card: keep a clear gap between cards.
                row.AddThemeConstantOverride("separation", 24);
            }

            // Title typography.
            var title = GetNodeOrNull<Label>("Title");
            if (title != null)
            {
                VisualTheme.StyleTitle(title, VisualTheme.Gold, 34);
                title.HorizontalAlignment = HorizontalAlignment.Center;
            }

            Visible = false;
        }

        private void ShowMenu()
        {
            _currentPerks = _progression.GetRandomPerks(3);
            SetPerkCard(Perk1Button, _currentPerks[0]);
            SetPerkCard(Perk2Button, _currentPerks[1]);
            SetPerkCard(Perk3Button, _currentPerks[2]);
            Visible = true;
            GameManager.Instance.PauseGame();
        }

        /// <summary>Perk card content: name plus what the perk actually does.</summary>
        private static void SetPerkCard(Button button, PerkType perk)
        {
            button.Text = $"{PerkDisplayName(perk)}\n{GetPerkDescription(perk)}";
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

            hero.AddPerk(perk);

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
