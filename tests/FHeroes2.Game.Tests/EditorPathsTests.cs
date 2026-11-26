using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The stream and road instruments of the editor: <c>setRoadOnTile</c> and <c>addStream</c> of the
/// reference (<c>src/fheroes2/maps/map_format_helper.cpp:884</c> and <c>:1202</c>).
/// </summary>
/// <remarks>
/// 参考靠物件表知道「哪一格有路」，端口只有 MP2 格记录，所以判路用的是参考运行时的贴图表（<c>isSpriteRoad</c>）。
/// 这些用例盯的就是这件事：一条路铺下去之后，自己与邻居的贴图都得对，而只有接头面片的格子不算有路。
/// </remarks>
public sealed class EditorPathsTests
{
    /// <summary>An MP2 map is 36, 72, 108 or 144 tiles wide; the smallest one keeps these tests quick.</summary>
    private const int MapSize = 36;

    [Fact]
    public void TheFirstRoadOfAnEmptyMapTakesTheLoneSprite()
    {
        MapEditSession session = CreateSession();

        Assert.True(session.LayRoad(new Point(2, 2)));

        Mp2TileInfo tile = session.Map.Tile(2, 2);
        Assert.Equal(Mp2ObjectIcnType.Road, tile.GroundIcnType);
        Assert.Equal(RoadSprite(0), tile.BottomIcnImageIndex);
        Assert.NotEqual(0u, tile.Level1ObjectUid);

        // 地形不动，路是画在地上的。
        Assert.Equal(MapGrounds.GrassStartImageIndex, tile.TerrainImageIndex);
    }

    [Fact]
    public void TwoRoadsSideBySideConnectToEachOther()
    {
        MapEditSession session = CreateSession();

        session.LayRoad(new Point(2, 2));
        session.LayRoad(new Point(3, 2));

        // 后铺的那条向西接（掩码 LEFT），先铺的那条被重画成向东接（掩码 RIGHT）。
        Assert.Equal(RoadSprite((int)Direction.Left), session.Map.Tile(3, 2).BottomIcnImageIndex);
        Assert.Equal(RoadSprite((int)Direction.Right), session.Map.Tile(2, 2).BottomIcnImageIndex);
    }

    [Fact]
    public void ARedrawnRoadKeepsItsOwnUid()
    {
        MapEditSession session = CreateSession();

        session.LayRoad(new Point(2, 2));
        uint uid = session.Map.Tile(2, 2).Level1ObjectUid;

        session.LayRoad(new Point(3, 2));

        // 拆开重铺走的是同一个号：对游戏来说这仍是同一条路。
        Assert.Equal(uid, session.Map.Tile(2, 2).Level1ObjectUid);
    }

    [Fact]
    public void ARoadUnderACastleIsTheEntranceRoad()
    {
        MapEditSession session = CreateSession();
        PlaceCastle(session, new Point(2, 1));

        Assert.True(session.LayRoad(new Point(2, 2)));

        Assert.Equal(RoadSprite(512), session.Map.Tile(2, 2).BottomIcnImageIndex);
    }

    [Fact]
    public void ARoadNextToTheEntranceKeepsTheEntranceSprite()
    {
        MapEditSession session = CreateSession();
        PlaceCastle(session, new Point(2, 1));

        session.LayRoad(new Point(2, 2));
        session.LayRoad(new Point(3, 2));

        // 引道不管接了几条路都还是引道，右边那条则照常往西接。
        Assert.Equal(RoadSprite(512), session.Map.Tile(2, 2).BottomIcnImageIndex);
        Assert.Equal(RoadSprite((int)Direction.Left), session.Map.Tile(3, 2).BottomIcnImageIndex);
    }

    [Fact]
    public void AConnectingCornerDoesNotCountAsARoad()
    {
        MapEditSession session = CreateSession();

        // 相邻格里伸过来的一条：它是别的道路的面片，不代表这一格自己有路（isSpriteRoad 不认它）。
        Mp2TileInfo corner = session.Map.Tile(2, 2) with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.Road << 2),
            BottomIcnImageIndex = 15,
            Level1ObjectUid = 9,
        };
        session.Map.SetTile(2, 2, corner);

        Assert.True(session.LayRoad(new Point(3, 2)));

        Assert.Equal(RoadSprite(0), session.Map.Tile(3, 2).BottomIcnImageIndex);
        Assert.Equal(15, session.Map.Tile(2, 2).BottomIcnImageIndex);
    }

    [Fact]
    public void OneRoadIsOneStepOfHistory()
    {
        MapEditSession session = CreateSession();

        session.LayRoad(new Point(2, 2));

        // 第一条道路会把接头面片伸进右邻格，撤销第二条时这一带要回到只有第一条的样子。
        string before = DescribeRoads(session.Map, new Point(2, 2));

        session.LayRoad(new Point(3, 2));
        Assert.NotEqual(before, DescribeRoads(session.Map, new Point(2, 2)));

        Assert.Equal(2, session.UndoDepth);
        Assert.True(session.Undo());
        Assert.Equal(1, session.UndoDepth);

        // 撤销一条路要连它伸进邻居的接头面片一起收回去，邻居自己那条一格不剩。
        Assert.Equal(before, DescribeRoads(session.Map, new Point(2, 2)));
        Assert.Equal(RoadSprite(0), session.Map.Tile(2, 2).BottomIcnImageIndex);
    }

    [Fact]
    public void ADragOfTwoRoadsIsOneUndo()
    {
        MapEditSession session = CreateSession();

        session.BeginStroke();
        Assert.True(session.LayRoad(new Point(2, 2)));
        Assert.True(session.LayRoad(new Point(3, 2)));
        session.EndStroke();

        Assert.Equal(1, session.UndoDepth);
    }

    [Fact]
    public void TheFirstStreamOfAnEmptyMapIsTheLonePiece()
    {
        MapEditSession session = CreateSession();

        Assert.True(session.LayStream(new Point(2, 2)));

        Mp2TileInfo tile = session.Map.Tile(2, 2);
        Assert.Equal(Mp2ObjectIcnType.Stream, tile.GroundIcnType);
        Assert.Equal(StreamSprite(3), tile.BottomIcnImageIndex);
        Assert.NotEqual(0u, tile.Level1ObjectUid);
        Assert.Equal(MapGrounds.GrassStartImageIndex, tile.TerrainImageIndex);
    }

    [Fact]
    public void TwoStreamsSideBySideBecomeAStretch()
    {
        MapEditSession session = CreateSession();

        session.LayStream(new Point(2, 2));
        session.LayStream(new Point(3, 2));

        // 两段都只剩左右两个方向，换成横着的那张；号不变，还是各自那一条。
        Assert.Equal(StreamSprite(2), session.Map.Tile(2, 2).BottomIcnImageIndex);
        Assert.Equal(StreamSprite(2), session.Map.Tile(3, 2).BottomIcnImageIndex);
    }

    [Fact]
    public void AStreamIsRefusedOnWater()
    {
        MapEditSession session = CreateSession();

        session.Map.SetTile(2, 2, session.Map.Tile(2, 2) with { TerrainImageIndex = MapGrounds.WaterStartImageIndex });

        Assert.False(session.LayStream(new Point(2, 2)));
        Assert.Equal(0, session.UndoDepth);
        Assert.False(session.Map.Tile(2, 2).HasGroundObject);
    }

    [Fact]
    public void AStreamIsRefusedWhereOneAlreadyRuns()
    {
        MapEditSession session = CreateSession();

        Assert.True(session.LayStream(new Point(2, 2)));
        Assert.False(session.LayStream(new Point(2, 2)));
        Assert.Equal(1, session.UndoDepth);
    }

    [Fact]
    public void AStreamStripsTheEmbeddedObjectsOfItsTile()
    {
        MapEditSession session = CreateSession();
        Point tile = new(2, 2);

        // 草地里带花草的那几张（相对号 46），河流盖上去要换成干净的那一张，否则会露出半截。
        ushort decorated = (ushort)(MapGrounds.GrassStartImageIndex + 46);
        Assert.True(MapGrounds.ContainsEmbeddedObjects(decorated));

        session.Map.SetTile(tile.X, tile.Y, session.Map.Tile(tile.X, tile.Y) with { TerrainImageIndex = decorated });

        Assert.True(session.LayStream(tile));
        Assert.Equal(TerrainTransitions.PlainImageIndex(MapGround.Grass, tile), session.Map.Tile(tile.X, tile.Y).TerrainImageIndex);
    }

    [Fact]
    public void TheInstrumentsIgnoreTilesOutsideTheMap()
    {
        MapEditSession session = CreateSession();

        Assert.False(session.LayRoad(new Point(-1, 0)));
        Assert.False(session.LayRoad(null));
        Assert.False(session.LayStream(new Point(MapSize, 0)));
        Assert.False(session.LayStream(null));
        Assert.Equal(0, session.UndoDepth);
    }

    /// <summary>The road pieces of the area around a tile, as sorted <c>x,y:sprite</c> pairs.</summary>
    /// <remarks>路的面片会落到邻居格的附加链里，所以比一格不够，得连周围一起看。</remarks>
    private static string DescribeRoads(Mp2Map map, Point tile)
    {
        List<string> pieces = [];

        for (int y = tile.Y - 1; y <= tile.Y + 2; ++y)
        {
            for (int x = tile.X - 1; x <= tile.X + 2; ++x)
            {
                if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                {
                    continue;
                }

                Mp2TileInfo info = map.Tile(x, y);

                if (info.GroundIcnType == Mp2ObjectIcnType.Road)
                {
                    pieces.Add($"{x},{y}:{info.BottomIcnImageIndex}");
                }

                foreach (int index in map.EnumerateAddonChain(info))
                {
                    Mp2AddonInfo addon = map.Addons[index];

                    if (addon.GroundIcnType == Mp2ObjectIcnType.Road)
                    {
                        pieces.Add($"{x},{y}:{addon.BottomIcnImageIndex}");
                    }
                }
            }
        }

        pieces.Sort(StringComparer.Ordinal);
        return string.Join(' ', pieces);
    }

    /// <summary>The sprite the anchor part of a road of <paramref name="objectIndex"/> draws.</summary>
    private static byte RoadSprite(int objectIndex) =>
        (byte)MapObjectCatalog.GetObjectInfo(ObjectGroup.Roads, objectIndex).GroundLevelParts[0].IcnIndex;

    /// <summary>The sprite of a stream direction, the <c>getStreamIndex</c> of the reference.</summary>
    private static byte StreamSprite(int objectIndex) =>
        (byte)MapObjectCatalog.GetObjectInfo(ObjectGroup.Streams, objectIndex).GroundLevelParts[0].IcnIndex;

    /// <summary>Puts a castle on a tile so that the tile below it becomes the entrance of its road.</summary>
    private static void PlaceCastle(MapEditSession session, Point tile)
    {
        Mp2ObjectPreset castle = MapObjectCatalogPresets
            .For(ObjectGroup.KingdomTowns)
            .First(preset => preset.HasGroundPart);

        session.Map.SetTile(tile.X, tile.Y, castle.Template);
    }

    private static MapEditSession CreateSession()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        TerrainTileSet tiles = SyntheticTerrain.Create(MapSize, 32);

        return new MapEditSession(new MapEditorView(new AdventureMapView(map, tiles, new Size(96, 96))));
    }
}
