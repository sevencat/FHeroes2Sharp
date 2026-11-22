using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The graphics page of the system options (<c>dialog_graphics_settings.cpp</c>): layout, the five leaves and the
/// settings <c>openGraphicsSettingsDialog()</c> applies through the display control.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class GraphicsSettingsDialogTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void WithoutEveryFrameThereIsNoPage()
    {
        Assert.False(Artwork(spanel: 19).IsComplete);
        Assert.Null(GraphicsSettingsDialog.TryCreate(Screen, Screen, Artwork(spanel: 19), Font(), Font()));

        Assert.False(Artwork(resolution: 0).IsComplete);
        Assert.False(Artwork(gameOption: 3).IsComplete);
        Assert.False(Artwork(emptyOption: 0).IsComplete);
        Assert.True(Artwork().IsComplete);
    }

    [Fact]
    public void TheGameSizeHasToBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GraphicsSettingsDialog.TryCreate(Screen, new Size(0, 480), Artwork(), Font(), Font()));
    }

    [Fact]
    public void ThePageIsTheSizeOfTheReferenceWindow()
    {
        GraphicsSettingsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(GraphicsSettingsDialog.Width, active.Width);
        Assert.Equal(GraphicsSettingsDialog.Height, active.Height);
        Assert.Equal(289, active.Width);
        Assert.Equal((OptionItem.OptionsStepY * 2) + 52, active.Height);
    }

    [Fact]
    public void TheFiveIconsSitAtTheReferenceOffsets()
    {
        GraphicsSettingsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(GraphicsSettingsDialog.ResolutionArea.Offset(active.X, active.Y), dialog.ResolutionRoi);
        Assert.Equal(GraphicsSettingsDialog.ModeArea.Offset(active.X, active.Y), dialog.ModeRoi);
        Assert.Equal(
            GraphicsSettingsDialog.ScreenScalingTypeArea.Offset(active.X, active.Y), dialog.ScreenScalingTypeRoi);
        Assert.Equal(GraphicsSettingsDialog.VSyncArea.Offset(active.X, active.Y), dialog.VSyncRoi);
        Assert.Equal(GraphicsSettingsDialog.SystemInfoArea.Offset(active.X, active.Y), dialog.SystemInfoRoi);

        Assert.Equal(65, dialog.ResolutionRoi.Width);
        Assert.Equal(92, dialog.ModeRoi.X - dialog.ResolutionRoi.X);
        Assert.Equal(92, dialog.ScreenScalingTypeRoi.X - dialog.ModeRoi.X);
        Assert.Equal(110, dialog.VSyncRoi.Y - dialog.ResolutionRoi.Y);
        Assert.Equal(118, dialog.SystemInfoRoi.X - dialog.VSyncRoi.X);
    }

    [Fact]
    public void ClickingALeafReturnsIt()
    {
        GraphicsSettingsDialog dialog = Create();

        Assert.Equal(GraphicsSettingsOption.Resolution, Click(dialog, dialog.ResolutionRoi));
        Assert.Equal(GraphicsSettingsOption.Mode, Click(dialog, dialog.ModeRoi));
        Assert.Equal(GraphicsSettingsOption.ScreenScalingType, Click(dialog, dialog.ScreenScalingTypeRoi));
        Assert.Equal(GraphicsSettingsOption.VSync, Click(dialog, dialog.VSyncRoi));
        Assert.Equal(GraphicsSettingsOption.SystemInfo, Click(dialog, dialog.SystemInfoRoi));
    }

    [Fact]
    public void EnterLeavesThePage()
    {
        GraphicsSettingsDialog dialog = Create();

        LocalEvent enter = new();
        enter.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(GraphicsSettingsOption.Exit, dialog.ProcessEvents(enter));

        LocalEvent escape = new();
        escape.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(GraphicsSettingsOption.Exit, dialog.ProcessEvents(escape));
    }

    [Fact]
    public void RightClicksAskForTheExplanations()
    {
        GraphicsSettingsDialog dialog = Create();

        RightClick(dialog, dialog.ScreenScalingTypeRoi);

        DialogPopup first = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Screen Scaling Type", first.Header);
        Assert.Contains("nearest", first.Body, StringComparison.Ordinal);
        Assert.NotNull(dialog.TakePopup());
        Assert.Null(dialog.Popup);

        RightClick(dialog, dialog.SystemInfoRoi);

        DialogPopup second = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("System Info", second.Header);
    }

    [Fact]
    public void TheModeSwitchGoesToTheDisplay()
    {
        Settings settings = Settings.Get();
        bool wasFullScreen = settings.IsFullScreen;

        try
        {
            FakeDisplayControl display = new(wasFullScreen);
            GraphicsSettingsDialog dialog = Create(display);

            bool dirty = GraphicsSettingsDialog.Apply(dialog, GraphicsSettingsOption.Mode, () => { });

            Assert.True(dirty);
            Assert.Equal(!wasFullScreen, settings.IsFullScreen);
            Assert.Equal(settings.IsFullScreen, display.IsFullScreen);
        }
        finally
        {
            settings.SetFullScreen(wasFullScreen);
        }
    }

    [Fact]
    public void TheScalingSwitchResetsTheRendererOnlyForTheMainMenu()
    {
        Settings settings = Settings.Get();
        bool wasNearest = settings.IsScreenScalingTypeNearest;
        int rebuilds = 0;
        FakeDisplayControl display = new(settings.IsFullScreen);
        GraphicsSettingsDialog dialog = Create(display);

        try
        {
            Assert.True(GraphicsSettingsDialog.Apply(
                dialog, GraphicsSettingsOption.ScreenScalingType, () => ++rebuilds));

            Assert.NotEqual(wasNearest, settings.IsScreenScalingTypeNearest);
            Assert.Equal(1, display.RendererResets);
            Assert.Equal(0, rebuilds);

            // 参考只在 updateUI 指向主菜单时才重画（dialog_graphics_settings.cpp:274-277）。
            Assert.True(GraphicsSettingsDialog.Apply(
                dialog, GraphicsSettingsOption.ScreenScalingType, () => ++rebuilds, isMainMenuUI: true));

            Assert.Equal(2, display.RendererResets);
            Assert.Equal(1, rebuilds);
        }
        finally
        {
            settings.SetScreenScalingTypeNearest(wasNearest);
        }
    }

    [Fact]
    public void VsyncAndSystemInfoAreToggledInPlace()
    {
        Settings settings = Settings.Get();
        bool wasVSync = settings.IsVSyncEnabled;
        bool wasSystemInfo = settings.IsSystemInfo;
        GraphicsSettingsDialog dialog = Create();

        try
        {
            Assert.True(GraphicsSettingsDialog.Apply(dialog, GraphicsSettingsOption.VSync, () => { }));
            Assert.NotEqual(wasVSync, settings.IsVSyncEnabled);

            Assert.True(GraphicsSettingsDialog.Apply(dialog, GraphicsSettingsOption.SystemInfo, () => { }));
            Assert.NotEqual(wasSystemInfo, settings.IsSystemInfo);
        }
        finally
        {
            settings.SetVSync(wasVSync);
            settings.SetSystemInfo(wasSystemInfo);
        }
    }

    [Fact]
    public void TheResolutionLeafRebuildsTheInterfaceOnlyWhenSomethingWasChosen()
    {
        int rebuilds = 0;
        GraphicsSettingsDialog dialog = Create();

        Assert.True(GraphicsSettingsDialog.Apply(
            dialog, GraphicsSettingsOption.Resolution, () => ++rebuilds, () => true));
        Assert.Equal(1, rebuilds);

        Assert.False(GraphicsSettingsDialog.Apply(
            dialog, GraphicsSettingsOption.Resolution, () => ++rebuilds, () => false));
        Assert.Equal(1, rebuilds);
    }

    private static GraphicsSettingsDialog Create(IDisplayControl? display = null) =>
        GraphicsSettingsDialog.TryCreate(Screen, Screen, Artwork(), Font(), Font(), display)
        ?? throw new InvalidOperationException("The page should have been created.");

    private static GraphicsSettingsOption Click(GraphicsSettingsDialog dialog, Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + 4, area.Y + 4);

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return dialog.ProcessEvents(events);
    }

    private static GraphicsSettingsOption RightClick(GraphicsSettingsDialog dialog, Rect area)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, new Point(area.X + 4, area.Y + 4));

        return dialog.ProcessEvents(events);
    }

    private static GraphicsSettingsArtwork Artwork(int spanel = 20, int resolution = 1, int gameOption = 4,
        int emptyOption = 1) =>
        new()
        {
            SpanelFrames = [.. Enumerable.Range(0, spanel).Select(_ => new Sprite(65, 65))],
            ResolutionFrames = [.. Enumerable.Range(0, resolution).Select(_ => new Sprite(65, 65))],
            GameOptionFrames = [.. Enumerable.Range(0, gameOption).Select(_ => new Sprite(65, 65))],
            EmptyOptionIconFrames = [.. Enumerable.Range(0, emptyOption).Select(_ => new Sprite(65, 65))],
            Buttons = new FakeButtonArtworkSource(),
        };

    private static FakeGlyphSource Font() => new(6, 12);

    private sealed class FakeDisplayControl : IDisplayControl
    {
        public FakeDisplayControl(bool isFullScreen) => IsFullScreen = isFullScreen;

        public bool IsFullScreen { get; private set; }

        public Size ScreenSize => new(640, 480);

        public int RendererResets { get; private set; }

        public ResolutionInfo? Applied { get; private set; }

        public IReadOnlyList<ResolutionInfo> GetAvailableResolutions() => [new ResolutionInfo(640, 480, 640, 480)];

        public void SetFullScreen(bool enable) => IsFullScreen = enable;

        public void SetResolution(ResolutionInfo resolution) => Applied = resolution;

        public void ResetRenderer() => ++RendererResets;
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
            glyph = new Sprite(new PixelBuffer(_advance, LineHeight), 0, 0);
            return true;
        }
    }
}
