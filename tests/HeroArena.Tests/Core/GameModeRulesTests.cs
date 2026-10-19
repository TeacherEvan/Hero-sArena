using System;
using FluentAssertions;
using Xunit;

namespace HeroArena.Tests.Core;

public class GameModeRulesTests
{
    [Fact]
    public void Survival_HasNoLimits()
    {
        var r = GameModeRules.SurvivalRules();
        r.Mode.Should().Be(GameMode.Survival);
        r.RushTimeLimitSeconds.Should().BeNull();
        r.BlitzKillTarget.Should().BeNull();
        r.WeaponPickupsDisabled.Should().BeFalse();
        r.GunsDisabled.Should().BeFalse();
        r.DisplayName.Should().Be("Survival");
        r.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Rush_HasTimeLimit()
    {
        var r = GameModeRules.RushRules(180);
        r.Mode.Should().Be(GameMode.Rush);
        r.RushTimeLimitSeconds.Should().Be(180);
        r.BlitzKillTarget.Should().BeNull();
        r.WeaponPickupsDisabled.Should().BeFalse();
        r.GunsDisabled.Should().BeFalse();
        r.DisplayName.Should().Be("Rush");
    }

    [Fact]
    public void Blitz_HasKillTarget()
    {
        var r = GameModeRules.BlitzRules(100);
        r.Mode.Should().Be(GameMode.Blitz);
        r.BlitzKillTarget.Should().Be(100);
        r.RushTimeLimitSeconds.Should().BeNull();
        r.DisplayName.Should().Be("Blitz");
    }

    [Fact]
    public void WeaponPicker_DisablesWeaponPickups()
    {
        var r = GameModeRules.WeaponPickerRules();
        r.Mode.Should().Be(GameMode.WeaponPicker);
        r.WeaponPickupsDisabled.Should().BeTrue();
        r.GunsDisabled.Should().BeFalse();
        r.DisplayName.Should().Be("Weapon Picker");
    }

    [Fact]
    public void Nukefism_DisablesPickupsAndGuns()
    {
        var r = GameModeRules.NukefismRules();
        r.Mode.Should().Be(GameMode.Nukefism);
        r.WeaponPickupsDisabled.Should().BeTrue();
        r.GunsDisabled.Should().BeTrue();
        r.DisplayName.Should().Be("Nukefism");
    }

    [Fact]
    public void AllModes_HaveNonEmptyDescriptions()
    {
        foreach (GameMode m in Enum.GetValues<GameMode>())
        {
            var r = m switch
            {
                GameMode.Survival => GameModeRules.SurvivalRules(),
                GameMode.Rush => GameModeRules.RushRules(60),
                GameMode.Blitz => GameModeRules.BlitzRules(50),
                GameMode.WeaponPicker => GameModeRules.WeaponPickerRules(),
                GameMode.Nukefism => GameModeRules.NukefismRules(),
                _ => throw new InvalidOperationException(),
            };
            r.DisplayName.Should().NotBeNullOrWhiteSpace();
            r.Description.Should().NotBeNullOrWhiteSpace();
        }
    }
}
