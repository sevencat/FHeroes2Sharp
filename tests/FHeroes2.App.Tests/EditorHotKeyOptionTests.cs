using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The Hot Keys option of the editor options page, <c>editor_options.cpp:317-321</c> opening
/// <c>fheroes2::openHotkeysDialog()</c>: one modal per turn, no result and nothing to save.
/// </summary>
[Collection(LocalEventCollection.Name)]
public sealed class EditorHotKeyOptionTests : IDisposable
{
    private const int MapSize = 36;

    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts once.</summary>
    public EditorHotKeyOptionTests()
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
    public void WithoutTheGameDataThereIsNoPage()
    {
        var host = new EditorSceneHost(assets: null, fonts: null);

        Assert.Null(host.TryCreateHotKeysDialog());

        host.RunHotKeysDialog(Dialog());
    }

    [Fact]
    public void ThePageIsBuiltFromTheInstalledArtwork()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        HotKeysDialog? page = host.TryCreateHotKeysDialog();

        Assert.NotNull(page);
        Assert.NotEmpty(page.Rows);
        Assert.Equal("Hot Keys:", page.Title);
    }

    [Fact]
    public void ClosingThePageWithOkayEndsTheModal()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateHotKeysDialog() is not { } page)
        {
            return;
        }

        host.RunModal = modal =>
        {
            var keys = (HotKeysModal)modal;

            Click(keys, keys.Dialog.Buttons[0].Area);

            Assert.True(keys.IsFinished);
        };

        host.RunHotKeysDialog(page);
    }

    [Fact]
    public void EscapeEndsTheModalToo()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateHotKeysDialog() is not { } page)
        {
            return;
        }

        host.RunModal = modal =>
        {
            var keys = (HotKeysModal)modal;

            PressKey(keys, Key.Escape);

            Assert.True(keys.IsFinished);
        };

        host.RunHotKeysDialog(page);
    }

    [Fact]
    public void WithoutTheFramePumpThePageIsSkipped()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateHotKeysDialog() is not { } page)
        {
            return;
        }

        Assert.Null(host.RunModal);

        host.RunHotKeysDialog(page);
    }

    [Fact]
    public void OpenHotKeys_RunsThePageThroughTheScene()
    {
        if (!TryCreateEditor(out EditorScene scene))
        {
            return;
        }

        bool ran = false;

        scene.RunModal = modal =>
        {
            var keys = (HotKeysModal)modal;

            ran = true;

            Click(keys, keys.Dialog.Buttons[0].Area);
        };

        scene.OpenHotKeys();

        Assert.True(ran);
    }

    /// <summary>Builds a page over the installed data, or a stand-in when the machine has none.</summary>
    private HotKeysDialog Dialog()
    {
        if (TryCreateHost(out EditorSceneHost host) && host.TryCreateHotKeysDialog() is { } page)
        {
            return page;
        }

        throw new InvalidOperationException("The hot keys page needs the game data, and the machine has none.");
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

    /// <summary>Feeds one key press and release to the page.</summary>
    private void PressKey(IModalScreen modal, Key key)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        modal.Update(_frame, 0.016);
    }
}
