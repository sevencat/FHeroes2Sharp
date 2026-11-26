using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The options window of the map editor (<c>editor_options.cpp</c>): the three by three grid, the four leaves, and the
/// five settings the caller applies (the window itself changes nothing).
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorOptionsDialogTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void WithoutEveryFrameThereIsNoWindow()
    {
        Assert.False(Artwork(spanel: 21).IsComplete);
        Assert.Null(EditorOptionsDialog.TryCreate(Screen, Artwork(spanel: 21)));

        Assert.False(Artwork(cspanel: 2).IsComplete);
        Assert.False(Artwork(gameOption: 1).IsComplete);
        Assert.False(Artwork(espanel: 5).IsComplete);
        Assert.True(Artwork().IsComplete);
    }

    [Fact]
    public void TheWindowIsTheSizeOfTheReference()
    {
        EditorOptionsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(289, EditorOptionsDialog.Width);
        Assert.Equal((OptionItem.OptionsStepY * 3) + 52, EditorOptionsDialog.Height);
        Assert.Equal(EditorOptionsDialog.Width, active.Width);
        Assert.Equal(EditorOptionsDialog.Height, active.Height);
    }

    [Fact]
    public void TheNineIconsSitAtTheReferenceOffsets()
    {
        EditorOptionsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(EditorOptionsDialog.LanguageArea.Offset(active.X, active.Y), dialog.LanguageRoi);
        Assert.Equal(EditorOptionsDialog.GraphicsArea.Offset(active.X, active.Y), dialog.GraphicsRoi);
        Assert.Equal(EditorOptionsDialog.AudioArea.Offset(active.X, active.Y), dialog.AudioRoi);
        Assert.Equal(EditorOptionsDialog.HotKeysArea.Offset(active.X, active.Y), dialog.HotKeysRoi);
        Assert.Equal(EditorOptionsDialog.AnimationArea.Offset(active.X, active.Y), dialog.AnimationRoi);
        Assert.Equal(EditorOptionsDialog.PassabilityArea.Offset(active.X, active.Y), dialog.PassabilityRoi);
        Assert.Equal(EditorOptionsDialog.InterfaceTypeArea.Offset(active.X, active.Y), dialog.InterfaceTypeRoi);
        Assert.Equal(EditorOptionsDialog.CursorTypeArea.Offset(active.X, active.Y), dialog.CursorTypeRoi);
        Assert.Equal(EditorOptionsDialog.ScrollSpeedArea.Offset(active.X, active.Y), dialog.ScrollSpeedRoi);

        // 三格一行：两个邻居之间隔一格步长，下一行落一个行高。
        Assert.Equal(OptionItem.ThreeOptionsStepX, dialog.GraphicsRoi.X - dialog.LanguageRoi.X);
        Assert.Equal(OptionItem.ThreeOptionsStepX, dialog.AudioRoi.X - dialog.GraphicsRoi.X);
        Assert.Equal(OptionItem.OptionsStepY, dialog.HotKeysRoi.Y - dialog.LanguageRoi.Y);
        Assert.Equal(OptionItem.OptionsStepY, dialog.InterfaceTypeRoi.Y - dialog.HotKeysRoi.Y);
        Assert.Equal(dialog.LanguageRoi.X, dialog.HotKeysRoi.X);
        Assert.Equal(dialog.GraphicsRoi.X, dialog.AnimationRoi.X);
        Assert.Equal(dialog.AudioRoi.X, dialog.PassabilityRoi.X);
    }

    [Fact]
    public void ClickingAnEntryThatOpensAPageReturnsIt()
    {
        EditorOptionsDialog dialog = Create();

        Assert.Equal(EditorOptionsAction.Language, Click(dialog, dialog.LanguageRoi));
        Assert.Equal(EditorOptionsAction.Graphics, Click(dialog, dialog.GraphicsRoi));
        Assert.Equal(EditorOptionsAction.AudioSettings, Click(dialog, dialog.AudioRoi));
        Assert.Equal(EditorOptionsAction.HotKeys, Click(dialog, dialog.HotKeysRoi));
    }

    [Fact]
    public void TheFiveSettingsAreOnlyReported()
    {
        Settings settings = Settings.Get();
        bool wasAnimation = settings.IsEditorAnimationEnabled;
        bool wasPassability = settings.IsEditorPassabilityEnabled;
        bool wasCursor = settings.IsMonochromeCursor;
        AdventureMapScrollSpeed wasSpeed = settings.ScrollSpeed;

        EditorOptionsDialog dialog = Create();

        try
        {
            // 参考把这几项交给 openEditorSettings() 去做：动画与通行性开关要重画地图，界面风格要重建面板。
            Assert.Equal(EditorOptionsAction.Animation, Click(dialog, dialog.AnimationRoi));
            Assert.Equal(EditorOptionsAction.Passability, Click(dialog, dialog.PassabilityRoi));
            Assert.Equal(EditorOptionsAction.InterfaceType, Click(dialog, dialog.InterfaceTypeRoi));
            Assert.Equal(EditorOptionsAction.CursorType, Click(dialog, dialog.CursorTypeRoi));
            Assert.Equal(EditorOptionsAction.ScrollSpeed, Click(dialog, dialog.ScrollSpeedRoi));

            Assert.Equal(wasAnimation, settings.IsEditorAnimationEnabled);
            Assert.Equal(wasPassability, settings.IsEditorPassabilityEnabled);
            Assert.Equal(wasCursor, settings.IsMonochromeCursor);
            Assert.Equal(wasSpeed, settings.ScrollSpeed);
        }
        finally
        {
            settings.SetEditorAnimation(wasAnimation);
            settings.SetEditorPassability(wasPassability);
            settings.SetMonochromeCursor(wasCursor);
            settings.SetScrollSpeed((int)wasSpeed);
        }
    }

    [Fact]
    public void TheWheelOverTheScrollSpeedStepsIt()
    {
        EditorOptionsDialog dialog = Create();

        Assert.Equal(EditorOptionsAction.IncreaseScrollSpeed, Wheel(dialog, dialog.ScrollSpeedRoi, up: true));
        Assert.Equal(EditorOptionsAction.DecreaseScrollSpeed, Wheel(dialog, dialog.ScrollSpeedRoi, up: false));
    }

    [Fact]
    public void EnterLeavesTheWindow()
    {
        EditorOptionsDialog dialog = Create();

        LocalEvent enter = new();
        enter.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(EditorOptionsAction.Exit, dialog.ProcessEvents(enter));

        LocalEvent escape = new();
        escape.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(EditorOptionsAction.Exit, dialog.ProcessEvents(escape));
    }

    [Fact]
    public void RightClicksAskForTheExplanations()
    {
        EditorOptionsDialog dialog = Create();

        RightClick(dialog, dialog.AnimationRoi);

        DialogPopup first = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Animation", first.Header);
        Assert.NotNull(dialog.TakePopup());
        Assert.Null(dialog.Popup);

        RightClick(dialog, dialog.PassabilityRoi);

        DialogPopup second = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Passability", second.Header);
    }

    private static EditorOptionsDialog Create() =>
        EditorOptionsDialog.TryCreate(Screen, Artwork())
        ?? throw new InvalidOperationException("The window should have been created.");

    private static EditorOptionsAction Click(EditorOptionsDialog dialog, Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + 4, area.Y + 4);

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return dialog.ProcessEvents(events);
    }

    private static EditorOptionsAction RightClick(EditorOptionsDialog dialog, Rect area)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, new Point(area.X + 4, area.Y + 4));

        return dialog.ProcessEvents(events);
    }

    private static EditorOptionsAction Wheel(EditorOptionsDialog dialog, Rect area, bool up)
    {
        LocalEvent events = new();

        events.OnMouseMotion(new Point(area.X + 4, area.Y + 4));
        events.OnMouseWheel(new Point(0, up ? 1 : -1));

        return dialog.ProcessEvents(events);
    }

    private static EditorOptionsArtwork Artwork(int spanel = 22, int cspanel = 8, int gameOption = 2, int espanel = 6) =>
        new()
        {
            SpanelFrames = [.. Enumerable.Range(0, spanel).Select(_ => new Sprite(65, 65))],
            CspanelFrames = [.. Enumerable.Range(0, cspanel).Select(_ => new Sprite(65, 65))],
            GameOptionFrames = [.. Enumerable.Range(0, gameOption).Select(_ => new Sprite(65, 65))],
            EspanelFrames = [.. Enumerable.Range(0, espanel).Select(_ => new Sprite(65, 65))],
            Buttons = new FakeButtonArtworkSource(),
        };

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
}
