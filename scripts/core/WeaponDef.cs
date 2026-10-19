using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Unique weapon identity. <see cref="None"/> means the hero is using
    /// their class signature attack (no weapon equipped). Crimsonland carries
    /// one weapon at a time; this enum mirrors that constraint.
    /// </summary>
    public enum WeaponId
    {
        None,
        Pistol,
        SubmachineGun,
        Shotgun,
        AssaultRifle,
        Minigun,
        SniperRifle,
        GrenadeLauncher,
        PlasmaRifle,
        Railgun,
    }

    /// <summary>
    /// Immutable weapon definition. Pure data — no engine dependencies — so
    /// the catalog is headlessly testable and easy to rebalance for the
    /// tournament build. Modeled on Crimsonland's one-weapon-at-a-time
    /// arsenal with ammo and reload.
    /// </summary>
    public sealed class WeaponDef
    {
        public WeaponId Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public float Damage { get; init; } = 10f;
        public float ProjectileSpeed { get; init; } = 700f;
        public float FireRate { get; init; } = 2f; // shots per second
        public int AmmoCapacity { get; init; } = 12;
        public float ReloadTime { get; init; } = 1.5f; // seconds
        public DamageType DamageType { get; init; } = DamageType.Kinetic;
        public int ProjectileCount { get; init; } = 1; // projectiles per shot (shotgun)
        public float SpreadDegrees { get; init; } = 0f; // cone spread for multi-shot
        public bool Piercing { get; init; }
        public bool Explosive { get; init; }
        public float AoERadius { get; init; } = 0f;

        /// <summary>Seconds between shots.</summary>
        public float ShotInterval => 1f / MathF.Max(FireRate, 0.001f);
    }

    /// <summary>
    /// The weapon catalog. Every weapon is defined here; <see cref="Get"/>
    /// returns the <see cref="WeaponId.None"/> sentinel (no weapon) for
    /// unknown ids so callers can safely degrade to class attacks.
    /// </summary>
    public static class WeaponCatalog
    {
        private static readonly WeaponDef[] _all =
        {
            new()
            {
                Id = WeaponId.Pistol,
                Name = "Pistol",
                Description = "Reliable sidearm. Moderate damage, fast reload.",
                Damage = 22f, ProjectileSpeed = 900f, FireRate = 3f,
                AmmoCapacity = 15, ReloadTime = 1.2f,
                DamageType = DamageType.Kinetic, ProjectileCount = 1,
            },
            new()
            {
                Id = WeaponId.SubmachineGun,
                Name = "Submachine Gun",
                Description = "High-volume spray. Low damage per shot, large mag.",
                Damage = 10f, ProjectileSpeed = 850f, FireRate = 12f,
                AmmoCapacity = 40, ReloadTime = 2.2f,
                DamageType = DamageType.Kinetic, ProjectileCount = 1,
            },
            new()
            {
                Id = WeaponId.Shotgun,
                Name = "Shotgun",
                Description = "Devastating close-range spread. Pump-action reload.",
                Damage = 14f, ProjectileSpeed = 700f, FireRate = 1.6f,
                AmmoCapacity = 8, ReloadTime = 2.4f,
                DamageType = DamageType.Kinetic, ProjectileCount = 8, SpreadDegrees = 22f,
            },
            new()
            {
                Id = WeaponId.AssaultRifle,
                Name = "Assault Rifle",
                Description = "Balanced military weapon. Good damage and range.",
                Damage = 16f, ProjectileSpeed = 950f, FireRate = 6f,
                AmmoCapacity = 30, ReloadTime = 2.0f,
                DamageType = DamageType.Kinetic, ProjectileCount = 1,
            },
            new()
            {
                Id = WeaponId.Minigun,
                Name = "Minigun",
                Description = "Suppressive fire. Eats ammo, melts crowds.",
                Damage = 7f, ProjectileSpeed = 900f, FireRate = 20f,
                AmmoCapacity = 100, ReloadTime = 3.0f,
                DamageType = DamageType.Kinetic, ProjectileCount = 1,
            },
            new()
            {
                Id = WeaponId.SniperRifle,
                Name = "Sniper Rifle",
                Description = "Precision long-range shot. High damage, slow fire.",
                Damage = 90f, ProjectileSpeed = 1200f, FireRate = 1.2f,
                AmmoCapacity = 6, ReloadTime = 2.6f,
                DamageType = DamageType.Kinetic, ProjectileCount = 1,
            },
            new()
            {
                Id = WeaponId.GrenadeLauncher,
                Name = "Grenade Launcher",
                Description = "Explosive rounds. Area damage on impact.",
                Damage = 55f, ProjectileSpeed = 550f, FireRate = 1.8f,
                AmmoCapacity = 6, ReloadTime = 2.8f,
                DamageType = DamageType.Explosive, ProjectileCount = 1,
                Explosive = true, AoERadius = 140f,
            },
            new()
            {
                Id = WeaponId.PlasmaRifle,
                Name = "Plasma Rifle",
                Description = "Energy bolts that pierce light armor.",
                Damage = 20f, ProjectileSpeed = 750f, FireRate = 5f,
                AmmoCapacity = 25, ReloadTime = 2.0f,
                DamageType = DamageType.Energy, ProjectileCount = 1, Piercing = true,
            },
            new()
            {
                Id = WeaponId.Railgun,
                Name = "Railgun",
                Description = "Sniper-grade energy weapon. Pierces everything.",
                Damage = 110f, ProjectileSpeed = 1400f, FireRate = 0.9f,
                AmmoCapacity = 8, ReloadTime = 3.2f,
                DamageType = DamageType.Energy, ProjectileCount = 1, Piercing = true,
            },
        };

        static WeaponCatalog() { }

        /// <summary>All defined weapons (excludes <see cref="WeaponId.None"/>).</summary>
        public static ReadOnlySpan<WeaponDef> All => _all;

        /// <summary>Returns the definition for <paramref name="id"/>, or a
        /// no-op "fist" def for <see cref="WeaponId.None"/> / unknown ids.</summary>
        public static WeaponDef Get(WeaponId id)
        {
            foreach (var def in _all)
                if (def.Id == id) return def;
            return new WeaponDef
            {
                Id = WeaponId.None,
                Name = "None",
                Description = "No weapon equipped. Use class signature attack.",
                Damage = 0f, ProjectileSpeed = 0f, FireRate = 0f,
                AmmoCapacity = 0, ReloadTime = 0f,
                DamageType = DamageType.Kinetic, ProjectileCount = 0,
            };
        }
    }
}
