using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Audio;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>Artwork of <see cref="AudioSettingsDialog"/>, gathered by the caller from the assets.</summary>
/// <remarks>
/// <see cref="SpanelFrames"/> is <c>SPANEL</c>: frame 0 and 1 are the music icon off and on, 2 and 3 the effects icon,
/// 10 and 11 the music type and 3D audio icons.
/// </remarks>
public sealed class AudioSettingsArtwork
{
    /// <summary>Frames of <c>SPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> SpanelFrames { get; init; }

    /// <summary>Source of the OK button.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window reads was supplied.</summary>
    public bool IsComplete => SpanelFrames.Count > 11;
}

/// <summary>
/// The audio page of the system options, <c>Dialog::openAudioSettingsDialog()</c>
/// (<c>src/fheroes2/dialog/dialog_audio.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// Four options in a two by two grid: music volume, effects volume, music type and 3D audio. The two volumes are
/// clicked through in place — <c>( volume + 1 ) % 11</c>, so off has to be passed again to get back to sound — and also
/// follow the wheel over their icon; every change is pushed to the audio device as <c>100 * volume / 10</c>, the
/// <c>Music::setVolume()</c> and <c>Mixer::setVolume()</c> of the reference. The music type is the only one that is
/// skipped when there is no expansion data.
/// </para>
/// <para>
/// The volumes are only offered when there is a device (<c>Audio::isValid()</c>); without one the icons still show
/// «off» and the clicks do nothing, as in the reference.
/// </para>
/// <para>
/// A right click asks for a <see cref="DialogPopup"/> with the explanation of the option; <see cref="Show"/> presents
/// it with a message box.
/// </para>
/// </remarks>
public sealed class AudioSettingsDialog
{
    /// <summary>Width of the page, <c>StandardWindow( 289, ... )</c>.</summary>
    public const int Width = 289;

    /// <summary>Height of the page, <c>StandardWindow( ..., 272 )</c>.</summary>
    public const int Height = 272;

    /// <summary>Left offset of the first icon, <c>windowRoi.x + 53</c>.</summary>
    public const int OptionsOffsetX = 53;

    /// <summary>Top offset of the first icon, <c>windowRoi.y + 31</c>.</summary>
    public const int OptionsOffsetY = 31;

    /// <summary>Horizontal step between the two columns, <c>optionStep.x</c>.</summary>
    public const int OptionsStepX = 118;

    /// <summary>Vertical step between the two rows, <c>optionStep.y</c>.</summary>
    public const int OptionsStepY = 110;

    /// <summary>Side of an option icon, the size of <c>SPANEL</c> frame 0 (65).</summary>
    public const int OptionIconSize = 65;

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly AudioSettingsArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _bodyFont;
    private readonly AudioManager? _audio;
    private readonly Rect _musicVolumeRoi;
    private readonly Rect _soundVolumeRoi;
    private readonly Rect _musicTypeRoi;
    private readonly Rect _audio3DRoi;

    private DialogPopup? _popup;

    private AudioSettingsDialog(Size screen, AudioSettingsArtwork artwork, IGlyphSource titleFont,
        IGlyphSource bodyFont, AudioManager? audio)
    {
        _artwork = artwork;
        _titleFont = titleFont;
        _bodyFont = bodyFont;
        _audio = audio;

        _window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);

        Rect roi = _window.ActiveArea;
        int x = roi.X + OptionsOffsetX;
        int y = roi.Y + OptionsOffsetY;
        Size icon = new(OptionIconSize, OptionIconSize);

        _musicVolumeRoi = new Rect(x, y, icon.Width, icon.Height);
        _soundVolumeRoi = new Rect(x + OptionsStepX, y, icon.Width, icon.Height);
        _musicTypeRoi = new Rect(x, y + OptionsStepY, icon.Width, icon.Height);
        _audio3DRoi = new Rect(x + OptionsStepX, y + OptionsStepY, icon.Width, icon.Height);

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

    /// <summary>Area of the music volume option.</summary>
    public Rect MusicVolumeArea => _musicVolumeRoi;

    /// <summary>Area of the effects volume option.</summary>
    public Rect SoundVolumeArea => _soundVolumeRoi;

    /// <summary>Area of the music type option.</summary>
    public Rect MusicTypeArea => _musicTypeRoi;

    /// <summary>Area of the 3D audio option.</summary>
    public Rect Audio3DArea => _audio3DRoi;

    /// <summary>Reads <see cref="Popup"/> and clears it.</summary>
    /// <returns>The popup, or <see langword="null"/> when none was asked for.</returns>
    public DialogPopup? TakePopup()
    {
        DialogPopup? popup = _popup;
        _popup = null;
        return popup;
    }

    /// <summary>
    /// Builds the page, or returns <see langword="null"/> when the artwork is incomplete
    /// (<c>dialog_audio.cpp:110-145</c>).
    /// </summary>
    /// <param name="screen">Screen to centre on.</param>
    /// <param name="artwork">Panel frames and the OK button.</param>
    /// <param name="titleFont">Font of the message boxes, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">Font of the message boxes, <c>FontType::normalWhite()</c>.</param>
    /// <param name="audio">Audio device, or <see langword="null"/> when the host has none.</param>
    /// <returns>The page, or <see langword="null"/> when a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> or a font is <see langword="null"/>.</exception>
    public static AudioSettingsDialog? TryCreate(Size screen, AudioSettingsArtwork artwork, IGlyphSource titleFont,
        IGlyphSource bodyFont, AudioManager? audio = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new AudioSettingsDialog(screen, artwork, titleFont, bodyFont, audio);
    }

    /// <summary>Draws the window, the four options and the OK button.</summary>
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

    /// <summary>Handles one frame: the button, the four options and their right clicks.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="environmentSoundMixer">
    /// Rebuilds the ambient loops after the 3D switch, the <c>Game::EnvironmentSoundMixer()</c> the reference calls
    /// when the page was opened from the adventure map; <see langword="null"/> for the other callers.
    /// </param>
    /// <returns>Whether the page has to close.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, Action? environmentSoundMixer = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (_buttons.ProcessEvents(events, events.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay : null) ==
                DialogButton.Ok ||
            events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.F10))
        {
            return true;
        }

        Settings settings = Settings.Get();

        if (_audio?.IsValid == true)
        {
            if (StepVolume(events, _musicVolumeRoi, () => settings.MusicVolume, settings.SetMusicVolume,
                    _audio.SetMusicVolume))
            {
                IsDirty = true;
            }

            if (StepVolume(events, _soundVolumeRoi, () => settings.SoundVolume, settings.SetSoundVolume,
                    _audio.SetSoundVolume))
            {
                IsDirty = true;
            }
        }

        if (events.MouseClickLeft(_musicTypeRoi))
        {
            int type = (int)settings.MusicType + 1;

            // 没有资料片时跳过 MIDI Expansion，参考 dialog_audio.cpp:209-221。
            if (type == (int)MusicSource.MidiExpansion && !settings.IsPriceOfLoyaltySupported)
            {
                ++type;
            }

            settings.SetMusicType(type > (int)MusicSource.External ? MusicSource.MidiOriginal : (MusicSource)type);
            _audio?.PlayCurrentMusic();
            IsDirty = true;
        }

        if (events.MouseClickLeft(_audio3DRoi))
        {
            settings.Set3DAudio(!settings.Is3DAudioEnabled);
            environmentSoundMixer?.Invoke();
            IsDirty = true;
        }

        ShowExplanation(events);

        return false;
    }

    /// <summary>
    /// Runs the page until it closes, the loop of <c>openAudioSettingsDialog()</c> (<c>dialog_audio.cpp:149-259</c>).
    /// </summary>
    /// <param name="target">Indexed frame buffer the scene draws and renders.</param>
    /// <param name="dialog">The page to run.</param>
    /// <param name="environmentSoundMixer">
    /// Rebuilds the ambient loops after the 3D switch; <see langword="null"/> when the page was not opened from the
    /// adventure map (<c>fromAdventureMap</c> false).
    /// </param>
    /// <param name="events">Input source; defaults to <see cref="LocalEvent.Current"/>.</param>
    /// <returns>
    /// <see langword="true"/> when a setting changed, i.e. when the caller should save the configuration, the
    /// <c>saveConfig</c> of the reference.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="dialog"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">There is no event engine to run the loop on.</exception>
    public static bool Show(PixelBuffer target, AudioSettingsDialog dialog, Action? environmentSoundMixer = null,
        LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dialog);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine
            ?? throw new InvalidOperationException("The audio settings window needs an event engine to run.");

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            bool exit = dialog.ProcessEvents(eventState, environmentSoundMixer);

            if (dialog.TakePopup() is { } popup)
            {
                MessageBox.Show(target, popup.Header, popup.Body, dialog._titleFont, dialog._bodyFont,
                    DialogButton.Ok, dialog._artwork.Buttons, eventState);
            }

            if (exit)
            {
                return dialog.IsDirty;
            }

            dialog.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return dialog.IsDirty;
    }

    private void DrawOptions(PixelBuffer target)
    {
        Settings settings = Settings.Get();

        bool isMusicOn = _audio?.IsValid == true && settings.MusicVolume > 0;

        OptionItem.Draw(target, _musicVolumeRoi, _artwork.SpanelFrames[isMusicOn ? 1 : 0], "Music",
            isMusicOn ? settings.MusicVolume.ToString() : "off", OptionItem.TwoElementsRowWidth);

        bool isSoundOn = _audio?.IsValid == true && settings.SoundVolume > 0;

        OptionItem.Draw(target, _soundVolumeRoi, _artwork.SpanelFrames[isSoundOn ? 3 : 2], "Effects",
            isSoundOn ? settings.SoundVolume.ToString() : "off", OptionItem.TwoElementsRowWidth);

        string musicTypeName = settings.MusicType switch
        {
            MusicSource.MidiOriginal => "MIDI",
            MusicSource.MidiExpansion => "MIDI Expansion",
            _ => "External",
        };

        OptionItem.Draw(target, _musicTypeRoi,
            _artwork.SpanelFrames[settings.MusicType == MusicSource.External ? 11 : 10], "Music Type",
            musicTypeName, OptionItem.TwoElementsRowWidth);

        OptionItem.Draw(target, _audio3DRoi, _artwork.SpanelFrames[settings.Is3DAudioEnabled ? 11 : 10], "3D Audio",
            settings.Is3DAudioEnabled ? "On" : "Off", OptionItem.TwoElementsRowWidth);
    }

    /// <summary>
    /// Cycles one volume on a click or a wheel over its icon and pushes it to the device, <c>dialog_audio.cpp:163-206</c>.
    /// </summary>
    private static bool StepVolume(LocalEvent events, Rect area, Func<int> readVolume,
        Action<int> applyToSettings, Action<int> applyToDevice)
    {
        if (events.MouseClickLeft(area))
        {
            applyToSettings((readVolume() + 1) % 11);
        }
        else if (events.IsMouseWheelUpInArea(area))
        {
            applyToSettings(readVolume() + 1);
        }
        else if (events.IsMouseWheelDownInArea(area))
        {
            applyToSettings(readVolume() - 1);
        }
        else
        {
            return false;
        }

        // 参考：Music::setVolume( 100 * MusicVolume() / 10 )，音量上限 10。
        applyToDevice(readVolume() * 10);
        return true;
    }

    /// <summary>Fills <see cref="_popup"/> for a right click.</summary>
    private void ShowExplanation(LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(_musicVolumeRoi))
        {
            _popup = new DialogPopup("Music", "Toggle ambient music level.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_soundVolumeRoi))
        {
            _popup = new DialogPopup("Effects", "Toggle foreground sounds level.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_musicTypeRoi))
        {
            _popup = new DialogPopup("Music Type", "Change the type of music.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_audio3DRoi))
        {
            _popup = new DialogPopup("3D Audio", "Toggle the 3D effect of foreground sounds.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup("Okay", "Exit this menu.");
        }
    }
}
