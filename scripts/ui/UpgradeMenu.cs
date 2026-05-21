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
            _progression = new LevelProgression();
            AddChild(_progression);

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
                case PerkType.MaxHealthUp: hero.MaxHealth *= 1.15f; break;
            }
        }

        private static string PerkDisplayName(PerkType perk) => perk switch
        {
            PerkType.DamageUp => "+15% Damage",
            PerkType.SpeedUp => "+10% Move Speed",
            PerkType.HealthRegen => "Health Regeneration",
            PerkType.MaxHealthUp => "+15% Max Health",
            PerkType.AttackSpeedUp => "+15% Attack Speed",
            PerkType.PiercingShots => "Piercing Shots",
            PerkType.ExplosiveRounds => "Explosive Rounds",
            PerkType.LifeSteal => "Life Steal",
            PerkType.ShieldBurst => "Shield Burst",
            PerkType.DodgeCooldownReduce => "-25% Dodge Cooldown",
            PerkType.CritChanceUp => "+10% Crit Chance",
            PerkType.AoERadiusUp => "+20% AoE Radius",
            PerkType.ProjectileCount => "+1 Projectile",
            PerkType.SlowOnHit => "Slow on Hit",
            PerkType.BurnOnHit => "Burn on Hit",
            PerkType.FrostAura => "Frost Aura",
            PerkType.ThornArmor => "Thorn Armor",
            PerkType.DoubleJump => "Double Dodge",
            PerkType.EnergyDamageUp => "+15% Energy Damage",
            PerkType.KineticDamageUp => "+15% Kinetic Damage",
            _ => perk.ToString()
        };

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnLevelUp -= _onLevelUpHandler;
        }
    }
}
