using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="AdventureSession"/>: the turn loop of the reference's
/// <c>Interface::AdventureMap::StartGame()</c> (<c>game_startgame.cpp:732-790</c>), the two steps at the
/// start of <c>humanTurn()</c> (<c>:1039-1043</c>) and the route the interface lays on a click.
/// </summary>
/// <remarks>
/// In the <see cref="SettingsSingletonCollection.Name"/> collection because starting a game fills the global
/// <see cref="Players"/> table and the human colours are chosen with the game type of the moment, both of which
/// are process wide state.
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AdventureSessionTests
{
    private const int MapSize = 36;

    /// <summary>Blue hero, the human of the tests, on row 1, far from the edges.</summary>
    private const int BlueHeroTile = 37;

    /// <summary>Green hero, the computer of the tests.</summary>
    private const int GreenHeroTile = 38;

    /// <summary>Red hero, the second human of the tests that need one.</summary>
    private const int RedHeroTile = 39;

    /// <summary>Empty grass east of the blue hero, the destination of the route tests.</summary>
    private const int TargetTile = 40;

    /// <summary>
    /// A hero tile: the <c>MINIHERO</c> sprite of <paramref name="spriteIndex"/> refers to information
    /// block <paramref name="blockId"/>. The sprite index encodes the color and the race, seven sprites per
    /// color in the order of <see cref="PlayerColors.Ordered"/> (0 blue knight, 7 green knight, 14 red knight).
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

    /// <summary>A hero information block without a custom portrait, so the pool hands out the race of the tile.</summary>
    private static byte[] HeroBlock()
    {
        byte[] data = new byte[Mp2HeroInfo.StructureSize];

        data[17] = 1;

        return data;
    }

    /// <summary>
    /// A map with one hero per color of <paramref name="colors"/>, the header narrowed down to the humans and
    /// the computer of <paramref name="computer"/>.
    /// </summary>
    private static Mp2Map MapWithHeroes(PlayerColor humans, PlayerColor computer)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(BlueHeroTile, HeroTile(blockId: 1, spriteIndex: 0));
        map.SetTileAt(GreenHeroTile, HeroTile(blockId: 2, spriteIndex: 7));
        map.SetTileAt(RedHeroTile, HeroTile(blockId: 3, spriteIndex: 14));

        return map with
        {
            InfoBlocks =
            [
                new Mp2InfoBlock(0, HeroBlock()),
                new Mp2InfoBlock(1, HeroBlock()),
                new Mp2InfoBlock(2, HeroBlock()),
            ],
            Info = map.Info with
            {
                KingdomColors = PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red,
                HumanPlayers = humans,
                ComputerPlayers = computer,
            },
        };
    }

    /// <summary>
    /// Loads the map of <see cref="MapWithHeroes"/> into a fresh world, with the settings put into
    /// <paramref name="gameType"/> first.
    /// </summary>
    /// <remarks>
    /// The game type has to be set before the map is loaded: the loader picks the human colours of the map
    /// (<c>World.Kingdoms.cs</c>), and a hot-seat game keeps every human-only colour, while a single-player game
    /// keeps just the first one.
    /// </remarks>
    /// <param name="humans">The colours a person may play.</param>
    /// <param name="computer">The colours the computer may play.</param>
    /// <param name="gameType">The kind of game about to be started.</param>
    /// <returns>The world and the header the session is started with.</returns>
    private static (World World, Mp2FileInfo Info) Load(
        PlayerColor humans,
        PlayerColor computer,
        GameTypes gameType = GameTypes.Standard)
    {
        Settings.Reset();
        Settings.Get().SetGameType(gameType);

        Mp2Map map = MapWithHeroes(humans, computer);
        World world = new();
        world.LoadMap(map);

        return (world, map.Info);
    }

    /// <summary>
    /// Lifts the fog of <paramref name="color"/> from the whole map: the pathfinder only searches explored
    /// tiles (<c>WorldPathfinder</c>), exactly as the reference's does.
    /// </summary>
    private static void Reveal(World world, PlayerColor color)
    {
        for (int index = 0; index < world.Size; ++index)
        {
            world.GetTile(index).ClearFog(color);
        }
    }

    [Fact]
    public void StartPutsTheGameOnDayOneWithTheHumanHeroInFocus()
    {
        (World world, Mp2FileInfo info) = Load(PlayerColor.Blue, PlayerColor.Green | PlayerColor.Red);

        AdventureSession? session = AdventureSession.Start(world, info);

        Assert.NotNull(session);
        Assert.Equal(1u, session.DayCount);
        Assert.Equal(PlayerColor.Blue, session.CurrentColor);
        Assert.Equal([PlayerColor.Blue], session.Players);

        // BeginTurn picks the first hero of the acting kingdom and gives him the movement points of the day.
        Hero hero = Assert.IsType<Hero>(session.FocusHero);

        Assert.Equal(PlayerColor.Blue, hero.Color);
        Assert.Equal(BlueHeroTile, hero.MapIndex);
        Assert.True(hero.MovePoints > 0);

        // The map loader now stamps the answer of Heroes::isControlHuman() on every hero.
        Assert.True(hero.IsControlHuman);
        Assert.False(world.Kingdoms.GetKingdom(PlayerColor.Green).Heroes[0].IsControlHuman);
    }

    [Fact]
    public void EndTurnOfTheOnlyHumanStartsTheNextDay()
    {
        (World world, Mp2FileInfo info) = Load(PlayerColor.Blue, PlayerColor.Green | PlayerColor.Red);
        AdventureSession session = AdventureSession.Start(world, info)!;
        Hero hero = session.FocusHero!;

        // The hero spends most of his points, so the new day has something to restore.
        hero.SetMovePoints(1);
        session.EndTurn();

        Assert.Equal(2u, session.DayCount);
        Assert.Equal(PlayerColor.Blue, session.CurrentColor);
        Assert.Equal(hero, session.FocusHero);
        Assert.Equal(hero.GetMaxMovePoints(), hero.MovePoints);
    }

    [Fact]
    public void EndTurnHandsTheMapToTheNextHumanAndSkipsTheComputer()
    {
        // Two people at one keyboard: only a hot-seat game keeps both human-only colours.
        (World world, Mp2FileInfo info) = Load(
            PlayerColor.Blue | PlayerColor.Red,
            PlayerColor.Green,
            GameTypes.HotSeat);

        AdventureSession session = AdventureSession.Start(world, info)!;

        Assert.Equal(PlayerColor.Blue, session.CurrentColor);

        // Green is the computer: it is never the current player, the turn goes from blue to red.
        session.EndTurn();

        Assert.Equal(PlayerColor.Red, session.CurrentColor);
        Assert.Equal(1u, session.DayCount);
        Assert.Equal(RedHeroTile, session.FocusHero!.MapIndex);

        // Red ends the last turn of the day, so the date moves on and the map is back at blue.
        session.EndTurn();

        Assert.Equal(2u, session.DayCount);
        Assert.Equal(PlayerColor.Blue, session.CurrentColor);
        Assert.Equal(BlueHeroTile, session.FocusHero!.MapIndex);
    }

    [Fact]
    public void SetPathLaysTheRouteOfAnOwnHero()
    {
        (World world, Mp2FileInfo info) = Load(PlayerColor.Blue, PlayerColor.Green);
        Reveal(world, PlayerColor.Blue);

        AdventureSession session = AdventureSession.Start(world, info)!;
        Hero hero = session.FocusHero!;

        Assert.True(session.SetPath(hero, TargetTile));
        Assert.Equal(TargetTile, hero.Path.DestinationIndex);
        Assert.True(hero.IsMoveEnabled);

        // A hero of another kingdom is not routed: the interface only drives the focus hero.
        Hero green = world.Kingdoms.GetKingdom(PlayerColor.Green).Heroes[0];

        Assert.False(session.SetPath(green, TargetTile));

        // A tile outside the map is not a destination either.
        Assert.False(session.SetPath(hero, world.Size));
    }

    [Fact]
    public void TheRouteIsRecalculatedWhenTheTurnComesBack()
    {
        (World world, Mp2FileInfo info) = Load(
            PlayerColor.Blue | PlayerColor.Red,
            PlayerColor.Green,
            GameTypes.HotSeat);

        Reveal(world, PlayerColor.Blue);

        AdventureSession session = AdventureSession.Start(world, info)!;
        Hero hero = session.FocusHero!;

        Assert.True(session.SetPath(hero, TargetTile));

        // Blue to red, then red to blue: the second transition starts a new day, and Kingdom::ActionBeforeTurn
        // walks the hero list again, which is where Heroes::calculatePath() keeps the destination.
        session.EndTurn();
        session.EndTurn();

        Assert.Equal(2u, session.DayCount);
        Assert.Same(hero, session.FocusHero);
        Assert.Equal(TargetTile, hero.Path.DestinationIndex);
    }

    [Fact]
    public void AMapWithoutAHumanPlayerHasNoSession()
    {
        (World world, Mp2FileInfo info) = Load(
            PlayerColor.None,
            PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red);

        // A map only meant for the computer (or for the editor) opens as a viewer: no day, no turn.
        Assert.Null(AdventureSession.Start(world, info));
        Assert.Equal(0u, world.DayCount);
    }
}
