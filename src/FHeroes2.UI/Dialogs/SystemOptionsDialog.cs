using System.Globalization;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>What the system options window asks for, <c>DialogAction</c> of the reference.</summary>
/// <remarks>
/// The reference enum also holds <c>UpdateInterface</c>, which <c>openSystemOptionsDialog()</c> never returns; the port
/// leaves it out. <see cref="Exit"/> is the reference's <c>Close</c>.
/// </remarks>
public enum SystemOptionsAction
{
    /// <summary>No action was asked for this frame.</summary>
    None,

    /// <summary>Open the language picker, <c>DialogAction::Language</c>.</summary>
    Language,

    /// <summary>Open the graphics page, <c>DialogAction::Graphics</c>.</summary>
    Graphics,

    /// <summary>Open the audio page, <c>DialogAction::AudioSettings</c>.</summary>
    AudioSettings,

    /// <summary>Open the hot keys window, <c>DialogAction::HotKeys</c>.</summary>
    HotKeys,

    /// <summary>Open the interface page, <c>DialogAction::InterfaceSettings</c>.</summary>
    InterfaceSettings,

    /// <summary>The window was closed, <c>DialogAction::Close</c>.</summary>
    Exit,
}

/// <summary>Artwork of <see cref="SystemOptionsDialog"/>, gathered by the caller from the assets.</summary>
/// <remarks>
/// Frames follow the reference: <see cref="SpanelFrames"/> is <c>SPANEL</c> (1 audio, 3-9 hero and enemy speed,
/// 9 "off", 16-17 interface theme, 18 language and manual battles), <see cref="CspanelFrames"/> is <c>CSPANEL</c>
/// (4 text support on, 6-7 auto resolved battles) and <see cref="GameOptionFrames"/> is <c>GAME_OPTION_ICON</c>
/// (0 hot keys, 1 graphics).
/// </remarks>
public sealed class SystemOptionsArtwork
{
    /// <summary>Frames of <c>SPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> SpanelFrames { get; init; }

    /// <summary>Frames of <c>CSPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> CspanelFrames { get; init; }

    /// <summary>Frames of <c>GAME_OPTION_ICON</c>.</summary>
    public required IReadOnlyList<Sprite> GameOptionFrames { get; init; }

    /// <summary>Source of the OK button.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window reads was supplied.</summary>
    public bool IsComplete => SpanelFrames.Count > 21 && CspanelFrames.Count > 7 && GameOptionFrames.Count > 1;
}

/// <summary>
/// The main window of the system options, <c>openSystemOptionsDialog()</c> of the reference
/// (<c>src/fheroes2/dialog/dialog_system_options.cpp:166-361</c>).
/// </summary>
/// <remarks>
/// <para>
/// Nine options in a three by three grid: language, graphics and audio on the first row, hero speed, enemy speed and
/// hot keys on the second, interface, text support and battles on the third. Language, graphics, audio, hot keys and
/// interface close the window with an action and the caller opens the matching page; the other four are changed in
/// place, which sets <see cref="IsDirty"/>. A right click asks for a <see cref="DialogPopup"/>.
/// </para>
/// <para>
/// The reference re-renders when text support mode is toggled through a global hot key (<c>:352-357</c>); the port's
/// caller redraws every frame, so there is nothing to restore.
/// </para>
/// <para>
/// <c>conf.Save( Settings::configFileName )</c> at the end of the reference has no counterpart — the port has no
/// configuration file layer yet, so <see cref="IsDirty"/> is all the window reports.
/// </para>
/// </remarks>
public sealed class SystemOptionsDialog
{
    /// <summary>Width of the page, <c>StandardWindow( 289, ... )</c>.</summary>
    public const int Width = 289;

    /// <summary>Height of the page, <c>optionsStepY * 3 + 52</c>.</summary>
    public const int Height = (OptionItem.OptionsStepY * 3) + 52;

    /// <summary>Language icon, <c>languageRoi</c>.</summary>
    public static readonly Rect LanguageArea =
        new(OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Graphics icon, <c>graphicsRoi</c>.</summary>
    public static readonly Rect GraphicsArea = new(
        OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX, OptionItem.OptionsOffsetY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Audio icon, <c>audioRoi</c>.</summary>
    public static readonly Rect AudioArea = new(
        OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2), OptionItem.OptionsOffsetY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Hero speed icon, <c>heroSpeedRoi</c>.</summary>
    public static readonly Rect HeroSpeedArea = new(
        OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY + OptionItem.OptionsStepY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Enemy speed icon, <c>enemySpeedRoi</c>.</summary>
    public static readonly Rect EnemySpeedArea = new(
        OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
        OptionItem.OptionsOffsetY + OptionItem.OptionsStepY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Hot keys icon, <c>hotKeyRoi</c>.</summary>
    public static readonly Rect HotKeysArea = new(
        OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2),
        OptionItem.OptionsOffsetY + OptionItem.OptionsStepY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Interface icon, <c>interfaceRoi</c>.</summary>
    public static readonly Rect InterfaceArea = new(
        OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2),
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Text support icon, <c>textSupportModeRoi</c>.</summary>
    public static readonly Rect TextSupportArea = new(
        OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
        OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2), OptionItem.OptionIconSize,
        OptionItem.OptionIconSize);

    /// <summary>Battles icon, <c>battlesRoi</c>.</summary>
    public static readonly Rect BattlesArea = new(
        OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2),
        OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2), OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly SystemOptionsArtwork _artwork;
    private readonly Rect _languageRoi;
    private readonly Rect _graphicsRoi;
    private readonly Rect _audioRoi;
    private readonly Rect _heroSpeedRoi;
    private readonly Rect _enemySpeedRoi;
    private readonly Rect _hotKeysRoi;
    private readonly Rect _interfaceRoi;
    private readonly Rect _textSupportRoi;
    private readonly Rect _battlesRoi;

    private DialogPopup? _popup;

    private SystemOptionsDialog(Size screen, SystemOptionsArtwork artwork)
    {
        _artwork = artwork;

        _window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);

        Rect roi = _window.ActiveArea;

        _languageRoi = LanguageArea with { X = LanguageArea.X + roi.X, Y = LanguageArea.Y + roi.Y };
        _graphicsRoi = GraphicsArea with { X = GraphicsArea.X + roi.X, Y = GraphicsArea.Y + roi.Y };
        _audioRoi = AudioArea with { X = AudioArea.X + roi.X, Y = AudioArea.Y + roi.Y };
        _heroSpeedRoi = HeroSpeedArea with { X = HeroSpeedArea.X + roi.X, Y = HeroSpeedArea.Y + roi.Y };
        _enemySpeedRoi = EnemySpeedArea with { X = EnemySpeedArea.X + roi.X, Y = EnemySpeedArea.Y + roi.Y };
        _hotKeysRoi = HotKeysArea with { X = HotKeysArea.X + roi.X, Y = HotKeysArea.Y + roi.Y };
        _interfaceRoi = InterfaceArea with { X = InterfaceArea.X + roi.X, Y = InterfaceArea.Y + roi.Y };
        _textSupportRoi = TextSupportArea with { X = TextSupportArea.X + roi.X, Y = TextSupportArea.Y + roi.Y };
        _battlesRoi = BattlesArea with { X = BattlesArea.X + roi.X, Y = BattlesArea.Y + roi.Y };

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

    /// <summary>Area of the language option, in screen coordinates.</summary>
    public Rect LanguageRoi => _languageRoi;

    /// <summary>Area of the graphics option, in screen coordinates.</summary>
    public Rect GraphicsRoi => _graphicsRoi;

    /// <summary>Area of the audio option, in screen coordinates.</summary>
    public Rect AudioRoi => _audioRoi;

    /// <summary>Area of the hero speed option, in screen coordinates.</summary>
    public Rect HeroSpeedRoi => _heroSpeedRoi;

    /// <summary>Area of the enemy speed option, in screen coordinates.</summary>
    public Rect EnemySpeedRoi => _enemySpeedRoi;

    /// <summary>Area of the hot keys option, in screen coordinates.</summary>
    public Rect HotKeysRoi => _hotKeysRoi;

    /// <summary>Area of the interface option, in screen coordinates.</summary>
    public Rect InterfaceRoi => _interfaceRoi;

    /// <summary>Area of the text support option, in screen coordinates.</summary>
    public Rect TextSupportRoi => _textSupportRoi;

    /// <summary>Area of the battles option, in screen coordinates.</summary>
    public Rect BattlesRoi => _battlesRoi;

    /// <summary>
    /// Builds the page, or returns <see langword="null"/> when the artwork is incomplete
    /// (<c>dialog_system_options.cpp:172-207</c>).
    /// </summary>
    /// <param name="screen">Screen to centre on.</param>
    /// <param name="artwork">Panel frames and the OK button.</param>
    /// <returns>The page, or <see langword="null"/> when a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public static SystemOptionsDialog? TryCreate(Size screen, SystemOptionsArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new SystemOptionsDialog(screen, artwork);
    }

    /// <summary>Draws the window, the nine options and the OK button.</summary>
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

    /// <summary>
    /// Handles one frame: the button, the nine options and their right clicks. The four in-place options are applied
    /// here, the rest are returned to the caller.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>The page to open, <see cref="SystemOptionsAction.Exit"/> when the window has to close, or
    /// <see cref="SystemOptionsAction.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public SystemOptionsAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (_buttons.ProcessEvents(events, events.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay : null) ==
                DialogButton.Ok ||
            events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.F10))
        {
            return SystemOptionsAction.Exit;
        }

        if (events.MouseClickLeft(_languageRoi))
        {
            return SystemOptionsAction.Language;
        }

        if (events.MouseClickLeft(_graphicsRoi))
        {
            return SystemOptionsAction.Graphics;
        }

        if (events.MouseClickLeft(_audioRoi))
        {
            return SystemOptionsAction.AudioSettings;
        }

        if (ChangeHeroSpeed(events) || ChangeEnemySpeed(events))
        {
            return SystemOptionsAction.None;
        }

        if (events.MouseClickLeft(_hotKeysRoi))
        {
            return SystemOptionsAction.HotKeys;
        }

        if (events.MouseClickLeft(_interfaceRoi))
        {
            return SystemOptionsAction.InterfaceSettings;
        }

        if (events.MouseClickLeft(_textSupportRoi))
        {
            Settings settings = Settings.Get();
            settings.SetTextSupportMode(!settings.IsTextSupportMode);
            IsDirty = true;

            return SystemOptionsAction.None;
        }

        if (events.MouseClickLeft(_battlesRoi))
        {
            CycleBattleMode(Settings.Get());
            IsDirty = true;

            return SystemOptionsAction.None;
        }

        ShowExplanation(events);

        return SystemOptionsAction.None;
    }

    /// <summary>Advances the auto battle mode, <c>dialog_system_options.cpp:299-317</c>.</summary>
    private static void CycleBattleMode(Settings settings)
    {
        if (settings.IsBattleAutoResolve)
        {
            if (settings.IsBattleAutoSpellcast)
            {
                settings.SetBattleAutoSpellcast(false);
            }
            else
            {
                settings.SetBattleAutoResolve(false);
            }
        }
        else
        {
            settings.SetBattleAutoResolve(true);
            settings.SetBattleAutoSpellcast(true);
        }
    }

    /// <summary>Click or wheel over the hero speed icon, <c>dialog_system_options.cpp:236-259</c>.</summary>
    private bool ChangeHeroSpeed(LocalEvent events)
    {
        Settings settings = Settings.Get();

        if (events.MouseClickLeft(_heroSpeedRoi))
        {
            settings.SetHeroesMoveSpeed((settings.HeroesMoveSpeed % 10) + 1);
        }
        else if (events.IsMouseWheelUpInArea(_heroSpeedRoi))
        {
            settings.SetHeroesMoveSpeed(settings.HeroesMoveSpeed + 1);
        }
        else if (events.IsMouseWheelDownInArea(_heroSpeedRoi))
        {
            settings.SetHeroesMoveSpeed(settings.HeroesMoveSpeed - 1);
        }
        else
        {
            return false;
        }

        GameDelays.UpdateGameSpeed();
        IsDirty = true;

        return true;
    }

    /// <summary>Click or wheel over the enemy speed icon, <c>dialog_system_options.cpp:261-284</c>.</summary>
    private bool ChangeEnemySpeed(LocalEvent events)
    {
        Settings settings = Settings.Get();

        if (events.MouseClickLeft(_enemySpeedRoi))
        {
            settings.SetAiMoveSpeed((settings.AiMoveSpeed + 1) % 11);
        }
        else if (events.IsMouseWheelUpInArea(_enemySpeedRoi))
        {
            settings.SetAiMoveSpeed(settings.AiMoveSpeed + 1);
        }
        else if (events.IsMouseWheelDownInArea(_enemySpeedRoi))
        {
            settings.SetAiMoveSpeed(settings.AiMoveSpeed - 1);
        }
        else
        {
            return false;
        }

        GameDelays.UpdateGameSpeed();
        IsDirty = true;

        return true;
    }

    private void DrawOptions(PixelBuffer target)
    {
        Settings settings = Settings.Get();

        OptionItem.DrawLanguage(target, _languageRoi, _artwork.SpanelFrames[18],
            GameLanguage.GetName(GameLanguage.Current), OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawGraphics(target, _graphicsRoi, _artwork.GameOptionFrames[1],
            OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawAudio(target, _audioRoi, _artwork.SpanelFrames[1], OptionItem.ThreeElementsRowWidth);

        OptionItem.Draw(target, _heroSpeedRoi, SpeedIcon(settings.HeroesMoveSpeed), "Hero Speed",
            settings.HeroesMoveSpeed == 10 ? "Jump" : settings.HeroesMoveSpeed.ToString(CultureInfo.InvariantCulture),
            OptionItem.ThreeElementsRowWidth);

        OptionItem.Draw(target, _enemySpeedRoi, SpeedIcon(settings.AiMoveSpeed), "Enemy Speed",
            EnemySpeedName(settings.AiMoveSpeed), OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawHotKeys(target, _hotKeysRoi, _artwork.GameOptionFrames[0],
            OptionItem.ThreeElementsRowWidth);

        OptionItem.Draw(target, _interfaceRoi,
            _artwork.SpanelFrames[settings.IsEvilInterfaceEnabled ? 17 : 16], "Interface", "Settings",
            OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawTextSupport(target, _textSupportRoi,
            settings.IsTextSupportMode ? _artwork.CspanelFrames[4] : _artwork.SpanelFrames[9], settings.IsTextSupportMode,
            OptionItem.ThreeElementsRowWidth);

        if (settings.IsBattleAutoResolve)
        {
            OptionItem.Draw(target, _battlesRoi,
                _artwork.CspanelFrames[settings.IsBattleAutoSpellcast ? 7 : 6], "Battles",
                settings.IsBattleAutoSpellcast ? "Auto Resolve" : "Auto, No Spells",
                OptionItem.ThreeElementsRowWidth);
        }
        else
        {
            OptionItem.Draw(target, _battlesRoi, _artwork.SpanelFrames[18], "Battles", "Manual",
                OptionItem.ThreeElementsRowWidth);
        }
    }

    /// <summary>The speed icon, <c>dialog_system_options.cpp:94-100</c>.</summary>
    private Sprite SpeedIcon(int speed)
    {
        int frame = 9;

        if (speed >= 4)
        {
            frame = 3 + (speed / 2);
        }
        else if (speed > 0)
        {
            frame = 4;
        }

        return _artwork.SpanelFrames[frame];
    }

    private static string EnemySpeedName(int speed) => speed switch
    {
        0 => "Don't Show",
        10 => "Jump",
        _ => speed.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Fills <see cref="_popup"/> for a right click, the <c>showStandardTextMessage()</c> calls of the reference.</summary>
    private void ShowExplanation(LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(_languageRoi))
        {
            _popup = new DialogPopup("Select Game Language", "Change the language of the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_graphicsRoi))
        {
            _popup = new DialogPopup("Graphics", "Change the graphics settings of the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_audioRoi))
        {
            _popup = new DialogPopup("Audio", "Change the audio settings of the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_heroSpeedRoi))
        {
            _popup = new DialogPopup("Hero Speed",
                "Change the speed at which your heroes move on the main screen.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_enemySpeedRoi))
        {
            _popup = new DialogPopup("Enemy Speed",
                "Sets the speed that computer heroes move at. You can also elect not to view computer movement at all.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_hotKeysRoi))
        {
            _popup = new DialogPopup("Hot Keys", "Check and configure all the hot keys present in the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_interfaceRoi))
        {
            _popup = new DialogPopup("Interface Settings", "Change the interface settings of the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_textSupportRoi))
        {
            _popup = new DialogPopup("Text Support",
                "Toggle text support mode to output extra information about windows and events in the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_battlesRoi))
        {
            _popup = new DialogPopup("Battles", "Toggle instant battle mode.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup("Okay", "Exit this menu.");
        }
    }
}
