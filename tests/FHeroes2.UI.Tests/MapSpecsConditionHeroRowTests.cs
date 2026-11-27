using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Editor;
using GameAssets = FHeroes2.Game.Assets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="MapSpecsConditionHeroRow"/>, the hero row of the condition panel
/// (<c>renderConditionsHeroIconAndName()</c>, <c>editor_map_specs_window.cpp:243-281</c>).
/// </summary>
/// <remarks>
/// 绘制要用 <c>SWAPWIN</c>、<c>FLAG32</c>、<c>STRIP</c> 与字体，所以真实绘制的那几条跟着游戏数据门控；
/// 几何与「没有资源就不画」这两条不依赖数据，始终运行。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MapSpecsConditionHeroRowTests : IDisposable
{
    /// <summary>The panel area the reference passes for a victory condition, <c>editor_map_specs_window.cpp:1040</c>.</summary>
    private static readonly Rect PanelRoi = new(99, 292, 114, 150);

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose() => GameAssets.Current = null;

    [Fact]
    public void TheFrameIsCentredInThePanelArea()
    {
        // roi.x + (roi.width - 111) / 2 = 99 + 1，roi.y + 4（:250）。
        Assert.Equal(new Rect(100, 296, 111, 105), MapSpecsConditionHeroRow.GetFrameRoi(PanelRoi));
    }

    [Fact]
    public void ATooNarrowPanelAreaStillGetsAFrame()
    {
        // 面板比框还窄时整数除法向零截断：宽度 20 得到 99 + (20 - 111) / 2 = 99 - 45。
        Rect narrow = new(99, 292, 20, 150);

        Assert.Equal(new Rect(54, 296, 111, 105), MapSpecsConditionHeroRow.GetFrameRoi(narrow));
    }

    [Fact]
    public void NothingIsDrawnWithoutTheWindowFrame()
    {
        GameAssets.Current = null;

        PixelBuffer buffer = new(320, 240);
        MapSpecsConditionHeroRowData hero = new(PlayerColor.Blue, MapRace.Knight, string.Empty, 84);

        Assert.False(MapSpecsConditionHeroRow.TryDraw(hero, 13, PanelRoi, buffer, BlackPalette()));
        Assert.Equal(0, CountInk(buffer, PanelRoi));
    }

    [Fact]
    public void TheRowNeedsANameString()
    {
        GameAssets.Current = null;

        PixelBuffer buffer = new(320, 240);
        MapSpecsConditionHeroRowData hero = new(PlayerColor.Blue, MapRace.Knight, null!, 84);

        Assert.Throws<ArgumentNullException>(
            () => MapSpecsConditionHeroRow.TryDraw(hero, 13, PanelRoi, buffer, BlackPalette()));
    }

    [Fact]
    public void TheRowNeedsAPositiveMapWidth()
    {
        GameAssets.Current = null;

        PixelBuffer buffer = new(320, 240);
        MapSpecsConditionHeroRowData hero = new(PlayerColor.Blue, MapRace.Knight, string.Empty, 84);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MapSpecsConditionHeroRow.TryDraw(hero, 0, PanelRoi, buffer, BlackPalette()));
    }

    [Fact]
    public void TheFrameTheFlagsThePortraitAndTheTitleArePainted()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null || assets.TryRead("KB.PAL") is not { } rawPalette)
        {
            return;
        }

        GameAssets.Current = assets;

        PixelBuffer buffer = new(640, 480);
        MapSpecsConditionHeroRowData hero = new(PlayerColor.Blue, MapRace.Knight, "Rangar", 84);

        Assert.True(MapSpecsConditionHeroRow.TryDraw(hero, 13, PanelRoi, buffer, Palette.FromGamePalette(rawPalette)));

        Rect row = MapSpecsConditionHeroRow.GetFrameRoi(PanelRoi);

        // 框本身（含圆角）必须画出来。
        Assert.True(CountInk(buffer, row) > row.Width * row.Height / 2, "the hero frame was not painted");

        // 左旗挂在框左外侧 21 像素处，右旗在框右 2 像素处（:258-259）；旗比框矮，那片区域本来只有底色。
        Rect leftFlag = new(row.X - MapSpecsConditionHeroRow.LeftFlagOverhang, row.Y + MapSpecsConditionHeroRow.FlagOffsetY, 20, 20);
        Rect rightFlag = new(row.X + row.Width + MapSpecsConditionHeroRow.RightFlagGap, row.Y + MapSpecsConditionHeroRow.FlagOffsetY, 20, 20);
        Assert.True(CountInk(buffer, leftFlag) > 0, "the left flag is missing");
        Assert.True(CountInk(buffer, rightFlag) > 0, "the right flag is missing");

        // 头像铺在框里 (5, 6) 起的 101x93 上，那一片应当完全被覆盖。
        Rect portrait = new(
            row.X + MapSpecsConditionHeroRow.PortraitOffsetX, row.Y + MapSpecsConditionHeroRow.PortraitOffsetY,
            MapSpecsConditionHeroRow.PortraitWidth, MapSpecsConditionHeroRow.PortraitHeight);
        Assert.Equal(portrait.Width * portrait.Height, CountInk(buffer, portrait));

        // 标题在框下 5 像素处，且被裁到面板宽度内（:277-278）。
        Rect title = new(
            PanelRoi.X, row.Y + row.Height + MapSpecsConditionHeroRow.TitleGap, PanelRoi.Width,
            MapSpecsConditionHeroRow.FrameHeight);
        Assert.True(CountInk(buffer, title) > 0, "the title is missing");
        Assert.Equal(0, CountInk(buffer, new Rect(PanelRoi.Right, title.Y, 640 - PanelRoi.Right, title.Height)));
    }

    [Fact]
    public void AnUnknownRaceKeepsTheRowWithoutAPortrait()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null || assets.TryRead("KB.PAL") is not { } rawPalette)
        {
            return;
        }

        GameAssets.Current = assets;

        PixelBuffer buffer = new(640, 480);
        MapSpecsConditionHeroRowData hero = new(PlayerColor.None, MapRace.None, string.Empty, 0);

        // 没有种族的英雄画不出头像，但框、旗与标题照画（参考对这种情况只有 assert）。
        Assert.True(MapSpecsConditionHeroRow.TryDraw(hero, 72, PanelRoi, buffer, Palette.FromGamePalette(rawPalette)));

        Rect row = MapSpecsConditionHeroRow.GetFrameRoi(PanelRoi);
        Rect portrait = new(
            row.X + MapSpecsConditionHeroRow.PortraitOffsetX, row.Y + MapSpecsConditionHeroRow.PortraitOffsetY,
            MapSpecsConditionHeroRow.PortraitWidth, MapSpecsConditionHeroRow.PortraitHeight);

        Assert.True(CountInk(buffer, portrait) < portrait.Width * portrait.Height);
    }

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>A palette whose every index maps to black, enough for the calls that never reach a resampler.</summary>
    private static Palette BlackPalette()
    {
        byte[] table = new byte[256 * 3];

        return Palette.FromGamePalette(table);
    }

    private static int CountInk(PixelBuffer buffer, in Rect area)
    {
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
