using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using HeroArena;

namespace HeroArena.Tests.Systems;

// WildlifeDirectorState: the Godot-free critter simulation behind
// AmbientWildlife. Covers population scheduling, spawn placement rules,
// wander bounds, panic/scatter, hide/EndHide moods, curiosity approach,
// wind coupling, determinism and simulation stability.
public class WildlifeDirectorStateTests
{
    private static WildlifeDirectorState.SpeciesProfile BoldSpecies() => new()
    {
        Name = "BoldMoth", Speed = 30f, PanicSpeed = 140f, Size = 0.5f,
        Glow01 = 0.8f, Height01 = 0.5f, FleeSensitivity01 = 0.3f,
    };

    private static WildlifeDirectorState.SpeciesProfile SkittishSpecies() => new()
    {
        Name = "SkittishCrawler", Speed = 30f, PanicSpeed = 90f, Size = 0.4f,
        Glow01 = 0.1f, Height01 = 0.1f, FleeSensitivity01 = 0.9f,
    };

    private static WildlifeDirectorState Make(
        WildlifeDirectorState.SpeciesProfile[] profiles, int population,
        int seed = 7, float halfW = 1000f, float halfH = 1000f)
    {
        var d = new WildlifeDirectorState();
        d.Configure(profiles, population, seed);
        d.SetArena(0f, 0f, halfW, halfH);
        return d;
    }

    private static void Settle(WildlifeDirectorState d, float seconds, float step = 0.05f)
    {
        for (float t = 0f; t < seconds; t += step)
            d.Update(step, 0f, 0f);
    }

    private static List<(int Index, WildlifeDirectorState.Critter Critter)> Active(WildlifeDirectorState d)
    {
        var list = new List<(int, WildlifeDirectorState.Critter)>();
        for (int i = 0; i < WildlifeDirectorState.MaxCritters; i++)
        {
            var c = d.GetCritter(i);
            if (c.Active) list.Add((i, c));
        }
        return list;
    }

    [Fact]
    public void Configure_ResetsPool_AndZeroPopulationSpawnsNothing()
    {
        var d = Make(new[] { BoldSpecies() }, 4);
        Settle(d, 2f);
        d.ActiveCount.Should().BeGreaterThan(0);

        d.Configure(new[] { BoldSpecies() }, 0, 7);
        Settle(d, 5f);

        d.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Update_SpawnsTowardTargetPopulation()
    {
        var d = Make(new[] { BoldSpecies() }, 3);

        Settle(d, 2f);

        d.ActiveCount.Should().Be(3);
        d.LivingCount.Should().Be(3);
    }

    [Fact]
    public void Update_RespectsSpawnInterval_OneSpawnPerInterval()
    {
        var d = Make(new[] { BoldSpecies(), SkittishSpecies() }, 5);

        d.Update(0.01f, 0f, 0f);
        d.ActiveCount.Should().Be(1);

        d.Update(0.01f, 0f, 0f); // inside the 0.35 s interval
        d.ActiveCount.Should().Be(1);
    }

    [Fact]
    public void Spawns_StayInsideArenaBounds()
    {
        var d = Make(new[] { BoldSpecies() }, 6);
        Settle(d, 4f);

        foreach (var (_, c) in Active(d))
        {
            c.X.Should().BeInRange(-880f - 0.1f, 880f + 0.1f);
            c.Y.Should().BeInRange(-880f - 0.1f, 880f + 0.1f);
        }
    }

    [Fact]
    public void Spawns_KeepClearanceFromHero_AcrossTheWave()
    {
        // The full pool, not one lucky spawn: with ~5% of uniform spawns
        // landing inside the ring, 64 attempts make a missing check virtually
        // certain to be caught (and a passing run samples the rule at scale).
        var d = Make(new[] { BoldSpecies() }, WildlifeDirectorState.MaxCritters);
        d.SetHero(0f, 0f);

        Settle(d, 24f); // 64 spawns × 0.35 s interval, plus slack

        var active = Active(d);
        active.Count.Should().Be(WildlifeDirectorState.MaxCritters);
        foreach (var (_, c) in active)
        {
            // Clearance is a spawn rule, so measure spawn origins: critters may
            // wander closer afterwards, but a home never moves.
            float dist = MathF.Sqrt(c.HomeX * c.HomeX + c.HomeY * c.HomeY);
            dist.Should().BeGreaterThanOrEqualTo(WildlifeDirectorState.HeroSpawnClearance - 0.5f);
        }
    }

    [Fact]
    public void IdleCritters_MoveUnderTheirSpeedCap()
    {
        var d = Make(new[] { BoldSpecies() }, 4);
        Settle(d, 3f);

        var spec = BoldSpecies();
        float cap = spec.Speed * 1.4f + 1f;
        foreach (var (_, c) in Active(d))
        {
            float speed = MathF.Sqrt(c.VX * c.VX + c.VY * c.VY);
            speed.Should().BeLessThanOrEqualTo(cap);
        }
    }

    [Fact]
    public void Panic_ThrowsCrittersAwayFromEpicentre()
    {
        var d = Make(new[] { BoldSpecies() }, 1);
        d.Update(0.01f, 0f, 0f);
        var before = Active(d)[0].Critter;

        // Epicentre just to the left of the critter: it should flee +X.
        d.Panic(before.X - 30f, before.Y, 400f, 1f);

        var after = Active(d)[0].Critter;
        after.PanicTime.Should().BeGreaterThan(0f);
        after.VX.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Panic_DurationScalesWithStrength()
    {
        // PanicTime is a max(), so strength comparison needs fresh directors.
        var profiles = new[] { BoldSpecies() };
        var strongD = Make(profiles, 1, seed: 5);
        var weakD = Make(profiles, 1, seed: 5);
        strongD.Update(0.01f, 0f, 0f);
        weakD.Update(0.01f, 0f, 0f);
        var c = Active(strongD)[0].Critter;

        strongD.Panic(c.X + 10f, c.Y, 400f, 1f);
        weakD.Panic(c.X + 10f, c.Y, 400f, 0f);

        Active(strongD)[0].Critter.PanicTime.Should().BeApproximately(4.5f, 0.01f);
        Active(weakD)[0].Critter.PanicTime.Should().BeApproximately(1.2f, 0.01f);
    }

    [Fact]
    public void Panic_LeavesDistantCrittersAlone()
    {
        var d = Make(new[] { BoldSpecies() }, 2);
        Settle(d, 1.5f);

        d.Panic(99990f, 99990f, 100f, 1f);

        foreach (var (_, c) in Active(d))
            c.PanicTime.Should().Be(0f);
    }

    [Fact]
    public void HideAll_FreezesCritters_AndFadesThemOut()
    {
        var d = Make(new[] { BoldSpecies() }, 2);
        Settle(d, 1.2f);
        var before = Active(d)[0].Critter;

        d.HideAll(5f);
        Settle(d, 1f);

        var after = Active(d)[0].Critter;
        after.X.Should().BeApproximately(before.X, 0.01f);
        after.Y.Should().BeApproximately(before.Y, 0.01f);
        after.Fade.Should().BeLessThan(0.5f);
        after.HideTime.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void HideAll_SkipsPanic_ThenEndHideResumes()
    {
        var d = Make(new[] { BoldSpecies() }, 1);
        d.Update(0.01f, 0f, 0f);
        var c = Active(d)[0].Critter;

        d.HideAll(5f);
        d.Panic(c.X, c.Y, 500f, 1f); // hidden critters don't respond
        Active(d)[0].Critter.PanicTime.Should().Be(0f);

        d.EndHide();
        Settle(d, 2.5f);

        var revived = Active(d)[0].Critter;
        revived.HideTime.Should().Be(0f);
        revived.Fade.Should().BeGreaterThan(0.9f);
    }

    [Fact]
    public void SetTargetPopulation_Lowers_ByFadingTheExtraCritters()
    {
        var d = Make(new[] { BoldSpecies() }, 4);
        Settle(d, 3f);
        d.ActiveCount.Should().Be(4);

        d.SetTargetPopulation(1);
        Settle(d, 1.6f); // longer than FadeOutSeconds

        d.ActiveCount.Should().Be(1);
        d.LivingCount.Should().Be(1);
    }

    [Fact]
    public void Curiosity_BoldCritterApproachesHero_ComparedToAWanderControl()
    {
        // Huge arena so walls never interfere; hero placed straight +Y of the
        // spawned critter, far beyond the stand-off ring. The same-seed control
        // run fixes the wander luck, so the test measures curiosity itself.
        var profiles = new[] { BoldSpecies() };
        var curious = Make(profiles, 1, halfW: 100000f, halfH: 100000f);
        var control = Make(profiles, 1, halfW: 100000f, halfH: 100000f);
        curious.Update(0.01f, 0f, 0f);
        control.Update(0.01f, 0f, 0f);

        var start = Active(curious)[0].Critter;
        var controlStart = Active(control)[0].Critter;
        start.X.Should().Be(controlStart.X);
        start.Y.Should().Be(controlStart.Y);

        curious.SetHero(start.X, start.Y + 2000f);
        control.SetHero(controlStart.X, controlStart.Y + 2000f);
        curious.SetCuriosity(1f); // beeline
        control.SetCuriosity(0f); // pure wander

        Settle(curious, 8f);
        Settle(control, 8f);

        float curiousGain = Active(curious)[0].Critter.Y - start.Y;
        float controlGain = Active(control)[0].Critter.Y - controlStart.Y;

        // A 30 px/s beeline closes ~230 px in 8 s; a wanderer oscillates around
        // its home. The margin is far outside wander luck — and an inversion of
        // the boldness gate collapses it to zero.
        (curiousGain - controlGain).Should().BeGreaterThan(80f);
    }

    [Fact]
    public void Curiosity_SkittishCritter_IgnoresTheInvitation()
    {
        var profiles = new[] { SkittishSpecies() };
        var curious = Make(profiles, 1, halfW: 100000f, halfH: 100000f);
        var control = Make(profiles, 1, halfW: 100000f, halfH: 100000f);
        curious.Update(0.01f, 0f, 0f);
        control.Update(0.01f, 0f, 0f);

        var start = Active(curious)[0].Critter;
        var controlStart = Active(control)[0].Critter;
        curious.SetHero(start.X, start.Y + 2000f);
        control.SetHero(controlStart.X, controlStart.Y + 2000f);
        curious.SetCuriosity(1f);
        control.SetCuriosity(0f);

        Settle(curious, 8f);
        Settle(control, 8f);

        float curiousGain = Active(curious)[0].Critter.Y - start.Y;
        float controlGain = Active(control)[0].Critter.Y - controlStart.Y;

        // Fear outweighs interest: no clear lead over the wander control (a
        // beeline would have opened hundreds of px by now; an inversion of the
        // boldness gate makes every skittish species curious and fails this).
        MathF.Abs(curiousGain - controlGain).Should().BeLessThan(60f);
    }

    [Fact]
    public void Wind_DriftsCrittersInItsDirection()
    {
        var profiles = new[] { BoldSpecies() };
        var calm = Make(profiles, 1, seed: 11);
        var windy = Make(profiles, 1, seed: 11);

        // Identical states up to this point: one coincident step with
        // different wind isolates the wind's contribution to steering.
        calm.Update(0.01f, 0f, 0f);
        windy.Update(0.01f, 0f, 0f);
        calm.Update(0.05f, 0f, 0f);
        windy.Update(0.05f, 90f, 0f);

        float calmVx = Active(calm)[0].Critter.VX;
        float windyVx = Active(windy)[0].Critter.VX;

        (windyVx - calmVx).Should().BeGreaterThan(2f);
    }

    [Fact]
    public void SameSeed_ProducesSameSpawnPositions()
    {
        var profiles = new[] { BoldSpecies(), SkittishSpecies() };
        var a = Make(profiles, 3, seed: 42);
        var b = Make(profiles, 3, seed: 42);

        for (float t = 0f; t < 1.5f; t += 0.05f)
        {
            a.Update(0.05f, 0f, 0f);
            b.Update(0.05f, 0f, 0f);
        }

        for (int i = 0; i < WildlifeDirectorState.MaxCritters; i++)
        {
            var ca = a.GetCritter(i);
            var cb = b.GetCritter(i);
            if (!ca.Active) continue;
            ca.X.Should().Be(cb.X);
            ca.Y.Should().Be(cb.Y);
            ca.Species.Should().Be(cb.Species);
        }
    }

    [Fact]
    public void TinyArena_Separation_KeepsSimulationStable()
    {
        var d = Make(new[] { BoldSpecies(), SkittishSpecies() }, 6, halfW: 80f, halfH: 80f);

        Settle(d, 5f);

        foreach (var (_, c) in Active(d))
        {
            float.IsNaN(c.X).Should().BeFalse();
            float.IsNaN(c.Y).Should().BeFalse();
            MathF.Abs(c.X).Should().BeLessThanOrEqualTo(100f);
            MathF.Abs(c.Y).Should().BeLessThanOrEqualTo(100f);
        }
    }

    [Fact]
    public void FireflyFlash_IsBounded_Deterministic_AndMostlyDark()
    {
        // Peaks smoothly inside the flash window…
        WildlifeDirectorState.FireflyFlash(0.132f, 0f).Should().BeGreaterThan(0.95f);
        // …and matches itself for identical inputs.
        WildlifeDirectorState.FireflyFlash(3.3f, 0.21f)
            .Should().Be(WildlifeDirectorState.FireflyFlash(3.3f, 0.21f));

        int lit = 0;
        int total = 0;
        for (float t = 0f; t < 200f; t += 0.05f)
        {
            float v = WildlifeDirectorState.FireflyFlash(t, 0.37f);
            v.Should().BeInRange(WildlifeDirectorState.FlashDimBase, 1f);
            if (v > 0.5f) lit++;
            total++;
        }

        // A firefly, not a lamp: lit well under 20% of the time.
        (lit / (float)total).Should().BeLessThan(0.2f);
    }
}
