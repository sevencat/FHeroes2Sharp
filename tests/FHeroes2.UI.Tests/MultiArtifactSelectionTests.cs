using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="MultiItemSelection"/> and <see cref="MultiArtifactSelectionWindow"/> against
/// <c>dialog_selectitems.cpp:88-129</c> and <c>:169-267</c>.
/// </summary>
public sealed class MultiArtifactSelectionTests
{
    private const byte Canvas = 3;
    private const byte IconColor = 41;

    [Fact]
    public void AnEmptySelectionPicksEverything()
    {
        MultiItemSelection selection = new([1, 2, 3], []);

        Assert.Equal([1, 2, 3], selection.GetSelected());
        Assert.False(selection.IsEmpty);
    }

    [Fact]
    public void OnlyTheIdsTheListOffersAreKept()
    {
        MultiItemSelection selection = new([1, 2, 3], [2, 9]);

        // 9 不在候选里，丢掉；2 留着，所以「为空则全选」不再触发。
        Assert.Equal([2], selection.GetSelected());
    }

    [Fact]
    public void TogglingArowFlipsIt()
    {
        MultiItemSelection selection = new([1, 2, 3], [1, 2, 3]);

        Assert.False(selection.Toggle(2));
        Assert.Equal([1, 3], selection.GetSelected());

        Assert.True(selection.Toggle(2));
        Assert.Equal([1, 2, 3], selection.GetSelected());
    }

    [Fact]
    public void TogglingTheLastPickedRowEmptiesTheSelection()
    {
        MultiItemSelection selection = new([7], [7]);

        Assert.False(selection.Toggle(7));
        Assert.True(selection.IsEmpty);
        Assert.Empty(selection.GetSelected());
    }

    [Fact]
    public void SelectAllAndClearAreTheTwoToggleButtons()
    {
        MultiItemSelection selection = new([1, 2, 3], []);

        selection.Clear();
        Assert.True(selection.IsEmpty);

        selection.SelectAll();
        Assert.Equal([1, 2, 3], selection.GetSelected());
    }

    [Fact]
    public void TheSelectionKeepsTheOrderOfTheList()
    {
        MultiItemSelection selection = new([5, 3, 9], [9, 5]);

        Assert.Equal([5, 9], selection.GetSelected());
    }

    [Fact]
    public void TheWindowStartsWithTheGivenSelectionAndDisablesOkay()
    {
        MultiArtifactSelectionWindow window = CreateWindow(selected: []);

        Assert.Equal(3, window.SelectedArtifacts.Count);
        Assert.True(window.Buttons[0].IsEnabled);

        // 点「全关」：选中集清空，OKAY 跟着灰掉（参考的 setButtonOkayStatus）。
        window.ProcessEvents(Click(window.ToggleAllOff!.Area.Center));

        Assert.Empty(window.SelectedArtifacts);
        Assert.False(window.Buttons[0].IsEnabled);

        // 再点「全开」：全选中，OKAY 回来。
        window.ProcessEvents(Click(window.ToggleAllOn!.Area.Center));

        Assert.Equal(3, window.SelectedArtifacts.Count);
        Assert.True(window.Buttons[0].IsEnabled);
    }

    [Fact]
    public void AClickTogglesTheRowAndNeverClosesTheWindow()
    {
        MultiArtifactSelectionWindow window = CreateWindow(selected: []);

        Assert.True(window.ToggleAllOff!.IsEnabled);
        Assert.False(window.ToggleAllOn!.IsEnabled);

        // 点第一行：取消它，窗口不关（参考的 isDoubleClicked() 恒为假）。
        window.ProcessEvents(Click(RowCenter(window, 0)));

        Assert.Equal(2, window.SelectedArtifacts.Count);
        Assert.Equal(DialogButton.None, window.Result);
        Assert.False(window.IsClosed);

        // 点第二行：再取消一个。
        window.ProcessEvents(Click(RowCenter(window, 1)));

        Assert.Single(window.SelectedArtifacts);
    }

    [Fact]
    public void TheToggleButtonsAppearOnlyWhenTheArtworkHasThem()
    {
        MultiArtifactSelectionWindow withArtwork = CreateWindow(selected: []);
        MultiArtifactSelectionWindow without = CreateWindow(selected: [], withToggles: false);

        Assert.NotNull(withArtwork.ToggleAllOn);
        Assert.NotNull(withArtwork.ToggleAllOff);
        Assert.Null(without.ToggleAllOn);
        Assert.Null(without.ToggleAllOff);
    }

    private static MultiArtifactSelectionWindow CreateWindow(IReadOnlyList<int> selected,
        bool withToggles = true) =>
        new([(int)ArtifactType.MagicBook, (int)ArtifactType.UltimateSword, (int)ArtifactType.Ankh],
            selected, new FakeIcons(), ArtifactSelectDialog.DialogSize(), CreateArtwork(withToggles),
            CreateFont(), CreateFont());

    private static ItemSelectionWindowArtwork CreateArtwork(bool withToggles)
    {
        PixelBuffer groove = CreateBuffer(560, 320, 9);

        for (int y = 176; y < 306; ++y)
        {
            for (int x = 536; x < 552; ++x)
            {
                groove[x, y] = 40;
            }
        }

        return new ItemSelectionWindowArtwork
        {
            ScrollBarBackground = new Sprite(groove),
            ScrollUpReleased = new Sprite(CreateBuffer(14, 15, 11)),
            ScrollUpPressed = new Sprite(CreateBuffer(14, 15, 12)),
            ScrollDownReleased = new Sprite(CreateBuffer(14, 15, 13)),
            ScrollDownPressed = new Sprite(CreateBuffer(14, 15, 14)),
            ScrollSlider = new Sprite(CreateBuffer(10, 25, 15)),
            Okay = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 16)),
                new Sprite(CreateBuffer(40, 20, 17))),
            Cancel = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 18)),
                new Sprite(CreateBuffer(40, 20, 19))),
            ToggleAllOn = withToggles
                ? (new Sprite(CreateBuffer(86, 25, 21)), new Sprite(CreateBuffer(86, 25, 22)))
                : null,
            ToggleAllOff = withToggles
                ? (new Sprite(CreateBuffer(86, 25, 23)), new Sprite(CreateBuffer(86, 25, 24)))
                : null,
        };
    }

    private static PixelBuffer CreateBuffer(int width, int height, int value)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill((byte)value);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new FakeGlyphSource();

    /// <summary>Centre of a list row, the point a click on that row lands on.</summary>
    /// <param name="window">The window whose list the row belongs to.</param>
    /// <param name="index">Zero based row.</param>
    /// <returns>The centre of the row.</returns>
    /// <remarks>
    /// 行高是 <c>ItemsArea.Height / VisibleItemCount</c>（<see cref="ListBox{T}"/> 的换算），列表从顶部挤满可见行，
    /// 所以第 n 行的中心是 <c>ItemsArea.Y + n * rowHeight + rowHeight / 2</c>。
    /// </remarks>
    private static Point RowCenter(MultiArtifactSelectionWindow window, int index)
    {
        int rowHeight = window.ItemsArea.Height / window.VisibleItemCount;

        return new Point(window.ItemsArea.X + 60, window.ItemsArea.Y + (index * rowHeight) + (rowHeight / 2));
    }

    /// <summary>Feeds one left click on a point, the press and the release in the same frame.</summary>
    /// <param name="point">The point to click.</param>
    /// <returns>The events of that frame.</returns>
    private static LocalEvent Click(Point point)
    {
        LocalEvent events = new();

        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        return events;
    }

    /// <summary>32x32 icons of one flat colour, which is enough to tell them from the shaded list.</summary>
    private sealed class FakeIcons : IArtifactIcons
    {
        public Sprite? GetMiniArtifact(uint frame) => new Sprite(CreateBuffer(32, 32, IconColor));
    }

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = new(CreateBuffer(6, 12, 1));

        public int LineHeight => 12;

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
