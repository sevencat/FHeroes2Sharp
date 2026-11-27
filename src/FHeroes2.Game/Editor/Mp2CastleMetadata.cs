using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;

namespace FHeroes2.Game.Editor;

/// <summary>
/// Reads the per-castle metadata the editor window edits out of an MP2 town block, the bridge that the reference does
/// not need because its editor works on <c>Maps::Map_Format</c> where <c>castleMetadata</c> is already filled in
/// (<c>editor_interface.cpp:2234-2246</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的编辑器读的是它自己的地图格式；端口的地图是 MP2，只有那 70 字节的信息块。这里按
/// <c>Castle::LoadFromMP2()</c> 的字节顺序（<c>castle.cpp:102-395</c>）把块读成
/// <see cref="CastleMetadata"/>，并且<b>不</b>做装载时会做的那些事情：默认建筑不掷骰、魔法行会不初始化、
/// 中立守军不加强。编辑器要的是作者写下去的原样。
/// </para>
/// <para>
/// 块的布局：1 字节归属色；1 字节「自定义建筑」标志，随后是 2 字节公共建筑位、2 字节住所位、1 字节魔法行会等级
/// （标志为 0 时这 5 字节跳过）；1 字节「自定义守军」标志，随后 5 只怪各 1 字节、5 个数量各 2 字节（标志为 0 时跳过
/// 15 字节）；1 字节指挥官、1 字节「自定义名」标志、13 字节名字、1 字节种族、1 字节「有要塞」、1 字节「禁止建要塞」，
/// 最后 29 字节参考从不读。
/// </para>
/// <para>
/// <b>没有自定义建筑时的写法</b>：<see cref="CastleMetadata.BuiltBuildings"/> 留空、<see cref="CastleMetadata.CustomBuildings"/>
/// 为假，与参考「作者没挑过建筑」的表示一致；城堡详情窗打开时会按种族铺默认值。
/// </para>
/// </remarks>
public static class Mp2CastleMetadata
{
    /// <summary>The size of a town information block, <c>Castle::Mp2BlockSize</c>.</summary>
    public const int BlockSize = 70;

    /// <summary>Bytes of the town name in the block, <c>data.Slice( offset, 13 )</c>.</summary>
    public const int NameFieldLength = 13;

    /// <summary>Bytes of the block the reference never reads, the tail of the structure.</summary>
    public const int UnusedTailLength = 29;

    /// <summary>
    /// Reads the metadata of the castle whose gate stands on a tile, plus the owner colour the block names.
    /// </summary>
    /// <remarks>
    /// 块的定位与 <c>World.FindInfoBlockTile</c> 同一个规则：地块的 <see cref="Mp2TileInfo.Metadata"/> 就是块的
    /// <see cref="Mp2InfoBlock.ObjectId"/>。找不到块、块的尺寸不对、或者那一格不是城堡时返回
    /// <see langword="false"/>——调用方照参考「表里一定有条目」的做法自己补一份空元数据。
    /// </remarks>
    /// <param name="map">The map being edited.</param>
    /// <param name="tile">The tile of the castle's gate, in map tiles.</param>
    /// <param name="metadata">The metadata the window edits.</param>
    /// <param name="owner">The owner colour, <see cref="PlayerColor.None"/> for an unclaimed town.</param>
    /// <returns><see langword="true"/> when the tile has a readable town block.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static bool TryRead(Mp2Map map, Point tile, out CastleMetadata metadata, out PlayerColor owner)
    {
        ArgumentNullException.ThrowIfNull(map);

        metadata = new CastleMetadata();
        owner = PlayerColor.None;

        if (tile.X < 0 || tile.X >= map.Width || tile.Y < 0 || tile.Y >= map.Height)
        {
            return false;
        }

        Mp2TileInfo info = map.Tile(tile.X, tile.Y);

        if (!IsTownTile(info.ObjectType))
        {
            return false;
        }

        uint blockId = info.Metadata;

        foreach (Mp2InfoBlock block in map.InfoBlocks)
        {
            if (block.ObjectId == (int)blockId && block.Data.Length == BlockSize)
            {
                metadata = ReadBlock(block.Data, out owner);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a 70-byte town block into the metadata table, <see cref="CastleMetadata"/> of
    /// <c>Maps::Map_Format</c>.
    /// </summary>
    /// <param name="block">The block, exactly <see cref="BlockSize"/> bytes.</param>
    /// <param name="owner">The owner colour, <see cref="PlayerColor.None"/> for an unclaimed town.</param>
    /// <returns>The metadata the window edits.</returns>
    /// <exception cref="ArgumentException"><paramref name="block"/> is not <see cref="BlockSize"/> bytes.</exception>
    public static CastleMetadata ReadBlock(ReadOnlySpan<byte> block, out PlayerColor owner)
    {
        if (block.Length != BlockSize)
        {
            throw new ArgumentException($"A town block is {BlockSize} bytes, not {block.Length}.", nameof(block));
        }

        CastleMetadata metadata = new();
        int offset = 0;

        owner = block[offset++] switch
        {
            0 => PlayerColor.Blue,
            1 => PlayerColor.Green,
            2 => PlayerColor.Red,
            3 => PlayerColor.Yellow,
            4 => PlayerColor.Orange,
            5 => PlayerColor.Purple,
            _ => PlayerColor.None,
        };

        if (block[offset++] != 0)
        {
            metadata.CustomBuildings = true;

            ushort common = ReadUInt16(block, ref offset);
            ushort dwellings = ReadUInt16(block, ref offset);
            int guildLevel = block[offset++];

            AddBuildings(metadata, CommonBuildings(common));
            AddBuildings(metadata, DwellingBuildings(dwellings));
            AddBuildings(metadata, GuildBuildings(guildLevel));
        }
        else
        {
            offset += 5;
        }

        if (block[offset++] != 0)
        {
            for (int slot = 0; slot < metadata.DefenderMonsterType.Length; ++slot)
            {
                // The format stores the type one less than MonsterType, because zero means "none".
                metadata.DefenderMonsterType[slot] = block[offset + slot] + 1;
            }

            offset += metadata.DefenderMonsterType.Length;

            for (int slot = 0; slot < metadata.DefenderMonsterCount.Length; ++slot)
            {
                metadata.DefenderMonsterCount[slot] = ReadUInt16(block, ref offset);
            }
        }
        else
        {
            offset += 15;
            MapFormatArmy.SetDefaultCastleDefenderArmy(metadata);
        }

        // The captain, the "custom name" flag, the name itself, the race, the fort and the banning of the fort: only
        // the name is metadata, the rest lives on the object (the race and the fort are the sprite's business).
        offset += 1;

        if (block[offset++] != 0)
        {
            metadata.CustomName = MapTextCodec.Decode(block.Slice(offset, NameFieldLength));
        }

        offset += NameFieldLength + 1 + 1;

        if (block[offset] != 0)
        {
            metadata.BannedBuildings.Add((uint)BuildingType.Castle);
        }

        return metadata;
    }

    /// <summary>Whether a tile's object type is a town or a random town.</summary>
    /// <param name="type">The object type of the tile.</param>
    /// <returns><see langword="true"/> for the six town types of the file.</returns>
    public static bool IsTownTile(Mp2ObjectType type) => type is
        Mp2ObjectType.Castle or Mp2ObjectType.ActionCastle or
        Mp2ObjectType.RandomCastle or Mp2ObjectType.ActionRandomCastle or
        Mp2ObjectType.RandomTown or Mp2ObjectType.ActionRandomTown;

    /// <summary>The buildings of the common bit table, <c>castle.cpp:1381-1438</c>.</summary>
    /// <param name="bits">The two bytes the block holds.</param>
    /// <returns>The mask those bits stand for.</returns>
    private static BuildingType CommonBuildings(ushort bits)
    {
        BuildingType buildings = BuildingType.Nothing;

        if ((bits & 0x0002) != 0)
        {
            buildings |= BuildingType.ThievesGuild;
        }

        if ((bits & 0x0004) != 0)
        {
            buildings |= BuildingType.Tavern;
        }

        if ((bits & 0x0008) != 0)
        {
            buildings |= BuildingType.Shipyard;
        }

        if ((bits & 0x0010) != 0)
        {
            buildings |= BuildingType.Well;
        }

        if ((bits & 0x0080) != 0)
        {
            buildings |= BuildingType.Statue;
        }

        if ((bits & 0x0100) != 0)
        {
            buildings |= BuildingType.LeftTurret;
        }

        if ((bits & 0x0200) != 0)
        {
            buildings |= BuildingType.RightTurret;
        }

        if ((bits & 0x0400) != 0)
        {
            buildings |= BuildingType.Marketplace;
        }

        if ((bits & 0x0800) != 0)
        {
            buildings |= BuildingType.Well2;
        }

        if ((bits & 0x1000) != 0)
        {
            buildings |= BuildingType.Moat;
        }

        if ((bits & 0x2000) != 0)
        {
            buildings |= BuildingType.Spec;
        }

        return buildings;
    }

    /// <summary>
    /// The dwellings of the second bit table, <c>castle.cpp:1440-1497</c>: an upgrade bit always brings its base
    /// dwelling along, because the format cannot say "upgraded" alone.
    /// </summary>
    /// <param name="bits">The two bytes the block holds.</param>
    /// <returns>The mask those bits stand for.</returns>
    private static BuildingType DwellingBuildings(ushort bits)
    {
        BuildingType buildings = BuildingType.Nothing;

        if ((bits & 0x0008) != 0)
        {
            buildings |= BuildingType.DwellingMonster1;
        }

        if ((bits & 0x0010) != 0)
        {
            buildings |= BuildingType.DwellingMonster2;
        }

        if ((bits & 0x0020) != 0)
        {
            buildings |= BuildingType.DwellingMonster3;
        }

        if ((bits & 0x0040) != 0)
        {
            buildings |= BuildingType.DwellingMonster4;
        }

        if ((bits & 0x0080) != 0)
        {
            buildings |= BuildingType.DwellingMonster5;
        }

        if ((bits & 0x0100) != 0)
        {
            buildings |= BuildingType.DwellingMonster6;
        }

        if ((bits & 0x0200) != 0)
        {
            buildings |= BuildingType.DwellingUpgrade2 | BuildingType.DwellingMonster2;
        }

        if ((bits & 0x0400) != 0)
        {
            buildings |= BuildingType.DwellingUpgrade3 | BuildingType.DwellingMonster3;
        }

        if ((bits & 0x0800) != 0)
        {
            buildings |= BuildingType.DwellingUpgrade4 | BuildingType.DwellingMonster4;
        }

        if ((bits & 0x1000) != 0)
        {
            buildings |= BuildingType.DwellingUpgrade5 | BuildingType.DwellingMonster5;
        }

        if ((bits & 0x2000) != 0)
        {
            buildings |= BuildingType.DwellingUpgrade6 | BuildingType.DwellingMonster6;
        }

        return buildings;
    }

    /// <summary>The mage guild of a level, <c>castle.cpp:1499-1525</c>: level 3 means the first three are built.</summary>
    /// <param name="level">The one byte the block holds.</param>
    /// <returns>The mask those levels stand for.</returns>
    private static BuildingType GuildBuildings(int level)
    {
        BuildingType buildings = BuildingType.Nothing;

        if (level > 0)
        {
            buildings |= BuildingType.MageGuild1;
        }

        if (level > 1)
        {
            buildings |= BuildingType.MageGuild2;
        }

        if (level > 2)
        {
            buildings |= BuildingType.MageGuild3;
        }

        if (level > 3)
        {
            buildings |= BuildingType.MageGuild4;
        }

        if (level > 4)
        {
            buildings |= BuildingType.MageGuild5;
        }

        return buildings;
    }

    /// <summary>
    /// Appends every single building of a mask to the list, so the list holds the buildings and not their sum.
    /// </summary>
    /// <remarks>
    /// <see cref="MapFormatArmy.GetBuildings"/> 只把列表或起来，所以放一个组合值也能用；但详情窗写回时是逐个放的，
    /// 读取时也逐个放，两次打开同一座城得到的列表才一样。
    /// </remarks>
    /// <param name="metadata">The metadata being filled.</param>
    /// <param name="buildings">The mask to split.</param>
    private static void AddBuildings(CastleMetadata metadata, BuildingType buildings)
    {
        foreach (BuildingType building in SingleBuildings)
        {
            if ((buildings & building) == building)
            {
                metadata.BuiltBuildings.Add((uint)building);
            }
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        ushort value = (ushort)(data[offset] | (data[offset + 1] << 8));

        offset += 2;

        return value;
    }

    /// <summary>Every building that is a single bit, i.e. what a metadata list may hold one of.</summary>
    private static readonly BuildingType[] SingleBuildings =
    [
        BuildingType.ThievesGuild,
        BuildingType.Tavern,
        BuildingType.Shipyard,
        BuildingType.Well,
        BuildingType.Statue,
        BuildingType.LeftTurret,
        BuildingType.RightTurret,
        BuildingType.Marketplace,
        BuildingType.Well2,
        BuildingType.Moat,
        BuildingType.Spec,
        BuildingType.Castle,
        BuildingType.Captain,
        BuildingType.Shrine,
        BuildingType.MageGuild1,
        BuildingType.MageGuild2,
        BuildingType.MageGuild3,
        BuildingType.MageGuild4,
        BuildingType.MageGuild5,
        BuildingType.Tent,
        BuildingType.DwellingMonster1,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingMonster6,
        BuildingType.DwellingUpgrade2,
        BuildingType.DwellingUpgrade3,
        BuildingType.DwellingUpgrade4,
        BuildingType.DwellingUpgrade5,
        BuildingType.DwellingUpgrade6,
        BuildingType.DwellingUpgrade7,
    ];
}
