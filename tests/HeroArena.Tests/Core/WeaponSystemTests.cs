using FluentAssertions;
using Godot;
using Xunit;

namespace HeroArena.Tests.Core;

public class WeaponSystemTests
{
    [Fact]
    public void Default_HasNoWeapon()
    {
        var w = new WeaponSystem();
        w.HasWeapon.Should().BeFalse();
        w.CurrentWeapon.Should().Be(WeaponId.None);
        w.CanFire().Should().BeFalse();
    }

    [Fact]
    public void Equip_RefillsAmmoToCapacity()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        w.Ammo.Should().Be(15);
        w.HasWeapon.Should().BeTrue();
    }

    [Fact]
    public void Equip_SameWeapon_TopsOffAmmo()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        w.Fire(default, Vector2.Right); // 15 -> 14
        w.Equip(WeaponId.Pistol);
        w.Ammo.Should().Be(15);
    }

    [Fact]
    public void Fire_ConsumesAmmoAndSetsCooldown()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        var shots = w.Fire(default, Vector2.Right);
        shots.Length.Should().Be(1);
        w.Ammo.Should().Be(14);
        w.CanFire().Should().BeFalse(); // still on cooldown
    }

    [Fact]
    public void Fire_Empty_AmmoReturnsNoShots()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        for (int i = 0; i < 15; i++)
        {
            w.Fire(default, Vector2.Right);
            w.Update(0.4f); // expire pistol cooldown (~0.33s)
        }
        w.Fire(default, Vector2.Right).Length.Should().Be(0);
        w.Ammo.Should().Be(0);
    }

    [Fact]
    public void Reload_Completes_AfterTime()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        w.Fire(default, Vector2.Right); // consume 1 ammo
        w.Reload().Should().BeTrue();
        w.IsReloading.Should().BeTrue();
        w.Update(1.2f); // Pistol reload time
        w.IsReloading.Should().BeFalse();
        w.Ammo.Should().Be(15);
    }

    [Fact]
    public void Reload_NoOp_WhenAlreadyFull()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        w.Reload().Should().BeFalse();
    }

    [Fact]
    public void Shotgun_FiresMultiplePellets()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Shotgun);
        var shots = w.Fire(default, Vector2.Right);
        shots.Length.Should().Be(8);
        w.Ammo.Should().Be(7);
    }

    [Fact]
    public void Unequip_ReturnsToNoWeapon()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        w.Unequip();
        w.HasWeapon.Should().BeFalse();
        w.CurrentWeapon.Should().Be(WeaponId.None);
        w.Ammo.Should().Be(0);
    }

    [Fact]
    public void AddAmmo_CappedAtCapacity()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol);
        for (int i = 0; i < 14; i++)
        {
            w.Fire(default, Vector2.Right); // 15 -> 1
            w.Update(0.4f); // expire pistol cooldown (~0.33s)
        }
        w.AddAmmo(100);
        w.Ammo.Should().Be(15);
    }

    [Fact]
    public void AddAmmo_NoWeapon_NoOp()
    {
        var w = new WeaponSystem();
        w.AddAmmo(10);
        w.Ammo.Should().Be(0);
    }

    [Fact]
    public void AmmoCapacityBonus_IncreasesCapacity()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol, ammoCapacityBonus: 5);
        w.AmmoCapacity.Should().Be(20);
        w.Ammo.Should().Be(20);
    }

    [Fact]
    public void ReloadSpeedMult_ShortensReload()
    {
        var w = new WeaponSystem();
        w.Equip(WeaponId.Pistol, reloadSpeedMult: 0.5f);
        w.Reload();
        w.Update(0.6f); // half of 1.2s
        w.IsReloading.Should().BeFalse();
        w.Ammo.Should().Be(15);
    }
}
