using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>One entry of the graphics settings window, <c>SelectedWindow</c> of the reference.</summary>
/// <remarks>
/// The five options are leaves of the same shape as the interface page: the caller applies the setting and opens the
/// window again, which is the round trip of <c>openGraphicsSettingsDialog()</c>
/// (<c>dialog_graphics_settings.cpp:238-295</c>).
/// </remarks>
public enum GraphicsSettingsOption
{
    /// <summary>No leaf was chosen this frame.</summary>
    None,

    /// <summary>Pick a new resolution, the reference's <c>Dialog::SelectResolution()</c>.</summary>
    Resolution,

    /// <summary>Toggle between fullscreen and windowed.</summary>
    Mode,

    /// <summary>Toggle between nearest and linear screen scaling.</summary>
    ScreenScalingType,

    /// <summary>Toggle vertical sync.</summary>
    VSync,

    /// <summary>Toggle the system information overlay.</summary>
    SystemInfo,

    /// <summary>The window was closed.</summary>
    Exit,
}

/// <summary>Artwork of <see cref="GraphicsSettingsDialog"/>, gathered by the caller from the assets.</summary>
/// <remarks>
/// Frames follow the reference: <see cref="SpanelFrames"/> is <c>SPANEL</c> (16-17 are the mode icons, 18-19 the V-Sync
/// icons), <see cref="ResolutionFrames"/> is <c>RESOLUTION_ICON</c> (frame 0), <see cref="GameOptionFrames"/> is
/// <c>GAME_OPTION_ICON</c> (2 is linear scaling, 3 nearest) and <see cref="EmptyOptionIconFrames"/> is
/// <c>EMPTY_OPTION_ICON_BACKGROUND</c> (frame 0), the blank icon the system info label is painted on.
/// </remarks>
public sealed class GraphicsSettingsArtwork
{
    /// <summary>Frames of <c>SPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> SpanelFrames { get; init; }

    /// <summary>Frames of <c>RESOLUTION_ICON</c>.</summary>
    public required IReadOnlyList<Sprite> ResolutionFrames { get; init; }

    /// <summary>Frames of <c>GAME_OPTION_ICON</c>.</summary>
    public required IReadOnlyList<Sprite> GameOptionFrames { get; init; }

    /// <summary>Frames of <c>EMPTY_OPTION_ICON_BACKGROUND</c>.</summary>
    public required IReadOnlyList<Sprite> EmptyOptionIconFrames { get; init; }

    /// <summary>Source of the OK button.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window reads was supplied.</summary>
    public bool IsComplete =>
        SpanelFrames.Count > 19 && ResolutionFrames.Count > 0 && GameOptionFrames.Count > 3 &&
        EmptyOptionIconFrames.Count > 0;
}

/// <summary>
/// The graphics page of the system options, <c>fheroes2::openGraphicsSettingsDialog()</c>
/// (<c>src/fheroes2/dialog/dialog_graphics_settings.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// Five options in the reference's grid: resolution, mode and screen scaling type on the top row of three, V-Sync and
/// system info on the second row of two. A left click closes the page with the choice and the caller applies the
/// setting; a right click asks for a <see cref="DialogPopup"/> with the explanation, presented by <see cref="Show"/>.
/// </para>
/// <para>
/// The reference reaches the window through <c>fheroes2::engine()</c> and <c>fheroes2::Display::instance()</c>. The port
/// passes an <see cref="IDisplayControl"/> instead; when it is <see langword="null"/> the page falls back to
/// <see cref="Settings.IsFullScreen"/> and the game size, so it can still be drawn in tests and before a window exists.
/// </para>
/// <para>
/// Deviations: the reference restores the window area and re-renders when a global hot key toggles fullscreen inside its
/// loop (<c>:221-229</c>); <see cref="Show"/> redraws every frame anyway, so only the cached state is compared.
/// <c>Assets::resetScaledBackgroundImages()</c> / <c>resetAllScaledImages()</c> have no counterpart — the port builds
/// scaled images on demand and keeps no global cache.
/// </para>
/// </remarks>
public sealed class GraphicsSettingsDialog
{
    /// <summary>Width of the page, <c>StandardWindow( 289, ... )</c>.</summary>
    public const int Width = 289;

    /// <summary>Height of the page, <c>optionsStepY * 2 + 52</c>.</summary>
    public const int Height = (OptionItem.OptionsStepY * 2) + 52;

    /// <summary>Resolution icon, <c>resolutionRoi</c>.</summary>
    public static readonly Rect ResolutionArea =
        new(OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Mode icon, <c>modeRoi</c>.</summary>
    public static readonly Rect ModeArea = new(
        OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX, OptionItem.OptionsOffsetY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Screen scaling type icon, <c>screenScalingTypeRoi</c>.</summary>
    public static readonly Rect ScreenScalingTypeArea = new(
        OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2), OptionItem.OptionsOffsetY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>V-Sync icon, <c>vSyncRoi</c>.</summary>
    public static readonly Rect VSyncArea = new(
        OptionItem.TwoOptionsOffsetX, OptionItem.OptionsOffsetY + OptionItem.OptionsStepY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>System info icon, <c>systemInfoRoi</c>.</summary>
    public static readonly Rect SystemInfoArea = new(
        OptionItem.TwoOptionsOffsetX + OptionItem.TwoOptionsStepX,
        OptionItem.OptionsOffsetY + OptionItem.OptionsStepY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>
    /// Source rectangle of the mode icon that is magnified in fullscreen, <c>Resize( icon, 6, 6, 53, 53, ... )</c>.
    /// </summary>
    private static readonly Rect ModeIconSourceArea = new(6, 6, 53, 53);

    /// <summary>
    /// Destination rectangle of the magnified mode icon, <c>Resize( ..., icon, 2, 2, 61, 61 )</c>.
    /// </summary>
    private static readonly Rect ModeIconTargetArea = new(2, 2, 61, 61);

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly GraphicsSettingsArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _bodyFont;
    private readonly IDisplayControl? _display;
    private readonly Size _gameSize;
    private readonly Rect _resolutionRoi;
    private readonly Rect _modeRoi;
    private readonly Rect _screenScalingTypeRoi;
    private readonly Rect _vSyncRoi;
    private readonly Rect _systemInfoRoi;

    private DialogPopup? _popup;

    private GraphicsSettingsDialog(Size screen, Size gameSize, IDisplayControl? display,
        GraphicsSettingsArtwork artwork, IGlyphSource titleFont, IGlyphSource bodyFont)
    {
        _gameSize = gameSize;
        _display = display;
        _artwork = artwork;
        _titleFont = titleFont;
        _bodyFont = bodyFont;

        _window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);

        Rect roi = _window.ActiveArea;

        _resolutionRoi = ResolutionArea with { X = ResolutionArea.X + roi.X, Y = ResolutionArea.Y + roi.Y };
        _modeRoi = ModeArea with { X = ModeArea.X + roi.X, Y = ModeArea.Y + roi.Y };
        _screenScalingTypeRoi = ScreenScalingTypeArea with
        {
            X = ScreenScalingTypeArea.X + roi.X,
            Y = ScreenScalingTypeArea.Y + roi.Y,
        };
        _vSyncRoi = VSyncArea with { X = VSyncArea.X + roi.X, Y = VSyncArea.Y + roi.Y };
        _systemInfoRoi = SystemInfoArea with { X = SystemInfoArea.X + roi.X, Y = SystemInfoArea.Y + roi.Y };

        _buttons = ButtonGroup.CreateStandard(roi, DialogButton.Ok, artwork.Buttons);
    }

    /// <summary>The window the page is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OK button of the page.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>Whether a setting was changed since the page opened, the reference's <c>saveConfiguration</c>.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Popup asked for by the last frame, or <see langword="null"/>.</summary>
    public DialogPopup? Popup => _popup;

    /// <summary>Reads <see cref="Popup"/> and clears it.</summary>
    /// <returns>The popup, or <see langword="null"/> when none was asked for.</returns>
    public DialogPopup? TakePopup()
    {
        DialogPopup? popup = _popup;
        _popup = null;
        return popup;
    }

    /// <summary>Area of the resolution option, in screen coordinates.</summary>
    public Rect ResolutionRoi => _resolutionRoi;

    /// <summary>Area of the mode option, in screen coordinates.</summary>
    public Rect ModeRoi => _modeRoi;

    /// <summary>Area of the screen scaling type option, in screen coordinates.</summary>
    public Rect ScreenScalingTypeRoi => _screenScalingTypeRoi;

    /// <summary>Area of the V-Sync option, in screen coordinates.</summary>
    public Rect VSyncRoi => _vSyncRoi;

    /// <summary>Area of the system info option, in screen coordinates.</summary>
    public Rect SystemInfoRoi => _systemInfoRoi;

    /// <summary>
    /// Builds the page, or returns <see langword="null"/> when the artwork is incomplete
    /// (<c>dialog_graphics_settings.cpp:139-152</c>).
    /// </summary>
    /// <param name="screen">Screen to centre on.</param>
    /// <param name="gameSize">Size of the logical frame buffer, the reference's <c>display.width()/height()</c>.</param>
    /// <param name="artwork">Panel frames, icons and the OK button.</param>
    /// <param name="titleFont">Font of the message boxes, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">Font of the message boxes, <c>FontType::normalWhite()</c>.</param>
    /// <param name="display">Window control, or <see langword="null"/> when there is no window.</param>
    /// <returns>The page, or <see langword="null"/> when a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> or a font is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gameSize"/> is not positive.</exception>
    public static GraphicsSettingsDialog? TryCreate(Size screen, Size gameSize, GraphicsSettingsArtwork artwork,
        IGlyphSource titleFont, IGlyphSource bodyFont, IDisplayControl? display = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        if (gameSize.Width <= 0 || gameSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameSize), gameSize, "The game size must be positive.");
        }

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new GraphicsSettingsDialog(screen, gameSize, display, artwork, titleFont, bodyFont);
    }

    /// <summary>Draws the window, the five options and the OK button.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);
        DrawOptions(target);

        _buttons.DrawShadows(target);
        _buttons.Draw(target);
    }

    /// <summary>Handles one frame: the button, the five options and their right clicks.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>
    /// <see cref="GraphicsSettingsOption.Exit"/> when the window has to close, a leaf when the caller has to apply that
    /// setting and reopen the page, <see cref="GraphicsSettingsOption.None"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public GraphicsSettingsOption ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (_buttons.ProcessEvents(events, events.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay : null) ==
                DialogButton.Ok ||
            events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.F10))
        {
            return GraphicsSettingsOption.Exit;
        }

        if (events.MouseClickLeft(_resolutionRoi))
        {
            return GraphicsSettingsOption.Resolution;
        }

        if (events.MouseClickLeft(_modeRoi))
        {
            return GraphicsSettingsOption.Mode;
        }

        if (events.MouseClickLeft(_screenScalingTypeRoi))
        {
            return GraphicsSettingsOption.ScreenScalingType;
        }

        if (events.MouseClickLeft(_vSyncRoi))
        {
            return GraphicsSettingsOption.VSync;
        }

        if (events.MouseClickLeft(_systemInfoRoi))
        {
            return GraphicsSettingsOption.SystemInfo;
        }

        ShowExplanation(events);

        return GraphicsSettingsOption.None;
    }

    /// <summary>
    /// Runs the page until it closes, the round trip of <c>openGraphicsSettingsDialog()</c>
    /// (<c>dialog_graphics_settings.cpp:238-295</c>).
    /// </summary>
    /// <param name="target">Indexed frame buffer the scene draws and renders.</param>
    /// <param name="dialog">The page to run.</param>
    /// <param name="updateUI">Rebuilds the interface after a setting changed, the reference's <c>updateUI()</c>.</param>
    /// <param name="selectResolution">
    /// Opens the resolution window, the reference's <c>Dialog::SelectResolution()</c>; returns whether a new resolution
    /// was chosen. <see langword="null"/> leaves the option inert, which is what a headless port needs.
    /// </param>
    /// <param name="isMainMenuUI">
    /// Whether <paramref name="updateUI"/> redraws the main menu. The reference compares the target of
    /// <c>std::function</c> with <c>drawMainMenuScreen</c> before refreshing after a scaling change (<c>:274-277</c>);
    /// C# has no such comparison, so the caller states it instead.
    /// </param>
    /// <param name="events">Input source; defaults to <see cref="LocalEvent.Current"/>.</param>
    /// <returns>Whether a setting was changed, the reference's <c>saveConfiguration</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/>, <paramref name="dialog"/> or
    /// <paramref name="updateUI"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">There is no event engine to run the loop on.</exception>
    public static bool Show(PixelBuffer target, GraphicsSettingsDialog dialog, Action updateUI,
        Func<bool>? selectResolution = null, bool isMainMenuUI = false, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(updateUI);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine
            ?? throw new InvalidOperationException("The graphics settings window needs an event engine to run.");

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            GraphicsSettingsOption option = dialog.ProcessEvents(eventState);

            if (dialog.TakePopup() is { } popup)
            {
                MessageBox.Show(target, popup.Header, popup.Body, dialog._titleFont, dialog._bodyFont,
                    DialogButton.Ok, dialog._artwork.Buttons, eventState);
            }

            if (option == GraphicsSettingsOption.Exit)
            {
                break;
            }

            if (Apply(dialog, option, updateUI, selectResolution, isMainMenuUI))
            {
                dialog.IsDirty = true;
            }

            dialog.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return dialog.IsDirty;
    }

    /// <summary>
    /// Applies a leaf the way <c>openGraphicsSettingsDialog()</c> does (<c>dialog_graphics_settings.cpp:240-295</c>).
    /// </summary>
    /// <param name="dialog">The page, for the display control it holds.</param>
    /// <param name="option">The leaf that was chosen.</param>
    /// <param name="updateUI">Rebuilds the interface, called after a resolution change and, for the main menu, after a
    /// scaling change.</param>
    /// <param name="selectResolution">Opens the resolution chooser; <see langword="null"/> keeps the option inert.</param>
    /// <param name="isMainMenuUI">Whether <paramref name="updateUI"/> redraws the main menu.</param>
    /// <returns>Whether a setting was changed.</returns>
    public static bool Apply(GraphicsSettingsDialog dialog, GraphicsSettingsOption option, Action updateUI,
        Func<bool>? selectResolution = null, bool isMainMenuUI = false)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(updateUI);

        Settings settings = Settings.Get();

        switch (option)
        {
            case GraphicsSettingsOption.Resolution:
                if (selectResolution is null || !selectResolution())
                {
                    return false;
                }

                updateUI();
                return true;
            case GraphicsSettingsOption.Mode:
                settings.SetFullScreen(!settings.IsFullScreen);
                dialog._display?.SetFullScreen(settings.IsFullScreen);
                return true;
            case GraphicsSettingsOption.ScreenScalingType:
                settings.SetScreenScalingTypeNearest(!settings.IsScreenScalingTypeNearest);
                dialog._display?.ResetRenderer();

                if (isMainMenuUI)
                {
                    updateUI();
                }

                return true;
            case GraphicsSettingsOption.VSync:
                settings.SetVSync(!settings.IsVSyncEnabled);
                return true;
            case GraphicsSettingsOption.SystemInfo:
                settings.SetSystemInfo(!settings.IsSystemInfo);
                return true;
            default:
                return false;
        }
    }

    private void DrawOptions(PixelBuffer target)
    {
        Settings settings = Settings.Get();

        DrawResolution(target);

        Sprite modeIcon = _artwork.SpanelFrames[settings.IsEvilInterfaceEnabled ? 17 : 16];

        if (IsFullScreen)
        {
            // The magnified icon keeps the original transparency layer, exactly like the reference: only the image
            // layer of the copy is resampled.
            Sprite magnified = modeIcon.Clone();
            ImageOps.Resize(modeIcon.Image, ModeIconSourceArea, magnified.Image, ModeIconTargetArea);

            OptionItem.Draw(target, _modeRoi, magnified, "Mode", "Fullscreen", OptionItem.ThreeElementsRowWidth);
        }
        else
        {
            OptionItem.Draw(target, _modeRoi, modeIcon, "Mode", "Windowed", OptionItem.ThreeElementsRowWidth);
        }

        OptionItem.Draw(target, _screenScalingTypeRoi,
            _artwork.GameOptionFrames[settings.IsScreenScalingTypeNearest ? 3 : 2], "Screen Scaling Type",
            settings.IsScreenScalingTypeNearest ? "Nearest" : "Linear", OptionItem.ThreeElementsRowWidth);

        OptionItem.Draw(target, _vSyncRoi, _artwork.SpanelFrames[settings.IsVSyncEnabled ? 18 : 19], "V-Sync",
            settings.IsVSyncEnabled ? "On" : "Off", OptionItem.TwoElementsRowWidth);

        OptionItem.Draw(target, _systemInfoRoi, BuildSystemInfoIcon(settings.IsSystemInfo), "System Info",
            settings.IsSystemInfo ? "On" : "Off", OptionItem.TwoElementsRowWidth);
    }

    /// <summary>Draws the resolution entry, <c>drawResolution</c> of the reference (<c>:68-85</c>).</summary>
    private void DrawResolution(PixelBuffer target)
    {
        Size screenSize = ScreenSize;
        string name;

        if (screenSize.Width != _gameSize.Width || screenSize.Height != _gameSize.Height)
        {
            int integer = screenSize.Width / _gameSize.Width;
            int fraction = ((screenSize.Width * 10) / _gameSize.Width) - (10 * integer);

            name = string.Create(CultureInfo.InvariantCulture,
                $"{_gameSize.Width}x{_gameSize.Height} (x{integer}.{fraction})");
        }
        else
        {
            name = string.Create(CultureInfo.InvariantCulture, $"{_gameSize.Width}x{_gameSize.Height}");
        }

        OptionItem.Draw(target, _resolutionRoi, _artwork.ResolutionFrames[0], "Resolution", name,
            OptionItem.ThreeElementsRowWidth);
    }

    /// <summary>Paints "FPS" or "N/A" onto the blank icon, <c>drawSystemInfo</c> of the reference (<c>:110-125</c>).</summary>
    private Sprite BuildSystemInfoIcon(bool isSystemInfoDisplayed)
    {
        Sprite image = _artwork.EmptyOptionIconFrames[0].Clone();
        Text info = isSystemInfoDisplayed
            ? new Text("FPS", FontType.NormalYellow())
            : new Text("N/A", new FontType(FontSize.Normal, FontColor.Gray));

        info.Draw((image.Width - info.Width()) / 2, (image.Height - info.Height()) / 2, image.Image);

        return image;
    }

    private bool IsFullScreen => _display?.IsFullScreen ?? Settings.Get().IsFullScreen;

    private Size ScreenSize => _display?.ScreenSize ?? _gameSize;

    /// <summary>Fills <see cref="_popup"/> for a right click, the <c>showStandardTextMessage()</c> calls of the reference.</summary>
    private void ShowExplanation(LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(_resolutionRoi))
        {
            _popup = new DialogPopup("Select Game Resolution", "Change the resolution of the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_modeRoi))
        {
            _popup = new DialogPopup("Mode", "Toggle between fullscreen and windowed modes.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_screenScalingTypeRoi))
        {
            _popup = new DialogPopup("Screen Scaling Type",
                "Toggle the type of screen scaling you want to use. Linear scaling makes the resized screen image " +
                "blurry while nearest scaling preserves the sharpness of the original game. The nearest scaling looks " +
                "best on integer scales, for example 2.0x or 3.0x.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_vSyncRoi))
        {
            _popup = new DialogPopup("V-Sync",
                "The V-Sync option can be enabled to resolve flickering issues on some monitors.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_systemInfoRoi))
        {
            _popup = new DialogPopup("System Info", "Show extra information such as FPS and current time.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup("Okay", "Exit this menu.");
        }
    }
}
