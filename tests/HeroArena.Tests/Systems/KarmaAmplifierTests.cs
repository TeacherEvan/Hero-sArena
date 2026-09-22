using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Systems;

// Pure-formula tests for the karma amplifier. Previously this class used the
// RuntimeHelpers.GetUninitializedObject bypass to instantiate the
// CollateralKarma Node headlessly (see git history); the math now lives in
// ProgressionFormulas, so no bypass, reflection, or GodotRuntime trait is
// needed. Node-level behavior (event subscription, DestructionCount) is
// covered in-engine by CoreSystemTests.TestCollateralKarmaBehavior.
public class KarmaAmplifierTests
{
    [Theory]
    [InlineData(0, 1.0f)]
    [InlineData(10, 1.168f)]
    [InlineData(50, 1.652f)]
    public void KarmaAmplifier_ScalesLogarithmicallyWithDestructionCount(int count, float expected)
    {
        ProgressionFormulas.CalcKarmaAmplifier(count)
            .Should().BeApproximately(expected, 0.005f);
    }

    [Fact]
    public void KarmaAmplifier_ZeroDestruction_EqualsOne()
    {
        ProgressionFormulas.CalcKarmaAmplifier(0).Should().BeApproximately(1f, 0.001f);
    }
}
