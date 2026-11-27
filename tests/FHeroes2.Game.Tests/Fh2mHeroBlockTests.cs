using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="Mp2HeroInfo.ToBlock"/> and the hero blocks <see cref="Fh2mMapReader"/> builds out of the hero
/// metadata of a file (<c>world_loadmap.cpp:579-596</c>).
/// </summary>
public sealed class Fh2mHeroBlockTests
{
    private const int MapSize = 36;

    private const int HeroTile = (MapSize * MapSize / 2) + 5;

    [Fact]
    public void TheBlockIsSeventySixBytes()
    {
        Assert.Equal(Mp2HeroInfo.StructureSize, new Mp2HeroInfo().ToBlock().Length);
    }

    [Fact]
    public void EveryFieldSurvivesABlockRoundTrip()
    {
        Mp2HeroInfo source = new()
        {
            HasCustomArmy = true,
            MonsterIds = [3, 0, 12, 0, 0],
            MonsterCounts = [10, 0, 250, 0, 0],
            HasCustomPortrait = true,
            PortraitId = 7,
            ArtifactIds = [1, 0, 5],
            Experience = 12345,
            HasCustomSecondarySkills = true,
            SecondarySkillIds = [2, 0, 0, 0, 0, 0, 0, 0],
            SecondarySkillLevels = [3, 0, 0, 0, 0, 0, 0, 0],
            HasCustomName = true,
            Name = "Hero",
            IsOnPatrol = true,
            PatrolDistanceOrJailRace = 4,
        };

        byte[] block = source.ToBlock();

        Assert.True(Mp2HeroInfo.TryRead(block, out Mp2HeroInfo read));

        Assert.True(read.HasCustomArmy);
        Assert.Equal([3, 0, 12, 0, 0], read.MonsterIds);
        Assert.Equal([10, 0, 250, 0, 0], read.MonsterCounts);
        Assert.True(read.HasCustomPortrait);
        Assert.Equal(7, read.PortraitId);
        Assert.Equal([1, 0, 5], read.ArtifactIds);
        Assert.Equal(12345u, read.Experience);
        Assert.True(read.HasCustomSecondarySkills);
        Assert.Equal(2, read.SecondarySkillIds[0]);
        Assert.Equal(3, read.SecondarySkillLevels[0]);
        Assert.True(read.HasCustomName);
        Assert.Equal("Hero", read.Name);
        Assert.True(read.IsOnPatrol);
        Assert.Equal(4, read.PatrolDistanceOrJailRace);
    }

    [Fact]
    public void AnEmptyBlockIsAllZeros()
    {
        byte[] block = new Mp2HeroInfo().ToBlock();

        Assert.All(block, value => Assert.Equal(0, value));

        // 读端仍然认得它：一块「什么都没有」的英雄块是合法的。
        Assert.True(Mp2HeroInfo.TryRead(block, out Mp2HeroInfo read));
        Assert.False(read.HasCustomArmy);
        Assert.False(read.HasCustomName);
        Assert.Equal(string.Empty, read.Name);
    }

    [Fact]
    public void TheBytesNobodyReadsStayZero()
    {
        Mp2HeroInfo source = new()
        {
            HasCustomArmy = true,
            MonsterIds = [1, 2, 3, 4, 5],
            MonsterCounts = [1, 2, 3, 4, 5],
            ArtifactIds = [1, 2, 3],
            Experience = 999,
            SecondarySkillIds = [1, 2, 3, 4, 5, 6, 7, 8],
            SecondarySkillLevels = [1, 2, 3, 4, 5, 6, 7, 8],
        };

        byte[] block = source.ToBlock();

        // 读端明确忽略的三处：0、22、44。
        Assert.Equal(0, block[0]);
        Assert.Equal(0, block[22]);
        Assert.Equal(0, block[44]);

        // 尾部 15 字节。
        for (int offset = Mp2HeroInfo.StructureSize - 15; offset < Mp2HeroInfo.StructureSize; ++offset)
        {
            Assert.Equal(0, block[offset]);
        }
    }

    [Fact]
    public void AHeroTileGetsABlockAndRefersToIt()
    {
        var metadata = new MapObjectMetadataStore();

        metadata.Heroes[77] = new HeroMetadata
        {
            CustomName = "Aurora",
            ArmyMonsterType = [2, 0, 0, 0, 0],
            ArmyMonsterCount = [30, 0, 0, 0, 0],
            CustomExperience = 5000,
            IsOnPatrol = true,
            PatrolRadius = 6,
        };

        Mp2Map read = RoundTrip(HeroMap(), metadata);

        Mp2InfoBlock block = Assert.Single(read.InfoBlocks);

        Assert.Equal(Mp2HeroInfo.StructureSize, block.Data.Length);
        Assert.True(Mp2HeroInfo.TryRead(block.Data, out Mp2HeroInfo info));
        Assert.Equal("Aurora", info.Name);
        Assert.Equal(5000u, info.Experience);
        Assert.True(info.IsOnPatrol);
        Assert.Equal(6, info.PatrolDistanceOrJailRace);

        // 元数据里怪物编号从 1 起，块里从 0 起。
        Assert.Equal(1, info.MonsterIds[0]);
        Assert.Equal(30, info.MonsterCounts[0]);

        // 瓦片引用这块：id = 下标 + 1，拆回那两个字节。
        Mp2TileInfo tile = read.Tiles[HeroTile];

        Assert.Equal(block.ObjectId, (int)tile.Metadata);
    }

    [Fact]
    public void AHeroWithoutMetadataStillGetsAnEmptyBlock()
    {
        Mp2Map read = RoundTrip(HeroMap(), new MapObjectMetadataStore());

        Mp2InfoBlock block = Assert.Single(read.InfoBlocks);

        Assert.All(block.Data, value => Assert.Equal(0, value));
        Assert.Equal(block.ObjectId, (int)read.Tiles[HeroTile].Metadata);
    }

    [Fact]
    public void AMapWithoutHeroesHasNoBlocks()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        Mp2Map read = RoundTrip(map, new MapObjectMetadataStore());

        Assert.Empty(read.InfoBlocks);
    }

    /// <summary>A map whose middle tile carries a hero, drawn with the first <c>MINIHERO</c> frame.</summary>
    /// <returns>The map.</returns>
    private static Mp2Map HeroMap()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTile, map.Tiles[HeroTile] with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
            BottomIcnImageIndex = 0,
            ObjectType = Mp2ObjectType.ActionHero,
            Level1ObjectUid = 77,
        });

        return map;
    }

    /// <summary>Writes the map with the given metadata and reads it back.</summary>
    /// <param name="source">The map to write.</param>
    /// <param name="metadata">The metadata tables to write.</param>
    /// <returns>The map that came back.</returns>
    private static Mp2Map RoundTrip(Mp2Map source, MapObjectMetadataStore metadata)
    {
        Fh2mBaseMap header = Fh2mMapWriter.Build(source, new MapSpecsState(source), metadata, out _);

        return Fh2mMapReader.Build(header);
    }
}
