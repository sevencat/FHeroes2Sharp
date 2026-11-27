using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The spell chooser of a shrine or a pyramid, the two <c>Editor::openSpellSelectionWindow()</c> calls of
/// <c>editor_interface.cpp:2484-2504</c> and <c>:2520-2540</c>.
/// </summary>
/// <remarks>
/// 窗口由 <see cref="EditorScene.RunModal"/> 跑，用例把它换成「点掉一个法术再回车」之类的桩；表里的写回在
/// <see cref="EditorScene.Metadata"/> 上核对。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorSceneSpellChooserTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 11;
    private const int ObjectX = 12;
    private const int ObjectY = 8;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the window reads its artwork from.</summary>
    public EditorSceneSpellChooserTests()
    {
        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void AShrineOpensItsOwnLevelAndKeepsWhatIsEnabled()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.ShrineSecondCircle);

        if (scene is null)
        {
            return;
        }

        int openedLevel = 0;
        string? openedTitle = null;
        int expected = 0;

        scene.RunModal = screen =>
        {
            var window = (EditorSpellSelectionModal)screen;

            openedLevel = window.Level;
            openedTitle = window.Window.Title;
            expected = window.Window.Grid.Count - 1;

            // 关掉一个法术，交回的不是「全默认」的空列表。
            Click(window, window.Window.Grid.Areas[0]);
            Feed(window, Key.Enter);
        };

        Assert.True(scene.OpenSpellChooser(new Point(ObjectX, ObjectY)));
        Assert.Equal(new Point(ObjectX, ObjectY), scene.LastSpellChooserTile);
        Assert.Equal(2, openedLevel);
        Assert.Equal(Mp2ObjectName.Of(Mp2ObjectType.ShrineSecondCircle) + ":", openedTitle);

        Assert.True(scene.Metadata.Selections.TryGetValue(ObjectUid, out SelectionObjectMetadata? kept));
        Assert.Equal(expected, kept.SelectedItems.Count);
    }

    [Fact]
    public void APyramidAsksForLevelFive()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.Pyramid);

        if (scene is null)
        {
            return;
        }

        int level = 0;

        scene.RunModal = screen =>
        {
            var window = (EditorSpellSelectionModal)screen;

            level = window.Level;
            Feed(window, Key.Escape);
        };

        Assert.True(scene.OpenSpellChooser(new Point(ObjectX, ObjectY)));
        Assert.Equal(5, level);
    }

    [Fact]
    public void CancellingLeavesTheTableAlone()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.ShrineFirstCircle);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen => Feed((IModalScreen)screen, Key.Escape);

        Assert.True(scene.OpenSpellChooser(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.Selections.ContainsKey(ObjectUid));
    }

    [Fact]
    public void APreviousSelectionIsWhatTheWindowStartsFrom()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.ShrineThirdCircle);

        if (scene is null)
        {
            return;
        }

        int shown = -1;

        scene.RunModal = screen =>
        {
            var window = (EditorSpellSelectionModal)screen;

            // 表里先放两个已启用的法术，窗口应把它们当成当前状态。
            shown = window.Window.Grid.SelectedCount;
            Feed(window, Key.Escape);
        };

        // 表里先放两个三级法术，窗口应把它们当成当前状态。
        IReadOnlyList<SpellType> level3 = Spell.SuitableForSpellBook(3);

        Assert.True(level3.Count >= 2, "the game data has fewer than two level three spells");

        scene.Metadata.Selections[ObjectUid] = new SelectionObjectMetadata
        {
            SelectedItems =
            [
                (int)level3[0],
                (int)level3[1],
            ],
        };

        Assert.True(scene.OpenSpellChooser(new Point(ObjectX, ObjectY)));
        Assert.Equal(2, shown);
    }

    [Fact]
    public void APlainTileIsNotASpellChooser()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.Mine);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on a mine");

        Assert.False(scene.OpenSpellChooser(new Point(ObjectX, ObjectY)));
        Assert.Null(scene.LastSpellChooserTile);
    }

    /// <summary>Opens the editor on a map with one object of <paramref name="type"/> on (12, 8).</summary>
    /// <param name="type">The object the tile carries.</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(Mp2ObjectType type)
    {
        if (_assets is null)
        {
            return null;
        }

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTile(ObjectX, ObjectY, map.Tile(ObjectX, ObjectY) with
        {
            ObjectType = type,
            Level1ObjectUid = ObjectUid,
        });

        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateFromMap(_frame, _assets, fonts, map, out _);
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

    /// <summary>Feeds one left click on an area of the window, the press and the release in the same frame.</summary>
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
}
