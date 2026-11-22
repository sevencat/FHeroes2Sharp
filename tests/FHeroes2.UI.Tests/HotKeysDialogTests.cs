using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The hot key window of <c>openHotkeysDialog()</c> (<c>dialog_hotkeys.cpp:307-483</c>): layout, category filtering,
/// the wait for a new key and the OK/Escape leaves.
/// </summary>
/// <remarks>
/// The table behind the window is static, so this class shares a collection with <see cref="HotKeysTests"/> and
/// restores the defaults after every test.
/// </remarks>
[Collection(HotKeysCollection.Name)]
public sealed class HotKeysDialogTests
{
    private static readonly Size Screen = new(640, 480);

    private static readonly HotKeyRow[] SampleRows =
    [
        new(HotKeyEvent.WorldLeft, HotKeyCategory.WorldMap),
        new(HotKeyEvent.WorldRight, HotKeyCategory.WorldMap),
        new(HotKeyEvent.BattleRetreat, HotKeyCategory.Battle),
    ];

    [Fact]
    public void WithoutAllScrollBarFramesThereIsNoWindow()
    {
        HotKeysArtwork artwork = Artwork(4);

        Assert.False(artwork.IsComplete);
        Assert.Null(HotKeysDialog.TryCreate(Screen, artwork, Font(), Font(), Font(), SampleRows));
    }

    [Fact]
    public void WithoutRowsThereIsNoWindow()
    {
        Assert.Null(HotKeysDialog.TryCreate(Screen, Artwork(5), Font(), Font(), Font(), []));
    }

    [Fact]
    public void TheWindowTakesTheScreenHeightMinusTheMargin()
    {
        HotKeysDialog dialog = Create();

        // 参考：std::max( display.height() - 100, DefaultDisplayHeight )，480 的屏幕于是取 480。
        Assert.Equal(Math.Max(Screen.Height - 100, HotKeysDialog.DefaultDisplayHeight),
            dialog.Window.ActiveArea.Height);

        HotKeysDialog tall = Create(screen: new Size(640, 900));

        Assert.Equal(900 - 100, tall.Window.ActiveArea.Height);
    }

    [Fact]
    public void TheWindowNeverShrinksBelowTheReferenceHeight()
    {
        // 屏幕只有 300 高时，参考仍然给窗口 480。
        HotKeysDialog dialog = Create(screen: new Size(640, 300));

        Assert.Equal(HotKeysDialog.DefaultDisplayHeight, dialog.Window.ActiveArea.Height);
    }

    [Fact]
    public void TheThreeColumnsSitWhereTheReferencePutsThem()
    {
        HotKeysDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(new Rect(
            active.X + HotKeysDialog.ListInsetX,
            active.Y + HotKeysDialog.ListTop,
            HotKeysDialog.MinCategoryWidth + HotKeysDialog.CategoryExtraWidth,
            active.Height - HotKeysDialog.ListHeightDeduction), dialog.CategoryArea);

        Assert.Equal(dialog.CategoryArea.Right, dialog.ListArea.X);
        Assert.Equal(dialog.CategoryArea.Y, dialog.ListArea.Y);
        Assert.Equal(dialog.CategoryArea.Height, dialog.ListArea.Height);
    }

    [Fact]
    public void TheWindowIsWideEnoughForTheThreeColumnsAndTheMargins()
    {
        HotKeysDialog dialog = Create();

        // 两块底板的宽度反推出三列：窗口宽 = 三列之和 + dialogExtraWidth。
        int threeColumns = (dialog.CategoryArea.Width - HotKeysDialog.CategoryExtraWidth) +
            (dialog.ListArea.Width - HotKeysDialog.HotKeyExtraWidth);

        Assert.Equal(threeColumns + HotKeysDialog.DialogExtraWidth, dialog.Window.ActiveArea.Width);
    }

    [Fact]
    public void EveryEventIsListedByDefault()
    {
        GameHotKeys.Reset();

        HotKeysDialog dialog = HotKeysDialog.TryCreate(Screen, Artwork(5), Font(), Font(), Font())
            ?? throw new InvalidOperationException("The window should have been created.");

        Assert.Equal(GameHotKeys.GetAllHotKeyEvents().Count, dialog.AllRows.Count);
        Assert.Equal(dialog.AllRows.Count, dialog.Rows.Count);
        Assert.Null(dialog.SelectedCategory);
    }

    [Fact]
    public void SelectingACategoryFiltersTheRows()
    {
        HotKeysDialog dialog = Create();

        dialog.SetCategory(HotKeyCategory.WorldMap);

        Assert.Equal(HotKeyCategory.WorldMap, dialog.SelectedCategory);
        Assert.Equal(2, dialog.Rows.Count);
        Assert.All(dialog.Rows, row => Assert.Equal(HotKeyCategory.WorldMap, row.Category));

        dialog.SetCategory(null);

        Assert.Equal(3, dialog.Rows.Count);
    }

    [Fact]
    public void ClickingTheSelectedCategoryChangesNothing()
    {
        HotKeysDialog dialog = Create();
        dialog.SetCategory(HotKeyCategory.Battle);

        // 分类列：All Categories 之后每个枚举值一行，行高 = 字体行高 + CategoryGap。
        int lineHeight = dialog.RowFont.LineHeight + HotKeysDialog.CategoryGap;
        int battleOffset = HotKeysDialog.CategoryTopSlop + (1 + (int)HotKeyCategory.Battle) * lineHeight;

        LocalEvent click = Click(dialog.CategoryArea.X + 4, dialog.CategoryArea.Y + battleOffset + 1);
        dialog.ProcessEvents(click);

        Assert.Equal(HotKeyCategory.Battle, dialog.SelectedCategory);
        Assert.Single(dialog.Rows);
    }

    [Fact]
    public void ASelectedRowStartsWaitingForAKey()
    {
        HotKeysDialog dialog = Create();

        dialog.BeginWaitingForHotKey(HotKeyEvent.WorldLeft);

        Assert.True(dialog.IsWaitingForHotKey);
        Assert.Equal(HotKeyEvent.WorldLeft, dialog.WaitingEvent);

        dialog.CancelWaitingForHotKey();

        Assert.False(dialog.IsWaitingForHotKey);
    }

    [Fact]
    public void TheNextPressedKeyBecomesTheBinding()
    {
        GameHotKeys.Reset();
        HotKeysDialog dialog = Create();
        dialog.BeginWaitingForHotKey(HotKeyEvent.WorldLeft);

        LocalEvent pressed = new();
        pressed.OnKeyboard(Key.F7, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(DialogButton.None, dialog.ProcessEvents(pressed));
        Assert.False(dialog.IsWaitingForHotKey);
        Assert.Equal(Key.F7, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldLeft));
        Assert.True(dialog.IsDirty);

        GameHotKeys.Reset();
    }

    [Fact]
    public void EnterWhileWaitingDoesNotCloseTheWindow()
    {
        GameHotKeys.Reset();
        HotKeysDialog dialog = Create();
        dialog.BeginWaitingForHotKey(HotKeyEvent.WorldRight);

        LocalEvent enter = new();
        enter.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(DialogButton.None, dialog.ProcessEvents(enter));
        Assert.Equal(Key.Enter, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldRight));

        GameHotKeys.Reset();
    }

    [Fact]
    public void EnterLeavesTheWindow()
    {
        HotKeysDialog dialog = Create();

        LocalEvent enter = new();
        enter.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(DialogButton.Ok, dialog.ProcessEvents(enter));

        // 单按钮窗口把 Cancel 热键也当 OK，参考的 ButtonGroup::ProcessHotKey() 就是这样（ui_button.cpp:520-526），
        // 所以 Escape 同样关窗，Show 对两者都返回。
        LocalEvent escape = new();
        escape.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(DialogButton.Ok, dialog.ProcessEvents(escape));
    }

    [Fact]
    public void ADoubleClickStartsWaitingThroughTheRowHandler()
    {
        HotKeysDialog dialog = Create();

        dialog.ActionListDoubleClick(SampleRows[0]);

        Assert.True(dialog.IsWaitingForHotKey);
        Assert.Equal(HotKeyEvent.WorldLeft, dialog.WaitingEvent);
    }

    [Fact]
    public void TheRightClickFillsAPopupWithTheRowDetails()
    {
        HotKeysDialog dialog = Create();

        dialog.ActionListPressRight(SampleRows[0]);

        DialogPopup popup = dialog.Popup ?? throw new InvalidOperationException("The right click should ask for a popup.");

        Assert.Contains("World Map", popup.Header, StringComparison.Ordinal);
        Assert.Contains("world map left", popup.Body, StringComparison.Ordinal);

        Assert.NotNull(dialog.TakePopup());
        Assert.Null(dialog.Popup);
    }

    [Fact]
    public void BeginWaitingRejectsPseudoEvents()
    {
        HotKeysDialog dialog = Create();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => dialog.BeginWaitingForHotKey(HotKeyEvent.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => dialog.BeginWaitingForHotKey(HotKeyEvent.NoEvent));
    }

    [Fact]
    public void CommittingWithoutWaitingChangesNothing()
    {
        GameHotKeys.Reset();
        HotKeysDialog dialog = Create();

        dialog.CommitHotKey(Key.F1);

        Assert.Equal(Key.KeyPad4, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldLeft));
        Assert.False(dialog.IsDirty);

        GameHotKeys.Reset();
    }

    private static HotKeysDialog Create(Size? screen = null, IReadOnlyList<HotKeyRow>? rows = null)
    {
        Size target = screen ?? Screen;

        return HotKeysDialog.TryCreate(target, Artwork(5), Font(), Font(), Font(), rows ?? SampleRows)
            ?? throw new InvalidOperationException("The window should have been created.");
    }

    private static HotKeysArtwork Artwork(int frames) => new()
    {
        ScrollBarFrames = [.. Enumerable.Range(0, frames).Select(_ => new Sprite(16, 16))],
        ScrollBarBackground = new Sprite(640, 480),
        Buttons = new FakeButtonArtworkSource(),
    };

    private static FakeGlyphSource Font() => new(6, 12);

    private static LocalEvent Click(int x, int y)
    {
        LocalEvent events = new();
        Point position = new(x, y);

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

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
            PixelBuffer image = new(_advance, LineHeight);

            for (int y = 0; y < image.Height; ++y)
            {
                for (int x = 0; x < image.Width; ++x)
                {
                    image[x, y] = 1;
                }
            }

            glyph = new Sprite(image, 0, 0);
            return true;
        }
    }
}

/// <summary>Serialises the tests that rewrite the static hot key table.</summary>
[CollectionDefinition(HotKeysCollection.Name, DisableParallelization = true)]
public sealed class HotKeysCollection
{
    /// <summary>Name both test classes pass to <see cref="CollectionAttribute"/>.</summary>
    public const string Name = "hot keys";
}
