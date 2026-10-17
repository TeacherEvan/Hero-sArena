using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

// Pure difficulty-menu logic behind MainMenu (R2): persisted-value parsing /
// serialization and button highlight states. MainMenu's Godot I/O (ConfigFile
// read/write in user://settings.cfg) is a thin wrapper around this class.
public class MenuDifficultyStateTests
{
    [Theory]
    [InlineData("Easy", Difficulty.Easy)]
    [InlineData("Normal", Difficulty.Normal)]
    [InlineData("Hard", Difficulty.Hard)]
    public void Parse_KnownNames_MapsToDifficulty(string raw, Difficulty expected)
    {
        MenuDifficultyState.Parse(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("easy")]
    [InlineData("EASY")]
    [InlineData("Impossible")]
    public void Parse_MissingOrUnknown_MapsToNormal(string? raw)
    {
        MenuDifficultyState.Parse(raw).Should().Be(Difficulty.Normal);
    }

    [Fact]
    public void Serialize_RoundTrips_EveryDifficulty()
    {
        foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
        {
            MenuDifficultyState.Parse(MenuDifficultyState.Serialize(difficulty))
                .Should().Be(difficulty);
        }
    }

    [Fact]
    public void Serialize_Normal_IsTheCanonicalDefault()
    {
        MenuDifficultyState.Serialize(Difficulty.Normal).Should().Be("Normal");
    }

    [Fact]
    public void IsSelected_MatchesOnlyTheCurrentDifficulty()
    {
        MenuDifficultyState.IsSelected(Difficulty.Easy, Difficulty.Easy).Should().BeTrue();
        MenuDifficultyState.IsSelected(Difficulty.Easy, Difficulty.Normal).Should().BeFalse();
        MenuDifficultyState.IsSelected(Difficulty.Hard, Difficulty.Normal).Should().BeFalse();
    }
}
