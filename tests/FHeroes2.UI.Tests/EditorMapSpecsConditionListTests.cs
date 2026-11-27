using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

public sealed class EditorMapSpecsConditionListTests
{
    private const byte TopRowColor = 1;
    private const byte BandColor = 2;
    private const byte OtherColor = 3;

    private const int FrameWidth = 20;
    private const int FrameHeight = 12;

    [Fact]
    public void TheAreaIsTheFrameWidthPlusTheRightEdge()
    {
        EditorMapSpecsConditionList list = CreateList(itemCount: 2);

        int listHeight = 2 * (EditorMapSpecsConditionList.ItemHeight + 2) + 6;

        // The reference's restorer is one pixel wider than the frame: the right edge column lives there.
        Assert.Equal(new Rect(10, 20, FrameWidth + 1, listHeight), list.Area);
    }

    [Fact]
    public void TheFrameIsTiledAndTheRightEdgeIsOneColumnOfTheFrame()
    {
        EditorMapSpecsConditionList list = CreateList(itemCount: 2);
        PixelBuffer output = new(64, 96);

        list.RedrawBackground(new Point(10, 20), output);

        int topPartHeight = FrameHeight - 2;
        int listHeight = list.Area.Height;
        int bottomOffsetY = listHeight - topPartHeight;

        // Top band: copied as is, so its first pixel keeps the frame's colour.
        Assert.Equal(TopRowColor, output[10, 20]);

        // Middle and bottom bands both start at the frame's second row.
        Assert.Equal(BandColor, output[10, 20 + topPartHeight]);
        Assert.Equal(BandColor, output[10, 20 + bottomOffsetY]);

        // The right edge column repeats the frame's left column.
        Assert.Equal(TopRowColor, output[10 + FrameWidth, 20]);
        Assert.Equal(BandColor, output[10 + FrameWidth, 20 + bottomOffsetY]);
    }

    [Fact]
    public void TheFirstRowIsSelected()
    {
        EditorMapSpecsConditionList list = CreateList(itemCount: 3);

        Assert.True(list.IsSelected);
        Assert.Equal(0, list.SelectedCondition);
    }

    [Fact]
    public void AClickMarksTheListAsClickedAndKeepsTheSelection()
    {
        EditorMapSpecsConditionList list = CreateList(itemCount: 3);
        EditorMapSpecsConditionList.ConditionRow row = new(2);

        Assert.False(list.IsClicked);

        list.ActionListSingleClick(row);
        Assert.True(list.IsClicked);

        // A double click means the same, as in the reference.
        EditorMapSpecsConditionList doubleClicked = CreateList(itemCount: 3);
        doubleClicked.ActionListDoubleClick(row);
        Assert.True(doubleClicked.IsClicked);
    }

    [Fact]
    public void ARightClickReportsTheConditionWithoutSelectingIt()
    {
        EditorMapSpecsConditionList list = CreateList(itemCount: 3);
        List<int> reported = [];

        list.RightClickHandler = reported.Add;
        list.ActionListPressRight(new EditorMapSpecsConditionList.ConditionRow(2));

        Assert.Equal([2], reported);
        Assert.Equal(0, list.SelectedCondition);
        Assert.False(list.IsClicked);
    }

    [Fact]
    public void TheRowRectangleComesFromTheFrameWidthAndTheRowHeight()
    {
        Rect? reportedRow = null;
        EditorMapSpecsConditionList list = CreateList(itemCount: 2, drawRow: (value, row, current, output) =>
        {
            _ = value;
            _ = current;
            _ = output;
            reportedRow = row;
        });

        list.RedrawItem(new EditorMapSpecsConditionList.ConditionRow(1), 4, 6, current: true, new PixelBuffer(32, 32));

        // The reference keeps an item rectangle six pixels narrower than the frame and one pixel taller than
        // the font.
        Assert.Equal(new Rect(4, 6, FrameWidth - 6, EditorMapSpecsConditionList.ItemHeight), reportedRow);
    }

    private static EditorMapSpecsConditionList CreateList(
        int itemCount,
        Action<int, Rect, bool, PixelBuffer>? drawRow = null) =>
        new(new Point(10, 20), itemCount, CreateFrame(), drawRow ?? NoOpRow);

    private static void NoOpRow(int value, Rect row, bool current, PixelBuffer output)
    {
        _ = value;
        _ = row;
        _ = current;
        _ = output;
    }

    /// <summary>
    /// A frame whose top two rows, band rows and the right edge can be told apart, so a wrong copy shows up as a
    /// wrong colour.
    /// </summary>
    /// <remarks>
    /// 参考从归档取 <c>DROPLISL</c> 的 0 号帧；端口把帧当参数收，测试因此不需要游戏数据。
    /// </remarks>
    private static Sprite CreateFrame()
    {
        Sprite frame = new(FrameWidth, FrameHeight);
        PixelBuffer image = frame.Image;

        for (int y = 0; y < FrameHeight; ++y)
        {
            for (int x = 0; x < FrameWidth; ++x)
            {
                // The top band is copied as is; the middle and bottom bands both start at row 2, so a distinct
                // row there shows whether the copy starts at the right line.
                byte color = y switch
                {
                    0 => TopRowColor,
                    2 => BandColor,
                    _ => OtherColor,
                };

                image[x, y] = color;
            }
        }

        return frame;
    }
}
