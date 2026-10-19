using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class QuestProgressionTests
{
    [Fact]
    public void HasSixtyQuests()
    {
        QuestProgression.TotalQuests.Should().Be(60);
        QuestProgression.All.Length.Should().Be(60);
    }

    [Fact]
    public void Chapter1Level1_UnlockedByDefault()
    {
        var q = new QuestProgression();
        q.IsUnlocked(1, 1).Should().BeTrue();
    }

    [Fact]
    public void Chapter2_LockedUntilChapter1Cleared()
    {
        var q = new QuestProgression();
        q.IsUnlocked(2, 1).Should().BeFalse();
    }

    [Fact]
    public void Level2_LockedUntilLevel1Cleared()
    {
        var q = new QuestProgression();
        q.IsUnlocked(1, 2).Should().BeFalse();
    }

    [Fact]
    public void StartQuest_UnlocksAndTracks()
    {
        var q = new QuestProgression();
        q.StartQuest(1, 1).Should().BeTrue();
        q.ActiveQuest.Should().NotBeNull();
        q.ActiveQuest!.Chapter.Should().Be(1);
        q.ActiveQuest!.Level.Should().Be(1);
        q.ActiveQuest!.TargetKills.Should().Be(15);
    }

    [Fact]
    public void StartQuest_Locked_ReturnsFalse()
    {
        var q = new QuestProgression();
        q.StartQuest(2, 1).Should().BeFalse();
    }

    [Fact]
    public void RecordKill_FillsProgression()
    {
        var q = new QuestProgression();
        q.StartQuest(1, 1);
        for (int i = 0; i < 15; i++)
            q.RecordKill();
        q.ActiveQuestComplete.Should().BeTrue();
        q.ActiveQuestProgress.Should().Be(1f);
    }

    [Fact]
    public void CompleteQuest_UnlocksNextAndGrantsReward()
    {
        var q = new QuestProgression();
        q.StartQuest(1, 1);
        for (int i = 0; i < 15; i++)
            q.RecordKill();
        var completed = q.CompleteActiveQuest();
        completed.Should().Be(0);
        q.CompletedQuests.Should().Contain(0);
        q.IsUnlocked(1, 2).Should().BeTrue(); // next quest unlocked
        q.UnlockedWeapons.Should().Contain(WeaponId.Pistol); // quest 1 reward
    }

    [Fact]
    public void ClearingChapter1_UnlocksChapter2()
    {
        var q = new QuestProgression();
        for (int level = 1; level <= 10; level++)
        {
            q.StartQuest(1, level);
            var def = QuestProgression.Get(1, level);
            for (int k = 0; k < def.TargetKills; k++)
                q.RecordKill();
            q.CompleteActiveQuest();
        }
        q.HighestUnlockedChapter.Should().Be(2);
        q.IsUnlocked(2, 1).Should().BeTrue();
    }

    [Fact]
    public void IsBeaten_WhenAllSixtyComplete()
    {
        var q = new QuestProgression();
        q.IsBeaten.Should().BeFalse();
        for (int i = 0; i < 60; i++)
            q.CompletedQuests.Add(i);
        q.IsBeaten.Should().BeTrue();
    }

    [Fact]
    public void QuestDef_DisplayLabel_Formatted()
    {
        var d = QuestProgression.Get(1, 1);
        d.DisplayLabel.Should().Be("Chapter 1 – Level 1: First Contact");
    }
}
