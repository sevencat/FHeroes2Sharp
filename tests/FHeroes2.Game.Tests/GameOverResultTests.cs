using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The end-of-scenario verdict of <c>game_over.cpp:464-702</c>, ported by <see cref="GameOverResult"/>.
/// </summary>
/// <remarks>
/// 参考的 <c>checkGameOver()</c> 直接弹窗、放片子；端口把那些副作用收进 <see cref="GameOverResult.Requests"/>，
/// 所以下面既能断言「判成什么」，也能断言「本该弹什么」。
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class GameOverResultTests
{
    private const int MapSize = 36;

    /// <summary>Column 1, row 1: the hero that keeps a kingdom from being lost.</summary>
    private const int HeroTileIndex = 37;

    /// <summary>The tile of the single hero the test maps carry.</summary>
    /// <returns>The tile.</returns>
    private static Mp2TileInfo HeroTile() => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = 0,
        Quantity1 = 1 << 3 | 1,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Hero,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>A synthetic map of two kingdoms, one blue hero and the given conditions.</summary>
    /// <param name="kingdomColors">The colors in play.</param>
    /// <param name="victory">How the scenario is won.</param>
    /// <param name="victoryParameters">The parameters of the victory condition.</param>
    /// <param name="loss">How the scenario is lost.</param>
    /// <param name="lossParameters">The parameters of the loss condition.</param>
    /// <param name="allowNormalVictory">Whether defeating everyone counts as a victory as well.</param>
    /// <returns>The map.</returns>
    private static Mp2Map Map(
        PlayerColor kingdomColors = PlayerColor.Blue,
        MapVictoryCondition victory = MapVictoryCondition.DefeatEveryone,
        int[]? victoryParameters = null,
        MapLossCondition loss = MapLossCondition.Everything,
        int[]? lossParameters = null,
        bool allowNormalVictory = true)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTileIndex, HeroTile());
        map = map with { InfoBlocks = [new Mp2InfoBlock(0, new byte[Mp2HeroInfo.StructureSize])] };

        return map with
        {
            Info = new Mp2FileInfo
            {
                FilePath = "gameover.mp2",
                Name = "GameOver",
                Description = string.Empty,
                Width = MapSize,
                Height = MapSize,
                Difficulty = GameDifficulty.Normal,
                KingdomColors = kingdomColors,
                HumanPlayers = kingdomColors,
                ComputerPlayers = kingdomColors,
                VictoryCondition = victory,
                AllowNormalVictory = allowNormalVictory,
                ComputerAlsoWins = false,
                VictoryConditionParameters = victoryParameters ?? [0, 0],
                LossCondition = loss,
                LossConditionParameters = lossParameters ?? [0, 0],
            },
        };
    }

    /// <summary>Loads a map into a fresh world and gives the humans their slots, the reference's game start.</summary>
    /// <param name="map">The map to load.</param>
    /// <param name="humanColors">The colors a person drives; every other color of the map falls to the computer.</param>
    /// <returns>The world, already assigned to <see cref="World.Current"/>.</returns>
    private static World StartGame(Mp2Map map, PlayerColor humanColors)
    {
        Settings.Reset();

        Settings settings = Settings.Get();
        settings.Players.Init(map.Info.KingdomColors);

        foreach (PlayerColor color in PlayerColors.Ordered)
        {
            if ((map.Info.KingdomColors & color) != 0)
            {
                Players.SetPlayerControl(color, (humanColors & color) != 0 ? PlayerControl.Human : PlayerControl.Ai);
            }
        }

        settings.Players.SetStartGame();

        World world = new();
        world.LoadMap(map);
        World.Current = world;

        return world;
    }

    [Fact]
    public void ResetTakesTheColorsOfThePlayerTable()
    {
        StartGame(Map(PlayerColor.Blue | PlayerColor.Red), PlayerColor.Blue);

        GameOverResult result = new();
        result.Reset();

        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, result.Colors);
        Assert.Equal(GameOver.ConditionNone, result.Condition);
        Assert.True(result.Requests.IsEmpty);
    }

    [Fact]
    public void AQuietTurnKeepsTheGameGoing()
    {
        // 蓝色有英雄所以没输；胜利条件要十万金币，谁都还没攒到。
        StartGame(
            Map(
                PlayerColor.Blue | PlayerColor.Red,
                victory: MapVictoryCondition.CollectGold,
                victoryParameters: [100, 0],
                allowNormalVictory: false),
            PlayerColor.Blue);

        GameOverResult result = new();
        result.Reset();

        Assert.Equal(GameOverMode.Cancel, result.CheckGameOver());
        Assert.Equal(GameOver.ConditionNone, result.Condition);
        Assert.True(result.Requests.IsEmpty);
    }

    [Fact]
    public void TheLastKingdomStandingWinsTheScenario()
    {
        // 只有蓝色有英雄，红色什么都没有：`DefeatEveryone` 立刻成立。
        StartGame(Map(PlayerColor.Blue | PlayerColor.Red), PlayerColor.Blue);

        GameOverResult result = new();
        result.Reset();

        Assert.Equal(GameOverMode.HighScoresStandard, result.CheckGameOver());
        Assert.Equal(GameOver.WinsAll, result.Condition);
        Assert.Equal(GameOver.WinsAll, result.Requests.WinsNotice);
        Assert.True(result.Requests.WinVideo);
        Assert.True(result.Requests.VictoryMusic);
        Assert.Null(result.Requests.LossNotice);
    }

    [Fact]
    public void GoldAlreadyInTheTreasuryWinsTheScenario()
    {
        StartGame(
            Map(
                victory: MapVictoryCondition.CollectGold,
                victoryParameters: [1, 0],
                allowNormalVictory: false),
            PlayerColor.Blue);

        // 普通难度开局 7500 金币，条件要的是一千。
        GameOverResult result = new();
        result.Reset();

        Assert.Equal(GameOverMode.HighScoresStandard, result.CheckGameOver());
        Assert.Equal(GameOver.WinsGold, result.Requests.WinsNotice);
    }

    [Fact]
    public void ACampaignVictoryStopsAtTheScenario()
    {
        StartGame(
            Map(victory: MapVictoryCondition.CollectGold, victoryParameters: [1, 0], allowNormalVictory: false),
            PlayerColor.Blue);
        Settings.Get().SetGameType(GameTypes.Campaign);

        GameOverResult result = new();
        result.Reset();

        // 战役的下一关选择由调用方接管，所以参考这时连片子都不放。
        Assert.Equal(GameOverMode.CompleteCampaignScenario, result.CheckGameOver());
        Assert.Equal(GameOver.WinsGold, result.Requests.WinsNotice);
        Assert.False(result.Requests.WinVideo);
        Assert.False(result.Requests.VictoryMusic);
    }

    [Fact]
    public void AVanquishedKingdomLosesTheSinglePlayerGame()
    {
        World world = StartGame(Map(), PlayerColor.Blue);

        world.GetKingdom(PlayerColor.Blue).LossPostActions();

        GameOverResult result = new();
        result.Reset();

        Assert.Equal(GameOverMode.MainMenu, result.CheckGameOver());
        Assert.Equal(GameOver.LossAll, result.Condition);
        Assert.Equal(GameOver.LossAll, result.Requests.LossNotice);
        Assert.True(result.Requests.LoseVideo);
        Assert.Empty(result.Requests.Vanquished);
    }

    [Fact]
    public void AVanquishedComputerIsAnnouncedAndDroppedFromTheColors()
    {
        World world = StartGame(Map(PlayerColor.Blue | PlayerColor.Red), PlayerColor.Blue);

        // 电脑红方出局：参考对电脑一定要说一声，然后把它从颜色集合里摘掉。
        world.GetKingdom(PlayerColor.Red).LossPostActions();

        GameOverResult result = new();
        result.Reset();
        result.CheckGameOver();

        Assert.Equal([PlayerColor.Red], result.Requests.Vanquished);
        Assert.Equal(PlayerColor.Blue, result.Colors);
    }

    [Fact]
    public void AVanquishedHumanInAMultiplayerGameDoesNotEndIt()
    {
        World world = StartGame(Map(PlayerColor.Blue | PlayerColor.Red), PlayerColor.Blue | PlayerColor.Red);

        world.GetKingdom(PlayerColor.Blue).LossPostActions();
        Settings.Get().Players.SetCurrentColor(PlayerColor.Blue);

        GameOverResult result = new();
        result.Reset();

        // 还有红方人类在局中，所以只弹一条失败通知，不回落主菜单。
        Assert.Equal(GameOverMode.Cancel, result.CheckGameOver());
        Assert.Equal(GameOver.LossAll, result.Condition);
        Assert.Equal(GameOver.LossAll, result.Requests.LossNotice);
        Assert.False(result.Requests.LoseVideo);
        Assert.Empty(result.Requests.Vanquished);
        Assert.Equal(PlayerColor.Red, result.Colors);
    }

    [Fact]
    public void TheVerdictTakesFiveBytesAndRoundTrips()
    {
        StartGame(Map(PlayerColor.Blue | PlayerColor.Red), PlayerColor.Blue);

        GameOverResult written = new();
        written.Reset();

        StreamBuf stream = new();
        written.WriteTo(stream);

        Assert.Equal(GameOverResult.SerializedSize, stream.Written.Length);

        GameOverResult read = new();
        Assert.True(read.ReadFrom(new RoStreamBuf(stream.Written.ToArray()), SaveFileFormatInfo.Current));
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, read.Colors);
        Assert.Equal(GameOver.ConditionNone, read.Condition);
    }

    [Fact]
    public void BeforeOneOneOhNineTheColorsAreFourBytesWide()
    {
        // 1.1.09 之前 PlayerColorsSet 是 int32_t：老存档读成 1 字节会让后面所有字段错位。
        StreamBuf stream = new();
        stream.Write((int)(PlayerColor.Blue | PlayerColor.Yellow));
        stream.Write(GameOver.LossTime);

        GameOverResult read = new();
        Assert.True(read.ReadFrom(new RoStreamBuf(stream.Written.ToArray()), SaveFileFormat.Release1_1_08));

        Assert.Equal(PlayerColor.Blue | PlayerColor.Yellow, read.Colors);
        Assert.Equal(GameOver.LossTime, read.Condition);
    }

    [Fact]
    public void AStreamThatAlreadyFailedIsNotRead()
    {
        RoStreamBuf source = new([(byte)PlayerColor.Blue, 0, 0, 0, 0]);
        source.SetFail();

        GameOverResult read = new();

        Assert.False(read.ReadFrom(source, SaveFileFormatInfo.Current));
        Assert.Equal(PlayerColor.None, read.Colors);
        Assert.Equal(GameOver.ConditionNone, read.Condition);
    }

    [Fact]
    public void ATruncatedVerdictReadsZeroesInsteadOfThrowing()
    {
        // RoStreamBuf 的短读按 StreamBase 的约定补零而不抛，所以损坏的存档由调用方在最后查 Fail。
        RoStreamBuf source = new([(byte)PlayerColor.Blue]);

        GameOverResult read = new();

        Assert.True(read.ReadFrom(source, SaveFileFormatInfo.Current));
        Assert.Equal(PlayerColor.Blue, read.Colors);
        Assert.Equal(GameOver.ConditionNone, read.Condition);
    }
}
