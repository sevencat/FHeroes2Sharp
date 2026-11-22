using FHeroes2.Assets.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The scenario rating: the two difficulties it adds up, <c>Game::GetRating()</c>
/// (<c>game.cpp:358-395</c>).
/// </summary>
public sealed class GameRatingTests
{
    [Theory]
    [InlineData(GameDifficulty.Easy, GameDifficulty.Easy, 50u)]
    [InlineData(GameDifficulty.Easy, GameDifficulty.Normal, 80u)]
    [InlineData(GameDifficulty.Easy, GameDifficulty.Hard, 100u)]
    [InlineData(GameDifficulty.Easy, GameDifficulty.Expert, 120u)]
    [InlineData(GameDifficulty.Easy, GameDifficulty.Impossible, 140u)]
    [InlineData(GameDifficulty.Normal, GameDifficulty.Easy, 70u)]
    [InlineData(GameDifficulty.Normal, GameDifficulty.Normal, 100u)]
    [InlineData(GameDifficulty.Hard, GameDifficulty.Easy, 90u)]
    [InlineData(GameDifficulty.Hard, GameDifficulty.Hard, 140u)]
    [InlineData(GameDifficulty.Expert, GameDifficulty.Easy, 130u)]
    [InlineData(GameDifficulty.Expert, GameDifficulty.Normal, 160u)]
    [InlineData(GameDifficulty.Impossible, GameDifficulty.Easy, 130u)]
    [InlineData(GameDifficulty.Impossible, GameDifficulty.Impossible, 220u)]
    public void TheRatingAddsTheMapAndTheGameDifficulty(GameDifficulty mapDifficulty,
        GameDifficulty gameDifficulty, uint expected)
    {
        // 两个难度各给一档，基线 50：Easy 在两边都不加，不可能在两边都加满（80 + 90）。
        Mp2FileInfo info = Info(mapDifficulty);

        Assert.Equal(expected, GameRating.GetRating(info, gameDifficulty));
    }

    [Fact]
    public void TheRatingSpans_50To220()
    {
        Assert.Equal(50u, GameRating.GetRating(Info(GameDifficulty.Easy), GameDifficulty.Easy));
        Assert.Equal(220u, GameRating.GetRating(Info(GameDifficulty.Impossible), GameDifficulty.Impossible));
    }

    [Fact]
    public void UnknownDifficultiesAddNothing()
    {
        // 参考在 default 里什么都不加，但仍然照常返回基线 50。
        Assert.Equal(50u, GameRating.GetRating(Info((GameDifficulty)42), (GameDifficulty)42));
    }

    [Fact]
    public void TheMapInformationIsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => GameRating.GetRating(null!, GameDifficulty.Normal));
    }

    private static Mp2FileInfo Info(GameDifficulty difficulty) => new()
    {
        FilePath = "rating.mp2",
        Name = "Rating",
        Description = "Only used for its difficulty.",
        Width = 72,
        Height = 72,
        Difficulty = difficulty,
    };
}
