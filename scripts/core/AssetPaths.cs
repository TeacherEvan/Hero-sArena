using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    public static class ProjectileSprites
    {
        /// <summary>Maps each DamageType to its 1024px-atlas-style sprite path.</summary>
        public static readonly Dictionary<DamageType, string> Paths = new()
        {
            [DamageType.Kinetic]    = "res://res/projectiles/kinetic.png",
            [DamageType.Energy]     = "res://res/projectiles/energy.png",
            [DamageType.Fire]       = "res://res/projectiles/fire.png",
            [DamageType.Acid]       = "res://res/projectiles/acid.png",
            [DamageType.Lightning]  = "res://res/projectiles/lightning.png",
            [DamageType.Explosive]  = "res://res/projectiles/explosive.png",
        };

        public static string DefaultPath => Paths[DamageType.Kinetic];
    }

    // ── Per-decal-type decal sprite paths ────────────────────────────────────
    public static class DecalSprites
    {
        public static readonly Dictionary<DecalType, string> Paths = new()
        {
            [DecalType.BloodSplat]     = "res://res/decals/blood.png",
            [DecalType.ScorchMark]     = "res://res/decals/scorch.png",
            [DecalType.CraterMark]     = "res://res/decals/crater.png",
            [DecalType.AcidPool]       = "res://res/decals/acid.png",
            [DecalType.ExplosionScorch]= "res://res/decals/explosion.png",
        };

        public static string DefaultPath => Paths[DecalType.BloodSplat];
    }
}
