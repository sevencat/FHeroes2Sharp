using System.Buffers.Binary;
using System.Text;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The head of an <c>.fh2m</c> map, the <c>Maps::Map_Format::BaseMapFormat</c> of
/// <c>map_format_info.h:345-387</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>.fh2m</c> 是「不压缩的头部 + 压缩的其余部分」两段：头部是这张图的元信息（谁在局中、胜负条件、作者写下的
/// 三种文本、以及每种语言的翻译），其余部分（瓦片、每日事件、各类物件元数据）整体压成一段。端口只解析头部，压缩段
/// 原样保留在 <see cref="Payload"/> 里——翻译落盘只关心头部，而重写瓦片与元数据是另一件事。
/// </para>
/// <para>
/// <b>字段宽度照参考的流算子</b>（<c>serialize.cpp:150-230</c>）：全大端；<c>bool</c> 一字节；语言是
/// <c>uint8_t</c>；字符串是 <c>uint32</c> 长度加字节；<c>std::vector</c> 与 <c>std::array</c> 都带 <c>uint32</c>
/// 计数前缀（参考对数组也走同一套模板，所以六个种族前面确实有那个 6）；语言表是 <c>uint32</c> 计数，每项先写语言再写
/// 三串。
/// </para>
/// <para>
/// <b>版本差异</b>（<c>map_format_info.cpp:452-487</c>）：<c>creatorNotes</c> 与 <c>translations</c> 都不是一开始就有，
/// 版本小于 9 没有前者、小于 11 没有后者。端口因此保留 <see cref="Version"/>，写出时按同一门槛决定写不写。
/// </para>
/// </remarks>
public sealed record Fh2mBaseMap
{
    /// <summary>The six bytes every file starts with, <c>magicWord</c> of <c>map_format_info.cpp:101</c>.</summary>
    public const string MagicWord = "h2map\0";

    /// <summary>Smallest file the reference accepts, <c>minFileSize</c> of <c>:105</c>.</summary>
    public const int MinimumFileSize = 512;

    /// <summary>Oldest layout the reference still reads, <c>minimumSupportedVersion</c>.</summary>
    public const int MinimumSupportedVersion = 2;

    /// <summary>Newest layout the reference writes, <c>currentSupportedVersion</c>.</summary>
    public const int CurrentSupportedVersion = 13;

    /// <summary>First version that stores <c>creatorNotes</c> (<c>map_format_info.cpp:463-469</c>).</summary>
    public const int CreatorNotesVersion = 9;

    /// <summary>First version that stores <c>translations</c> (<c>:471-478</c>).</summary>
    public const int TranslationsVersion = 11;

    /// <summary>How many races the header stores, the six player slots.</summary>
    public const int PlayerRaceCount = 6;

    /// <summary>Layout version the file was written with.</summary>
    public int Version { get; init; } = CurrentSupportedVersion;

    /// <summary>Whether the map belongs to a campaign.</summary>
    public bool IsCampaign { get; init; }

    /// <summary>Difficulty suggested by the map maker.</summary>
    public GameDifficulty Difficulty { get; init; } = GameDifficulty.Normal;

    /// <summary>Colours that take part in the game.</summary>
    public PlayerColor AvailablePlayerColors { get; init; }

    /// <summary>Colours a person may play.</summary>
    public PlayerColor HumanPlayerColors { get; init; }

    /// <summary>Colours the computer may play.</summary>
    public PlayerColor ComputerPlayerColors { get; init; }

    /// <summary>The alliances of the map, one colour mask per entry.</summary>
    public IReadOnlyList<PlayerColor> Alliances { get; init; } = [];

    /// <summary>The race of each player slot, index 0 being blue.</summary>
    public IReadOnlyList<MapRace> PlayerRaces { get; init; } = [];

    /// <summary>Victory condition type, <c>victoryConditionType</c>.</summary>
    public int VictoryConditionType { get; init; }

    /// <summary>Whether the victory condition applies to the computer as well.</summary>
    public bool IsVictoryConditionApplicableForAi { get; init; }

    /// <summary>Whether defeating every opponent is still a victory.</summary>
    public bool AllowNormalVictory { get; init; }

    /// <summary>Numbers the victory condition needs, <c>victoryConditionMetadata</c>.</summary>
    public IReadOnlyList<uint> VictoryConditionMetadata { get; init; } = [];

    /// <summary>Loss condition type, <c>lossConditionType</c>.</summary>
    public int LossConditionType { get; init; }

    /// <summary>Numbers the loss condition needs, <c>lossConditionMetadata</c>.</summary>
    public IReadOnlyList<uint> LossConditionMetadata { get; init; } = [];

    /// <summary>Width of the square map in tiles.</summary>
    public int Width { get; init; }

    /// <summary>The language the texts below are written in, <c>mainLanguage</c>.</summary>
    public SupportedLanguage MainLanguage { get; init; } = SupportedLanguage.English;

    /// <summary>Scenario name, <c>name</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Scenario description, <c>description</c>.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Contact details of the map maker, <c>creatorNotes</c>; empty before version 9.</summary>
    public string CreatorNotes { get; init; } = string.Empty;

    /// <summary>The texts of every language other than <see cref="MainLanguage"/>, <c>translations</c>.</summary>
    public IReadOnlyDictionary<SupportedLanguage, MapTranslationText> Translations { get; init; } =
        new Dictionary<SupportedLanguage, MapTranslationText>();

    /// <summary>
    /// Everything the header does not describe, exactly as the file stored it: the zlib stream of the tiles, the
    /// daily events, the rumours and every metadata table.
    /// </summary>
    /// <remarks>
    /// 参考在这段里放的是 <c>MapFormat</c> 的其余成员，先压成一段再写；端口不解析它，所以原样保存、原样写回。
    /// 只读头部的调用方（语言窗）因此不会弄丢瓦片。
    /// </remarks>
    public byte[] Payload { get; init; } = [];

    /// <summary>Whether <see cref="Version"/> stores <see cref="CreatorNotes"/>.</summary>
    public bool HasCreatorNotes => Version >= CreatorNotesVersion;

    /// <summary>Whether <see cref="Version"/> stores <see cref="Translations"/>.</summary>
    public bool HasTranslations => Version >= TranslationsVersion;

    /// <summary>
    /// Reads the header of an <c>.fh2m</c> file, <c>loadBaseMap()</c> (<c>map_format_info.cpp:770-800</c>).
    /// </summary>
    /// <param name="stream">The file, read from its start.</param>
    /// <returns>The header and the unparsed payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    /// The stream is shorter than <see cref="MinimumFileSize"/>, does not start with <see cref="MagicWord"/>, holds an
    /// unsupported version, or ends before the header does.
    /// </exception>
    public static Fh2mBaseMap Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek && stream.Length - stream.Position < MinimumFileSize)
        {
            throw new InvalidDataException(
                $"An .fh2m file is at least {MinimumFileSize} bytes, and this stream holds fewer.");
        }

        BigEndianReader reader = new(stream);

        foreach (char expected in MagicWord)
        {
            if (reader.ReadByte() != expected)
            {
                throw new InvalidDataException("The file does not start with the h2map signature.");
            }
        }

        int version = reader.ReadUInt16();

        if (version < MinimumSupportedVersion || version > CurrentSupportedVersion)
        {
            throw new InvalidDataException(
                $"Version {version} of the .fh2m layout is not supported ({MinimumSupportedVersion}..{CurrentSupportedVersion}).");
        }

        bool isCampaign = reader.ReadBool();
        GameDifficulty difficulty = (GameDifficulty)reader.ReadByte();
        PlayerColor available = (PlayerColor)reader.ReadByte();
        PlayerColor humans = (PlayerColor)reader.ReadByte();
        PlayerColor computers = (PlayerColor)reader.ReadByte();

        int allianceCount = reader.ReadCount("alliances");
        PlayerColor[] alliances = new PlayerColor[allianceCount];

        for (int index = 0; index < allianceCount; ++index)
        {
            alliances[index] = (PlayerColor)reader.ReadByte();
        }

        // std::array 走的是和 std::vector 同一套模板，所以六个种族前面确实有一个计数。
        int raceCount = reader.ReadCount("player races");
        MapRace[] races = new MapRace[raceCount];

        for (int index = 0; index < raceCount; ++index)
        {
            races[index] = (MapRace)reader.ReadByte();
        }

        int victoryType = reader.ReadByte();
        bool victoryForAi = reader.ReadBool();
        bool allowNormalVictory = reader.ReadBool();
        uint[] victoryMetadata = reader.ReadUInt32Array("victory metadata");
        int lossType = reader.ReadByte();
        uint[] lossMetadata = reader.ReadUInt32Array("loss metadata");
        int width = reader.ReadInt32();

        if (width <= 0)
        {
            throw new InvalidDataException($"The map claims to be {width} tiles wide.");
        }

        SupportedLanguage mainLanguage = (SupportedLanguage)reader.ReadByte();
        string name = reader.ReadString();
        string description = reader.ReadString();
        string creatorNotes = version >= CreatorNotesVersion ? reader.ReadString() : string.Empty;

        Dictionary<SupportedLanguage, MapTranslationText> translations = [];

        if (version >= TranslationsVersion)
        {
            int languageCount = reader.ReadCount("translations");

            for (int index = 0; index < languageCount; ++index)
            {
                SupportedLanguage language = (SupportedLanguage)reader.ReadByte();

                translations[language] = new MapTranslationText
                {
                    Name = reader.ReadString(),
                    Description = reader.ReadString(),
                    CreatorNotes = reader.ReadString(),
                };
            }
        }

        byte[] payload = reader.ReadToEnd();

        return new Fh2mBaseMap
        {
            Version = version,
            IsCampaign = isCampaign,
            Difficulty = difficulty,
            AvailablePlayerColors = available,
            HumanPlayerColors = humans,
            ComputerPlayerColors = computers,
            Alliances = alliances,
            PlayerRaces = races,
            VictoryConditionType = victoryType,
            IsVictoryConditionApplicableForAi = victoryForAi,
            AllowNormalVictory = allowNormalVictory,
            VictoryConditionMetadata = victoryMetadata,
            LossConditionType = lossType,
            LossConditionMetadata = lossMetadata,
            Width = width,
            MainLanguage = mainLanguage,
            Name = name,
            Description = description,
            CreatorNotes = creatorNotes,
            Translations = translations,
            Payload = payload,
        };
    }

    /// <summary>
    /// Reads an <c>.fh2m</c> file, the file form of <see cref="Read(Stream)"/>.
    /// </summary>
    /// <param name="filePath">Path of the file.</param>
    /// <returns>The header and the unparsed payload.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The file is not a readable <c>.fh2m</c>; see <see cref="Read(Stream)"/>.</exception>
    public static Fh2mBaseMap ReadFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using FileStream file = File.OpenRead(filePath);

        return Read(file);
    }

    /// <summary>
    /// Writes the file, <c>saveMap()</c> (<c>map_format_info.cpp:806-830</c>): the signature, the header and the
    /// payload exactly as it was read.
    /// </summary>
    /// <param name="stream">Destination, written from its current position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void Write(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        BigEndianWriter writer = new(stream);

        foreach (char value in MagicWord)
        {
            writer.WriteByte((byte)value);
        }

        writer.WriteUInt16((ushort)Version);
        writer.WriteBool(IsCampaign);
        writer.WriteByte((byte)Difficulty);
        writer.WriteByte((byte)AvailablePlayerColors);
        writer.WriteByte((byte)HumanPlayerColors);
        writer.WriteByte((byte)ComputerPlayerColors);

        writer.WriteCount(Alliances.Count);

        foreach (PlayerColor alliance in Alliances)
        {
            writer.WriteByte((byte)alliance);
        }

        writer.WriteCount(PlayerRaces.Count);

        foreach (MapRace race in PlayerRaces)
        {
            writer.WriteByte((byte)race);
        }

        writer.WriteByte((byte)VictoryConditionType);
        writer.WriteBool(IsVictoryConditionApplicableForAi);
        writer.WriteBool(AllowNormalVictory);
        writer.WriteUInt32Array(VictoryConditionMetadata);
        writer.WriteByte((byte)LossConditionType);
        writer.WriteUInt32Array(LossConditionMetadata);
        writer.WriteInt32(Width);
        writer.WriteByte((byte)MainLanguage);
        writer.WriteString(Name);
        writer.WriteString(Description);

        if (HasCreatorNotes)
        {
            writer.WriteString(CreatorNotes);
        }

        if (HasTranslations)
        {
            // std::map 是按键升序写出的，读回来也是那个顺序。
            SupportedLanguage[] languages = [.. Translations.Keys.Order()];

            writer.WriteCount(languages.Length);

            foreach (SupportedLanguage language in languages)
            {
                MapTranslationText text = Translations[language];

                writer.WriteByte((byte)language);
                writer.WriteString(text.Name);
                writer.WriteString(text.Description);
                writer.WriteString(text.CreatorNotes);
            }
        }

        writer.WriteRaw(Payload);
        writer.Flush();
    }

    /// <summary>Writes an <c>.fh2m</c> file, the file form of <see cref="Write(Stream)"/>.</summary>
    /// <param name="filePath">Path of the file; it is overwritten.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    public void WriteFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using FileStream file = File.Create(filePath);

        Write(file);
    }

    /// <summary>
    /// Whether the bytes at the start of <paramref name="stream"/> are the <c>.fh2m</c> signature; the stream is put
    /// back where it was.
    /// </summary>
    /// <param name="stream">The stream to peek at.</param>
    /// <returns><see langword="true"/> for an <c>.fh2m</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static bool HasSignature(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream has to be seekable to be peeked at.", nameof(stream));
        }

        long start = stream.Position;
        Span<byte> magic = stackalloc byte[MagicWord.Length];
        int read = stream.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false);

        stream.Position = start;

        if (read != magic.Length)
        {
            return false;
        }

        for (int index = 0; index < magic.Length; ++index)
        {
            if (magic[index] != (byte)MagicWord[index])
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Big-endian reading of the <c>.fh2m</c> header, the <c>IStreamBase</c> of the reference.</summary>
/// <remarks>
/// 参考的流默认小端、<c>.fh2m</c> 的打开处显式 <c>setBigendian( true )</c>（<c>map_format_info.cpp:775</c>）。
/// 端口不复用 <see cref="Core.Io.LittleEndianReader"/>：这份格式每一个多字节数字都要换字节序，单独一层更不容易写错。
/// </remarks>
internal sealed class BigEndianReader
{
    private readonly Stream _stream;
    private readonly byte[] _scratch = new byte[4];

    internal BigEndianReader(Stream stream) => _stream = stream;

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The stream ended.</exception>
    internal byte ReadByte()
    {
        int value = _stream.ReadByte();

        return value >= 0 ? (byte)value : throw new InvalidDataException("The .fh2m header ends early.");
    }

    /// <summary>Reads a <c>bool</c>, one byte in the reference.</summary>
    /// <returns>The value.</returns>
    internal bool ReadBool() => ReadByte() != 0;

    /// <summary>Reads a big-endian <c>uint16</c>.</summary>
    /// <returns>The value.</returns>
    internal ushort ReadUInt16()
    {
        Fill(2);

        return BinaryPrimitives.ReadUInt16BigEndian(_scratch);
    }

    /// <summary>Reads a big-endian <c>int32</c>.</summary>
    /// <returns>The value.</returns>
    internal int ReadInt32()
    {
        Fill(4);

        return BinaryPrimitives.ReadInt32BigEndian(_scratch);
    }

    /// <summary>Reads a container count, refusing a value the rest of the header cannot back.</summary>
    /// <param name="what">What is being counted, for the error message.</param>
    /// <returns>The count.</returns>
    /// <exception cref="InvalidDataException">The count is larger than the bytes left.</exception>
    internal int ReadCount(string what)
    {
        int count = ReadInt32();

        if (count < 0 || (count > 0 && count > _stream.Length - _stream.Position))
        {
            throw new InvalidDataException($"The .fh2m header claims {count} {what}, which the file cannot hold.");
        }

        return count;
    }

    /// <summary>Reads a <c>uint32</c> length prefixed string.</summary>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">The length is not backed by the file.</exception>
    internal string ReadString()
    {
        int length = ReadCount("bytes of text");
        byte[] bytes = new byte[length];
        int read = _stream.ReadAtLeast(bytes, length, throwOnEndOfStream: false);

        if (read != length)
        {
            throw new InvalidDataException("The .fh2m header ends inside a text.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Reads a big-endian <c>uint32</c>.</summary>
    /// <returns>The value.</returns>
    internal uint ReadUInt32()
    {
        Fill(4);

        return BinaryPrimitives.ReadUInt32BigEndian(_scratch);
    }

    /// <summary>Reads a big-endian <c>int16</c>.</summary>
    /// <returns>The value.</returns>
    internal short ReadInt16()
    {
        Fill(2);

        return BinaryPrimitives.ReadInt16BigEndian(_scratch);
    }

    /// <summary>Reads a <c>uint32</c> count prefixed array of big-endian <c>uint32</c>.</summary>
    /// <param name="what">What is being read, for the error message.</param>
    /// <returns>The numbers.</returns>
    internal uint[] ReadUInt32Array(string what)
    {
        int count = ReadCount(what);
        uint[] values = new uint[count];

        for (int index = 0; index < count; ++index)
        {
            Fill(4);
            values[index] = BinaryPrimitives.ReadUInt32BigEndian(_scratch);
        }

        return values;
    }

    /// <summary>
    /// Reads a count prefixed array of big-endian <c>int32</c>, a <c>std::array&lt;int32_t, N&gt;</c> or a
    /// <c>std::vector&lt;int32_t&gt;</c>; the reference writes the count for both.
    /// </summary>
    /// <param name="what">What is being read, for the error message.</param>
    /// <returns>The numbers.</returns>
    internal int[] ReadInt32Array(string what)
    {
        int count = ReadCount(what);
        int[] values = new int[count];

        for (int index = 0; index < count; ++index)
        {
            values[index] = ReadInt32();
        }

        return values;
    }

    /// <summary>Reads a count prefixed array of <c>int8</c>, a <c>std::array&lt;int8_t, N&gt;</c>.</summary>
    /// <param name="what">What is being read, for the error message.</param>
    /// <returns>The values.</returns>
    internal sbyte[] ReadSByteArray(string what)
    {
        int count = ReadCount(what);
        sbyte[] values = new sbyte[count];

        for (int index = 0; index < count; ++index)
        {
            values[index] = (sbyte)ReadByte();
        }

        return values;
    }

    /// <summary>Reads a count prefixed array of bytes, a <c>std::array&lt;uint8_t, N&gt;</c>.</summary>
    /// <param name="what">What is being read, for the error message.</param>
    /// <returns>The values.</returns>
    internal byte[] ReadByteArray(string what)
    {
        int count = ReadCount(what);
        byte[] values = new byte[count];

        for (int index = 0; index < count; ++index)
        {
            values[index] = ReadByte();
        }

        return values;
    }

    /// <summary>Reads a <c>uint32</c> count prefixed list of strings, a <c>std::vector&lt;std::string&gt;</c>.</summary>
    /// <returns>The texts.</returns>
    internal List<string> ReadStrings()
    {
        int count = ReadCount("texts of a list");
        List<string> values = new(count);

        for (int index = 0; index < count; ++index)
        {
            values.Add(ReadString());
        }

        return values;
    }

    /// <summary>Reads a <c>std::map&lt;uint8_t, int32_t&gt;</c>: a count and that many (key, value) pairs.</summary>
    /// <returns>The table.</returns>
    internal SortedDictionary<byte, int> ReadByteKeyedInt32Map()
    {
        int count = ReadCount("entries of a spell table");
        SortedDictionary<byte, int> table = [];

        for (int index = 0; index < count; ++index)
        {
            byte key = ReadByte();

            table[key] = ReadInt32();
        }

        return table;
    }

    /// <summary>Reads <paramref name="count"/> raw bytes.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidDataException">The stream ends early.</exception>
    internal byte[] ReadBytes(int count)
    {
        byte[] values = new byte[count];
        int read = _stream.ReadAtLeast(values, count, throwOnEndOfStream: false);

        if (read != count)
        {
            throw new InvalidDataException("The .fh2m header ends early.");
        }

        return values;
    }

    /// <summary>Reads whatever is left, the compressed payload.</summary>
    /// <returns>The remaining bytes.</returns>
    internal byte[] ReadToEnd()
    {
        using MemoryStream rest = new();
        _stream.CopyTo(rest);

        return rest.ToArray();
    }

    private void Fill(int count)
    {
        if (_stream.ReadAtLeast(_scratch.AsSpan(0, count), count, throwOnEndOfStream: false) != count)
        {
            throw new InvalidDataException("The .fh2m header ends early.");
        }
    }
}

/// <summary>Big-endian writing of the <c>.fh2m</c> header, the <c>OStreamBase</c> of the reference.</summary>
internal sealed class BigEndianWriter
{
    private readonly Stream _stream;
    private readonly byte[] _scratch = new byte[4];

    internal BigEndianWriter(Stream stream) => _stream = stream;

    internal void WriteByte(byte value) => _stream.WriteByte(value);

    internal void WriteBool(bool value) => _stream.WriteByte(value ? (byte)1 : (byte)0);

    internal void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(_scratch, value);
        _stream.Write(_scratch, 0, 2);
    }

    internal void WriteInt32(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_scratch, value);
        _stream.Write(_scratch, 0, 4);
    }

    internal void WriteCount(int count) => WriteInt32(count);

    internal void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_scratch, value);
        _stream.Write(_scratch, 0, 4);
    }

    internal void WriteInt16(short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(_scratch, value);
        _stream.Write(_scratch, 0, 2);
    }

    /// <summary>
    /// Writes a count prefixed array of big-endian <c>int32</c>; the reference writes the count for
    /// <c>std::array</c> and <c>std::vector</c> alike.
    /// </summary>
    /// <param name="values">The numbers.</param>
    internal void WriteInt32Array(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        WriteCount(values.Count);

        foreach (int value in values)
        {
            WriteInt32(value);
        }
    }

    /// <summary>Writes a count prefixed array of <c>int8</c>.</summary>
    /// <param name="values">The values.</param>
    internal void WriteSByteArray(IReadOnlyList<sbyte> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        WriteCount(values.Count);

        foreach (sbyte value in values)
        {
            WriteByte((byte)value);
        }
    }

    /// <summary>Writes a count prefixed array of bytes.</summary>
    /// <param name="values">The values.</param>
    internal void WriteByteArray(IReadOnlyList<byte> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        WriteCount(values.Count);

        foreach (byte value in values)
        {
            WriteByte(value);
        }
    }

    /// <summary>Writes a <c>uint32</c> count prefixed list of strings.</summary>
    /// <param name="values">The texts.</param>
    internal void WriteStrings(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        WriteCount(values.Count);

        foreach (string value in values)
        {
            WriteString(value);
        }
    }

    /// <summary>Writes a <c>std::map&lt;uint8_t, int32_t&gt;</c>: a count and that many key/value pairs.</summary>
    /// <param name="table">The table, written in key order.</param>
    internal void WriteByteKeyedInt32Map(IReadOnlyDictionary<byte, int> table)
    {
        ArgumentNullException.ThrowIfNull(table);

        WriteCount(table.Count);

        foreach ((byte key, int value) in table)
        {
            WriteByte(key);
            WriteInt32(value);
        }
    }

    internal void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        byte[] bytes = Encoding.UTF8.GetBytes(value);

        WriteCount(bytes.Length);
        _stream.Write(bytes, 0, bytes.Length);
    }

    internal void WriteUInt32Array(IReadOnlyList<uint> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        WriteCount(values.Count);

        foreach (uint value in values)
        {
            BinaryPrimitives.WriteUInt32BigEndian(_scratch, value);
            _stream.Write(_scratch, 0, 4);
        }
    }

    internal void WriteRaw(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        _stream.Write(data, 0, data.Length);
    }

    internal void Flush() => _stream.Flush();
}
