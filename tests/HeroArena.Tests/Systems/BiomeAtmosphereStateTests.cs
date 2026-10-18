using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// BiomeAtmosphereState: the Godot-free identity table for the five arenas.
// Covers path resolution, profile sanity ranges, and the weather-intensity
// curve. Profile VALUES are pinned only where the game's readability depends
// on them (distinct signature fields, sane ranges) — not to exact digits.
public class BiomeAtmosphereStateTests
{
    private static readonly BiomeKind[] AllBiomes =
    {
        BiomeKind.RuinedMetropolis,
        BiomeKind.VolcanoCrater,
        BiomeKind.OrbitalPlatform,
        BiomeKind.SubterraneanLab,
        BiomeKind.AlienWasteland,
    };

    [Theory]
    [InlineData("res://scenes/maps/RuinedMetropolis.tscn", BiomeKind.RuinedMetropolis)]
    [InlineData("res://scenes/maps/VolcanoCrater.tscn", BiomeKind.VolcanoCrater)]
    [InlineData("res://scenes/maps/OrbitalPlatform.tscn", BiomeKind.OrbitalPlatform)]
    [InlineData("res://scenes/maps/SubterraneanLab.tscn", BiomeKind.SubterraneanLab)]
    [InlineData("res://scenes/maps/AlienWasteland.tscn", BiomeKind.AlienWasteland)]
    [InlineData("VOLCANOCrater.TSCN", BiomeKind.VolcanoCrater)]
    [InlineData("volcanocrater", BiomeKind.VolcanoCrater)]
    public void BiomeForMapPath_ResolvesKnownMaps(string path, BiomeKind expected)
    {
        BiomeAtmosphereState.BiomeForMapPath(path).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("res://scenes/maps/UnknownFutureMap.tscn")]
    public void BiomeForMapPath_FallsBackToDefault(string? path)
    {
        BiomeAtmosphereState.BiomeForMapPath(path).Should().Be(BiomeKind.RuinedMetropolis);
    }

    [Fact]
    public void ProfileFor_ReturnsProfileWithMatchingKind_ForEveryBiome()
    {
        foreach (var kind in AllBiomes)
        {
            var profile = BiomeAtmosphereState.ProfileFor(kind);
            profile.Kind.Should().Be(kind);
            profile.DisplayName.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void EveryBiome_HasItsOwnSignatureFieldKind()
    {
        var kinds = AllBiomes
            .Select(b => BiomeAtmosphereState.ProfileFor(b).PrimaryField.Kind)
            .ToHashSet();

        // Five arenas read as five places only if no two share a signature.
        kinds.Should().HaveCount(AllBiomes.Length);
    }

    [Fact]
    public void EveryBiome_FieldValues_AreSane()
    {
        foreach (var kind in AllBiomes)
        {
            var profile = BiomeAtmosphereState.ProfileFor(kind);
            foreach (var field in new[] { profile.PrimaryField, profile.SecondaryField })
            {
                field.Amount.Should().BeInRange(0, 400);
                field.R.Should().BeInRange(0f, 1f);
                field.G.Should().BeInRange(0f, 1f);
                field.B.Should().BeInRange(0f, 1f);
                field.A.Should().BeInRange(0f, 1f);
                field.SpeedMin.Should().BeGreaterThanOrEqualTo(0f);
                field.SpeedMax.Should().BeGreaterThanOrEqualTo(field.SpeedMin);
                field.SizeScale.Should().BeGreaterThan(0f);
                field.Lifetime.Should().BeGreaterThan(0f);
                field.WindInfluence01.Should().BeInRange(0f, 2f);
            }
        }
    }

    [Fact]
    public void EveryBiome_CritterRosters_AreSane()
    {
        foreach (var kind in AllBiomes)
        {
            var profile = BiomeAtmosphereState.ProfileFor(kind);
            foreach (var critter in new[] { profile.PrimaryCritter, profile.SecondaryCritter })
            {
                critter.Name.Should().NotBeNullOrWhiteSpace();
                critter.Speed.Should().BeGreaterThan(0f);
                critter.PanicSpeed.Should().BeGreaterThanOrEqualTo(critter.Speed);
                critter.Size.Should().BeInRange(0.05f, 2f);
                critter.Glow01.Should().BeInRange(0f, 1f);
                critter.Height01.Should().BeInRange(0f, 1f);
                critter.FleeSensitivity01.Should().BeInRange(0f, 1f);
            }
        }
    }

    [Fact]
    public void EveryBiome_TintWindAndPopulation_AreSane()
    {
        foreach (var kind in AllBiomes)
        {
            var profile = BiomeAtmosphereState.ProfileFor(kind);
            profile.TintR.Should().BeInRange(0f, 1f);
            profile.TintG.Should().BeInRange(0f, 1f);
            profile.TintB.Should().BeInRange(0f, 1f);
            profile.WindScale.Should().BeInRange(0.4f, 2f);
            profile.WildlifePopulation.Should().BeInRange(1, WildlifeConfig.MaxCritters);
        }
    }

    [Fact]
    public void EveryBiome_HasAtLeastOneBoldSpecies_SoCuriosityReads()
    {
        foreach (var kind in AllBiomes)
        {
            var profile = BiomeAtmosphereState.ProfileFor(kind);
            bool bold = profile.PrimaryCritter.FleeSensitivity01 <= WildlifeConfig.CuriosityBoldnessThreshold
                || profile.SecondaryCritter.FleeSensitivity01 <= WildlifeConfig.CuriosityBoldnessThreshold;
            bold.Should().BeTrue($"{kind} needs one curious species for calm-time encounters");
        }
    }

    [Fact]
    public void FieldIntensity_GrowsWithWaveAndThreat_ThenClamps()
    {
        float calm = BiomeAtmosphereState.FieldIntensity(0, 0);
        float mid = BiomeAtmosphereState.FieldIntensity(5, 3);
        float max = BiomeAtmosphereState.FieldIntensity(10, 5);
        float beyond = BiomeAtmosphereState.FieldIntensity(100, 100);

        calm.Should().BeGreaterThan(0.5f);
        mid.Should().BeGreaterThan(calm);
        max.Should().BeGreaterThan(mid);
        beyond.Should().Be(BiomeAtmosphereState.MaxFieldIntensity);
        beyond.Should().Be(max);
    }

    [Fact]
    public void FieldIntensity_NeverDropsBelowItsBase()
    {
        BiomeAtmosphereState.FieldIntensity(-5, -5).Should().BeGreaterThanOrEqualTo(0.8f);
    }

    [Fact]
    public void BossStorm_MultiplierPinsItsDocumentedEnvelope()
    {
        // The node multiplies the capped field intensity by this during a boss
        // storm; pin both values so the mood curve stays traceable.
        BiomeAtmosphereState.BossStormMultiplier.Should().BeApproximately(1.35f, 0.0001f);
        (BiomeAtmosphereState.MaxFieldIntensity * BiomeAtmosphereState.BossStormMultiplier)
            .Should().BeApproximately(2.025f, 0.0001f);
    }
}
