using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="Fh2mMapReader"/>: an <c>.fh2m</c> file turned back into the editor's map, the mirror of
/// <see cref="Fh2mMapWriter"/>.
/// </summary>
public sealed class Fh2mMapReaderTests
{
    private const int MapSize = 36;

    /// <summary>Index of a tile near the middle, far from the edges.</summary>
    private const int MiddleTile = (MapSize * MapSize / 2) + 5;

    [Fact]
    public void AnEmptyMapComesBackWithItsTiles()
    {
        Mp2Map source = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        Mp2Map read = ReadBack(source, out int skipped);

        Assert.Equal(0, skipped);
        Assert.Equal(source.Tiles.Count, read.Tiles.Count);
        Assert.Equal(MapSize, read.Width);
        Assert.Equal(MapSize, read.Height);
        Assert.Equal(MapGrounds.GrassStartImageIndex, read.Tiles[MiddleTile].TerrainImageIndex);
        Assert.Equal(Mp2ObjectType.None, read.Tiles[MiddleTile].ObjectType);
    }

    [Fact]
    public void AObjectComesBackAsItsIcnAndFrame()
    {
        Mp2Map source = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        source.SetTileAt(MiddleTile, source.Tiles[MiddleTile] with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.Mons32 << 2),
            BottomIcnImageIndex = 3,
            ObjectType = Mp2ObjectType.ActionMonster,
            Level1ObjectUid = 42,
        });

        Mp2Map read = ReadBack(source, out int skipped);

        Assert.Equal(0, skipped);

        Mp2TileInfo tile = read.Tiles[MiddleTile];

        Assert.Equal(Mp2ObjectIcnType.Mons32, tile.GroundIcnType);
        Assert.Equal(3, tile.BottomIcnImageIndex);
        Assert.Equal(42u, tile.Level1ObjectUid);
        Assert.Equal(Mp2ObjectType.ActionMonster, tile.ObjectType);
        Assert.True(tile.HasGroundObject);
    }

    [Fact]
    public void TheHeaderOfTheFileBecomesTheHeaderOfTheMap()
    {
        Mp2Map source = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        var specs = new MapSpecsState(source);

        specs.Name = "Read back";
        specs.Description = "A description";
        specs.CreatorNotes = "notes";
        specs.Difficulty = GameDifficulty.Hard;
        specs.VictoryCondition = MapVictoryCondition.CollectGold;
        specs.VictoryGoldInThousands = 12;
        specs.LossCondition = MapLossCondition.OutOfTime;
        specs.LossDays = 99;

        Mp2Map read = ReadBack(source, specs, out _);

        Assert.Equal("Read back", read.Info.Name);
        Assert.Equal("A description", read.Info.Description);
        Assert.Equal("notes", read.Info.CreatorNotes);
        Assert.Equal(GameDifficulty.Hard, read.Info.Difficulty);
        Assert.Equal(MapVictoryCondition.CollectGold, read.Info.VictoryCondition);

        // 编辑器把「千金」这个数字原样写进头部参数，所以读回来还是 12。
        Assert.Equal(12, read.Info.VictoryConditionParameters[0]);
        Assert.Equal(MapLossCondition.OutOfTime, read.Info.LossCondition);
        Assert.Equal(99, read.Info.LossConditionParameters[0]);
    }

    [Fact]
    public void AMapThatClaimsTheWrongNumberOfTilesIsRefused()
    {
        // 头部说 36 宽，尾段里却只有一格。
        Fh2mBaseMap header = new()
        {
            Width = 36,
            Payload = new Fh2mPayload { Tiles = [new Fh2mTile(0, 0, [])] }.Write(),
        };

        Assert.Throws<InvalidDataException>(() => Fh2mMapReader.Build(header));
    }

    [Fact]
    public void AnObjectAddressThatDoesNotExistIsSkipped()
    {
        // 组 200 不存在：写进去也不会崩，读回来那一格是空的。
        Fh2mBaseMap header = new()
        {
            Width = 36,
            Payload = new Fh2mPayload
            {
                Tiles = [.. Enumerable.Repeat(new Fh2mTile(0, 0, [new Fh2mTileObject(1, 200, 0)]), 36 * 36)],
            }.Write(),
        };

        Mp2Map read = Fh2mMapReader.Build(header);

        Assert.Equal(Mp2ObjectType.None, read.Tiles[0].ObjectType);
        Assert.False(read.Tiles[0].HasGroundObject);
    }

    [Fact]
    public void TheRoundTripKeepsTwoObjectsOfATile()
    {
        Mp2Map source = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        // 地面放一只怪物、顶层放一枚英雄：两层都要回来。
        source.SetTileAt(MiddleTile, source.Tiles[MiddleTile] with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.Mons32 << 2),
            BottomIcnImageIndex = 5,
            ObjectName2 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
            TopIcnImageIndex = 9,
            ObjectType = Mp2ObjectType.ActionMonster,
            Level1ObjectUid = 1,
            Level2ObjectUid = 2,
        });

        Mp2Map read = ReadBack(source, out int skipped);

        Assert.Equal(0, skipped);

        Mp2TileInfo tile = read.Tiles[MiddleTile];

        Assert.Equal(Mp2ObjectIcnType.Mons32, tile.GroundIcnType);
        Assert.Equal(5, tile.BottomIcnImageIndex);
        Assert.Equal(Mp2ObjectIcnType.MiniHero, tile.TopIcnType);
        Assert.Equal(9, tile.TopIcnImageIndex);
        Assert.True(tile.HasTopObject);
    }

    /// <summary>Writes the map with <see cref="Fh2mMapWriter"/> and reads it back.</summary>
    /// <param name="source">The map to write.</param>
    /// <param name="skipped">Objects the writer could not address.</param>
    /// <returns>The map that came back.</returns>
    private static Mp2Map ReadBack(Mp2Map source, out int skipped) =>
        ReadBack(source, new MapSpecsState(source), out skipped);

    /// <inheritdoc cref="ReadBack(Mp2Map, out int)"/>
    /// <param name="source">The map to write.</param>
    /// <param name="specs">The specifications to write.</param>
    /// <param name="skipped">Objects the writer could not address.</param>
    private static Mp2Map ReadBack(Mp2Map source, MapSpecsState specs, out int skipped)
    {
        Fh2mBaseMap header = Fh2mMapWriter.Build(source, specs, new MapObjectMetadataStore(), out skipped);

        return Fh2mMapReader.Build(header);
    }
}
