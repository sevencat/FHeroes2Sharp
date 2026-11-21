using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="StatusPanel"/>, <c>Interface::StatusPanel</c> (<c>gui/interface_status.cpp:55-524</c>).
/// </summary>
public sealed class StatusPanelTests
{
    public StatusPanelTests() => Settings.Reset();

    [Fact]
    public void Constructor_RejectsNulls()
    {
        PixelBuffer display = new(800, 600);
        World world = WorldWith();
        StubSprites sprites = new();
        StubMonsters monsters = new();
        StubInterface stub = new(display);

        Assert.Throws<ArgumentNullException>(() => new StatusPanel(null!, display, world, sprites, monsters));
        Assert.Throws<ArgumentNullException>(() => new StatusPanel(stub, null!, world, sprites, monsters));
        Assert.Throws<ArgumentNullException>(() => new StatusPanel(stub, display, null!, sprites, monsters));
        Assert.Throws<ArgumentNullException>(() => new StatusPanel(stub, display, world, null!, monsters));
        Assert.Throws<ArgumentNullException>(() => new StatusPanel(stub, display, world, sprites, null!));
    }

    [Fact]
    public void SetPos_StretchesThePanelToTheBottom()
    {
        (StatusPanel panel, _, _, PixelBuffer display) = CreatePanel();

        panel.SetPos(0, 100);

        Assert.Equal(new Rect(0, 100, StatusPanel.Width, display.Height - 100 - 16), panel.Area);
    }

    [Fact]
    public void SetPos_KeepsOneStateWhenTheInterfaceIsHidden()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.HideInterfaceEnabled = true;
        panel.SetPos(10, 20);

        Assert.Equal(new Rect(10 + 16, 20 + 16, StatusPanel.Width, StatusPanel.Height), panel.Area);
    }

    [Fact]
    public void SetRedraw_AddsTheStatusBit()
    {
        (StatusPanel panel, StubInterface stub, _, _) = CreatePanel();

        panel.SetRedraw(Redraw.None);
        Assert.Equal(Redraw.Status, stub.GetRedrawMask());

        panel.SetRedraw(Redraw.Status);
        Assert.Equal(Redraw.Status, stub.GetRedrawMask());

        panel.SetRedraw();
        Assert.Equal(Redraw.Status, stub.GetRedrawMask());
    }

    [Fact]
    public void SetRedraw_RejectsOtherBits()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        Assert.Throws<ArgumentOutOfRangeException>(() => panel.SetRedraw(Redraw.Buttons));
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.SetRedraw(Redraw.Status | Redraw.Radar));
    }

    [Fact]
    public void SetState_RejectsTheResourceState()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.SetState(StatusType.Day);
        Assert.Equal(StatusType.Day, panel.State);

        Assert.Throws<ArgumentException>(() => panel.SetState(StatusType.Resource));
    }

    [Fact]
    public void SetState_DoesNotReplaceAPendingResourceMessage()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.SetResource(Resource.Gold, 500);
        panel.SetState(StatusType.Army);

        Assert.Equal(StatusType.Resource, panel.State);
    }

    [Fact]
    public void SetResource_StartsTheMessage()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.SetResource(Resource.Crystal, 7);

        Assert.Equal(StatusType.Resource, panel.State);
        Assert.Equal(Resource.Crystal, panel.LastResource);
        Assert.Equal(7u, panel.LastResourceCount);
    }

    [Fact]
    public void NextState_CyclesThroughTheStatesWithoutFocus()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.Reset();
        Assert.Equal(StatusType.Day, panel.State);

        panel.NextState();
        Assert.Equal(StatusType.Funds, panel.State);

        panel.NextState();
        Assert.Equal(StatusType.Day, panel.State);
    }

    [Fact]
    public void NextState_LeavesTheResourceMessage()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.SetResource(Resource.Wood, 3);
        panel.NextState();

        Assert.Equal(StatusType.Army, panel.State);
    }

    [Fact]
    public void TimerEventProcessing_KeepsTheMessageUntilTheDelayPasses()
    {
        long now = 0;
        (StatusPanel panel, StubInterface stub, _, _) = CreatePanel(() => now);

        panel.SetResource(Resource.Gold, 100);
        stub.Clear();

        now = StatusPanel.ResourceMessageDelayMs - 1;
        panel.TimerEventProcessing();
        Assert.Equal(StatusType.Resource, panel.State);
        Assert.Equal(Redraw.None, stub.GetRedrawMask());

        now = StatusPanel.ResourceMessageDelayMs;
        panel.TimerEventProcessing();
        Assert.Equal(StatusType.Day, panel.State);
        Assert.Equal(Redraw.Status, stub.GetRedrawMask());
    }

    [Fact]
    public void QueueEventProcessing_DoesNothingOutsideThePanel()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();
        LocalEvent events = new();

        events.OnMouseMotion(new Point(700, 500));

        Assert.Equal(StatusPanelAction.None, panel.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_CyclesTheStateOnLeftClick()
    {
        (StatusPanel panel, StubInterface stub, _, _) = CreatePanel();
        LocalEvent events = new();
        Point inside = new(10, 10);

        panel.Reset();
        panel.SetPos(0, 0);
        stub.Clear();

        events.OnMouseMotion(inside);
        events.OnMouseButton(true, MouseButtonType.Left, inside);
        events.OnMouseButton(false, MouseButtonType.Left, inside);

        Assert.Equal(StatusPanelAction.StateCycled, panel.QueueEventProcessing(events));
        Assert.Equal(StatusType.Funds, panel.State);
        Assert.Equal(Redraw.Status, stub.GetRedrawMask());
    }

    [Fact]
    public void QueueEventProcessing_AsksForTheTooltipOnRightClick()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();
        LocalEvent events = new();
        Point inside = new(10, 10);

        panel.SetPos(0, 0);

        events.OnMouseMotion(inside);
        events.OnMouseButton(true, MouseButtonType.Right, inside);

        Assert.Equal(StatusPanelAction.ShowTooltip, panel.QueueEventProcessing(events));
    }

    [Fact]
    public void RedrawStatus_SkipsEverythingWhenHiddenAndNotShown()
    {
        (StatusPanel panel, _, _, PixelBuffer display) = CreatePanel();

        display.Fill(0);
        panel.HideInterfaceEnabled = true;
        panel.ShowStatus = false;
        panel.SetPos(0, 0);
        panel.RedrawStatus();

        Assert.Equal(0, display[20, 20]);
    }

    [Fact]
    public void RedrawStatus_PaintsTheBackgroundWithoutADate()
    {
        (StatusPanel panel, _, _, PixelBuffer display) = CreatePanel(new World());

        display.Fill(0);
        panel.SetPos(0, 0);
        panel.RedrawStatus();

        Assert.NotEqual(0, display[20, 20]);
    }

    [Fact]
    public void RedrawStatus_PaintsTheDateWithRealAssets()
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            PixelBuffer display = new(800, 600);
            StatusPanel panel = CreateAssetPanel(assets, display);

            display.Fill(0);
            panel.SetPos(0, 100);
            panel.Reset();
            panel.RedrawStatus();

            Assert.True(HasPaintedPixels(display));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void RedrawStatus_PaintsTheResourceMessageWithRealAssets()
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            PixelBuffer display = new(800, 600);
            StatusPanel panel = CreateAssetPanel(assets, display);

            display.Fill(0);
            panel.SetPos(0, 100);
            panel.SetResource(Resource.Gold, 250);
            panel.RedrawStatus();

            Assert.True(HasPaintedPixels(display));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void RedrawStatus_PaintsTheAiTurnHourglassWithRealAssets()
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            PixelBuffer display = new(800, 600);
            StatusPanel panel = CreateAssetPanel(assets, display);

            Settings.Get().Players.SetCurrentColor(PlayerColor.Blue);

            display.Fill(0);
            panel.SetPos(0, 100);
            panel.SetState(StatusType.AiTurn);
            panel.RedrawStatus();

            Assert.True(HasPaintedPixels(display));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void Reset_ReturnsToTheDateState()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.SetResource(Resource.Gold, 1);
        panel.Reset();

        Assert.Equal(StatusType.Day, panel.State);
        Assert.Equal(Resource.Unknown, panel.LastResource);
        Assert.Equal(0u, panel.LastResourceCount);
    }

    [Fact]
    public void DrawAiTurnProgress_RepaintsTheStatus()
    {
        (StatusPanel panel, StubInterface stub, _, _) = CreatePanel();

        stub.Clear();
        panel.DrawAiTurnProgress(1);

        Assert.Equal(Redraw.Status, stub.GetRenderedMask());
    }

    [Fact]
    public void SavePosition_StoresTheFloatingPosition()
    {
        (StatusPanel panel, _, _, _) = CreatePanel();

        panel.HideInterfaceEnabled = true;
        panel.SetPos(30, 40);
        panel.SavePosition();

        Assert.Equal(new Point(30, 40), Settings.Get().PosStatus);
    }

    private static (StatusPanel Panel, StubInterface Stub, StubSprites Sprites, PixelBuffer Display) CreatePanel(Func<long>? clockMs = null) =>
        CreatePanel(WorldWith(), clockMs);

    private static (StatusPanel Panel, StubInterface Stub, StubSprites Sprites, PixelBuffer Display) CreatePanel(
        World world, Func<long>? clockMs = null)
    {
        PixelBuffer display = new(800, 600);
        StubInterface stub = new(display);
        StubSprites sprites = new();

        sprites.Add(IcnType.Stonback, 0, PaintedSprite(StatusPanel.Width, StatusPanel.Height));
        sprites.Add(IcnType.Stonbake, 0, PaintedSprite(StatusPanel.Width, StatusPanel.Height));
        sprites.Add(IcnType.Ressmall, 0, PaintedSprite(70, 27));
        sprites.Add(IcnType.Sunmoon, 0, PaintedSprite(30, 30));

        StatusPanel panel = new(stub, display, world, sprites, new StubMonsters(), clockMs);

        return (panel, stub, sprites, display);
    }

    private static StatusPanel CreateAssetPanel(AssetBundle assets, PixelBuffer display)
    {
        IMiniMonsterSprites monsters = Mons32Sprites.TryCreate(assets) ?? (IMiniMonsterSprites)new StubMonsters();

        return new StatusPanel(new StubInterface(display), display, WorldWith(),
            new IcnViewWorldSpriteSource(assets), monsters);
    }

    private static Sprite PaintedSprite(int width, int height)
    {
        Sprite sprite = new(width, height);
        sprite.Image.Fill(1);

        return sprite;
    }

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static bool HasPaintedPixels(PixelBuffer display)
    {
        for (int y = 0; y < display.Height; ++y)
        {
            for (int x = 0; x < display.Width; ++x)
            {
                if (display[x, y] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static World WorldWith()
    {
        World world = new();
        world.GenerateUninitializedMap(72);
        world.NewDay();

        return world;
    }

    /// <summary>The sprites a test needs, and only those.</summary>
    private sealed class StubSprites : IViewWorldSpriteSource
    {
        private readonly Dictionary<(IcnType Icn, int Index), Sprite> _sprites = [];

        public void Add(IcnType icn, int index, Sprite sprite) => _sprites[(icn, index)] = sprite;

        public Sprite? GetSprite(IcnType icn, int index) =>
            _sprites.TryGetValue((icn, index), out Sprite? sprite) ? sprite : null;
    }

    /// <summary>A monster set that draws nothing.</summary>
    private sealed class StubMonsters : IMiniMonsterSprites
    {
        public Sprite? GetMiniMonster(MonsterType monster) => null;
    }

    /// <summary>An interface that records what the panel asks of its owner.</summary>
    private sealed class StubInterface : BaseInterface
    {
        private Redraw _rendered;

        public StubInterface(PixelBuffer display)
            : base(isEditor: false, display)
        {
        }

        public void Clear()
        {
            SetRedraw(Redraw.None);
            _rendered = Redraw.None;
        }

        public Redraw GetRenderedMask() => _rendered;

        public override void RedrawInterface(Redraw force) => _rendered |= force;

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
