using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

// RunStats lives in the HeroArena namespace (parent namespace, visible here).

public class AchievementManagerTests
{
    [Fact]
    public void HasTenAchievements()
    {
        AchievementManager.TotalAchievements.Should().Be(10);
        AchievementManager.All.Length.Should().Be(10);
    }

    [Fact]
    public void FirstBlood_UnlocksOnFirstKill()
    {
        var a = new AchievementManager();
        a.Check(new RunStats(0, 0, 0, 0, 0, 0, false));
        a.IsUnlocked("first_blood").Should().BeFalse();

        a.Check(new RunStats(1, 0, 0, 0, 0, 0, false));
        a.IsUnlocked("first_blood").Should().BeTrue();
    }

    [Fact]
    public void WaveSurvivor_UnlocksAtWave10()
    {
        var a = new AchievementManager();
        a.Check(new RunStats(0, 9, 0, 0, 0, 0, false));
        a.IsUnlocked("wave_survivor").Should().BeFalse();

        a.Check(new RunStats(0, 10, 0, 0, 0, 0, false));
        a.IsUnlocked("wave_survivor").Should().BeTrue();
    }

    [Fact]
    public void GodLike_RequiresNoDamageRun()
    {
        var a = new AchievementManager();
        a.Check(new RunStats(500, 30, 50, 10, 1, 10, true));
        a.IsUnlocked("god_like").Should().BeFalse();

        a.Check(new RunStats(500, 30, 50, 10, 1, 10, false));
        a.IsUnlocked("god_like").Should().BeTrue();
    }

    [Fact]
    public void QuestMaster_RequiresAllSixty()
    {
        var a = new AchievementManager();
        a.Check(new RunStats(0, 0, 0, 0, 0, 59, false));
        a.IsUnlocked("quest_master").Should().BeFalse();

        a.Check(new RunStats(0, 0, 0, 0, 0, 60, false));
        a.IsUnlocked("quest_master").Should().BeTrue();
    }

    [Fact]
    public void NewlyUnlocked_TracksJustUnlocked()
    {
        var a = new AchievementManager();
        var first = a.Check(new RunStats(1, 0, 0, 0, 0, 0, false));
        first.Should().Contain("first_blood");
        a.NewlyUnlocked.Should().Contain("first_blood");

        // Already unlocked -> not newly unlocked again.
        var second = a.Check(new RunStats(1, 0, 0, 0, 0, 0, false));
        second.Should().BeEmpty();
    }

    [Fact]
    public void IsComplete_WhenAllUnlocked()
    {
        var a = new AchievementManager();
        foreach (var def in AchievementManager.All)
            a.Unlock(def.Id);
        a.IsComplete.Should().BeTrue();
    }
}
