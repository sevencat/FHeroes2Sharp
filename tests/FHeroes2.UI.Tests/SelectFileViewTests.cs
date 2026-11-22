using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The window half of the save/load file chooser: layout, selection, sorting and the row/date drawing of
/// <c>selectFileListSimple()</c> (<c>dialog_selectfile.cpp:323-733</c>).
/// </summary>
public sealed class SelectFileViewTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TheWindowIsAsWideAsTheDateColumnsAllow()
    {
        SelectFileView view = Create(files: 0);

        Assert.Equal(SelectFileView.MaxFileNameWidth + SelectFileView.WidthDeduction,
            view.Window.ActiveArea.Width);
    }

    [Fact]
    public void FewFilesStillReserveTheMinimumNumberOfRows()
    {
        SelectFileView view = Create(files: 0);

        Assert.Equal(259, view.Window.ActiveArea.Height);
    }

    [Fact]
    public void TheHeightStopsAtTheMaximumNumberOfRows()
    {
        SelectFileView view = Create(files: 1000, screen: new Size(640, 700));

        Assert.Equal(
            (SelectFileView.MaximumVisibleRows * RowHeight) + SelectFileView.SmallGap + 4 +
            SelectFileView.ListHeightDeduction,
            view.Window.ActiveArea.Height);
    }

    [Fact]
    public void TheHeightNeverEatsMoreThanTheScreenAllows()
    {
        SelectFileView view = Create(files: 1000, screen: new Size(640, 480));

        Assert.Equal(Screen.Height - 100, view.Window.ActiveArea.Height);
    }

    [Fact]
    public void TheListSitsWhereTheDateColumnsNeedIt()
    {
        SelectFileView view = Create(files: 3);
        Rect active = view.Window.ActiveArea;

        Assert.Equal(new Rect(
            active.X + SelectFileView.ListInsetX,
            active.Y + SelectFileView.ListTop,
            active.Width - SelectFileView.ListWidthDeduction,
            active.Height - SelectFileView.ListHeightDeduction), view.ListArea);

        Assert.Equal(new Rect(view.ListArea.X, view.ListArea.Y - SelectFileView.HeaderHeight, 268, 28),
            view.NameHeaderArea);
        Assert.Equal(new Rect(view.NameHeaderArea.X + 268, view.ListArea.Y - SelectFileView.HeaderHeight,
            view.ListArea.Width - 268, 28), view.DateHeaderArea);
        Assert.Equal(new Rect(view.ListArea.X, view.ListArea.Y + view.ListArea.Height + SelectFileView.SmallGap,
            268, SelectFileView.TextInputHeight), view.TextInputArea);
    }

    [Fact]
    public void TheRowsAreSortedAndTheFirstOneIsSelected()
    {
        SelectFileView view = Create(files: 0);

        view.SetFiles([Row("beta", 2), Row("alpha", 1)], SaveFileSortingMethod.Filename);

        Assert.Equal("alpha", view.SelectedFile?.Name);

        // 参考里 SetListContent() 只选行，输入框的名字由调用方另外填。
        Assert.Equal(string.Empty, view.FileName);
    }

    [Fact]
    public void SelectingByPathMovesTheRowAndTheName()
    {
        SelectFileView view = Create(files: 0);
        view.SetFiles([Row("alpha", 1), Row("bravo", 2)], SaveFileSortingMethod.Filename);

        Assert.True(view.SelectFile(@"C:\saves\bravo.SAV"));
        Assert.Equal("bravo", view.SelectedFile?.Name);
        Assert.Equal("bravo", view.FileName);
        Assert.Equal(5, view.CursorPosition);
    }

    [Fact]
    public void SelectingAnUnknownFileLeavesEverythingAlone()
    {
        SelectFileView view = Create(files: 0);
        view.SetFiles([Row("alpha", 1)], SaveFileSortingMethod.Filename);
        view.FileName = "alpha";

        Assert.False(view.SelectFile(@"C:\saves\charlie.SAV"));
        Assert.Equal("alpha", view.SelectedFile?.Name);
        Assert.Equal("alpha", view.FileName);
    }

    [Fact]
    public void TheCaretStaysInsideTheTypedName()
    {
        SelectFileView view = Create(files: 0, isEditing: true);

        view.FileName = "bravo";
        view.CursorPosition = 1000;
        Assert.Equal(5, view.CursorPosition);

        view.CursorPosition = -5;
        Assert.Equal(0, view.CursorPosition);
    }

    [Fact]
    public void ALoadWindowTakesNoTypedName()
    {
        SelectFileView view = Create(files: 0, isEditing: false);
        view.FileName = "typed";

        LocalEvent events = new();
        events.OnKeyboard(Key.A, KeyModifier.None, KeyboardEventState.Down);

        view.ProcessEvents(events);

        Assert.Equal("typed", view.FileName);
    }

    [Fact]
    public void ReSortingKeepsTheSameSaveSelected()
    {
        SelectFileView view = Create(files: 0);
        view.SetFiles([Row("alpha", 3), Row("bravo", 1)], SaveFileSortingMethod.Filename);
        Assert.Equal("alpha", view.SelectedFile?.Name);

        Assert.True(view.SwitchSorting(SaveFileSortingMethod.Timestamp));

        Assert.Equal(SaveFileSortingMethod.Timestamp, view.Sorting);
        Assert.Equal("alpha", view.SelectedFile?.Name);
    }

    [Fact]
    public void SwitchingToTheSortOrderAlreadyInUseChangesNothing()
    {
        SelectFileView view = Create(files: 0);
        view.SetFiles([Row("alpha", 1)], SaveFileSortingMethod.Filename);

        Assert.False(view.SwitchSorting(SaveFileSortingMethod.Filename));
    }

    [Fact]
    public void DeletingARowSelectsTheOneBeforeIt()
    {
        SelectFileView view = Create(files: 0, isEditing: true);
        view.SetFiles([Row("alpha", 1), Row("bravo", 2), Row("charlie", 3)], SaveFileSortingMethod.Filename);
        Assert.True(view.SelectFile(@"C:\saves\bravo.SAV"));

        Assert.True(view.RemoveSelectedFile());

        Assert.Equal(new[] { "alpha", "charlie" }, VisibleNames(view));
        Assert.Equal("alpha", view.SelectedFile?.Name);
    }

    [Fact]
    public void DeletingTheLastRowEmptiesTheName()
    {
        SelectFileView view = Create(files: 0, isEditing: true);
        view.SetFiles([Row("alpha", 1)], SaveFileSortingMethod.Filename);

        Assert.True(view.RemoveSelectedFile());

        Assert.Null(view.SelectedFile);
        Assert.Equal(string.Empty, view.FileName);
        Assert.Empty(view.Files);
    }

    [Fact]
    public void AClickOnTheDateHeaderSwitchesToTheDateOrder()
    {
        SelectFileView view = Create(files: 0);
        view.SetFiles([Row("alpha", 1), Row("bravo", 2)], SaveFileSortingMethod.Filename);
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Left, Center(view.DateHeaderArea));
        events.OnMouseButton(false, MouseButtonType.Left, Center(view.DateHeaderArea));

        Assert.True(view.ProcessEvents(events));
        Assert.Equal(SaveFileSortingMethod.Timestamp, view.Sorting);

        // 换排序不动选中的那一行，参考的 switchFileSorting() 用 getSelectedFile() 找回来。
        Assert.Equal("alpha", view.SelectedFile?.Name);
    }

    [Fact]
    public void ARightClickOnTheNameHeaderExplainsTheNameOrder()
    {
        SelectFileView view = Create(files: 0);
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, Center(view.NameHeaderArea));

        view.ProcessEvents(events);

        Assert.Equal("Sort by Name", view.Popup?.Header);
    }

    [Fact]
    public void ARightClickOnTheDateHeaderExplainsTheDateOrder()
    {
        SelectFileView view = Create(files: 0);
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, Center(view.DateHeaderArea));

        view.ProcessEvents(events);

        Assert.Equal("Sort by Date", view.Popup?.Header);
    }

    [Fact]
    public void DeleteOnlyAsksForConfirmationWhileSaving()
    {
        SelectFileView saving = Create(files: 0, isEditing: true);
        saving.SetFiles([Row("alpha", 1)], SaveFileSortingMethod.Filename);

        LocalEvent delete = new();
        delete.OnKeyboard(Key.Delete, KeyModifier.None, KeyboardEventState.Down);

        Assert.True(saving.ProcessEvents(delete));
        Assert.True(saving.DeleteRequested);

        SelectFileView loading = Create(files: 0, isEditing: false);
        loading.SetFiles([Row("alpha", 1)], SaveFileSortingMethod.Filename);

        LocalEvent otherDelete = new();
        otherDelete.OnKeyboard(Key.Delete, KeyModifier.None, KeyboardEventState.Down);

        loading.ProcessEvents(otherDelete);

        Assert.False(loading.DeleteRequested);
    }

    [Fact]
    public void TheRowNameIsCentredInTheNameColumn()
    {
        SelectFileView view = Create(files: 0);
        PixelBuffer output = new(640, 480);

        view.RedrawItem(Row("abc", 1), 100, 200, current: true, output);

        // 三个字符，每个 6 像素：4 + (260 - 18) / 2 = 125。
        Assert.Equal(1, output[100 + 125, 200 + 2]);
        Assert.Equal(0, output[100 + 124, 200 + 2]);
    }

    [Fact]
    public void TheDateColumnsKeepTheirOffsets()
    {
        SelectFileView view = Create(files: 0);
        PixelBuffer output = new(640, 480);

        view.RedrawItem(new SaveFileEntry(@"C:\saves\alpha.SAV", "alpha", "Island",
            new DateTime(2024, 3, 9, 7, 5, 0), 0, 0, 0), 100, 200, current: false, output);

        int dateX = 100 + SelectFileView.MaxFileNameWidth + 9;

        Assert.Equal(1, output[dateX + SelectFileView.MonthOffsetX, 202]);
        Assert.Equal(1, output[dateX + SelectFileView.CommaOffsetX, 202]);
        Assert.Equal(1, output[dateX + SelectFileView.ColonOffsetX, 202]);
    }

    private const int RowHeight = 12;

    private static SelectFileView Create(int files, bool isEditing = false, Size? screen = null) =>
        SelectFileView.TryCreate(screen ?? Screen, @"C:\saves", files, SaveFileSortingMethod.Filename, isEditing,
            Artwork(), new FakeGlyphSource(6, RowHeight), new FakeGlyphSource(6, RowHeight))
        ?? throw new InvalidOperationException("The window should have been created.");

    private static SelectFileArtwork Artwork() => new()
    {
        ScrollBarFrames =
        [
            new Sprite(16, 16), new Sprite(16, 16), new Sprite(16, 16), new Sprite(16, 16), new Sprite(16, 16),
        ],
        SortMarkFrames =
        [
            new Sprite(12, 12), new Sprite(12, 12), new Sprite(12, 12), new Sprite(12, 12), new Sprite(12, 12),
            new Sprite(12, 12),
        ],
        ScrollBarBackground = new Sprite(640, 480),
        Buttons = new FakeButtonArtworkSource(),
    };

    private static SaveFileEntry Row(string name, int timestamp) => new(
        Path.Combine(@"C:\saves", name + ".SAV"), name, "Island",
        new DateTime(2024, 3, 9, 7, 5, 0).AddSeconds(timestamp), 0, 0, 0);

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static IEnumerable<string> VisibleNames(SelectFileView view) =>
        view.Files.Select(file => file.Name);

    private sealed class FakeButtonArtworkSource : IDialogButtonArtworkSource
    {
        private readonly DialogButtonArtwork _artwork;

        public FakeButtonArtworkSource()
        {
            Sprite sprite = new(new PixelBuffer(60, 25), 0, 0);
            _artwork = new DialogButtonArtwork(sprite, sprite);
        }

        public DialogButtonArtwork Get(DialogButton button) => _artwork;
    }

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly int _advance;

        public FakeGlyphSource(int advance, int lineHeight)
        {
            _advance = advance;
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

        public int SpaceWidth => _advance;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => _advance;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            // 调色板索引 0 是全透明，测试要看得见断言就把整块涂成 1。
            PixelBuffer image = new(_advance, LineHeight);
            image.AsSpan().Fill(1);

            glyph = new Sprite(image, 0, 0);
            return true;
        }
    }
}
