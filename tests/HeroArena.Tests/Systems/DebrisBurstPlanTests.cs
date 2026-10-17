using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// DebrisBurstPlan / DebrisShardState: the Godot-free core of DestructionDrama.
// Covers deterministic seeding (same spot shatters the same way every time),
// plan bounds, position-seed stability, and the battlefield-ballistics
// integration (gravity, drag, landing, resting, delayed start, alpha fade).
public class DebrisBurstPlanTests
{
    [Fact]
    public void Generate_SameSeed_ProducesIdenticalPlan()
    {
        var a = new DebrisBurstPlan.Shard[DebrisBurstPlan.MaxShards];
        var b = new DebrisBurstPlan.Shard[DebrisBurstPlan.MaxShards];

        int countA = DebrisBurstPlan.Generate(12345, 8, 1f, a);
        int countB = DebrisBurstPlan.Generate(12345, 8, 1f, b);

        countA.Should().Be(8);
        countB.Should().Be(8);
        for (int i = 0; i < countA; i++)
        {
            a[i].Angle.Should().Be(b[i].Angle);
            a[i].Speed.Should().Be(b[i].Speed);
            a[i].Spin.Should().Be(b[i].Spin);
            a[i].Size.Should().Be(b[i].Size);
            a[i].Life.Should().Be(b[i].Life);
            a[i].Delay.Should().Be(b[i].Delay);
            a[i].GroundDrop.Should().Be(b[i].GroundDrop);
        }
    }

    [Fact]
    public void Generate_DifferentSeeds_ProduceDifferentPlans()
    {
        var a = new DebrisBurstPlan.Shard[DebrisBurstPlan.MaxShards];
        var b = new DebrisBurstPlan.Shard[DebrisBurstPlan.MaxShards];

        DebrisBurstPlan.Generate(1, 8, 1f, a);
        DebrisBurstPlan.Generate(2, 8, 1f, b);

        bool anyDifference = false;
        for (int i = 0; i < 8 && !anyDifference; i++)
            anyDifference = a[i].Angle != b[i].Angle || a[i].Speed != b[i].Speed;
        anyDifference.Should().BeTrue();
    }

    [Fact]
    public void Generate_ClampsCountToBufferAndCap()
    {
        var small = new DebrisBurstPlan.Shard[3];
        DebrisBurstPlan.Generate(1, 10, 1f, small).Should().Be(3);

        var big = new DebrisBurstPlan.Shard[64];
        DebrisBurstPlan.Generate(1, 64, 1f, big).Should().Be(DebrisBurstPlan.MaxShards);
    }

    [Fact]
    public void Generate_ValuesStayWithinDesignedBounds()
    {
        var shards = new DebrisBurstPlan.Shard[DebrisBurstPlan.MaxShards];
        int count = DebrisBurstPlan.Generate(777, DebrisBurstPlan.MaxShards, 1f, shards);

        for (int i = 0; i < count; i++)
        {
            shards[i].Angle.Should().BeInRange(0f, 2f * System.MathF.PI);
            shards[i].Speed.Should().BeInRange(90f, 350f);
            shards[i].Spin.Should().BeInRange(-9f, 9f);
            shards[i].Size.Should().BeInRange(0.55f, 1.4f);
            shards[i].Life.Should().BeInRange(0.6f, 1.3f);
            shards[i].Delay.Should().BeInRange(0f, 0.05f);
            shards[i].GroundDrop.Should().BeInRange(10f, 36f);
        }
    }

    [Fact]
    public void Generate_PowerScalesSpeed()
    {
        var weak = new DebrisBurstPlan.Shard[1];
        var strong = new DebrisBurstPlan.Shard[1];

        DebrisBurstPlan.Generate(42, 1, 0.5f, weak);
        DebrisBurstPlan.Generate(42, 1, 2f, strong);

        strong[0].Speed.Should().BeGreaterThan(weak[0].Speed);
    }

    [Fact]
    public void SeedFromPosition_IsStable_AndVariesWithPosition()
    {
        DebrisBurstPlan.SeedFromPosition(123.5f, -44.25f)
            .Should().Be(DebrisBurstPlan.SeedFromPosition(123.5f, -44.25f));

        DebrisBurstPlan.SeedFromPosition(123.5f, -44.25f)
            .Should().NotBe(DebrisBurstPlan.SeedFromPosition(123.5f, -44.05f));
    }

    [Theory]
    [InlineData(0f, 4)]
    [InlineData(24f, 4)]
    [InlineData(120f, 10)]
    [InlineData(500f, 12)]
    public void CountForRadius_ClampsToPoolRange(float radius, int expected)
    {
        DebrisBurstPlan.CountForRadius(radius).Should().Be(expected);
    }

    [Fact]
    public void FromPlan_UpwardBiasesVelocity_AndHonorsDelay()
    {
        var shard = new DebrisBurstPlan.Shard
        {
            Angle = 0f,
            Speed = 200f,
            Spin = 3f,
            Size = 1f,
            Life = 1f,
            Delay = 0.04f,
            GroundDrop = 20f,
        };

        var state = DebrisShardState.FromPlan(shard, 100f, 50f);

        state.X.Should().Be(100f);
        state.Y.Should().Be(50f);
        state.VX.Should().BeApproximately(200f, 0.01f);
        state.VY.Should().BeApproximately(-50f, 0.01f); // 25% of speed pulled upward
        state.Age.Should().BeApproximately(-0.04f, 0.0001f);
        state.GroundY.Should().Be(70f);
        state.Alpha.Should().Be(0f); // not yet visible while delayed

        // While delayed the shard must not move.
        state.Step(0.02f, 900f, 0.5f);
        state.X.Should().Be(100f);
        state.Y.Should().Be(50f);
    }

    [Fact]
    public void Step_GravityPullsDown_OverTime()
    {
        var shard = new DebrisBurstPlan.Shard
        {
            Angle = 0f,
            Speed = 200f,
            Spin = 0f,
            Size = 1f,
            Life = 5f,
            Delay = 0f,
            GroundDrop = 10000f, // effectively no landing during the test
        };
        var state = DebrisShardState.FromPlan(shard, 0f, 0f);

        state.Step(0.5f, 900f, 0f);
        float vyAfterFirst = state.VY;

        state.Step(0.5f, 900f, 0f);
        state.VY.Should().BeGreaterThan(vyAfterFirst);
    }

    [Fact]
    public void Step_ShardLandsBouncesAndComesToRest()
    {
        var shard = new DebrisBurstPlan.Shard
        {
            Angle = 1.2f, // mostly downward-ish outward
            Speed = 260f,
            Spin = 4f,
            Size = 1f,
            Life = 3f,
            Delay = 0f,
            GroundDrop = 12f,
        };
        var state = DebrisShardState.FromPlan(shard, 0f, 0f);

        // Step generously but well within the shard's life.
        for (int i = 0; i < 120 && !state.Resting; i++)
            state.Step(1f / 60f, 900f, 0.6f);

        state.Resting.Should().BeTrue();
        state.Y.Should().BeApproximately(state.GroundY, 0.01f);

        // Once resting, the shard stays put.
        float xBefore = state.X;
        state.Step(1f / 60f, 900f, 0.6f);
        state.X.Should().Be(xBefore);
    }

    [Fact]
    public void Alpha_FullUntilHalfLife_ThenFadesToZero()
    {
        var shard = new DebrisBurstPlan.Shard
        {
            Angle = 0f, Speed = 100f, Spin = 0f, Size = 1f,
            Life = 1f, Delay = 0f, GroundDrop = 5f,
        };
        var state = DebrisShardState.FromPlan(shard, 0f, 0f);

        state.Alpha.Should().Be(1f);

        state.Step(0.5f, 0f, 0f);
        state.Alpha.Should().Be(1f);

        state.Step(0.25f, 0f, 0f);
        state.Alpha.Should().BeApproximately(0.5f, 0.01f);

        state.Step(0.25f, 0f, 0f);
        state.Alpha.Should().Be(0f);
        state.Finished.Should().BeTrue();
    }

    [Fact]
    public void Step_AfterLifeCompletes_ShardIsFinishedAndFrozen()
    {
        var shard = new DebrisBurstPlan.Shard
        {
            Angle = 0f, Speed = 0f, Spin = 0f, Size = 1f,
            Life = 0.2f, Delay = 0f, GroundDrop = 5f,
        };
        var state = DebrisShardState.FromPlan(shard, 0f, 0f);

        state.Step(0.5f, 900f, 0f);

        state.Finished.Should().BeTrue();
        state.Y.Should().Be(0f); // no drift after completion
    }
}
