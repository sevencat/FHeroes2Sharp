using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The window around the View World map, <c>ViewWorld::ViewWorldWindow()</c> of <c>view_world.cpp:589-795</c>.
/// </summary>
public sealed class ViewWorldWindowTests
{
    private const int MapSize = 72;
    private const int DisplayWidth = 640;
    private const int DisplayHeight = 480;

    private static readonly Rect VisibleArea = new(16, 16, 432, 448);
    private static readonly Rect AdventureRoi = new(30, 30, 12, 12);

    [Fact]
    public void ANullSpriteSourceIsRejected()
    {
        World world = LandWorld(MapSize);

        Assert.Throws<ArgumentNullException>(() => new ViewWorldWindow(
            world,
            ViewWorldMode.ViewAll,
            PlayerColor.None,
            ZoomLevel.ZoomLevel2,
            VisibleArea,
            AdventureRoi,
            new ViewWorldMapCache(ViewWorldMode.ViewAll, world.Width, world.Height, ViewWorldZoomLevels.RegularZoomLevels, (_, _, _) => { }),
            DisplayWidth,
            DisplayHeight,
            null!));
    }

    [Fact]
    public void TheRadarSitsInTheTopRightCorner()
    {
        ViewWorldWindow window = Open();

        Assert.Equal(new Rect(DisplayWidth - 16 - 144, 16, 144, 144), window.RadarArea);
    }

    [Fact]
    public void TheButtonsSitUnderTheRadar()
    {
        ViewWorldWindow window = Open(sprites: ButtonSprites());

        Assert.Equal(new Rect(512, 304, 96, 30), window.ZoomButtonArea);
        Assert.Equal(new Rect(512, 412, 96, 30), window.ExitButtonArea);
    }

    [Fact]
    public void ButtonsWithoutArtworkTakeNoClicks()
    {
        ViewWorldWindow window = Open();

        Assert.True(window.ZoomButtonArea.IsEmpty);
        Assert.False(window.IsInZoomButton(new Point(512, 304)));
    }

    [Fact]
    public void TheFadeAreaLeavesTheRadarAndTheBorderAlone()
    {
        ViewWorldWindow window = Open();

        // 640 - 3 * 16 - 144 = 448 wide, 480 - 2 * 16 = 448 high, from (16, 16).
        Assert.Equal(new Rect(16, 16, 448, 448), window.FadeArea);
    }

    [Fact]
    public void AHiddenInterfaceFadesTheWholeScreen()
    {
        ViewWorldWindow window = Open(isHideInterface: true);

        Assert.Equal(new Rect(0, 0, DisplayWidth, DisplayHeight), window.FadeArea);
    }

    [Fact]
    public void TheViewStartsOnTheAdventureMapCentre()
    {
        ViewWorldWindow window = Open();

        // 30 * 32 + (432 + 32) / 2 and 30 * 32 + (448 + 32) / 2.
        Assert.Equal(new Point(1192, 1200), window.Zoom.Center);
    }

    [Fact]
    public void AWholeMapThatFitsIsCentredOnTheMap()
    {
        World world = LandWorld(36);
        ViewWorldWindow window = Open(world, zoomLevel: ZoomLevel.ZoomLevel0);

        // 36 tiles at 1:4 are 144 pixels, which fit the window, so the centre is the middle of the map.
        // The map is 1152 pixels wide and the clamp of the reference asks for (1152 - 1) / 2.
        Assert.Equal(new Point(575, 575), window.Zoom.Center);
    }

    [Fact]
    public void ADragMovesTheViewBackwards()
    {
        ViewWorldWindow window = Open();
        Point before = window.Zoom.Center;

        Assert.True(window.Press(new Point(200, 200)));
        Assert.True(window.IsDragging);

        // 32 screen pixels at 1:12 are 32 * 32 / 12 = 85 map pixels.
        Assert.True(window.DragTo(new Point(232, 200)));
        Assert.Equal(new Point(before.X - 85, before.Y), window.Zoom.Center);
    }

    [Fact]
    public void ADragWithoutAPressDoesNothing()
    {
        ViewWorldWindow window = Open();

        Assert.False(window.DragTo(new Point(232, 200)));
        Assert.Equal(new Point(1192, 1200), window.Zoom.Center);
    }

    [Fact]
    public void LeavingTheWindowEndsTheDrag()
    {
        ViewWorldWindow window = Open();

        Assert.True(window.Press(new Point(200, 200)));
        Assert.False(window.DragTo(new Point(1, 1)));
        Assert.False(window.IsDragging);
        Assert.False(window.DragTo(new Point(232, 200)));
    }

    [Fact]
    public void APressOutsideTheWindowDoesNotStartADrag()
    {
        ViewWorldWindow window = Open();

        Assert.False(window.Press(new Point(1, 1)));
        Assert.False(window.IsDragging);
    }

    [Fact]
    public void TheWheelZoomsWithoutCycling()
    {
        ViewWorldWindow window = Open(zoomLevel: ZoomLevel.ZoomLevel1);

        Assert.True(window.WheelUp());
        Assert.Equal(ZoomLevel.ZoomLevel2, window.Zoom.ZoomLevel);

        // The third level is the last one, so one more notch finds nowhere to go.
        Assert.False(window.WheelUp());

        Assert.True(window.WheelDown());
        Assert.Equal(ZoomLevel.ZoomLevel1, window.Zoom.ZoomLevel);
    }

    [Fact]
    public void TheZoomButtonCyclesThroughTheLevels()
    {
        ViewWorldWindow window = Open(zoomLevel: ZoomLevel.ZoomLevel0);

        Assert.True(window.ClickZoom());
        Assert.Equal(ZoomLevel.ZoomLevel2, window.Zoom.ZoomLevel);
        Assert.True(window.ClickZoom());
        Assert.Equal(ZoomLevel.ZoomLevel1, window.Zoom.ZoomLevel);
    }

    [Fact]
    public void AClickOnTheRadarCentresTheViewOnThatTile()
    {
        ViewWorldWindow window = Open();
        Rect radar = window.RadarArea;

        // Panel pixel (72, 72) of a 72 tile map is tile (36, 36), whose pixel centre is 1136.
        Assert.True(window.ClickRadar(new Point(radar.X + 72, radar.Y + 72)));
        Assert.Equal(new Point(1136, 1136), window.Zoom.Center);
    }

    [Fact]
    public void AClickOutsideTheRadarLeavesTheViewAlone()
    {
        ViewWorldWindow window = Open();

        Assert.False(window.ClickRadar(new Point(200, 200)));
        Assert.Equal(new Point(1192, 1200), window.Zoom.Center);
    }

    [Fact]
    public void ClosingAnswersTheZoomLevelToRemember()
    {
        ViewWorldWindow window = Open(zoomLevel: ZoomLevel.ZoomLevel1);
        Assert.True(window.Press(new Point(200, 200)));

        Assert.True(window.WheelUp());
        Assert.Equal(ZoomLevel.ZoomLevel2, window.Close());
        Assert.False(window.IsDragging);
    }

    [Fact]
    public void ALevelTheSmallerTableCannotHoldIsClamped()
    {
        ViewWorldWindow window = Open(zoomLevel: ZoomLevel.ZoomLevel3);

        Assert.Equal(ZoomLevel.ZoomLevel2, window.Zoom.ZoomLevel);
    }

    [Fact]
    public void TheSidePanelIsFilledWithStoneOnATallScreen()
    {
        const int tall = 800;
        ViewWorldWindow window = Open(
            displayHeight: tall,
            sprites: ButtonSprites()
                .Add(IcnType.ViewAll, 0, 96, 446, 5)
                .Add(IcnType.Stonback, 0, 96, 60, 6));

        var display = new PixelBuffer(DisplayWidth, tall);
        display.Fill(9);

        window.DrawChrome(display);

        // The panel starts at x = 640 - 96 - 16 = 528 and y = 32 + 144 = 176.
        Assert.Equal(5, display[528, 176]);
        Assert.Equal(5, display[528, 176 + 274]);

        // 800 - 16 - 451 - 171 = 162 rows of stone: three full repeats and a 24 row tail.
        Assert.Equal(6, display[528, 176 + 275]);
        Assert.Equal(6, display[528, 176 + 275 + 161]);
        Assert.Equal(5, display[528, 176 + 275 + 162]);
        Assert.Equal(9, display[0, 0]);
    }

    [Fact]
    public void TheButtonsSwitchArtworkWhileHeld()
    {
        ViewWorldWindow window = Open(sprites: ButtonSprites());
        var display = new PixelBuffer(DisplayWidth, DisplayHeight);

        window.DrawChrome(display);
        Assert.Equal(3, display[window.ZoomButtonArea.X, window.ZoomButtonArea.Y]);
        Assert.Equal(3, display[window.ExitButtonArea.X, window.ExitButtonArea.Y]);

        window.DrawChrome(display, zoomPressed: true, exitPressed: true);
        Assert.Equal(4, display[window.ZoomButtonArea.X, window.ZoomButtonArea.Y]);
        Assert.Equal(4, display[window.ExitButtonArea.X, window.ExitButtonArea.Y]);
    }

    [Fact]
    public void TheRadarIsDrawnAtItsOwnCorner()
    {
        ViewWorldWindow window = Open();
        var display = new PixelBuffer(DisplayWidth, DisplayHeight);
        display.Fill(9);

        window.DrawRadar(display, renderMapObjects: false);

        Assert.Equal(window.Radar.ColorAt(0, 0), display[window.RadarArea.X, window.RadarArea.Y]);
        Assert.Equal(9, display[window.RadarArea.X - 1, window.RadarArea.Y]);
    }

    /// <summary>Opens the window on a grass map, with the fake sprites of the side panel when asked.</summary>
    /// <param name="world">The world to show; a 72 tile grass map by default.</param>
    /// <param name="zoomLevel">The remembered zoom level.</param>
    /// <param name="displayHeight">Height of the screen.</param>
    /// <param name="sprites">The sprite source.</param>
    /// <param name="isHideInterface">Whether the adventure map hides its interface.</param>
    /// <returns>The window.</returns>
    private static ViewWorldWindow Open(
        World? world = null,
        ZoomLevel zoomLevel = ZoomLevel.ZoomLevel2,
        int displayHeight = DisplayHeight,
        IViewWorldSpriteSource? sprites = null,
        bool isHideInterface = false)
    {
        world ??= LandWorld(MapSize);
        sprites ??= new FakeSprites();

        return new ViewWorldWindow(
            world,
            ViewWorldMode.ViewAll,
            PlayerColor.None,
            zoomLevel,
            VisibleArea,
            AdventureRoi,
            new ViewWorldMapCache(ViewWorldMode.ViewAll, world.Width, world.Height, ViewWorldZoomLevels.RegularZoomLevels, (_, _, _) => { }),
            DisplayWidth,
            displayHeight,
            sprites!,
            isEvilInterface: false,
            isHideInterface);
    }

    /// <summary>A sprite source with the two buttons only, both 96 x 30, normal index colour 3 and pressed 4.</summary>
    /// <returns>The source.</returns>
    private static FakeSprites ButtonSprites() =>
        new FakeSprites()
            .Add(IcnType.Lgndxtra, 0, 96, 30, 3)
            .Add(IcnType.Lgndxtra, 1, 96, 30, 4)
            .Add(IcnType.ButtonViewworldExitGood, 0, 96, 30, 3)
            .Add(IcnType.ButtonViewworldExitGood, 1, 96, 30, 4);

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

    /// <summary>A sprite source backed by solid coloured sprites, so a blit is visible as one palette index.</summary>
    private sealed class FakeSprites : IViewWorldSpriteSource
    {
        private readonly Dictionary<(IcnType Icn, int Index), Sprite> _sprites = [];

        public FakeSprites Add(IcnType icn, int index, int width, int height, byte color)
        {
            var image = new PixelBuffer(width, height);
            image.Fill(color);
            _sprites[(icn, index)] = new Sprite(image);

            return this;
        }

        public Sprite? GetSprite(IcnType icn, int index) =>
            _sprites.TryGetValue((icn, index), out Sprite? sprite) ? sprite : null;
    }
}
