using System;
using Xunit;
using FluentAssertions;
using System.Reflection;
using System.Runtime.Serialization;
using HeroArena;

namespace HeroArena.Tests.Heroes;

public partial class DummyHero : HeroBase
{
    public override void Attack() { }
    public override void Dodge() { }
    public override void UseAbility() { }

    // We override LevelUp so it just increases Level without calling EventBus
    protected override void LevelUp()
    {
        var levelProp = typeof(HeroBase).GetProperty("Level");
        int current = (int)levelProp.GetValue(this)!;
        levelProp.SetValue(this, current + 1);
    }
}

public class HeroBaseTests
{
    [Theory]
    [InlineData(0, 1, 0)]          // 0 XP -> Lvl 1, 0 XP
    [InlineData(100, 1, 100)]      // 100 XP -> still Lvl 1, 100 XP (needs 160)
    [InlineData(160, 2, 0)]        // 160 XP -> Lvl 2, 0 XP
    [InlineData(200, 2, 40)]       // 200 XP -> Lvl 2, 40 XP (needs 160 for Lvl 2, leaves 40. Next needs 240)
    [InlineData(400, 3, 0)]        // 400 XP -> Lvl 2 (160) -> 240 XP left -> Lvl 3 (240 req) -> 0 XP left -> Lvl 3, 0 XP
    [InlineData(1000, 4, 260)]     // 1000 XP -> 160 (Lvl 2) -> 240 (Lvl 3) -> 340 (Lvl 4) -> 740 used. 260 left. Lvl 4, 260 XP (needs 460 for Lvl 5)
    public void AddExperience_LevelsUpCorrectly(int xpToAdd, int expectedLevel, int expectedExp)
    {
#pragma warning disable SYSLIB0050
        var hero = (DummyHero)FormatterServices.GetUninitializedObject(typeof(DummyHero));

        var levelProgField = typeof(HeroBase).GetField("_levelProg", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var levelProg = (LevelProgression)FormatterServices.GetUninitializedObject(typeof(LevelProgression));
        levelProgField.SetValue(hero, levelProg);

        var levelProp = typeof(HeroBase).GetProperty("Level")!;
        levelProp.SetValue(hero, 1);

        var expProp = typeof(HeroBase).GetProperty("Experience")!;
        expProp.SetValue(hero, 0);
#pragma warning restore SYSLIB0050

        hero.Level.Should().Be(1);
        hero.Experience.Should().Be(0);

        hero.AddExperience(xpToAdd);

        hero.Level.Should().Be(expectedLevel);
        hero.Experience.Should().Be(expectedExp);
    }

    [Fact]
    public void AddExperience_SequentialAdds_LevelsUpCorrectly()
    {
#pragma warning disable SYSLIB0050
        var hero = (DummyHero)FormatterServices.GetUninitializedObject(typeof(DummyHero));

        var levelProgField = typeof(HeroBase).GetField("_levelProg", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var levelProg = (LevelProgression)FormatterServices.GetUninitializedObject(typeof(LevelProgression));
        levelProgField.SetValue(hero, levelProg);

        var levelProp = typeof(HeroBase).GetProperty("Level")!;
        levelProp.SetValue(hero, 1);

        var expProp = typeof(HeroBase).GetProperty("Experience")!;
        expProp.SetValue(hero, 0);
#pragma warning restore SYSLIB0050

        // Add 100 XP (still level 1)
        hero.AddExperience(100);
        hero.Level.Should().Be(1);
        hero.Experience.Should().Be(100);

        // Add 60 XP (reaches 160, levels up to 2, 0 XP left)
        hero.AddExperience(60);
        hero.Level.Should().Be(2);
        hero.Experience.Should().Be(0);

        // Add 300 XP (level 3 requires 240. 300-240=60 left. levels up to 3)
        hero.AddExperience(300);
        hero.Level.Should().Be(3);
        hero.Experience.Should().Be(60);
    }
}
