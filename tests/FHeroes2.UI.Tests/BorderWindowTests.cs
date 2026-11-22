using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="BorderWindow"/> and <see cref="BorderDrag"/>, the floating panels of
/// <c>gui/interface_border.cpp:420-524</c>.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class BorderWindowTests
{
    [Fact]
    public void GetRect_IsTheArea_WhileTheInterfaceIsVisible()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(10, 20, 100, 60));

        Assert.Equal(new Rect(10, 20, 100, 60), panel.GetRect);
        Assert.Equal(new Rect(10, 20, 100, 60), panel.Area);
    }

    [Fact]
    public void SetPos_WithAHiddenInterface_ClampsTheFloatingWindowToTheDisplay()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(0, 0, 100, 60)) { HideInterfaceEnabled = true };

        panel.MoveTo(-50, -50);

        // The frame is pushed inside the display, so the contents start one border width in.
        Assert.Equal(new Rect(0, 0, 132, 92), panel.GetRect);
        Assert.Equal(new Rect(16, 16, 100, 60), panel.Area);

        panel.MoveTo(1000, 1000);

        Assert.Equal(640 - 132, panel.GetRect.X);
        Assert.Equal(480 - 92, panel.GetRect.Y);
    }

    [Fact]
    public void SetPos_WithAHiddenInterface_SavesTheNewPosition()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(0, 0, 100, 60)) { HideInterfaceEnabled = true };

        panel.MoveTo(120, 140);

        Assert.Equal(1, panel.SaveCount);
        Assert.Equal(new Point(136, 156), panel.SavedAreaOrigin);
    }

    [Fact]
    public void SetPos_WithAVisibleInterface_MovesTheAreaOnly()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(0, 0, 100, 60));

        panel.MoveTo(120, 140);

        Assert.Equal(new Rect(120, 140, 100, 60), panel.Area);
        Assert.Equal(0, panel.SaveCount);
    }

    [Fact]
    public void QueueEventProcessing_IgnoresAPressOutsideTheTopBorder()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(10, 10, 100, 60)) { HideInterfaceEnabled = true };
        LocalEvent events = new();

        // Nothing is placed yet, so the border has no size and the top strip is empty.
        events.OnMouseButton(true, MouseButtonType.Left, new Point(20, 20));
        Assert.Null(panel.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_StartsADragFromTheTopBorder()
    {
        using AssetBundle? assets = OpenGameData();
        if (assets is null)
        {
            return;
        }

        try
        {
            PixelBuffer target = new(640, 480);
            TestPanel panel = new(target, new Rect(10, 10, 100, 60)) { HideInterfaceEnabled = true };
            panel.MoveTo(100, 100);

            LocalEvent events = new();
            events.OnMouseButton(true, MouseButtonType.Left, new Point(120, 105));

            BorderDrag? drag = panel.QueueEventProcessing(events);

            Assert.NotNull(drag);
            Assert.Equal(new Rect(100, 100, 132, 92), drag!.Panel);
            Assert.Equal(new Point(20, 5), drag.GrabOffset);
            Assert.Equal(new Point(100, 100), drag.Position);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void QueueEventProcessing_IgnoresAPressOnThePanelBody()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(10, 10, 100, 60)) { HideInterfaceEnabled = true };
        panel.MoveTo(100, 100);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, new Point(150, 150));

        Assert.Null(panel.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_IgnoresAPress_WhenTheInterfaceIsVisible()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(10, 10, 100, 60));
        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, new Point(20, 20));

        Assert.Null(panel.QueueEventProcessing(events));
    }

    [Fact]
    public void BorderDrag_FollowsTheCursorAndPaintsTheOutline()
    {
        using AssetBundle? assets = OpenGameData();
        if (assets is null)
        {
            return;
        }

        try
        {
            BorderDrag drag = new(new Rect(100, 100, 40, 30), new Point(10, 5));

            drag.MoveTo(new Point(210, 155));

            Assert.Equal(new Point(200, 150), drag.Position);

            PixelBuffer target = new(400, 300);
            drag.Draw(target);

            // The outline is the dotted yellow border of the panel-sized sprite, so its corners are painted and
            // the middle of the panel is not.
            Assert.Equal(BorderDrag.OutlineColor, target[200, 150]);
            Assert.Equal((byte)0, target[220, 165]);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void IsMouseCaptured_ReleasesOnTheFrameTheButtonGoesUp()
    {
        PixelBuffer target = new(640, 480);
        TestPanel panel = new(target, new Rect(10, 10, 100, 60));
        LocalEvent events = new();

        Assert.False(panel.IsMouseCaptured(events));

        events.OnMouseButton(true, MouseButtonType.Left, new Point(20, 20));
        panel.CaptureMouse(events);

        Assert.True(panel.IsMouseCaptured(events));

        // Still captured on the release frame, so the click does not fall through to the map.
        events.OnMouseButton(false, MouseButtonType.Left, new Point(20, 20));
        Assert.True(panel.IsMouseCaptured(events));

        Assert.False(panel.IsMouseCaptured(events));
    }

    /// <summary>
    /// Opens the game archive and hands it to the asset layer; the drag outline resolves its colour through
    /// <c>KB.PAL</c>, so these two tests need real data and stay silent without it.
    /// </summary>
    private static AssetBundle? OpenGameData()
    {
        if (AssetLocator.Resolve() is not { } directory)
        {
            return null;
        }

        AssetBundle? bundle = AssetBundle.Open(directory);
        GameAssets.Current = bundle;

        return bundle;
    }

    private sealed class TestPanel : BorderWindow
    {
        public TestPanel(PixelBuffer target, Rect area, Sprite? artwork = null)
            : base(target, area, artwork)
        {
        }

        public int SaveCount { get; private set; }

        public Point SavedAreaOrigin { get; private set; }

        public override void SetPos(int x, int y) => SetPosition(x, y);

        public override void SavePosition()
        {
            ++SaveCount;
            SavedAreaOrigin = new Point(Area.X, Area.Y);
        }

        public void MoveTo(int x, int y) => SetPosition(x, y);

        public void Paint() => Redraw();
    }
}
