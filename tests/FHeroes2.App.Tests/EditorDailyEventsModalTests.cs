using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The daily events window on the host's frame pump: an add or an edit stacks the event window, the event window stacks
/// its message box, and only two accepted answers in a row write anything, <c>openDailyEventsWindow()</c>.
/// </summary>
/// <remarks>
/// 用例自己当帧泵，事件塞进 <see cref="LocalEvent.Current"/>，同 <c>EditorRumorModalTests</c>。两层 UI 窗（列表与事件窗）
/// 都从 <see cref="LocalEvent"/> 读点击；输入框不吃它，只有宿主的 <c>SetCursor</c>/<c>SetLeftButton</c> 能点。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorDailyEventsModalTests : IDisposable
{
    /// <summary>Size of the editor map the end-to-end test builds.</summary>
    private const int MapSize = 36;

    /// <summary>Entry of the specifications button in the system table, three columns per row.</summary>
    private const int SpecsButtonIndex = 3;

    /// <summary>Width of one button of the bay, the 48x36 frames of <c>EDITBTNS.ICN</c>.</summary>
    private const int ButtonWidth = 48;

    /// <summary>Height of one button of the bay.</summary>
    private const int ButtonHeight = 36;

    /// <summary>Every colour the map format knows, what a test map allows both sides to play.</summary>
    private const PlayerColor EveryColor = PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red |
        PlayerColor.Yellow | PlayerColor.Orange | PlayerColor.Purple;

    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts once for the whole test.</summary>
    public EditorDailyEventsModalTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;

        _assets?.Dispose();
    }

    [Fact]
    public void AnAddedEventIsKeptWhenTheWindowIsAccepted() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorDailyEventsModal(window, Host(), EveryColor, EveryColor);

            Click(modal, window.AddButton.Area);

            Assert.True(modal.HasInnerWindow, "the event window did not open");
            Assert.Single(window.Events);

            // 新事件原样收下：参考 :254-261 的 emplace_back。
            Click(modal, ((EditorDailyEventSpecModal)modal.TopWindow!).Window.Okay.Area);
            Pump(modal);

            Assert.False(modal.HasInnerWindow, "the event window did not close");
            Assert.Equal(2, window.Events.Count);

            Click(modal, window.Okay.Area);

            Assert.True(modal.IsFinished);
            Assert.Equal(EditorListOutcome.Accepted, modal.Outcome);
            Assert.NotNull(modal.AcceptedEvents);
            Assert.Equal(2, modal.AcceptedEvents.Count);

            return true;
        }));

    [Fact]
    public void CancellingTheEventWindowAddsNothing() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorDailyEventsModal(window, Host(), EveryColor, EveryColor);

            Click(modal, window.AddButton.Area);
            Click(modal, ((EditorDailyEventSpecModal)modal.TopWindow!).Window.Cancel.Area);
            Pump(modal);

            return !modal.HasInnerWindow && window.Events.Count == 1 && !modal.IsFinished;
        }));

    [Fact]
    public void CancellingTheWindowWritesNothing() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorDailyEventsModal(window, Host(), EveryColor, EveryColor);

            Click(modal, window.Cancel.Area);

            return modal.IsFinished && modal.Outcome == EditorListOutcome.Cancelled &&
                modal.AcceptedEvents is null;
        }));

    [Fact]
    public void TheMessageTypedIntoTheInputBoxLandsOnTheEvent() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorDailyEventsModal(window, Host(), EveryColor, EveryColor);

            Click(modal, window.AddButton.Area);

            var spec = (EditorDailyEventSpecModal)modal.TopWindow!;

            // 点消息区开输入框（参考 :332-344），字从键盘进去，OKAY 用光标点。
            Click(modal, spec.Window.MessageArea);

            Assert.True(spec.HasInnerWindow, "the input box did not open");

            modal.PressKey(Key.Z, KeyModifier.None);
            AcceptInput(modal);
            Pump(modal);

            Assert.False(spec.HasInnerWindow, "the input box did not close");
            Assert.Equal("z", spec.Window.Event.Message);

            Click(modal, spec.Window.Okay.Area);
            Pump(modal);

            return window.Events.Count == 2 && window.Events[1].Message == "z";
        }));

    [Fact]
    public void EditingTheSelectedEventReplacesIt() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorDailyEventsModal(window, Host(), EveryColor, EveryColor);

            Click(modal, RowCenter(window, 0));
            Click(modal, window.EditButton.Area);

            var spec = (EditorDailyEventSpecModal)modal.TopWindow!;

            Assert.Equal("old", spec.Window.Event.Message);

            // 退三格再打一个字母，整条换掉（参考 :273-278 的 GetCurrent() = std::move( temp )）。
            Click(modal, spec.Window.MessageArea);

            for (int i = 0; i < 3; ++i)
            {
                modal.PressKey(Key.Backspace, KeyModifier.None);
            }

            modal.PressKey(Key.Q, KeyModifier.None);
            AcceptInput(modal);
            Pump(modal);

            Click(modal, spec.Window.Okay.Area);
            Pump(modal);

            return window.Events.Count == 1 && window.Events[0].Message == "q";
        }));

    [Fact]
    public void TheSpecificationsPageOpensTheDailyEventsWindow()
    {
        if (!TryOpenEditor(out EditorScene scene, out _))
        {
            return;
        }

        bool opened = false;

        scene.RunModal = modal =>
        {
            var page = (EditorMapSpecsModal)modal;

            Click(page, page.Window.Events.Area);

            opened = page.TopWindow is EditorDailyEventsModal;

            // 事件窗先关，规格页再关：参考的两层阻塞循环（editor_map_specs_window.cpp:2280-2300）。
            Feed(page, Key.Escape);
            Pump(page);

            Feed(page, Key.Escape);
        };

        ClickSpecsButton(scene);

        Assert.True(opened);

        // 全都取消，地图还是「没动过」的样子。
        Assert.False(scene.Edits.IsDirty);
    }

    /// <summary>Runs <paramref name="body"/> against a list over one "old" event, skipping without game data.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Whatever <paramref name="body"/> returned, <see langword="true"/> when the data is missing.</returns>
    private bool Try(Func<EditorDailyEventsWindow, bool> body)
    {
        if (Host() is not { } host || host.TryCreateListArtwork() is not { } artwork ||
            !host.TryGetFonts(out GameFonts? fonts))
        {
            return true;
        }

        EditorDailyEventsWindow window = EditorDailyEventsWindow.Create(ScreenBuffer.DefaultSize,
            [new MapDailyEvent { Message = "old" }], artwork, fonts.Yellow, fonts.White);

        return body(window);
    }

    /// <summary>Builds the host over the installed data.</summary>
    /// <returns>The host; its members report the missing data themselves.</returns>
    private EditorSceneHost Host() => new(_assets, _fonts);

    /// <summary>
    /// Clicks OKAY of the input box on top; a multi line field has no Enter to answer, the reference's OKAY button.
    /// </summary>
    /// <param name="modal">The events window; the click goes to the box stacked above it.</param>
    private void AcceptInput(EditorDailyEventsModal modal)
    {
        var spec = (EditorDailyEventSpecModal)modal.TopWindow!;
        var input = (InputStringModal)spec.TopWindow!;
        Rect area = input.Window.OkayArea;

        PressClick(modal, new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2)));
    }

    /// <summary>
    /// Clicks through the cursor and the button, the path the host drives an inner window with; the innermost window
    /// reads <see cref="LocalEvent"/> only when it is the outermost one.
    /// </summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="point">The point to click.</param>
    private static void PressClick(IModalScreen modal, Point point)
    {
        modal.SetCursor(point);
        modal.SetLeftButton(true);
        modal.SetLeftButton(false);
    }

    /// <summary>Runs one modal frame without input.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on an area of the window, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(IModalScreen modal, Rect area) =>
        Click(modal, new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2)));

    /// <summary>Feeds one left click on a point, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="point">The point to click.</param>
    private void Click(IModalScreen modal, Point point)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Centre of one row of the event list.</summary>
    /// <param name="window">The window.</param>
    /// <param name="row">Index of the row.</param>
    /// <returns>The point inside the row.</returns>
    private static Point RowCenter(EditorDailyEventsWindow window, int row)
    {
        int height = window.ListArea.Height - EditorDailyEventsWindow.ListAreaHeightDeduction;
        int rowHeight = Math.Max(1, height / EditorDailyEventsWindow.MaxVisibleItemCount);

        return new Point(window.ListArea.X + 40, window.ListArea.Y + (row * rowHeight) + (rowHeight / 2));
    }

    /// <summary>Runs one modal frame with a key pressed.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="key">The key the frame carries.</param>
    private void Feed(IModalScreen modal, Key key)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        modal.Update(_frame, 0.016);
    }

    /// <summary>
    /// Opens the editor with the fonts the pages need; <see langword="false"/> when the machine has no game data.
    /// </summary>
    /// <param name="scene">The scene, when it could be made.</param>
    /// <param name="frame">The frame the scene draws into.</param>
    /// <returns>Whether the scene could be made.</returns>
    private bool TryOpenEditor(out EditorScene scene, out ScreenBuffer frame)
    {
        scene = null!;
        frame = new ScreenBuffer(new Size(640, 480), new Palette());

        if (_assets is null || GameFonts.TryLoad(_assets) is not { } fonts)
        {
            return false;
        }

        GameAssets.Current = _assets;

        EditorScene? created = EditorScene.CreateNew(frame, _assets, fonts, MapSize, out _);

        if (created is null)
        {
            return false;
        }

        scene = created;

        return true;
    }

    /// <summary>Clicks the specifications cell of the panel, the left button path of the scene.</summary>
    /// <param name="scene">The editor scene.</param>
    private static void ClickSpecsButton(EditorScene scene)
    {
        scene.SetCursor(SpecsButtonCentre(scene));
        scene.SetLeftButton(true);
        scene.SetLeftButton(false);
    }

    /// <summary>Centre of the specifications button: the two rows sit at the bottom of the panel bay.</summary>
    /// <param name="scene">The editor scene.</param>
    /// <returns>The point inside the button.</returns>
    private static Point SpecsButtonCentre(EditorScene scene)
    {
        Rect panel = scene.PanelArea;

        return new Point(
            panel.X + ((SpecsButtonIndex % 3) * ButtonWidth) + (ButtonWidth / 2),
            panel.Y + panel.Height - (2 * ButtonHeight) + ((SpecsButtonIndex / 3) * ButtonHeight) +
            (ButtonHeight / 2));
    }
}
