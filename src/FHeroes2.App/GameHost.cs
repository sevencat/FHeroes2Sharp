using System.Diagnostics;
using FHeroes2.Assets;
using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Maps;
using FHeroes2.App.Audio;
using FHeroes2.App.Fonts;
using FHeroes2.App.Graphics;
using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Core.Timing;
using FHeroes2.Game.Editor;
using SDL3;

namespace FHeroes2.App;

/// <summary>
/// Composition root and SDL3 host of the desktop client: owns the SDL lifetime, the window and
/// renderer (through <see cref="SdlPresenter"/>), the text backend, the frame loop and the
/// currently active scene.
/// </summary>
public sealed class GameHost : IDisposable
{
    /// <summary><c>SDL_BUTTON_LEFT</c>; picks a sign of the menu.</summary>
    private const int LeftMouseButton = 1;

    /// <summary><c>SDL_BUTTON_RIGHT</c>; shows the tooltip of the sign under it while held.</summary>
    private const int RightMouseButton = 3;

    /// <summary>Frame at which <c>--left-click</c> fires: late enough for the menu to be laid out.</summary>
    private const int LeftClickFrame = 10;

    private readonly CommandLineOptions _options;
    private readonly FrameTimer _timer = new();
    private readonly bool _syntheticCursor;

    private AssetBundle? _assets;
    private Palette _palette = new();
    private ScreenBuffer _frame = null!;
    private SdlPresenter? _presenter;
    private SdlTextRenderer? _text;
    private GameFonts? _fonts;
    private AudioPlayer? _audio;
    private IScene? _scene;
    private IScene? _drawnScene;
    private MainMenuScene? _menu;
    private DiagnosticsScene? _diagnostics;
    private AdventureMapScene? _adventure;
    private EditorScene? _editor;
    private bool _supportsCjk;
    private bool _sdlInitialised;
    private bool _ttfInitialised;
    private bool _running;
    private int _screenshotFrame = -1;
    private bool _screenshotRequested;
    private bool _disposed;

    public GameHost(CommandLineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _syntheticCursor = options.Cursor is not null;

        if (options.ScreenshotPath is not null)
        {
            int captureFrame = options.ScreenshotFrame;
            if (options.SmokeFrames is int smokeFrames && smokeFrames < captureFrame)
            {
                captureFrame = smokeFrames;
            }

            _screenshotFrame = Math.Max(1, captureFrame);
        }
    }

    /// <summary>Process exit code; non zero when mandatory game data or SDL was missing.</summary>
    public int ExitCode { get; private set; }

    /// <summary>Runs the client until the window is closed or the smoke frame budget is spent.</summary>
    public void Run()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            GameLog.Error($"SDL_Init failed: {SDL.GetError()}");
            ExitCode = 1;
            return;
        }

        _sdlInitialised = true;

        if (!TTF.Init())
        {
            GameLog.Error($"TTF_Init failed: {SDL.GetError()}");
            ExitCode = 1;
            return;
        }

        _ttfInitialised = true;
        GameLog.Info($"{BuildInfo.DisplayName} starting (SDL {SDL.GetVersion()}, SDL_ttf {TTF.Version()})");

        LoadGameData();
        _fonts = GameFonts.TryLoad(_assets);

        try
        {
            _presenter = new SdlPresenter(ScreenBuffer.DefaultSize, _options.WindowScale, _options.Fullscreen);
            _presenter.SetPalette(_palette);

            _frame = new ScreenBuffer(ScreenBuffer.DefaultSize, _palette);
            CreateTextRenderer();
            CreateScenes();
            StartAudio();

            GameLog.Info(
                $"Presentation ready: {_presenter.OutputSize} output, x{_presenter.Viewport.Scale:0.##} scale, " +
                $"logical {_presenter.LogicalSize}.");

            if (_options.SmokeFrames is int frames)
            {
                GameLog.Info($"Smoke mode: the client will exit after {frames} rendered frames.");
            }

            _running = true;
            Loop();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            GameLog.Error("The client stopped because of an SDL error", exception);
            ExitCode = 1;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Textures belong to the renderer, so the text backend has to go first.
        _text?.Dispose();
        _text = null;

        _presenter?.Dispose();
        _presenter = null;

        // Music and sound effects own SDL resources of their own, cleaned up before SDL_Quit.
        _audio?.Dispose();
        _audio = null;

        _assets?.Dispose();
        _assets = null;

        if (_ttfInitialised)
        {
            TTF.Quit();
            _ttfInitialised = false;
        }

        if (_sdlInitialised)
        {
            SDL.Quit();
            _sdlInitialised = false;
        }
    }

    private void Loop()
    {
        Stopwatch clock = Stopwatch.StartNew();
        double previous = clock.Elapsed.TotalSeconds;

        while (_running)
        {
            if (!PumpEvents())
            {
                break;
            }

            // --left-click replays one click from inside the loop, i.e. after the event batch and
            // before the scene update, which is the frame order a player produces. Doing it in
            // CreateScenes would skip the first frames of the new scene.
            if (_options.LeftClick && _menu is not null && _timer.FrameCount == LeftClickFrame)
            {
                _menu.SetCursor(_options.Cursor);
                _menu.SetLeftButton(true);

                if (_menu.SetLeftButton(false) is { } clicked)
                {
                    HandleMenuAction(clicked);
                }
            }

            // The menu does not know about scenes: a confirmed scenario surfaces here as soon as the
            // input batch that confirmed it has been handled.
            TryEnterAdventureMap();

            double now = clock.Elapsed.TotalSeconds;

            // Clamp the step so that dragging the window does not teleport the animation.
            double elapsed = Math.Clamp(now - previous, 0.0, 0.5);
            previous = now;

            Update(elapsed);
            _audio?.Update();
            Draw();
        }
    }

    /// <summary>Drains the SDL event queue; returns <see langword="false"/> when the client must stop.</summary>
    private bool PumpEvents()
    {
        while (SDL.PollEvent(out SDL.Event evt))
        {
            switch ((SDL.EventType)evt.Type)
            {
                case SDL.EventType.Quit:
                case SDL.EventType.WindowCloseRequested:
                    return false;

                case SDL.EventType.KeyDown when HandleKeyDown(evt.Key):
                    return false;

                case SDL.EventType.MouseMotion:
                    // With --cursor the synthetic pointer is what a screenshot should show, so the
                    // real one must not steal the hover state.
                    if (!_syntheticCursor)
                    {
                        if (_editor is { } editing)
                        {
                            editing.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }
                        else if (_adventure is { } moving)
                        {
                            moving.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }
                        else
                        {
                            _menu?.SetCursor(ToLogical(evt.Motion.X, evt.Motion.Y));
                        }
                    }

                    break;

                case SDL.EventType.WindowMouseLeave:
                    if (!_syntheticCursor)
                    {
                        _editor?.SetCursor(null);
                        _adventure?.SetCursor(null);
                        _menu?.SetCursor(null);
                    }

                    break;

                case SDL.EventType.MouseButtonDown:
                case SDL.EventType.MouseButtonUp:
                    if (HandleMouseButton(evt.Button))
                    {
                        return false;
                    }

                    break;

                case SDL.EventType.MouseWheel:
                    // SDL reports a positive y when the wheel turns away from the user, which rolls
                    // a list up and moves the view towards the top of the map.
                    if (!_syntheticCursor)
                    {
                        int notches = evt.Wheel.IntegerY != 0 ? evt.Wheel.IntegerY : (int)Math.Sign(evt.Wheel.Y);

                        if (notches != 0)
                        {
                            if (_editor is { } edited)
                            {
                                edited.ScrollByWheel(0, -notches);
                            }
                            else if (_adventure is { } scrolled)
                            {
                                scrolled.ScrollByWheel(0, -notches);
                            }
                            else
                            {
                                _menu?.ScrollScenario(-notches);
                            }
                        }
                    }

                    break;

                default:
                    break;
            }
        }

        return true;
    }

    /// <summary>Handles a key press; returns <see langword="true"/> when the client must stop.</summary>
    private bool HandleKeyDown(in SDL.KeyboardEvent key)
    {
        // Engine wide keys stay available on every screen, including the adventure map.
        switch (key.Key)
        {
            case SDL.Keycode.F11 when !key.Repeat:
                _presenter?.ToggleFullscreen();
                return false;

            case SDL.Keycode.F12 when !key.Repeat:
                _screenshotRequested = true;
                return false;

            default:
                break;
        }

        if (_editor is { } editor)
        {
            return HandleEditorKey(key, editor);
        }

        if (_adventure is { } adventure)
        {
            return HandleAdventureKey(key, adventure);
        }

        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                // Escape closes an open modal window first, the CANCEL button of the original; only
                // a bare menu Escape leaves the client.
                if (_menu?.HasModalDialog == true)
                {
                    _menu.Select((char)SDL.Keycode.Escape);
                    return false;
                }

                return true;

            case SDL.Keycode.Up when _menu?.Scenario is not null:
                _menu.MoveScenarioSelection(-1);
                return false;

            case SDL.Keycode.Down when _menu?.Scenario is not null:
                _menu.MoveScenarioSelection(1);
                return false;

            case SDL.Keycode.Pageup when _menu?.Scenario is not null:
                _menu.MoveScenarioPage(-1);
                return false;

            case SDL.Keycode.Pagedown when _menu?.Scenario is not null:
                _menu.MoveScenarioPage(1);
                return false;

            case SDL.Keycode.Home when _menu?.Scenario is not null:
                _menu.SelectFirstScenario();
                return false;

            case SDL.Keycode.End when _menu?.Scenario is not null:
                _menu.SelectLastScenario();
                return false;

            // The language hot key rebuilds the menu, so it stays out of the way while a modal
            // window owns the text: the settings dialog has its own language row, the scenario list
            // is rebuilt in the new language the next time it opens.
            case SDL.Keycode.F2 when !key.Repeat && _menu?.HasModalDialog != true:
                _menu?.NextLanguage();
                return false;

            default:
                // SDL key codes of the letters are their ASCII values, so the hot keys of the
                // original main menu (N, L, H, C, Q) can be forwarded as characters.
                return _menu is not null
                    && !key.Repeat
                    && _menu.Select((char)key.Key) is { } hotKeyAction
                    && HandleMenuAction(hotKeyAction);
        }
    }

    /// <summary>Handles a mouse button event; returns <see langword="true"/> when the client must stop.</summary>
    private bool HandleMouseButton(in SDL.MouseButtonEvent button)
    {
        if (_editor is { } editor)
        {
            if (!_syntheticCursor)
            {
                editor.SetCursor(ToLogical(button.X, button.Y));
            }

            // The editor paints with the left button and pans with the right one, the way the
            // reference routes the left click to the active instrument and the right one to the
            // map drag.
            if (button.Button == LeftMouseButton)
            {
                if (editor.SetLeftButton(button.Down))
                {
                    GameLog.Info($"Painted with '{editor.Edits.Brush}'; {editor.Edits.UndoDepth} undoable strokes.");
                }
            }
            else if (button.Button == RightMouseButton)
            {
                if (editor.SetRightButton(button.Down))
                {
                    GameLog.Info($"Picked the '{editor.Edits.Brush}' brush from the map.");
                }
            }

            return false;
        }

        if (_adventure is { } adventure)
        {
            // The map answers the left button only: the right one opens the info window of the
            // object under the pointer in the reference, which is not implemented yet.
            if (!_syntheticCursor)
            {
                adventure.SetCursor(ToLogical(button.X, button.Y));
            }

            if (button.Button == LeftMouseButton)
            {
                adventure.SetLeftButton(button.Down);
            }

            return false;
        }

        if (_menu is null)
        {
            return false;
        }

        if (!_syntheticCursor)
        {
            _menu.SetCursor(ToLogical(button.X, button.Y));
        }

        switch (button.Button)
        {
            case RightMouseButton:
                _menu.SetRightButton(button.Down);
                return false;

            case LeftMouseButton:
                return _menu.SetLeftButton(button.Down) is { } action && HandleMenuAction(action);

            default:
                return false;
        }
    }

    /// <summary>Reacts to the choice of a main menu sign; returns <see langword="true"/> to stop.</summary>
    private bool HandleMenuAction(MainMenuAction action)
    {
        // The editor opens on the first map of the catalog, the way the reference reacts to its
        // editor button; choosing a map is the editor main menu of a later milestone.
        if (action == MainMenuAction.Editor)
        {
            OpenEditor(null);

            if (_editor is { } editor)
            {
                _scene = editor;
            }

            return false;
        }

        if (action != MainMenuAction.Quit)
        {
            // The other signs are wired up from M3 on; the scene already shows this in its status bar.
            return false;
        }

        GameLog.Info("Quit selected, closing the client.");
        _running = false;
        return true;
    }

    /// <summary>
    /// Handles the keys of the adventure map: the arrow keys scroll by one tile, Home returns to the
    /// top left corner and Escape goes back to the main menu (the reference opens the system dialog
    /// there; that dialog belongs to a later milestone).
    /// </summary>
    private bool HandleAdventureKey(in SDL.KeyboardEvent key, AdventureMapScene adventure)
    {
        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                ReturnToMenu();
                return false;

            case SDL.Keycode.Left:
                adventure.ScrollByTiles(-1, 0);
                return false;

            case SDL.Keycode.Right:
                adventure.ScrollByTiles(1, 0);
                return false;

            case SDL.Keycode.Up:
                adventure.ScrollByTiles(0, -1);
                return false;

            case SDL.Keycode.Down:
                adventure.ScrollByTiles(0, 1);
                return false;

            case SDL.Keycode.Home:
                adventure.ScrollToStart();
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Handles the keys of the editor: the arrow keys scroll by one tile, Home returns to the top left
    /// corner, G toggles the grid, 1 to 9 pick a ground for the brush, Ctrl+Z and Ctrl+Y walk the undo
    /// history and Escape closes the editor. There is no menu to return to when the editor was started
    /// with <c>--scene editor</c>, so Escape then leaves the client.
    /// </summary>
    private bool HandleEditorKey(in SDL.KeyboardEvent key, EditorScene editor)
    {
        // The information box is modal, like the popup the reference shows on a right click: the keys
        // that step the data and the ones that close it are taken, and a key meant for the box cannot
        // paint or undo behind it.
        if (editor.IsObjectInfoOpen)
        {
            switch (key.Key)
            {
                case SDL.Keycode.I or SDL.Keycode.Escape when !key.Repeat:
                    editor.HideObjectInfo();
                    GameLog.Info("Editor info: closed.");
                    break;

                case SDL.Keycode.LeftBracket when !key.Repeat:
                case SDL.Keycode.RightBracket when !key.Repeat:
                    bool stepped = editor.AdjustObjectData(key.Key == SDL.Keycode.LeftBracket ? -1 : 1);

                    GameLog.Info(
                        $"Editor info: {(stepped ? "data changed" : "data unchanged")}, {editor.ObjectInfoSummary}.");

                    break;
            }

            return false;
        }

        if ((key.Mod & SDL.Keymod.Ctrl) != 0)
        {
            switch (key.Key)
            {
                case SDL.Keycode.Z when !key.Repeat:
                    GameLog.Info(editor.Undo() ? "Editor: undo." : "Editor: nothing to undo.");
                    return false;

                case SDL.Keycode.Y when !key.Repeat:
                    GameLog.Info(editor.Redo() ? "Editor: redo." : "Editor: nothing to redo.");
                    return false;

                case SDL.Keycode.S when !key.Repeat:
                    if (editor.Save(out string? saveError))
                    {
                        GameLog.Info($"Editor: saved '{editor.MapName}' to '{editor.Edits.SavePath}'.");
                    }
                    else
                    {
                        GameLog.Warn($"Editor: could not save '{editor.MapName}': {saveError}");
                    }

                    return false;

                default:
                    return false;
            }
        }

        switch (key.Key)
        {
            case SDL.Keycode.Escape:
                CloseEditor();
                return _menu is null;

            case SDL.Keycode.Alpha1 or SDL.Keycode.Alpha2 or SDL.Keycode.Alpha3
                or SDL.Keycode.Alpha4 or SDL.Keycode.Alpha5 or SDL.Keycode.Alpha6
                or SDL.Keycode.Alpha7 or SDL.Keycode.Alpha8 or SDL.Keycode.Alpha9 when !key.Repeat:
                // The grounds are numbered 1 (water) to 9 (beach), the same order as the enum.
                editor.SetBrushByNumber((int)key.Key - (int)SDL.Keycode.Alpha1 + 1);
                return false;

            case SDL.Keycode.G when !key.Repeat:
                editor.ToggleGrid();
                return false;

            case SDL.Keycode.T when !key.Repeat:
                editor.SetTool(EditorTool.Terrain);
                return false;

            case SDL.Keycode.O when !key.Repeat:
                GameLog.Info($"Editor object: {editor.CycleObject(1).GetSummary()}.");
                return false;

            case SDL.Keycode.X when !key.Repeat:
                editor.SetTool(EditorTool.Erase);
                return false;

            case SDL.Keycode.I when !key.Repeat:
                if (editor.IsObjectInfoOpen)
                {
                    editor.HideObjectInfo();
                    GameLog.Info("Editor info: closed.");
                }
                else if (editor.ToggleObjectInfo())
                {
                    GameLog.Info($"Editor info: {editor.ObjectInfoSummary}.");
                }
                else
                {
                    GameLog.Info("Editor info: the pointer is not over the map.");
                }

                return false;

            case SDL.Keycode.Left:
                editor.ScrollByTiles(-1, 0);
                return false;

            case SDL.Keycode.Right:
                editor.ScrollByTiles(1, 0);
                return false;

            case SDL.Keycode.Up:
                editor.ScrollByTiles(0, -1);
                return false;

            case SDL.Keycode.Down:
                editor.ScrollByTiles(0, 1);
                return false;

            case SDL.Keycode.Home:
                editor.ScrollToStart();
                return false;

            default:
                return false;
        }
    }

    /// <summary>Closes the editor and returns to the menu, or to the diagnostics screen without one.</summary>
    private void CloseEditor()
    {
        if (_editor is null)
        {
            return;
        }

        GameLog.Info($"Closing the editor of '{_editor.MapName}'.");
        _editor = null;
        _scene = _menu ?? (IScene?)_diagnostics;
    }

    /// <summary>
    /// Opens the editor: on a new empty map when <c>--editor-new</c> asked for one, otherwise on
    /// <paramref name="filePath"/>, or on the first map of the catalog when no file is given: an editor
    /// without a map would be an empty screen, and the first map is the one the scenario list shows
    /// first.
    /// </summary>
    private void OpenEditor(string? filePath)
    {
        EditorScene? scene;

        if (_options.EditorNewMapSize is { } size)
        {
            scene = EditorScene.CreateNew(_frame, _assets, _fonts, size, out string? error);
            if (scene is null)
            {
                GameLog.Warn($"A new {size}x{size} map cannot be created: {error}");
                return;
            }
        }
        else
        {
            scene = OpenEditorMap(filePath);
            if (scene is null)
            {
                return;
            }
        }

        // The synthetic pointer of a screenshot has to reach the editor as well, or the highlighted
        // tile and the hover text of the status bar would be missing from every baseline.
        if (_options.Cursor is { } cursor)
        {
            scene.SetCursor(cursor);
        }

        // --editor-paint is the scripted brush stroke: a screenshot cannot press a button, and the
        // point of the flag is to show that a painted tile reaches the frame.
        if (_options.EditorPaint is { } paint && scene.PaintArea(paint.Tile, paint.Width, paint.Height, paint.Ground))
        {
            GameLog.Info(
                $"Painted {paint.Width}x{paint.Height} tiles of '{paint.Ground}' at " +
                $"({paint.Tile.X}; {paint.Tile.Y}).");

            // The snapshot is taken from the pointer that the paint moved away from; put it back so
            // the outlined tile and the hover text keep describing the frame that is captured.
            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-object and --editor-place are the scripted object tool: the palette key O and a
        // click, which a screenshot cannot produce.
        if (_options.EditorObject is { } objectName)
        {
            if (scene.SelectObject(objectName))
            {
                GameLog.Info($"Editor object: {scene.Edits.ObjectBrush.GetSummary()}.");
            }
            else
            {
                GameLog.Warn(
                    $"The editor has no object called '{objectName}'; pass a name or 1..{Mp2ObjectPresets.Count}.");
            }
        }

        if (_options.EditorPlacements.Count > 0)
        {
            int placed = 0;

            foreach (Point placement in _options.EditorPlacements)
            {
                if (scene.PlaceObject(placement))
                {
                    ++placed;
                }
            }

            GameLog.Info($"Placed {placed} of {_options.EditorPlacements.Count} objects.");

            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-radar-click is the scripted radar click: the minimap only reacts to the pointer, so
        // a screenshot cannot show that a click on the panel moves the view.
        if (_options.EditorRadarClick is { } radar)
        {
            bool moved = scene.ClickRadar(radar.Start, radar.DragTo);

            GameLog.Info(
                $"Minimap {(radar.DragTo is null ? "click" : "drag")} at ({radar.Start.X}; {radar.Start.Y}) " +
                $"put the view at ({scene.View.Scroll.X}; {scene.View.Scroll.Y})" +
                $"{(moved ? "." : ", where it already was.")}");

            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-data writes the data word of one tile: the property of an object is a number in the
        // tile, and the information box below is the only way to see it, so the two belong together.
        if (_options.EditorData is { } data)
        {
            GameLog.Info(
                scene.SetObjectData(data.Tile, data.Value)
                    ? $"Wrote data {data.Value} into the object at ({data.Tile.X}; {data.Tile.Y})."
                    : $"Nothing at ({data.Tile.X}; {data.Tile.Y}) takes a data word.");

            if (_options.Cursor is { } pointer)
            {
                scene.SetCursor(pointer);
            }
        }

        // --editor-info opens the information box on one tile: the box is drawn over the map and the
        // panel, so a screenshot of it shows what the I key reports.
        if (_options.EditorInfoTile is { } infoTile)
        {
            GameLog.Info(
                scene.ShowObjectInfo(infoTile)
                    ? $"Editor info: {scene.ObjectInfoSummary}."
                    : $"({infoTile.X}; {infoTile.Y}) is outside the map.");
        }

        // --editor-panel-click clicks the controls of the editor panel in order: the panel is drawn from
        // the artwork of the archive, so a screenshot alone cannot prove that its buttons respond, and
        // selecting an instrument before its picker needs more than one click.
        if (_options.EditorPanelClicks.Count > 0)
        {
            Rect panel = EditorScene.PanelBayOf(scene.FrameSize);

            foreach (Point panelPoint in _options.EditorPanelClicks)
            {
                EditorPanelHit hit = scene.ClickPanel(new Point(panel.X + panelPoint.X, panel.Y + panelPoint.Y));

                GameLog.Info(
                    $"Panel click at ({panelPoint.X}; {panelPoint.Y}) hit {hit.Kind}. " +
                    $"Tool {scene.Tool}, ground {scene.Brush}, size {scene.BrushSizeName}, object '{scene.ObjectName}'.");
            }
        }

        // --editor-save closes the loop that --editor-paint opens: the map is written back at once, so
        // a script can open, change and save a real map without a mouse.
        if (_options.EditorSavePath is { } savePath)
        {
            if (scene.SaveAs(savePath, out string? saveError))
            {
                GameLog.Info($"Saved '{scene.MapName}' to '{savePath}'.");
            }
            else
            {
                GameLog.Warn($"Could not save '{scene.MapName}' to '{savePath}': {saveError}");
            }
        }

        _editor = scene;
    }

    /// <summary>
    /// Opens the editor on an existing map file; <see langword="null"/> when there is nothing usable to
    /// open, with the reason in the log.
    /// </summary>
    private EditorScene? OpenEditorMap(string? filePath)
    {
        string? path = filePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            string? directory = _assets?.DataDirectory;
            path = directory is null ? null : MapCatalog.ReadAll([directory]).FirstOrDefault()?.FilePath;
        }

        if (path is null)
        {
            GameLog.Warn("The editor needs a map: pass --map <file.mp2>, --editor-new <size>, or install the map collection.");
            return null;
        }

        Mp2FileInfo mapInfo;
        try
        {
            mapInfo = Mp2FileInfo.Read(path);
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException or UnauthorizedAccessException)
        {
            GameLog.Error($"The map file '{path}' could not be read", exception);
            return null;
        }

        EditorScene? scene = EditorScene.TryCreate(_frame, _assets, _fonts, mapInfo, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The map file '{path}' cannot be edited: {error}");
            return null;
        }

        return scene;
    }

    /// <summary>
    /// Starts the adventure map of a scenario the menu has just confirmed. The map or the terrain
    /// tiles may be unusable; the client then stays on the menu, which keeps its status bar, instead
    /// of showing a black screen.
    /// </summary>
    private void TryEnterAdventureMap()
    {
        if (_adventure is not null || _menu is null)
        {
            return;
        }

        if (_menu.TakeConfirmedScenario() is not { } map)
        {
            return;
        }

        AdventureMapScene? scene = AdventureMapScene.TryCreate(_frame, _assets, _fonts, map, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The scenario '{map.Name}' cannot be started: {error}");
            return;
        }

        _adventure = scene;
        _scene = scene;
    }

    /// <summary>
    /// Opens one map file on the adventure map, bypassing the menu and the scenario list
    /// (<c>--map</c>). A file that cannot be read leaves the client where it was, which is what makes
    /// the flag safe to use in scripts.
    /// </summary>
    private void OpenMapFile(string filePath)
    {
        Mp2FileInfo mapInfo;
        try
        {
            mapInfo = Mp2FileInfo.Read(filePath);
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException or UnauthorizedAccessException)
        {
            GameLog.Error($"The map file '{filePath}' could not be read", exception);
            return;
        }

        AdventureMapScene? scene = AdventureMapScene.TryCreate(_frame, _assets, _fonts, mapInfo, out string? error);
        if (scene is null)
        {
            GameLog.Warn($"The map file '{filePath}' cannot be shown: {error}");
            return;
        }

        _adventure = scene;
        _scene = scene;
    }

    /// <summary>Leaves the adventure map and goes back to the main menu.</summary>
    private void ReturnToMenu()
    {
        if (_adventure is null)
        {
            return;
        }

        GameLog.Info($"Leaving the adventure map '{_adventure.MapName}'.");
        _adventure = null;
        _scene = _menu is not null ? _menu : _diagnostics;
    }

    /// <summary>Maps a window coordinate onto the 640x480 logical frame, or <see langword="null"/> outside it.</summary>
    private Point? ToLogical(float x, float y)
    {
        if (_presenter is null)
        {
            return null;
        }

        Point output = new((int)MathF.Round(x), (int)MathF.Round(y));

        return _presenter.Viewport.TryToLogical(output, out Point logical) ? logical : null;
    }

    private void Update(double elapsedSeconds)
    {
        _timer.Tick(elapsedSeconds);
        _scene?.Update(elapsedSeconds);

        if (_options.SmokeFrames is int maxFrames && _timer.FrameCount >= maxFrames)
        {
            GameLog.Info($"Smoke mode finished after {_timer.FrameCount} frames ({_timer.FramesPerSecond:0.0} FPS).");
            _running = false;
        }
    }

    private void Draw()
    {
        if (_presenter is null || _scene is null)
        {
            return;
        }

        DiagnosticsState state = new(
            _assets?.DataDirectory,
            _assets?.BaseArchive.Count ?? 0,
            _assets?.ExpansionArchive?.Count ?? 0,
            _text?.Name ?? "none",
            _fonts?.Description ?? "none",
            _presenter.OutputSize,
            _presenter.Viewport.Scale,
            _timer.FramesPerSecond,
            _timer.FrameCount);

        // One line per scene change: it tells whether a switch reached the renderer, which is the
        // difference between "the click did nothing" and "the new scene drew nothing" (ADR-042).
        if (_drawnScene != _scene)
        {
            _drawnScene = _scene;
            GameLog.Info($"Drawing the '{_scene.GetType().Name}'.");
        }

        _scene.DrawFrame(state);
        _presenter.Upload(_frame);

        _presenter.BeginLogicalFrame();
        _presenter.DrawFrameTexture();

        _text?.BeginFrame();
        _scene.DrawOverlay(state);

        CaptureScreenshot();

        _presenter.EndFrame();
    }

    /// <summary>
    /// Creates the screens of this milestone and picks the start screen. The main menu is the
    /// default; the diagnostics screen stays available for engine checks and as the fallback when
    /// the artwork or the fonts are missing.
    /// </summary>
    private void CreateScenes()
    {
        _diagnostics = new DiagnosticsScene(_frame, _text, _supportsCjk, _fonts);

        // The editor is a screen of its own, not a dialog of the menu: it starts without the menu so
        // that a screenshot of it is the editor and nothing else.
        if (_options.Scene == SceneKind.Editor)
        {
            OpenEditor(_options.MapFile);
            _scene = _editor ?? (IScene?)_diagnostics;

            GameLog.Info(_editor is null
                ? "Starting on the diagnostics screen (the editor could not open a map)."
                : $"Starting on the map editor ('{_editor.MapName}').");

            return;
        }

        if (_options.Scene == SceneKind.Menu)
        {
            _menu = MainMenuScene.TryCreate(_frame, _assets, _fonts, _options.Language);

            if (_menu is not null)
            {
                _menu.AnimationEnabled = !_options.FreezeAnimation;

                if (_options.OpenSettings)
                {
                    _menu.OpenSettings();
                }

                if (_options.OpenScenario || _options.ScenarioSelection is not null)
                {
                    if (_menu.OpenScenario() && _options.ScenarioSelection is { } selection)
                    {
                        _menu.Scenario?.Select(selection);
                    }
                }

                if (_options.Cursor is { } cursor)
                {
                    _menu.SetCursor(cursor);

                    // A screenshot of a tooltip needs the right button held: the message box of the
                    // original exists only while it is pressed.
                    if (_options.RightButton)
                    {
                        _menu.SetRightButton(true);
                    }
                }
            }
        }

        _scene = _menu is not null ? _menu : _diagnostics;

        if (_options.EnterMap && _menu is not null)
        {
            if (_menu.Scenario is null)
            {
                _menu.OpenScenario();
            }

            // Confirm what the list shows: the first map, or the one --scenario-select picked.
            _menu.ConfirmScenario();
            TryEnterAdventureMap();
        }

        if (!string.IsNullOrWhiteSpace(_options.MapFile))
        {
            OpenMapFile(_options.MapFile);
        }

        GameLog.Info(_menu is null
            ? "Starting on the diagnostics screen."
            : $"Starting on the main menu (language '{_menu.Text.Culture}', " +
              $"{_menu.Languages.Count} available).");
    }

    private void CaptureScreenshot()
    {
        bool requestedByCommandLine = _screenshotFrame >= 0 && _timer.FrameCount >= _screenshotFrame;
        bool requestedByKey = _screenshotRequested;
        if (!requestedByCommandLine && !requestedByKey)
        {
            return;
        }

        _screenshotFrame = -1;
        _screenshotRequested = false;

        string path = requestedByKey
            ? $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png"
            : _options.ScreenshotPath!;

        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _presenter!.SaveLogicalFrame(path);
            GameLog.Info($"Screenshot saved to '{Path.GetFullPath(path)}'.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            GameLog.Error("Screenshot could not be written", exception);
            ExitCode = 1;
        }

        // --screenshot is a script, not a session: the shell that asked for the file has to get its
        // prompt back, so the client stops once the frame is written (or the write has failed). F12
        // keeps the window open, because a player takes one screenshot and carries on playing. The
        // flag decides, not the key, so that an F12 that beats the capture frame cannot leave a
        // scripted run waiting for a window to be closed by hand.
        if (_options.ScreenshotPath is not null)
        {
            GameLog.Info("Screenshot was requested on the command line, closing the client.");
            _running = false;
        }
    }

    private void LoadGameData()
    {
        try
        {
            _assets = AssetBundle.Open(_options.DataDirectory);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException or IOException or BinaryFormatException)
        {
            GameLog.Error("The original game data could not be loaded", exception);
            GameLog.Warn("Continuing with the fallback grayscale palette; pass --data <dir> to fix this.");
            ExitCode = 1;
            return;
        }

        byte[]? paletteData = _assets.TryRead("KB.PAL");
        if (paletteData is null || paletteData.Length < Palette.RawPaletteSize)
        {
            GameLog.Warn("KB.PAL is missing or truncated; using the fallback grayscale palette.");
            return;
        }

        _palette = Palette.FromGamePalette(paletteData);
        GameLog.Info($"Palette loaded from KB.PAL ({paletteData.Length} bytes, 6 bit VGA normalized).");
    }

    /// <summary>
    /// Opens the audio device and starts the main menu theme. Everything is optional: a machine
    /// without audio, or a player without the separate soundtrack files, only logs a message.
    /// </summary>
    private void StartAudio()
    {
        if (_options.NoAudio)
        {
            GameLog.Info("Audio disabled by --no-audio.");
            return;
        }

        _audio = AudioPlayer.TryCreate(_assets);
        if (_audio is null)
        {
            GameLog.Warn("Continuing without music and sound effects.");
            return;
        }

        // The main menu theme is the only music track of M2; scenes from M3 on switch it.
        if (!_audio.PlayMusic(MusicTrack.MainMenu))
        {
            GameLog.Info("The main menu theme is not available; the menu stays silent.");
        }

        if (_options.PlaySound is string soundName && !_audio.PlaySound(soundName))
        {
            GameLog.Warn($"The sound effect '{soundName}' could not be played.");
        }
    }

    private void CreateTextRenderer()
    {
        if (_presenter is null)
        {
            return;
        }

        foreach (FontChoice candidate in SystemFontLocator.EnumerateCandidates())
        {
            try
            {
                _text = new SdlTextRenderer(_presenter.Renderer, candidate.Path);
                _supportsCjk = candidate.SupportsCjk;
                GameLog.Info($"Text backend ready: {_text.Name} (CJK: {(_supportsCjk ? "yes" : "no")}).");
                return;
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException)
            {
                GameLog.Warn($"Font '{candidate.Path}' could not be used: {exception.Message}");
            }
        }

        GameLog.Warn("No usable TrueType font found; the diagnostics overlay stays disabled.");
    }
}
