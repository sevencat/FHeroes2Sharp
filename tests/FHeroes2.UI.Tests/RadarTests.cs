using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="Radar"/>, <c>Interface::Radar</c> (<c>gui/interface_radar.cpp:142-499</c>).
/// </summary>
public sealed class RadarTests
{
    [Fact]
    public void Build_SizesTheZoomForTheMap()
    {
        Radar radar = CreateRadar();

        radar.Build(WorldWith(72));
        Assert.Equal(2.0, radar.Zoom);
        Assert.Equal(new Rect(0, 0, 72, 72), radar.RenderArea);

        radar.Build(WorldWith(144));
        Assert.Equal(1.0, radar.Zoom);

        radar.Build(WorldWith(36));
        Assert.Equal(4.0, radar.Zoom);
    }

    [Fact]
    public void Build_RejectsANullWorld()
    {
        Radar radar = CreateRadar();

        Assert.Throws<ArgumentNullException>(() => radar.Build(null!));
    }

    [Fact]
    public void SetRedraw_QueuesOnlyTheRadarBits()
    {
        StubInterface stub = new(new PixelBuffer(800, 600));
        Radar radar = stub.GetRadar();

        radar.SetRedraw(Redraw.Radar);
        Assert.Equal(Redraw.Radar, stub.GetRedrawMask());

        radar.SetRedraw(Redraw.RadarCursor);
        Assert.Equal(Redraw.Radar | Redraw.RadarCursor, stub.GetRedrawMask());

        Assert.Throws<ArgumentException>(() => radar.SetRedraw(Redraw.Buttons));
    }

    [Fact]
    public void SetRenderArea_ClipsToTheWorld()
    {
        Radar radar = CreateRadar();
        radar.Build(WorldWith(72));

        radar.SetRenderArea(new Rect(-5, -5, 20, 20));
        Assert.Equal(new Rect(0, 0, 15, 15), radar.RenderArea);

        radar.SetRenderArea(new Rect(60, 60, 40, 40));
        Assert.Equal(new Rect(60, 60, 12, 12), radar.RenderArea);
    }

    [Fact]
    public void SetRenderArea_IsDroppedWhileTheFoldedRadarIsOffScreen()
    {
        Radar radar = CreateRadar();
        radar.Build(WorldWith(72));
        radar.HideInterfaceEnabled = true;
        radar.ShowRadar = false;

        radar.SetRenderArea(new Rect(4, 4, 8, 8));

        Assert.Equal(new Rect(0, 0, 72, 72), radar.RenderArea);
    }

    [Fact]
    public void RedrawObjects_PaintsNothingWhileEverythingIsFogged()
    {
        Radar radar = CreateRadar();
        radar.Build(WorldWith(72));

        radar.RedrawObjects(PlayerColor.None, ViewWorldMode.OnlyVisible);

        Assert.All(radar.MapImage.Pixels, pixel => Assert.Equal(Radar.BlackColor, pixel));
    }

    [Fact]
    public void RedrawObjects_ScalesEveryTileToTheZoom()
    {
        Radar radar = CreateRadar();
        World world = WorldWith(72);
        radar.Build(world);

        radar.RedrawObjects(PlayerColor.None, ViewWorldMode.ViewAll);

        // Two pixels per tile on a 72x72 map, and a fresh tile carries neither a road nor a town, so every block of
        // the image is the plain ground colour of its tile.
        foreach (Point tile in new[] { new Point(0, 0), new Point(5, 9), new Point(70, 71) })
        {
            byte expected = Radar.GetPaletteIndexOfGround(world.GetTile(tile.X, tile.Y).Ground);

            Assert.Equal(expected, radar.MapImage[tile.X * 2, tile.Y * 2]);
            Assert.Equal(expected, radar.MapImage[(tile.X * 2) + 1, (tile.Y * 2) + 1]);
        }
    }

    [Fact]
    public void RedrawRadar_ShowsTheLogoWhileTheRadarIsFolded()
    {
        PixelBuffer display = new(800, 600);
        display.Fill(0);

        Sprite logo = new(new PixelBuffer(Radar.PanelSize, Radar.PanelSize));
        logo.Image.Fill(7);

        StubInterface stub = new(display);
        Radar radar = new(stub, display, logo);
        radar.Build(WorldWith(72));
        radar.SetPos(100, 50);
        radar.SetHide(true);

        radar.RedrawRadar(redrawMapObjects: true, PlayerColor.None);

        Assert.Equal(7, display[100, 50]);
        Assert.Equal(7, display[100 + Radar.PanelSize - 1, 50 + Radar.PanelSize - 1]);
        Assert.Equal(0, display[99, 50]);
    }

    [Fact]
    public void RedrawRadar_PaintsNothingWhileTheRadarIsHiddenAway()
    {
        PixelBuffer display = new(800, 600);
        display.Fill(0);

        Sprite logo = new(new PixelBuffer(Radar.PanelSize, Radar.PanelSize));
        logo.Image.Fill(7);

        StubInterface stub = new(display);
        Radar radar = new(stub, display, logo);
        radar.Build(WorldWith(72));
        radar.SetPos(100, 50);
        radar.SetHide(true);
        radar.HideInterfaceEnabled = true;
        radar.ShowRadar = false;

        radar.RedrawRadar(redrawMapObjects: true, PlayerColor.None);

        Assert.All(display.Pixels, pixel => Assert.Equal(0, pixel));
        Assert.Equal(new Rect(0, 0, 72, 72), radar.RenderArea);
    }

    [Fact]
    public void RedrawCursor_OutlinesTheVisibleRectangle()
    {
        PixelBuffer display = new(800, 600);
        display.Fill(0);

        StubInterface stub = new(display);
        Radar radar = stub.GetRadar();
        radar.Build(WorldWith(72));
        radar.SetPos(10, 10);

        radar.RedrawCursor(new Rect(20, 10, 20, 15));

        MovableSprite cursor = Assert.IsType<MovableSprite>(radar.CursorArea);

        // 20x15 tiles at two pixels each, placed at the scaled corner of the rectangle.
        Assert.Equal(40, cursor.Sprite!.Width);
        Assert.Equal(30, cursor.Sprite.Height);
        Assert.Equal(10 + 40, cursor.X);
        Assert.Equal(10 + 20, cursor.Y);

        // The frame is dotted with RADARCOLOR over the map that was not repainted.
        bool painted = false;

        for (int y = cursor.Y; y < cursor.Y + 30 && !painted; ++y)
        {
            for (int x = cursor.X; x < cursor.X + 40 && !painted; ++x)
            {
                painted = display[x, y] == Radar.RadarColor;
            }
        }

        Assert.True(painted, "The cursor frame is missing from the frame buffer.");
    }

    [Fact]
    public void RedrawCursor_HidesTheCursorOnAWholeMapView()
    {
        Radar radar = CreateRadar();
        radar.Build(WorldWith(72));

        radar.RedrawCursor(new Rect(0, 0, 80, 80));

        Assert.Null(radar.CursorArea);
    }

    [Fact]
    public void RedrawCursor_AsksTheGameAreaWithoutAnExplicitRectangle()
    {
        StubInterface stub = new(new PixelBuffer(800, 600));
        Radar radar = stub.GetRadar();
        radar.Build(WorldWith(72));

        stub.GetGameArea().Generate(new Size(800, 600), withoutBorders: false);

        radar.RedrawCursor();

        // The viewport shows less than the whole map, so the radar has a frame to draw.
        Assert.NotNull(radar.CursorArea);
    }

    [Fact]
    public void QueueEventProcessing_ReportsARightPressForTheTooltip()
    {
        Radar radar = CreateRadar();
        radar.Build(WorldWith(72));
        radar.SetPos(100, 50);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Right, new Point(150, 100));

        Assert.Equal(RadarAction.ShowTooltip, radar.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_IgnoresAClickOutsideTheRadar()
    {
        Radar radar = CreateRadar();
        radar.Build(WorldWith(72));
        radar.SetPos(100, 50);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));

        Assert.Equal(RadarAction.None, radar.QueueEventProcessing(events));
    }

    [Theory]
    [InlineData(PlayerColor.Blue, Radar.BlueColor)]
    [InlineData(PlayerColor.Green, Radar.GreenColor)]
    [InlineData(PlayerColor.Red, Radar.RedColor)]
    [InlineData(PlayerColor.Yellow, Radar.YellowColor)]
    [InlineData(PlayerColor.Orange, Radar.OrangeColor)]
    [InlineData(PlayerColor.Purple, Radar.PurpleColor)]
    [InlineData(PlayerColor.None, Radar.WhiteColor)]
    [InlineData(PlayerColor.Blue | PlayerColor.Red, Radar.WhiteColor)]
    public void GetPaletteIndexOfColor_MapsEveryKingdom(PlayerColor color, byte expected)
    {
        Assert.Equal(expected, Radar.GetPaletteIndexOfColor(color));
    }

    [Fact]
    public void GetPaletteIndexOfGround_MapsEveryGround()
    {
        Assert.Equal(Radar.GrassColor, Radar.GetPaletteIndexOfGround(MapGround.Grass));
        Assert.Equal(Radar.WaterColor, Radar.GetPaletteIndexOfGround(MapGround.Water));
        Assert.Equal(Radar.DesertColor, Radar.GetPaletteIndexOfGround(MapGround.Desert));
        Assert.Equal(Radar.SnowColor, Radar.GetPaletteIndexOfGround(MapGround.Snow));
        Assert.Equal(Radar.SwampColor, Radar.GetPaletteIndexOfGround(MapGround.Swamp));
        Assert.Equal(Radar.WastelandColor, Radar.GetPaletteIndexOfGround(MapGround.Wasteland));
        Assert.Equal(Radar.BeachColor, Radar.GetPaletteIndexOfGround(MapGround.Beach));
        Assert.Equal(Radar.LavaColor, Radar.GetPaletteIndexOfGround(MapGround.Lava));
        Assert.Equal(Radar.DirtColor, Radar.GetPaletteIndexOfGround(MapGround.Dirt));
        Assert.Equal(Radar.BlackColor, Radar.GetPaletteIndexOfGround((MapGround)0xFF));
    }

    [Fact]
    public void SavePosition_StoresTheFloatingPosition()
    {
        Radar radar = CreateRadar();

        radar.HideInterfaceEnabled = true;
        radar.SetPos(30, 40);
        radar.SavePosition();

        Assert.Equal(new Point(30, 40), FHeroes2.Game.System.Settings.Get().PosRadar);
    }

    [Fact]
    public void Create_WorksWithoutAnArchive()
    {
        StubInterface stub = new(new PixelBuffer(800, 600));

        Radar radar = Radar.Create(stub, new PixelBuffer(800, 600), null, false);

        Assert.Null(radar.World);

        // Without a logo the folded radar paints nothing, but it still accepts the call.
        radar.Build(WorldWith(72));
        radar.SetHide(true);
        radar.RedrawRadar(redrawMapObjects: true, PlayerColor.None);
    }

    [Fact]
    public void Create_ReadsTheLogoFromTheArchive()
    {
        if (AssetLocator.Resolve() is not { } directory)
        {
            return;
        }

        using AssetBundle? assets = AssetBundle.Open(directory);
        if (assets is null)
        {
            return;
        }

        StubInterface stub = new(new PixelBuffer(800, 600));
        Radar radar = Radar.Create(stub, new PixelBuffer(800, 600), assets, false);

        radar.Build(WorldWith(72));
        radar.SetHide(true);

        PixelBuffer display = new(800, 600);
        display.Fill(0);

        radar.SetPos(0, 0);
        radar.RedrawRadar(redrawMapObjects: false, PlayerColor.None);
    }

    private static Radar CreateRadar()
    {
        StubInterface stub = new(new PixelBuffer(800, 600));

        return stub.GetRadar();
    }

    private static World WorldWith(int size)
    {
        World world = new();
        world.GenerateUninitializedMap(size);

        return world;
    }

    /// <summary>An interface that does nothing, which is all the radar needs of its owner in these tests.</summary>
    private sealed class StubInterface : BaseInterface
    {
        public StubInterface(PixelBuffer display)
            : base(isEditor: false, display)
        {
        }

        public override void RedrawInterface(Redraw force)
        {
        }

        public override void MouseCursorAreaClickLeft(int tileIndex)
        {
        }

        public override void MouseCursorAreaPressRight(int tileIndex)
        {
        }

        public override void MouseCursorAreaLongPressLeft(int tileIndex)
        {
        }

        public override void Reset()
        {
        }

        public override void UpdateCursor(int tileIndex)
        {
        }
    }
}
