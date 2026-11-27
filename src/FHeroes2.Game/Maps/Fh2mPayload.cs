using System.IO.Compression;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>One object of a tile, <c>Maps::Map_Format::TileObjectInfo</c> (<c>map_format_info.h:39-46</c>).</summary>
/// <param name="Id">Unique object id on the map, <c>id</c>.</param>
/// <param name="Group">Which table the object belongs to, <c>group</c>.</param>
/// <param name="Index">Index inside that table, <c>index</c>.</param>
public readonly record struct Fh2mTileObject(uint Id, byte Group, uint Index);

/// <summary>One tile of the map, <c>Maps::Map_Format::TileInfo</c> (<c>map_format_info.h:48-54</c>).</summary>
/// <param name="TerrainIndex">Terrain image the tile shows, <c>terrainIndex</c>.</param>
/// <param name="TerrainFlags">Flags of the terrain, <c>terrainFlags</c>.</param>
/// <param name="Objects">Objects standing on the tile, in drawing order.</param>
public readonly record struct Fh2mTile(ushort TerrainIndex, byte TerrainFlags, IReadOnlyList<Fh2mTileObject> Objects);

/// <summary>
/// The texts of one translation that live next to the tiles rather than in the header,
/// <c>Maps::Map_Format::TranslationFormat</c> (<c>map_format_info.h:328-343</c>).
/// </summary>
/// <remarks>
/// 参考把它挂在 <c>translationInfo</c> 上（语言到这份结构），所以传闻、每日事件、各处的自定义名也能按语言各存一份。
/// 端口只解析它，让读写不丢数据；编辑器目前不编辑这些按语言的文本。
/// </remarks>
public sealed class Fh2mTranslationFormat
{
    /// <summary>Daily events in this language.</summary>
    public List<MapDailyEvent> DailyEvents { get; init; } = [];

    /// <summary>Rumours in this language.</summary>
    public List<string> Rumors { get; init; } = [];

    /// <summary>Custom names and armies of the towns.</summary>
    public SortedDictionary<uint, CastleMetadata> Castles { get; init; } = [];

    /// <summary>Custom names, armies and skills of the heroes.</summary>
    public SortedDictionary<uint, HeroMetadata> Heroes { get; init; } = [];

    /// <summary>Riddles and answers of the sphinxes.</summary>
    public SortedDictionary<uint, SphinxMetadata> Sphinxes { get; init; } = [];

    /// <summary>Texts of the signs.</summary>
    public SortedDictionary<uint, SignMetadata> Signs { get; init; } = [];

    /// <summary>Messages of the adventure map events.</summary>
    public SortedDictionary<uint, AdventureMapEventMetadata> Events { get; init; } = [];
}

/// <summary>
/// Everything an <c>.fh2m</c> file holds below its header, the <c>MapFormat</c> half of
/// <c>map_format_info.cpp:489-500</c>.
/// </summary>
/// <remarks>
/// <para>
/// 这段在文件里是一整块 zlib 流（参考先序列化再 <c>Compression::zipData</c>）。字段顺序照
/// <c>saveToStream( MapFormat &amp; )</c>：<c>additionalInfo</c>、<c>tiles</c>、<c>dailyEvents</c>、<c>rumors</c>、
/// 十张按对象 UID 索引的元数据表，最后是按语言分的 <c>translationInfo</c>。
/// </para>
/// <para>
/// <b>元数据直接复用编辑器那一套类</b>（<see cref="CastleMetadata"/> 等）：它们的字段与参考一一对应，端口没有理由
/// 再造一份。<see cref="Fh2mTile"/> 里的 <c>Group</c> 与 <c>Index</c> 保持原始数值——把它们翻成对象组与组内下标是
/// 编辑器另存那一步的事（见 <c>oaq.2</c>），序列化层不该依赖那张大表。
/// </para>
/// </remarks>
public sealed class Fh2mPayload
{
    /// <summary>Numbers the campaign maps need, <c>additionalInfo</c>; empty for a normal map.</summary>
    public IReadOnlyList<uint> AdditionalInfo { get; init; } = [];

    /// <summary>Every tile of the map, row by row.</summary>
    public IReadOnlyList<Fh2mTile> Tiles { get; init; } = [];

    /// <summary>Daily events in the main language.</summary>
    public IReadOnlyList<MapDailyEvent> DailyEvents { get; init; } = [];

    /// <summary>Rumours in the main language.</summary>
    public IReadOnlyList<string> Rumors { get; init; } = [];

    /// <summary>Custom names and armies of the towns, keyed by object UID.</summary>
    public SortedDictionary<uint, CastleMetadata> Castles { get; init; } = [];

    /// <summary>Custom names, armies and skills of the heroes, keyed by object UID.</summary>
    public SortedDictionary<uint, HeroMetadata> Heroes { get; init; } = [];

    /// <summary>Riddles and answers of the sphinxes, keyed by object UID.</summary>
    public SortedDictionary<uint, SphinxMetadata> Sphinxes { get; init; } = [];

    /// <summary>Texts of the signs, keyed by object UID.</summary>
    public SortedDictionary<uint, SignMetadata> Signs { get; init; } = [];

    /// <summary>Messages of the adventure map events, keyed by object UID.</summary>
    public SortedDictionary<uint, AdventureMapEventMetadata> Events { get; init; } = [];

    /// <summary>Items a selection object hands out, keyed by object UID.</summary>
    public SortedDictionary<uint, SelectionObjectMetadata> Selections { get; init; } = [];

    /// <summary>Owners of the capturable objects, keyed by object UID.</summary>
    public SortedDictionary<uint, CapturableObjectMetadata> Capturables { get; init; } = [];

    /// <summary>Counts of the monster stacks, keyed by object UID.</summary>
    public SortedDictionary<uint, MonsterMetadata> Monsters { get; init; } = [];

    /// <summary>Radiuses and selections of the artifacts, keyed by object UID.</summary>
    public SortedDictionary<uint, ArtifactMetadata> Artifacts { get; init; } = [];

    /// <summary>Counts of the resource piles, keyed by object UID.</summary>
    public SortedDictionary<uint, ResourceMetadata> Resources { get; init; } = [];

    /// <summary>The per-language texts that live next to the tiles, <c>translationInfo</c>.</summary>
    public SortedDictionary<SupportedLanguage, Fh2mTranslationFormat> TranslationInfo { get; init; } = [];

    /// <summary>
    /// Reads a payload out of its compressed bytes, <c>loadFromStream( MapFormat &amp; )</c>'s middle
    /// (<c>map_format_info.cpp:505-540</c>).
    /// </summary>
    /// <param name="compressed">The zlib stream the file stored after the header.</param>
    /// <returns>The payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="compressed"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The stream is empty, corrupt, or describes more than it holds.</exception>
    public static Fh2mPayload Read(byte[] compressed)
    {
        ArgumentNullException.ThrowIfNull(compressed);

        if (compressed.Length == 0)
        {
            throw new InvalidDataException("The .fh2m payload is empty.");
        }

        byte[] raw;

        try
        {
            using var input = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            raw = output.ToArray();
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"The .fh2m payload is not a valid zlib stream: {error.Message}", error);
        }

        using var stream = new MemoryStream(raw);
        BigEndianReader reader = new(stream);

        uint[] additionalInfo = reader.ReadUInt32Array("additional info");

        int tileCount = reader.ReadCount("tiles");
        Fh2mTile[] tiles = new Fh2mTile[tileCount];

        for (int index = 0; index < tileCount; ++index)
        {
            tiles[index] = ReadTile(reader);
        }

        List<MapDailyEvent> dailyEvents = ReadDailyEvents(reader);
        List<string> rumors = ReadRumors(reader);

        return new Fh2mPayload
        {
            AdditionalInfo = additionalInfo,
            Tiles = tiles,
            DailyEvents = dailyEvents,
            Rumors = rumors,
            Castles = ReadUIntKeyed(reader, ReadCastle),
            Heroes = ReadUIntKeyed(reader, ReadHero),
            Sphinxes = ReadUIntKeyed(reader, ReadSphinx),
            Signs = ReadUIntKeyed(reader, ReadSign),
            Events = ReadUIntKeyed(reader, ReadEvent),
            Selections = ReadUIntKeyed(reader, ReadSelection),
            Capturables = ReadUIntKeyed(reader, ReadCapturable),
            Monsters = ReadUIntKeyed(reader, ReadMonster),
            Artifacts = ReadUIntKeyed(reader, ReadArtifact),
            Resources = ReadUIntKeyed(reader, ReadResource),
            TranslationInfo = ReadTranslationInfo(reader),
        };
    }

    /// <summary>
    /// Writes the payload as the zlib stream that goes after the header, <c>saveToStream( MapFormat &amp; )</c>'s
    /// middle (<c>map_format_info.cpp:489-500</c>).
    /// </summary>
    /// <returns>The compressed bytes.</returns>
    public byte[] Write()
    {
        using var raw = new MemoryStream();

        BigEndianWriter writer = new(raw);

        writer.WriteUInt32Array(AdditionalInfo);
        writer.WriteCount(Tiles.Count);

        foreach (Fh2mTile tile in Tiles)
        {
            writer.WriteUInt16(tile.TerrainIndex);
            writer.WriteByte(tile.TerrainFlags);
            writer.WriteCount(tile.Objects.Count);

            foreach (Fh2mTileObject item in tile.Objects)
            {
                writer.WriteUInt32(item.Id);
                writer.WriteByte(item.Group);
                writer.WriteUInt32(item.Index);
            }
        }

        WriteDailyEvents(writer, DailyEvents);
        WriteRumors(writer, Rumors);
        WriteUIntKeyed(writer, Castles, WriteCastle);
        WriteUIntKeyed(writer, Heroes, WriteHero);
        WriteUIntKeyed(writer, Sphinxes, WriteSphinx);
        WriteUIntKeyed(writer, Signs, WriteSign);
        WriteUIntKeyed(writer, Events, WriteEvent);
        WriteUIntKeyed(writer, Selections, WriteSelection);
        WriteUIntKeyed(writer, Capturables, WriteCapturable);
        WriteUIntKeyed(writer, Monsters, WriteMonster);
        WriteUIntKeyed(writer, Artifacts, WriteArtifact);
        WriteUIntKeyed(writer, Resources, WriteResource);
        WriteTranslationInfo(writer);

        writer.Flush();

        using var compressed = new MemoryStream();

        // 参考用 zipData( …, false )，也就是 zlib 的默认压缩级别。
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        return compressed.ToArray();
    }

    private static Fh2mTile ReadTile(BigEndianReader reader)
    {
        ushort terrainIndex = reader.ReadUInt16();
        byte terrainFlags = reader.ReadByte();

        int objectCount = reader.ReadCount("objects of a tile");
        Fh2mTileObject[] objects = new Fh2mTileObject[objectCount];

        for (int index = 0; index < objectCount; ++index)
        {
            objects[index] = new Fh2mTileObject(reader.ReadUInt32(), reader.ReadByte(), reader.ReadUInt32());
        }

        return new Fh2mTile(terrainIndex, terrainFlags, objects);
    }

    private static List<MapDailyEvent> ReadDailyEvents(BigEndianReader reader)
    {
        int count = reader.ReadCount("daily events");
        List<MapDailyEvent> events = new(count);

        for (int index = 0; index < count; ++index)
        {
            events.Add(new MapDailyEvent
            {
                Message = reader.ReadString(),
                HumanPlayerColors = (PlayerColor)reader.ReadByte(),
                ComputerPlayerColors = (PlayerColor)reader.ReadByte(),
                FirstOccurrenceDay = reader.ReadUInt32(),
                RepeatPeriodInDays = reader.ReadUInt32(),
                Resources = ReadFunds(reader),
            });
        }

        return events;
    }

    private static List<string> ReadRumors(BigEndianReader reader)
    {
        int count = reader.ReadCount("rumors");
        List<string> rumors = new(count);

        for (int index = 0; index < count; ++index)
        {
            rumors.Add(reader.ReadString());
        }

        return rumors;
    }

    private static void WriteDailyEvents(BigEndianWriter writer, IReadOnlyList<MapDailyEvent> events)
    {
        writer.WriteCount(events.Count);

        foreach (MapDailyEvent item in events)
        {
            writer.WriteString(item.Message);
            writer.WriteByte((byte)item.HumanPlayerColors);
            writer.WriteByte((byte)item.ComputerPlayerColors);
            writer.WriteUInt32(item.FirstOccurrenceDay);
            writer.WriteUInt32(item.RepeatPeriodInDays);
            WriteFunds(writer, item.Resources);
        }
    }

    private static void WriteRumors(BigEndianWriter writer, IReadOnlyList<string> rumors)
    {
        writer.WriteCount(rumors.Count);

        foreach (string rumor in rumors)
        {
            writer.WriteString(rumor);
        }
    }

    /// <summary>Reads a <c>std::map&lt;uint32_t, T&gt;</c>: a count and that many key/value pairs.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="reader">The stream.</param>
    /// <param name="read">Reads one value.</param>
    /// <returns>The table, in the order the file listed it.</returns>
    private static SortedDictionary<uint, T> ReadUIntKeyed<T>(BigEndianReader reader, Func<BigEndianReader, T> read)
    {
        int count = reader.ReadCount("entries of a metadata table");
        SortedDictionary<uint, T> table = [];

        for (int index = 0; index < count; ++index)
        {
            uint key = reader.ReadUInt32();

            table[key] = read(reader);
        }

        return table;
    }

    private static void WriteUIntKeyed<T>(BigEndianWriter writer, SortedDictionary<uint, T> table,
        Action<BigEndianWriter, T> write)
    {
        writer.WriteCount(table.Count);

        foreach ((uint key, T value) in table)
        {
            writer.WriteUInt32(key);
            write(writer, value);
        }
    }

    private static SortedDictionary<SupportedLanguage, Fh2mTranslationFormat> ReadTranslationInfo(
        BigEndianReader reader)
    {
        int count = reader.ReadCount("translations of the tiles");
        SortedDictionary<SupportedLanguage, Fh2mTranslationFormat> table = [];

        for (int index = 0; index < count; ++index)
        {
            SupportedLanguage language = (SupportedLanguage)reader.ReadByte();

            table[language] = new Fh2mTranslationFormat
            {
                DailyEvents = ReadDailyEvents(reader),
                Rumors = ReadRumors(reader),
                Castles = ReadUIntKeyed(reader, ReadCastle),
                Heroes = ReadUIntKeyed(reader, ReadHero),
                Sphinxes = ReadUIntKeyed(reader, ReadSphinx),
                Signs = ReadUIntKeyed(reader, ReadSign),
                Events = ReadUIntKeyed(reader, ReadEvent),
            };
        }

        return table;
    }

    private void WriteTranslationInfo(BigEndianWriter writer)
    {
        writer.WriteCount(TranslationInfo.Count);

        foreach ((SupportedLanguage language, Fh2mTranslationFormat text) in TranslationInfo)
        {
            writer.WriteByte((byte)language);
            WriteDailyEvents(writer, text.DailyEvents);
            WriteRumors(writer, text.Rumors);
            WriteUIntKeyed(writer, text.Castles, WriteCastle);
            WriteUIntKeyed(writer, text.Heroes, WriteHero);
            WriteUIntKeyed(writer, text.Sphinxes, WriteSphinx);
            WriteUIntKeyed(writer, text.Signs, WriteSign);
            WriteUIntKeyed(writer, text.Events, WriteEvent);
        }
    }

    private static CastleMetadata ReadCastle(BigEndianReader reader) => new()
    {
        CustomName = reader.ReadString(),
        DefenderMonsterType = reader.ReadInt32Array("defender monsters"),
        DefenderMonsterCount = reader.ReadInt32Array("defender monsters"),
        CustomBuildings = reader.ReadBool(),
        BuiltBuildings = [.. reader.ReadUInt32Array("built buildings")],
        BannedBuildings = [.. reader.ReadUInt32Array("banned buildings")],
        MustHaveSpells = reader.ReadByteKeyedInt32Map(),
        BannedSpells = [.. reader.ReadInt32Array("banned spells")],
        AvailableToHireMonsterCount = reader.ReadInt32Array("dwellings"),
    };

    private static void WriteCastle(BigEndianWriter writer, CastleMetadata metadata)
    {
        writer.WriteString(metadata.CustomName);
        writer.WriteInt32Array(metadata.DefenderMonsterType);
        writer.WriteInt32Array(metadata.DefenderMonsterCount);
        writer.WriteBool(metadata.CustomBuildings);
        writer.WriteUInt32Array(metadata.BuiltBuildings);
        writer.WriteUInt32Array(metadata.BannedBuildings);
        writer.WriteByteKeyedInt32Map(metadata.MustHaveSpells);
        writer.WriteInt32Array(metadata.BannedSpells);
        writer.WriteInt32Array(metadata.AvailableToHireMonsterCount);
    }

    private static HeroMetadata ReadHero(BigEndianReader reader) => new()
    {
        CustomName = reader.ReadString(),
        CustomPortrait = reader.ReadInt32(),
        ArmyMonsterType = reader.ReadInt32Array("army of a hero"),
        ArmyMonsterCount = reader.ReadInt32Array("army of a hero"),
        Artifact = reader.ReadInt32Array("artifacts of a hero"),
        ArtifactMetadata = reader.ReadInt32Array("artifacts of a hero"),
        AvailableSpells = [.. reader.ReadInt32Array("spells of a hero")],
        IsOnPatrol = reader.ReadBool(),
        PatrolRadius = reader.ReadByte(),
        SecondarySkill = reader.ReadSByteArray("skills of a hero"),
        SecondarySkillLevel = reader.ReadByteArray("skill levels of a hero"),
        CustomLevel = reader.ReadInt16(),
        CustomExperience = reader.ReadInt32(),
        CustomAttack = reader.ReadInt16(),
        CustomDefense = reader.ReadInt16(),
        CustomKnowledge = reader.ReadInt16(),
        CustomSpellPower = reader.ReadInt16(),
        MagicPoints = reader.ReadInt16(),
        Race = reader.ReadByte(),
    };

    private static void WriteHero(BigEndianWriter writer, HeroMetadata metadata)
    {
        writer.WriteString(metadata.CustomName);
        writer.WriteInt32(metadata.CustomPortrait);
        writer.WriteInt32Array(metadata.ArmyMonsterType);
        writer.WriteInt32Array(metadata.ArmyMonsterCount);
        writer.WriteInt32Array(metadata.Artifact);
        writer.WriteInt32Array(metadata.ArtifactMetadata);
        writer.WriteInt32Array(metadata.AvailableSpells);
        writer.WriteBool(metadata.IsOnPatrol);
        writer.WriteByte(metadata.PatrolRadius);
        writer.WriteSByteArray(metadata.SecondarySkill);
        writer.WriteByteArray(metadata.SecondarySkillLevel);
        writer.WriteInt16(metadata.CustomLevel);
        writer.WriteInt32(metadata.CustomExperience);
        writer.WriteInt16(metadata.CustomAttack);
        writer.WriteInt16(metadata.CustomDefense);
        writer.WriteInt16(metadata.CustomKnowledge);
        writer.WriteInt16(metadata.CustomSpellPower);
        writer.WriteInt16(metadata.MagicPoints);
        writer.WriteByte(metadata.Race);
    }

    private static SphinxMetadata ReadSphinx(BigEndianReader reader) => new()
    {
        Riddle = reader.ReadString(),
        Answers = reader.ReadStrings(),
        Artifact = reader.ReadInt32(),
        ArtifactMetadata = reader.ReadInt32(),
        Resources = ReadFunds(reader),
    };

    private static void WriteSphinx(BigEndianWriter writer, SphinxMetadata metadata)
    {
        writer.WriteString(metadata.Riddle);
        writer.WriteStrings(metadata.Answers);
        writer.WriteInt32(metadata.Artifact);
        writer.WriteInt32(metadata.ArtifactMetadata);
        WriteFunds(writer, metadata.Resources);
    }

    private static SignMetadata ReadSign(BigEndianReader reader) => new() { Message = reader.ReadString() };

    private static void WriteSign(BigEndianWriter writer, SignMetadata metadata) =>
        writer.WriteString(metadata.Message);

    private static AdventureMapEventMetadata ReadEvent(BigEndianReader reader) => new()
    {
        Message = reader.ReadString(),
        HumanPlayerColors = reader.ReadByte(),
        ComputerPlayerColors = reader.ReadByte(),
        IsRecurringEvent = reader.ReadBool(),
        Artifact = reader.ReadInt32(),
        ArtifactMetadata = reader.ReadInt32(),
        Resources = ReadFunds(reader),
        Attack = reader.ReadInt16(),
        Defense = reader.ReadInt16(),
        Knowledge = reader.ReadInt16(),
        SpellPower = reader.ReadInt16(),
        Experience = reader.ReadInt32(),
        SecondarySkill = reader.ReadByte(),
        SecondarySkillLevel = reader.ReadByte(),
        MonsterType = reader.ReadInt32(),
        MonsterCount = reader.ReadInt32(),
    };

    private static void WriteEvent(BigEndianWriter writer, AdventureMapEventMetadata metadata)
    {
        writer.WriteString(metadata.Message);
        writer.WriteByte(metadata.HumanPlayerColors);
        writer.WriteByte(metadata.ComputerPlayerColors);
        writer.WriteBool(metadata.IsRecurringEvent);
        writer.WriteInt32(metadata.Artifact);
        writer.WriteInt32(metadata.ArtifactMetadata);
        WriteFunds(writer, metadata.Resources);
        writer.WriteInt16(metadata.Attack);
        writer.WriteInt16(metadata.Defense);
        writer.WriteInt16(metadata.Knowledge);
        writer.WriteInt16(metadata.SpellPower);
        writer.WriteInt32(metadata.Experience);
        writer.WriteByte(metadata.SecondarySkill);
        writer.WriteByte(metadata.SecondarySkillLevel);
        writer.WriteInt32(metadata.MonsterType);
        writer.WriteInt32(metadata.MonsterCount);
    }

    private static SelectionObjectMetadata ReadSelection(BigEndianReader reader) => new()
    {
        SelectedItems = [.. reader.ReadInt32Array("items of a selection")],
    };

    private static void WriteSelection(BigEndianWriter writer, SelectionObjectMetadata metadata) =>
        writer.WriteInt32Array(metadata.SelectedItems);

    private static CapturableObjectMetadata ReadCapturable(BigEndianReader reader) => new()
    {
        OwnerColor = (PlayerColor)reader.ReadByte(),
    };

    private static void WriteCapturable(BigEndianWriter writer, CapturableObjectMetadata metadata) =>
        writer.WriteByte((byte)metadata.OwnerColor);

    private static MonsterMetadata ReadMonster(BigEndianReader reader) => new()
    {
        Count = reader.ReadInt32(),
        JoinCondition = reader.ReadInt32(),
        IsWeeklyGrowthDisabled = reader.ReadBool(),
        Selected = [.. reader.ReadInt32Array("monsters of a stack")],
    };

    private static void WriteMonster(BigEndianWriter writer, MonsterMetadata metadata)
    {
        writer.WriteInt32(metadata.Count);
        writer.WriteInt32(metadata.JoinCondition);
        writer.WriteBool(metadata.IsWeeklyGrowthDisabled);
        writer.WriteInt32Array(metadata.Selected);
    }

    private static ArtifactMetadata ReadArtifact(BigEndianReader reader) => new()
    {
        Radius = reader.ReadInt32(),
        CaptureCondition = reader.ReadInt32(),
        Selected = [.. reader.ReadInt32Array("artifacts of a selection")],
    };

    private static void WriteArtifact(BigEndianWriter writer, ArtifactMetadata metadata)
    {
        writer.WriteInt32(metadata.Radius);
        writer.WriteInt32(metadata.CaptureCondition);
        writer.WriteInt32Array(metadata.Selected);
    }

    private static ResourceMetadata ReadResource(BigEndianReader reader) => new() { Count = reader.ReadInt32() };

    private static void WriteResource(BigEndianWriter writer, ResourceMetadata metadata) =>
        writer.WriteInt32(metadata.Count);

    /// <summary>Reads the seven resource amounts, <c>operator&gt;&gt;( Funds )</c> (<c>resource.cpp:671</c>).</summary>
    /// <param name="reader">The stream.</param>
    /// <returns>The amounts; the field order is wood, mercury, ore, sulfur, crystal, gems, gold.</returns>
    private static Funds ReadFunds(BigEndianReader reader)
    {
        int wood = reader.ReadInt32();
        int mercury = reader.ReadInt32();
        int ore = reader.ReadInt32();
        int sulfur = reader.ReadInt32();
        int crystal = reader.ReadInt32();
        int gems = reader.ReadInt32();
        int gold = reader.ReadInt32();

        return new Funds(ore, wood, mercury, sulfur, crystal, gems, gold);
    }

    private static void WriteFunds(BigEndianWriter writer, Funds funds)
    {
        writer.WriteInt32(funds.Wood);
        writer.WriteInt32(funds.Mercury);
        writer.WriteInt32(funds.Ore);
        writer.WriteInt32(funds.Sulfur);
        writer.WriteInt32(funds.Crystal);
        writer.WriteInt32(funds.Gems);
        writer.WriteInt32(funds.Gold);
    }
}
