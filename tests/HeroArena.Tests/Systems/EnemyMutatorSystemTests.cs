using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Systems;

/// <summary>
/// Covers the pure mutator formulas (<see cref="MutatorFormulas"/>) headlessly,
/// replacing the retired <see cref="EnemyMutatorSystemTests"/> that used the
/// deprecated <see cref="System.Runtime.Serialization.FormatterServices.GetUninitializedObject"/>
/// bypass on Godot <see cref="EnemyBase"/> nodes.
///
/// The idempotency invariant (audit P1-1): mutator scale is a total multiplier
/// applied to spawn-time base stats, never compounded on current values, so
/// re-application at the same threat tier yields identical stats.
/// </summary>
public class EnemyMutatorSystemTests
{
    [Theory]
    [InlineData(0, 1f, 1f)]    // no threat → no multiplier
    [InlineData(1, 1f, 1.1f)]  // threat 1 → +10%
    [InlineData(2, 1f, 1.2f)]  // threat 2 → +20%
    [InlineData(5, 1f, 1.5f)]  // threat 5 → +50%
    [InlineData(10, 1f, 2.0f)] // threat 10 → +100%
    [InlineData(5, 0.5f, 1.25f)] // mutatorScale scales the tier
    public void ComputeScale_TotalMultiplier_IsDeterministic_AndIdempotent(int threatLevel, float mutatorScale, float expected)
    {
        MutatorFormulas.ComputeScale(threatLevel, mutatorScale).Should().Be(expected);

        // Idempotency: calling again with the same inputs returns the same
        // scale — no compounding on previous results (this is a pure function).
        MutatorFormulas.ComputeScale(threatLevel, mutatorScale).Should().Be(expected);
    }

    [Fact]
    public void ComputeScale_ZeroThreat_IsIdentity_NoMutatorsApplied()
    {
        MutatorFormulas.ComputeScale(0).Should().Be(1f);
        MutatorFormulas.ComputeMutatorCount(0, MutatorTypeExtensions.Count(), 1f).Should().Be(0);
    }

    [Theory]
    [InlineData(0, 5, 1f, 0)]
    [InlineData(1, 5, 1f, 1)]
    [InlineData(2, 5, 1f, 2)]
    [InlineData(5, 5, 1f, 5)]
    [InlineData(10, 5, 1f, 5)] // clamped to pool size
    [InlineData(3, 5, 0.5f, 2)] // Ceil(3 * 0.5) = 2
    [InlineData(1, 5, 0.0f, 0)] // zero scale → zero mutators
    public void ComputeMutatorCount_Clamped_ToPoolSize(int threatLevel, int maxMutators, float mutatorScale, int expected)
    {
        MutatorFormulas.ComputeMutatorCount(threatLevel, maxMutators, mutatorScale).Should().Be(expected);
    }

    /// <summary>
    /// Regression for audit P1-1: the scale must be monotonic in threat level
    /// and never compound. A tier-2 application must equal 1.2× base, not
    /// 1.1× then 1.2× (1.32×) of an already-mutated value.
    /// </summary>
    [Fact]
    public void Scale_DoesNotCompound_AcrossThreatTiers()
    {
        float tier1 = MutatorFormulas.ComputeScale(1);
        float tier2 = MutatorFormulas.ComputeScale(2);

        tier1.Should().Be(1.1f);
        tier2.Should().Be(1.2f);
        // Compounding would apply tier2 on top of tier1: 1.1f * 1.2f = 1.32f.
        // Idempotent application yields exactly tier2 = 1.2f (base × tier).
        tier2.Should().NotBe(tier1 * tier2);
    }
}

/// <summary>Extension to expose the mutator pool size for test assertions.</summary>
public static class MutatorTypeExtensions
{
    public static int Count() => Enum.GetValues(typeof(MutatorType)).Length;
}
