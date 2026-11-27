using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The editor's file menu on the host's frame pump: the seven generated plates are built from the installed fonts,
/// the menu reports the button it was left with, and a right click stacks its explanation above it.
/// </summary>
/// <remarks>
/// 用例自己当帧泵，事件塞进 <see cref="LocalEvent.Current"/>，同 <c>EditorOptionsModalTests</c>。这套窗口比选项页
/// 多一道坎：七块牌子要靠字体现压（<c>game_assets.cpp:1579-1593</c>），所以字体没装时窗口根本不该开。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorFileMenuModalTests : IDisposable
{
    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts once for the whole test.</summary>
    public EditorFileMenuModalTests()
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
    public void TheSevenButtonsAreBuiltFromTheInstalledFonts()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        int buttons = 0;
        bool insideWindow = false;

        host.RunModal = modal =>
        {
            var menu = (EditorFileModal)modal;

            buttons = menu.Dialog.Buttons.Count;

            // 取消键要落在窗口里：参考把它挂在底边中上方 11 像素（editor_interface.cpp:2090）。
            insideWindow = menu.Dialog.Window.ActiveArea.Contains(menu.Dialog.Cancel.Area);

            Feed(modal, Key.Escape);
        };

        Assert.Equal(EditorFileResult.Cancel, host.RunFileDialog());
        Assert.Equal(EditorFileDialog.ButtonCount, buttons);
        Assert.True(insideWindow);
    }

    [Fact]
    public void ClickingABigButtonReportsIt() =>
        Assert.True(Try(host =>
        {
            host.RunModal = modal => Click(modal, ((EditorFileModal)modal).Dialog.SaveMapRoi);

            return host.RunFileDialog() == EditorFileResult.SaveMap;
        }));

    [Fact]
    public void ClickingQuitReportsIt() =>
        Assert.True(Try(host =>
        {
            host.RunModal = modal => Click(modal, ((EditorFileModal)modal).Dialog.QuitRoi);

            return host.RunFileDialog() == EditorFileResult.Quit;
        }));

    [Fact]
    public void ClickingTheCancelButtonReportsIt() =>
        Assert.True(Try(host =>
        {
            host.RunModal = modal => Click(modal, ((EditorFileModal)modal).Dialog.Cancel.Area);

            return host.RunFileDialog() == EditorFileResult.Cancel;
        }));

    [Fact]
    public void ARightClickOpensTheExplanationAboveTheMenu() =>
        Assert.True(Try(host =>
        {
            bool menuSurvived = false;

            host.RunModal = modal =>
            {
                var menu = (EditorFileModal)modal;

                RightClick(menu, menu.Dialog.SaveMapRoi);

                // 说明框摞在上面：菜单这一帧既不结束也没有结论。
                menuSurvived = !menu.IsFinished && menu.Result == EditorFileResult.None;

                menu.HotKey(ButtonHotKey.Okay);
                Pump(menu);

                menuSurvived &= !menu.IsFinished;

                Feed(menu, Key.Escape);
            };

            bool reported = host.RunFileDialog() == EditorFileResult.Cancel;

            return menuSurvived && reported;
        }));

    [Fact]
    public void WithoutFontsTheMenuIsSkipped()
    {
        if (_assets is null)
        {
            return;
        }

        var host = new EditorSceneHost(_assets, fonts: null);
        int windows = 0;

        host.RunModal = _ => ++windows;

        Assert.Equal(EditorFileResult.Cancel, host.RunFileDialog());
        Assert.Equal(0, windows);
    }

    /// <summary>Runs <paramref name="body"/> against a host, skipping the test when the data is missing.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Whatever <paramref name="body"/> returned, <see langword="true"/> when the data is missing.</returns>
    private bool Try(Func<EditorSceneHost, bool> body) =>
        TryCreateHost(out EditorSceneHost host) is false || body(host);

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

    /// <summary>Runs one modal frame without input.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on an area of the menu, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one right press on an area of the menu, the <c>showStandardTextMessage</c> of the reference.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to right click; its centre is the point.</param>
    private void RightClick(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Right, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Builds a host over the installed data; <see langword="false"/> when the machine has none.</summary>
    /// <param name="host">The host, when one could be made.</param>
    /// <returns>Whether the host could be made.</returns>
    private bool TryCreateHost(out EditorSceneHost host)
    {
        host = new EditorSceneHost(_assets, _fonts);

        return _assets is not null && _fonts is not null;
    }
}
