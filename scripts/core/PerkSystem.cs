using System;

namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free perk system. All perk effects, names, descriptions and
    /// synergy tags live here so they are headlessly testable via xUnit.
    /// The Godot Node (<see cref="scripts.ui.UpgradeMenu"/>) delegates to this
    /// class for perk application and display text.
    /// </summary>
    public static class PerkSystem
    {
        private static readonly (string Name, string Description, string SynergyTag)[] PerkMeta =
        {
            ("DamageUp",            "+15% Damage",              "Damage"),
            ("SpeedUp",             "+10% Move Speed",          "Movement"),
            ("HealthRegen",         "+2 HP/s while idle",       "Survival"),
            ("MaxHealthUp",         "+15% Max Health",          "Survival"),
            ("AttackSpeedUp",       "-15% Attack cooldown",     "Damage"),
            ("PiercingShots",       "Projectiles pass through", "AoE"),
            ("ExplosiveRounds",     "Projectiles explode on impact", "AoE"),
            ("LifeSteal",           "+3% lifesteal",            "Survival"),
            ("ShieldBurst",         "+2 shield charges",        "Defense"),
            ("DodgeCooldownReduce", "-25% Dodge cooldown",      "Movement"),
            ("CritChanceUp",        "+10% Crit chance",         "Damage"),
            ("AoERadiusUp",         "+20% AoE radius",          "AoE"),
            ("ProjectileCount",     "+1 Projectile per shot",   "Damage"),
            ("SlowOnHit",           "Slow enemies 30% on hit",  "Control"),
            ("BurnOnHit",           "Enemies burn 5 HP/s",      "Damage"),
            ("FrostAura",           "Nearby enemies slowed 20%", "Control"),
            ("ThornArmor",          "Reflect 10% melee damage", "Defense"),
            ("DoubleJump",          "Second dodge charge",      "Movement"),
            ("EnergyDamageUp",      "+15% Energy damage",       "Damage"),
            ("KineticDamageUp",     "+15% Kinetic damage",      "Damage"),
        };

        public static string PerkName(PerkType perk)
        {
            int i = (int)perk;
            if ((uint)i < (uint)PerkMeta.Length)
                return PerkMeta[i].Name;
            return perk.ToString();
        }

        public static string PerkDescription(PerkType perk)
        {
            int i = (int)perk;
            if ((uint)i < (uint)PerkMeta.Length)
                return PerkMeta[i].Description;
            return string.Empty;
        }

        public static string PerkSynergyTag(PerkType perk)
        {
            int i = (int)perk;
            if ((uint)i < (uint)PerkMeta.Length)
                return PerkMeta[i].SynergyTag;
            return string.Empty;
        }

        public static void ApplyPerk(PerkType perk, HeroStats stats)
        {
            if (stats == null) return;
            switch (perk)
            {
                case PerkType.DamageUp:            stats.DamageMult *= 1.15f; break;
                case PerkType.SpeedUp:             stats.MoveSpeed *= 1.10f; break;
                case PerkType.HealthRegen:         stats.HpRegenPerSec += 2f; break;
                case PerkType.MaxHealthUp:         stats.MaxHealth *= 1.15f; break;
                case PerkType.AttackSpeedUp:       stats.AttackCooldownMult *= 0.85f; break;
                case PerkType.PiercingShots:       stats.ProjectilePiercing = true; break;
                case PerkType.ExplosiveRounds:     stats.ProjectileExplosive = true; break;
                case PerkType.LifeSteal:           stats.LifeStealPct += 0.03f; break;
                case PerkType.ShieldBurst:         stats.ShieldCharges += 2; break;
                case PerkType.DodgeCooldownReduce: stats.DodgeCooldownMult *= 0.75f; break;
                case PerkType.CritChanceUp:        stats.CritChance += 0.10f; break;
                case PerkType.AoERadiusUp:         stats.AoeRadiusMult *= 1.20f; break;
                case PerkType.ProjectileCount:     stats.ProjectileCountBonus += 1; break;
                case PerkType.SlowOnHit:           stats.SlowOnHit = true; break;
                case PerkType.BurnOnHit:           stats.BurnOnHit = true; break;
                case PerkType.FrostAura:           stats.FrostAura = true; break;
                case PerkType.ThornArmor:          stats.ThornArmor = true; break;
                case PerkType.DoubleJump:          stats.DodgeCharges = 2; break;
                case PerkType.EnergyDamageUp:      stats.EnergyDamageMult *= 1.15f; break;
                case PerkType.KineticDamageUp:     stats.KineticDamageMult *= 1.15f; break;
            }
        }
    }
}
