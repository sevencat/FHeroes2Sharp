using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The three object editors that only need an existing window and one metadata table: the sign text, the stack count
/// and the resource pile count (<c>editor_interface.cpp:2266-2362</c>).
/// </summary>
/// <remarks>
/// 每支都是「开窗 → 给值 → 按 OKAY」，所以用例把 <see cref="EditorScene.RunModal"/> 换成一段按窗口类型给值的桩，
/// 再核对 <see cref="EditorScene.Metadata"/> 里的表。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorSceneObjectEditorTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 21;
    private const int ObjectX = 14;
    private const int ObjectY = 9;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the windows read their artwork from.</summary>
    public EditorSceneObjectEditorTests()
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
    public void ASignKeepsTheTextTheAuthorTyped()
    {
        EditorScene? scene = CreateScene(SignTile());

        if (scene is null)
        {
            return;
        }

        string? header = null;

        scene.RunModal = screen =>
        {
            var input = (InputStringModal)screen;

            header = input.Window.Dialog.Body;

            // 多行输入里回车是换行，确认只能点 OKAY；这里打三个字母再点。
            foreach (Key letter in new[] { Key.M, Key.I, Key.L })
            {
                screen.PressKey(letter, KeyModifier.None);
            }

            Click(input, input.Window.OkayArea);
        };

        Assert.True(scene.EditSign(new Point(ObjectX, ObjectY)));
        Assert.Equal(string.Format(EditorScene.InputObjectTextHeader,
            Mp2ObjectName.Of(Mp2ObjectType.Sign)), header);
        Assert.Equal(new Point(ObjectX, ObjectY), scene.LastObjectEditorTile);

        Assert.True(scene.Metadata.Signs.TryGetValue(ObjectUid, out SignMetadata? kept));
        // 没有修饰键时字母出小写，与参考的 InsertKeySym 一致。
        Assert.Equal("mil", kept.Message);
    }

    [Fact]
    public void CancellingTheSignBoxLeavesTheTableAlone()
    {
        EditorScene? scene = CreateScene(SignTile());

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen => screen.HotKey(ButtonHotKey.Cancel);

        Assert.True(scene.EditSign(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.Signs.ContainsKey(ObjectUid));
    }

    [Fact]
    public void AStackKeepsTheCountTheAuthorChose()
    {
        EditorScene? scene = CreateScene(MonsterTile());

        if (scene is null)
        {
            return;
        }

        string? header = null;

        scene.RunModal = screen =>
        {
            var count = (SelectCountModal)screen;

            header = count.View.Dialog.Header;
            count.View.Value.Value = 42;
            screen.HotKey(ButtonHotKey.Okay);
        };

        Assert.True(scene.EditMonsterCount(new Point(ObjectX, ObjectY)));
        Assert.Equal(string.Format(EditorScene.SetMonsterCountHeader,
            MonsterNames.GetSingular(MonsterType.Peasant)), header);

        Assert.True(scene.Metadata.Monsters.TryGetValue(ObjectUid, out MonsterMetadata? kept));
        Assert.Equal(42, kept.Count);
    }

    [Fact]
    public void APileKeepsTheCountTheAuthorChose()
    {
        EditorScene? scene = CreateScene(ResourceTile());

        if (scene is null)
        {
            return;
        }

        string? header = null;

        scene.RunModal = screen =>
        {
            var count = (SelectCountModal)screen;

            header = count.View.Dialog.Header;
            count.View.Value.Value = 7;
            screen.HotKey(ButtonHotKey.Okay);
        };

        Assert.True(scene.EditResourceCount(new Point(ObjectX, ObjectY)));
        Assert.Equal(string.Format(EditorScene.SetResourceCountHeader, Mp2ObjectData.Name(Mp2ResourceType.Wood)),
            header);

        Assert.True(scene.Metadata.Resources.TryGetValue(ObjectUid, out ResourceMetadata? kept));
        Assert.Equal(7, kept.Count);
    }

    [Fact]
    public void CancellingTheCountWindowLeavesTheTableAlone()
    {
        EditorScene? scene = CreateScene(ResourceTile());

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen => screen.HotKey(ButtonHotKey.Cancel);

        Assert.True(scene.EditResourceCount(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.Resources.ContainsKey(ObjectUid));
    }

    [Fact]
    public void TheEditorsIgnoreTilesTheyDoNotOwn()
    {
        EditorScene? scene = CreateScene(SignTile());

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open");

        // 一格是标牌，那它就不该被怪物或资源那两支认领；图外的点谁也不认。
        Assert.False(scene.EditMonsterCount(new Point(ObjectX, ObjectY)));
        Assert.False(scene.EditResourceCount(new Point(ObjectX, ObjectY)));
        Assert.False(scene.EditSign(new Point(-1, 0)));
        Assert.Null(scene.LastObjectEditorTile);
    }

    /// <summary>A sign on the object tile.</summary>
    private static Mp2TileInfo SignTile() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex).Tile(0, 0) with
    {
        ObjectType = Mp2ObjectType.ActionSign,
    };

    /// <summary>A peasant stack: the object type carries the troop count, the sprite names the creature.</summary>
    private static Mp2TileInfo MonsterTile() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex).Tile(0, 0) with
    {
        ObjectType = Mp2ObjectType.ActionMonster,
        ObjectName2 = (byte)((int)Mp2ObjectIcnType.Mons32 << 2),

        // MONS32 frame 0 is the peasant.
        TopIcnImageIndex = 0,
        Quantity1 = 3,
    };

    /// <summary>A wood pile: the sprite names the resource.</summary>
    private static Mp2TileInfo ResourceTile() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex).Tile(0, 0) with
    {
        ObjectType = Mp2ObjectType.ActionResource,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnRsrc << 2),
        BottomIcnImageIndex = 1,
    };

    /// <summary>
    /// Feeds one left click to a window that takes the cursor and the button separately, which is what
    /// <see cref="InputStringModal"/> does.
    /// </summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private static void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        modal.SetCursor(point);
        modal.SetLeftButton(true);
        modal.SetLeftButton(false);
    }

    /// <summary>Opens the editor on a map whose object tile holds <paramref name="tile"/>.</summary>
    /// <param name="tile">The tile to place on (14, 9), object type and UID already set by the caller.</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(Mp2TileInfo tile)
    {
        if (_assets is null)
        {
            return null;
        }

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTile(ObjectX, ObjectY, tile with
        {
            ObjectType = TileTypeOf(tile),
            Level1ObjectUid = ObjectUid,
        });

        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateFromMap(_frame, _assets, fonts, map, out _);
    }

    private static Mp2ObjectType TileTypeOf(Mp2TileInfo tile) => tile.ObjectType;
}
