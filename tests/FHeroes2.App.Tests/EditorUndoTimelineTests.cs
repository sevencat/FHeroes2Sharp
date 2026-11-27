using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The single undo timeline of the editor: a metadata change and a tile stroke take turns in the same history, so
/// <c>Ctrl</c>+<c>Z</c> walks them back in the reverse order they were made (<c>history_manager.h:97-188</c>).
/// </summary>
/// <remarks>
/// 参考只有一条 <c>HistoryManager</c>；端口原来把地块与元数据分在两个栈里。这一组用例盯的是「合并成一条」之后
/// 的顺序与深度。
/// </remarks>
public sealed class EditorUndoTimelineTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 31;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the scene reads its terrain tiles from.</summary>
    public EditorUndoTimelineTests()
    {
        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }
    }

    /// <summary>Restores the previous bundle.</summary>
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void AMetadataChangeIsUndoneByTheSameStackAsAStroke()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        // 先画一格（地块步骤）。
        Assert.True(scene.PaintArea(new Point(2, 2), 1, 1, MapGround.Water));
        Assert.Equal(1, scene.Edits.UndoDepth);

        // 再改一条元数据（元数据步骤），它要落到同一条栈上。
        scene.Metadata.Signs[ObjectUid] = new SignMetadata { Message = "before" };

        MetadataMapAction<SignMetadata> action = new(scene.Metadata.Signs, metadata => metadata.Clone());
        scene.Metadata.Signs[ObjectUid] = new SignMetadata { Message = "after" };
        Assert.True(action.Prepare(0));

        scene.Edits.RecordStep(action);

        Assert.Equal(2, scene.Edits.UndoDepth);

        // 第一次撤销退回元数据，第二次撤销退回地块。
        Assert.True(scene.Edits.Undo());
        Assert.Equal("before", scene.Metadata.Signs[ObjectUid].Message);

        Assert.True(scene.Edits.Undo());
        Assert.Equal(MapGround.Grass, GroundAt(scene, 2, 2));

        Assert.Equal(0, scene.Edits.UndoDepth);
        Assert.False(scene.Edits.Undo());
    }

    [Fact]
    public void RedoWalksTheSameTimelineForward()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        Assert.True(scene.PaintArea(new Point(3, 3), 1, 1, MapGround.Water));

        MetadataMapAction<SignMetadata> action = new(scene.Metadata.Signs, metadata => metadata.Clone());
        scene.Metadata.Signs[ObjectUid] = new SignMetadata { Message = "after" };
        Assert.True(action.Prepare(0));
        scene.Edits.RecordStep(action);

        Assert.True(scene.Edits.Undo());
        Assert.True(scene.Edits.Undo());

        // 重做按原顺序回来：先地块，再元数据。
        Assert.True(scene.Edits.Redo());
        Assert.Equal(MapGround.Water, GroundAt(scene, 3, 3));

        Assert.True(scene.Edits.Redo());
        Assert.Equal("after", scene.Metadata.Signs[ObjectUid].Message);
        Assert.Equal(2, scene.Edits.UndoDepth);
    }

    [Fact]
    public void AnEmptyMetadataTableSurvivesTheRoundTrip()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        MetadataMapAction<SignMetadata> action = new(scene.Metadata.Signs, metadata => metadata.Clone());
        scene.Metadata.Signs[ObjectUid] = new SignMetadata { Message = "only one" };
        Assert.True(action.Prepare(0));
        scene.Edits.RecordStep(action);

        Assert.True(scene.Edits.Undo());
        Assert.False(scene.Metadata.Signs.ContainsKey(ObjectUid));

        Assert.True(scene.Edits.Redo());
        Assert.Equal("only one", scene.Metadata.Signs[ObjectUid].Message);
    }

    /// <summary>Opens the editor on a new grass map, or reports the missing data by returning <see langword="null"/>.</summary>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene()
    {
        if (_assets is null)
        {
            return null;
        }

        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateNew(_frame, _assets, fonts, MapSize, out _);
    }

    private static MapGround GroundAt(EditorScene scene, int x, int y) =>
        MapGrounds.FromTerrainImageIndex(scene.Map.Tile(x, y).TerrainImageIndex);
}
