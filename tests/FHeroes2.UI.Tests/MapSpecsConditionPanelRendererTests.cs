using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Widgets;
using Xunit;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="MapSpecsConditionPanelRenderer"/>, the port of <c>VictoryConditionUI::render()</c>
/// (<c>editor_map_specs_window.cpp:1021-1217</c>) and <c>LossConditionUI::render()</c> (<c>:1668-1743</c>).
/// </summary>
/// <remarks>
/// 需要真实美术件与字体的断言跟着游戏数据门控；只算布局与抛异常的断言不门控，没有数据也照样跑。
/// 数值控件用 <see cref="FakeGlyphSource"/> 拼出来：这里验证的是「画到了布局给的位置上」，
/// 数字本身的字形不是本层的职责。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MapSpecsConditionPanelRendererTests : IDisposable
{
    /// <summary>The area the reference gives the victory panel, <c>editor_map_specs_window.cpp:1040</c>.</summary>
    private static readonly Rect PanelRoi = new(99, 292, 114, 150);

    private static readonly MapSpecsConditionTownRowData Town =
        new(PlayerColor.Blue, MapRace.Knight, "Rangar", HasFort: true, TileIndex: 84);

    private static readonly MapSpecsConditionTownRowData NeutralTown =
        new(PlayerColor.None, MapRace.Knight, string.Empty, HasFort: false, TileIndex: 84);

    private static readonly MapSpecsConditionHeroRowData Hero =
        new(PlayerColor.Blue, MapRace.Knight, string.Empty, TileIndex: 84);

    private AssetBundle? _assets;

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose()
    {
        GameAssets.Current = null;
        _assets?.Dispose();
        _assets = null;
    }

    [Fact]
    public void TheDefeatEveryoneConditionHasNoPanel()
    {
        MapSpecsVictoryPanelView view = new(MapVictoryCondition.DefeatEveryone, MapWidth: 13);

        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PanelRoi);

        Assert.True(layout.SelectRowRoi.IsEmpty);
        Assert.True(layout.ValueAreaRoi.IsEmpty);
        Assert.True(layout.AllowNormalVictoryRoi.IsEmpty);
        Assert.True(layout.AllowForAiRoi.IsEmpty);
        Assert.Empty(layout.AllianceRowRois);
    }

    [Fact]
    public void TheDefeatEveryoneConditionPaintsNothing()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(MapVictoryCondition.DefeatEveryone, MapWidth: 13);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanel panel = MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette());

        Assert.Empty(panel.AllianceCheckboxes);
        Assert.Equal(0, CountInk(buffer, PanelRoi));
    }

    [Fact]
    public void TheTownConditionPaintsTheRowAndBothCheckboxesForANeutralTown()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(MapVictoryCondition.CaptureTown, MapWidth: 13, Town: NeutralTown);
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PanelRoi);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette());

        Assert.True(CountInk(buffer, layout.SelectRowRoi) > 0, "The town row was not painted.");
        Assert.True(CountInk(buffer, layout.AllowForAiRoi) > 0, "The AI checkbox was not painted.");
        Assert.True(CountInk(buffer, layout.AllowNormalVictoryRoi) > 0, "The standard victory checkbox was not painted.");
    }

    [Fact]
    public void TheTownOfAPlayerGetsNoAiCheckbox()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(MapVictoryCondition.CaptureTown, MapWidth: 13, Town: Town);
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PanelRoi);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette());

        // 参考只在城镇属于中立时给「也给 AI」的框（:1066）。
        Assert.True(layout.AllowForAiRoi.IsEmpty);
        Assert.True(CountInk(buffer, layout.SelectRowRoi) > 0, "The town row was not painted.");
        Assert.True(CountInk(buffer, layout.AllowNormalVictoryRoi) > 0, "The standard victory checkbox was not painted.");
    }

    [Fact]
    public void TheKillHeroConditionPaintsTheHeroFrameAndNoCheckboxes()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(MapVictoryCondition.KillHero, MapWidth: 13, Hero: Hero);
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PanelRoi);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette());

        // 参考只贴英雄行，不给这个条件任何勾选项（:1098）。
        Assert.True(layout.AllowNormalVictoryRoi.IsEmpty);
        Assert.True(layout.AllowForAiRoi.IsEmpty);
        Assert.Equal(MapSpecsConditionHeroRow.FrameHeight, layout.SelectRowRoi.Height);
        Assert.True(CountInk(buffer, layout.SelectRowRoi) > 0, "The hero row was not painted.");
    }

    [Fact]
    public void TheArtifactConditionPaintsTheSlotAndTheStandardVictoryBox()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(
            MapVictoryCondition.ObtainArtifact, MapWidth: 13, Artifact: ArtifactType.UltimateSword);

        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PanelRoi);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette());

        Sprite slot = GameAssets.GetImage("RESOURCE.ICN", MapSpecsConditionArtifactRow.FrameId)!;
        Assert.Equal(new Size(slot.Width, slot.Height), layout.SelectRowRoi.Size);
        Assert.True(CountInk(buffer, layout.SelectRowRoi) > 0, "The artifact slot was not painted.");
        Assert.True(CountInk(buffer, layout.AllowNormalVictoryRoi) > 0, "The standard victory checkbox was not painted.");
    }

    [Fact]
    public void TheAllianceConditionHandsBackOneCheckboxRowPerAlliance()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(
            MapVictoryCondition.DefeatOtherSide, MapWidth: 13,
            AvailableColors: PlayerColor.Blue | PlayerColor.Green,
            Alliances: [PlayerColor.Blue, PlayerColor.None]);

        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PanelRoi);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanel panel = MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette());

        Assert.Equal(layout.AllianceRowRois.Count, panel.AllianceCheckboxes.Count);
        Assert.All(panel.AllianceCheckboxes, row => Assert.Equal(2, row.Count));
        Assert.True(CountInk(buffer, layout.AllianceTitleRoi) > 0, "The alliance title was not painted.");
    }

    [Fact]
    public void TheGoldConditionPaintsTheNumberWithItsLabel()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(MapVictoryCondition.CollectGold, MapWidth: 13);
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PanelRoi);
        ValueSelectionElement gold = CreateValue(10000, 1000000, 10000, layout.ValueAreaRoi.Position);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette(), gold);

        Assert.True(CountInk(buffer, layout.ValueAreaRoi) > 0, "The gold element was not painted.");

        // 标签贴在数值区左侧 5 像素、垂直居中 +2 处（:1177-1178）。
        Rect label = new(
            layout.ValueAreaRoi.X - new Text(MapSpecsConditionPanelRenderer.GoldLabel, FHeroes2.Game.Gui.FontType.NormalWhite()).Width()
            - MapSpecsConditionPanelRenderer.LabelGap, layout.ValueAreaRoi.Y, 40, 12);

        Assert.True(CountInk(buffer, label) > 0, "The gold label was not painted.");
    }

    [Fact]
    public void TheGoldConditionWithoutANumberElementIsRejected()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsVictoryPanelView view = new(MapVictoryCondition.CollectGold, MapWidth: 13);

        Assert.Throws<ArgumentException>(() => MapSpecsConditionPanelRenderer.DrawVictory(
            view, PanelRoi, new PixelBuffer(640, 480), isEvilInterface: false, BlackPalette()));
    }

    [Fact]
    public void TheOutOfTimeConditionPaintsTheNumberWithTheDateUnderIt()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsLossPanelView view = new(MapLossCondition.OutOfTime, MapWidth: 13);
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetLossLayout(view, PanelRoi);
        ValueSelectionElement days = CreateValue(1, 3650, 28, layout.ValueAreaRoi.Position);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanelRenderer.DrawLoss(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette(), days);

        Assert.True(CountInk(buffer, layout.ValueAreaRoi) > 0, "The days element was not painted.");
        Assert.True(CountInk(buffer, layout.DaysDescriptionRoi) > 0, "The date description was not painted.");

        // 28 天是第四周的第七天（:1730-1733 用的是 getDateDescription）。
        Assert.Equal("Day: 7 Week: 4 Month: 1", GameDate.Describe(days.Value));
    }

    [Fact]
    public void TheOutOfTimeConditionWithoutANumberElementIsRejected()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsLossPanelView view = new(MapLossCondition.OutOfTime, MapWidth: 13);

        Assert.Throws<ArgumentException>(() => MapSpecsConditionPanelRenderer.DrawLoss(
            view, PanelRoi, new PixelBuffer(640, 480), isEvilInterface: false, BlackPalette()));
    }

    [Fact]
    public void TheLoseTownConditionPaintsTheTownRow()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsLossPanelView view = new(MapLossCondition.LoseTown, MapWidth: 13, Town: Town);
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelRenderer.GetLossLayout(view, PanelRoi);
        PixelBuffer buffer = new(640, 480);

        MapSpecsConditionPanel panel = MapSpecsConditionPanelRenderer.DrawLoss(
            view, PanelRoi, buffer, isEvilInterface: false, BlackPalette());

        Assert.Empty(panel.AllianceCheckboxes);
        Assert.True(CountInk(buffer, layout.SelectRowRoi) > 0, "The town row was not painted.");
    }

    [Fact]
    public void AConditionWithoutItsTargetIsRejected()
    {
        Assert.Throws<ArgumentException>(() => MapSpecsConditionPanelRenderer.GetVictoryLayout(
            new MapSpecsVictoryPanelView(MapVictoryCondition.CaptureTown, MapWidth: 13), PanelRoi));

        Assert.Throws<ArgumentException>(() => MapSpecsConditionPanelRenderer.GetVictoryLayout(
            new MapSpecsVictoryPanelView(MapVictoryCondition.KillHero, MapWidth: 13), PanelRoi));

        Assert.Throws<ArgumentException>(() => MapSpecsConditionPanelRenderer.GetLossLayout(
            new MapSpecsLossPanelView(MapLossCondition.LoseHero, MapWidth: 13), PanelRoi));
    }

    [Fact]
    public void AMapWithoutWidthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapSpecsConditionPanelRenderer.GetVictoryLayout(
            new MapSpecsVictoryPanelView(MapVictoryCondition.DefeatEveryone, MapWidth: 0), PanelRoi));

        Assert.Throws<ArgumentOutOfRangeException>(() => MapSpecsConditionPanelRenderer.GetLossLayout(
            new MapSpecsLossPanelView(MapLossCondition.Everything, MapWidth: 0), PanelRoi));
    }

    private static ValueSelectionElement CreateValue(int minimum, int maximum, int value, Point offset)
    {
        List<Sprite> frames = [];

        for (int frame = 0; frame < ValueSelectionElement.RequiredFrameCount; ++frame)
        {
            frames.Add(GameAssets.GetImage("TOWNWIND.ICN", frame)!);
        }

        return new ValueSelectionElement(minimum, maximum, value, 1, offset, new FakeGlyphSource(1, 14), frames);
    }

    private bool OpenAssets()
    {
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        GameAssets.Current = _assets;

        return _assets is not null;
    }

    /// <summary>A palette whose every index maps to black, enough for the calls that never reach a resampler.</summary>
    private static Palette BlackPalette()
    {
        byte[] table = new byte[256 * 3];

        return Palette.FromGamePalette(table);
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

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color, int lineHeight)
        {
            PixelBuffer image = new(6, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
