using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>One entry of the interface settings window, <c>SelectedWindow</c> of the reference.</summary>
/// <remarks>
/// The five options are leaves: the caller changes the setting and calls the window again, which is the
/// <c>updateUI()</c> round trip of <c>openInterfaceSettingsDialog()</c> (<c>dialog_interface_settings.cpp:226-260</c>).
/// Scroll speed is the exception and is changed in place.
/// </remarks>
public enum InterfaceSettingsOption
{
    /// <summary>No leaf was chosen this frame.</summary>
    None,

    /// <summary>Cycle the interface set, <c>Settings.SwitchToNextInterfaceType()</c>.</summary>
    InterfaceType,

    /// <summary>Show or hide the adventure interface.</summary>
    InterfacePresence,

    /// <summary>Switch between the colour and the black and white cursor.</summary>
    CursorType,

    /// <summary>Switch between canonical and numeric army sizes.</summary>
    ArmyEstimationMode,

    /// <summary>The window was closed.</summary>
    Exit,
}

/// <summary>Artwork of <see cref="InterfaceSettingsDialog"/>, gathered by the caller from the assets.</summary>
/// <remarks>
/// Frames follow the reference: <see cref="SpanelFrames"/> is <c>SPANEL</c> (22 frames are used, 9, 15-17 and 20-21
/// matter), <see cref="CspanelFrames"/> is <c>CSPANEL</c> (0-2), <see cref="EvilSpanelFrames"/> is <c>ESPANEL</c>
/// (frame 4 is the hidden interface icon) and <see cref="ArmyEstimationFrames"/> is <c>ARMY_ESTIMATION_ICON</c> (0-1).
/// </remarks>
public sealed class InterfaceSettingsArtwork
{
    /// <summary>Frames of <c>SPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> SpanelFrames { get; init; }

    /// <summary>Frames of <c>CSPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> CspanelFrames { get; init; }

    /// <summary>Frames of <c>ESPANEL</c>, the evil panel the hidden interface icon comes from.</summary>
    public required IReadOnlyList<Sprite> EvilSpanelFrames { get; init; }

    /// <summary>Frames of <c>ARMY_ESTIMATION_ICON</c>.</summary>
    public required IReadOnlyList<Sprite> ArmyEstimationFrames { get; init; }

    /// <summary>Source of the OK button.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window reads was supplied.</summary>
    public bool IsComplete =>
        SpanelFrames.Count > 21 && CspanelFrames.Count > 2 && EvilSpanelFrames.Count > 4 &&
        ArmyEstimationFrames.Count > 1;
}

/// <summary>
/// The interface page of the system options, <c>fheroes2::openInterfaceSettingsDialog()</c>
/// (<c>src/fheroes2/dialog/dialog_interface_settings.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// Five options in the reference's grid: interface set, show/hide, army estimation on the top row of three, cursor
/// and scroll speed on the second row of two. Clicking the first four closes the page with the choice, and the caller
/// applies the setting, rebuilds the interface (<c>updateUI()</c>) and opens the page again; clicking the scroll speed
/// — or its mouse wheel — cycles the five speeds in place and sets <see cref="IsDirty"/>.
/// </para>
/// <para>
/// A right click asks for a <see cref="DialogPopup"/> with the explanation of the option; <see cref="Show"/> presents
/// it with a message box, the <c>showStandardTextMessage()</c> of the reference.
/// </para>
/// <para>
/// One deviation: the reference re-renders when the full screen state changes through a global hot key. The port's
/// <see cref="IEventEngine"/> has no full screen query — the application owns the window — so that branch is left out.
/// </para>
/// </remarks>
public sealed class InterfaceSettingsDialog
{
    /// <summary>Width of the page, <c>StandardWindow( 289, ... )</c>.</summary>
    public const int Width = 289;

    /// <summary>Height of the page, <c>optionsStepY * 2 + 52</c>.</summary>
    public const int Height = (OptionItem.OptionsStepY * 2) + 52;

    /// <summary>Interface set icon, <c>interfaceTypeRoi</c>.</summary>
    public static readonly Rect InterfaceTypeArea =
        new(OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Show/hide icon, <c>interfacePresenceRoi</c>.</summary>
    public static readonly Rect InterfacePresenceArea = new(
        OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX, OptionItem.OptionsOffsetY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Army estimation icon, <c>armyEstimationModeRoi</c>.</summary>
    public static readonly Rect ArmyEstimationArea = new(
        OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2), OptionItem.OptionsOffsetY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Cursor icon, <c>cursorTypeRoi</c>.</summary>
    public static readonly Rect CursorTypeArea = new(
        OptionItem.TwoOptionsOffsetX, OptionItem.OptionsOffsetY + OptionItem.OptionsStepY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Scroll speed icon, <c>scrollSpeedRoi</c>.</summary>
    public static readonly Rect ScrollSpeedArea = new(
        OptionItem.TwoOptionsOffsetX + OptionItem.TwoOptionsStepX,
        OptionItem.OptionsOffsetY + OptionItem.OptionsStepY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly InterfaceSettingsArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _bodyFont;
    private readonly Rect _interfaceTypeRoi;
    private readonly Rect _interfacePresenceRoi;
    private readonly Rect _cursorTypeRoi;
    private readonly Rect _scrollSpeedRoi;
    private readonly Rect _armyEstimationRoi;

    private DialogPopup? _popup;

    private InterfaceSettingsDialog(Size screen, InterfaceSettingsArtwork artwork, IGlyphSource titleFont,
        IGlyphSource bodyFont)
    {
        _artwork = artwork;
        _titleFont = titleFont;
        _bodyFont = bodyFont;

        _window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);

        Rect roi = _window.ActiveArea;

        _interfaceTypeRoi = InterfaceTypeArea with { X = InterfaceTypeArea.X + roi.X, Y = InterfaceTypeArea.Y + roi.Y };
        _interfacePresenceRoi = InterfacePresenceArea with
        {
            X = InterfacePresenceArea.X + roi.X,
            Y = InterfacePresenceArea.Y + roi.Y,
        };
        _cursorTypeRoi = CursorTypeArea with { X = CursorTypeArea.X + roi.X, Y = CursorTypeArea.Y + roi.Y };
        _scrollSpeedRoi = ScrollSpeedArea with { X = ScrollSpeedArea.X + roi.X, Y = ScrollSpeedArea.Y + roi.Y };
        _armyEstimationRoi = ArmyEstimationArea with
        {
            X = ArmyEstimationArea.X + roi.X,
            Y = ArmyEstimationArea.Y + roi.Y,
        };

        _buttons = ButtonGroup.CreateStandard(roi, DialogButton.Ok, artwork.Buttons);
    }

    /// <summary>The window the page is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OK button of the page.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>Whether a setting was changed since the page opened.</summary>
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

    /// <summary>Area of the interface set option, in screen coordinates.</summary>
    public Rect InterfaceTypeRoi => _interfaceTypeRoi;

    /// <summary>Area of the show/hide option, in screen coordinates.</summary>
    public Rect InterfacePresenceRoi => _interfacePresenceRoi;

    /// <summary>Area of the cursor option, in screen coordinates.</summary>
    public Rect CursorTypeRoi => _cursorTypeRoi;

    /// <summary>Area of the scroll speed option, in screen coordinates.</summary>
    public Rect ScrollSpeedRoi => _scrollSpeedRoi;

    /// <summary>Area of the army estimation option, in screen coordinates.</summary>
    public Rect ArmyEstimationRoi => _armyEstimationRoi;

    /// <summary>
    /// Builds the page, or returns <see langword="null"/> when the artwork is incomplete
    /// (<c>dialog_interface_settings.cpp:91-133</c>).
    /// </summary>
    /// <param name="screen">Screen to centre on.</param>
    /// <param name="artwork">Panel frames and the OK button.</param>
    /// <param name="titleFont">Font of the message boxes, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">Font of the message boxes, <c>FontType::normalWhite()</c>.</param>
    /// <returns>The page, or <see langword="null"/> when a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> or a font is <see langword="null"/>.</exception>
    public static InterfaceSettingsDialog? TryCreate(Size screen, InterfaceSettingsArtwork artwork,
        IGlyphSource titleFont, IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new InterfaceSettingsDialog(screen, artwork, titleFont, bodyFont);
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
    /// <see cref="InterfaceSettingsOption.Exit"/> when the window has to close, a leaf when the caller has to apply
    /// that setting and reopen the page, <see cref="InterfaceSettingsOption.None"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public InterfaceSettingsOption ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (_buttons.ProcessEvents(events, events.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay : null) ==
                DialogButton.Ok ||
            events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.F10))
        {
            return InterfaceSettingsOption.Exit;
        }

        if (events.MouseClickLeft(_interfaceTypeRoi))
        {
            return InterfaceSettingsOption.InterfaceType;
        }

        if (events.MouseClickLeft(_interfacePresenceRoi))
        {
            return InterfaceSettingsOption.InterfacePresence;
        }

        if (events.MouseClickLeft(_cursorTypeRoi))
        {
            return InterfaceSettingsOption.CursorType;
        }

        if (events.MouseClickLeft(_armyEstimationRoi))
        {
            return InterfaceSettingsOption.ArmyEstimationMode;
        }

        if (CycleScrollSpeed(events))
        {
            return InterfaceSettingsOption.None;
        }

        ShowExplanation(events);

        return InterfaceSettingsOption.None;
    }

    /// <summary>
    /// Runs the page until it closes, the round trip of <c>openInterfaceSettingsDialog()</c>
    /// (<c>dialog_interface_settings.cpp:217-263</c>).
    /// </summary>
    /// <param name="target">Indexed frame buffer the scene draws and renders.</param>
    /// <param name="dialog">The page to run.</param>
    /// <param name="updateUI">Rebuilds the interface after a setting changed, the reference's <c>updateUI()</c>.</param>
    /// <param name="events">Input source; defaults to <see cref="LocalEvent.Current"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/>, <paramref name="dialog"/> or
    /// <paramref name="updateUI"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">There is no event engine to run the loop on.</exception>
    public static void Show(PixelBuffer target, InterfaceSettingsDialog dialog, Action updateUI,
        LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(updateUI);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine
            ?? throw new InvalidOperationException("The interface settings window needs an event engine to run.");

        Settings settings = Settings.Get();

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            InterfaceSettingsOption option = dialog.ProcessEvents(eventState);

            if (dialog.TakePopup() is { } popup)
            {
                MessageBox.Show(target, popup.Header, popup.Body, dialog._titleFont, dialog._bodyFont,
                    DialogButton.Ok, dialog._artwork.Buttons, eventState);
            }

            if (option == InterfaceSettingsOption.Exit)
            {
                return;
            }

            if (ApplyOption(settings, option))
            {
                dialog.IsDirty = true;
                updateUI();
            }

            dialog.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }
    }

    /// <summary>Applies a leaf the way <c>openInterfaceSettingsDialog()</c> does.</summary>
    private static bool ApplyOption(Settings settings, InterfaceSettingsOption option)
    {
        switch (option)
        {
            case InterfaceSettingsOption.InterfaceType:
                settings.SwitchToNextInterfaceType();
                return true;
            case InterfaceSettingsOption.InterfacePresence:
                settings.SetHideInterface(!settings.IsHideInterface);
                return true;
            case InterfaceSettingsOption.CursorType:
                settings.SetMonochromeCursor(!settings.IsMonochromeCursor);
                return true;
            case InterfaceSettingsOption.ArmyEstimationMode:
                settings.SetNumericArmyEstimationView(!settings.IsNumericArmyEstimationView);
                return true;
            default:
                return false;
        }
    }

    private void DrawOptions(PixelBuffer target)
    {
        Settings settings = Settings.Get();

        OptionItem.DrawInterfaceType(target, _interfaceTypeRoi,
            _artwork.SpanelFrames[settings.InterfaceType switch
            {
                InterfaceType.Good => 16,
                InterfaceType.Evil => 17,
                _ => 15,
            }], settings.InterfaceType, OptionItem.ThreeElementsRowWidth);

        Sprite presenceIcon = settings.IsHideInterface
            ? _artwork.EvilSpanelFrames[4]
            : _artwork.SpanelFrames[settings.IsEvilInterfaceEnabled ? 17 : 16];

        OptionItem.Draw(target, _interfacePresenceRoi, presenceIcon, "Interface",
            settings.IsHideInterface ? "Hide" : "Show", OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawCursor(target, _cursorTypeRoi, _artwork.SpanelFrames[settings.IsMonochromeCursor ? 20 : 21],
            settings.IsMonochromeCursor, OptionItem.TwoElementsRowWidth);

        OptionItem.DrawScrollSpeed(target, _scrollSpeedRoi, ScrollSpeedIcon(settings.ScrollSpeed),
            settings.ScrollSpeed, OptionItem.TwoElementsRowWidth);

        OptionItem.Draw(target, _armyEstimationRoi,
            _artwork.ArmyEstimationFrames[settings.IsNumericArmyEstimationView ? 1 : 0], "Army Estimation",
            settings.IsNumericArmyEstimationView ? "Numeric" : "Canonical", OptionItem.ThreeElementsRowWidth);
    }

    private Sprite ScrollSpeedIcon(AdventureMapScrollSpeed speed) => speed switch
    {
        AdventureMapScrollSpeed.None => _artwork.SpanelFrames[9],
        AdventureMapScrollSpeed.Fast => _artwork.CspanelFrames[1],
        AdventureMapScrollSpeed.VeryFast => _artwork.CspanelFrames[2],
        _ => _artwork.CspanelFrames[0],
    };

    /// <summary>Cycles the scroll speed on a click or a wheel over its icon, <c>dialog_interface_settings.cpp:156-176</c>.</summary>
    private bool CycleScrollSpeed(LocalEvent events)
    {
        Settings settings = Settings.Get();
        const int speedCount = (int)AdventureMapScrollSpeed.VeryFast + 1;

        if (events.MouseClickLeft(_scrollSpeedRoi))
        {
            settings.SetScrollSpeed(((int)settings.ScrollSpeed + 1) % speedCount);
        }
        else if (events.IsMouseWheelUpInArea(_scrollSpeedRoi))
        {
            settings.SetScrollSpeed((int)settings.ScrollSpeed + 1);
        }
        else if (events.IsMouseWheelDownInArea(_scrollSpeedRoi))
        {
            settings.SetScrollSpeed((int)settings.ScrollSpeed - 1);
        }
        else
        {
            return false;
        }

        IsDirty = true;
        return true;
    }

    /// <summary>Fills <see cref="_popup"/> for a right click, the <c>showStandardTextMessage()</c> calls of the reference.</summary>
    private void ShowExplanation(LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(_interfaceTypeRoi))
        {
            _popup = new DialogPopup("Interface Type", "Toggle the type of interface you want to use.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_interfacePresenceRoi))
        {
            _popup = new DialogPopup("Interface", "Toggle interface visibility.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_cursorTypeRoi))
        {
            _popup = new DialogPopup("Mouse Cursor",
                "Toggle colored cursor on or off. This is only an aesthetic choice.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_scrollSpeedRoi))
        {
            _popup = new DialogPopup("Scroll Speed", "Sets the speed at which you scroll the window.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_armyEstimationRoi))
        {
            _popup = new DialogPopup("Army Estimation",
                "Toggle how army sizes are displayed when right-clicking armies on the adventure map. \n\n" +
                "Canonical: Army sizes are shown as descriptive text (e.g. \"Few\").\n\n" +
                "Numeric: Army sizes are shown as numeric ranges (e.g. \"1-4\").");
        }
        else if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup("Okay", "Exit this menu.");
        }
    }
}
