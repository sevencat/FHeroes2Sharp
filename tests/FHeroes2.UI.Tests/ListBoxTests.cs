using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The selection and scroll state machine of a list, <c>Interface::ListBox&lt;Item&gt;</c>
/// (<c>src/fheroes2/gui/interface_list.h:54-667</c>).
/// </summary>
public sealed class ListBoxTests
{
    private static readonly Rect RowsArea = new(0, 100, 100, 80);

    [Fact]
    public void SetListContent_SelectsTheFirstRowAndFillsTheScrollBar()
    {
        TestList list = CreateList(10);

        Assert.True(list.IsValid);
        Assert.Equal(0, list.TopId);
        Assert.Equal(0, list.CurrentId);
        Assert.Equal(10, list.Size);
        Assert.Equal(4, list.VisibleItemCount);
        Assert.Equal(6, list.ScrollBar.MaxIndex);
    }

    [Fact]
    public void EmptyContent_IsInvalidAndResetsTheScrollBar()
    {
        TestList list = new(RowsArea);

        Assert.False(list.IsValid);
        Assert.Equal(-1, list.TopId);
        Assert.Equal(-1, list.CurrentId);
        Assert.Empty(list.Drawn);
    }

    [Fact]
    public void DownKey_MovesTheSelectionAndCallsTheHook()
    {
        TestList list = CreateList(10);

        list.QueueEventProcessing(KeyDown(Key.Down));

        Assert.Equal(1, list.CurrentId);
        Assert.Equal(1, list.DownCalls);
        Assert.Equal(0, list.UpCalls);
    }

    [Fact]
    public void EndKey_ScrollsToTheBottomAndSelectsTheLastRow()
    {
        TestList list = CreateList(10);

        list.QueueEventProcessing(KeyDown(Key.End));

        Assert.Equal(6, list.TopId);
        Assert.Equal(9, list.CurrentId);
        Assert.Equal(6, list.ScrollBar.CurrentIndex);
    }

    [Fact]
    public void HomeKey_ReturnsToTheTop()
    {
        TestList list = CreateList(10);

        list.QueueEventProcessing(KeyDown(Key.End));
        list.QueueEventProcessing(KeyDown(Key.Home));

        Assert.Equal(0, list.TopId);
        Assert.Equal(0, list.CurrentId);
    }

    [Fact]
    public void PageDown_ScrollsByOnePageAndKeepsTheSelectionInView()
    {
        TestList list = CreateList(10);

        list.QueueEventProcessing(KeyDown(Key.PageDown));

        Assert.Equal(4, list.TopId);
        Assert.Equal(4, list.CurrentId);

        list.QueueEventProcessing(KeyDown(Key.PageDown));

        // The last page is aligned to the bottom, not scrolled past it; the selection follows by two rows.
        Assert.Equal(6, list.TopId);
        Assert.Equal(6, list.CurrentId);
    }

    [Fact]
    public void DisableHotkeys_IgnoresTheKeyboard()
    {
        TestList list = CreateList(10);

        list.DisableHotkeys(true);
        Assert.False(list.QueueEventProcessing(KeyDown(Key.Down)));

        Assert.Equal(0, list.CurrentId);
    }

    [Fact]
    public void WheelDown_ScrollsByOneRow()
    {
        TestList list = CreateList(10);

        LocalEvent events = new();
        events.OnMouseMotion(new Point(50, 110));
        events.OnMouseWheel(new Point(0, -1));

        Assert.True(list.QueueEventProcessing(events));
        Assert.Equal(1, list.TopId);

        events.OnMouseWheel(new Point(0, 1));
        list.QueueEventProcessing(events);
        Assert.Equal(0, list.TopId);
    }

    [Fact]
    public void Click_SelectsTheRowUnderTheCursor()
    {
        TestList list = CreateList(10);

        Click(list, new Point(50, 150));

        // 50 pixels into an 80 pixel area with four rows is row 2.
        Assert.Equal(2, list.CurrentId);
        Assert.Equal(["2"], list.SingleClicks);
    }

    [Fact]
    public void ClickOnTheSelectedRow_IsADoubleClick()
    {
        TestList list = CreateList(10);

        Click(list, new Point(50, 150));
        Click(list, new Point(50, 150));

        Assert.Equal(["2"], list.SingleClicks);
        Assert.Equal(["2"], list.DoubleClicks);
    }

    [Fact]
    public void DragScrolling_MovesTheViewWithoutClicking()
    {
        TestList list = CreateList(10);

        LocalEvent events = new();
        events.OnMouseMotion(new Point(50, 150));
        events.OnMouseButton(true, MouseButtonType.Left, new Point(50, 150));

        list.QueueEventProcessing(events);

        // Move the held cursor up by more than one row: 40 pixels against a 20 pixel row.
        events.OnMouseMotion(new Point(50, 110));

        Assert.True(list.QueueEventProcessing(events));
        Assert.Equal(2, list.TopId);

        // Releasing after the drag must not select anything.
        events.OnMouseButton(false, MouseButtonType.Left, new Point(50, 110));

        list.QueueEventProcessing(events);

        Assert.Empty(list.SingleClicks);
        Assert.Empty(list.DoubleClicks);
    }

    [Fact]
    public void GetFromPosition_TruncatesIntoRowIndices()
    {
        TestList list = CreateList(10);

        Assert.Equal("0", list.GetFromPosition(new Point(50, 100)));
        Assert.Equal("1", list.GetFromPosition(new Point(50, 125)));
        Assert.Equal("3", list.GetFromPosition(new Point(50, 179)));
        Assert.Null(list.GetFromPosition(new Point(50, 180)));
        Assert.Null(list.GetFromPosition(new Point(150, 110)));
    }

    [Fact]
    public void SetTopVisibleItem_ClampsIntoTheList()
    {
        TestList list = CreateList(10);

        list.SetTopVisibleItem(100);
        Assert.Equal(6, list.TopId);
        Assert.Equal(6, list.ScrollBar.CurrentIndex);

        list.SetTopVisibleItem(-5);
        Assert.Equal(0, list.TopId);
    }

    [Fact]
    public void SetCurrent_ScrollsTheRowIntoView()
    {
        TestList list = CreateList(10);

        list.SetCurrent(8);

        Assert.Equal(8, list.CurrentId);
        Assert.Equal(5, list.TopId);
    }

    [Fact]
    public void RemoveSelected_ShortensTheListAndKeepsTheViewSane()
    {
        TestList list = CreateList(10);

        list.SetCurrent(2);
        list.RemoveSelected();

        Assert.Equal(9, list.Size);
        Assert.Equal("3", list.Items[2]);
        Assert.Equal(2, list.CurrentId);

        // Removing the last row moves the selection up by one.
        list.SetCurrent(8);
        list.RemoveSelected();

        Assert.Equal(8, list.Size);
        Assert.Equal(7, list.CurrentId);
    }

    [Fact]
    public void Redraw_PaintsOnlyTheVisibleRows()
    {
        TestList list = CreateList(10);
        PixelBuffer output = new(100, 200);

        list.Redraw(output);

        Assert.Equal(["0", "1", "2", "3"], list.Drawn);

        list.SetTopVisibleItem(6);
        list.Redraw(output);

        Assert.Equal(["0", "1", "2", "3", "6", "7", "8", "9"], list.Drawn);
        Assert.Equal(2, list.RedrawBackgroundCalls);
    }

    [Fact]
    public void Unselect_ClearsTheSelectionWithoutLosingTheView()
    {
        TestList list = CreateList(10);

        list.SetCurrent(5);
        list.Unselect();

        Assert.False(list.IsSelected);
        Assert.Equal(-1, list.CurrentId);
        Assert.Equal(2, list.TopId);
    }

    private static TestList CreateList(int count)
    {
        TestList list = new(RowsArea);
        List<string> items = [];
        for (int i = 0; i < count; ++i)
        {
            items.Add(i.ToString());
        }

        list.Items = items;
        list.SetAreaItems(RowsArea);
        list.SetScrollBarArea(new Rect(90, 100, 8, 80));
        list.SetScrollBarImage(new Sprite(8, 20));
        list.SetAreaMaxItems(4);
        list.SetListContent(items);

        return list;
    }

    private static LocalEvent KeyDown(Key key)
    {
        LocalEvent events = new();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        return events;
    }

    private static void Click(TestList list, Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        list.QueueEventProcessing(events);

        events.OnMouseButton(false, MouseButtonType.Left, position);
        list.QueueEventProcessing(events);
    }

    private sealed class TestList : ListBox<string>
    {
        public TestList(Rect rowsArea)
            : base(new Point(0, 0))
        {
            SetAreaItems(rowsArea);
        }

        public List<string> Items { get; set; } = [];

        public List<string> Drawn { get; } = [];

        public List<string> SingleClicks { get; } = [];

        public List<string> DoubleClicks { get; } = [];

        public int UpCalls { get; private set; }

        public int DownCalls { get; private set; }

        public int RedrawBackgroundCalls { get; private set; }

        public override void RedrawItem(string item, int x, int y, bool current, PixelBuffer output) =>
            Drawn.Add(item);

        public override void RedrawBackground(Point destination, PixelBuffer output) => ++RedrawBackgroundCalls;

        public override void ActionCurrentUp() => ++UpCalls;

        public override void ActionCurrentDn() => ++DownCalls;

        public override void ActionListSingleClick(string item) => SingleClicks.Add(item);

        public override void ActionListDoubleClick(string item) => DoubleClicks.Add(item);

        public override void ActionListPressRight(string item)
        {
        }
    }
}
