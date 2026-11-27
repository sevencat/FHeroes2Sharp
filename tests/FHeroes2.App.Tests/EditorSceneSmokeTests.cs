using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

namespace FHeroes2.App.Tests;

/// <summary>
/// Opens the editor scene the way the host does, without SDL: a logical frame, the archives (when the
/// machine has them) and a map size.
/// </summary>
public sealed class EditorSceneSmokeTests
{
    private const int MapSize = 36;

    [Fact]
    public void CreatingANewMapOpensTheEditorOnAnEmptyGrassMap()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory
            ? AssetBundle.Open(directory)
            : null;

        var frame = new ScreenBuffer(new Size(640, 480), new Palette());
        EditorScene? scene = EditorScene.CreateNew(frame, assets, fonts: null, MapSize, out string? error);

        // Without GROUND32.TIL the scene cannot draw a map at all; the host shows the error instead.
        if (scene is null)
        {
            Assert.NotNull(error);
            return;
        }

        Assert.Null(error);
        Assert.Equal(MapSize, scene.Map.Width);
        Assert.Equal(MapSize, scene.Map.Height);

        // A fresh map is plain grass everywhere, has nothing to undo and starts on the terrain tool with
        // the grass brush, the state the panel shows the moment the editor opens.
        Assert.Equal(MapGround.Grass, scene.Edits.Brush);
        Assert.Equal(EditorTool.Terrain, scene.Edits.Tool);
        Assert.Equal(0, scene.Edits.UndoDepth);
        Assert.Equal(MapGround.Grass, GroundAt(scene, 0, 0));
        Assert.Equal(MapGround.Grass, GroundAt(scene, MapSize - 1, MapSize - 1));
        Assert.False(scene.IsObjectInfoOpen);
        Assert.False(scene.IsObjectPopupOpen);
    }

    private static MapGround GroundAt(EditorScene scene, int x, int y) =>
        MapGrounds.FromTerrainImageIndex(scene.Map.Tile(x, y).TerrainImageIndex);
}
