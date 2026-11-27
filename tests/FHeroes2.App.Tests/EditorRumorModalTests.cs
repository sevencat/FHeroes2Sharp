using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The rumors window on the host's frame pump: it stacks the input box it asks for, keeps what was typed, refuses a
/// duplicate with a message and writes nothing when it is cancelled, <c>openRumorWindow()</c> of the reference.
/// </summary>
/// <remarks>
/// 用例自己当帧泵，事件塞进 <see cref="LocalEvent.Current"/>，同 <c>EditorFileMenuModalTests</c>。输入框里的字照参考走
/// 键盘：<see cref="Key.A"/>..<see cref="Key.Z"/> 落到小写，<see cref="ButtonHotKey.Okay"/> 是回车、取消是 ESC——
/// 这正是参考 <c>Dialog::inputString</c> 的返回 <c>true</c> / <c>false</c>。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorRumorModalTests : IDisposable
{
    private const int MapSize = 36;

    /// <summary>Entry of the specifications button in the system table, three columns per row.</summary>
    private const int SpecsButtonIndex = 3;

    /// <summary>Width of one button of the bay, the 48x36 frames of <c>EDITBTNS.ICN</c>.</summary>
    private const int ButtonWidth = 48;

    /// <summary>Height of one button of the bay.</summary>
    private const int ButtonHeight = 36;

    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts once for the whole test.</summary>
    public EditorRumorModalTests()
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
    public void ARumorTypedIntoTheInputBoxIsKeptWhenTheWindowIsAccepted() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorRumorModal(window, Host());

            Click(modal, window.AddButton.Area);

            // 输入框摞在列表上，列表这一帧什么都不做。
            Assert.True(modal.HasInnerWindow, "the input box did not open");
            Assert.False(modal.IsFinished);
            Assert.Single(window.Rumors);

            modal.PressKey(Key.Z, KeyModifier.None);
            AcceptInput(modal);
            Pump(modal);

            Assert.False(modal.HasInnerWindow, "the input box did not close");

            Click(modal, window.Okay.Area);

            Assert.True(modal.IsFinished);
            Assert.Equal(EditorListOutcome.Accepted, modal.Outcome);

            IReadOnlyList<string>? rumors = modal.AcceptedRumors;

            Assert.NotNull(rumors);
            Assert.Equal(2, rumors.Count);
            Assert.Equal("z", rumors[1]);

            return true;
        }));

    [Fact]
    public void CancellingTheWindowWritesNothing() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorRumorModal(window, Host());

            Click(modal, window.Cancel.Area);

            return modal.IsFinished && modal.Outcome == EditorListOutcome.Cancelled &&
                modal.AcceptedRumors is null;
        }));

    [Fact]
    public void CancellingTheInputBoxLeavesTheListAlone() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorRumorModal(window, Host());

            Click(modal, window.AddButton.Area);

            modal.PressKey(Key.Z, KeyModifier.None);
            modal.HotKey(ButtonHotKey.Cancel);
            Pump(modal);

            return !modal.HasInnerWindow && window.Rumors.Count == 1 && window.Rumors[0] == "old";
        }));

    [Fact]
    public void ADuplicateRumorIsRefusedWithTheMessageOfTheReference() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorRumorModal(window, Host());

            Click(modal, window.AddButton.Area);

            // 「old」逐字打进去：重复的条目照参考 :236-239 骂一句，列表不长。
            modal.PressKey(Key.O, KeyModifier.None);
            modal.PressKey(Key.L, KeyModifier.None);
            modal.PressKey(Key.D, KeyModifier.None);
            AcceptInput(modal);
            Pump(modal);

            bool refused = window.Rumors.Count == 1 && !modal.IsFinished;

            // 提示框摞着：这一下 OKAY 落在提示框上，列表关不掉，得先把提示框按掉。
            PressClick(modal, new Point(window.Okay.Area.X + 5, window.Okay.Area.Y + 5));

            refused &= !modal.IsFinished && modal.HasInnerWindow;

            modal.HotKey(ButtonHotKey.Okay);
            Pump(modal);

            return refused && !modal.HasInnerWindow;
        }));

    [Fact]
    public void EditingTheSelectedRumorReplacesItsText() =>
        Assert.True(Try(window =>
        {
            var modal = new EditorRumorModal(window, Host());

            Click(modal, RowCenter(window, 0));
            Click(modal, window.EditButton.Area);

            // 输入框带出选中的那条（参考 :255），退三格再打一个字母就把整条换掉。
            bool seeded = modal.HasInnerWindow;

            for (int i = 0; i < 3; ++i)
            {
                modal.PressKey(Key.Backspace, KeyModifier.None);
            }

            modal.PressKey(Key.Q, KeyModifier.None);
            AcceptInput(modal);
            Pump(modal);

            seeded &= !modal.HasInnerWindow;

            Click(modal, window.Okay.Area);

            return seeded && modal.AcceptedRumors is { Count: 1 } rumors && rumors[0] == "q";
        }));

    [Fact]
    public void TheSpecificationsPageOpensTheRumorsWindow()
    {
        if (!TryOpenEditor(out EditorScene scene, out _))
        {
            return;
        }

        bool opened = false;

        scene.RunModal = modal =>
        {
            var page = (EditorMapSpecsModal)modal;

            Click(page, page.Window.Rumors.Area);

            opened = page.TopWindow is EditorRumorModal;

            // 传闻窗先关，规格页再关：参考的两层阻塞循环（editor_map_specs_window.cpp:2282-2288）。
            Feed(page, Key.Escape);
            Pump(page);

            Feed(page, Key.Escape);
        };

        ClickSpecsButton(scene);

        Assert.True(opened);

        // 全都取消，地图还是「没动过」的样子。
        Assert.False(scene.Edits.IsDirty);
    }

    /// <summary>Runs <paramref name="body"/> against a window over the "old" rumor, skipping without game data.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Whatever <paramref name="body"/> returned, <see langword="true"/> when the data is missing.</returns>
    private bool Try(Func<EditorRumorWindow, bool> body)
    {
        if (Host() is not { } host || host.TryCreateListArtwork() is not { } artwork ||
            !host.TryGetFonts(out GameFonts? fonts))
        {
            return true;
        }

        EditorRumorWindow window = EditorRumorWindow.Create(ScreenBuffer.DefaultSize, ["old"], artwork,
            fonts.Yellow, fonts.White);

        return body(window);
    }

    /// <summary>Builds the host over the installed data.</summary>
    /// <returns>The host; its members report the missing data themselves.</returns>
    private EditorSceneHost Host() => new(_assets, _fonts);

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
    /// Clicks OKAY of the input box on top; a multi line field has no Enter to answer, the reference's OKAY button.
    /// </summary>
    /// <param name="modal">The rumors window; the click goes to the box stacked above it.</param>
    private void AcceptInput(EditorRumorModal modal)
    {
        var input = (InputStringModal)modal.TopWindow!;
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

    /// <summary>Centre of one row of the rumor list.</summary>
    /// <param name="window">The window.</param>
    /// <param name="row">Index of the row.</param>
    /// <returns>The point inside the row.</returns>
    private static Point RowCenter(EditorRumorWindow window, int row)
    {
        int height = window.ListArea.Height - EditorRumorWindow.ListAreaHeightDeduction;
        int rowHeight = Math.Max(1, height / EditorRumorWindow.MaxVisibleItemCount);

        return new Point(window.ListArea.X + 40, window.ListArea.Y + (row * rowHeight) + (rowHeight / 2));
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
