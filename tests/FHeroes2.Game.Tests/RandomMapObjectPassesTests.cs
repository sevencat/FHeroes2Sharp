using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The object phase of <c>generateMap()</c>: steps 5 to 11 in the order of the reference
/// (<c>map_random_generator.cpp:751-957</c>).
/// </summary>
public sealed class RandomMapObjectPassesTests : IDisposable
{
    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(), $"fh2-random-objects-{Guid.NewGuid():N}.mp2");

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    [Fact]
    public void AGeneratedMapCarriesMinesTreasuresAndDecorations()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 424242, PlayerCount = 2 });

        Assert.True(CountOfType(map, Mp2ObjectType.Sawmill, Mp2ObjectType.Mine, Mp2ObjectType.AbandonedMine) > 0,
            "a generated map has no mines");
        Assert.True(CountOfType(map, Mp2ObjectType.Trees, Mp2ObjectType.Mountains) > 0,
            "a generated map has no decorations");
        Assert.True(
            CountOfType(
                map,
                Mp2ObjectType.TreasureChest,
                Mp2ObjectType.ActionTreasureChest,
                Mp2ObjectType.Artifact,
                Mp2ObjectType.ActionArtifact,
                Mp2ObjectType.Campfire,
                Mp2ObjectType.ActionCampfire) > 0,
            "a generated map has no treasures");
        Assert.True(
            CountOfType(map, Mp2ObjectType.Monster, Mp2ObjectType.ActionMonster, Mp2ObjectType.ActionRandomMonster) > 0,
            "a generated map has no guards");
        Assert.True(
            CountOfType(map, Mp2ObjectType.ActionRandomResource, Mp2ObjectType.RandomResource) > 0,
            "a generated map has no free pickups");
    }

    [Fact]
    public void TheSameSeedGivesTheSameObjects()
    {
        Mp2Map first = Generate(new RandomMapConfig { Seed = 777 });
        Mp2Map second = Generate(new RandomMapConfig { Seed = 777 });

        Assert.Equal(first.Castles.Count, second.Castles.Count);
        Assert.Equal(first.InfoBlocks.Count, second.InfoBlocks.Count);
        Assert.Equal(first.LastObjectUid, second.LastObjectUid);

        for (int index = 0; index < first.TileCount; ++index)
        {
            Mp2TileInfo left = first.Tiles[index];
            Mp2TileInfo right = second.Tiles[index];

            Assert.Equal(left.ObjectType, right.ObjectType);
            Assert.Equal(left.ObjectName1, right.ObjectName1);
            Assert.Equal(left.BottomIcnImageIndex, right.BottomIcnImageIndex);
            Assert.Equal(left.ObjectName2, right.ObjectName2);
            Assert.Equal(left.TopIcnImageIndex, right.TopIcnImageIndex);
            Assert.Equal(left.Metadata, right.Metadata);
        }
    }

    [Fact]
    public void AGeneratedMapSurvivesAFileRoundTripWithItsObjects()
    {
        Mp2Map generated = Generate(new RandomMapConfig { Seed = 20261126 });

        Mp2MapWriter.WriteAll(_filePath, generated);
        Mp2Map loaded = Mp2Map.Read(_filePath);

        Assert.Equal(generated.TileCount, loaded.TileCount);
        Assert.Equal(generated.Addons.Count, loaded.Addons.Count);
        Assert.Equal(generated.LastObjectUid, loaded.LastObjectUid);

        for (int index = 0; index < generated.TileCount; ++index)
        {
            Assert.Equal(generated.Tiles[index].ObjectType, loaded.Tiles[index].ObjectType);
            Assert.Equal(generated.Tiles[index].BottomIcnImageIndex, loaded.Tiles[index].BottomIcnImageIndex);
        }
    }

    [Fact]
    public void EveryTileHoldsAtMostOneActionObject()
    {
        // 动作位决定玩家能不能站上去，一格两件可交互物件会让读图时「谁在那儿」失去意义。
        Mp2Map map = Generate(new RandomMapConfig { Seed = 4711 });

        foreach (Mp2TileInfo tile in map.Tiles)
        {
            if (!Mp2ObjectTypes.IsActionObject(tile.ObjectType))
            {
                continue;
            }

            int actionPieces = 0;

            if (Mp2ObjectTypes.IsActionObject(tile.GroundIcnType == Mp2ObjectIcnType.Unknown
                    ? Mp2ObjectType.None
                    : MapObjectCatalog.GetObjectTypeByIcn(tile.GroundIcnType, tile.BottomIcnImageIndex)))
            {
                ++actionPieces;
            }

            Assert.True(actionPieces <= 1, $"tile {tile.ObjectType} holds two action pieces");
        }
    }

    [Fact]
    public void ASmallMapIsGeneratedToo()
    {
        // 36×36 是守卫强度分档的那一档，也是区域最少的一档：物件阶段不能因为地方不够就崩掉。
        Mp2Map map = Generate(new RandomMapConfig { Seed = 36 }, size: 36);

        Assert.Equal(36, map.Width);
        Assert.True(CountOfType(map, Mp2ObjectType.Trees, Mp2ObjectType.Mountains) > 0);
    }

    private static Mp2Map Generate(RandomMapConfig config, int size = 72) =>
        RandomMapGenerator.Generate(config, size, size)
        ?? throw new InvalidOperationException("The map was not generated.");

    private static int CountOfType(Mp2Map map, params Mp2ObjectType[] types) =>
        map.Tiles.Count(tile => types.Contains(tile.ObjectType));
}
