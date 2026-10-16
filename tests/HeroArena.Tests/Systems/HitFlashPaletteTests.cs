using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// HitFlash (the F-31 OnProjectileHit consumer) damage-type color mapping and
// fade curve. Extracted to HitFlashPalette so all 6 DamageType branches are
// asserted headlessly instead of in-engine source-text greps.
public class HitFlashPaletteTests
{
    public static TheoryData<DamageType, float, float, float> KnownColors => new()
    {
        { DamageType.Kinetic,    1f,  0.2f, 0.2f },
        { DamageType.Energy,     0.3f, 0.8f, 1f },
        { DamageType.Lightning,  1f,  1f,   0.3f },
        { DamageType.Acid,       0.4f, 1f,   0.4f },
        { DamageType.Fire,       1f,  0.5f,  0.1f },
        { DamageType.Explosive,  1f,  1f,   1f },
    };

    [Theory]
    [MemberData(nameof(KnownColors))]
    public void ColorFor_MapsEveryDamageType(DamageType type, float r, float g, float b)
    {
        var color = HitFlashPalette.ColorFor(type);

        color.R.Should().BeApproximately(r, 0.001f);
        color.G.Should().BeApproximately(g, 0.001f);
        color.B.Should().BeApproximately(b, 0.001f);
    }

    [Fact]
    public void ColorFor_UnmappedValue_FallsBackToRed()
    {
        var color = HitFlashPalette.ColorFor((DamageType)99);

        color.R.Should().BeApproximately(1f, 0.001f);
        color.G.Should().BeApproximately(0f, 0.001f);
        color.B.Should().BeApproximately(0f, 0.001f);
    }

    [Fact]
    public void ColorFor_KineticDiffersFromFallbackRed()
    {
        // The fallback must be distinguishable from a real damage type so a
        // silent unmapped value is not mistaken for kinetic.
        HitFlashPalette.ColorFor(DamageType.Kinetic)
            .Should().NotBe(HitFlashPalette.ColorFor((DamageType)99));
    }

    [Theory]
    [InlineData(0.08f, 0.25f)]  // start of flash: full alpha
    [InlineData(0.04f, 0.125f)] // mid flash: half alpha
    [InlineData(0.008f, 0.025f)]// nearly elapsed: ~10%
    public void AlphaFor_ScalesLinearlyWithRemainingTime(float remaining, float expected)
    {
        HitFlashPalette.AlphaFor(remaining).Should().BeApproximately(expected, 0.001f);
    }

    [Fact]
    public void AlphaFor_ZeroRemaining_IsZero()
    {
        HitFlashPalette.AlphaFor(0f).Should().Be(0f);
    }

    [Fact]
    public void AlphaFor_NegativeRemaining_IsZero()
    {
        HitFlashPalette.AlphaFor(-1f).Should().Be(0f);
    }

    [Fact]
    public void FlashDuration_IsPositive()
    {
        HitFlashPalette.Duration.Should().BeGreaterThan(0f);
    }
}
