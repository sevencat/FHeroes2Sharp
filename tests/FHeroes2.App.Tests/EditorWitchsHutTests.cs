using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The witch's hut of the editor, <c>editor_interface.cpp:2513-2530</c>: a click opens the secondary skill picker, and
/// only an accepted window writes the selection back.
/// </summary>
/// <remarks>
/// 窗口是全屏的（640x448 活动区），用例把 <see cref="EditorScene.RunModal"/> 换成一段按窗口作答的桩——点一个格子
/// 再按 OKAY。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorWitchsHutTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 51;
    private const int ObjectX = 18;
    private const int ObjectY = 12;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the window reads its artwork from.</summary>
    public EditorWitchsHutTests()
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
    public void AClickOnAWitchsHutOpensTheSkillPicker()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        string? title = null;
        bool opened = false;

        scene.RunModal = screen =>
        {
            var picker = Assert.IsType<EditorSecondarySkillSelectionModal>(screen);

            opened = true;
            title = picker.Window.Title;

            // 「全关」再「全开」再关窗前先选一个：直接用 Cancel 只证明取消。
            screen.HotKey(ButtonHotKey.Cancel);
        };

        Assert.True(scene.EditWitchsHut(new Point(ObjectX, ObjectY)));
        Assert.True(opened, "the skill picker did not open");
        Assert.Equal(Mp2ObjectName.Of(Mp2ObjectType.WitchsHut) + ":", title);

        // 取消什么都不写。
        Assert.False(scene.Metadata.Selections.ContainsKey(ObjectUid));
    }

    [Fact]
    public void AnAcceptedSelectionIsWrittenToTheTable()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        bool opened = false;

        scene.RunModal = screen =>
        {
            opened = true;
            var picker = (EditorSecondarySkillSelectionModal)screen;

            // 关掉一格，于是交回的不是「全开」的空列表（空列表在参考里表示「按默认」，不写表）。
            Click(picker, picker.Window.Grid.Areas[0]);
            Click(picker, picker.Window.Okay.Area);
            Pump(picker);
        };

        Assert.True(scene.EditWitchsHut(new Point(ObjectX, ObjectY)));
        Assert.True(opened, "the skill picker did not open");

        Assert.True(scene.Metadata.Selections.TryGetValue(ObjectUid, out SelectionObjectMetadata? kept));
        Assert.NotEmpty(kept.SelectedItems);
    }

    [Fact]
    public void EnablingEverythingWritesNothingBecauseThatMeansTheDefault()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var picker = (EditorSecondarySkillSelectionModal)screen;

            // 窗口一打开就是「全开」，OKAY 交回空列表；参考读空列表为「按默认」，表里因此不该多一条。
            Click(picker, picker.Window.Okay.Area);
            Pump(picker);
        };

        Assert.True(scene.EditWitchsHut(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.Selections.ContainsKey(ObjectUid));
    }

    [Fact]
    public void TheSelectionIsUndoneByTheSameStackAsAStroke()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var picker = (EditorSecondarySkillSelectionModal)screen;

            Click(picker, picker.Window.Grid.Areas[0]);
            Click(picker, picker.Window.Okay.Area);
            Pump(picker);
        };

        Assert.True(scene.EditWitchsHut(new Point(ObjectX, ObjectY)));
        Assert.True(scene.Metadata.Selections.ContainsKey(ObjectUid));

        Assert.True(scene.Edits.Undo());
        Assert.False(scene.Metadata.Selections.ContainsKey(ObjectUid));
    }

    [Fact]
    public void APlainTileIsNotAWitchsHut()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.Mine);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on a mine");

        Assert.False(scene.EditWitchsHut(new Point(ObjectX, ObjectY)));
    }

    /// <summary>Opens the editor on a map with one object on (18, 12).</summary>
    /// <param name="type">The object type; a witch's hut when omitted.</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(Mp2ObjectType type = Mp2ObjectType.ActionWitchsHut)
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

    /// <summary>Feeds one left click on an area, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Runs one modal frame without input.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }
}
