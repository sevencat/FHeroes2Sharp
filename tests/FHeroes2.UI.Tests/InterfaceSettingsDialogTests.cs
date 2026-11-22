using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The interface page of the system options (<c>dialog_interface_settings.cpp</c>): layout, the five leaves and the
/// in-place scroll speed cycling.
/// </summary>
public sealed class InterfaceSettingsDialogTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void WithoutEveryFrameThereIsNoPage()
    {
        Assert.False(Artwork(spanel: 21).IsComplete);
        Assert.Null(InterfaceSettingsDialog.TryCreate(Screen, Artwork(spanel: 21), Font(), Font()));

        Assert.False(Artwork(cspanel: 2).IsComplete);
        Assert.False(Artwork(espanel: 4).IsComplete);
        Assert.False(Artwork(army: 1).IsComplete);
        Assert.True(Artwork().IsComplete);
    }

    [Fact]
    public void ThePageIsTheSizeOfTheReferenceWindow()
    {
        InterfaceSettingsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(InterfaceSettingsDialog.Width, active.Width);
        Assert.Equal(InterfaceSettingsDialog.Height, active.Height);
        Assert.Equal(OptionItem.OptionsStepY * 2 + 52, active.Height);
    }

    [Fact]
    public void TheFiveIconsSitAtTheReferenceOffsets()
    {
        InterfaceSettingsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(InterfaceSettingsDialog.InterfaceTypeArea.Offset(active.X, active.Y), dialog.InterfaceTypeRoi);
        Assert.Equal(
            InterfaceSettingsDialog.InterfacePresenceArea.Offset(active.X, active.Y), dialog.InterfacePresenceRoi);
        Assert.Equal(InterfaceSettingsDialog.CursorTypeArea.Offset(active.X, active.Y), dialog.CursorTypeRoi);
        Assert.Equal(InterfaceSettingsDialog.ScrollSpeedArea.Offset(active.X, active.Y), dialog.ScrollSpeedRoi);
        Assert.Equal(
            InterfaceSettingsDialog.ArmyEstimationArea.Offset(active.X, active.Y), dialog.ArmyEstimationRoi);

        Assert.Equal(65, dialog.InterfaceTypeRoi.Width);
        Assert.Equal(92, dialog.InterfacePresenceRoi.X - dialog.InterfaceTypeRoi.X);
        Assert.Equal(92, dialog.ArmyEstimationRoi.X - dialog.InterfacePresenceRoi.X);
        Assert.Equal(110, dialog.CursorTypeRoi.Y - dialog.InterfaceTypeRoi.Y);
        Assert.Equal(118, dialog.ScrollSpeedRoi.X - dialog.CursorTypeRoi.X);
    }

    [Fact]
    public void ClickingALeafReturnsIt()
    {
        InterfaceSettingsDialog dialog = Create();

        Assert.Equal(InterfaceSettingsOption.InterfaceType, Click(dialog, dialog.InterfaceTypeRoi));
        Assert.Equal(InterfaceSettingsOption.InterfacePresence, Click(dialog, dialog.InterfacePresenceRoi));
        Assert.Equal(InterfaceSettingsOption.CursorType, Click(dialog, dialog.CursorTypeRoi));
        Assert.Equal(InterfaceSettingsOption.ArmyEstimationMode, Click(dialog, dialog.ArmyEstimationRoi));
    }

    [Fact]
    public void ClickingTheScrollSpeedCyclesItInPlace()
    {
        Settings settings = Settings.Get();
        Settings.Get().SetScrollSpeed((int)AdventureMapScrollSpeed.Normal);
        InterfaceSettingsDialog dialog = Create();

        Assert.Equal(InterfaceSettingsOption.None, Click(dialog, dialog.ScrollSpeedRoi));
        Assert.Equal(AdventureMapScrollSpeed.Fast, settings.ScrollSpeed);
        Assert.True(dialog.IsDirty);

        Click(dialog, dialog.ScrollSpeedRoi);
        Assert.Equal(AdventureMapScrollSpeed.VeryFast, settings.ScrollSpeed);

        // 到顶后回到 Off，参考的 `% ( SCROLL_SPEED_VERY_FAST + 1 )`。
        Click(dialog, dialog.ScrollSpeedRoi);
        Assert.Equal(AdventureMapScrollSpeed.None, settings.ScrollSpeed);

        settings.SetScrollSpeed((int)AdventureMapScrollSpeed.Normal);
    }

    [Fact]
    public void TheWheelOverTheScrollSpeedStepsIt()
    {
        Settings settings = Settings.Get();
        settings.SetScrollSpeed((int)AdventureMapScrollSpeed.Normal);
        InterfaceSettingsDialog dialog = Create();

        Assert.Equal(InterfaceSettingsOption.None, Wheel(dialog, dialog.ScrollSpeedRoi, up: true));
        Assert.Equal(AdventureMapScrollSpeed.Fast, settings.ScrollSpeed);

        Wheel(dialog, dialog.ScrollSpeedRoi, up: false);
        Assert.Equal(AdventureMapScrollSpeed.Normal, settings.ScrollSpeed);

        settings.SetScrollSpeed((int)AdventureMapScrollSpeed.Normal);
    }

    [Fact]
    public void EnterLeavesThePage()
    {
        InterfaceSettingsDialog dialog = Create();

        LocalEvent enter = new();
        enter.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(InterfaceSettingsOption.Exit, dialog.ProcessEvents(enter));

        LocalEvent escape = new();
        escape.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(InterfaceSettingsOption.Exit, dialog.ProcessEvents(escape));
    }

    [Fact]
    public void RightClicksAskForTheExplanations()
    {
        InterfaceSettingsDialog dialog = Create();

        RightClick(dialog, dialog.InterfaceTypeRoi);

        DialogPopup first = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Interface Type", first.Header);
        Assert.NotNull(dialog.TakePopup());
        Assert.Null(dialog.Popup);

        RightClick(dialog, dialog.ArmyEstimationRoi);

        DialogPopup second = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Army Estimation", second.Header);
        Assert.Contains("Numeric", second.Body, StringComparison.Ordinal);
    }

    private static InterfaceSettingsDialog Create() =>
        InterfaceSettingsDialog.TryCreate(Screen, Artwork(), Font(), Font())
            ?? throw new InvalidOperationException("The page should have been created.");

    private static InterfaceSettingsOption Click(InterfaceSettingsDialog dialog, Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + 4, area.Y + 4);

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return dialog.ProcessEvents(events);
    }

    private static InterfaceSettingsOption RightClick(InterfaceSettingsDialog dialog, Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + 4, area.Y + 4);

        events.OnMouseButton(true, MouseButtonType.Right, position);

        return dialog.ProcessEvents(events);
    }

    private static InterfaceSettingsOption Wheel(InterfaceSettingsDialog dialog, Rect area, bool up)
    {
        LocalEvent events = new();

        events.OnMouseMotion(new Point(area.X + 4, area.Y + 4));
        events.OnMouseWheel(new Point(0, up ? 1 : -1));

        return dialog.ProcessEvents(events);
    }

    private static InterfaceSettingsArtwork Artwork(int spanel = 22, int cspanel = 5, int espanel = 5, int army = 2) =>
        new()
        {
            SpanelFrames = [.. Enumerable.Range(0, spanel).Select(_ => new Sprite(65, 65))],
            CspanelFrames = [.. Enumerable.Range(0, cspanel).Select(_ => new Sprite(65, 65))],
            EvilSpanelFrames = [.. Enumerable.Range(0, espanel).Select(_ => new Sprite(65, 65))],
            ArmyEstimationFrames = [.. Enumerable.Range(0, army).Select(_ => new Sprite(65, 65))],
            Buttons = new FakeButtonArtworkSource(),
        };

    private static FakeGlyphSource Font() => new(6, 12);

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
            glyph = new Sprite(new PixelBuffer(_advance, LineHeight), 0, 0);
            return true;
        }
    }
}
