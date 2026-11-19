using FHeroes2.Assets.Icn;
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
/// The pixel half of the View World window: <c>DrawWorld()</c> of <c>view_world.cpp:270-315</c>,
/// <c>GetSpriteResource()</c> of <c>:462-480</c> and <c>DrawObjectsIcons()</c> of <c>:317-460</c>.
/// </summary>
public sealed class ViewWorldRendererTests
{
    private const int MapSize = 18;

    /// <summary>The palette index every fake icon is filled with.</summary>
    private const byte IconPixel = 42;

    /// <summary>Palette index the tests fill the screen with before drawing, so "untouched" is visible.</summary>
    private const byte ScreenPixel = 9;

    [Theory]
    [InlineData(ViewWorldMode.ViewAll, false, IcnType.ViewAll)]
    [InlineData(ViewWorldMode.ViewAll, true, IcnType.EviwAll)]
    [InlineData(ViewWorldMode.ViewArtifacts, false, IcnType.Viewrtfx)]
    [InlineData(ViewWorldMode.ViewArtifacts, true, IcnType.Eviwrtfx)]
    [InlineData(ViewWorldMode.ViewMines, false, IcnType.Viewmine)]
    [InlineData(ViewWorldMode.ViewMines, true, IcnType.Eviwmine)]
    [InlineData(ViewWorldMode.ViewResources, false, IcnType.Viewrsrc)]
    [InlineData(ViewWorldMode.ViewResources, true, IcnType.Eviwrsrc)]
    [InlineData(ViewWorldMode.ViewHeroes, false, IcnType.Viewhros)]
    [InlineData(ViewWorldMode.ViewHeroes, true, IcnType.Eviwhros)]
    [InlineData(ViewWorldMode.ViewTowns, false, IcnType.Viewtwns)]
    [InlineData(ViewWorldMode.ViewTowns, true, IcnType.Eviwtwns)]
    [InlineData(ViewWorldMode.OnlyVisible, false, IcnType.Viewwrld)]
    [InlineData(ViewWorldMode.OnlyVisible, true, IcnType.Eviwwrld)]
    public void TheSidePanelOfAModeComesFromItsArchive(ViewWorldMode mode, bool evil, IcnType expected) =>
        Assert.Equal(expected, ViewWorldRenderer.GetSpriteResource(mode, evil));

    [Fact]
    public void AViewWithAnotherNumberOfLevelsThanTheCacheIsRejected()
    {
        ViewWorldMapCache cache = EmptyCache(ViewWorldMode.ViewAll);
        ViewWorldZoom zoom = new(ZoomLevel.ZoomLevel3, new Point(0, 0), new Rect(0, 0, 100, 100),
            ViewWorldZoomLevels.TotalZoomLevels, MapSize, MapSize);

        Assert.Throws<ArgumentException>(
            () => ViewWorldRenderer.DrawWorld(zoom, cache, new PixelBuffer(276, 132), new Rect(0, 0, 100, 100)));
    }

    [Fact]
    public void AMapSmallerThanTheWindowIsCentredAndFramedInBlack()
    {
        // 18 x 18 tiles at 1:4 is 72 x 72 pixels inside a 200 x 150 window.
        ViewWorldMapCache cache = new(ViewWorldMode.ViewAll, MapSize, MapSize, ViewWorldZoomLevels.RegularZoomLevels,
            (block, _, _) => block.Fill(5));

        var display = new PixelBuffer(376, 182);
        display.Fill(ScreenPixel);

        ViewWorldZoom zoom = new(ZoomLevel.ZoomLevel0, new Point(1000, 1000), new Rect(0, 0, 200, 150),
            ViewWorldZoomLevels.RegularZoomLevels, MapSize, MapSize);

        ViewWorldRenderer.DrawWorld(zoom, cache, display, new Rect(0, 0, 200, 150));

        // The window is wider than the map, so the centre is pinned on the map and the left bar is 64 pixels.
        Assert.Equal(5, display[80, 55]);
        Assert.Equal(5, display[151, 126]);
        Assert.Equal(0, display[79, 55]);
        Assert.Equal(0, display[152, 55]);
        Assert.Equal(0, display[80, 54]);
        Assert.Equal(0, display[80, 127]);

        // Outside the window nothing is painted: the frame corner and the radar strip keep what was there.
        Assert.Equal(ScreenPixel, display[0, 0]);
        Assert.Equal(ScreenPixel, display[216, 20]);
    }

    [Fact]
    public void AMapLargerThanTheWindowIsCutToTheVisibleSlice()
    {
        // 18 x 18 tiles at 1:12 is 216 x 216 pixels inside a 100 x 100 window, so the slice starts at 58 of it.
        // Bands of 64 map pixels, i.e. of 24 cached pixels, make the alignment visible.
        ViewWorldMapCache cache = new(ViewWorldMode.ViewAll, MapSize, MapSize, ViewWorldZoomLevels.RegularZoomLevels,
            (block, _, _) => PaintBands(block));

        var display = new PixelBuffer(276, 132);
        display.Fill(ScreenPixel);

        ViewWorldZoom zoom = new(ZoomLevel.ZoomLevel2, new Point(288, 288), new Rect(0, 0, 100, 100),
            ViewWorldZoomLevels.RegularZoomLevels, MapSize, MapSize);

        ViewWorldRenderer.DrawWorld(zoom, cache, display, new Rect(0, 0, 100, 100));

        // Band three starts at 58, band five at 98: the slice leaves out the two bands before it.
        Assert.Equal(3, display[16, 16]);
        Assert.Equal(5, display[56, 16]);

        // The image is larger than the window in both directions, so no bar is filled.
        Assert.Equal(ScreenPixel, display[0, 0]);
        Assert.Equal(ScreenPixel, display[116, 116]);
    }

    [Fact]
    public void AHeroIsDrawnWithTheSeventhTerrainIndexPlusItsColor()
    {
        World world = LandWorld();
        Tile tile = world.GetTile(3, 4);
        Hero hero = world.GetHero(12)!;
        hero.SetColor(PlayerColor.Green);
        tile.SetHero(world, hero);
        hero.SetMapIndex(tile.Index);

        ViewWorldMapCache cache = EmptyCache(ViewWorldMode.ViewAll);
        var sprites = new RecordingSprites();

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.ViewAll, cache, world, sprites);

        // Green is offset one, so the icon is the eighth one of every terrain archive.
        Assert.Contains((IcnType.Misc4, 8), sprites.Requests);
        Assert.Contains((IcnType.Misc6, 8), sprites.Requests);
        Assert.Contains((IcnType.Misc12, 8), sprites.Requests);

        // 2 x 2 icon on the centre of tile (3, 4) at 1:4.
        Assert.Equal(IconPixel, cache.GetImage(ZoomLevel.ZoomLevel0)[13, 17]);
    }

    [Fact]
    public void FogHidesAHeroUnlessTheModeRevealsIt()
    {
        World world = LandWorld();
        Tile tile = world.GetTile(3, 4);
        Hero hero = world.GetHero(12)!;
        hero.SetColor(PlayerColor.Red);
        tile.SetHero(world, hero);
        hero.SetMapIndex(tile.Index);

        ViewWorldMapCache cache = EmptyCache(ViewWorldMode.ViewAll);
        var sprites = new RecordingSprites();

        // A fresh tile is under the fog of every player, so the only reveal mode is the hero one.
        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.OnlyVisible, cache, world, sprites);
        Assert.Empty(sprites.Requests);

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.ViewHeroes, cache, world, sprites);
        Assert.Contains((IcnType.Misc12, 9), sprites.Requests);
    }

    [Fact]
    public void AMineDrawsItsIncomeResourceAndTheLetter()
    {
        World world = LandWorld();
        Tile tile = world.GetTile(2, 3);
        tile.SetMainObjectType(Mp2ObjectType.ActionMine);
        tile.SetResourceOnTile(Resource.Ore, 2);

        ViewWorldMapCache cache = EmptyCache(ViewWorldMode.ViewMines);
        var sprites = new RecordingSprites();

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.ViewMines, cache, world, sprites);

        // An unowned mine is the seventh colour, and ore is the third letter.
        Assert.Contains((IcnType.Misc4, 6), sprites.Requests);
        Assert.Contains((IcnType.Letter4, 2), sprites.Requests);
    }

    [Fact]
    public void AResourceTileDrawsItsResourceAndTheThirteenthIcon()
    {
        World world = LandWorld();
        Tile tile = world.GetTile(5, 6);
        tile.SetMainObjectType(Mp2ObjectType.ActionResource);
        tile.SetResourceOnTile(Resource.Gold, 500);

        ViewWorldMapCache cache = EmptyCache(ViewWorldMode.ViewResources);
        var sprites = new RecordingSprites();

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.ViewResources, cache, world, sprites);

        Assert.Contains((IcnType.Misc4, 13), sprites.Requests);
        Assert.Contains((IcnType.Letter4, 6), sprites.Requests);
    }

    [Fact]
    public void ObjectsWithNoResourceInTheTileDataDrawNoIcon()
    {
        // 多格矿场与资源堆只有一格带资源覆盖图，参考给整个对象的所有格子写同一份元数据；端口按格读
        // MP2，邻格的 `metadata` 全是 0，渲染器必须当「没有资源」跳过，而不是断言崩掉或画五份图标。
        World world = LandWorld();
        world.GetTile(2, 3).SetMainObjectType(Mp2ObjectType.ActionMine);
        world.GetTile(5, 6).SetMainObjectType(Mp2ObjectType.ActionResource);

        ViewWorldMapCache cache = EmptyCache(ViewWorldMode.ViewAll);
        var sprites = new RecordingSprites();

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.ViewAll, cache, world, sprites);

        Assert.Empty(sprites.Requests);
    }

    [Fact]
    public void AnArtifactUsesTheFourteenthIcon()
    {
        World world = LandWorld();
        world.GetTile(1, 1).SetMainObjectType(Mp2ObjectType.ActionArtifact);

        ViewWorldMapCache cache = EmptyCache(ViewWorldMode.ViewArtifacts);
        var sprites = new RecordingSprites();

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.ViewArtifacts, cache, world, sprites);

        Assert.Contains((IcnType.Misc4, 14), sprites.Requests);
    }

    [Fact]
    public void ATownEntranceDrawsTwoFlagsOfItsOwnersColor()
    {
        World world = WorldWithTown();
        ViewWorldMapCache cache = new(ViewWorldMode.ViewTowns, 36, 36, ViewWorldZoomLevels.RegularZoomLevels,
            (_, _, _) => { });
        var sprites = new RecordingSprites();

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.Blue, ViewWorldMode.ViewTowns, cache, world, sprites);

        // The town has no owner in the map file, so its flags are the neutral seventh ones of every archive.
        Assert.Contains((IcnType.Vwflag4, 6), sprites.Requests);
        Assert.Contains((IcnType.Vwflag12, 6), sprites.Requests);

        // At 1:4 the entrance is at (72, 72), the two 2 x 2 flags sit one tile to either side and one pixel down.
        PixelBuffer level0 = cache.GetImage(ZoomLevel.ZoomLevel0);
        Assert.Equal(IconPixel, level0[77, 74]);
        Assert.Equal(IconPixel, level0[69, 74]);
    }

    /// <summary>An 18 x 18 grass world whose tiles know their index, the smallest map the cache accepts.</summary>
    /// <returns>The world.</returns>
    private static World LandWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

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

    /// <summary>A cache whose blocks stay black, i.e. only the icons are painted on it.</summary>
    /// <param name="mode">The display mode the cache is built for.</param>
    /// <returns>The cache.</returns>
    private static ViewWorldMapCache EmptyCache(ViewWorldMode mode) =>
        new(mode, MapSize, MapSize, ViewWorldZoomLevels.RegularZoomLevels, (_, _, _) => { });

    /// <summary>Fills a block with vertical bands 64 pixels wide, numbered from one.</summary>
    /// <param name="block">The block to paint.</param>
    private static void PaintBands(PixelBuffer block)
    {
        for (int y = 0; y < block.Height; ++y)
        {
            for (int x = 0; x < block.Width; ++x)
            {
                block[x, y] = (byte)(1 + (x / 64));
            }
        }
    }

    /// <summary>Hands out a 2 x 2 opaque icon for every request and records what was asked for.</summary>
    private sealed class RecordingSprites : IViewWorldSpriteSource
    {
        /// <summary>Every (archive, index) pair the renderer asked for, in order.</summary>
        public List<(IcnType Icn, int Index)> Requests { get; } = [];

        /// <summary>Records the request and returns a small icon of <see cref="IconPixel"/>.</summary>
        /// <param name="icn">Archive asked for.</param>
        /// <param name="index">Sprite index inside the archive.</param>
        /// <returns>The icon.</returns>
        public Sprite? GetSprite(IcnType icn, int index)
        {
            Requests.Add((icn, index));

            var sprite = new Sprite(2, 2);
            sprite.Image.Fill(IconPixel);

            return sprite;
        }
    }
}
