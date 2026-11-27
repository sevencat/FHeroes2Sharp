using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Editor;
using Xunit;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="EditorMapSpecsWindow"/>, the assembly half of
/// <c>mapSpecificationsDialog()</c> (<c>editor_map_specs_window.cpp:2043-2235</c>).
/// </summary>
/// <remarks>
/// 需要真实美术件的断言跟着游戏数据门控；纯几何的断言（窗口活动区、六个按钮的相互位置）不需要数据，
/// 没有数据也照样跑。窗口只在 <see cref="EditorMapSpecsWindow.Create"/> 里读一次 Settings，
/// 这里不碰设置，测的是默认的 GOOD 主题。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorMapSpecsWindowTests : IDisposable
{
    /// <summary>The area the reference gives the window on a 640x480 screen.</summary>
    private static readonly Rect ExpectedActiveArea = new(16, 16, 608, 448);

    private AssetBundle? _assets;

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose()
    {
        GameAssets.Current = null;
        _assets?.Dispose();
        _assets = null;
    }

    [Fact]
    public void TheWindowFillsTheScreen()
    {
        if (!OpenAssets())
        {
            return;
        }

        EditorMapSpecsWindow window = CreateWindow();

        Assert.Equal(ExpectedActiveArea, window.ActiveArea);
        Assert.Equal(ExpectedActiveArea, window.PageLayout.ActiveArea);
    }

    [Fact]
    public void EveryButtonStaysInsideTheActiveAreaAndTheyDoNotOverlap()
    {
        if (!OpenAssets())
        {
            return;
        }

        EditorMapSpecsWindow window = CreateWindow();
        Rect[] areas =
        [
            window.Okay.Area,
            window.Cancel.Area,
            window.Rumors.Area,
            window.Events.Area,
            window.Language.Area,
            window.About.Area,
        ];

        foreach (Rect area in areas)
        {
            Assert.False(area.IsEmpty);
            Assert.Equal(area, window.ActiveArea.Intersect(area));
        }

        for (int first = 0; first < areas.Length; ++first)
        {
            for (int second = first + 1; second < areas.Length; ++second)
            {
                Assert.True(areas[first].Intersect(areas[second]).IsEmpty,
                    $"{areas[first]} overlaps {areas[second]}.");
            }
        }
    }

    [Fact]
    public void TheBottomButtonsSitSixPixelsAboveTheBottomEdge()
    {
        if (!OpenAssets())
        {
            return;
        }

        EditorMapSpecsWindow window = CreateWindow();

        foreach (Rect area in new[] { window.Okay.Area, window.Cancel.Area, window.Rumors.Area, window.Events.Area, window.Language.Area })
        {
            Assert.Equal(window.ActiveArea.Bottom - EditorMapSpecsWindow.BottomButtonOffsetY, area.Bottom);
        }

        Assert.Equal(window.ActiveArea.X + EditorMapSpecsWindow.BottomButtonOffsetX, window.Rumors.Area.X);
    }

    [Fact]
    public void TheOkayButtonSitsTenPixelsLeftOfCancel()
    {
        if (!OpenAssets())
        {
            return;
        }

        EditorMapSpecsWindow window = CreateWindow();

        // 参考 :2217 先定 CANCEL 再让 OKAY 往左让 10 像素，两者都贴右下。
        Assert.Equal(window.ActiveArea.Right - EditorMapSpecsWindow.BottomButtonOffsetX, window.Cancel.Area.Right);
        Assert.Equal(window.Cancel.Area.X - EditorMapSpecsWindow.ButtonGap, window.Okay.Area.Right);
        Assert.Equal(window.Cancel.Area.Y, window.Okay.Area.Y);
    }

    [Fact]
    public void TheAboutButtonSitsInTheTopRightCorner()
    {
        if (!OpenAssets())
        {
            return;
        }

        EditorMapSpecsWindow window = CreateWindow();

        // 参考 :2235 的 { 21, 12 } 顶右偏移。
        Assert.Equal(window.ActiveArea.Right - EditorMapSpecsWindow.AboutOffsetX, window.About.Area.Right);
        Assert.Equal(window.ActiveArea.Y + EditorMapSpecsWindow.AboutOffsetY, window.About.Area.Y);
    }

    [Fact]
    public void PaintingDrawsEveryButton()
    {
        if (!OpenAssets())
        {
            return;
        }

        EditorMapSpecsWindow window = CreateWindow();
        PixelBuffer buffer = new(EditorMapSpecsWindow.ScreenWidth, EditorMapSpecsWindow.ScreenHeight);

        window.Paint(buffer);

        Assert.True(CountInk(buffer, window.Okay.Area) > 0, "OKAY was not painted.");
        Assert.True(CountInk(buffer, window.Cancel.Area) > 0, "CANCEL was not painted.");
        Assert.True(CountInk(buffer, window.Rumors.Area) > 0, "RUMORS was not painted.");
        Assert.True(CountInk(buffer, window.Events.Area) > 0, "EVENTS was not painted.");
        Assert.True(CountInk(buffer, window.Language.Area) > 0, "LANGUAGE was not painted.");
        Assert.True(CountInk(buffer, window.About.Area) > 0, "ABOUT was not painted.");
    }

    [Fact]
    public void PaintingTwiceGivesTheSamePixels()
    {
        if (!OpenAssets())
        {
            return;
        }

        EditorMapSpecsWindow window = CreateWindow();
        PixelBuffer once = new(EditorMapSpecsWindow.ScreenWidth, EditorMapSpecsWindow.ScreenHeight);
        PixelBuffer twice = new(EditorMapSpecsWindow.ScreenWidth, EditorMapSpecsWindow.ScreenHeight);

        window.Paint(once);
        window.Paint(twice);
        window.Paint(twice);

        // 按钮阴影是「加法」渐变，重复画在同一缓冲上会越来越亮；整块重画必须每帧从头来。
        Assert.Equal(once.Pixels, twice.Pixels);
    }

    private static EditorMapSpecsWindow CreateWindow()
    {
        MapSpecsPageArtwork artwork = MapSpecsPageArtwork.TryCreate(isEvilInterface: false)
            ?? throw new InvalidOperationException("The installed game data has no map specifications page artwork.");

        return EditorMapSpecsWindow.Create(artwork, new MapSpecsState(Mp2Map.CreateBlank(36, terrainImageIndex: 0)));
    }

    private bool OpenAssets()
    {
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        GameAssets.Current = _assets;

        return _assets is not null;
    }

    private static int CountInk(PixelBuffer buffer, in Rect area)
    {
        if (area.IsEmpty)
        {
            return 0;
        }

        Rect clipped = area.Intersect(buffer.Bounds);
        int count = 0;

        for (int y = clipped.Y; y < clipped.Bottom; ++y)
        {
            ReadOnlySpan<byte> row = buffer.Row(y);

            for (int x = clipped.X; x < clipped.Right; ++x)
            {
                if (row[x] != 0)
                {
                    ++count;
                }
            }
        }

        return count;
    }
}
