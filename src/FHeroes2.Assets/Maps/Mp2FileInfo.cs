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
/// Deviations from the reference implementation, both recorded in <c>docs/PROGRESS.md</c>:
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
public sealed record Mp2FileInfo
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

    /// <summary>Colors that must be played by a human (<c>FileInfo::HumanOnlyColors</c>).</summary>
    public PlayerColor HumanOnlyColors => HumanPlayers & ~ComputerPlayers;

    /// <summary>Colors that can only be played by the computer (<c>FileInfo::ComputerOnlyColors</c>).</summary>
    public PlayerColor ComputerOnlyColors => ComputerPlayers & ~HumanPlayers;

    /// <summary>Colors that either side may play (<c>FileInfo::AllowCompHumanColors</c>).</summary>
    public PlayerColor SharedColors => HumanPlayers & ComputerPlayers;

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
            KingdomColors = ReadColors(span, KingdomColorsOffset),
            HumanPlayers = humanPlayers,
            ComputerPlayers = ReadColors(span, ComputerColorsOffset),
            RandomRacePlayers = randomRacePlayers,
            PlayerRaces = races,
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
