using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class WeaponCatalogTests
{
    [Fact]
    public void Get_ReturnsSentinelForNone()
    {
        var def = WeaponCatalog.Get(WeaponId.None);
        def.Id.Should().Be(WeaponId.None);
        def.Damage.Should().Be(0f);
        def.Name.Should().Be("None");
    }

    [Fact]
    public void Get_ReturnsSentinelForUnknownId()
    {
        var def = WeaponCatalog.Get((WeaponId)999);
        def.Id.Should().Be(WeaponId.None);
    }

    [Fact]
    public void All_HasNineDefinedWeapons()
    {
        WeaponCatalog.All.Length.Should().Be(9);
        foreach (var def in WeaponCatalog.All)
            def.Id.Should().NotBe(WeaponId.None);
    }

    [Fact]
    public void Pistol_HasExpectedStats()
    {
        var p = WeaponCatalog.Get(WeaponId.Pistol);
        p.Name.Should().Be("Pistol");
        p.Damage.Should().Be(22f);
        p.AmmoCapacity.Should().Be(15);
        p.ReloadTime.Should().Be(1.2f);
        p.ShotInterval.Should().BeApproximately(1f / 3f, 0.0001f);
    }

    [Fact]
    public void Shotgun_HasMultiplePelletsAndSpread()
    {
        var s = WeaponCatalog.Get(WeaponId.Shotgun);
        s.ProjectileCount.Should().Be(8);
        s.SpreadDegrees.Should().Be(22f);
    }

    [Fact]
    public void GrenadeLauncher_IsExplosive()
    {
        var g = WeaponCatalog.Get(WeaponId.GrenadeLauncher);
        g.Explosive.Should().BeTrue();
        g.AoERadius.Should().Be(140f);
    }
}
