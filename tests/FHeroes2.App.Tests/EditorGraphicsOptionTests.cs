using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The Graphics option of the editor options page, <c>editor_options.cpp:307-311</c> opening
/// <c>openGraphicsSettingsDialog( rebuildEditor )</c>: one turn of the page per modal, the leaf applied to the settings,
/// the page reopened until the author closes it.
/// </summary>
/// <remarks>
/// 用例自己当帧泵，事件塞进 <see cref="LocalEvent.Current"/>，同 <c>EditorFileMenuModalTests</c>；页面只认那一套输入
/// （<see cref="GraphicsSettingsDialog.ProcessEvents"/> 不解 <c>IModalScreen</c> 的 <c>SetCursor</c>）。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorGraphicsOptionTests : IDisposable
{
    private const int MapSize = 36;

    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());
    private readonly bool _previousVSync;

    /// <summary>Opens the archive and the fonts once, and keeps the settings the page writes.</summary>
    public EditorGraphicsOptionTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
        _previousVSync = Settings.Get().IsVSyncEnabled;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Settings.Get().SetVSync(_previousVSync);
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void WithoutTheGameDataThereIsNoPage()
    {
        var host = new EditorSceneHost(assets: null, fonts: null);

        Assert.Null(host.TryCreateGraphicsSettingsDialog());
        Assert.Equal(GraphicsSettingsOption.Exit, host.RunGraphicsSettingsDialog(Page()));
    }

    [Fact]
    public void ThePageIsBuiltFromTheInstalledArtwork()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        GraphicsSettingsDialog? page = host.TryCreateGraphicsSettingsDialog();

        Assert.NotNull(page);
        Assert.Equal(GraphicsSettingsDialog.Width, page.Window.ActiveArea.Width);
        Assert.Equal(GraphicsSettingsDialog.Height, page.Window.ActiveArea.Height);
    }

    [Fact]
    public void WithoutTheFramePumpThePageIsSkipped()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateGraphicsSettingsDialog() is not { } page)
        {
            return;
        }

        Assert.Null(host.RunModal);
        Assert.Equal(GraphicsSettingsOption.Exit, host.RunGraphicsSettingsDialog(page));
    }

    [Fact]
    public void ClickingALeafEndsThePageWithIt()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        GraphicsSettingsOption option = GraphicsSettingsOption.None;

        host.RunModal = modal =>
        {
            var page = (GraphicsSettingsModal)modal;

            Click(page, page.Dialog.VSyncRoi);
            option = page.Option;
        };

        Assert.Equal(GraphicsSettingsOption.VSync, host.RunGraphicsSettingsDialog(Page()));
        Assert.Equal(GraphicsSettingsOption.VSync, option);
    }

    [Fact]
    public void ClickingOkayClosesThePage()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        host.RunModal = modal =>
        {
            var page = (GraphicsSettingsModal)modal;

            Click(page, page.Dialog.Buttons[0].Area);
        };

        Assert.Equal(GraphicsSettingsOption.Exit, host.RunGraphicsSettingsDialog(Page()));
    }

    [Fact]
    public void OpenGraphicsSettings_AppliesTheLeafAndReportsTheChange()
    {
        if (!TryCreateEditor(out EditorScene scene))
        {
            return;
        }

        Settings settings = Settings.Get();
        bool before = settings.IsVSyncEnabled;
        int pages = 0;

        // 第一轮点 V-Sync（叶子），第二轮点 OK（关窗），与参考「选完回 Configuration」的循环一致。
        scene.RunModal = modal =>
        {
            var page = (GraphicsSettingsModal)modal;

            ++pages;

            Click(page, pages == 1 ? page.Dialog.VSyncRoi : page.Dialog.Buttons[0].Area);
        };

        Assert.True(scene.OpenGraphicsSettings());
        Assert.Equal(2, pages);
        Assert.Equal(!before, settings.IsVSyncEnabled);
    }

    [Fact]
    public void OpenGraphicsSettings_ClosingItRightAwayChangesNothing()
    {
        if (!TryCreateEditor(out EditorScene scene))
        {
            return;
        }

        bool before = Settings.Get().IsVSyncEnabled;

        scene.RunModal = modal =>
        {
            var page = (GraphicsSettingsModal)modal;

            Click(page, page.Dialog.Buttons[0].Area);
        };

        Assert.False(scene.OpenGraphicsSettings());
        Assert.Equal(before, Settings.Get().IsVSyncEnabled);
    }

    /// <summary>Builds a page over the installed data, or a stand-in when the machine has none.</summary>
    private GraphicsSettingsDialog Page()
    {
        if (TryCreateHost(out EditorSceneHost host) && host.TryCreateGraphicsSettingsDialog() is { } page)
        {
            return page;
        }

        throw new InvalidOperationException("The graphics page needs the game data, and the machine has none.");
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

    /// <summary>Feeds one left click on an area of the page, the press and the release in the same frame.</summary>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }
}
