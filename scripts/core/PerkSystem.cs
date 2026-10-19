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
            ("ReloadSpeedUp",       "-20% weapon reload time",  "Weapon"),
            ("AmmoUp",              "+4 magazine capacity",     "Weapon"),
            ("CritDamageUp",        "+50% critical damage",     "Damage"),
            ("HealOnKill",          "Heal 5 HP on kill",        "Survival"),
            ("Knockback",           "Projectiles knock back",   "Control"),
            ("Ricochet",            "Projectiles bounce once",  "AoE"),
            ("LifeStealUp",         "+3% lifesteal",            "Survival"),
            ("MaxHealthPlus",       "+10% max health",          "Survival"),
            ("SpeedPlus",           "+10% move speed",          "Movement"),
            ("DamagePlus",          "+10% damage",              "Damage"),
            ("RegenPlus",           "+2 HP/s regen",            "Survival"),
            ("ShieldPlus",          "+2 shield charges",        "Defense"),
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
                case PerkType.ReloadSpeedUp:       stats.ReloadSpeedMult *= 0.80f; break;
                case PerkType.AmmoUp:              stats.AmmoCapacityBonus += 4; break;
                case PerkType.CritDamageUp:        stats.CritDamageMult *= 1.50f; break;
                case PerkType.HealOnKill:          stats.HealOnKill = true; break;
                case PerkType.Knockback:           stats.KnockbackForce = 350f; break;
                case PerkType.Ricochet:            stats.ProjectileRicochet = true; break;
                case PerkType.LifeStealUp:         stats.LifeStealPct += 0.03f; break;
                case PerkType.MaxHealthPlus:       stats.MaxHealth *= 1.10f; break;
                case PerkType.SpeedPlus:           stats.MoveSpeed *= 1.10f; break;
                case PerkType.DamagePlus:          stats.DamageMult *= 1.10f; break;
                case PerkType.RegenPlus:           stats.HpRegenPerSec += 2f; break;
                case PerkType.ShieldPlus:          stats.ShieldCharges += 2; break;
            }
        }

        // ── Perk Synergies (R2) ────────────────────────────────────────────────
        public record struct PerkSynergyCombo(string Name, string Description, PerkType ReqA, PerkType ReqB);

        public static readonly PerkSynergyCombo[] SynergyCombos =
        {
            new("Blizzard", "Frost Aura + Slow on Hit grants +25% AoE Radius", PerkType.FrostAura, PerkType.SlowOnHit),
            new("Firestorm", "Burn on Hit + Explosive Rounds grants +20% Damage", PerkType.BurnOnHit, PerkType.ExplosiveRounds),
            new("Vampiric Aegis", "Life Steal + Thorn Armor grants +30 Max Health", PerkType.LifeSteal, PerkType.ThornArmor),
            new("Phantom Step", "Double Jump + Dodge Cooldown grants +15% Move Speed", PerkType.DoubleJump, PerkType.DodgeCooldownReduce),
            new("Rapid Reload", "Reload Speed + Ammo Up grants -30% reload time and +6 capacity", PerkType.ReloadSpeedUp, PerkType.AmmoUp),
            new("Pinball", "Knockback + Ricochet: bullets bounce and knock enemies into each other", PerkType.Knockback, PerkType.Ricochet),
        };

        /// <summary>
        /// Evaluates active perks taken by the hero and applies any unlocked synergy bonuses.
        /// </summary>
        public static void ApplySynergies(System.Collections.Generic.HashSet<PerkType> takenPerks, HeroStats stats)
        {
            if (stats == null || takenPerks == null) return;
            foreach (var combo in SynergyCombos)
            {
                if (takenPerks.Contains(combo.ReqA) && takenPerks.Contains(combo.ReqB))
                {
                    ApplySynergyBonus(combo.Name, stats);
                }
            }
        }

        private static void ApplySynergyBonus(string comboName, HeroStats stats)
        {
            switch (comboName)
            {
                case "Blizzard":
                    stats.AoeRadiusMult *= 1.25f;
                    break;
                case "Firestorm":
                    stats.DamageMult *= 1.20f;
                    break;
                case "Vampiric Aegis":
                    stats.MaxHealth += 30f;
                    break;
                case "Phantom Step":
                    stats.MoveSpeed *= 1.15f;
                    break;
                case "Rapid Reload":
                    stats.ReloadSpeedMult *= 0.70f; // stacks with ReloadSpeedUp's 0.80
                    stats.AmmoCapacityBonus += 6;
                    break;
                case "Pinball":
                    stats.ProjectileRicochet = true;
                    stats.KnockbackForce = MathF.Max(stats.KnockbackForce, 500f);
                    break;
            }
        }
    }
}
