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
            [DamageType.Kinetic]    = "res://projectiles/kinetic.png",
            [DamageType.Energy]     = "res://projectiles/energy.png",
            [DamageType.Fire]       = "res://projectiles/fire.png",
            [DamageType.Acid]       = "res://projectiles/acid.png",
            [DamageType.Lightning]  = "res://projectiles/lightning.png",
            [DamageType.Explosive]  = "res://projectiles/explosive.png",
        };

        public static string DefaultPath => Paths[DamageType.Kinetic];
    }

    // ── Per-decal-type decal sprite paths ────────────────────────────────────
    public static class DecalSprites
    {
        public static readonly Dictionary<DecalType, string> Paths = new()
        {
            [DecalType.BloodSplat]     = "res://decals/blood.png",
            [DecalType.ScorchMark]     = "res://decals/scorch.png",
            [DecalType.CraterMark]     = "res://decals/crater.png",
            [DecalType.AcidPool]       = "res://decals/acid.png",
            [DecalType.ExplosionScorch]= "res://decals/explosion.png",
        };

        public static string DefaultPath => Paths[DecalType.BloodSplat];
    }
}
