using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The four artifact editors of the editor, <c>editor_interface.cpp:2346-2437</c>: the ultimate artifact's radius, the
/// spell of a spell scroll, the candidates of a random artifact, and the plain description of a single one.
/// </summary>
/// <remarks>
/// 四支共用一个入口 <see cref="EditorScene.EditArtifact"/>，按物件类型分派；用例各自造一格、把
/// <see cref="EditorScene.RunModal"/> 换成按窗口类型作答的桩，再核对 <see cref="EditorScene.Metadata"/> 里的表。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorArtifactEditorTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 41;
    private const int ObjectX = 16;
    private const int ObjectY = 11;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the windows read their artwork from.</summary>
    public EditorArtifactEditorTests()
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
    public void ASingleArtifactOpensItsDescriptionAndWritesNothing()
    {
        EditorScene? scene = CreateScene(ArtifactTile(ArtifactType.Ankh, Mp2ObjectType.ActionArtifact));

        if (scene is null)
        {
            return;
        }

        string? header = null;

        scene.RunModal = screen =>
        {
            var box = (MessageBoxModal)screen;

            header = box.Box.Header;
            box.HotKey(ButtonHotKey.Okay);
        };

        Assert.True(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.Equal(new Point(ObjectX, ObjectY), scene.LastArtifactEditorTile);
        Assert.Equal(ArtifactTable.Get(ArtifactType.Ankh).Name, header);

        // 说明框什么都不写。
        Assert.False(scene.Metadata.Artifacts.ContainsKey(ObjectUid));
    }

    [Fact]
    public void TheUltimateArtifactKeepsTheRadiusTheAuthorChose()
    {
        EditorScene? scene = CreateScene(ArtifactTile(ArtifactType.Unknown, Mp2ObjectType.ActionRandomUltimateArtifact));

        if (scene is null)
        {
            return;
        }

        string? header = null;

        scene.RunModal = screen =>
        {
            var count = (SelectCountModal)screen;

            header = count.View.Dialog.Header;
            count.View.Value.Value = 33;
            screen.HotKey(ButtonHotKey.Okay);
        };

        Assert.True(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.Equal(EditorScene.SetUltimateRadiusHeader, header);

        Assert.True(scene.Metadata.Artifacts.TryGetValue(ObjectUid, out ArtifactMetadata? kept));
        Assert.Equal(33, kept.Radius);
    }

    [Fact]
    public void AChosenRadiusIsUndoneByTheSameStackAsAStroke()
    {
        EditorScene? scene = CreateScene(ArtifactTile(ArtifactType.Unknown, Mp2ObjectType.ActionRandomUltimateArtifact));

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var count = (SelectCountModal)screen;

            count.View.Value.Value = 12;
            screen.HotKey(ButtonHotKey.Okay);
        };

        Assert.True(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.Equal(12, scene.Metadata.Artifacts[ObjectUid].Radius);

        // 写完进的是同一条撤销栈（ADR-359），Ctrl+Z 退回「表里没有这一条」。
        Assert.True(scene.Edits.Undo());
        Assert.False(scene.Metadata.Artifacts.ContainsKey(ObjectUid));
    }

    [Fact]
    public void CancellingTheRadiusWindowLeavesTheTableAlone()
    {
        EditorScene? scene = CreateScene(ArtifactTile(ArtifactType.Unknown, Mp2ObjectType.ActionRandomUltimateArtifact));

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen => screen.HotKey(ButtonHotKey.Cancel);

        Assert.True(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.Artifacts.ContainsKey(ObjectUid));
    }

    [Fact]
    public void TheSpellScrollKeepsTheSpellTheAuthorPicked()
    {
        EditorScene? scene = CreateScene(ArtifactTile(ArtifactType.SpellScroll, Mp2ObjectType.ActionArtifact));

        if (scene is null)
        {
            return;
        }

        bool opened = false;

        scene.RunModal = screen =>
        {
            opened = true;

            var picker = Assert.IsType<SpellSelectModal>(screen);

            // 列表把「点当前行」当 OK：两下同一个位置就关窗。
            Point row = new(picker.Window.ItemsArea.X + 20, picker.Window.ItemsArea.Y + 10);

            Click(picker, row);
            Click(picker, row);
        };

        Assert.True(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.True(opened, "the spell picker did not open");

        Assert.True(scene.Metadata.Artifacts.TryGetValue(ObjectUid, out ArtifactMetadata? kept));
        Assert.Single(kept.Selected);
    }

    [Fact]
    public void ARandomArtifactKeepsTheCandidatesTheAuthorPicked()
    {
        EditorScene? scene = CreateScene(ArtifactTile(ArtifactType.Unknown, Mp2ObjectType.ActionRandomArtifactMinor));

        if (scene is null)
        {
            return;
        }

        bool opened = false;

        scene.RunModal = screen =>
        {
            opened = true;

            var picker = Assert.IsType<MultiArtifactSelectModal>(screen);

            // 列表一打开就全选（参考的 _setup 在空集时全选），直接确定即可。
            // 热键要先过一帧才交给窗口（模态的 HotKey 只记下，Update 才转），同其它列表窗。
            screen.HotKey(ButtonHotKey.Okay);
            Pump(screen);
        };

        Assert.True(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.True(opened, "the artifact picker did not open");

        Assert.True(scene.Metadata.Artifacts.TryGetValue(ObjectUid, out ArtifactMetadata? kept));

        // 次要神器的候选都落在 Minor 这一档。
        Assert.NotEmpty(kept.Selected);

        foreach (int id in kept.Selected)
        {
            Assert.Equal(ArtifactLevel.Minor,
                ArtifactLevels.Level((ArtifactType)id, scene.Map.Info.Version) & ArtifactLevel.Minor);
        }
    }

    [Fact]
    public void CancellingTheRandomArtifactPickerLeavesTheTableAlone()
    {
        EditorScene? scene = CreateScene(ArtifactTile(ArtifactType.Unknown, Mp2ObjectType.ActionRandomArtifact));

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            screen.HotKey(ButtonHotKey.Cancel);
            Pump(screen);
        };

        Assert.True(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.Artifacts.ContainsKey(ObjectUid));
    }

    [Fact]
    public void APlainTileIsNotAnArtifact()
    {
        EditorScene? scene = CreateScene(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex).Tile(0, 0));

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on plain ground");

        Assert.False(scene.EditArtifact(new Point(ObjectX, ObjectY)));
        Assert.Null(scene.LastArtifactEditorTile);
    }

    [Fact]
    public void AnObeliskShowsHowManyThereAreAndWritesNothing()
    {
        EditorScene? scene = CreateScene(ObeliskTile());

        if (scene is null)
        {
            return;
        }

        string? body = null;

        scene.RunModal = screen =>
        {
            var box = (MessageBoxModal)screen;

            body = box.Box.Body;
            box.HotKey(ButtonHotKey.Okay);
        };

        Assert.True(scene.ShowObeliskCount(new Point(ObjectX, ObjectY)));
        Assert.Equal(string.Format(EditorScene.ObeliskCountMessage, 1), body);

        // 这一支不写任何元数据。
        Assert.False(scene.Metadata.Artifacts.ContainsKey(ObjectUid));
        Assert.False(scene.Metadata.Selections.ContainsKey(ObjectUid));
    }

    [Fact]
    public void APlainTileIsNotAnObelisk()
    {
        EditorScene? scene = CreateScene(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex).Tile(0, 0));

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on plain ground");

        Assert.False(scene.ShowObeliskCount(new Point(ObjectX, ObjectY)));
    }

    /// <summary>An obelisk on the object tile.</summary>
    private static Mp2TileInfo ObeliskTile() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex).Tile(0, 0)
        with
        {
            ObjectType = Mp2ObjectType.ActionObelisk,
        };

    /// <summary>A tile of one artifact object, its sprite frame naming the artifact.</summary>
    /// <param name="artifact">The artifact the sprite stands for; <see cref="ArtifactType.Unknown"/> for a random one.</param>
    /// <param name="type">The object type of the tile.</param>
    /// <returns>The tile, without its UID.</returns>
    private static Mp2TileInfo ArtifactTile(ArtifactType artifact, Mp2ObjectType type)
    {
        Mp2TileInfo blank = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex).Tile(0, 0);

        // 单件神器与法术卷轴的精灵帧是 2 * id - 1；随机的三支各有自己的固定帧（163 起），类型本身已经说明了一切。
        int frame = artifact == ArtifactType.Unknown ? 163 : (2 * (int)artifact) - 1;

        return blank with
        {
            ObjectType = type,
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnArti << 2),
            BottomIcnImageIndex = (byte)Math.Clamp(frame, 0, 255),
        };
    }

    /// <summary>Opens the editor on a map whose object tile holds <paramref name="tile"/>.</summary>
    /// <param name="tile">The tile to place on (16, 11).</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(Mp2TileInfo tile)
    {
        if (_assets is null)
        {
            return null;
        }

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        // 新图默认是 SuccessionWars，那种版本里资料片神器的地图下标不被认（与参考同口径），
        // 而法术卷轴正是资料片神器，所以用例显式用 PriceOfLoyalty。
        map = map with { Info = map.Info with { Version = MapVersion.PriceOfLoyalty } };

        map.SetTile(ObjectX, ObjectY, tile with { Level1ObjectUid = ObjectUid });

        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateFromMap(_frame, _assets, fonts, map, out _);
    }

    /// <summary>Runs one modal frame without input, the frame a kept hot key is handed over on.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on a point, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="point">The point to click.</param>
    private void Click(IModalScreen modal, Point point)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }
}
