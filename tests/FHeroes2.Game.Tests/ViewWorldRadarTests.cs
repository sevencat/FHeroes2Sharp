using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The radar of the View World window, <c>Radar::RedrawObjects</c> and
/// <c>Radar::QueueEventProcessingForWorldView</c> of <c>interface_radar.cpp:283-534</c>.
/// </summary>
public sealed class ViewWorldRadarTests
{
    private const int MapSize = 18;

    [Fact]
    public void ANullWorldIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new ViewWorldRadar(null!));

    [Fact]
    public void EveryTileBecomesFourPixelsOfThePanel()
    {
        World world = LandWorld(36);
        var radar = new ViewWorldRadar(world);

        radar.RedrawObjects(PlayerColor.None, ViewWorldMode.ViewAll);

        Assert.Equal(Minimap.PanelSize, radar.Image.Width);
        Assert.Equal(Minimap.PanelSize, radar.Image.Height);
        Assert.Equal(Minimap.GrassColor, radar.ColorAt(0, 0));
        Assert.Equal(Minimap.GrassColor, radar.Image[0, 0]);
        Assert.Equal(Minimap.GrassColor, radar.Image[143, 143]);
    }

    [Fact]
    public void ViewAllSeesThroughTheFog()
    {
        World world = LandWorld(MapSize);
        var radar = new ViewWorldRadar(world);

        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.ViewAll);

        Assert.Equal(Minimap.GrassColor, radar.ColorAt(0, 0));
    }

    [Fact]
    public void OnlyVisibleLeavesFoggedTilesBlack()
    {
        World world = LandWorld(MapSize);
        var radar = new ViewWorldRadar(world);

        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.OnlyVisible);

        Assert.Equal(Minimap.BlackColor, radar.ColorAt(0, 0));
    }

    [Fact]
    public void ARevealedTileShowsItsGround()
    {
        World world = LandWorld(MapSize);
        world.GetTile(2, 2).ClearFog(PlayerColor.Blue);

        var radar = new ViewWorldRadar(world);
        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.OnlyVisible);

        Assert.Equal(Minimap.GrassColor, radar.ColorAt(2, 2));
        Assert.Equal(Minimap.BlackColor, radar.ColorAt(3, 2));
    }

    [Fact]
    public void MountainsAndTreesAreThreeShadesDarkerThanTheGround()
    {
        World world = LandWorld(MapSize);
        world.GetTile(1, 1).SetMainObjectType(Mp2ObjectType.Mountains);
        world.GetTile(2, 2).SetMainObjectType(Mp2ObjectType.Trees);

        var radar = new ViewWorldRadar(world);
        radar.RedrawObjects(PlayerColor.None, ViewWorldMode.ViewAll);

        Assert.Equal((byte)(Minimap.GrassColor + 3), radar.ColorAt(1, 1));
        Assert.Equal((byte)(Minimap.GrassColor + 3), radar.ColorAt(2, 2));
        Assert.Equal(Minimap.GrassColor, radar.ColorAt(4, 4));
    }

    [Fact]
    public void AMineShowsTheColourOfItsOwner()
    {
        World world = LandWorld(MapSize);
        Tile mine = world.GetTile(3, 3);
        mine.SetMainObjectType(Mp2ObjectType.ActionMine);
        world.CaptureObject(mine.Index, PlayerColor.Red);

        var radar = new ViewWorldRadar(world);
        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.ViewMines);

        Assert.Equal(Minimap.RedColor, radar.ColorAt(3, 3));
    }

    [Fact]
    public void AFoggedMineIsOnlyShownByTheMineMode()
    {
        World world = LandWorld(MapSize);
        Tile mine = world.GetTile(3, 3);
        mine.SetMainObjectType(Mp2ObjectType.ActionMine);
        world.CaptureObject(mine.Index, PlayerColor.Red);

        var radar = new ViewWorldRadar(world);

        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.OnlyVisible);
        Assert.Equal(Minimap.BlackColor, radar.ColorAt(3, 3));

        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.ViewTowns);
        Assert.Equal(Minimap.BlackColor, radar.ColorAt(3, 3));
    }

    [Fact]
    public void ArtifactsAndResourcesAreGray()
    {
        World world = LandWorld(MapSize);
        world.GetTile(1, 2).SetMainObjectType(Mp2ObjectType.ActionArtifact);

        Tile resource = world.GetTile(2, 3);
        resource.SetMainObjectType(Mp2ObjectType.ActionResource);
        resource.SetResourceOnTile(Resource.Gold, 500);

        var radar = new ViewWorldRadar(world);
        radar.RedrawObjects(PlayerColor.None, ViewWorldMode.ViewAll);

        Assert.Equal(Minimap.GrayColor, radar.ColorAt(1, 2));
        Assert.Equal(Minimap.GrayColor, radar.ColorAt(2, 3));
    }

    [Fact]
    public void AHeroIsShownInTheColourOfItsKingdom()
    {
        World world = LandWorld(MapSize);
        Tile tile = world.GetTile(4, 5);
        Hero hero = world.GetHero(12)!;
        hero.SetColor(PlayerColor.Green);
        tile.SetHero(world, hero);
        hero.SetMapIndex(tile.Index);

        var radar = new ViewWorldRadar(world);

        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.OnlyVisible);
        Assert.Equal(Minimap.BlackColor, radar.ColorAt(4, 5));

        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.ViewHeroes);
        Assert.Equal(Minimap.GreenColor, radar.ColorAt(4, 5));
    }

    [Fact]
    public void TownsAreShownByTheTownModeEvenUnderTheFog()
    {
        World world = WorldWithTown();
        var radar = new ViewWorldRadar(world);

        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.OnlyVisible);
        Assert.Equal(Minimap.BlackColor, radar.ColorAt(18, 18));

        // The map file gives the town no owner, which is the white seventh colour.
        radar.RedrawObjects(PlayerColor.Blue, ViewWorldMode.ViewTowns);
        Assert.Equal(Minimap.WhiteColor, radar.ColorAt(18, 18));
        Assert.Equal(Minimap.WhiteColor, radar.ColorAt(16, 16));
    }

    [Fact]
    public void TheFrameDisappearsWhenTheWholeMapFits()
    {
        ViewWorldZoom zoom = ZoomOnWholeMap();
        var radar = new ViewWorldRadar(LandWorld(MapSize));

        Assert.True(radar.CursorArea(zoom.RoiInTiles).IsEmpty);
    }

    [Fact]
    public void TheFrameIsTheVisibleRegionScaledToThePanel()
    {
        ViewWorldZoom zoom = ZoomOnHalfOfTheMap();
        var radar = new ViewWorldRadar(LandWorld(MapSize));

        // The region is 8 x 8 tiles out of 18 starting at (5, 5), so the frame is 64 x 64 pixels from 40.
        Assert.Equal(new Rect(40, 40, 64, 64), radar.CursorArea(zoom.RoiInTiles));
    }

    [Fact]
    public void DrawPaintsThePanelAtTheOriginAndFramesIt()
    {
        ViewWorldZoom zoom = ZoomOnHalfOfTheMap();
        World world = LandWorld(MapSize);
        var radar = new ViewWorldRadar(world);
        radar.RedrawObjects(PlayerColor.None, ViewWorldMode.ViewAll);

        var target = new PixelBuffer(300, 300);
        target.Fill(9);

        radar.Draw(target, new Point(10, 10), zoom.RoiInTiles);

        Assert.Equal(Minimap.GrassColor, target[10, 10]);
        Assert.Equal(Minimap.GrassColor, target[10 + 143, 10 + 143]);
        Assert.Equal(Minimap.CursorColor, target[10 + 40, 10 + 40]);
        Assert.Equal(9, target[0, 0]);
    }

    [Fact]
    public void ClickingThePanelCentresTheViewOnThatTile()
    {
        ViewWorldZoom zoom = ZoomOnHalfOfTheMap();
        var radar = new ViewWorldRadar(LandWorld(MapSize));

        // Panel point (36, 36) of an 18 tile map is tile (4, 4); its pixel centre is 112, which the view clamps.
        Assert.True(radar.CenterOn(zoom, new Point(36, 36)));
        Assert.Equal(new Point(133, 133), zoom.Center);
        Assert.Equal(0, zoom.RoiInPixels.X);
    }

    [Fact]
    public void ClickingTheSameTileTwiceChangesNothing()
    {
        ViewWorldZoom zoom = ZoomOnHalfOfTheMap();
        var radar = new ViewWorldRadar(LandWorld(MapSize));

        Assert.True(radar.CenterOn(zoom, new Point(36, 36)));
        Assert.False(radar.CenterOn(zoom, new Point(36, 36)));
    }

    /// <summary>An all grass world of the given size, every tile knowing its index.</summary>
    /// <param name="size">Width and height in tiles.</param>
    /// <returns>The world.</returns>
    private static World LandWorld(int size)
    {
        var world = new World();
        world.GenerateUninitializedMap(size);

        for (int i = 0; i < world.Size; ++i)
        {
            Tile tile = world.Tiles[i];
            tile.Index = i;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        world.Heroes.Init(MapVersion.SuccessionWars);

        return world;
    }

    /// <summary>A 36 x 36 grass map with one town, loaded the way the game loads a map file.</summary>
    /// <returns>The world.</returns>
    private static World WorldWithTown()
    {
        const int townX = 18;
        const int townY = 18;

        Mp2Map map = Mp2Map.CreateBlank(36, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(map.TileIndex(townX, townY), map.Tile(townX, townY) with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnTwrd << 2),
            BottomIcnImageIndex = 0,
            Quantity1 = (byte)((1 << 3) | 1),
            TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            ObjectType = Mp2ObjectType.Castle,
        });

        map = map with { Castles = [new Mp2CastlePosition(townX, townY, 0, MapRace.Knight)] };

        var world = new World();
        world.LoadMap(map);

        return world;
    }

    /// <summary>A view of the whole 18 tile map at 1:4, i.e. one that needs no frame.</summary>
    /// <returns>The view.</returns>
    private static ViewWorldZoom ZoomOnWholeMap() =>
        new(ZoomLevel.ZoomLevel0, new Point(0, 0), new Rect(0, 0, 400, 300),
            ViewWorldZoomLevels.RegularZoomLevels, MapSize, MapSize);

    /// <summary>A 100 x 100 window at 1:12 on an 18 tile map, i.e. the middle 8 x 8 tiles.</summary>
    /// <returns>The view.</returns>
    private static ViewWorldZoom ZoomOnHalfOfTheMap() =>
        new(ZoomLevel.ZoomLevel2, new Point(288, 288), new Rect(0, 0, 100, 100),
            ViewWorldZoomLevels.RegularZoomLevels, MapSize, MapSize);
}
