using FHeroes2.Assets.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The names of the difficulty levels, <c>Difficulty::String()</c> (<c>difficulty.cpp:39-57</c>).
/// </summary>
public sealed class DifficultyNameTests
{
    [Theory]
    [InlineData(GameDifficulty.Easy, "Easy")]
    [InlineData(GameDifficulty.Normal, "Normal")]
    [InlineData(GameDifficulty.Hard, "Hard")]
    [InlineData(GameDifficulty.Expert, "Expert")]
    [InlineData(GameDifficulty.Impossible, "Impossible")]
    public void EveryLevelHasItsName(GameDifficulty difficulty, string expected)
    {
        Assert.Equal(expected, Difficulty.GetName(difficulty));
    }

    [Fact]
    public void AnUnknownLevelIsNamedUnknown()
    {
        Assert.Equal("Unknown", Difficulty.GetName((GameDifficulty)7));
    }
}
