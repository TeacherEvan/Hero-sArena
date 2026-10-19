using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free weapon state machine: current weapon, ammo, reload,
    /// and firing. Modeled on Crimsonland's one-weapon-at-a-time arsenal
    /// with ammo and reload. Zero engine dependencies so every transition is
    /// headlessly testable via xUnit. The Godot Node (HeroBase) owns the
    /// instance and calls <see cref="Update"/> each frame; firing returns
    /// <see cref="ProjectileData"/> arrays that the Node spawns through the
    /// <see cref="ObjectPoolManager"/>.
    /// </summary>
    public sealed class WeaponSystem
    {
        public WeaponId CurrentWeapon { get; private set; } = WeaponId.None;
        public int Ammo { get; private set; }
        public int AmmoCapacity => WeaponCatalog.Get(CurrentWeapon).AmmoCapacity + _ammoCapacityBonus;
        public bool IsReloading { get; private set; }
        public float ReloadTimeRemaining { get; private set; }
        public float FireCooldownRemaining { get; private set; }
        public bool HasWeapon => CurrentWeapon != WeaponId.None;
        public WeaponDef CurrentDef => WeaponCatalog.Get(CurrentWeapon);

        private int _ammoCapacityBonus;
        private float _reloadSpeedMult = 1f;

        /// <summary>Equip <paramref name="weapon"/>. Refills ammo to capacity.
        /// Equipping the same weapon tops off ammo (Crimsonland ammo pickup).
        /// <paramref name="ammoCapacityBonus"/> and <paramref name="reloadSpeedMult"/>
        /// are hero-stat modifiers (perks) applied to this weapon instance.</summary>
        public void Equip(WeaponId weapon, int ammoCapacityBonus = 0, float reloadSpeedMult = 1f)
        {
            if (weapon == CurrentWeapon)
            {
                _ammoCapacityBonus = ammoCapacityBonus;
                _reloadSpeedMult = reloadSpeedMult;
                Ammo = AmmoCapacity;
                return;
            }
            CurrentWeapon = weapon;
            _ammoCapacityBonus = ammoCapacityBonus;
            _reloadSpeedMult = reloadSpeedMult;
            IsReloading = false;
            ReloadTimeRemaining = 0f;
            FireCooldownRemaining = 0f;
            Ammo = AmmoCapacity;
        }

        /// <summary>Unequip the current weapon, returning to the class attack.</summary>
        public void Unequip()
        {
            CurrentWeapon = WeaponId.None;
            Ammo = 0;
            IsReloading = false;
            ReloadTimeRemaining = 0f;
            FireCooldownRemaining = 0f;
        }

        /// <summary>Adds ammo to the equipped weapon (ammo pickup), capped at
        /// capacity. No-op when no weapon is equipped.</summary>
        public void AddAmmo(int amount)
        {
            if (!HasWeapon || amount <= 0) return;
            Ammo = Math.Min(AmmoCapacity, Ammo + amount);
        }

        /// <summary>Begin reloading if the weapon is not full and not already
        /// reloading. Returns true when a reload was started.</summary>
        public bool Reload()
        {
            if (!HasWeapon || IsReloading || Ammo >= AmmoCapacity) return false;
            IsReloading = true;
            ReloadTimeRemaining = CurrentDef.ReloadTime * _reloadSpeedMult;
            FireCooldownRemaining = 0f;
            return true;
        }

        /// <summary>Try to fire one shot at <paramref name="aimDirection"/>
        /// from <paramref name="position"/>. Returns the projectile data for
        /// each shot (length 1 for most weapons, &gt;1 for shotguns), or an
        /// empty array when the weapon cannot fire (empty, reloading, or
        /// still on cooldown). Caller spawns each via the pool.</summary>
        public ProjectileData[] Fire(Vector2 position, Vector2 aimDirection)
        {
            if (!HasWeapon || IsReloading || Ammo <= 0) return Array.Empty<ProjectileData>();
            if (FireCooldownRemaining > 0f) return Array.Empty<ProjectileData>();

            var def = CurrentDef;
            Ammo--;
            FireCooldownRemaining = def.ShotInterval;

            var shots = new List<ProjectileData>(def.ProjectileCount);
            float halfSpread = Mathf.DegToRad(def.SpreadDegrees) * 0.5f;
            for (int i = 0; i < def.ProjectileCount; i++)
            {
                Vector2 dir = aimDirection;
                if (def.ProjectileCount > 1)
                {
                    // Evenly distribute pellets across the spread cone.
                    float t = def.ProjectileCount == 1 ? 0f : i / (float)(def.ProjectileCount - 1);
                    float angle = -halfSpread + halfSpread * 2f * t;
                    dir = aimDirection.Rotated(angle);
                }
                shots.Add(new ProjectileData(
                    position, dir, def.ProjectileSpeed, def.Damage, def.DamageType));
            }
            return shots.ToArray();
        }

        /// <summary>Advance the weapon state by <paramref name="dt"/> seconds.
        /// Called by HeroBase each physics frame. Not in the hot render loop,
        /// so no per-frame allocations occur here.</summary>
        public void Update(float dt)
        {
            if (FireCooldownRemaining > 0f)
            {
                FireCooldownRemaining = Mathf.Max(0f, FireCooldownRemaining - dt);
            }
            if (IsReloading)
            {
                ReloadTimeRemaining = Mathf.Max(0f, ReloadTimeRemaining - dt);
                if (ReloadTimeRemaining <= 0f)
                {
                    IsReloading = false;
                    Ammo = AmmoCapacity;
                }
            }
        }

        /// <summary>Returns true when a shot can be fired right now.</summary>
        public bool CanFire() => HasWeapon && !IsReloading && Ammo > 0 && FireCooldownRemaining <= 0f;

        /// <summary>Forces the current weapon's cooldown to zero (used by
        /// attack-speed perks that want the next shot sooner).</summary>
        public void ResetCooldown() => FireCooldownRemaining = 0f;
    }
}
