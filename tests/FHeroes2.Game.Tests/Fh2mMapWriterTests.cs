using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="Fh2mMapWriter"/>: an editor map turned into a complete <c>.fh2m</c> file and read back.
/// </summary>
public sealed class Fh2mMapWriterTests
{
    private const int MapSize = 36;

    /// <summary>Index of a tile near the middle, far from the edges.</summary>
    private const int MiddleTile = (MapSize * MapSize / 2) + 5;

    [Fact]
    public void TheTilesOfTheMapBecomeTheTilesOfTheFile()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        Fh2mBaseMap header = Write(map, out int skipped);

        Assert.Equal(0, skipped);

        Fh2mPayload payload = Fh2mPayload.Read(header.Payload);

        Assert.Equal(MapSize * MapSize, payload.Tiles.Count);
        Assert.All(payload.Tiles, tile => Assert.Empty(tile.Objects));
        Assert.Equal(MapGrounds.GrassStartImageIndex, payload.Tiles[MiddleTile].TerrainIndex);
    }

    [Fact]
    public void AnObjectOnATileBecomesItsGroupAndIndex()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        // 一只农民：MONS32 的第 0 帧，怪物组的第 0 项。
        map.SetTileAt(MiddleTile, map.Tiles[MiddleTile] with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.Mons32 << 2),
            BottomIcnImageIndex = 0,
            ObjectType = Mp2ObjectType.ActionMonster,
            Level1ObjectUid = 42,
        });

        Fh2mBaseMap header = Write(map, out int skipped);

        Assert.Equal(0, skipped);

        Fh2mTile tile = Fh2mPayload.Read(header.Payload).Tiles[MiddleTile];
        Fh2mTileObject item = Assert.Single(tile.Objects);

        Assert.Equal((byte)ObjectGroup.Monsters, item.Group);
        Assert.Equal(0u, item.Index);
        Assert.Equal(42u, item.Id);
    }

    [Fact]
    public void AnObjectWithNoPlaceInTheTablesIsCountedAndDropped()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        // 帧号 999 在 MONS32 里不存在：反查不到地址，于是这一格的对象被丢掉并计数。
        map.SetTileAt(MiddleTile, map.Tiles[MiddleTile] with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.Mons32 << 2),
            BottomIcnImageIndex = 200,
            ObjectType = Mp2ObjectType.ActionMonster,
            Level1ObjectUid = 7,
        });

        Fh2mBaseMap header = Write(map, out int skipped);

        Assert.Equal(1, skipped);
        Assert.Empty(Fh2mPayload.Read(header.Payload).Tiles[MiddleTile].Objects);
    }

    [Fact]
    public void TheMetadataTablesReachTheFile()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        var metadata = new MapObjectMetadataStore();

        metadata.Monsters[11] = new MonsterMetadata { Count = 250, JoinCondition = 1 };
        metadata.Resources[12] = new ResourceMetadata { Count = 1500 };
        metadata.Signs[13] = new SignMetadata { Message = "North" };

        Fh2mBaseMap header = Build(map, metadata, out _);
        Fh2mPayload payload = Fh2mPayload.Read(header.Payload);

        Assert.True(payload.Monsters.TryGetValue(11u, out MonsterMetadata? stack));
        Assert.Equal(250, stack.Count);
        Assert.Equal(1, stack.JoinCondition);

        Assert.True(payload.Resources.TryGetValue(12u, out ResourceMetadata? pile));
        Assert.Equal(1500, pile.Count);

        Assert.True(payload.Signs.TryGetValue(13u, out SignMetadata? sign));
        Assert.Equal("North", sign.Message);
    }

    [Fact]
    public void RumorsAndDailyEventsOfTheSpecificationsReachTheFile()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        var specs = new MapSpecsState(map);

        specs.SetRumors(["A rumor"]);
        specs.SetDailyEvents([new MapDailyEvent { Message = "A gift", FirstOccurrenceDay = 3 }]);

        Fh2mBaseMap header = Fh2mMapWriter.Build(map, specs, new MapObjectMetadataStore(), out _);
        Fh2mPayload payload = Fh2mPayload.Read(header.Payload);

        Assert.Equal(["A rumor"], payload.Rumors);
        Assert.Equal("A gift", Assert.Single(payload.DailyEvents).Message);
    }

    [Fact]
    public void TheHeaderCarriesTheSpecifications()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        var specs = new MapSpecsState(map);

        specs.Name = "Saved map";
        specs.Description = "A description";

        Fh2mBaseMap header = Fh2mMapWriter.Build(map, specs, new MapObjectMetadataStore(), out _);

        Assert.Equal(Fh2mBaseMap.CurrentSupportedVersion, header.Version);
        Assert.Equal(MapSize, header.Width);
        Assert.Equal("Saved map", header.Name);
        Assert.Equal("A description", header.Description);
        Assert.NotEmpty(header.Payload);
    }

    [Fact]
    public void TheFileCanBeWrittenAndReadBack()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        var specs = new MapSpecsState(map);
        var metadata = new MapObjectMetadataStore();

        specs.Name = "Round trip";

        // 一张几乎全空的图压完不到 512 字节——格式的读取下限——所以这里塞一批计数各不相同的怪，让尾段有内容。
        for (uint id = 1; id <= 400; ++id)
        {
            metadata.Monsters[id] = new MonsterMetadata { Count = (int)(id * 37 % 5000) + 1 };
        }

        string path = Path.Combine(Path.GetTempPath(), "fh2m-" + Guid.NewGuid().ToString("N") + ".fh2m");

        try
        {
            int skipped = Fh2mMapWriter.Save(path, map, specs, metadata);

            Assert.Equal(0, skipped);

            using (FileStream file = File.OpenRead(path))
            {
                Assert.True(Fh2mBaseMap.HasSignature(file));
            }

            Fh2mBaseMap header = Fh2mBaseMap.ReadFile(path);

            Assert.Equal("Round trip", header.Name);

            Fh2mPayload payload = Fh2mPayload.Read(header.Payload);

            Assert.Equal(MapSize * MapSize, payload.Tiles.Count);
            Assert.Equal(400, payload.Monsters.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Fh2mBaseMap Write(Mp2Map map, out int skipped) =>
        Build(map, new MapObjectMetadataStore(), out skipped);

    private static Fh2mBaseMap Build(Mp2Map map, MapObjectMetadataStore metadata, out int skipped) =>
        Fh2mMapWriter.Build(map, new MapSpecsState(map), metadata, out skipped);
}
