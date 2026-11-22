using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The modal loop of the View World window, <c>ViewWorld::ViewWorldWindow()</c>
/// (<c>src/fheroes2/kingdom/view_world.cpp:700-795</c>).
/// </summary>
public sealed class ViewWorldDialogTests
{
    private const int MapSize = 72;
    private const int DisplayWidth = 640;
    private const int DisplayHeight = 480;

    private static readonly Rect VisibleArea = new(16, 16, 432, 448);
    private static readonly Rect AdventureRoi = new(30, 30, 12, 12);

    [Fact]
    public void TheCloseHotKeysAreEscapeAndEnter()
    {
        // Game::HotKeyCloseWindow() = DEFAULT_CANCEL || DEFAULT_OKAY；端口沿用 HeroDialog 的 Escape + Enter。
        LocalEvent events = new(() => 0);

        events.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);
        Assert.True(ViewWorldDialog.IsCloseHotKey(events));

        events = new LocalEvent(() => 0);
        events.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);
        Assert.True(ViewWorldDialog.IsCloseHotKey(events));

        events = new LocalEvent(() => 0);
        events.OnKeyboard(Key.Space, KeyModifier.None, KeyboardEventState.Down);
        Assert.False(ViewWorldDialog.IsCloseHotKey(events));
    }

    [Fact]
    public void TheTooltipsAreTheReferenceOnes()
    {
        Assert.Equal("Exit", ViewWorldDialog.ExitTitle);
        Assert.Equal("Exit this menu.", ViewWorldDialog.ExitBody);
        Assert.Equal("Zoom", ViewWorldDialog.ZoomTitle);
        Assert.Equal("Click this button to adjust the level of zoom.", ViewWorldDialog.ZoomBody);
    }

    [Fact]
    public void TheLoopPaintsTheMapAndAnswersTheZoomLevelItWasOpenedWith()
    {
        ViewWorldWindow window = Open(ZoomLevel.ZoomLevel2);
        var display = new PixelBuffer(DisplayWidth, DisplayHeight);
        display.Fill(9);

        int invalidations = 0;
        ZoomLevel left = ViewWorldDialog.Show(
            display,
            window,
            new LocalEvent(() => 0) { Engine = new StoppingEngine() },
            showHint: null,
            invalidate: () => ++invalidations);

        Assert.Equal(ZoomLevel.ZoomLevel2, left);

        // 首帧把地图 blit 进可视区：草地的颜色不是初始的 9。
        Assert.NotEqual(9, display[VisibleArea.X, VisibleArea.Y]);
        Assert.True(invalidations >= 1);
    }

    [Fact]
    public void TheLoopEndsOnTheExitButton()
    {
        ViewWorldWindow window = Open(ZoomLevel.ZoomLevel1);
        var display = new PixelBuffer(DisplayWidth, DisplayHeight);
        LocalEvent events = new(() => 0) { Engine = new ClickingEngine(window.ExitButtonArea.Center) };

        ZoomLevel left = ViewWorldDialog.Show(display, window, events);

        Assert.Equal(ZoomLevel.ZoomLevel1, left);
    }

    [Fact]
    public void TheRightClickOnAButtonAsksForItsTooltip()
    {
        ViewWorldWindow window = Open(ZoomLevel.ZoomLevel1);
        var display = new PixelBuffer(DisplayWidth, DisplayHeight);
        LocalEvent events = new(() => 0) { Engine = new RightClickingEngine(window.ZoomButtonArea.Center) };

        string? title = null;
        string? body = null;

        ViewWorldDialog.Show(display, window, events, (hint, hintBody) =>
        {
            title = hint;
            body = hintBody;
        });

        Assert.Equal(ViewWorldDialog.ZoomTitle, title);
        Assert.Equal(ViewWorldDialog.ZoomBody, body);
    }

    private static ViewWorldWindow Open(ZoomLevel zoomLevel)
    {
        World world = LandWorld(MapSize);

        return new ViewWorldWindow(
            world,
            ViewWorldMode.ViewAll,
            PlayerColor.None,
            zoomLevel,
            VisibleArea,
            AdventureRoi,
            new ViewWorldMapCache(
                ViewWorldMode.ViewAll, world.Width, world.Height, ViewWorldZoomLevels.RegularZoomLevels, (_, _, _) => { }),
            DisplayWidth,
            DisplayHeight,
            new FakeSprites());
    }

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

    /// <summary>An event engine that has nothing to poll and asks the loop to stop.</summary>
    private sealed class StoppingEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, DisplayWidth, DisplayHeight);

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            return false;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    /// <summary>One left click at a point, then the loop stops.</summary>
    private sealed class ClickingEngine : IEventEngine
    {
        private readonly Point _point;
        private bool _clicked;

        public ClickingEngine(Point point) => _point = point;

        public Rect ScreenArea => new(0, 0, DisplayWidth, DisplayHeight);

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (_clicked)
            {
                return false;
            }

            _clicked = true;
            state.OnMouseMotion(_point);
            state.OnMouseButton(true, MouseButtonType.Left, _point);
            state.OnMouseButton(false, MouseButtonType.Left, _point);

            return true;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    /// <summary>One right click at a point, then the loop stops.</summary>
    private sealed class RightClickingEngine : IEventEngine
    {
        private readonly Point _point;
        private bool _clicked;

        public RightClickingEngine(Point point) => _point = point;

        public Rect ScreenArea => new(0, 0, DisplayWidth, DisplayHeight);

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (_clicked)
            {
                return false;
            }

            _clicked = true;
            state.OnMouseMotion(_point);
            state.OnMouseButton(true, MouseButtonType.Right, _point);

            return true;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    /// <summary>The two buttons and the side panel, all solid colours.</summary>
    private sealed class FakeSprites : IViewWorldSpriteSource
    {
        private readonly Dictionary<(IcnType Icn, int Index), Sprite> _sprites = [];

        public FakeSprites()
        {
            Add(IcnType.Lgndxtra, 0, 96, 30, 3);
            Add(IcnType.Lgndxtra, 1, 96, 30, 4);
            Add(IcnType.ButtonViewworldExitGood, 0, 96, 30, 3);
            Add(IcnType.ButtonViewworldExitGood, 1, 96, 30, 4);
        }

        public Sprite? GetSprite(IcnType icn, int index) =>
            _sprites.TryGetValue((icn, index), out Sprite? sprite) ? sprite : null;

        private void Add(IcnType icn, int index, int width, int height, byte colour)
        {
            var image = new PixelBuffer(width, height);
            image.Fill(colour);
            _sprites[(icn, index)] = new Sprite(image);
        }
    }
}
