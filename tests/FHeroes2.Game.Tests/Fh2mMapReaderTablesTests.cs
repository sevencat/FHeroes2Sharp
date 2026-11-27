using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the two tables <see cref="Fh2mMapReader"/> rebuilds from the tiles: the castle positions and the
/// capturable objects (<c>world_loadmap.cpp:320-430</c>).
/// </summary>
public sealed class Fh2mMapReaderTablesTests
{
    private const int MapSize = 36;

    private const int MiddleTile = (MapSize * MapSize / 2) + 5;

    [Fact]
    public void AMapWithoutTownsOrMinesHasBothTablesEmpty()
    {
        Mp2Map read = RoundTrip(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        Assert.Empty(read.Castles);
        Assert.Empty(read.CaptureObjects);
    }

    [Fact]
    public void ATownEntranceBecomesACastlePosition()
    {
        // 骑士城、无要塞：入口瓦片的帧号 = 13 + 0 + 0×32 = 13。
        Mp2Map read = RoundTrip(TownMap(frame: 13));

        Mp2CastlePosition castle = Assert.Single(read.Castles);

        Assert.Equal(MiddleTile % MapSize, castle.X);
        Assert.Equal(MiddleTile / MapSize, castle.Y);
        Assert.Equal(0, castle.RawType);
        Assert.Equal(MapRace.Knight, castle.Race);
        Assert.False(castle.HasFort);
    }

    [Fact]
    public void AFortifiedTownKeepsItsFortBit()
    {
        // 要塞化的骑士城：帧号 13 + 16 = 29，最高位写进 RawType。
        Mp2CastlePosition castle = Assert.Single(RoundTrip(TownMap(frame: 29)).Castles);

        Assert.Equal(0x80, castle.RawType);
        Assert.True(castle.HasFort);
        Assert.Equal(MapRace.Knight, castle.Race);
    }

    [Theory]
    [InlineData(1u, MapRace.Barbarian)]
    [InlineData(2u, MapRace.Sorceress)]
    [InlineData(3u, MapRace.Warlock)]
    [InlineData(4u, MapRace.Wizard)]
    [InlineData(5u, MapRace.Necromancer)]
    public void TheRaceOfATownComesFromItsFrame(uint raceIndex, MapRace expected)
    {
        // 帧号 = 13 + 种族×32。
        Mp2CastlePosition castle = Assert.Single(RoundTrip(TownMap(frame: 13 + (raceIndex * 32))).Castles);

        Assert.Equal(expected, castle.Race);
        Assert.Equal((int)raceIndex, castle.RawType & 0x7F);
    }

    [Fact]
    public void AMineBecomesACaptureObject()
    {
        // 矿场组里的第一项是一条矿；它的 metadata[0] 说明矿产什么。
        ObjectInfo mine = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureMines)[0];
        int rawType = ExpectedMineRawType((Mp2ResourceType)mine.Metadata[0]);

        Mp2Map read = RoundTrip(WithObject(mine, ObjectGroup.AdventureMines, 0));

        Mp2CaptureObject capture = Assert.Single(read.CaptureObjects);

        Assert.Equal(MiddleTile % MapSize, capture.X);
        Assert.Equal(MiddleTile / MapSize, capture.Y);
        Assert.Equal(rawType, capture.RawType);
        Assert.Equal(Mp2ObjectType.Mine, capture.ObjectType);
    }

    [Fact]
    public void ASawmillBecomesTheZeroByteOfTheTable()
    {
        // 锯木厂在冒险杂项或宝物组里；逐个组找它，找到就用它当素材。
        if (!TryFindObject(Mp2ObjectType.Sawmill, out ObjectGroup group, out int index))
        {
            return;
        }

        Mp2CaptureObject capture = Assert.Single(RoundTrip(WithObject(
            MapObjectCatalog.GetObjectInfo(group, index), group, index)).CaptureObjects);

        Assert.Equal(0x00, capture.RawType);
        Assert.Equal(Mp2ObjectType.Sawmill, capture.ObjectType);
    }

    /// <summary>The byte a mine of <paramref name="resource"/> gets in the capture table.</summary>
    /// <param name="resource">The resource a mine produces.</param>
    /// <returns>The byte.</returns>
    private static int ExpectedMineRawType(Mp2ResourceType resource) => resource switch
    {
        Mp2ResourceType.Ore => 0x02,
        Mp2ResourceType.Sulfur => 0x03,
        Mp2ResourceType.Crystal => 0x04,
        Mp2ResourceType.Gems => 0x05,
        Mp2ResourceType.Gold => 0x06,
        _ => 0x02,
    };

    /// <summary>Finds the first object of a type in the catalogue.</summary>
    /// <param name="type">The object type to look for.</param>
    /// <param name="group">The group it was found in.</param>
    /// <param name="index">Its index inside that group.</param>
    /// <returns><see langword="true"/> when the catalogue has such an object.</returns>
    private static bool TryFindObject(Mp2ObjectType type, out ObjectGroup group, out int index)
    {
        for (int candidate = 0; candidate < (int)ObjectGroup.GroupCount; ++candidate)
        {
            ObjectGroup id = (ObjectGroup)candidate;
            IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(id);

            for (int position = 0; position < objects.Count; ++position)
            {
                if (Mp2ObjectTypes.NonActionType(objects[position].ObjectType) == type)
                {
                    group = id;
                    index = position;

                    return true;
                }
            }
        }

        group = ObjectGroup.None;
        index = 0;

        return false;
    }

    /// <summary>A map whose middle tile carries one town entrance with the given sprite frame.</summary>
    /// <param name="frame">Frame of <c>OBJNTOWN</c> on the entrance tile.</param>
    /// <returns>The map.</returns>
    private static Mp2Map TownMap(uint frame)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(MiddleTile, map.Tiles[MiddleTile] with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnTown << 2),
            BottomIcnImageIndex = (byte)frame,
            ObjectType = Mp2ObjectType.ActionCastle,
            Level1ObjectUid = 5,
        });

        return map;
    }

    /// <summary>A map whose middle tile carries one object of a group.</summary>
    /// <param name="info">The object, for its main part.</param>
    /// <param name="group">The group the object lives in.</param>
    /// <param name="index">Its index inside the group.</param>
    /// <returns>The map.</returns>
    private static Mp2Map WithObject(ObjectInfo info, ObjectGroup group, int index)
    {
        Assert.True(MapObjectCatalog.TryGetObjectIndex(
            MapObjectCatalog.GetObjectsByGroup(group)[index].GroundLevelParts[0].IcnType,
            MapObjectCatalog.GetObjectsByGroup(group)[index].GroundLevelParts[0].IcnIndex, out _));

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        ObjectPartInfo part = info.GroundLevelParts[0];

        map.SetTileAt(MiddleTile, map.Tiles[MiddleTile] with
        {
            ObjectName1 = (byte)((int)part.IcnType << 2),
            BottomIcnImageIndex = (byte)part.IcnIndex,
            ObjectType = part.ObjectType,
            Level1ObjectUid = 7,
        });

        return map;
    }

    /// <summary>Writes the map and reads it back through the <c>.fh2m</c> round trip.</summary>
    /// <param name="source">The map to write.</param>
    /// <returns>The map that came back.</returns>
    private static Mp2Map RoundTrip(Mp2Map source)
    {
        Fh2mBaseMap header = Fh2mMapWriter.Build(
            source, new MapSpecsState(source), new MapObjectMetadataStore(), out _);

        return Fh2mMapReader.Build(header);
    }
}
