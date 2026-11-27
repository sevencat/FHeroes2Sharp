using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.H2D;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>What the editor options window asks for, <c>DialogAction</c> of the reference.</summary>
/// <remarks>
/// The reference enum also holds <c>UpdateSettings</c>, which <c>openEditorSettings()</c> never returns; the port leaves
/// it out. <see cref="Exit"/> is the reference's <c>Close</c>. The reference spells passability <c>Passabiility</c>,
/// which the port fixes.
/// </remarks>
public enum EditorOptionsAction
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

    /// <summary>Toggle object animation, <c>DialogAction::Animation</c>.</summary>
    Animation,

    /// <summary>Toggle the passability overlay, <c>DialogAction::Passabiility</c>.</summary>
    Passability,

    /// <summary>Switch the interface set, <c>DialogAction::InterfaceType</c>.</summary>
    InterfaceType,

    /// <summary>Switch between the colour and the black and white cursor, <c>DialogAction::CursorType</c>.</summary>
    CursorType,

    /// <summary>Advance the scroll speed, <c>DialogAction::UpdateScrollSpeed</c>.</summary>
    ScrollSpeed,

    /// <summary>One step faster scrolling, <c>DialogAction::IncreaseScrollSpeed</c>.</summary>
    IncreaseScrollSpeed,

    /// <summary>One step slower scrolling, <c>DialogAction::DecreaseScrollSpeed</c>.</summary>
    DecreaseScrollSpeed,

    /// <summary>The window was closed, <c>DialogAction::Close</c>.</summary>
    Exit,
}

/// <summary>Artwork of <see cref="EditorOptionsDialog"/>, gathered by the caller from the assets.</summary>
/// <remarks>
/// Frames follow the reference: <see cref="SpanelFrames"/> is <c>SPANEL</c> (1 audio, 9 scrolling off, 15-17 interface
/// set, 18 language, 20-21 cursor), <see cref="CspanelFrames"/> is <c>CSPANEL</c> (0-2 scrolling speed),
/// <see cref="GameOptionFrames"/> is <c>GAME_OPTION_ICON</c> (0 hot keys, 1 graphics) and
/// <see cref="EspanelFrames"/> is <c>ESPANEL</c> (0-1 animation, 4-5 passability).
/// </remarks>
public sealed class EditorOptionsArtwork
{
    /// <summary>Frames of <c>SPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> SpanelFrames { get; init; }

    /// <summary>Frames of <c>CSPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> CspanelFrames { get; init; }

    /// <summary>Frames of <c>GAME_OPTION_ICON</c>.</summary>
    public required IReadOnlyList<Sprite> GameOptionFrames { get; init; }

    /// <summary>Frames of <c>ESPANEL</c>.</summary>
    public required IReadOnlyList<Sprite> EspanelFrames { get; init; }

    /// <summary>Source of the OK button.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window reads was supplied.</summary>
    public bool IsComplete =>
        SpanelFrames.Count > 21 && CspanelFrames.Count > 2 && GameOptionFrames.Count > 1 && EspanelFrames.Count > 5;

    /// <summary>
    /// Reads the four archives the page draws from out of the installed game data.
    /// </summary>
    /// <param name="buttons">Source of the OK button, which the host already has for its other windows.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <remarks>
    /// 依赖 <see cref="GameAssets.Current"/> 已指向游戏数据，同 <see cref="EditorSpellSelectionArtwork.TryCreate"/>。
    /// 四个归档与界面主题无关（参考这里不管 <c>isEvilInterface</c>），所以只要按钮来源。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buttons"/> is <see langword="null"/>.</exception>
    public static EditorOptionsArtwork? TryCreate(IDialogButtonArtworkSource buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        if (ReadFrames(IcnType.Spanel, SpanelFrameCount) is not { } spanel ||
            ReadFrames(IcnType.Cspanel, CspanelFrameCount) is not { } cspanel ||
            ComposeGameOptionFrames() is not { } gameOptions ||
            ReadFrames(IcnType.Espanel, EspanelFrameCount) is not { } espanel)
        {
            return null;
        }

        return new EditorOptionsArtwork
        {
            SpanelFrames = spanel,
            CspanelFrames = cspanel,
            GameOptionFrames = gameOptions,
            EspanelFrames = espanel,
            Buttons = buttons,
        };
    }

    /// <summary>How many frames of <c>SPANEL</c> the page reads, the highest index plus one.</summary>
    private const int SpanelFrameCount = 22;

    /// <summary>How many frames of <c>CSPANEL</c> the page reads.</summary>
    private const int CspanelFrameCount = 3;

    /// <summary>How many frames of <c>ESPANEL</c> the page reads.</summary>
    private const int EspanelFrameCount = 6;

    /// <summary>Edge of the two icons the page composes, 65 by 65 in the reference.</summary>
    private const int OptionIconSize = 65;

    /// <summary>
    /// Builds the two <c>GAME_OPTION_ICON</c> frames, the hotkeys and the graphics icons.
    /// </summary>
    /// <remarks>
    /// 这两个图标不是归档里的帧，参考在载入时合成（<c>game_assets.cpp:5187-5203</c>）：从 <c>ESPANBKG</c> 的
    /// (69, 47) 抠一块 65×65 的底，再把 <c>hotkeys_option_icon.image</c> / <c>graphics_option_icon.image</c>
    /// 按它们自己的偏移贴上去。H2D 归档没加载时图标缺一块，底还在——参考的读取器在初始化之前也是这个行为。
    /// </remarks>
    private static IReadOnlyList<Sprite>? ComposeGameOptionFrames()
    {
        if (GameAssets.GetImage(GameAssets.GetFileName(IcnType.Espanbkg), 0) is not { } sheet)
        {
            return null;
        }

        var background = new Rect(69, 47, OptionIconSize, OptionIconSize);
        string[] icons = ["hotkeys_option_icon.image", "graphics_option_icon.image"];
        Sprite[] frames = new Sprite[icons.Length];

        for (int i = 0; i < icons.Length; ++i)
        {
            var frame = new Sprite(OptionIconSize, OptionIconSize);

            ImageOps.Copy(sheet, background, frame, new Point(0, 0));

            if (H2DResources.TryReadCurrentImage(icons[i], out Sprite? icon))
            {
                ImageOps.BlitSprite(frame, new Point(icon.OffsetX, icon.OffsetY), icon);
            }

            frames[i] = frame;
        }

        return frames;
    }

    private static IReadOnlyList<Sprite>? ReadFrames(IcnType type, int count)
    {
        string fileName = GameAssets.GetFileName(type);
        Sprite[] frames = new Sprite[count];

        for (int i = 0; i < count; ++i)
        {
            if (GameAssets.GetImage(fileName, i) is not { } frame)
            {
                return null;
            }

            frames[i] = frame;
        }

        return frames;
    }
}

/// <summary>
/// The options window of the map editor, <c>openEditorOptionsDialog()</c> of the reference
/// (<c>src/fheroes2/editor/editor_options.cpp:113-233</c>).
/// </summary>
/// <remarks>
/// <para>
/// The same three by three grid as the system options: language, graphics and audio on the first row, hot keys, object
/// animation and passability on the second, interface set, cursor and scroll speed on the third. Language, graphics,
/// audio and hot keys open a page of their own; the other five are reported to the caller, which is what the loop of
/// <c>openEditorSettings()</c> does — the editor has to redraw the map after the animation and passability toggles and
/// rebuild its panels after an interface change, so none of them is applied here.
/// </para>
/// <para>
/// The reference saves the configuration when the window closes (<c>:311-315</c>); the port has no configuration file
/// layer, and the caller of the port applies the change, so there is nothing to report here.
/// </para>
/// </remarks>
public sealed class EditorOptionsDialog
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

    /// <summary>Hot keys icon, <c>hotKeyRoi</c>.</summary>
    public static readonly Rect HotKeysArea = new(
        OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY + OptionItem.OptionsStepY,
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Animation icon, <c>animationRoi</c>.</summary>
    public static readonly Rect AnimationArea = new(
        OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
        OptionItem.OptionsOffsetY + OptionItem.OptionsStepY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Passability icon, <c>passabilityRoi</c>.</summary>
    public static readonly Rect PassabilityArea = new(
        OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2),
        OptionItem.OptionsOffsetY + OptionItem.OptionsStepY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Interface set icon, <c>interfaceTypeRoi</c>.</summary>
    public static readonly Rect InterfaceTypeArea = new(
        OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2),
        OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Cursor icon, <c>cursorTypeRoi</c>.</summary>
    public static readonly Rect CursorTypeArea = new(
        OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
        OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2), OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    /// <summary>Scroll speed icon, <c>scrollSpeedRoi</c>.</summary>
    public static readonly Rect ScrollSpeedArea = new(
        OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2),
        OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2), OptionItem.OptionIconSize, OptionItem.OptionIconSize);

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly EditorOptionsArtwork _artwork;
    private readonly Rect _languageRoi;
    private readonly Rect _graphicsRoi;
    private readonly Rect _audioRoi;
    private readonly Rect _hotKeysRoi;
    private readonly Rect _animationRoi;
    private readonly Rect _passabilityRoi;
    private readonly Rect _interfaceTypeRoi;
    private readonly Rect _cursorTypeRoi;
    private readonly Rect _scrollSpeedRoi;

    private DialogPopup? _popup;

    private EditorOptionsDialog(Size screen, EditorOptionsArtwork artwork)
    {
        _artwork = artwork;

        _window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);

        Rect roi = _window.ActiveArea;

        _languageRoi = LanguageArea with { X = LanguageArea.X + roi.X, Y = LanguageArea.Y + roi.Y };
        _graphicsRoi = GraphicsArea with { X = GraphicsArea.X + roi.X, Y = GraphicsArea.Y + roi.Y };
        _audioRoi = AudioArea with { X = AudioArea.X + roi.X, Y = AudioArea.Y + roi.Y };
        _hotKeysRoi = HotKeysArea with { X = HotKeysArea.X + roi.X, Y = HotKeysArea.Y + roi.Y };
        _animationRoi = AnimationArea with { X = AnimationArea.X + roi.X, Y = AnimationArea.Y + roi.Y };
        _passabilityRoi = PassabilityArea with { X = PassabilityArea.X + roi.X, Y = PassabilityArea.Y + roi.Y };
        _interfaceTypeRoi = InterfaceTypeArea with { X = InterfaceTypeArea.X + roi.X, Y = InterfaceTypeArea.Y + roi.Y };
        _cursorTypeRoi = CursorTypeArea with { X = CursorTypeArea.X + roi.X, Y = CursorTypeArea.Y + roi.Y };
        _scrollSpeedRoi = ScrollSpeedArea with { X = ScrollSpeedArea.X + roi.X, Y = ScrollSpeedArea.Y + roi.Y };

        _buttons = ButtonGroup.CreateStandard(roi, DialogButton.Ok, artwork.Buttons);
    }

    /// <summary>The window the page is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OK button of the page.</summary>
    public ButtonGroup Buttons => _buttons;

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

    /// <summary>Area of the hot keys option, in screen coordinates.</summary>
    public Rect HotKeysRoi => _hotKeysRoi;

    /// <summary>Area of the animation option, in screen coordinates.</summary>
    public Rect AnimationRoi => _animationRoi;

    /// <summary>Area of the passability option, in screen coordinates.</summary>
    public Rect PassabilityRoi => _passabilityRoi;

    /// <summary>Area of the interface set option, in screen coordinates.</summary>
    public Rect InterfaceTypeRoi => _interfaceTypeRoi;

    /// <summary>Area of the cursor option, in screen coordinates.</summary>
    public Rect CursorTypeRoi => _cursorTypeRoi;

    /// <summary>Area of the scroll speed option, in screen coordinates.</summary>
    public Rect ScrollSpeedRoi => _scrollSpeedRoi;

    /// <summary>
    /// Builds the page, or returns <see langword="null"/> when the artwork is incomplete
    /// (<c>editor_options.cpp:130-151</c>).
    /// </summary>
    /// <param name="screen">Screen to centre on.</param>
    /// <param name="artwork">Panel frames and the OK button.</param>
    /// <returns>The page, or <see langword="null"/> when a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public static EditorOptionsDialog? TryCreate(Size screen, EditorOptionsArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new EditorOptionsDialog(screen, artwork);
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
    /// Handles one frame: the button, the nine options and their right clicks.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>What the caller has to do, <see cref="EditorOptionsAction.Exit"/> when the window has to close, or
    /// <see cref="EditorOptionsAction.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorOptionsAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (_buttons.ProcessEvents(events, events.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay : null) ==
                DialogButton.Ok ||
            events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.F10))
        {
            return EditorOptionsAction.Exit;
        }

        if (events.MouseClickLeft(_languageRoi))
        {
            return EditorOptionsAction.Language;
        }

        if (events.MouseClickLeft(_graphicsRoi))
        {
            return EditorOptionsAction.Graphics;
        }

        if (events.MouseClickLeft(_audioRoi))
        {
            return EditorOptionsAction.AudioSettings;
        }

        if (events.MouseClickLeft(_hotKeysRoi))
        {
            return EditorOptionsAction.HotKeys;
        }

        if (events.MouseClickLeft(_animationRoi))
        {
            return EditorOptionsAction.Animation;
        }

        if (events.MouseClickLeft(_passabilityRoi))
        {
            return EditorOptionsAction.Passability;
        }

        if (events.MouseClickLeft(_interfaceTypeRoi))
        {
            return EditorOptionsAction.InterfaceType;
        }

        if (events.MouseClickLeft(_cursorTypeRoi))
        {
            return EditorOptionsAction.CursorType;
        }

        // 参考对滚动速度先看左键、再看滚轮（:187-195）：点一下走一档并回绕，滚轮走一档不回绕。
        if (events.MouseClickLeft(_scrollSpeedRoi))
        {
            return EditorOptionsAction.ScrollSpeed;
        }

        if (events.IsMouseWheelUpInArea(_scrollSpeedRoi))
        {
            return EditorOptionsAction.IncreaseScrollSpeed;
        }

        if (events.IsMouseWheelDownInArea(_scrollSpeedRoi))
        {
            return EditorOptionsAction.DecreaseScrollSpeed;
        }

        ShowExplanation(events);

        return EditorOptionsAction.None;
    }

    private void DrawOptions(PixelBuffer target)
    {
        Settings settings = Settings.Get();

        OptionItem.DrawLanguage(target, _languageRoi, _artwork.SpanelFrames[18],
            GameLanguage.GetName(GameLanguage.Current), OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawGraphics(target, _graphicsRoi, _artwork.GameOptionFrames[1],
            OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawAudio(target, _audioRoi, _artwork.SpanelFrames[1], OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawHotKeys(target, _hotKeysRoi, _artwork.GameOptionFrames[0],
            OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawAnimation(target, _animationRoi,
            _artwork.EspanelFrames[settings.IsEditorAnimationEnabled ? 1 : 0], settings.IsEditorAnimationEnabled,
            OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawPassability(target, _passabilityRoi,
            _artwork.EspanelFrames[settings.IsEditorPassabilityEnabled ? 5 : 4], settings.IsEditorPassabilityEnabled,
            OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawInterfaceType(target, _interfaceTypeRoi,
            _artwork.SpanelFrames[InterfaceFrame(settings.InterfaceType)], settings.InterfaceType,
            OptionItem.ThreeElementsRowWidth);

        OptionItem.DrawCursor(target, _cursorTypeRoi,
            _artwork.SpanelFrames[settings.IsMonochromeCursor ? 20 : 21], settings.IsMonochromeCursor,
            OptionItem.ThreeElementsRowWidth);

        // 参考把滚动速度写死成两格宽（ui_option_item.cpp:63），这一行里偏偏是三个格子，照抄。
        OptionItem.DrawScrollSpeed(target, _scrollSpeedRoi, ScrollSpeedIcon(settings.ScrollSpeed),
            settings.ScrollSpeed, OptionItem.TwoElementsRowWidth);
    }

    /// <summary>The icon of an interface set, <c>ui_option_item.cpp:101-118</c>.</summary>
    /// <param name="interfaceType">The set that is selected.</param>
    /// <returns>Frame 15 for the dynamic set, 16 for the good one, 17 for the evil one.</returns>
    private static int InterfaceFrame(InterfaceType interfaceType) => interfaceType switch
    {
        InterfaceType.Dynamic => 15,
        InterfaceType.Good => 16,
        InterfaceType.Evil => 17,
        _ => throw new ArgumentOutOfRangeException(nameof(interfaceType), interfaceType, "Not an interface set."),
    };

    /// <summary>The icon of a scroll speed, <c>ui_option_item.cpp:63-77</c>.</summary>
    /// <param name="speed">The speed that is selected.</param>
    /// <returns><c>SPANEL</c> frame 9 when scrolling is off, <c>CSPANEL</c> 0 for slow and normal, 1 for fast, 2 for
    /// the fastest.</returns>
    private Sprite ScrollSpeedIcon(AdventureMapScrollSpeed speed) => speed switch
    {
        AdventureMapScrollSpeed.None => _artwork.SpanelFrames[9],
        AdventureMapScrollSpeed.Slow or AdventureMapScrollSpeed.Normal => _artwork.CspanelFrames[0],
        AdventureMapScrollSpeed.Fast => _artwork.CspanelFrames[1],
        AdventureMapScrollSpeed.VeryFast => _artwork.CspanelFrames[2],
        _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, "Not a scroll speed."),
    };

    /// <summary>Fills <see cref="_popup"/> for a right click, <c>editor_options.cpp:197-226</c>.</summary>
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
        else if (events.IsMouseRightButtonPressedInArea(_hotKeysRoi))
        {
            _popup = new DialogPopup("Hot Keys", "Check and configure all the hot keys present in the game.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_animationRoi))
        {
            _popup = new DialogPopup("Animation", "Toggle animation of the objects.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_passabilityRoi))
        {
            _popup = new DialogPopup("Passability", "Toggle display of objects' passability.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_interfaceTypeRoi))
        {
            _popup = new DialogPopup("Interface Type", "Toggle the type of interface you want to use.");
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
        else if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup("Okay", "Exit this menu.");
        }
    }
}
