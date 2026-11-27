using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the pieces the condition panel is assembled from: <see cref="MapSpecsConditionTownRow"/>,
/// <see cref="MapSpecsConditionArtifactRow"/>, <see cref="MapSpecsConditionCheckboxRow"/> and
/// <see cref="MapSpecsConditionAllianceBlock"/> (<c>editor_map_specs_window.cpp:1021-1217</c>, <c>:1668-1743</c>).
/// </summary>
/// <remarks>
/// 全部跟着游戏数据门控：这些零件都要真的取 ATM 美术件与字体，没有数据就没有可验证的东西。
/// 断言看「贴到哪儿、有没有贴上、勾有没有画出来」，不比像素值。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MapSpecsConditionPanelPiecesTests : IDisposable
{
    /// <summary>The panel area the reference passes for a victory condition, <c>editor_map_specs_window.cpp:1040</c>.</summary>
    private static readonly Rect PanelRoi = new(99, 292, 114, 150);

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose() => Assets.Current = null;

    [Fact]
    public void TheTownRowPutsTheIconAtTheLeftEdgeAndTheTitleNextToIt()
    {
        if (!OpenAssets())
        {
            return;
        }

        MapSpecsConditionTownRowData town = new(PlayerColor.Blue, MapRace.Knight, "Rangar", HasFort: true, TileIndex: 84);
        PixelBuffer buffer = new(640, 480);

        Rect row = MapSpecsConditionTownRow.Draw(town, 13, PanelRoi, buffer, isEvilInterface: false);

        Sprite? icon = MapSpecsConditionIcons.BuildTownIcon(
            town.Race, town.Color, town.HasFort, isEvilInterface: false);
        Assert.NotNull(icon);

        // 图标贴在 roi.x、roi.y + 4，标题从图标右侧 5 像素起（:294-299）。
        Assert.Equal(PanelRoi.X, row.X);
        Assert.Equal(PanelRoi.Y + MapSpecsConditionTownRow.IconOffsetY, row.Y);
        Assert.Equal(icon.Image.Height, row.Height);

        Text title = new(
            MapSpecsConditionTitles.GetTownTitle(town.Name, town.Race, isTown: !town.HasFort, town.TileIndex, 13),
            FontType.NormalWhite());
        title.FitToOneRow(PanelRoi.Width - icon.Image.Width - MapSpecsConditionTownRow.TitleGap);

        Assert.Equal(icon.Image.Width + MapSpecsConditionTownRow.TitleGap + title.Width(), row.Width);

        Rect iconArea = new(row.X, row.Y, icon.Image.Width, icon.Image.Height);
        Rect titleArea = new(
            row.X + icon.Image.Width + MapSpecsConditionTownRow.TitleGap, row.Y + MapSpecsConditionTownRow.TitleOffsetY,
            title.Width(), title.Height());

        Assert.True(CountInk(buffer, iconArea) > 0, "The town icon was not painted.");
        Assert.True(CountInk(buffer, titleArea) > 0, "The town title was not painted.");
    }

    [Fact]
    public void TheEmptySlotIsPaintedAndTheArtifactGoesIntoIt()
    {
        if (!OpenAssets())
        {
            return;
        }

        Rect slot = new(105, 296, 101, 93);
        PixelBuffer empty = new(640, 480);
        PixelBuffer filled = new(640, 480);

        Assert.True(MapSpecsConditionArtifactRow.TryDraw(ArtifactType.Unknown, slot, empty));
        Assert.True(MapSpecsConditionArtifactRow.TryDraw(ArtifactType.UltimateSword, slot, filled));

        Assert.True(CountInk(empty, slot) > 0, "The empty artifact slot was not painted.");

        // 神器图贴在底座 +6,+6 的 64x64 上（:1119-1121）：那一片必须和空底座不同。
        Rect iconArea = new(
            slot.X + MapSpecsConditionArtifactRow.ArtifactOffsetX, slot.Y + MapSpecsConditionArtifactRow.ArtifactOffsetY,
            64, 64);

        Assert.False(SameArea(empty, filled, iconArea), "The artifact icon was not painted into the slot.");
    }

    [Fact]
    public void ACheckedRowPaintsTheMark()
    {
        if (!OpenAssets())
        {
            return;
        }

        const string Label = "Allow standard victory conditions";
        Rect row = new(104, 337, 104, 25);
        PixelBuffer uncheckedBuffer = new(640, 480);
        PixelBuffer checkedBuffer = new(640, 480);

        Rect uncheckedArea = MapSpecsConditionCheckboxRow.Draw(Label, isChecked: false, row, uncheckedBuffer, isEvilInterface: false);
        Rect checkedArea = MapSpecsConditionCheckboxRow.Draw(Label, isChecked: true, row, checkedBuffer, isEvilInterface: false);

        // 区域 = 23 像素的框宽 + 文本宽，高是 CELLWIN 帧 1 的高（editor_ui_helper.cpp:75）；
        // 文字先按 maxWidth - 23 折行（这里是 104 - 23 = 81），所以要比对折行后的宽度。
        Text fitted = new(Label, FontType.NormalWhite());
        fitted.FitToOneRow(row.Width - 23);

        Assert.Equal(23 + fitted.Width(), uncheckedArea.Width);
        Assert.Equal(uncheckedArea, checkedArea);

        Assert.False(SameArea(uncheckedBuffer, checkedBuffer, uncheckedArea), "The check mark was not painted.");
    }

    [Fact]
    public void TheEvilCheckboxRowIsNotSupportedYet()
    {
        // CELLWIN_EVIL 要靠启动时生成，端口还没有那条生成器；参考走的是 _isEvilInterface 分支。
        PixelBuffer buffer = new(64, 64);

        Assert.Throws<NotSupportedException>(
            () => MapSpecsConditionCheckboxRow.Draw("x", isChecked: false, new Rect(0, 0, 40, 25), buffer, isEvilInterface: true));
    }

    [Fact]
    public void TheAllianceBlockDrawsABoxPerColourInBothRows()
    {
        if (!OpenAssets())
        {
            return;
        }

        PlayerColor available = PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red;
        Rect titleRoi = new(99, 197, 114, 20);
        Rect[] rowRois = [new Rect(104, 225, 104, 25), new Rect(104, 250, 104, 25)];
        PixelBuffer buffer = new(640, 480);

        IReadOnlyList<List<Checkbox>> rows = MapSpecsConditionAllianceBlock.Draw(
            available, [PlayerColor.None, PlayerColor.None], titleRoi, rowRois, buffer);

        Assert.Equal(rowRois.Length, rows.Count);
        Assert.All(rows, row => Assert.Equal(3, row.Count));

        // 每个框一个颜色，顺序与 PlayerColors.ToList 一致，步进 32（:1144-1157）。
        IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(available);
        Assert.Equal(colors, rows[0].Select(box => box.Color));

        for (int index = 1; index < rows[0].Count; ++index)
        {
            Assert.Equal(
                MapSpecsConditionAllianceBlock.CheckboxStep,
                rows[0][index].Area.X - rows[0][index - 1].Area.X);
        }

        // 每行的框自己算位置（文本宽不同就左右挪）；颜色多时整排会溢出面板，这是参考的行为（:1145）。
        Assert.All(rows, row => Assert.All(row, box => Assert.True(box.Area.Width > 0)));
        Assert.True(CountInk(buffer, titleRoi) > 0, "The alliance title was not painted.");
    }

    [Fact]
    public void TheCheckedAllianceColoursPaintTheirMarks()
    {
        if (!OpenAssets())
        {
            return;
        }

        PlayerColor available = PlayerColor.Blue | PlayerColor.Green;
        Rect titleRoi = new(99, 197, 114, 20);
        Rect[] rowRois = [new Rect(104, 225, 104, 25), new Rect(104, 250, 104, 25)];
        PixelBuffer plain = new(640, 480);
        PixelBuffer marked = new(640, 480);
        Rect rows = new(rowRois[0].X, rowRois[0].Y, rowRois[0].Width, rowRois[1].Bottom - rowRois[0].Y);

        MapSpecsConditionAllianceBlock.Draw(available, [PlayerColor.None, PlayerColor.None], titleRoi, rowRois, plain);
        MapSpecsConditionAllianceBlock.Draw(
            available, [PlayerColor.Blue, PlayerColor.Green], titleRoi, rowRois, marked);

        Assert.False(SameArea(plain, marked, rows), "The alliance check marks were not painted.");
    }

    private static bool OpenAssets()
    {
        Assets.Current = GameData.TryOpen();

        return Assets.Current is not null;
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

    private static bool SameArea(PixelBuffer left, PixelBuffer right, in Rect area)
    {
        Rect clipped = area.Intersect(left.Bounds);

        for (int y = clipped.Y; y < clipped.Bottom; ++y)
        {
            ReadOnlySpan<byte> leftRow = left.Row(y);
            ReadOnlySpan<byte> rightRow = right.Row(y);

            if (!leftRow.Slice(clipped.X, clipped.Width).SequenceEqual(rightRow.Slice(clipped.X, clipped.Width)))
            {
                return false;
            }
        }

        return true;
    }
}
