using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="MapPlaytest"/>: the headless run of the editor's AUTO PLAYTEST
/// (<c>game_auto_playtest.cpp:280-360</c>), which plays every kingdom with the computer for a number of
/// days and reports how each one ended.
/// </summary>
/// <remarks>
/// In the <see cref="SettingsSingletonCollection.Name"/> collection because starting a game fills the global
/// <see cref="Players"/> table, which every test here does.
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class MapPlaytestTests
{
    private const int MapSize = 36;

    /// <summary>Blue hero, row 1 near the north-west corner.</summary>
    private const int BlueHeroTile = 37;

    /// <summary>Green hero, the far south-east corner, so the two never meet within a few days.</summary>
    private const int GreenHeroTile = (MapSize * MapSize) - 38;

    /// <summary>
    /// A hero tile: the <c>MINIHERO</c> sprite of <paramref name="spriteIndex"/> refers to information
    /// block <paramref name="blockId"/>. Seven sprites per color in <see cref="PlayerColors.Ordered"/>
    /// order, so 0 is a blue knight and 7 a green one.
    /// </summary>
    private static Mp2TileInfo HeroTile(int blockId, int spriteIndex) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = (byte)spriteIndex,
        Quantity1 = (byte)((blockId << 3) | 1),
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Hero,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>A hero information block without a custom portrait, so the tile's race is kept.</summary>
    private static byte[] HeroBlock()
    {
        byte[] data = new byte[Mp2HeroInfo.StructureSize];

        data[17] = 1;

        return data;
    }

    /// <summary>
    /// A small map whose header puts <paramref name="kingdomColors"/> in play; <paramref name="footer"/>
    /// adds the heroes and information blocks.
    /// </summary>
    /// <param name="kingdomColors">The colours that start with a kingdom.</param>
    /// <param name="footer">Edits to apply to the blank map before its header is narrowed down.</param>
    /// <returns>The map.</returns>
    private static Mp2Map Map(PlayerColor kingdomColors, Func<Mp2Map, Mp2Map>? footer = null)
    {
        Settings.Reset();

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        if (footer is not null)
        {
            map = footer(map);
        }

        return map with
        {
            Info = map.Info with
            {
                KingdomColors = kingdomColors,

                // 两个玩家色都能让人类玩、也都能让电脑玩——试玩必须自己把「人类」那张表清掉。
                HumanPlayers = kingdomColors,
                ComputerPlayers = kingdomColors,
            },
        };
    }

    /// <summary>A map where both colours start with a hero and therefore stay in the game.</summary>
    private static Mp2Map TwoHeroes()
    {
        return Map(PlayerColor.Blue | PlayerColor.Green, map =>
        {
            map.SetTileAt(BlueHeroTile, HeroTile(blockId: 1, spriteIndex: 0));
            map.SetTileAt(GreenHeroTile, HeroTile(blockId: 2, spriteIndex: 7));

            return map with
            {
                InfoBlocks =
                [
                    new Mp2InfoBlock(0, HeroBlock()),
                    new Mp2InfoBlock(1, HeroBlock()),
                ],
            };
        });
    }

    [Fact]
    public void RunsToTheDayLimitWithEveryPlayerStillStanding()
    {
        PlaytestReport report = MapPlaytest.Run(TwoHeroes(), maxDays: 3);

        // EndTurn rolls the date to 4 after the last round, but nobody played on that day.
        Assert.Equal(3u, report.Day);
        Assert.Equal(2, report.Players.Count);

        Assert.Equal(PlayerColor.Blue, report.Players[0].Color);
        Assert.Equal(PlayerColor.Green, report.Players[1].Color);

        Assert.All(report.Players, player =>
        {
            Assert.Equal(PlaytestPlayerState.TimeLimit, player.State);
            Assert.Equal(3u, player.Day);
        });

        Assert.Equal(PlayerColor.None, report.Winner);
    }

    [Fact]
    public void StopsOnTheDayTheLastRivalIsGone()
    {
        // 绿没有城也没有英雄：开局的第一天就出局，试玩不该再跑满十天。
        Mp2Map map = Map(PlayerColor.Blue | PlayerColor.Green, map =>
        {
            map.SetTileAt(BlueHeroTile, HeroTile(blockId: 1, spriteIndex: 0));

            return map with { InfoBlocks = [new Mp2InfoBlock(0, HeroBlock())] };
        });

        PlaytestReport report = MapPlaytest.Run(map, maxDays: 10);

        Assert.Equal(1u, report.Day);

        PlaytestPlayerResult blue = Assert.Single(report.Players, player => player.Color == PlayerColor.Blue);
        PlaytestPlayerResult green = Assert.Single(report.Players, player => player.Color == PlayerColor.Green);

        Assert.Equal(PlaytestPlayerState.Winner, blue.State);
        Assert.Equal(1u, blue.Day);

        Assert.Equal(PlaytestPlayerState.Loser, green.State);
        Assert.Equal(PlayerColor.Blue, report.Winner);
    }

    [Fact]
    public void PlaysAMapWhereEveryPlayerIsComputerControlled()
    {
        Mp2Map map = TwoHeroes();

        // 整张图的头里写着两个色都能让人类玩：AdventureSession.Start 会拒绝全员电脑的开局，
        // 试玩必须把这张表清掉之后照跑不误。
        Assert.NotEqual(PlayerColor.None, map.Info.HumanPlayers);

        PlaytestReport report = MapPlaytest.Run(map, maxDays: 2);

        Assert.Equal(2, report.Players.Count);
        Assert.Equal(2u, report.Day);
    }

    [Fact]
    public void LeavesTheEditorMapAndTheCurrentWorldAlone()
    {
        Mp2Map map = TwoHeroes();
        World sentinel = new();

        World previous = World.Current;
        World.Current = sentinel;

        PlaytestReport report;

        try
        {
            report = MapPlaytest.Run(map, maxDays: 2);

            // 试玩期间换上的临时世界已经换回调用方原来那个。
            Assert.Same(sentinel, World.Current);
        }
        finally
        {
            World.Current = previous;
        }

        Assert.Equal(2u, report.Day);

        // 地图头是调用方（编辑器）的，试玩只在自己的副本上清人类表。
        Assert.Equal(PlayerColor.Blue | PlayerColor.Green, map.Info.HumanPlayers);
    }

    [Fact]
    public void ADayLimitBelowOneIsRefused()
    {
        Mp2Map map = TwoHeroes();

        Assert.Throws<ArgumentOutOfRangeException>(() => MapPlaytest.Run(map, maxDays: 0));
    }

    [Fact]
    public void SettingsStartAtTheDefaultsOfTheReference()
    {
        PlaytestSettings settings = new();

        Assert.Equal(1, settings.MaxPlaythroughs);
        Assert.Equal(336, settings.MaxDays);
        Assert.Equal(PlaytestSettings.AnimationSpeedLimit, settings.AnimationSpeed);
        Assert.True(settings.AnimationEnabled);
        Assert.True(settings.EnvironmentSounds);
    }

    [Fact]
    public void SettingsAreClampedIntoTheirSliderRanges()
    {
        PlaytestSettings tooSmall = new(maxPlaythroughs: 0, maxDays: -5, animationSpeed: 0);

        Assert.Equal(PlaytestSettings.MinimumPlaythroughs, tooSmall.MaxPlaythroughs);
        Assert.Equal(MapPlaytest.MinimumDays, tooSmall.MaxDays);
        Assert.Equal(PlaytestSettings.MinimumAnimationSpeed, tooSmall.AnimationSpeed);

        PlaytestSettings tooLarge = new(maxPlaythroughs: 500, maxDays: 99999, animationSpeed: 42);

        Assert.Equal(PlaytestSettings.PlaythroughLimit, tooLarge.MaxPlaythroughs);
        Assert.Equal(MapPlaytest.DayLimit, tooLarge.MaxDays);
        Assert.Equal(PlaytestSettings.AnimationSpeedLimit, tooLarge.AnimationSpeed);
    }

    [Fact]
    public void TheSummaryCountsWinnersAgainstEveryPlaythrough()
    {
        // 三轮：一轮打满天数上限、一轮蓝胜、一轮绿胜。上限那一轮整轮剔除，但分母仍是 3——
        // 参考的 wins * 100 / playthroughCount 就是这么算的。
        PlaytestSummary summary = new(
        [
            Report(PlayerColor.Blue, PlaytestPlayerState.TimeLimit, PlayerColor.Green, PlaytestPlayerState.TimeLimit),
            Report(PlayerColor.Blue, PlaytestPlayerState.Winner, PlayerColor.Green, PlaytestPlayerState.Loser),
            Report(PlayerColor.Blue, PlaytestPlayerState.Loser, PlayerColor.Green, PlaytestPlayerState.Winner),
        ]);

        Assert.Equal(3, summary.Count);
        Assert.Equal(1, summary.TimeLimitCount);

        PlaytestColorResult blue = Assert.Single(summary.Colors, color => color.Color == PlayerColor.Blue);

        Assert.Equal(1, blue.Wins);
        Assert.Equal(3, blue.Playthroughs);
        Assert.Equal(33, blue.Percentage);

        PlaytestColorResult green = Assert.Single(summary.Colors, color => color.Color == PlayerColor.Green);

        Assert.Equal(1, green.Wins);
        Assert.Equal(33, green.Percentage);
    }

    [Fact]
    public void APlaytestThatAlwaysHitTheDayLimitGivesNobodyAPercentage()
    {
        PlaytestSummary summary = new(
        [
            Report(PlayerColor.Blue, PlaytestPlayerState.TimeLimit, PlayerColor.Green, PlaytestPlayerState.TimeLimit),
            Report(PlayerColor.Blue, PlaytestPlayerState.TimeLimit, PlayerColor.Green, PlaytestPlayerState.TimeLimit),
        ]);

        Assert.Equal(2, summary.TimeLimitCount);
        Assert.All(summary.Colors, color =>
        {
            Assert.Equal(0, color.Wins);
            Assert.Equal(0, color.Percentage);
        });
    }

    [Fact]
    public void AnEmptySummaryListsNoColour()
    {
        PlaytestSummary summary = PlaytestSummary.Empty;

        Assert.Equal(0, summary.Count);
        Assert.Equal(0, summary.TimeLimitCount);
        Assert.Empty(summary.Colors);
    }

    [Fact]
    public void ARelativePathRunsEveryPlaythroughAndTalliesTheResults()
    {
        // 两个英雄在地图两角，两天内谁也碰不到谁：两轮都打满天数上限。
        PlaytestSummary summary = MapPlaytest.RunMany(TwoHeroes(), new PlaytestSettings(maxPlaythroughs: 2, maxDays: 2));

        Assert.Equal(2, summary.Count);
        Assert.Equal(2, summary.TimeLimitCount);
        Assert.All(summary.Reports, report => Assert.Equal(2u, report.Day));
        Assert.All(summary.Colors, color => Assert.Equal(0, color.Percentage));
    }

    [Fact]
    public void ASeriesWithAWinnerCountsTheWin()
    {
        // 绿没有英雄，开局第一天就出局：唯一的一轮蓝胜。
        Mp2Map map = Map(PlayerColor.Blue | PlayerColor.Green, map =>
        {
            map.SetTileAt(BlueHeroTile, HeroTile(blockId: 1, spriteIndex: 0));

            return map with { InfoBlocks = [new Mp2InfoBlock(0, HeroBlock())] };
        });

        PlaytestSummary summary = MapPlaytest.RunMany(map, new PlaytestSettings(maxPlaythroughs: 1, maxDays: 5));

        Assert.Equal(1, summary.Count);
        Assert.Equal(0, summary.TimeLimitCount);

        PlaytestColorResult blue = Assert.Single(summary.Colors, color => color.Color == PlayerColor.Blue);
        PlaytestColorResult green = Assert.Single(summary.Colors, color => color.Color == PlayerColor.Green);

        Assert.Equal(100, blue.Percentage);
        Assert.Equal(0, green.Percentage);
    }

    private static PlaytestReport Report(
        PlayerColor firstColor, PlaytestPlayerState firstState, PlayerColor secondColor, PlaytestPlayerState secondState) =>
        new(1,
        [
            new PlaytestPlayerResult(firstColor, firstState, 1),
            new PlaytestPlayerResult(secondColor, secondState, 1),
        ]);
}
