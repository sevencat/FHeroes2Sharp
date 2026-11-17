using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the win and loss checks of <see cref="World"/> (<c>world.cpp:941-1157</c>) and the way the loader
/// remembers the two heroes the conditions name (<c>world_loadmap.cpp:1758</c>).
/// </summary>
public sealed class WorldConditionsTests
{
    private const int MapSize = 36;

    /// <summary>Tile the hero of the hero tests stands on: column 1, row 1.</summary>
    private const int HeroTileIndex = 37;

    /// <summary>
    /// The hero tile of the map file: the <c>MINIHERO</c> sprite says which kingdom and class the hero is,
    /// and the quantity word refers to information block 1.
    /// </summary>
    private static Mp2TileInfo HeroTile(int spriteIndex = 0) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = (byte)spriteIndex,
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

    /// <summary>Tile of the town the town tests use: column 18, row 18.</summary>
    private const int TownTileIndex = (18 * MapSize) + 18;

    /// <summary>The gate of the town tests, drawn from <c>OBJNTWRD</c> and referring to information block 1.</summary>
    /// <returns>The tile.</returns>
    private static Mp2TileInfo TownGateTile() => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnTwrd << 2),
        BottomIcnImageIndex = 0,
        Quantity1 = 1 << 3 | 1,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Castle,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>
    /// A synthetic map with the given header fields; <paramref name="hero"/> adds one blue hero on
    /// <see cref="HeroTileIndex"/>, which is what makes a kingdom "not lost".
    /// </summary>
    private static Mp2Map Map(
        PlayerColor kingdomColors = PlayerColor.Blue,
        PlayerColor humanPlayers = PlayerColor.Blue,
        PlayerColor computerPlayers = PlayerColor.None,
        MapVictoryCondition victory = MapVictoryCondition.DefeatEveryone,
        bool allowNormalVictory = true,
        bool computerAlsoWins = false,
        int[]? victoryParameters = null,
        MapLossCondition loss = MapLossCondition.Everything,
        int[]? lossParameters = null,
        bool hero = true,
        bool town = false)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        if (hero)
        {
            map.SetTileAt(HeroTileIndex, HeroTile(spriteIndex: 0));
            map = map with { InfoBlocks = [new Mp2InfoBlock(0, new byte[Mp2HeroInfo.StructureSize])] };
        }

        if (town)
        {
            // 无主的城：表里颜色 0 就是 PlayerColor.None，正好用来测「城不是我的」那两条分支。
            map.SetTileAt(TownTileIndex, TownGateTile());
            map = map with { Castles = [new Mp2CastlePosition(18, 18, 0, MapRace.Knight)] };
        }

        return map with
        {
            Info = new Mp2FileInfo
            {
                FilePath = "conditions.mp2",
                Name = "Conditions",
                Description = string.Empty,
                Width = MapSize,
                Height = MapSize,
                Difficulty = GameDifficulty.Normal,
                KingdomColors = kingdomColors,
                HumanPlayers = humanPlayers,
                ComputerPlayers = computerPlayers,
                VictoryCondition = victory,
                AllowNormalVictory = allowNormalVictory,
                ComputerAlsoWins = computerAlsoWins,
                VictoryConditionParameters = victoryParameters ?? [0, 0],
                LossCondition = loss,
                LossConditionParameters = lossParameters ?? [0, 0],
            },
        };
    }

    [Fact]
    public void AnEmptyWorldHasNoConditions()
    {
        World world = new();

        Assert.Null(world.MapInfo);
        Assert.Null(world.GetHeroesCondWins());
        Assert.Null(world.GetHeroesCondLoss());
        Assert.Equal(GameOver.ConditionNone, world.CheckKingdomWins(world.GetKingdom(PlayerColor.Blue)));
        Assert.Equal(GameOver.ConditionNone, world.CheckKingdomLoss(world.GetKingdom(PlayerColor.Blue)));
    }

    [Fact]
    public void TheHeaderTheWorldWasLoadedFromIsKept()
    {
        // 参考每次判定去 Settings 取那份文件头；端口把它存进世界，判定才有东西可读。
        World world = new();
        world.LoadMap(Map());

        Assert.NotNull(world.MapInfo);
        Assert.Equal(36, world.MapInfo!.Width);
    }

    [Fact]
    public void TheLastKingdomStandingWinsByDefeatingEveryone()
    {
        World world = new();
        world.LoadMap(Map(kingdomColors: PlayerColor.Blue | PlayerColor.Red, computerPlayers: PlayerColor.Red));

        // 红方没有城也没有英雄，已经算输（Kingdom::isLoss），所以剩下的蓝色就是唯一的「没输」。
        Assert.True(world.KingdomIsWins(world.GetKingdom(PlayerColor.Blue), GameOver.WinsAll));
        Assert.False(world.KingdomIsWins(world.GetKingdom(PlayerColor.Red), GameOver.WinsAll));
    }

    [Fact]
    public void DefeatingTheOtherSideNeedsEveryOtherKingdomToBeAFriend()
    {
        World world = new();
        world.LoadMap(Map(victory: MapVictoryCondition.DefeatOtherSide));

        Assert.True(world.KingdomIsWins(world.GetKingdom(PlayerColor.Blue), GameOver.WinsSide));

        World twoSides = new();
        twoSides.LoadMap(Map(
            kingdomColors: PlayerColor.Blue | PlayerColor.Red,
            computerPlayers: PlayerColor.Red,
            victory: MapVictoryCondition.DefeatOtherSide));

        Assert.False(twoSides.KingdomIsWins(twoSides.GetKingdom(PlayerColor.Blue), GameOver.WinsSide));
    }

    [Fact]
    public void EnoughGoldWinsTheScenario()
    {
        World world = new();
        world.LoadMap(Map(
            victory: MapVictoryCondition.CollectGold,
            victoryParameters: [10, 0],
            computerAlsoWins: true));

        var kingdom = world.GetKingdom(PlayerColor.Blue);

        // 普通难度开局 7500 金币（Kingdom::GetStartingResources），条件要的是一万。
        Assert.Equal(7_500, kingdom.Treasury.Gold);

        kingdom.AddFundsResource(new Funds(Resource.Gold, 2_000));

        Assert.False(world.KingdomIsWins(kingdom, GameOver.WinsGold));

        kingdom.AddFundsResource(new Funds(Resource.Gold, 500));

        Assert.True(world.KingdomIsWins(kingdom, GameOver.WinsGold));
    }

    [Fact]
    public void TheComputerCannotWinByGoldUnlessTheMapSaysSo()
    {
        World world = new();
        world.LoadMap(Map(
            kingdomColors: PlayerColor.Blue | PlayerColor.Red,
            computerPlayers: PlayerColor.Red,
            victory: MapVictoryCondition.CollectGold,
            victoryParameters: [1, 0]));

        var red = world.GetKingdom(PlayerColor.Red);
        red.AddFundsResource(new Funds(Resource.Gold, 20_000));

        Assert.False(world.KingdomIsWins(red, GameOver.WinsGold));

        World shareThePrize = new();
        shareThePrize.LoadMap(Map(
            kingdomColors: PlayerColor.Blue | PlayerColor.Red,
            computerPlayers: PlayerColor.Red,
            victory: MapVictoryCondition.CollectGold,
            victoryParameters: [1, 0],
            computerAlsoWins: true));

        var redAgain = shareThePrize.GetKingdom(PlayerColor.Red);
        redAgain.AddFundsResource(new Funds(Resource.Gold, 20_000));

        Assert.True(shareThePrize.KingdomIsWins(redAgain, GameOver.WinsGold));
    }

    [Fact]
    public void TheDayLimitIsExceededOnlyAfterItHasPassed()
    {
        World world = new();
        world.LoadMap(Map(loss: MapLossCondition.OutOfTime, lossParameters: [2, 0]));

        var kingdom = world.GetKingdom(PlayerColor.Blue);

        // 参考判的是 CountDay() > days，所以第 1、2 天都还在期限内。
        world.NewDay();
        Assert.False(world.KingdomIsLoss(kingdom, GameOver.LossTime));

        world.NewDay();
        Assert.False(world.KingdomIsLoss(kingdom, GameOver.LossTime));

        world.NewDay();
        Assert.True(world.KingdomIsLoss(kingdom, GameOver.LossTime));
    }

    [Fact]
    public void AKingdomWithoutTownsAndHeroesHasLost()
    {
        World world = new();
        world.LoadMap(Map(hero: false));

        Assert.True(world.KingdomIsLoss(world.GetKingdom(PlayerColor.Blue), GameOver.LossAll));
    }

    [Fact]
    public void TheHeroTheVictoryConditionNamesIsTheOneOnTheTile()
    {
        World world = new();
        world.LoadMap(Map(
            victory: MapVictoryCondition.KillHero,
            victoryParameters: [1, 1]));

        Hero? hero = world.GetHeroesCondWins();

        Assert.NotNull(hero);
        Assert.Equal(HeroTileIndex, hero!.MapIndex);
    }

    [Fact]
    public void TheNamedHeroMustLeaveTheEnemyToWin()
    {
        // 参考（world.cpp:984）：英雄要么回到酒馆等着被雇，要么已经归到人类控制的王国名下，才算这个条件达成。
        // 他仍由电脑控制的王国带在身边时，玩家什么都还没赢到。
        World world = new();
        world.LoadMap(Map(
            computerPlayers: PlayerColor.Blue,
            humanPlayers: PlayerColor.None,
            victory: MapVictoryCondition.KillHero,
            victoryParameters: [1, 1]));

        Assert.False(world.KingdomIsWins(world.GetKingdom(PlayerColor.Blue), GameOver.WinsHero));

        // 换成人类控制的同一个王国：条件达成（英雄已经在自己手里，对面带不走他了）。
        World humanWorld = new();
        humanWorld.LoadMap(Map(victory: MapVictoryCondition.KillHero, victoryParameters: [1, 1]));

        Assert.True(humanWorld.KingdomIsWins(humanWorld.GetKingdom(PlayerColor.Blue), GameOver.WinsHero));
    }

    [Fact]
    public void TheHeroTheLossConditionNamesCannotBeDismissed()
    {
        World world = new();
        world.LoadMap(Map(loss: MapLossCondition.LoseHero, lossParameters: [1, 1]));

        Hero? hero = world.GetHeroesCondLoss();

        Assert.NotNull(hero);
        Assert.Equal(HeroModes.NotDismiss | HeroModes.Custom, hero!.Modes & (HeroModes.NotDismiss | HeroModes.Custom));
    }

    [Fact]
    public void LosingTheNamedHeroLosesTheScenario()
    {
        // 英雄被电脑控制的王国夺走：蓝色由电脑控制，所以「已经不在自己手里」成立。
        World world = new();
        world.LoadMap(Map(
            computerPlayers: PlayerColor.Blue,
            humanPlayers: PlayerColor.None,
            loss: MapLossCondition.LoseHero,
            lossParameters: [1, 1]));

        Assert.True(world.KingdomIsLoss(world.GetKingdom(PlayerColor.Blue), GameOver.LossHero));
    }

    [Fact]
    public void AConditionWithoutTheNamedHeroIsNeverMet()
    {
        // 条件指着一格空地：参考让整张图装载失败，端口记日志继续，判定退化成「永远不满足」。
        World world = new();
        world.LoadMap(Map(victory: MapVictoryCondition.KillHero, victoryParameters: [30, 30]));

        Assert.Null(world.GetHeroesCondWins());
        Assert.False(world.KingdomIsWins(world.GetKingdom(PlayerColor.Blue), GameOver.WinsHero));
    }

    [Fact]
    public void TheFirstConditionInTheReferenceOrderIsReported()
    {
        // 占城胜利同时允许全灭（allowNormalVictory），而蓝方已经是唯一的「没输」，
        // 参考按 WINS_ALL 在前、WINS_TOWN 在后的顺序问，所以报的是全灭。
        World world = new();
        world.LoadMap(Map(
            kingdomColors: PlayerColor.Blue | PlayerColor.Red,
            computerPlayers: PlayerColor.Red,
            victory: MapVictoryCondition.CaptureTown,
            victoryParameters: [5, 5]));

        Assert.Equal(GameOver.WinsAll, world.CheckKingdomWins(world.GetKingdom(PlayerColor.Blue)));
    }

    [Fact]
    public void AnEnemyVictoryIsReportedBeforeYourOwnLosses()
    {
        World world = new();
        world.LoadMap(Map(
            kingdomColors: PlayerColor.Blue | PlayerColor.Red,
            computerPlayers: PlayerColor.Blue | PlayerColor.Red,
            victory: MapVictoryCondition.CollectGold,
            victoryParameters: [1, 0],
            computerAlsoWins: true));

        world.GetKingdom(PlayerColor.Blue).AddFundsResource(new Funds(Resource.Gold, 1_000));

        // 红方没有城也没有英雄，本来按 LOSS_ALL 就该输；对手先达成条件那条排在前面，所以报的是它。
        Assert.Equal(GameOver.LossEnemyWinsGold, world.CheckKingdomLoss(world.GetKingdom(PlayerColor.Red)));
        Assert.Equal(GameOver.ConditionNone, world.CheckKingdomLoss(world.GetKingdom(PlayerColor.Blue)));
    }

    [Fact]
    public void TheConditionTownIsLookedUpByItsGateTile()
    {
        // 条件里记的是城门那一格（world.cpp:968 的 getCastleEntrance），不是城的中心格。
        World world = new();
        world.LoadMap(Map(
            victory: MapVictoryCondition.CaptureTown,
            victoryParameters: [18, 18],
            allowNormalVictory: false,
            loss: MapLossCondition.LoseTown,
            lossParameters: [18, 18],
            town: true));

        Assert.NotNull(world.GetCastleEntrance(new Core.Geometry.Point(18, 18)));
        Assert.False(world.KingdomIsWins(world.GetKingdom(PlayerColor.Blue), GameOver.WinsTown));

        // 城没人占（PlayerColor.None），所以「别人拿走了我的城」成立。
        Assert.True(world.KingdomIsLoss(world.GetKingdom(PlayerColor.Blue), GameOver.LossTown));
    }

    [Fact]
    public void ATownOutsideTheMapIsNoCondition()
    {
        World world = new();
        world.LoadMap(Map(loss: MapLossCondition.LoseTown, lossParameters: [-1, -1], town: true));

        Assert.Null(world.GetCastleEntrance(new Core.Geometry.Point(-1, -1)));
        Assert.False(world.KingdomIsLoss(world.GetKingdom(PlayerColor.Blue), GameOver.LossTown));
    }

    [Fact]
    public void TheWorldMeasuresFromWhereTheHeroStands()
    {
        // World::getDistance / getPath 只做两件事：把英雄的五个值交给寻路器，然后把同一个问题转过去。
        World world = new();
        world.LoadMap(Map());

        // 寻路只看已经探索过的格子，所以先把地图点亮。
        for (int index = 0; index < world.Size; ++index)
        {
            world.GetTile(index).ClearFog(PlayerColor.Blue);
        }

        Hero hero = world.Heroes.GetAtMapIndex(HeroTileIndex)!;

        Assert.Equal(0u, world.GetDistance(hero, HeroTileIndex));
        Assert.Single(world.GetPath(hero, HeroTileIndex + 1));
    }

    [Fact]
    public void AHeroOutsideTheMapCannotBeMeasuredFrom()
    {
        World world = new();
        world.LoadMap(Map());

        Assert.Throws<ArgumentException>(
            () => world.GetDistance(new Hero(1, MapRace.Knight), HeroTileIndex));
    }

    [Fact]
    public void LoadingAnotherMapForgetsThePreviousConditions()
    {
        World world = new();
        world.LoadMap(Map(victory: MapVictoryCondition.KillHero, victoryParameters: [1, 1]));

        Assert.NotNull(world.GetHeroesCondWins());

        world.LoadMap(Map(victory: MapVictoryCondition.DefeatEveryone, hero: false));

        Assert.Null(world.GetHeroesCondWins());
    }
}
