using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class LivesManagerTests
{
    [Fact]
    public void PerPlayer_StartingLives_Available()
    {
        var lives = new LivesManager(LivesMode.PerPlayer, startingLives: 3, playerCount: 2);
        lives.HasLives(0).Should().BeTrue();
        lives.HasLives(1).Should().BeTrue();
    }

    [Fact]
    public void PerPlayer_SpendLastLife_PlayerOutButNotEveryoneOut()
    {
        var lives = new LivesManager(LivesMode.PerPlayer, startingLives: 1, playerCount: 2);
        bool allOut = false;
        lives.OnAllOut += () => allOut = true;

        lives.SpendLife(0).Should().BeTrue(); // spent the life
        lives.HasLives(0).Should().BeFalse(); // player 0 is out
        lives.HasLives(1).Should().BeTrue(); // player 1 still has lives
        allOut.Should().BeFalse(); // not everyone out
    }

    [Fact]
    public void PerPlayer_EveryoneOut_AllOutFires()
    {
        var lives = new LivesManager(LivesMode.PerPlayer, startingLives: 1, playerCount: 2);
        bool allOut = false;
        lives.OnAllOut += () => allOut = true;

        lives.SpendLife(0);
        lives.SpendLife(1);
        allOut.Should().BeTrue();
    }

    [Fact]
    public void PerPlayer_NoRespawn_WhenOut()
    {
        var lives = new LivesManager(LivesMode.PerPlayer, startingLives: 1, playerCount: 1);
        lives.SpendLife(0);
        lives.HasLives(0).Should().BeFalse();
        lives.SpendLife(0).Should().BeFalse(); // can't spend again
    }

    [Fact]
    public void SharedPool_SpendLife_DecrementsShared()
    {
        var lives = new LivesManager(LivesMode.SharedPool, startingLives: 2, playerCount: 4);
        lives.HasLives(0).Should().BeTrue();
        lives.SpendLife(0).Should().BeTrue();
        lives.SharedLives.Should().Be(1);
        lives.HasLives(1).Should().BeTrue(); // shared pool still has 1
    }

    [Fact]
    public void SharedPool_Empty_EveryoneOut()
    {
        var lives = new LivesManager(LivesMode.SharedPool, startingLives: 1, playerCount: 4);
        bool allOut = false;
        lives.OnAllOut += () => allOut = true;

        lives.SpendLife(0);
        allOut.Should().BeTrue();
        lives.HasLives(0).Should().BeFalse();
    }

    [Fact]
    public void AddLife_IncreasesLives()
    {
        var lives = new LivesManager(LivesMode.PerPlayer, startingLives: 1, playerCount: 2);
        lives.SpendLife(0);
        lives.HasLives(0).Should().BeFalse();
        lives.AddLife(0);
        lives.HasLives(0).Should().BeTrue();
    }
}
