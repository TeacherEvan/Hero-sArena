namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free container for all hero stats that can be modified by perks.
    /// Used by <see cref="PerkSystem"/> to apply perk effects without engine
    /// dependencies, making perk logic headlessly testable via xUnit.
    /// </summary>
    public class HeroStats
    {
        public float MaxHealth        { get; set; } = 100f;
        public float MoveSpeed        { get; set; } = 200f;
        public float DamageMult       { get; set; } = 1f;
        public float HpRegenPerSec    { get; set; } = 0f;
        public float AttackCooldownMult { get; set; } = 1f;
        public bool  ProjectilePiercing { get; set; }
        public bool  ProjectileExplosive { get; set; }
        public float LifeStealPct     { get; set; } = 0f;
        public int   ShieldCharges    { get; set; }
        public float DodgeCooldownMult { get; set; } = 1f;
        public float CritChance       { get; set; } = 0f;
        public float AoeRadiusMult    { get; set; } = 1f;
        public int   ProjectileCountBonus { get; set; }
        public bool  SlowOnHit        { get; set; }
        public bool  BurnOnHit        { get; set; }
        public bool  FrostAura        { get; set; }
        public bool  ThornArmor       { get; set; }
        public int   DodgeCharges     { get; set; } = 1;
        public float EnergyDamageMult { get; set; } = 1f;
        public float KineticDamageMult { get; set; } = 1f;

        // ── Weapon-system perks ──
        public float ReloadSpeedMult    { get; set; } = 1f;   // <1 = faster reload
        public int   AmmoCapacityBonus  { get; set; } = 0;    // extra magazine rounds
        public float CritDamageMult     { get; set; } = 1f;   // critical hit multiplier
        public bool  HealOnKill         { get; set; } = false;// heal on enemy kill
        public float KnockbackForce     { get; set; } = 0f;   // projectile knockback
        public bool  ProjectileRicochet { get; set; } = false;// bullets bounce once

        /// <summary>Restores all stats to their default values.</summary>
        public void ResetToDefaults()
        {
            MaxHealth = 100f;
            MoveSpeed = 200f;
            DamageMult = 1f;
            HpRegenPerSec = 0f;
            AttackCooldownMult = 1f;
            ProjectilePiercing = false;
            ProjectileExplosive = false;
            LifeStealPct = 0f;
            ShieldCharges = 0;
            DodgeCooldownMult = 1f;
            CritChance = 0f;
            AoeRadiusMult = 1f;
            ProjectileCountBonus = 0;
            SlowOnHit = false;
            BurnOnHit = false;
            FrostAura = false;
            ThornArmor = false;
            DodgeCharges = 1;
            EnergyDamageMult = 1f;
            KineticDamageMult = 1f;

            // Weapon-system perks
            ReloadSpeedMult = 1f;
            AmmoCapacityBonus = 0;
            CritDamageMult = 1f;
            HealOnKill = false;
            KnockbackForce = 0f;
            ProjectileRicochet = false;
        }
    }
}
