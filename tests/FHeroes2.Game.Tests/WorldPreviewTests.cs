using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="World.CreatePreview"/>: the editor's View World needs a world for the map it is editing
/// (<c>editor_interface.cpp:2183</c>), without becoming the world the game plays.
/// </summary>
public sealed class WorldPreviewTests
{
    private const int MapSize = 36;

    /// <summary>Index of a tile of the synthetic map, <c>x + y * width</c>.</summary>
    /// <param name="x">Column.</param>
    /// <param name="y">Row.</param>
    /// <returns>The tile index.</returns>
    private static int At(int x, int y) => x + (y * MapSize);

    /// <summary>An empty tile of the given terrain.</summary>
    /// <param name="terrainImageIndex">Terrain image; at or above the water index means water.</param>
    /// <returns>The tile structure.</returns>
    private static Mp2TileInfo EmptyTile(ushort terrainImageIndex) => new()
    {
        TerrainImageIndex = terrainImageIndex,
        ObjectName1 = 0,
        BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        Quantity1 = 0,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.None,
        NextAddonIndex = 0,
        Level1ObjectUid = 0,
        Level2ObjectUid = 0,
    };

    [Fact]
    public void CreatePreviewCopiesTheMapAndLeavesThePlayedWorldAlone()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        map.SetTileAt(At(7, 3), EmptyTile(MapGrounds.WaterStartImageIndex));

        World played = World.Current;

        World world = World.CreatePreview(map);

        // 预览是独立的一份：游戏正在跑的那个世界不能被它换掉。
        Assert.Same(played, World.Current);
        Assert.NotSame(played, world);

        Assert.Equal(MapSize, world.Width);
        Assert.Equal(MapSize, world.Height);
        Assert.Equal(MapSize * MapSize, world.Size);

        Assert.True(world.GetTile(7, 3).IsWater);
        Assert.False(world.GetTile(8, 3).IsWater);

        // 地图对象只被读：预览建完之后格子还是原来的样子。
        Assert.Equal(MapGrounds.WaterStartImageIndex, map.Tile(7, 3).TerrainImageIndex);
    }

    [Fact]
    public void CreatePreviewGivesTheWindowAFogToReadTheRadar()
    {
        World world = World.CreatePreview(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        // 雷达按每格的雾色着色（ViewWorldRadar），所以预览世界必须能给出这一层。新世界的每格都是
        // 「所有玩家都还在雾里」（maps_tiles.h:421），预览窗按 ViewAll 打开，不读这层，但雷达要它。
        Assert.NotNull(world.Fog);
        Assert.Equal(PlayerColors.All, world.GetTile(0, 0).FogColors);
        Assert.Equal(PlayerColors.All, world.GetTile(MapSize - 1, MapSize - 1).FogColors);
    }

    [Fact]
    public void CreatePreviewRejectsAMalformedMap()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        Mp2Map broken = map with { Tiles = [] };

        Assert.Throws<ArgumentException>(() => World.CreatePreview(broken));
    }

    [Fact]
    public void CreatePreviewRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => World.CreatePreview(null!));
    }
}
