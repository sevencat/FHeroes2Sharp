using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>src/fheroes2/heroes/heroes_move.cpp</c> as ported by <c>Hero.Movement.cs</c>: the nine frames of
/// <c>_moveStep()</c>, the turning block of <c>_angleStep()</c>, <c>getCurrentPixelOffset()</c>, the frame loop
/// <c>Move()</c>, and the small queries around them.
/// </summary>
/// <remarks>
/// The hero stands on tile 12 of a 5 × 5 grassland and walks right to 13. Tile indexes are assigned by hand
/// because <see cref="World.GenerateUninitializedMap"/> leaves them at zero on purpose. The four calls into
/// subsystems that do not exist yet — <c>Scout</c>, <c>ScoutRadar</c>, <c>Action</c>,
/// <c>ActionNewPosition</c> — are recorded by <see cref="RecordingHandler"/>, which is what the
/// <see cref="IHeroMovementHandler"/> seam is for.
/// </remarks>
public sealed class HeroMovementTests
{
    private const int MapSize = 5;
    private const int HeroTile = 12;
    private const int NextTile = 13;

    [Fact]
    public void TheFirstFrameOnlyAdvancesTheSprite()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);

        Assert.Equal(18, hero.SpriteIndex);
        Assert.False(hero.MoveStep(false, world));

        Assert.Equal(19, hero.SpriteIndex);
        Assert.Equal(HeroTile, hero.MapIndex);
        Assert.Equal(1, hero.Path.Count);
        Assert.Empty(handler.Scouted);
        Assert.Empty(handler.Acted);
        Assert.Equal(0, handler.ActionNewPositionCount);
    }

    [Fact]
    public void TheSecondFrameClearsTheFogAhead()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);
        hero.SpriteIndex = 19;

        Assert.False(hero.MoveStep(false, world));

        // The tile the hero is about to enter, not the one he stands on.
        Assert.Equal([NextTile], handler.Scouted);
        Assert.Equal(20, hero.SpriteIndex);
    }

    [Fact]
    public void TheMiddleFramesOnlyAdvanceTheSprite()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);
        hero.SetMovePoints(1000);
        hero.SpriteIndex = 20;

        for (int frame = 2; frame < 8; ++frame)
        {
            Assert.False(hero.MoveStep(false, world));
            Assert.Equal(18 + frame + 1, hero.SpriteIndex);
        }

        Assert.Empty(handler.Scouted);
        Assert.Empty(handler.Acted);
        Assert.Equal(1000u, hero.MovePoints);
        Assert.Equal(HeroTile, hero.MapIndex);
    }

    [Fact]
    public void TheLastFrameMovesTheHero()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right, 100)]);
        hero.SetMovePoints(1000);
        hero.SpriteIndex = 26;

        Assert.True(hero.MoveStep(false, world));

        // The sprite jumps back eight frames, i.e. to the standing pose of the new tile.
        Assert.Equal(18, hero.SpriteIndex);
        Assert.Equal(NextTile, hero.MapIndex);
        Assert.Same(hero, world.GetTile(NextTile).GetHero(world));
        Assert.Null(world.GetTile(HeroTile).GetHero(world));
        Assert.True(hero.Path.IsEmpty);
        Assert.Equal(900u, hero.MovePoints);
        Assert.Equal(1, handler.ScoutRadarCount);
        Assert.Equal(1, handler.ActionNewPositionCount);
        Assert.Equal([NextTile], handler.Acted);
    }

    [Fact]
    public void AnAiHeroDoesNotRedrawTheMinimap()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        hero.IsControlHuman = false;
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);
        hero.SpriteIndex = 26;

        Assert.True(hero.MoveStep(false, world));

        Assert.Equal(0, handler.ScoutRadarCount);
        Assert.Equal(1, handler.ActionNewPositionCount);
    }

    [Fact]
    public void AHeroWithoutAKingdomHasNobodyToAct()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        hero.SetColor(PlayerColor.None);
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);
        hero.SpriteIndex = 26;

        Assert.True(hero.MoveStep(false, world));

        // ActionNewPosition() still runs — the hero did step somewhere — but the object action does not.
        Assert.Equal(1, handler.ActionNewPositionCount);
        Assert.Empty(handler.Acted);
    }

    [Fact]
    public void AnObjectTheHeroCannotStepOntoStopsHimInFront()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        world.GetTile(NextTile).SetMainObjectType(Mp2ObjectType.ActionMonster);
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right, 100)]);
        hero.SetMovePoints(1000);

        Assert.True(hero.MoveStep(false, world));

        // Frame 0 takes the step in the bookkeeping sense — the points are paid, the route ends — but the
        // hero stays where he is and the object gets the action.
        Assert.Equal(HeroTile, hero.MapIndex);
        Assert.Equal(18, hero.SpriteIndex);
        Assert.True(hero.Path.IsEmpty);
        Assert.Equal(900u, hero.MovePoints);
        Assert.Equal([NextTile], handler.Acted);
        Assert.Empty(handler.Scouted);
        Assert.Equal(0, handler.ActionNewPositionCount);
    }

    [Fact]
    public void JumpToNextTileTakesTheWholeStepAtOnce()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right, 100)]);
        hero.SetMovePoints(1000);

        Assert.True(hero.MoveStep(true, world));

        Assert.Equal(NextTile, hero.MapIndex);
        Assert.Equal(900u, hero.MovePoints);
        Assert.Equal([NextTile], handler.Scouted);
        Assert.Equal([NextTile], handler.Acted);
    }

    [Fact]
    public void JumpToNextTileStillStopsInFrontOfTheObject()
    {
        (World world, Hero hero) = CreatePlacedHero();
        var handler = new RecordingHandler();
        hero.MovementHandler = handler;
        world.GetTile(NextTile).SetMainObjectType(Mp2ObjectType.ActionMonster);
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);
        hero.SpriteIndex = 22;

        Assert.True(hero.MoveStep(true, world));

        Assert.Equal(HeroTile, hero.MapIndex);
        Assert.Equal(22, hero.SpriteIndex);
        Assert.Equal([NextTile], handler.Acted);
        Assert.Empty(handler.Scouted);
    }

    [Fact]
    public void TheStandingFrameIsSkippedWhenTheRouteGoesStraightOn()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath(
        [
            Step(HeroTile, NextTile, Direction.Right),
            Step(NextTile, NextTile + 1, Direction.Right),
        ]);
        hero.SetMove(true);
        hero.SpriteIndex = 26;

        Assert.True(hero.MoveStep(false, world));

        // Frame 1 of the new tile: the walker never pauses between two tiles of a straight line.
        Assert.Equal(NextTile, hero.MapIndex);
        Assert.Equal(19, hero.SpriteIndex);
        Assert.Equal(1, hero.Path.Count);
    }

    [Fact]
    public void TheStandingFrameIsNotSkippedWhenTheRouteTurns()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath(
        [
            Step(HeroTile, NextTile, Direction.Right),
            Step(NextTile, NextTile + MapSize, Direction.Bottom),
        ]);
        hero.SetMove(true);
        hero.SpriteIndex = 26;

        Assert.True(hero.MoveStep(false, world));

        Assert.Equal(18, hero.SpriteIndex);
    }

    [Fact]
    public void TheStandingFrameIsNotSkippedWhenMovementIsOff()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath(
        [
            Step(HeroTile, NextTile, Direction.Right),
            Step(NextTile, NextTile + 1, Direction.Right),
        ]);
        hero.SpriteIndex = 26;

        Assert.True(hero.MoveStep(false, world));

        Assert.Equal(18, hero.SpriteIndex);
    }

    [Fact]
    public void TheStandingFrameIsNotSkippedInFrontOfAnObject()
    {
        (World world, Hero hero) = CreatePlacedHero();
        world.GetTile(NextTile + 1).SetMainObjectType(Mp2ObjectType.ActionMonster);
        hero.Path.SetPath(
        [
            Step(HeroTile, NextTile, Direction.Right),
            Step(NextTile, NextTile + 1, Direction.Right),
        ]);
        hero.SetMove(true);
        hero.SpriteIndex = 26;

        Assert.True(hero.MoveStep(false, world));

        Assert.Equal(18, hero.SpriteIndex);
    }

    [Fact]
    public void ATurnWalksTheBlockAndLandsOnTheNewStandingFrame()
    {
        var hero = new Hero(Hero.LordKilburn, MapRace.Knight)
        {
            Direction = Direction.Bottom,
            SpriteIndex = 36,
        };

        // Bottom to Right goes the short way round through BottomRight: the entry/exit frames of the turning
        // block are 50..53, and the block for BottomRight starts at 27.
        Assert.Equal(53, TurnOnce(hero, Direction.Right));
        Assert.Equal(52, TurnOnce(hero, Direction.Right));
        Assert.Equal(51, TurnOnce(hero, Direction.Right));
        Assert.Equal(27, TurnOnce(hero, Direction.Right));
        Assert.Equal(Direction.BottomRight, hero.Direction);
        Assert.Equal(50, TurnOnce(hero, Direction.Right));
        Assert.Equal(49, TurnOnce(hero, Direction.Right));
        Assert.Equal(18, TurnOnce(hero, Direction.Right));

        Assert.Equal(18, hero.SpriteIndex);
        Assert.Equal(Direction.Right, hero.Direction);
    }

    [Fact]
    public void ATurnTheOtherWayEntersTheOtherEndOfTheBlock()
    {
        var hero = new Hero(Hero.LordKilburn, MapRace.Knight)
        {
            Direction = Direction.Bottom,
            SpriteIndex = 36,
        };

        Assert.Equal(52, TurnOnce(hero, Direction.Left));
        Assert.Equal(51, TurnOnce(hero, Direction.Left));
        Assert.Equal(27, TurnOnce(hero, Direction.Left));
        Assert.Equal(Direction.BottomLeft, hero.Direction);
        Assert.Equal(50, TurnOnce(hero, Direction.Left));
        Assert.Equal(49, TurnOnce(hero, Direction.Left));
        Assert.Equal(18, TurnOnce(hero, Direction.Left));

        Assert.Equal(18, hero.SpriteIndex);
        Assert.Equal(Direction.Left, hero.Direction);
    }

    [Fact]
    public void AFasterAnimationSkipsMoreFramesOfTheTurn()
    {
        var hero = new Hero(Hero.LordKilburn, MapRace.Knight)
        {
            Direction = Direction.Bottom,
            SpriteIndex = 36,
        };

        // framesToSkip = clamp( 4 / speedMultiplier, 1, 3 ): at the base speed only every third frame counts.
        hero.AngleStep(Direction.Right);
        hero.AngleStep(Direction.Right);
        Assert.Equal(36, hero.SpriteIndex);

        hero.AngleStep(Direction.Right);
        Assert.Equal(53, hero.SpriteIndex);

        var fast = new Hero(Hero.LordKilburn, MapRace.Knight)
        {
            Direction = Direction.Bottom,
            SpriteIndex = 36,
            AnimationSpeedMultiplier = 4,
        };

        fast.AngleStep(Direction.Right);
        Assert.Equal(53, fast.SpriteIndex);
    }

    [Fact]
    public void ThePixelOffsetCoversTheFramesBetweenTwoTiles()
    {
        var hero = new Hero(Hero.LordKilburn, MapRace.Knight) { Direction = Direction.Bottom };

        // Frames 0 and 1 of a tile stand on the tile itself.
        hero.SpriteIndex = 18;
        Assert.Equal(new Point(0, 0), hero.GetCurrentPixelOffset());

        hero.SpriteIndex = 19;
        Assert.Equal(new Point(0, 0), hero.GetCurrentPixelOffset());

        hero.SpriteIndex = 20;
        Assert.Equal(new Point(0, 4), hero.GetCurrentPixelOffset());

        hero.SpriteIndex = 26;
        Assert.Equal(new Point(0, 28), hero.GetCurrentPixelOffset());
    }

    [Fact]
    public void ThePixelOffsetFollowsTheDirection()
    {
        var hero = new Hero(Hero.LordKilburn, MapRace.Knight)
        {
            Direction = Direction.Right,
            SpriteIndex = 24,
        };

        // Frame 6 walks five steps of four pixels: the sprite is one step behind the frame count.
        Assert.Equal(new Point(20, 0), hero.GetCurrentPixelOffset());

        hero.Direction = Direction.TopLeft;
        hero.SpriteIndex = 21;
        Assert.Equal(new Point(-8, -8), hero.GetCurrentPixelOffset());
    }

    [Fact]
    public void ThePixelOffsetIsZeroWhileTurning()
    {
        var hero = new Hero(Hero.LordKilburn, MapRace.Knight) { SpriteIndex = 47 };

        Assert.Equal(new Point(0, 0), hero.GetCurrentPixelOffset());
    }

    [Fact]
    public void ThePixelOffsetIsRelativeToTheTileOffset()
    {
        var hero = new Hero(Hero.LordKilburn, MapRace.Knight)
        {
            Direction = Direction.Bottom,
            Offset = new Point(2, 3),
        };

        hero.SpriteIndex = 18;
        Assert.Equal(new Point(2, 3), hero.GetCurrentPixelOffset());

        hero.SpriteIndex = 20;
        Assert.Equal(new Point(2, 7), hero.GetCurrentPixelOffset());
    }

    [Fact]
    public void MoveStopsWhenTheRouteIsEmpty()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.SetMovePoints(1000);

        // SetMove() cannot even turn the flag on without a route, and Move() turns it off again.
        hero.SetMove(true);
        Assert.False(hero.IsMoveEnabled);
        Assert.False(hero.Move(false, world));
        Assert.False(hero.IsMoveEnabled);
    }

    [Fact]
    public void MoveStopsWhenTheRoutePointsOffTheMap()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath([new RouteStep(MapSize * MapSize + 3, HeroTile, Direction.Bottom, 100)]);
        hero.SetMovePoints(1000);
        hero.SetMove(true);

        Assert.False(hero.Move(false, world));
        Assert.False(hero.IsMoveEnabled);
    }

    [Fact]
    public void MoveTurnsTheHeroBeforeWalking()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Direction = Direction.Top;
        hero.AnimationSpeedMultiplier = 4;
        hero.Path.SetPath([Step(HeroTile, HeroTile + 1, Direction.Right)]);
        hero.SetMove(true);

        // Top to Right is one step clockwise: Top block → TopRight block → Right block.
        int turns = 0;
        while (hero.Direction != Direction.Right)
        {
            Assert.False(hero.Move(false, world));
            Assert.True(++turns < 20);
        }

        Assert.Equal(6, turns);
        Assert.Equal(Direction.Right, hero.Direction);
        Assert.Equal(18, hero.SpriteIndex);
        Assert.Equal(HeroTile, hero.MapIndex);

        // Facing the right way now, the next frame starts the walk.
        Assert.False(hero.Move(false, world));
        Assert.Equal(19, hero.SpriteIndex);
    }

    [Fact]
    public void MoveFinishesAStepThatWasAlreadyUnderWay()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right, 100)]);
        hero.SpriteIndex = 20;

        // The last movement point of the day can arrive in the middle of a tile; the sprite would be stuck
        // between two tiles if the flag stopped him here.
        Assert.False(hero.Move(false, world));
        Assert.Equal(21, hero.SpriteIndex);
    }

    [Fact]
    public void MoveDoesNothingOnTheStandingFrameWhenTheFlagIsOff()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right, 100)]);
        hero.SpriteIndex = 18;

        Assert.False(hero.Move(false, world));
        Assert.Equal(18, hero.SpriteIndex);
        Assert.Equal(HeroTile, hero.MapIndex);
    }

    [Fact]
    public void MoveClearsTheActionMode()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right, 100)]);
        hero.SetModes(HeroModes.Action);

        hero.Move(false, world);

        Assert.False(hero.Modes.HasFlag(HeroModes.Action));
    }

    [Fact]
    public void MoveWithJumpToNextTileArrivesImmediately()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Direction = Direction.Top;
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right, 100)]);
        hero.SetMove(true);

        Assert.True(hero.Move(true, world));

        Assert.Equal(NextTile, hero.MapIndex);
        Assert.Equal(Direction.Right, hero.Direction);
    }

    [Fact]
    public void TheMovementDirectionOnlyCoversTheWalkingFrames()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);

        // Frames 0 and 8 stand on the tile, and the frame at 27 belongs to the next block.
        hero.SpriteIndex = 19;
        Assert.Equal(new Point(0, 0), hero.MovementDirection(world));

        hero.SpriteIndex = 20;
        Assert.Equal(new Point(1, 0), hero.MovementDirection(world));

        hero.SpriteIndex = 26;
        Assert.Equal(new Point(1, 0), hero.MovementDirection(world));

        hero.SpriteIndex = 27;
        Assert.Equal(new Point(0, 0), hero.MovementDirection(world));
    }

    [Fact]
    public void TheMovementDirectionIsZeroWithoutARoute()
    {
        (World world, Hero hero) = CreatePlacedHero();

        // No route: FrontDirection is unknown, which GetDirectionIndex answers with -1.
        Assert.Equal(new Point(0, 0), hero.MovementDirection(world));

        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);
        hero.SpriteIndex = 24;
        Assert.Equal(new Point(1, 0), hero.MovementDirection(world));
    }

    [Fact]
    public void TheMovementDirectionIsZeroForAHeroOffTheMap()
    {
        (World world, _) = CreatePlacedHero();
        var tavernHero = new Hero(Hero.Celia, MapRace.Knight);
        tavernHero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);

        Assert.Equal(Hero.NoPosition, tavernHero.MapIndex);
        Assert.Equal(new Point(0, 0), tavernHero.MovementDirection(world));
    }

    [Fact]
    public void SetValidDirectionSpriteRepairsAFrameFromAnotherDirection()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);

        hero.Direction = Direction.Right;
        hero.SpriteIndex = 18;
        hero.SetValidDirectionSprite(world);
        Assert.Equal(18, hero.SpriteIndex);

        hero.Direction = Direction.Top;
        hero.SpriteIndex = 18;
        hero.SetValidDirectionSprite(world);
        Assert.Equal(0, hero.SpriteIndex);

        hero.Direction = Direction.BottomLeft;
        hero.SpriteIndex = 45;
        hero.SetValidDirectionSprite(world);
        Assert.Equal(27, hero.SpriteIndex);
    }

    [Fact]
    public void SetValidDirectionSpriteDoesNothingWithoutATarget()
    {
        (World world, Hero hero) = CreatePlacedHero();
        hero.Direction = Direction.Right;
        hero.SpriteIndex = 45;

        // No route: there is no direction block to pick.
        hero.SetValidDirectionSprite(world);
        Assert.Equal(45, hero.SpriteIndex);

        hero.Path.SetPath([Step(HeroTile, NextTile, Direction.Right)]);
        hero.Direction = Direction.Unknown;
        hero.SetValidDirectionSprite(world);
        Assert.Equal(45, hero.SpriteIndex);
    }

    [Fact]
    public void SetValidDirectionSpriteDoesNothingForAHeroOffTheMap()
    {
        (World world, _) = CreatePlacedHero();
        var tavernHero = new Hero(Hero.Celia, MapRace.Knight)
        {
            SpriteIndex = 45,
            Direction = Direction.Bottom,
        };
        tavernHero.Path.SetPath([Step(HeroTile, NextTile, Direction.Bottom)]);

        tavernHero.SetValidDirectionSprite(world);

        Assert.Equal(45, tavernHero.SpriteIndex);
    }

    [Fact]
    public void TheObjectTableDecidesWhetherTheHeroMustStayInFront()
    {
        (World world, Hero hero) = CreatePlacedHero();

        Assert.False(hero.IsNeedStayFrontObject(world, NextTile));

        world.GetTile(NextTile).SetMainObjectType(Mp2ObjectType.ActionMonster);
        Assert.True(hero.IsNeedStayFrontObject(world, NextTile));
    }

    [Fact]
    public void MovingToTheSameTileChangesNothing()
    {
        (World world, Hero hero) = CreatePlacedHero();

        hero.MoveToDest(HeroTile, world);

        Assert.Equal(HeroTile, hero.MapIndex);
        Assert.Same(hero, world.GetTile(HeroTile).GetHero(world));
    }

    [Fact]
    public void MovingToAnotherTileMovesTheOccupancy()
    {
        (World world, Hero hero) = CreatePlacedHero();

        hero.MoveToDest(NextTile, world);

        Assert.Equal(NextTile, hero.MapIndex);
        Assert.Null(world.GetTile(HeroTile).GetHero(world));
        Assert.Same(hero, world.GetTile(NextTile).GetHero(world));
    }

    [Fact]
    public void LandInSightIsNotTheDeepOcean()
    {
        (World world, Hero hero) = CreatePlacedHero(CreateWorld(0));
        hero.Direction = Direction.Bottom;
        hero.SpriteIndex = 22;

        Assert.True(hero.IsInDeepOcean(world));

        // Tile 22 is a neighbour of 17 but not of 12, so it only counts once the hero has walked past the
        // middle of his tile.
        world.GetTile(HeroTile + 2 * MapSize).SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        Assert.True(hero.IsInDeepOcean(world));

        hero.SpriteIndex = 23;
        Assert.False(hero.IsInDeepOcean(world));
    }

    private static int TurnOnce(Hero hero, Direction target)
    {
        hero.AnimationSpeedMultiplier = 4;
        hero.AngleStep(target);

        return hero.SpriteIndex;
    }

    private static RouteStep Step(int from, int index, Direction direction, uint penalty = 100) =>
        new(index, from, direction, penalty);

    private static (World World, Hero Hero) CreatePlacedHero() =>
        CreatePlacedHero(CreateWorld(MapGrounds.GrassStartImageIndex));

    private static (World World, Hero Hero) CreatePlacedHero(World world)
    {
        Hero hero = world.GetHero(Hero.LordKilburn)!;
        hero.SetColor(PlayerColor.Blue);
        hero.SetMapIndex(HeroTile);
        world.GetTile(HeroTile).SetHero(world, hero);

        return (world, hero);
    }

    private static World CreateWorld(ushort terrainImageIndex)
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

        for (int i = 0; i < world.Size; ++i)
        {
            Tile tile = world.Tiles[i];
            tile.Index = i;
            tile.SetTerrain(terrainImageIndex, 0);
        }

        world.Heroes.Init(MapVersion.SuccessionWars);

        return world;
    }

    /// <summary>
    /// Records the four calls into subsystems the port does not have.
    /// </summary>
    private sealed class RecordingHandler : IHeroMovementHandler
    {
        public List<int> Scouted { get; } = [];

        public List<int> Acted { get; } = [];

        public int ActionNewPositionCount { get; private set; }

        public int ScoutRadarCount { get; private set; }

        public void Scout(Hero hero, int tileIndex) => Scouted.Add(tileIndex);

        public void Action(Hero hero, int tileIndex) => Acted.Add(tileIndex);

        public void ActionNewPosition(Hero hero) => ++ActionNewPositionCount;

        public void ScoutRadar(Hero hero) => ++ScoutRadarCount;
    }
}
