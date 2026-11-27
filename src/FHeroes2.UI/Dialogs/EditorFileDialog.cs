using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the editor's file menu asks for, the <c>fheroes2::GameMode</c> values
/// <c>EditorInterface::eventFileDialog()</c> can return (<c>editor_interface.cpp:2068-2181</c>).
/// </summary>
/// <remarks>
/// 参考返回的是整个 <c>GameMode</c>，因为调用方是游戏循环；端口用场景代替 <c>GameMode</c> 的 <c>switch</c>，
/// 所以把这七颗按钮的去向写成这个枚举。<c>EDITOR_NEW_MAP</c>/<c>EDITOR_LOAD_MAP</c>/<c>MAIN_MENU</c>/
/// <c>NEW_STANDARD</c>/<c>QUIT_GAME</c> 都能一一对上，剩下的：
/// 存图那一支参考先 <c>hideWindow()</c> 再 <c>saveMapToFile()</c>、最后回 <c>CANCEL</c>；
/// 自动测试成功也回 <c>CANCEL</c>。端口把这两支单独列出来，让调用方知道是「哪颗按钮」而不是「什么都没发生」。
/// </remarks>
public enum EditorFileResult
{
    /// <summary>Nothing was decided yet.</summary>
    None = 0,

    /// <summary>Start a new map, <c>GameMode::EDITOR_NEW_MAP</c>.</summary>
    NewMap = 1,

    /// <summary>Load an existing map, <c>GameMode::EDITOR_LOAD_MAP</c>.</summary>
    LoadMap = 2,

    /// <summary>Leave the editor and play the map, <c>GameMode::NEW_STANDARD</c>.</summary>
    StartMap = 3,

    /// <summary>Save the map, the <c>saveMapToFile()</c> branch of <c>:2111-2117</c>.</summary>
    SaveMap = 4,

    /// <summary>Return to the game's main menu, <c>GameMode::MAIN_MENU</c>.</summary>
    MainMenu = 5,

    /// <summary>Quit the application, <c>GameMode::QUIT_GAME</c>.</summary>
    Quit = 6,

    /// <summary>Run the automatic playtest, the <c>_runAutoPlaytest()</c> branch of <c>:2144-2150</c>.</summary>
    AutoPlaytest = 7,

    /// <summary>The window was closed without doing anything, <c>GameMode::CANCEL</c>.</summary>
    Cancel = 8,
}

/// <summary>
/// The file menu of the map editor as it is drawn: a stone window, the seven generated buttons of
/// <c>BUTTONS_EDITOR_FILE_DIALOG</c> and the small cancel button under them,
/// <c>EditorInterface::eventFileDialog()</c> (<c>editor_interface.cpp:2068-2181</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference generates the seven plates from their labels at asset load time
/// (<c>makeSymmetricBackgroundSprites</c>, <c>game_assets.cpp:1579-1593</c>) and lays them out with
/// <c>StandardWindow</c>; the port has no generated-asset cache, so this class takes the fourteen frames the caller
/// produced with <see cref="ButtonFactory.MakeSymmetricBackgroundSprites"/> and only owns the layout, exactly like
/// <see cref="FileOptionsView"/> does for the game's own file menu.
/// </para>
/// <para>
/// 取消键不同：游戏那张取 <c>SYSTEM.ICN</c> 的 3/4 帧，这里照参考（<c>:2090</c>）取
/// <c>BUTTON_SMALL_CANCEL</c> 的 0/1 帧，位置同样是窗口底边中上方 11 像素（<c>{ 0, 11 }</c>）。
/// </para>
/// <para>
/// 七颗按钮的次序与 <c>optionButtons.button( 0..6 )</c> 一致：新图、载图、开始、存图、主菜单、退出、自动测试。
/// </para>
/// </remarks>
public sealed class EditorFileDialog
{
    /// <summary>Number of the big buttons, the <c>button( 0 )</c> to <c>button( 6 )</c> of the reference.</summary>
    public const int ButtonCount = 7;

    /// <summary>Frames the seven buttons take, released and pressed each.</summary>
    public const int ButtonFrameCount = ButtonCount * 2;

    /// <summary>
    /// Smallest width the generated plates are stretched to, the <c>86</c> of <c>game_assets.cpp:1591</c>.
    /// </summary>
    public const int MinimumButtonWidth = 86;

    /// <summary>
    /// Distance of the cancel button from the bottom centre of the window, the <c>{ 0, 11 }</c> of
    /// <c>editor_interface.cpp:2090</c>.
    /// </summary>
    public static Point CancelOffset => new(0, 11);

    /// <summary>Labels of the seven buttons, in the order of <c>BUTTONS_EDITOR_FILE_DIALOG</c>.</summary>
    /// <remarks>
    /// The reference passes <c>gettext_noop()</c> strings; the port has no translation layer yet, so these are the
    /// English originals of <c>game_assets.cpp:1584-1590</c>.
    /// </remarks>
    public static IReadOnlyList<string> ButtonLabels => Labels;

    private static readonly string[] Labels =
        ["NEW\nMAP", "LOAD\nMAP", "START\nMAP", "SAVE\nMAP", "MAIN\nMENU", "QUIT", "AUTO\nPLAYTEST"];

    /// <summary>Identity of the seven buttons, in the order of <see cref="ButtonLabels"/>.</summary>
    public static IReadOnlyList<DialogButton> ButtonValues => Values;

    private static readonly DialogButton[] Values =
    [
        DialogButton.NewGame, DialogButton.LoadGame, DialogButton.EditorStartMap,
        DialogButton.SaveGame, DialogButton.EditorToMainMenu, DialogButton.Quit,
        DialogButton.EditorAutoPlaytest,
    ];

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly ButtonSprite _cancel;
    private DialogPopup? _popup;

    private EditorFileDialog(StandardWindow window, ButtonGroup buttons, ButtonSprite cancel)
    {
        _window = window;
        _buttons = buttons;
        _cancel = cancel;
    }

    /// <summary>The window the menu is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The seven buttons, already placed in the window.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The cancel button, already placed in the window.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>Area of the new map button, in screen coordinates.</summary>
    public Rect NewMapRoi => _buttons[0].Area;

    /// <summary>Area of the load map button, in screen coordinates.</summary>
    public Rect LoadMapRoi => _buttons[1].Area;

    /// <summary>Area of the start map button, in screen coordinates.</summary>
    public Rect StartMapRoi => _buttons[2].Area;

    /// <summary>Area of the save map button, in screen coordinates.</summary>
    public Rect SaveMapRoi => _buttons[3].Area;

    /// <summary>Area of the main menu button, in screen coordinates.</summary>
    public Rect MainMenuRoi => _buttons[4].Area;

    /// <summary>Area of the quit button, in screen coordinates.</summary>
    public Rect QuitRoi => _buttons[5].Area;

    /// <summary>Area of the auto playtest button, in screen coordinates.</summary>
    public Rect AutoPlaytestRoi => _buttons[6].Area;

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

    /// <summary>
    /// Composes the menu from the generated frames and the small cancel button.
    /// </summary>
    /// <param name="screen">Screen to centre on.</param>
    /// <param name="buttonFrames">
    /// Frames of the seven generated plates: released then pressed per button, fourteen of them.
    /// </param>
    /// <param name="cancelFrames">Frames of <c>BUTTON_SMALL_CANCEL</c>, two at least.</param>
    /// <returns><see langword="null"/> when an input is missing or too short.</returns>
    public static EditorFileDialog? TryCreate(Size screen, IReadOnlyList<Sprite>? buttonFrames,
        IReadOnlyList<Sprite>? cancelFrames)
    {
        if (buttonFrames is null || cancelFrames is null)
        {
            return null;
        }

        if (buttonFrames.Count < ButtonFrameCount || cancelFrames.Count < 2)
        {
            return null;
        }

        ButtonGroup buttons = ButtonGroup.CreateFromFrames([.. buttonFrames.Take(ButtonFrameCount)], Values);

        StandardWindow window = StandardWindow.CreateForSymmetricButtons(buttons, screen: screen);
        window.PlaceSymmetricButtons(buttons);

        ButtonSprite cancel = new(cancelFrames[0], cancelFrames[1]);
        Point cancelPosition = window.RenderPosition(
            CancelOffset, new Size(cancel.Area.Width, cancel.Area.Height), WindowPadding.BottomCenter);
        cancel.SetPosition(cancelPosition.X, cancelPosition.Y);

        return new EditorFileDialog(window, buttons, cancel);
    }

    /// <summary>
    /// Draws the window, the seven buttons and the cancel button with their shadows, in the order the reference
    /// renders them.
    /// </summary>
    /// <remarks>
    /// 按压态由 <see cref="ProcessEvents"/> 事先设好，这里只看状态——同 <see cref="EditorOptionsDialog"/>。
    /// </remarks>
    /// <param name="target">Frame to draw into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);
        _buttons.DrawShadows(target);
        _buttons.Draw(target);
        _cancel.DrawShadow(target);
        _cancel.Draw(target);
    }

    /// <summary>
    /// Handles one frame: the seven buttons, the cancel button, their hot keys and their right clicks
    /// (<c>editor_interface.cpp:2097-2179</c>).
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>The button that was released, <see cref="EditorFileResult.Cancel"/> for the cancel button and the
    /// close hot keys, or <see cref="EditorFileResult.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorFileResult ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // 七颗按钮的按压态由组自己按光标算（drawOnState），这里只取被松开的那一颗。
        DialogButton pressed = _buttons.ProcessEvents(events, null);

        if (pressed != DialogButton.None)
        {
            return FromDialogButton(pressed);
        }

        _cancel.UpdatePressState(events);

        // 参考的次序是「退出」先、「主菜单/开始/自动测试/取消」一条 else-if 链（:2119-2153）；
        // 这里的按钮互不重叠，次序只影响热键同时按下时谁赢，照抄。
        if (HotKeyPressed(events, HotKeyEvent.EditorNewMapMenu))
        {
            return EditorFileResult.NewMap;
        }

        if (HotKeyPressed(events, HotKeyEvent.MainMenuLoadGame))
        {
            return EditorFileResult.LoadMap;
        }

        if (HotKeyPressed(events, HotKeyEvent.WorldSaveGame))
        {
            return EditorFileResult.SaveMap;
        }

        if (HotKeyPressed(events, HotKeyEvent.GlobalAppQuit))
        {
            return EditorFileResult.Quit;
        }

        if (HotKeyPressed(events, HotKeyEvent.EditorToGameMainMenu))
        {
            return EditorFileResult.MainMenu;
        }

        if (events.MouseClickLeft(_cancel.Area) ||
            HotKeyPressed(events, HotKeyEvent.DefaultCancel) ||
            HotKeyPressed(events, HotKeyEvent.DefaultOkay))
        {
            return EditorFileResult.Cancel;
        }

        ShowExplanation(events);

        return EditorFileResult.None;
    }

    /// <summary>Maps a button identity to what it asks for.</summary>
    /// <param name="button">Button that was released.</param>
    /// <returns>The outcome, <see cref="EditorFileResult.None"/> for an identity this menu does not use.</returns>
    public static EditorFileResult FromDialogButton(DialogButton button) => button switch
    {
        DialogButton.NewGame => EditorFileResult.NewMap,
        DialogButton.LoadGame => EditorFileResult.LoadMap,
        DialogButton.EditorStartMap => EditorFileResult.StartMap,
        DialogButton.SaveGame => EditorFileResult.SaveMap,
        DialogButton.EditorToMainMenu => EditorFileResult.MainMenu,
        DialogButton.Quit => EditorFileResult.Quit,
        DialogButton.EditorAutoPlaytest => EditorFileResult.AutoPlaytest,
        _ => EditorFileResult.None,
    };

    /// <summary>Whether the frame carries the key bound to a hot key event.</summary>
    /// <remarks>
    /// 参考用 <c>Game::HotKeyPressEvent()</c>（读全局输入单例）；端口的手写窗口一律拿手上这一帧的
    /// <see cref="LocalEvent"/> 去比，同 <c>AdventureMap.Turn.HotKeyPressed</c>——这样窗口在测试里也能被喂事件。
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="eventId">Hot key event to look for.</param>
    /// <returns><see langword="true"/> when the frame pressed that key.</returns>
    private static bool HotKeyPressed(LocalEvent events, HotKeyEvent eventId) =>
        events.IsAnyKeyPressed && events.PressedKeyValue == GameHotKeys.GetHotKeyForEvent(eventId);

    /// <summary>Fills <see cref="_popup"/> for a right click, <c>editor_interface.cpp:2155-2178</c>.</summary>
    /// <remarks>
    /// 参考的次序：新图、载图、存图、退出、自动测试、主菜单、开始、取消。照抄——虽然这些区域互不重叠。
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    private void ShowExplanation(LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(NewMapRoi))
        {
            _popup = new DialogPopup("New Map",
                "Create a new map, either from scratch or using the random map generator.");
        }
        else if (events.IsMouseRightButtonPressedInArea(LoadMapRoi))
        {
            _popup = new DialogPopup("Load Map", "Load an existing map.");
        }
        else if (events.IsMouseRightButtonPressedInArea(SaveMapRoi))
        {
            _popup = new DialogPopup("Save Map", "Save the current map.");
        }
        else if (events.IsMouseRightButtonPressedInArea(QuitRoi))
        {
            _popup = new DialogPopup("Quit", "Quit out of the map editor.");
        }
        else if (events.IsMouseRightButtonPressedInArea(AutoPlaytestRoi))
        {
            _popup = new DialogPopup("Auto Playtest", "Run an automatic playtest of the map.");
        }
        else if (events.IsMouseRightButtonPressedInArea(MainMenuRoi))
        {
            _popup = new DialogPopup("Main Menu", "Return to the game's Main Menu.");
        }
        else if (events.IsMouseRightButtonPressedInArea(StartMapRoi))
        {
            _popup = new DialogPopup("Start Map", "Leave the Editor and play the map in the Standard Game mode.");
        }
        else if (events.IsMouseRightButtonPressedInArea(_cancel.Area))
        {
            _popup = new DialogPopup("Cancel", "Exit this menu without doing anything.");
        }
    }
}
