using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the shared item-bar base, <c>gui/interface_itemsbar.h</c>.
/// </summary>
public sealed class ItemsBarTests
{
    [Fact]
    public void FreshBarHasNoCellsAndDrawsNothing()
    {
        TestBar bar = new();
        PixelBuffer output = new(100, 100);

        Assert.Equal(0, bar.Count);
        Assert.True(bar.IsEmpty);

        // The reference returns before touching the frame when no table size was set.
        bar.Redraw(output);

        Assert.Empty(bar.PaintedBackgrounds);
        Assert.Empty(bar.PaintedItems);
    }

    [Fact]
    public void TableSizeAndCellSizeGiveTheAreaAndThePositions()
    {
        TestBar bar = new();
        bar.SetSingleItemSize(new Size(43, 43));
        bar.SetTableSize(new Size(5, 1));

        Assert.Equal(5, bar.Count);
        Assert.Equal(new Rect(0, 0, 215, 43), bar.RenderingArea);
        Assert.Equal(new Point(0, 0), bar.ItemPositions[0]);
        Assert.Equal(new Point(43, 0), bar.ItemPositions[1]);
        Assert.Equal(new Point(172, 0), bar.ItemPositions[4]);
    }

    [Fact]
    public void GapBetweenCellsWidensTheAreaAndTheStep()
    {
        TestBar bar = new();
        bar.SetSingleItemSize(new Size(43, 43));
        bar.SetInBetweenItemsOffset(new Size(1, 0));
        bar.SetTableSize(new Size(5, 1));

        Assert.Equal(new Rect(0, 0, 219, 43), bar.RenderingArea);
        Assert.Equal(new Point(44, 0), bar.ItemPositions[1]);
    }

    [Fact]
    public void RenderingOffsetMovesEveryCell()
    {
        TestBar bar = new();
        bar.SetSingleItemSize(new Size(10, 10));
        bar.SetTableSize(new Size(2, 2));
        bar.SetRenderingOffset(new Point(10, 20));

        Assert.Equal(new Rect(10, 20, 20, 20), bar.RenderingArea);
        Assert.Equal(new Point(10, 20), bar.ItemPositions[0]);
        Assert.Equal(new Point(20, 20), bar.ItemPositions[1]);
        Assert.Equal(new Point(10, 30), bar.ItemPositions[2]);
    }

    [Fact]
    public void CustomRowCountsAreHonouredOnlyWhenThereIsOnePerRow()
    {
        TestBar bar = new();
        bar.SetSingleItemSize(new Size(10, 10));
        bar.SetTableSize(new Size(5, 2));

        // Three cells per row: the table narrows to three columns and the rows are not centred.
        bar.SetItemsCountPerRow([3, 3]);

        Assert.Equal(6, bar.Count);
        Assert.Equal(new Size(3, 2), bar.TableSize);

        // A list with the wrong length is ignored, like the reference's "exactly one per row" test.
        bar.SetTableSize(new Size(5, 2));
        bar.SetItemsCountPerRow([2]);

        Assert.Equal(10, bar.Count);
        Assert.Equal(new Size(5, 2), bar.TableSize);
    }

    [Fact]
    public void CellsPastTheLastItemAnswerNothing()
    {
        TestBar bar = new();
        bar.SetSingleItemSize(new Size(10, 10));
        bar.SetTableSize(new Size(5, 1));
        bar.SetContent(new[] { 10, 20, 30, 40 });

        Assert.Equal(0, bar.IndexAt(new Point(5, 5)));
        Assert.Equal(3, bar.IndexAt(new Point(35, 5)));

        // The fifth cell exists but has no item behind it.
        Assert.Equal(-1, bar.IndexAt(new Point(45, 5)));
        Assert.Equal(-1, bar.IndexAt(new Point(100, 100)));

        Assert.True(bar.TryGetItem(new Point(15, 5), out int item));
        Assert.Equal(20, item);
        Assert.False(bar.TryGetItem(new Point(45, 5), out _));
    }

    [Fact]
    public void RedrawPaintsEveryBackgroundButOnlyTheFilledSlots()
    {
        TestBar bar = new();
        bar.SetSingleItemSize(new Size(10, 10));
        bar.SetTableSize(new Size(5, 1));
        bar.SetContent(new[] { 10, 20, 30, 40 });

        bar.Redraw(new PixelBuffer(100, 100));

        Assert.Equal(5, bar.PaintedBackgrounds.Count);
        Assert.Equal(4, bar.PaintedItems.Count);
        Assert.Equal(20, bar.PaintedItems[1]);
    }

    [Fact]
    public void ActionBarStartsUnselectedAndForgetsTheSelectionWithNewContent()
    {
        TestActionBar bar = new();
        bar.SetSingleItemSize(new Size(10, 10));
        bar.SetTableSize(new Size(5, 1));
        bar.SetContent(new[] { 10, 20 });

        Assert.False(bar.IsSelected);

        // "Nothing selected" is the item count in the reference, not -1.
        Assert.Equal(2, bar.GetSelectedIndex());
        Assert.Equal(default, bar.GetSelectedItem());
        Assert.Equal(default, bar.GetSelectedPos());

        bar.ResetSelected();

        Assert.False(bar.IsSelected);
        Assert.Equal(0, bar.TopItemIndex);
    }

    private class TestBar : ItemsBar<int>
    {
        public List<Rect> PaintedBackgrounds { get; } = [];

        public List<int> PaintedItems { get; } = [];

        public override void RedrawBackground(Rect area, PixelBuffer output)
        {
            _ = output;
            PaintedBackgrounds.Add(area);
        }

        public override void RedrawItem(int item, Rect position, PixelBuffer output)
        {
            _ = position;
            _ = output;
            PaintedItems.Add(item);
        }
    }

    private sealed class TestActionBar : ItemsActionBar<int>
    {
        public override void RedrawBackground(Rect area, PixelBuffer output)
        {
            _ = area;
            _ = output;
        }
    }
}
