using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class LeaderboardManagerTests
{
    private static LeaderboardEntry Entry(string name, long score, int waves, int kills,
        Difficulty difficulty = Difficulty.Normal, bool coop = false, int players = 1) =>
        new()
        {
            PlayerName = name,
            Score = score,
            Waves = waves,
            Kills = kills,
            Difficulty = difficulty,
            IsCoop = coop,
            PlayerCount = players,
        };

    [Fact]
    public void Top_ReturnsHighestScoreFirst()
    {
        var m = new LeaderboardManager();
        m.Add(Entry("A", 100, 5, 10));
        m.Add(Entry("B", 300, 10, 50));
        m.Add(Entry("C", 200, 7, 30));

        var top = m.Top();
        top[0].PlayerName.Should().Be("B");
        top[1].PlayerName.Should().Be("C");
        top[2].PlayerName.Should().Be("A");
    }

    [Fact]
    public void Top_LimitsToRequestedCount()
    {
        var m = new LeaderboardManager();
        for (int i = 0; i < 25; i++)
            m.Add(Entry($"P{i}", i * 10, i, i));
        var top = m.Top(10);
        top.Count.Should().Be(10);
        top[0].Score.Should().Be(240);
    }

    [Fact]
    public void TopCoop_FiltersByCoopFlag()
    {
        var m = new LeaderboardManager();
        m.Add(Entry("Solo1", 100, 5, 10, coop: false));
        m.Add(Entry("Coop1", 200, 10, 50, coop: true));
        m.Add(Entry("Solo2", 150, 6, 20, coop: false));

        var coop = m.TopCoop(true);
        coop.Should().HaveCount(1);
        coop[0].PlayerName.Should().Be("Coop1");

        var solo = m.TopCoop(false);
        solo.Should().HaveCount(2);
    }

    [Fact]
    public void TopDifficulty_FiltersByDifficulty()
    {
        var m = new LeaderboardManager();
        m.Add(Entry("Easy", 100, 5, 10, Difficulty.Easy));
        m.Add(Entry("Hard", 200, 10, 50, Difficulty.Hard));
        m.Add(Entry("Easy2", 150, 6, 20, Difficulty.Easy));

        var hard = m.TopDifficulty(Difficulty.Hard);
        hard.Should().HaveCount(1);
        hard[0].PlayerName.Should().Be("Hard");

        var easy = m.TopDifficulty(Difficulty.Easy);
        easy.Should().HaveCount(2);
    }

    [Fact]
    public void RankOf_ReturnsOneBasedRank()
    {
        var m = new LeaderboardManager();
        var a = Entry("A", 100, 5, 10);
        var b = Entry("B", 300, 10, 50);
        var c = Entry("C", 200, 7, 30);
        m.Add(a);
        m.Add(b);
        m.Add(c);

        m.RankOf(b).Should().Be(1);
        m.RankOf(c).Should().Be(2);
        m.RankOf(a).Should().Be(3);
    }

    [Fact]
    public void RankOf_ReturnsNull_WhenEntryNotPresent()
    {
        var m = new LeaderboardManager();
        m.Add(Entry("A", 100, 5, 10));
        m.RankOf(Entry("Ghost", 999, 99, 99)).Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var m = new LeaderboardManager();
        m.Add(Entry("A", 100, 5, 10));
        m.Clear();
        m.Count.Should().Be(0);
        m.Top().Should().BeEmpty();
    }
}
