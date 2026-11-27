using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The MP2 town block read into <see cref="CastleMetadata"/>, the bridge the editor needs because its map is an MP2
/// file and the reference's <c>castleMetadata</c> table only exists in its own map format.
/// </summary>
public sealed class Mp2CastleMetadataTests
{
    private const int MapSize = 36;

    [Fact]
    public void TheOwnerByteNamesTheColour()
    {
        Assert.Equal(PlayerColor.Blue, ReadOwner(0));
        Assert.Equal(PlayerColor.Green, ReadOwner(1));
        Assert.Equal(PlayerColor.Red, ReadOwner(2));
        Assert.Equal(PlayerColor.Yellow, ReadOwner(3));
        Assert.Equal(PlayerColor.Orange, ReadOwner(4));
        Assert.Equal(PlayerColor.Purple, ReadOwner(5));
        Assert.Equal(PlayerColor.None, ReadOwner(6));
        Assert.Equal(PlayerColor.None, ReadOwner(0xFF));
    }

    [Fact]
    public void CustomBuildingsBecomeTheMaskTheWindowShows()
    {
        CastleMetadata metadata = Read(block => block.WithCustomBuildings(common: 0x0004 | 0x0010,
            dwellings: 0x0008 | 0x0200 | 0x2000, guildLevel: 3));

        Assert.True(metadata.CustomBuildings);

        BuildingType built = MapFormatArmy.GetBuiltBuildings(metadata);

        Assert.True(built.HasFlag(BuildingType.Tavern));
        Assert.True(built.HasFlag(BuildingType.Well));
        Assert.True(built.HasFlag(BuildingType.DwellingMonster1));
        Assert.True(built.HasFlag(BuildingType.DwellingMonster2));
        Assert.True(built.HasFlag(BuildingType.DwellingUpgrade6));
        Assert.True(built.HasFlag(BuildingType.MageGuild1));
        Assert.True(built.HasFlag(BuildingType.MageGuild2));
        Assert.True(built.HasFlag(BuildingType.MageGuild3));
        Assert.False(built.HasFlag(BuildingType.MageGuild4));
        Assert.False(built.HasFlag(BuildingType.ThievesGuild));
    }

    [Fact]
    public void AnUpgradeBitBringsItsBaseDwellingAlong()
    {
        CastleMetadata metadata = Read(block => block.WithCustomBuildings(common: 0, dwellings: 0x0200,
            guildLevel: 0));

        BuildingType built = MapFormatArmy.GetBuiltBuildings(metadata);

        // 参考的格式里「升级」不可能单独存在，所以 0x0200 那一支同时点上了二级住所本身。
        Assert.True(built.HasFlag(BuildingType.DwellingMonster2));
        Assert.True(built.HasFlag(BuildingType.DwellingUpgrade2));
        Assert.False(built.HasFlag(BuildingType.DwellingMonster1));

        Assert.Contains((uint)BuildingType.DwellingUpgrade2, metadata.BuiltBuildings);
        Assert.Contains((uint)BuildingType.DwellingMonster2, metadata.BuiltBuildings);
    }

    [Fact]
    public void TheDefaultBuildingsLeaveTheListEmpty()
    {
        CastleMetadata metadata = Read(block => block.WithDefaultBuildings());

        Assert.False(metadata.CustomBuildings);
        Assert.Empty(metadata.BuiltBuildings);
        Assert.Equal(BuildingType.Nothing, MapFormatArmy.GetBuiltBuildings(metadata));
    }

    [Fact]
    public void TheCustomGuardKeepsItsMonstersAndCounts()
    {
        CastleMetadata metadata = Read(block => block.WithCustomDefenders([0, 4, 6, 0x0A, 0x22], [10, 20, 30, 40, 50]));

        Assert.False(MapFormatArmy.IsDefaultCastleDefenderArmy(metadata));

        // 块里存的是 MonsterType 减一，于是 0 是农民、0x22 是农民之后的第三十四只。
        Assert.Equal((int)MonsterType.Peasant, metadata.DefenderMonsterType[0]);
        Assert.Equal((int)MonsterType.VeteranPikeman, metadata.DefenderMonsterType[1]);
        Assert.Equal((int)MonsterType.MasterSwordsman, metadata.DefenderMonsterType[2]);
        Assert.Equal(11, metadata.DefenderMonsterType[3]);
        Assert.Equal(35, metadata.DefenderMonsterType[4]);

        Assert.Equal([10, 20, 30, 40, 50], metadata.DefenderMonsterCount);
    }

    [Fact]
    public void TheDefaultGuardIsTheNegativeSentinel()
    {
        CastleMetadata metadata = Read(block => block.WithDefaultDefenders());

        Assert.True(MapFormatArmy.IsDefaultCastleDefenderArmy(metadata));
        Assert.All(metadata.DefenderMonsterType, type => Assert.Equal(-1, type));
        Assert.All(metadata.DefenderMonsterCount, count => Assert.Equal(0, count));
    }

    [Fact]
    public void TheCustomNameComesOutOfTheThirteenByteField()
    {
        CastleMetadata metadata = Read(block => block.WithName("Old Keep"));

        Assert.Equal("Old Keep", metadata.CustomName);
    }

    [Fact]
    public void NoCustomNameLeavesTheNameEmpty()
    {
        CastleMetadata metadata = Read(block => block.WithDefaultName());

        Assert.Equal(string.Empty, metadata.CustomName);
    }

    [Fact]
    public void BanningTheFortGoesIntoTheBannedList()
    {
        CastleMetadata banned = Read(block => block.WithBannedFort());
        CastleMetadata allowed = Read(block => block.WithDefaultName());

        Assert.Contains((uint)BuildingType.Castle, banned.BannedBuildings);
        Assert.Empty(allowed.BannedBuildings);
    }

    [Fact]
    public void AWrongBlockSizeIsRefused()
    {
        Assert.Throws<ArgumentException>(() =>
            Mp2CastleMetadata.ReadBlock(new byte[Mp2CastleMetadata.BlockSize - 1], out _));
    }

    [Fact]
    public void TheMapLookupFollowsTheBlockIdOfTheTile()
    {
        Mp2Map map = MapWithCastle(blockId: 1,
            [TownBlock(block => block.WithOwner(2).WithName("Old Keep"))]);

        Assert.True(Mp2CastleMetadata.TryRead(map, new Point(10, 10), out CastleMetadata metadata,
            out PlayerColor owner));

        Assert.Equal("Old Keep", metadata.CustomName);
        Assert.Equal(PlayerColor.Red, owner);
    }

    [Fact]
    public void ATileWithoutItsBlockIsNotACastleToEdit()
    {
        Mp2Map map = MapWithCastle(blockId: 2, [TownBlock(block => block.WithName("Old Keep"))]);

        Assert.False(Mp2CastleMetadata.TryRead(map, new Point(10, 10), out _, out _));
    }

    [Fact]
    public void APlainTileIsNotACastleToEdit()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        Assert.False(Mp2CastleMetadata.TryRead(map, new Point(3, 4), out _, out _));
        Assert.False(Mp2CastleMetadata.TryRead(map, new Point(-1, 4), out _, out _));
        Assert.False(Mp2CastleMetadata.TryRead(map, new Point(MapSize, 4), out _, out _));
    }

    [Fact]
    public void OnlyTheSixTownTypesCount()
    {
        Assert.True(Mp2CastleMetadata.IsTownTile(Mp2ObjectType.Castle));
        Assert.True(Mp2CastleMetadata.IsTownTile(Mp2ObjectType.ActionRandomTown));
        Assert.False(Mp2CastleMetadata.IsTownTile(Mp2ObjectType.Mine));
        Assert.False(Mp2CastleMetadata.IsTownTile(Mp2ObjectType.None));
    }

    private static PlayerColor ReadOwner(int owner)
    {
        _ = Mp2CastleMetadata.ReadBlock(TownBlock(block => block.WithOwner(owner)).Data, out PlayerColor color);

        return color;
    }

    private static CastleMetadata Read(Action<TownBlockBuilder> build) =>
        Mp2CastleMetadata.ReadBlock(TownBlock(build).Data, out _);

    /// <summary>
    /// A castle on (10, 10) whose block id is the one the tile's metadata names.
    /// </summary>
    /// <param name="blockId">The <c>ObjectId</c> the tile points at.</param>
    /// <param name="blocks">The blocks of the map.</param>
    private static Mp2Map MapWithCastle(int blockId, Mp2InfoBlock[] blocks)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        Mp2TileInfo tile = map.Tile(10, 10) with
        {
            ObjectType = Mp2ObjectType.Castle,

            // The block id lives in quantity1/quantity2 three bits up: 8 is id 1, 16 is id 2.
            Quantity1 = (byte)(blockId << 3),
        };

        map.SetTile(10, 10, tile);

        return map with { InfoBlocks = blocks };
    }

    private static Mp2InfoBlock TownBlock(Action<TownBlockBuilder> build)
    {
        TownBlockBuilder builder = new();

        build(builder);

        return new Mp2InfoBlock(0, builder.ToArray());
    }

    /// <summary>Writes a 70-byte town block field by field, the order of <c>Castle::LoadFromMP2()</c>.</summary>
    private sealed class TownBlockBuilder
    {
        private readonly byte[] _data = new byte[Mp2CastleMetadata.BlockSize];

        public TownBlockBuilder WithOwner(int owner) => Set(0, owner);

        public TownBlockBuilder WithCustomBuildings(ushort common, ushort dwellings, int guildLevel)
        {
            _data[1] = 1;
            _data[2] = (byte)common;
            _data[3] = (byte)(common >> 8);
            _data[4] = (byte)dwellings;
            _data[5] = (byte)(dwellings >> 8);
            _data[6] = (byte)guildLevel;

            return this;
        }

        public TownBlockBuilder WithDefaultBuildings() => this;

        public TownBlockBuilder WithCustomDefenders(int[] monsters, int[] counts)
        {
            _data[7] = 1;

            for (int slot = 0; slot < 5; ++slot)
            {
                _data[8 + slot] = (byte)monsters[slot];

                int offset = 13 + (slot * 2);

                _data[offset] = (byte)counts[slot];
                _data[offset + 1] = (byte)(counts[slot] >> 8);
            }

            return this;
        }

        public TownBlockBuilder WithDefaultDefenders() => this;

        public TownBlockBuilder WithName(string name)
        {
            _data[24] = 1;

            byte[] encoded = MapTextCodec.Encode(name, Mp2CastleMetadata.NameFieldLength);

            for (int index = 0; index < encoded.Length; ++index)
            {
                _data[25 + index] = encoded[index];
            }

            return this;
        }

        public TownBlockBuilder WithDefaultName() => this;

        public TownBlockBuilder WithBannedFort()
        {
            _data[40] = 1;

            return this;
        }

        public byte[] ToArray() => _data;

        private TownBlockBuilder Set(int offset, int value)
        {
            _data[offset] = (byte)value;

            return this;
        }
    }
}
