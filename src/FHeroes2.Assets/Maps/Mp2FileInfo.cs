using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// The map information block of an MP2 map file: the leading 428 byte header that
/// <c>Maps::FileInfo::readMP2Map</c> (<c>maps_fileinfo.cpp</c>) reads. It carries everything the
/// scenario selection dialog shows — name, description, size, difficulty, the colors and races of the
/// players and both conditions — but no tile data, so it can be read without the map loader.
///
/// <para>
/// Deviations from the reference implementation, both recorded in <c>bd 归档（PROGRESS.md，FHeroes2Sharp-jwk）</c>:
/// <list type="bullet">
///   <item><description>
///   map strings are decoded as Latin-1 instead of the DOS code page the original editor used; the
///   shipped maps only contain ASCII text.
///   </description></item>
///   <item><description>
///   the "lose a specific hero on a computer owned tile" rewrite of the victory condition is not
///   applied yet: it needs the tile data and the hero sprite table of the adventure map renderer.
///   </description></item>
/// </list>
/// </para>
/// </summary>
public sealed partial record Mp2FileInfo
{
    /// <summary>Size of the map information block that precedes the tile data.</summary>
    public const int HeaderSize = 428;

    /// <summary>Offset of the NUL padded map name.</summary>
    public const int NameFieldOffset = 58;

    /// <summary>Length of the map name field.</summary>
    public const int NameFieldLength = 16;

    /// <summary>Offset of the NUL padded map description.</summary>
    public const int DescriptionFieldOffset = 118;

    /// <summary>Length of the map description field.</summary>
    public const int DescriptionFieldLength = 200;

    private const uint Magic = 0x5C00_0000;
    private const int DifficultyOffset = 4;
    private const int DimensionOffset = 6;
    private const int KingdomColorsOffset = 8;
    private const int HumanColorsOffset = 14;
    private const int ComputerColorsOffset = 20;
    private const int VictoryBlockOffset = 29;
    private const int RaceBlockOffset = 38;

    /// <summary>Full path of the map file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Name of the scenario, e.g. <c>Beltway</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Scenario description as written by the map maker; may be empty.</summary>
    public required string Description { get; init; }

    /// <summary>Width of the map in tiles.</summary>
    public required int Width { get; init; }

    /// <summary>Height of the map in tiles.</summary>
    public required int Height { get; init; }

    /// <summary>Difficulty level chosen by the map maker.</summary>
    public required GameDifficulty Difficulty { get; init; }

    /// <summary>Colors that have a kingdom on the map.</summary>
    public PlayerColor KingdomColors { get; init; }

    /// <summary>Colors that a human may control.</summary>
    public PlayerColor HumanPlayers { get; init; }

    /// <summary>Colors that the computer may control.</summary>
    public PlayerColor ComputerPlayers { get; init; }

    /// <summary>Colors whose starting race is random.</summary>
    public PlayerColor RandomRacePlayers { get; init; }

    /// <summary>Starting race per player index (0 = blue … 5 = purple).</summary>
    public IReadOnlyList<MapRace> PlayerRaces { get; init; } =
        [MapRace.None, MapRace.None, MapRace.None, MapRace.None, MapRace.None, MapRace.None];

    /// <summary>Victory condition of the scenario.</summary>
    public MapVictoryCondition VictoryCondition { get; init; }

    /// <summary>Whether the victory condition also applies to computer players.</summary>
    public bool ComputerAlsoWins { get; init; }

    /// <summary>Whether defeating everyone still counts as a victory.</summary>
    public bool AllowNormalVictory { get; init; }

    /// <summary>Two parameters of the victory condition (target position, artifact, gold, …).</summary>
    public IReadOnlyList<int> VictoryConditionParameters { get; init; } = [0, 0];

    /// <summary>Loss condition of the scenario.</summary>
    public MapLossCondition LossCondition { get; init; }

    /// <summary>Two parameters of the loss condition (target position, day count, …).</summary>
    public IReadOnlyList<int> LossConditionParameters { get; init; } = [0, 0];

    /// <summary>Whether the game starts with a hero in the first castle.</summary>
    public bool StartWithHeroInFirstCastle { get; init; }

    /// <summary>Product the map belongs to; derived from the file extension.</summary>
    public MapVersion Version { get; init; }

    /// <summary>
    /// The colors that fight on the same side as each player, per player index, <c>FileInfo::unions</c>
    /// (<c>maps_fileinfo.h:220</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每一项都是一个颜色集合（<see cref="PlayerColor"/> 是位标志），不是「联盟领袖色」：同一方的所有人都拿到
    /// 同一份集合。默认每人自成一方，即 <see cref="PlayerColors.Ordered"/> 本身（<c>maps_fileinfo.cpp:157-160</c>）。
    /// </para>
    /// <para>
    /// MP2 头在「击败另一方」的地图上按胜利条件的第 0 个参数把王国颜色切成两侧（<c>maps_fileinfo.cpp:341-371</c>）；
    /// 存档文件直接存这张表（<c>maps_fileinfo.cpp:664-675</c>）。
    /// </para>
    /// </remarks>
    public IReadOnlyList<PlayerColor> PlayerUnions { get; init; } =
    [
        PlayerColor.Blue,
        PlayerColor.Green,
        PlayerColor.Red,
        PlayerColor.Yellow,
        PlayerColor.Orange,
        PlayerColor.Purple,
    ];

    /// <summary>
    /// When the save was written, in seconds since the Unix epoch, <c>FileInfo::timestamp</c>.
    /// </summary>
    /// <remarks>Only a save file carries it; the save list sorts on it so the newest file comes first.</remarks>
    public uint Timestamp { get; init; }

    /// <summary>The day of the world date when the save was written, <c>FileInfo::worldDay</c>.</summary>
    public uint WorldDay { get; init; }

    /// <summary>The week of the world date when the save was written, <c>FileInfo::worldWeek</c>.</summary>
    public uint WorldWeek { get; init; }

    /// <summary>The month of the world date when the save was written, <c>FileInfo::worldMonth</c>.</summary>
    public uint WorldMonth { get; init; }

    /// <summary>
    /// The language the map's texts are in, as the raw <c>SupportedLanguage</c> code of the save file.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>FileInfo::mainLanguage</c> 是 <c>fheroes2::SupportedLanguage</c>，那个枚举在 Game 层；这一层只存原始
    /// 字节，换算交给 <c>SaveFileInfo</c>。0 是英文，也是「不知道」时用的值。
    /// </remarks>
    public byte MainLanguage { get; init; }

    /// <summary>Notes the map maker left in a map of the fheroes2 format, <c>FileInfo::creatorNotes</c>.</summary>
    public string CreatorNotes { get; init; } = string.Empty;

    /// <summary>
    /// The 428 header bytes the map was read from, <see langword="null"/> when the header was built
    /// from the fields alone (a new map).
    /// </summary>
    /// <remarks>
    /// The header holds fields this record does not model — the AI settings, the "unknown" bytes
    /// around the conditions and reserved padding — so writing a map back starts from the original
    /// image and only overwrites what is modelled here. A map that was read and written again
    /// therefore keeps every byte the reader ignored.
    /// </remarks>
    public byte[]? HeaderBytes { get; init; }

    /// <summary>
    /// Builds the <see cref="HeaderSize"/> byte header this record describes: the original image when
    /// there is one, a zeroed header otherwise, with every modelled field written over it.
    /// </summary>
    /// <returns>A new array of exactly <see cref="HeaderSize"/> bytes.</returns>
    public byte[] ToHeaderBytes()
    {
        byte[] header = HeaderBytes is { Length: HeaderSize } original
            ? (byte[])original.Clone()
            : new byte[HeaderSize];

        // The magic is the one big endian value of the file, everything else is little endian.
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(), Magic);

        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(DifficultyOffset), (ushort)Difficulty);
        header[DimensionOffset] = (byte)Width;
        header[DimensionOffset + 1] = (byte)Height;
        WriteColors(header, KingdomColorsOffset, KingdomColors);
        WriteColors(header, HumanColorsOffset, HumanPlayers);
        WriteColors(header, ComputerColorsOffset, ComputerPlayers);

        header[VictoryBlockOffset] = (byte)VictoryCondition;
        header[VictoryBlockOffset + 1] = (byte)(ComputerAlsoWins ? 1 : 0);
        header[VictoryBlockOffset + 2] = (byte)(AllowNormalVictory ? 1 : 0);
        WriteParameter(header, VictoryBlockOffset + 3, VictoryConditionParameters, 0);
        header[VictoryBlockOffset + 5] = (byte)LossCondition;
        WriteParameter(header, VictoryBlockOffset + 6, LossConditionParameters, 0);
        header[VictoryBlockOffset + 8] = (byte)(StartWithHeroInFirstCastle ? 0 : 1);
        WriteParameter(header, VictoryBlockOffset + 15, VictoryConditionParameters, 1);
        WriteParameter(header, VictoryBlockOffset + 17, LossConditionParameters, 1);

        for (int index = 0; index < PlayerColors.Ordered.Count; ++index)
        {
            MapRace race = index < PlayerRaces.Count ? PlayerRaces[index] : MapRace.None;

            // A race the header cannot hold keeps the byte it already had: the shipped maps put 255
            // in the slots of players the map does not use, and a header built from zeros keeps zero.
            if (MapRaces.ToIndex(race) is { } stored)
            {
                header[RaceBlockOffset + index] = (byte)stored;
            }
        }

        // The two text fields keep their original bytes when the text did not change. A field is only
        // sixteen or two hundred bytes wide and the map makers sometimes put more than one string in
        // it - Alteris stores "10" right after the name's NUL - so rewriting the whole field would
        // drop text that the reader never showed.
        if (HeaderBytes is null
            || !string.Equals(ReadField(header, NameFieldOffset, NameFieldLength), Name, StringComparison.Ordinal))
        {
            MapTextCodec.Encode(Name, NameFieldLength).CopyTo(header, NameFieldOffset);
        }

        if (HeaderBytes is null
            || !string.Equals(
                ReadField(header, DescriptionFieldOffset, DescriptionFieldLength),
                Description,
                StringComparison.Ordinal))
        {
            MapTextCodec.Encode(Description, DescriptionFieldLength).CopyTo(header, DescriptionFieldOffset);
        }

        return header;
    }

    /// <summary>Colors that must be played by a human (<c>FileInfo::HumanOnlyColors</c>).</summary>
    public PlayerColor HumanOnlyColors => HumanPlayers & ~ComputerPlayers;

    /// <summary>Colors that can only be played by the computer (<c>FileInfo::ComputerOnlyColors</c>).</summary>
    public PlayerColor ComputerOnlyColors => ComputerPlayers & ~HumanPlayers;

    /// <summary>Colors that either side may play (<c>FileInfo::AllowCompHumanColors</c>).</summary>
    public PlayerColor SharedColors => HumanPlayers & ComputerPlayers;

    /// <summary>
    /// Whether the race of a color may still be picked, <c>FileInfo::AllowChangeRace()</c>
    /// (<c>maps_fileinfo.h:140-143</c>).
    /// </summary>
    /// <param name="color">The color to ask about.</param>
    /// <returns>True when the map left that player's race open.</returns>
    public bool AllowChangeRace(PlayerColor color) => (RandomRacePlayers & color) != PlayerColor.None;

    /// <summary>Number of colors a human may control.</summary>
    public int HumanPlayerCount => PlayerColors.Count(HumanPlayers);

    /// <summary>Number of colors the computer may control.</summary>
    public int ComputerPlayerCount => PlayerColors.Count(ComputerPlayers);

    /// <summary>Number of kingdoms on the map.</summary>
    public int PlayerCount => PlayerColors.Count(KingdomColors);

    /// <summary>Starting race of <paramref name="color"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not exactly one playable color.</exception>
    public MapRace RaceOf(PlayerColor color) => PlayerRaces[PlayerColors.IndexOf(color)];

    /// <summary>
    /// Copy of this information with the human colors narrowed down, mirroring
    /// <c>FileInfo::removeHumanColors</c> of the scenario list: when a map has exactly as many
    /// human-only colors as the requested number of players, those are the ones to choose from.
    /// </summary>
    public Mp2FileInfo RestrictHumanColorsTo(PlayerColor colors) => this with { HumanPlayers = colors & PlayerColors.All };

    /// <summary>
    /// Short human readable summary, the port of <c>Maps::FileInfo::getSummary</c>. Used by the
    /// diagnostic tools and the tests, never inside the game.
    /// </summary>
    public string GetSummary()
    {
        StringBuilder builder = new();
        builder.Append("Map name: ").AppendLine(Name);
        builder.Append("File name: ").AppendLine(FilePath);
        builder.Append("Description: ").AppendLine(Description);
        builder.Append("Size: ").Append(Width.ToString(CultureInfo.InvariantCulture))
            .Append('x').AppendLine(Height.ToString(CultureInfo.InvariantCulture));
        builder.Append("Difficulty: ").AppendLine(Difficulty.ToString());
        return builder.ToString();
    }

    /// <summary>
    /// Reads the information block of <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path of an <c>.mp2</c> (or <c>.mx2</c>) map file.</param>
    /// <param name="forEditor">
    /// When <see langword="true"/>, maps without a human playable color are accepted, mirroring the
    /// <c>isForEditor</c> argument of <c>readMP2Map</c>.
    /// </param>
    /// <exception cref="BinaryFormatException">The file is not a usable map file.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static Mp2FileInfo Read(string filePath, bool forEditor = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        byte[] header = ReadHeader(filePath);
        ReadOnlySpan<byte> span = header;

        if (BinaryPrimitives.ReadUInt32BigEndian(span) != Magic)
        {
            throw new BinaryFormatException($"'{filePath}' is not a map file: the magic value does not match.");
        }

        int rawDifficulty = BinaryPrimitives.ReadUInt16LittleEndian(span[DifficultyOffset..]);
        if (rawDifficulty > (int)GameDifficulty.Expert)
        {
            throw new BinaryFormatException($"'{filePath}' has an unsupported difficulty level {rawDifficulty}.");
        }

        PlayerColor humanPlayers = ReadColors(span, HumanColorsOffset);
        if (!forEditor && humanPlayers == PlayerColor.None)
        {
            throw new BinaryFormatException($"'{filePath}' cannot be played: no human player is allowed.");
        }

        string name = ReadField(span, NameFieldOffset, NameFieldLength);
        if (name.Length == 0)
        {
            throw new BinaryFormatException($"'{filePath}' has no map name.");
        }

        int victoryType = span[VictoryBlockOffset];
        int lossType = span[VictoryBlockOffset + 5];
        PlayerColor kingdomColors = ReadColors(span, KingdomColorsOffset);

        // 阵营表（maps_fileinfo.cpp:341-371）：默认每人自成一方；「击败另一方」的地图按胜利条件参数 0
        // 把王国颜色切成两侧，同一侧的人共享视野、胜负与外交。参数非法时参考整张图读不出来，这里抛出。
        PlayerColor[] unions = [.. PlayerColors.Ordered];

        if (victoryType == (int)MapVictoryCondition.DefeatOtherSide)
        {
            IReadOnlyList<PlayerColor> available = PlayerColors.ToList(kingdomColors);
            int side1Count = BinaryPrimitives.ReadUInt16LittleEndian(span[(VictoryBlockOffset + 3)..]);

            if (available.Count == 0 || side1Count <= 0 || side1Count >= available.Count)
            {
                throw new BinaryFormatException(
                    $"'{filePath}' has an invalid victory condition parameter for an alliance map.");
            }

            PlayerColor side1 = PlayerColor.None;
            PlayerColor side2 = PlayerColor.None;

            for (int index = 0; index < available.Count; ++index)
            {
                if (index < side1Count)
                {
                    side1 |= available[index];
                }
                else
                {
                    side2 |= available[index];
                }
            }

            unions = FillUnions(side1, side2);
        }

        List<MapRace> races = [];
        PlayerColor randomRacePlayers = PlayerColor.None;
        for (int index = 0; index < PlayerColors.Ordered.Count; ++index)
        {
            MapRace race = MapRaces.FromIndex(span[RaceBlockOffset + index]);
            races.Add(race);

            if (race == MapRace.Random)
            {
                randomRacePlayers |= PlayerColors.FromIndex(index);
            }
        }

        return new Mp2FileInfo
        {
            FilePath = Path.GetFullPath(filePath),
            Name = name,
            Description = ReadField(span, DescriptionFieldOffset, DescriptionFieldLength),
            Width = span[DimensionOffset],
            Height = span[DimensionOffset + 1],
            Difficulty = (GameDifficulty)rawDifficulty,
            KingdomColors = kingdomColors,
            HumanPlayers = humanPlayers,
            ComputerPlayers = ReadColors(span, ComputerColorsOffset),
            RandomRacePlayers = randomRacePlayers,
            PlayerRaces = races,
            PlayerUnions = unions,
            VictoryCondition = (MapVictoryCondition)victoryType,
            ComputerAlsoWins = span[VictoryBlockOffset + 1] != 0,
            AllowNormalVictory = span[VictoryBlockOffset + 2] != 0,
            VictoryConditionParameters =
            [
                BinaryPrimitives.ReadUInt16LittleEndian(span[(VictoryBlockOffset + 3)..]),
                BinaryPrimitives.ReadUInt16LittleEndian(span[(VictoryBlockOffset + 15)..]),
            ],
            LossCondition = (MapLossCondition)lossType,
            LossConditionParameters =
            [
                BinaryPrimitives.ReadUInt16LittleEndian(span[(VictoryBlockOffset + 6)..]),
                BinaryPrimitives.ReadUInt16LittleEndian(span[(VictoryBlockOffset + 17)..]),
            ],
            StartWithHeroInFirstCastle = span[VictoryBlockOffset + 8] == 0,
            Version = VersionFromPath(filePath),
            HeaderBytes = header,
        };
    }

    /// <summary>
    /// Same as <see cref="Read"/> but returns <see langword="false"/> instead of throwing when the
    /// file is not a usable map (a corrupt or hacked map, or a missing file).
    /// </summary>
    public static bool TryRead(string filePath, out Mp2FileInfo? info, bool forEditor = false)
    {
        try
        {
            info = Read(filePath, forEditor);
            return true;
        }
        catch (Exception exception) when (
            exception is BinaryFormatException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            info = null;
            return false;
        }
    }

    /// <summary>
    /// Builds the alliance table for two sides, <c>FileInfo::FillUnions()</c> (<c>maps_fileinfo.cpp:542-559</c>).
    /// </summary>
    /// <param name="side1">The colors of the first side.</param>
    /// <param name="side2">The colors of the second side.</param>
    /// <returns>One color set per player index; a color outside both sides stands alone.</returns>
    private static PlayerColor[] FillUnions(PlayerColor side1, PlayerColor side2)
    {
        PlayerColor[] unions = new PlayerColor[PlayerColors.Ordered.Count];

        for (int index = 0; index < unions.Length; ++index)
        {
            PlayerColor color = PlayerColors.Ordered[index];

            unions[index] = (side1 & color) != 0 ? side1 : (side2 & color) != 0 ? side2 : color;
        }

        return unions;
    }

    private static byte[] ReadHeader(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);

        if (stream.Length < HeaderSize)
        {
            throw new BinaryFormatException(
                $"'{filePath}' is too short to be a map file: {stream.Length} bytes instead of {HeaderSize}.");
        }

        byte[] header = new byte[HeaderSize];
        stream.ReadExactly(header);
        return header;
    }

    private static PlayerColor ReadColors(ReadOnlySpan<byte> header, int offset)
    {
        PlayerColor colors = PlayerColor.None;
        for (int index = 0; index < PlayerColors.Ordered.Count; ++index)
        {
            if (header[offset + index] != 0)
            {
                colors |= PlayerColors.FromIndex(index);
            }
        }

        return colors;
    }

    private static string ReadField(ReadOnlySpan<byte> header, int offset, int length) =>
        MapTextCodec.Decode(header.Slice(offset, length));

    /// <summary>
    /// Writes the six color bytes of one player group. A set bit only raises a zero byte to one:
    /// the headers of the shipped maps sometimes hold other non zero values, and those mean "set"
    /// as well, so overwriting them would change a file that is only being written back unchanged.
    /// </summary>
    private static void WriteColors(Span<byte> header, int offset, PlayerColor colors)
    {
        for (int index = 0; index < PlayerColors.Ordered.Count; ++index)
        {
            if ((colors & PlayerColors.Ordered[index]) == 0)
            {
                header[offset + index] = 0;
            }
            else if (header[offset + index] == 0)
            {
                header[offset + index] = 1;
            }
        }
    }

    /// <summary>Writes one 16 bit condition parameter, zero when the list is shorter.</summary>
    private static void WriteParameter(Span<byte> header, int offset, IReadOnlyList<int> parameters, int index)
    {
        int value = index < parameters.Count ? Math.Clamp(parameters[index], 0, ushort.MaxValue) : 0;
        BinaryPrimitives.WriteUInt16LittleEndian(header[offset..], (ushort)value);
    }

    private static MapVersion VersionFromPath(string filePath)
    {
        string extension = Path.GetExtension(filePath);

        if (extension.Equals(".mx2", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".hxc", StringComparison.OrdinalIgnoreCase))
        {
            return MapVersion.PriceOfLoyalty;
        }

        return extension.Equals(".fh2m", StringComparison.OrdinalIgnoreCase)
            ? MapVersion.Resurrection
            : MapVersion.SuccessionWars;
    }
}
