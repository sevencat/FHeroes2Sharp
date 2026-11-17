using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the translation from the map header's two condition blocks into <see cref="GameOver"/> flags and
/// parameters (<c>maps_fileinfo.cpp:602-649</c>).
/// </summary>
public sealed class MapConditionsTests
{
    private static Mp2FileInfo Info(
        MapVictoryCondition victory = MapVictoryCondition.DefeatEveryone,
        bool allowNormalVictory = true,
        bool computerAlsoWins = false,
        int[]? victoryParameters = null,
        MapLossCondition loss = MapLossCondition.Everything,
        int[]? lossParameters = null) => new()
        {
            FilePath = "conditions.mp2",
            Name = "Conditions",
            Description = string.Empty,
            Width = 36,
            Height = 36,
            Difficulty = GameDifficulty.Normal,
            VictoryCondition = victory,
            AllowNormalVictory = allowNormalVictory,
            ComputerAlsoWins = computerAlsoWins,
            VictoryConditionParameters = victoryParameters ?? [0, 0],
            LossCondition = loss,
            LossConditionParameters = lossParameters ?? [0, 0],
        };

    [Fact]
    public void DefeatingEveryoneIsTheOnlyWayToWinByDefault()
    {
        Assert.Equal(GameOver.WinsAll, MapConditions.Wins(Info()));
    }

    [Fact]
    public void ASpecialConditionAddsTheNormalVictoryUnlessTheMapForbidsIt()
    {
        // 参考的 ConditionWins() 把 WINS_ALL 叠在特殊条件上，除非地图关掉了 allowNormalVictory。
        Assert.Equal(
            GameOver.WinsTown | GameOver.WinsAll,
            MapConditions.Wins(Info(MapVictoryCondition.CaptureTown)));

        Assert.Equal(
            GameOver.WinsTown,
            MapConditions.Wins(Info(MapVictoryCondition.CaptureTown, allowNormalVictory: false)));
    }

    [Theory]
    [InlineData(MapVictoryCondition.KillHero, GameOver.WinsHero)]
    [InlineData(MapVictoryCondition.ObtainArtifact, GameOver.WinsArtifact)]
    [InlineData(MapVictoryCondition.CollectGold, GameOver.WinsGold)]
    public void EachVictoryConditionMapsToItsFlag(MapVictoryCondition condition, uint expected)
    {
        Assert.Equal(expected | GameOver.WinsAll, MapConditions.Wins(Info(condition)));
    }

    [Fact]
    public void DefeatingTheOtherSideDoesNotAllowDefeatingEveryone()
    {
        // 灭同盟对面本身就是「把对面全灭」，再叠 WINS_ALL 会把同盟也算进去（参考单独处理这一条）。
        Assert.Equal(
            GameOver.WinsSide,
            MapConditions.Wins(Info(MapVictoryCondition.DefeatOtherSide)));
    }

    [Theory]
    [InlineData(MapLossCondition.Everything, GameOver.LossAll)]
    [InlineData(MapLossCondition.LoseTown, GameOver.LossTown)]
    [InlineData(MapLossCondition.LoseHero, GameOver.LossHero)]
    [InlineData(MapLossCondition.OutOfTime, GameOver.LossTime)]
    public void EachLossConditionMapsToItsFlag(MapLossCondition condition, uint expected)
    {
        Assert.Equal(expected, MapConditions.Loss(Info(loss: condition)));
    }

    [Fact]
    public void TheComputerSharesThePrizeOnlyForTownAndGold()
    {
        // WinsCompAlsoWins() 只对占城与攒金放行：杀将、找神器、灭同盟对电脑没有意义。
        Assert.True(MapConditions.ComputerAlsoWins(
            Info(MapVictoryCondition.CaptureTown, computerAlsoWins: true)));

        Assert.True(MapConditions.ComputerAlsoWins(
            Info(MapVictoryCondition.CollectGold, computerAlsoWins: true)));

        Assert.False(MapConditions.ComputerAlsoWins(
            Info(MapVictoryCondition.KillHero, computerAlsoWins: true)));

        Assert.False(MapConditions.ComputerAlsoWins(
            Info(MapVictoryCondition.CaptureTown, computerAlsoWins: false)));
    }

    [Fact]
    public void TheVictoryPositionIsAColumnAndARow()
    {
        Mp2FileInfo info = Info(MapVictoryCondition.CaptureTown, victoryParameters: [23, 41]);

        Assert.Equal(new Core.Geometry.Point(23, 41), MapConditions.WinsPosition(info));
    }

    [Fact]
    public void TheLossPositionComesFromTheLossBlock()
    {
        Mp2FileInfo info = Info(loss: MapLossCondition.LoseTown, lossParameters: [7, 9]);

        Assert.Equal(new Core.Geometry.Point(7, 9), MapConditions.LossPosition(info));
    }

    [Fact]
    public void AZeroArtifactMeansTheBuriedOne()
    {
        Mp2FileInfo info = Info(MapVictoryCondition.ObtainArtifact, victoryParameters: [0, 0]);

        Assert.True(MapConditions.WinsUltimateArtifact(info));
        Assert.Equal(ArtifactType.Unknown, MapConditions.WinsArtifact(info));
    }

    [Fact]
    public void ANonZeroArtifactIsTheOneToFind()
    {
        Mp2FileInfo info = Info(MapVictoryCondition.ObtainArtifact, victoryParameters: [7, 0]);

        Assert.False(MapConditions.WinsUltimateArtifact(info));
        Assert.Equal((ArtifactType)7, MapConditions.WinsArtifact(info));
    }

    [Fact]
    public void TheWinningGoldIsCountedInThousands()
    {
        Assert.Equal(10_000u, MapConditions.WinningGold(
            Info(MapVictoryCondition.CollectGold, victoryParameters: [10, 0])));
    }

    [Fact]
    public void TheDayLimitComesFromTheFirstLossParameter()
    {
        Assert.Equal(120u, MapConditions.LossDays(Info(loss: MapLossCondition.OutOfTime, lossParameters: [120, 0])));
    }

    [Fact]
    public void AnUnknownConditionLetsNobodyWin()
    {
        // 参考这条分支是 assert，发布构建返回 COND_NONE。
        Mp2FileInfo info = Info(victory: (MapVictoryCondition)0xEE);

        Assert.Equal(GameOver.ConditionNone, MapConditions.Wins(info));
    }
}
