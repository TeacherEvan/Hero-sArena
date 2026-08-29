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
            Visible = false;
            GameManager.Instance.ResumeGame();
        }

        private static void ApplyPerk(PerkType perk)
        {
            var hero = GameManager.Instance.ActiveHero;
            if (hero == null) return;
            switch (perk)
            {
                case PerkType.DamageUp: break; // Applied in hero weapon systems
                case PerkType.SpeedUp: hero.MoveSpeed *= 1.1f; break;
                case PerkType.MaxHealthUp:
                    float ratio = hero.CurrentHealth / hero.MaxHealth;
                    hero.MaxHealth *= 1.15f;
                    hero.SetCurrentHealth(hero.MaxHealth * ratio);
                    break;
            }
        }

        private static readonly string[] _perkNames = new string[]
        {
            "+15% Damage",
            "+10% Move Speed",
            "Health Regeneration",
            "+15% Max Health",
            "+15% Attack Speed",
            "Piercing Shots",
            "Explosive Rounds",
            "Life Steal",
            "Shield Burst",
            "-25% Dodge Cooldown",
            "+10% Crit Chance",
            "+20% AoE Radius",
            "+1 Projectile",
            "Slow on Hit",
            "Burn on Hit",
            "Frost Aura",
            "Thorn Armor",
            "Double Dodge",
            "+15% Energy Damage",
            "+15% Kinetic Damage"
        };

        private static string PerkDisplayName(PerkType perk)
        {
            int index = (int)perk;
            if ((uint)index < (uint)_perkNames.Length)
                return _perkNames[index];
            return perk.ToString();
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnLevelUp -= _onLevelUpHandler;
        }
    }
}
