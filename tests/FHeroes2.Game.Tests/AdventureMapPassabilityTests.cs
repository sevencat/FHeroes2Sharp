using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The passability pass of <see cref="AdventureMapView"/>, the <c>LEVEL_PASSABILITIES</c> branch of
/// <c>Interface::GameArea::Redraw()</c> (<c>interface_gamearea.cpp:768-776</c>).
/// </summary>
/// <remarks>
/// 用例用 2x2 的地块和 4x4 的视口：31x31 的标记整块盖住视口，所以“边上一个像素被换成标记色、内部还是地形色”这两点同时
/// 说明了标记画在了地块左上角、并且按视口裁剪。
/// </remarks>
public sealed class AdventureMapPassabilityTests
{
    private const int TileSize = 2;

    /// <summary>`SyntheticMap.BlankTile` uses terrain image 1, which the til below fills with 2.</summary>
    private const byte Terrain = 2;
    private const byte Red = 0xBA;

    [Fact]
    public void Draw_WithoutTheOverlay_PaintsThePlainTerrain()
    {
        PixelBuffer target = new(4, 4);

        CreateView(BlockedMap()).Draw(target);

        Assert.Equal(Terrain, target.Row(0)[0]);
    }

    [Fact]
    public void Draw_WithTheOverlay_PaintsTheMarkerOfABlockedTile()
    {
        Mp2Map map = BlockedMap();
        PixelBuffer target = new(4, 4);

        CreateView(map).Draw(target, passability: MapTileSet.Create(map));

        // 山把整格封死，框是红的；框里面不涂，地形还在。
        Assert.Equal(Red, target.Row(0)[0]);
        Assert.Equal(Red, target.Row(0)[1]);
        Assert.Equal(Terrain, target.Row(1)[1]);
    }

    [Fact]
    public void Draw_WithTheOverlay_LeavesFreeTilesAlone()
    {
        Mp2Map map = SyntheticMap.Create(2, 2, _ => SyntheticMap.BlankTile());
        PixelBuffer target = new(4, 4);

        CreateView(map).Draw(target, passability: MapTileSet.Create(map));

        // 全通又不是动作对象的地块没有标记（maps_tiles_render.cpp:926-928），地形原样露出来。
        Assert.Equal(Terrain, target.Row(0)[0]);
        Assert.Equal(Terrain, target.Row(0)[1]);
    }

    [Fact]
    public void Draw_WithATileSetOfAnotherMap_Throws()
    {
        Mp2Map map = BlockedMap();
        MapTileSet foreign = MapTileSet.Create(SyntheticMap.Create(3, 3, _ => SyntheticMap.BlankTile()));

        Assert.Throws<ArgumentException>(() => CreateView(map).Draw(new PixelBuffer(4, 4), passability: foreign));
    }

    /// <summary>A 2x2 map whose first tile carries a mountain, i.e. a tile a hero cannot leave.</summary>
    private static Mp2Map BlockedMap() =>
        SyntheticMap.Create(2, 2, index => index == 0 ? SyntheticMap.BlankTile() with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.MtnGras << 2),
            BottomIcnImageIndex = 14,
            Quantity1 = (byte)ObjectLayerType.Object,
            Level1ObjectUid = 1,
            ObjectType = Mp2ObjectType.Mountains,
        } : SyntheticMap.BlankTile());

    private static AdventureMapView CreateView(Mp2Map map) =>
        new(map, TerrainTileSet.Create(CreateTerrainTil(4)), new Size(4, 4));

    /// <summary>One flat 2x2 image per tile; image <c>i</c> is filled with <c>i + 1</c>.</summary>
    private static TilFile CreateTerrainTil(int count)
    {
        List<byte> data = [(byte)count, (byte)(count >> 8), TileSize, 0, TileSize, 0];

        for (int index = 0; index < count; ++index)
        {
            for (int pixel = 0; pixel < TileSize * TileSize; ++pixel)
            {
                data.Add((byte)(index + 1));
            }
        }

        return TilFile.Parse([.. data]);
    }
}
