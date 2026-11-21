using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.App.Fonts;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// Which page of the editor main menu is on screen, the three branches of
/// <c>Editor::menuMain()</c> (<c>editor_mainmenu.cpp:225-378</c>).
/// </summary>
public enum EditorMenuStage
{
    /// <summary>New Map / Load Map / Main Menu, the state the menu opens in.</summary>
    MainMenu,

    /// <summary>From Scratch / Random / Back, after New Map was chosen.</summary>
    NewMap,

    /// <summary>The four map sizes, after From Scratch or Random was chosen.</summary>
    MapSize,
}

/// <summary>What the editor main menu asks the host to do.</summary>
public enum EditorMenuAction
{
    /// <summary>Create a blank map of the requested size, <c>editNewMapFromScratch()</c>.</summary>
    NewMapFromScratch,

    /// <summary>Create a generated map of the requested size, <c>editNewRandomMap()</c>.</summary>
    NewRandomMap,

    /// <summary>Open the map list, <c>Editor::menuLoadMap()</c>.</summary>
    LoadMap,

    /// <summary>Leave the editor, <c>GameMode::MAIN_MENU</c>.</summary>
    MainMenu,
}

/// <summary>
/// A request of the editor main menu: one action and, for the two map creation actions, the edge
/// length the user picked.
/// </summary>
/// <param name="Action">Action the user asked for.</param>
/// <param name="MapSize">
/// Size the new map has to have; <see cref="MapSize.None"/> for the actions that do not create a map.
/// </param>
public readonly record struct EditorMenuRequest(EditorMenuAction Action, MapSize MapSize = MapSize.None);

/// <summary>
/// The entry screen of the map editor: the <c>EDITOR.ICN</c> artwork with a gem decorated stone
/// window whose buttons walk New Map -&gt; From Scratch / Random -&gt; map size, a port of
/// <c>Editor::menuMain()</c> (<c>src/fheroes2/editor/editor_mainmenu.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference runs the menu as one loop with three exclusive branches; the port keeps the same
/// three states in <see cref="Stage"/> and turns every click into an <see cref="EditorMenuRequest"/>
/// that the host resolves, because a scene does not own the game loop. The drawing order, the button
/// artwork (<c>BUTTONS_EDITOR_MENU_GOOD.ICN</c> frames 0..7) and the layout arithmetic - including the
/// single column placement of the window at the right hand side of the artwork - are the ones of
/// <c>getSymmetricDialogActiveArea()</c> and <c>StandardWindow::renderSymmetricButtons()</c>.
/// </para>
/// <para>
/// The reference restores the empty dialog and draws only the buttons of the current state, so the
/// buttons of the other states are erased rather than painted in their disabled look. The port
/// redraws the whole frame every time and therefore draws nothing for the other states - the empty
/// stone interior is what the reference's <c>emptyDialog.restore()</c> leaves behind.
/// </para>
/// <para>
/// Right clicking a button shows the <c>showStandardTextMessage( ..., Dialog::ZERO )</c> box of the
/// reference while the button is held, and the hot keys are the defaults of
/// <c>game_hotkeys.cpp</c>: N, L, S, R, S/M/L/X and Escape.
/// </para>
/// </remarks>
public sealed class EditorMainMenuScene : IScene
{
    /// <summary>The menu artwork of the editor, <c>ICN::EDITOR</c> frame 0.</summary>
    public const string BackgroundAssetName = "EDITOR.ICN";

    /// <summary>The editor's New Map and Load Map buttons, frames 0..3 of the reference's table.</summary>
    public const string MainButtonAssetName = "BTNEMAIN.ICN";

    /// <summary>The From Scratch and Random buttons, frames 8..11 of the reference's table.</summary>
    public const string NewMapButtonAssetName = "BTNENEW.ICN";

    /// <summary>The four map size buttons, frames 12..19 of the reference's table.</summary>
    public const string MapSizeButtonAssetName = "BTNESIZE.ICN";

    /// <summary>
    /// The blank button the Main Menu and Back labels are printed on, <c>ICN::EMPTY_GOOD_BUTTON</c>
    /// of <c>getCustomNormalButton()</c>.
    /// </summary>
    public const string BlankButtonAssetName = ButtonFactory.GoodBlankAssetName;

    /// <summary>Vertical distance between two buttons, <c>spaceBetweenButtons</c> of the reference.</summary>
    public const int SpaceBetweenButtons = 10;

    private readonly ScreenBuffer _frame;
    private readonly AssetBundle _assets;
    private readonly Sprite _background;
    private readonly Sprite _tile;
    private readonly Rect _backgroundArea;
    private readonly StandardWindow _window;
    private readonly FrameBoxChrome? _messageChrome;
    private readonly GameFonts _fonts;
    private readonly ButtonSprite _newMap;
    private readonly ButtonSprite _loadMap;
    private readonly ButtonSprite _mainMenu;
    private readonly ButtonSprite _back;
    private readonly ButtonSprite _scratch;
    private readonly ButtonSprite _random;
    private readonly ButtonSprite[] _sizes;

    private EditorMenuStage _stage = EditorMenuStage.MainMenu;
    private bool _randomMap;
    private Point? _cursor;
    private bool _leftButtonDown;
    private EditorMenuAction? _pressedAction;
    private MessageBox? _tip;

    private EditorMainMenuScene(
        ScreenBuffer frame,
        AssetBundle assets,
        Sprite background,
        Sprite tile,
        StandardWindow window,
        FrameBoxChrome? messageChrome,
        GameFonts fonts,
        ButtonSprite newMap,
        ButtonSprite loadMap,
        ButtonSprite mainMenu,
        ButtonSprite back,
        ButtonSprite scratch,
        ButtonSprite random,
        ButtonSprite[] sizes)
    {
        _frame = frame;
        _assets = assets;
        _background = background;
        _tile = tile;
        _backgroundArea = new Rect(background.OffsetX, background.OffsetY, background.Width, background.Height);
        _window = window;
        _messageChrome = messageChrome;
        _fonts = fonts;
        _newMap = newMap;
        _loadMap = loadMap;
        _mainMenu = mainMenu;
        _back = back;
        _scratch = scratch;
        _random = random;
        _sizes = sizes;
    }

    /// <summary>The state the menu is in, the branch the reference loop is currently in.</summary>
    public EditorMenuStage Stage => _stage;

    /// <summary>
    /// Whether the map that is about to be created is generated, i.e. the user came through Random
    /// instead of From Scratch.
    /// </summary>
    public bool IsRandomMap => _randomMap;

    /// <summary>The tooltip that is on screen while the right mouse button is held.</summary>
    public MessageBox? Tip => _tip;

    /// <summary>The window the buttons sit in, exposed for tests and diagnostics.</summary>
    public StandardWindow Window => _window;

    /// <summary>
    /// Builds the menu from the game data.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> when the artwork, the stone window or the fonts are missing; the host
    /// then stays on the diagnostics screen instead of showing a half empty menu.
    /// </returns>
    public static EditorMainMenuScene? TryCreate(ScreenBuffer frame, AssetBundle? assets, GameFonts? fonts)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (assets is null || fonts is null)
        {
            GameLog.Warn("The editor main menu needs the game data and the bitmap fonts.");
            return null;
        }

        try
        {
            IcnSpriteFile? background = assets.TryReadIcn(BackgroundAssetName);
            IcnSpriteFile? mainButtons = assets.TryReadIcn(MainButtonAssetName);
            IcnSpriteFile? newMapButtons = assets.TryReadIcn(NewMapButtonAssetName);
            IcnSpriteFile? sizeButtons = assets.TryReadIcn(MapSizeButtonAssetName);
            IcnSpriteFile? horizontal = assets.TryReadIcn(StandardWindowChrome.HorizontalAssetName);
            IcnSpriteFile? vertical = assets.TryReadIcn(StandardWindowChrome.VerticalAssetName);
            IcnSpriteFile? windowBackground = assets.TryReadIcn(StandardWindowChrome.BackgroundAssetName);

            StandardWindowChrome? chrome = StandardWindowChrome.TryCreate(
                horizontal?.Sprites, vertical?.Sprites, windowBackground?.Sprites);

            // The reference assembles the twenty frames of the button table from the editor's own
            // artwork and only generates the two labels it has no frames for; see the
            // ICN::BUTTONS_EDITOR_MENU_GOOD case of game_assets.cpp:1724.
            const int RequiredFrames = 20;

            if (background is not { Count: > 0 } || mainButtons is not { Count: >= 4 } ||
                newMapButtons is not { Count: >= 4 } || sizeButtons is not { Count: >= 8 } ||
                chrome is null)
            {
                GameLog.Warn(
                    $"The editor main menu artwork is missing: '{BackgroundAssetName}' " +
                    $"({background?.Count ?? 0} frames), '{MainButtonAssetName}' " +
                    $"({mainButtons?.Count ?? 0} frames), '{NewMapButtonAssetName}' " +
                    $"({newMapButtons?.Count ?? 0} frames), '{MapSizeButtonAssetName}' " +
                    $"({sizeButtons?.Count ?? 0} frames), stone window " +
                    (chrome is null ? "missing." : "ok."));
                return null;
            }

            List<Sprite> buttonFrames = BuildButtonFrames(mainButtons, newMapButtons, sizeButtons, chrome.Background);

            if (buttonFrames.Count < RequiredFrames)
            {
                GameLog.Warn("The editor main menu buttons could not be generated.");
                return null;
            }

            FrameBoxChrome? messageChrome = CreateMessageChrome(assets);

            // The two buttons the window is sized around, exactly like the reference: they are created
            // first, placed by the window and only then do the other buttons take their position.
            ButtonGroup mainModeButtons = new();
            ButtonSprite newMap = mainModeButtons.CreateButton(0, 0, buttonFrames[0], buttonFrames[1], DialogButton.None);
            ButtonSprite loadMap = mainModeButtons.CreateButton(0, 0, buttonFrames[2], buttonFrames[3], DialogButton.None);

            Rect newMapArea = newMap.Area;
            int extraHeight = (newMapArea.Height + SpaceBetweenButtons) * 4;

            // getSymmetricDialogActiveArea() puts a single column at the right hand side of the main
            // menu artwork instead of centring it: the size comes from the port, the position has to be
            // taken from the reference.
            Rect activeArea = StandardWindow.ActiveAreaForSymmetricButtons(
                mainModeButtons, true, extraHeight, frame.Size);
            Rect placed = new(
                frame.Size.Width - background.Sprites[0].OffsetX - (activeArea.Width + StandardWindow.BorderWidth) - 8,
                background.Sprites[0].OffsetY + StandardWindow.BorderWidth + 8,
                activeArea.Width,
                activeArea.Height);

            StandardWindow window = StandardWindow.Create(chrome, placed);
            window.PlaceSymmetricButtons(mainModeButtons, 0, true);

            newMapArea = newMap.Area;

            // Main Menu and Back share one slot under the group: the reference creates Back on top of
            // Main Menu and shows whichever the state needs.
            int bottomY = (placed.Y * 2) + placed.Height - newMapArea.Y - newMapArea.Height;
            ButtonSprite mainMenu = new(buttonFrames[4], buttonFrames[5], null, newMapArea.X, bottomY);
            ButtonSprite back = new(buttonFrames[6], buttonFrames[7], null, newMapArea.X, bottomY);

            ButtonGroup creationButtons = new();
            ButtonSprite scratch = creationButtons.CreateButton(
                newMapArea.X, newMapArea.Y, buttonFrames[8], buttonFrames[9], DialogButton.None);
            ButtonSprite random = creationButtons.CreateButton(
                newMapArea.X, newMapArea.Y + newMapArea.Height + SpaceBetweenButtons,
                buttonFrames[10], buttonFrames[11], DialogButton.None);

            ButtonSprite[] sizes = new ButtonSprite[4];
            for (int index = 0; index < sizes.Length; ++index)
            {
                int released = (index + 6) * 2;
                sizes[index] = new ButtonSprite(
                    buttonFrames[released],
                    buttonFrames[released + 1],
                    null,
                    newMapArea.X,
                    newMapArea.Y + (index * (newMapArea.Height + SpaceBetweenButtons)));
            }

            EditorMainMenuScene scene = new(
                frame, assets, background.Sprites[0], chrome.Background, window, messageChrome, fonts,
                newMap, loadMap, mainMenu, back, scratch, random, sizes);

            GameLog.Info(
                $"Editor main menu ready: {BackgroundAssetName} {background.Sprites[0].Width}x" +
                $"{background.Sprites[0].Height}, window {placed.Width}x{placed.Height} at " +
                $"({placed.X}, {placed.Y}), {RequiredFrames} button frames from " +
                $"'{MainButtonAssetName}', '{NewMapButtonAssetName}', '{MapSizeButtonAssetName}' and " +
                $"'{BlankButtonAssetName}', " +
                (messageChrome is null ? "no tooltips." : "right click tooltips."));

            return scene;
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The editor main menu could not be created: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Assembles the twenty frames of the editor button table the way
    /// <c>ICN::BUTTONS_EDITOR_MENU_GOOD</c> is built for the original resources
    /// (<c>game_assets.cpp:1724-1777</c>).
    /// </summary>
    /// <remarks>
    /// Frames 0..3 come from <c>BTNEMAIN</c>, 8..11 from <c>BTNENEW</c> and 12..19 from <c>BTNESIZE</c>.
    /// Main Menu and Back have no artwork of their own, so <c>makeButtonSprites()</c> prints them on
    /// <c>EMPTY_GOOD_BUTTON</c> at the size of the New Map button minus its horizontal margin, with
    /// <c>STONEBAK</c> behind the corners the pressed plate leaves transparent.
    /// </remarks>
    private static List<Sprite> BuildButtonFrames(
        IcnSpriteFile mainButtons,
        IcnSpriteFile newMapButtons,
        IcnSpriteFile sizeButtons,
        Sprite stoneBackground)
    {
        List<Sprite> frames = new(20);

        for (int index = 0; index < 4; ++index)
        {
            frames.Add(mainButtons.Sprites[index]);
        }

        Size textArea = new(
            mainButtons.Sprites[0].Width - ButtonFactory.TextWidthMargin,
            mainButtons.Sprites[0].Height);

        (Sprite Released, Sprite Pressed) mainMenu = ButtonFactory.MakeButtonSprites(
            "MAIN\nMENU", textArea, isEvilInterface: false, stoneBackground);
        frames.Add(mainMenu.Released);
        frames.Add(mainMenu.Pressed);

        (Sprite Released, Sprite Pressed) back = ButtonFactory.MakeButtonSprites(
            "BACK", textArea, isEvilInterface: false, stoneBackground);
        frames.Add(back.Released);
        frames.Add(back.Pressed);

        for (int index = 0; index < 4; ++index)
        {
            frames.Add(newMapButtons.Sprites[index]);
        }

        for (int index = 0; index < 8; ++index)
        {
            frames.Add(sizeButtons.Sprites[index]);
        }

        return frames;
    }

    /// <summary>Records the cursor, which drives the pressed look of the buttons and the tooltip.</summary>
    /// <param name="cursor">Cursor position in logical pixels, or <see langword="null"/> when it left.</param>
    public void SetCursor(Point? cursor)
    {
        _cursor = cursor;

        if (_tip is not null)
        {
            _tip = CreateTip();
        }
    }

    /// <summary>
    /// Presses or releases the left mouse button and returns the request the release completes.
    /// </summary>
    /// <param name="down">Whether the left mouse button is now held.</param>
    /// <returns>The action the click asked for, or <see langword="null"/>.</returns>
    public EditorMenuRequest? SetLeftButton(bool down)
    {
        if (down)
        {
            _leftButtonDown = true;
            _pressedAction = ActionAt(_cursor);
            return null;
        }

        _leftButtonDown = false;

        EditorMenuAction? pressed = _pressedAction;
        _pressedAction = null;

        if (pressed is not { } action || ActionAt(_cursor) != action)
        {
            return null;
        }

        return Choose(action);
    }

    /// <summary>
    /// Tracks the right mouse button: pressing it over a button opens the tooltip of that button,
    /// releasing it closes the box again (<c>Dialog::ZERO</c> of the reference).
    /// </summary>
    /// <param name="down">Whether the right mouse button is currently held.</param>
    public void SetRightButton(bool down) => _tip = down ? CreateTip() : null;

    /// <summary>
    /// Resolves a hot key of the current state, the <c>Game::HotKeyPressEvent()</c> calls of the
    /// reference loop.
    /// </summary>
    /// <param name="hotKey">Character that was pressed.</param>
    /// <returns>The action the key asked for, or <see langword="null"/>.</returns>
    public EditorMenuRequest? Select(char hotKey)
    {
        // An open tooltip swallows the key press, like every dialog of the reference does.
        if (_tip is not null || char.IsControl(hotKey) && hotKey is not ('\u001b' or '\r' or '\n'))
        {
            return null;
        }

        char key = char.ToUpperInvariant(hotKey);
        bool escape = hotKey == '\u001b';

        switch (_stage)
        {
            case EditorMenuStage.MainMenu:
                if (key == 'N')
                {
                    EnterNewMap();
                    return null;
                }

                if (key == 'L')
                {
                    return new EditorMenuRequest(EditorMenuAction.LoadMap);
                }

                return escape ? new EditorMenuRequest(EditorMenuAction.MainMenu) : null;

            case EditorMenuStage.NewMap:
                if (key == 'S')
                {
                    EnterMapSize(random: false);
                    return null;
                }

                if (key == 'R')
                {
                    EnterMapSize(random: true);
                    return null;
                }

                if (escape)
                {
                    EnterMainMenu();
                }

                return null;

            default:
                if (escape)
                {
                    EnterNewMap();
                    return null;
                }

                int sizeIndex = key switch
                {
                    'S' => 0,
                    'M' => 1,
                    'L' => 2,
                    'X' => 3,
                    _ => -1,
                };

                return sizeIndex < 0 ? null : Choose(MapSizes.All[sizeIndex]);
        }
    }

    /// <summary>Nothing animates in this menu; the artwork and the buttons are static.</summary>
    /// <param name="elapsedSeconds">Time since the previous frame.</param>
    public void Update(double elapsedSeconds)
    {
        // Intentionally empty: the editor main menu of the reference has no animation either.
    }

    /// <inheritdoc/>
    public void DrawFrame(in DiagnosticsState state)
    {
        _frame.InvalidateAll();

        PixelBuffer pixels = _frame.Pixels;

        DrawBackground(pixels);

        _window.Draw(pixels);
        _window.TryApplyGemDecoratedCorners(pixels, _assets);

        ButtonSprite[] visible = VisibleButtons();
        foreach (ButtonSprite button in visible)
        {
            // The port paints every frame from scratch, so the state is set first and the sprite is
            // always blitted; drawOnState() of the reference only paints on a state change.
            if (IsPressed(button))
            {
                button.Press();
            }
            else
            {
                button.Release();
            }

            button.Draw(pixels);
        }

        foreach (ButtonSprite button in visible)
        {
            button.DrawShadow(pixels);
        }

        _tip?.Draw(pixels);
    }

    /// <inheritdoc/>
    public void DrawOverlay(in DiagnosticsState state)
    {
        // The editor main menu draws its text with the game fonts, so there is no vector overlay.
    }

    /// <summary>The buttons of the current state, in the order the reference draws them.</summary>
    private ButtonSprite[] VisibleButtons() => _stage switch
    {
        EditorMenuStage.MainMenu => [_newMap, _loadMap, _mainMenu],
        EditorMenuStage.NewMap => [_scratch, _random, _back],
        _ => [.. _sizes, _back],
    };

    private bool IsPressed(ButtonSprite button) =>
        _leftButtonDown && _pressedAction is not null && _cursor is { } cursor && button.Area.Contains(cursor) &&
        ActionAt(cursor) == _pressedAction;

    /// <summary>The action a click at <paramref name="point"/> would ask for in the current state.</summary>
    private EditorMenuAction? ActionAt(Point? point)
    {
        if (point is not { } cursor)
        {
            return null;
        }

        switch (_stage)
        {
            case EditorMenuStage.MainMenu:
                if (_newMap.Area.Contains(cursor))
                {
                    return EditorMenuAction.NewMapFromScratch;
                }

                if (_loadMap.Area.Contains(cursor))
                {
                    return EditorMenuAction.LoadMap;
                }

                return _mainMenu.Area.Contains(cursor) ? EditorMenuAction.MainMenu : null;

            case EditorMenuStage.NewMap:
                if (_scratch.Area.Contains(cursor))
                {
                    return EditorMenuAction.NewMapFromScratch;
                }

                if (_random.Area.Contains(cursor))
                {
                    return EditorMenuAction.NewRandomMap;
                }

                return _back.Area.Contains(cursor) ? EditorMenuAction.MainMenu : null;

            default:
                for (int index = 0; index < _sizes.Length; ++index)
                {
                    if (_sizes[index].Area.Contains(cursor))
                    {
                        return _randomMap ? EditorMenuAction.NewRandomMap : EditorMenuAction.NewMapFromScratch;
                    }
                }

                return _back.Area.Contains(cursor) ? EditorMenuAction.MainMenu : null;
        }
    }

    /// <summary>
    /// Turns a click into a request or into a state change, the body of the three branches of the
    /// reference loop.
    /// </summary>
    private EditorMenuRequest? Choose(EditorMenuAction action)
    {
        switch (_stage)
        {
            case EditorMenuStage.MainMenu:
                if (action == EditorMenuAction.NewMapFromScratch)
                {
                    EnterNewMap();
                    return null;
                }

                return new EditorMenuRequest(action);

            case EditorMenuStage.NewMap:
                if (action == EditorMenuAction.NewMapFromScratch)
                {
                    EnterMapSize(random: false);
                    return null;
                }

                if (action == EditorMenuAction.NewRandomMap)
                {
                    EnterMapSize(random: true);
                    return null;
                }

                EnterMainMenu();
                return null;

            default:
                if (action == EditorMenuAction.MainMenu)
                {
                    EnterNewMap();
                    return null;
                }

                // The map size buttons are resolved by their position: the state does not tell which of
                // the four was clicked, so the cursor does it.
                for (int index = 0; index < _sizes.Length; ++index)
                {
                    if (_cursor is { } cursor && _sizes[index].Area.Contains(cursor))
                    {
                        return Choose(MapSizes.All[index]);
                    }
                }

                return null;
        }
    }

    private EditorMenuRequest Choose(MapSize size) =>
        new(_randomMap ? EditorMenuAction.NewRandomMap : EditorMenuAction.NewMapFromScratch, size);

    private void EnterMainMenu()
    {
        _stage = EditorMenuStage.MainMenu;
        _tip = null;
        GameLog.Info("Editor main menu: back to the editor's main options.");
    }

    private void EnterNewMap()
    {
        _stage = EditorMenuStage.NewMap;
        _tip = null;
        GameLog.Info("Editor main menu: 'New Map' chosen.");
    }

    private void EnterMapSize(bool random)
    {
        _stage = EditorMenuStage.MapSize;
        _randomMap = random;
        _tip = null;
        GameLog.Info($"Editor main menu: '{(random ? "Random" : "From Scratch")}' chosen, pick a map size.");
    }

    /// <summary>
    /// Draws <c>EDITOR.ICN</c> at its own anchor and fills whatever the artwork does not cover, the
    /// <c>drawEditorMainMenuScreen()</c> of <c>game_mainmenu_ui.cpp:108-116</c>.
    /// </summary>
    private void DrawBackground(PixelBuffer pixels)
    {
        ImageOps.BlitSprite(pixels, _background);

        if (_backgroundArea.Y == 0 && _backgroundArea.X == 0 &&
            _backgroundArea.Width >= pixels.Width && _backgroundArea.Height >= pixels.Height)
        {
            return;
        }

        if (_backgroundArea.Y == 0)
        {
            TileArea(pixels, new Rect(0, 0, _backgroundArea.X, pixels.Height));
            TileArea(pixels, new Rect(
                _backgroundArea.X + _backgroundArea.Width,
                0,
                pixels.Width - _backgroundArea.X - _backgroundArea.Width,
                pixels.Height));
            return;
        }

        TileArea(pixels, new Rect(0, 0, pixels.Width, _backgroundArea.Y));
        TileArea(pixels, new Rect(
            0,
            _backgroundArea.Y + _backgroundArea.Height,
            pixels.Width,
            pixels.Height - _backgroundArea.Y - _backgroundArea.Height));
    }

    /// <summary>Tiles the stone texture into <paramref name="area"/>, <c>renderWindowBackground()</c>.</summary>
    private void TileArea(PixelBuffer pixels, Rect area)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        for (int y = area.Y; y < area.Y + area.Height; y += _tile.Height)
        {
            int height = Math.Min(_tile.Height, area.Y + area.Height - y);

            for (int x = area.X; x < area.X + area.Width; x += _tile.Width)
            {
                int width = Math.Min(_tile.Width, area.X + area.Width - x);

                ImageOps.Blit(pixels, new Point(x, y), _tile.Image, new Rect(0, 0, width, height));
            }
        }
    }

    /// <summary>
    /// Builds the tooltip of the button under the cursor, or <see langword="null"/> when the cursor is
    /// not over one of the buttons of the current state.
    /// </summary>
    private MessageBox? CreateTip()
    {
        if (_messageChrome is null || _cursor is not { } cursor)
        {
            return null;
        }

        (string Header, string Body)? text = _stage switch
        {
            EditorMenuStage.MainMenu => _newMap.Area.Contains(cursor)
                ? ("New Map", "Create a new map, either from scratch or using the random map generator.")
                : _loadMap.Area.Contains(cursor)
                    ? ("Load Map", "Load an existing map.")
                    : _mainMenu.Area.Contains(cursor)
                        ? ("Main Menu", "Exit the Editor and return to the game's Main Menu.")
                        : null,
            EditorMenuStage.NewMap => _scratch.Area.Contains(cursor)
                ? ("From Scratch", "Start from scratch with a blank map.")
                : _random.Area.Contains(cursor)
                    ? ("Random", "Create a randomly generated map.")
                    : _back.Area.Contains(cursor)
                        ? ("Back", "Return to the Editor's main menu options.")
                        : null,
            _ => SizeTip(cursor),
        };

        if (text is not { } tip)
        {
            return null;
        }

        return MessageBox.Create(tip.Header, tip.Body, _messageChrome, _fonts.Yellow, _fonts.Normal);
    }

    private (string Header, string Body)? SizeTip(Point cursor)
    {
        if (_back.Area.Contains(cursor))
        {
            return ("Back", "Return to the previous menu options.");
        }

        for (int index = 0; index < _sizes.Length; ++index)
        {
            if (!_sizes[index].Area.Contains(cursor))
            {
                continue;
            }

            string size = MapSizes.EdgeLength(MapSizes.All[index]).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string body = _randomMap
                ? $"Generate a random map that is {size} squares wide and {size} squares high."
                : $"Create a map that is {size} squares wide and {size} squares high.";

            return ($"{size} x {size}", body);
        }

        return null;
    }

    /// <summary>Loads the message frame, which only costs the tooltips when it is missing.</summary>
    private static FrameBoxChrome? CreateMessageChrome(AssetBundle assets)
    {
        IcnSpriteFile? file = assets.TryReadIcn(FrameBoxChrome.AssetName);
        FrameBoxChrome? chrome = FrameBoxChrome.TryCreate(file?.Sprites);

        if (chrome is null)
        {
            GameLog.Warn(
                $"'{FrameBoxChrome.AssetName}' is missing or has fewer than " +
                $"{FrameBoxChrome.RequiredSpriteCount} sprites; the editor menu will not show tooltips.");
        }

        return chrome;
    }
}
