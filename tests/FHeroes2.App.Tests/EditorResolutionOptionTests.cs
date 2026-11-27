using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The Resolution row of the graphics page, <c>dialog_graphics_settings.cpp:255-269</c> calling
/// <c>Dialog::SelectResolution()</c>: the chooser built out of <c>SCROLL</c> and <c>ADVBORD</c>, applied through
/// <see cref="IDisplayControl"/>.
/// </summary>
[Collection(LocalEventCollection.Name)]
public sealed class EditorResolutionOptionTests : IDisposable
{
    private const int MapSize = 36;

    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly IEventEngine? _previousEngine;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());
    private readonly FakeDisplay _display = new();

    /// <summary>Opens the archive, the fonts and a display to offer resolutions on.</summary>
    public EditorResolutionOptionTests()
    {
        _previousAssets = GameAssets.Current;
        _previousEngine = LocalEvent.Current.Engine;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
        LocalEvent.Current.Engine = new FakeEngine(_display);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        LocalEvent.Current.Engine = _previousEngine;
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void WithoutTheGameDataThereIsNoChooser()
    {
        var host = new EditorSceneHost(assets: null, fonts: null);

        Assert.Null(host.TryCreateResolutionDialog());
        Assert.False(host.RunResolutionPicker());
    }

    [Fact]
    public void WithoutADisplayThereIsNoChooser()
    {
        LocalEvent.Current.Engine = null;

        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        Assert.Null(host.TryCreateResolutionDialog());
        Assert.False(host.RunResolutionPicker());
    }

    [Fact]
    public void TheChooserOffersOnlyTheWindowsOfTheFrameBuffer()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        ResolutionDialog? page = host.TryCreateResolutionDialog();

        Assert.NotNull(page);
        Assert.Equal(2, page.Resolutions.Count);
        Assert.All(page.Resolutions, row => Assert.Equal(ScreenBuffer.DefaultSize, row.GameSize));
    }

    [Fact]
    public void ThePeakOfTheSeamIsWiredByDefault()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        Assert.NotNull(host.SelectResolution);
    }

    [Fact]
    public void OkaySwitchesTheWindow()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateResolutionDialog() is not { } page)
        {
            return;
        }

        host.RunModal = modal =>
        {
            var picker = (ResolutionPickerModal)modal;

            Click(picker, picker.Dialog.Buttons[0].Area);

            Assert.True(picker.IsFinished);
            Assert.True(picker.Accepted);
        };

        Assert.True(host.RunResolutionPicker());
        Assert.NotNull(_display.Applied);
    }

    [Fact]
    public void EscapeKeepsTheWindow()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateResolutionDialog() is not { } page)
        {
            return;
        }

        host.RunModal = modal =>
        {
            var picker = (ResolutionPickerModal)modal;

            PressKey(picker, Key.Escape);

            Assert.True(picker.IsFinished);
            Assert.False(picker.Accepted);
        };

        Assert.False(host.RunResolutionPicker());
        Assert.Null(_display.Applied);
    }

    [Fact]
    public void TheResolutionLeafSwitchesTheWindowAndRebuilds()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateGraphicsSettingsDialog() is not { } page)
        {
            return;
        }

        host.RunModal = modal =>
        {
            var picker = (ResolutionPickerModal)modal;

            Click(picker, picker.Dialog.Buttons[0].Area);
        };

        bool rebuilt = false;

        Assert.True(GraphicsSettingsDialog.Apply(page, GraphicsSettingsOption.Resolution,
            () => rebuilt = true, host.SelectResolution));
        Assert.True(rebuilt);
        Assert.NotNull(_display.Applied);
    }

    [Fact]
    public void OpenGraphicsSettings_SwitchesTheWindowThroughTheScene()
    {
        if (!TryCreateEditor(out EditorScene scene))
        {
            return;
        }

        bool picked = false;

        scene.RunModal = modal =>
        {
            switch (modal)
            {
                case GraphicsSettingsModal graphics when !picked:
                    Click(graphics, graphics.Dialog.ResolutionRoi);
                    break;

                case GraphicsSettingsModal graphics:
                    // 分辨率换完之后回到同一页，第二轮的 OK 才是 Exit。
                    Click(graphics, graphics.Dialog.Buttons[0].Area);
                    break;

                case ResolutionPickerModal picker:
                    Click(picker, picker.Dialog.Buttons[0].Area);
                    picked = true;
                    break;

                default:
                    Assert.Fail($"Unexpected window {modal.GetType().Name}.");
                    break;
            }
        };

        Assert.True(scene.OpenGraphicsSettings());
        Assert.NotNull(_display.Applied);
    }

    /// <summary>Builds a host over the installed data; <see langword="false"/> when the machine has none.</summary>
    private bool TryCreateHost(out EditorSceneHost host)
    {
        host = new EditorSceneHost(_assets, _fonts);

        return _assets is not null && _fonts is not null;
    }

    /// <summary>Builds a scene on a blank map.</summary>
    private bool TryCreateEditor(out EditorScene scene)
    {
        scene = null!;

        if (_assets is null || _fonts is null)
        {
            return false;
        }

        EditorScene? created = EditorScene.CreateFromMap(
            _frame, _assets, _fonts, Mp2Map.CreateBlank(MapSize, 0), out _);

        if (created is null)
        {
            return false;
        }

        scene = created;

        return true;
    }

    /// <summary>Feeds one left click on an area of a window, the press and the release in the same frame.</summary>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one key press and release to a window.</summary>
    private void PressKey(IModalScreen modal, Key key)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        modal.Update(_frame, 0.016);
    }

    /// <summary>A window with resolutions to offer and a record of what was applied.</summary>
    private sealed class FakeDisplay : IDisplayControl
    {
        /// <summary>The resolution handed to <see cref="SetResolution"/>, if any.</summary>
        public ResolutionInfo? Applied { get; private set; }

        /// <inheritdoc />
        public bool IsFullScreen => false;

        /// <inheritdoc />
        public Size ScreenSize => new(800, 600);

        /// <inheritdoc />
        public IReadOnlyList<ResolutionInfo> GetAvailableResolutions() =>
        [
            new ResolutionInfo(640, 480, 800, 600),
            new ResolutionInfo(640, 480, 1024, 768),

            // 游戏尺寸不等于帧缓冲的那一行不该出现在列表里。
            new ResolutionInfo(800, 600, 800, 600),
        ];

        /// <inheritdoc />
        public void SetFullScreen(bool enable) => _ = enable;

        /// <inheritdoc />
        public void SetResolution(ResolutionInfo resolution) => Applied = resolution;

        /// <inheritdoc />
        public void ResetRenderer()
        {
        }
    }

    /// <summary>An engine that only carries the display.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public FakeEngine(IDisplayControl display) => Display = display;

        /// <inheritdoc />
        public Rect ScreenArea => new(new Point(0, 0), new Size(640, 480));

        /// <inheritdoc />
        public IDisplayControl? Display { get; }

        /// <inheritdoc />
        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            _ = state;
            _ = allowExit;
            displayRefreshRequired = false;

            return true;
        }

        /// <inheritdoc />
        public void Render(Rect area) => _ = area;

        /// <inheritdoc />
        public void Sleep(ulong milliseconds) => _ = milliseconds;
    }
}
