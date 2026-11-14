using System.Buffers.Binary;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// One entry of the castle position table of an MP2 file. The table always has
/// <see cref="Mp2Map.CastleCount"/> entries; unused slots are marked by <c>0xFF</c> in both
/// coordinates and are not returned.
/// </summary>
/// <param name="X">Column of the castle entrance.</param>
/// <param name="Y">Row of the castle entrance.</param>
/// <param name="RawType">The byte stored in the file, <c>0x00…0x06</c> for a town and <c>0x80…0x86</c> for a fortified castle.</param>
/// <param name="Race">Race derived from <paramref name="RawType"/>.</param>
public sealed record Mp2CastlePosition(int X, int Y, int RawType, MapRace Race)
{
    /// <summary>Whether the castle is already fortified.</summary>
    public bool HasFort => (RawType & 0x80) != 0;
}

/// <summary>
/// One entry of the capturable object table of an MP2 file. The table always has
/// <see cref="Mp2Map.CaptureObjectCount"/> entries; unused slots are not returned.
/// </summary>
/// <param name="X">Column of the object.</param>
/// <param name="Y">Row of the object.</param>
/// <param name="RawType">The byte stored in the file.</param>
/// <param name="ObjectType">Object type the byte stands for, as resolved by the reference implementation.</param>
public sealed record Mp2CaptureObject(int X, int Y, int RawType, Mp2ObjectType ObjectType);

/// <summary>
/// A trailing information block of an MP2 file: the castle, hero, sign, bottle, event, sphinx,
/// daily event and rumor payloads. The blocks are stored as a size prefixed byte array; what the
/// bytes mean depends on the object that refers to them, which is why they are kept raw here.
/// </summary>
/// <param name="Index">Zero based index in the block list.</param>
/// <param name="Data">The block payload without its two byte size prefix.</param>
public sealed record Mp2InfoBlock(int Index, byte[] Data)
{
    /// <summary>Index the map file uses to refer to this block: <c>Index + 1</c>, because 0 means "no block".</summary>
    public int ObjectId => Index + 1;
}

/// <summary>
/// The complete tile level content of an MP2 map file, the port of the first half of
/// <c>World::loadMapMP2</c> (<c>world_loadmap.cpp</c>). It reads everything the file stores about
/// the map — tiles, add-ons, the castle and capturable object tables, the obelisk counter and the
/// information blocks — but builds no game objects: kingdoms, heroes, castles and the adventure map
/// renderer all consume this data later.
///
/// <para>
/// The layout of the file is, in this order:
/// <list type="number">
///   <item><description>the <see cref="Mp2FileInfo.HeaderSize"/> byte information block;</description></item>
///   <item><description><c>width * height</c> tile structures of <see cref="Mp2TileInfo.StructureSize"/> bytes;</description></item>
///   <item><description>a 32 bit add-on count followed by that many add-on structures of <see cref="Mp2AddonInfo.StructureSize"/> bytes;</description></item>
///   <item><description><see cref="CastleCount"/> castle positions of three bytes each;</description></item>
///   <item><description><see cref="CaptureObjectCount"/> capturable object positions of three bytes each;</description></item>
///   <item><description>the number of obelisks;</description></item>
///   <item><description>a list of 16 bit counts terminated by two zero bytes, then the information blocks;</description></item>
///   <item><description>the 32 bit object UID counter, which is the last thing in the file.</description></item>
/// </list>
/// </para>
/// </summary>
public sealed record Mp2Map
{
    /// <summary>Number of castle position entries, fixed by the format.</summary>
    public const int CastleCount = 72;

    /// <summary>Size of one castle position entry.</summary>
    public const int CastlePositionSize = 3;

    /// <summary>Number of capturable object entries, fixed by the format.</summary>
    public const int CaptureObjectCount = 144;

    /// <summary>Size of one capturable object entry.</summary>
    public const int CaptureObjectPositionSize = 3;

    /// <summary>Size of the add-on count that precedes the add-on array.</summary>
    public const int AddonCountSize = 4;

    /// <summary>Size of the trailing object UID counter.</summary>
    public const int ObjectUidSize = 4;

    private static readonly int[] SupportedDimensions = [36, 72, 108, 144];

    private static readonly MapRace[] CastleRaces =
    [
        MapRace.Knight,
        MapRace.Barbarian,
        MapRace.Sorceress,
        MapRace.Warlock,
        MapRace.Wizard,
        MapRace.Necromancer,
        MapRace.None,
    ];

    /// <summary>Information block of the map, read from the leading 428 bytes.</summary>
    public required Mp2FileInfo Info { get; init; }

    /// <summary>All tiles, row by row, <c>index = x + y * width</c>.</summary>
    public required IReadOnlyList<Mp2TileInfo> Tiles { get; init; }

    /// <summary>All add-ons; index 0 is a placeholder that means "no add-on".</summary>
    public required IReadOnlyList<Mp2AddonInfo> Addons { get; init; }

    /// <summary>Non empty entries of the castle position table.</summary>
    public required IReadOnlyList<Mp2CastlePosition> Castles { get; init; }

    /// <summary>Non empty entries of the capturable object table.</summary>
    public required IReadOnlyList<Mp2CaptureObject> CaptureObjects { get; init; }

    /// <summary>Heroes standing on the map, in tile order.</summary>
    public required IReadOnlyList<Mp2MapHero> Heroes { get; init; }

    /// <summary>The obelisk counter stored right after the capturable object table.</summary>
    public int ObeliskCount { get; init; }

    /// <summary>Information blocks, in file order.</summary>
    public required IReadOnlyList<Mp2InfoBlock> InfoBlocks { get; init; }

    /// <summary>The object UID counter stored in the last four bytes of the file.</summary>
    public uint LastObjectUid { get; init; }

    /// <summary>Width of the map in tiles.</summary>
    public int Width => Info.Width;

    /// <summary>Height of the map in tiles.</summary>
    public int Height => Info.Height;

    /// <summary>Number of tiles, <c>width * height</c>.</summary>
    public int TileCount => Tiles.Count;

    /// <summary>Index of the tile at <paramref name="x"/>, <paramref name="y"/>; no bounds check.</summary>
    public int TileIndex(int x, int y) => x + (y * Width);

    /// <summary>Tile at <paramref name="x"/>, <paramref name="y"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The point is outside the map.</exception>
    public Mp2TileInfo Tile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}; {y}) is outside the {Width}x{Height} map.");
        }

        return Tiles[TileIndex(x, y)];
    }

    /// <summary>
    /// Walks the add-on chain that starts at <paramref name="tile"/>. Every returned value is a valid
    /// index into <see cref="Addons"/>. The walk stops at index 0, at an out of range index (a
    /// corrupt chain) or after <see cref="Addons"/>.-1 steps, so a chain that loops cannot hang.
    /// </summary>
    public IEnumerable<int> EnumerateAddonChain(Mp2TileInfo tile)
    {
        int index = tile.NextAddonIndex;
        for (int step = 0; index > 0 && index < Addons.Count && step < Addons.Count; ++step)
        {
            yield return index;
            index = Addons[index].NextAddonIndex;
        }
    }

    /// <summary>
    /// Reads an MP2 map file, everything but the header included.
    /// </summary>
    /// <param name="filePath">Path of an <c>.mp2</c> (or <c>.mx2</c>) map file.</param>
    /// <param name="rejectPriceOfLoyaltyObjects">
    /// When <see langword="true"/>, maps that place The Price of Loyalty objects are rejected, which is
    /// what the reference implementation does for a file that is not recognised as an expansion map.
    /// </param>
    /// <exception cref="BinaryFormatException">The file is not a usable map file.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static Mp2Map Read(string filePath, bool rejectPriceOfLoyaltyObjects = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Mp2FileInfo info = Mp2FileInfo.Read(filePath);

        if (info.Width != info.Height || !SupportedDimensions.Contains(info.Width))
        {
            throw new BinaryFormatException(
                $"'{filePath}' has an unsupported map size {info.Width}x{info.Height}: only square maps of 36, 72, 108 or 144 tiles exist.");
        }

        using FileStream stream = File.OpenRead(filePath);
        using LittleEndianReader reader = new(stream);

        long totalSize = stream.Length;
        int worldSize = info.Width * info.Height;
        long tilesStart = Mp2FileInfo.HeaderSize;
        long tilesEnd = tilesStart + ((long)worldSize * Mp2TileInfo.StructureSize);

        if (totalSize < tilesEnd + AddonCountSize)
        {
            throw new BinaryFormatException(
                $"'{filePath}' is truncated: {totalSize} bytes, but a {info.Width}x{info.Height} map needs at least {tilesEnd + AddonCountSize}.");
        }

        uint lastObjectUid = ReadLastObjectUid(reader, totalSize);

        // The add-ons come first, right after the tile area; the tiles are read afterwards from their
        // own position because a tile has to know its add-on chain in advance.
        reader.Position = tilesEnd;
        uint addonCount = reader.ReadUInt32();
        long addonsEnd = reader.Position + ((long)addonCount * Mp2AddonInfo.StructureSize);

        if (addonsEnd > totalSize)
        {
            throw new BinaryFormatException(
                $"'{filePath}' is truncated: it claims {addonCount} add-ons but has no room for them.");
        }

        Mp2AddonInfo[] addons = new Mp2AddonInfo[addonCount];
        for (int index = 0; index < addons.Length; ++index)
        {
            addons[index] = Mp2AddonInfo.Read(reader);
        }

        reader.Position = tilesStart;
        Mp2TileInfo[] tiles = new Mp2TileInfo[worldSize];
        for (int index = 0; index < tiles.Length; ++index)
        {
            Mp2TileInfo tile = Mp2TileInfo.Read(reader);
            tile = FixLegacyPeasantHut(tile);

            if (rejectPriceOfLoyaltyObjects && IsPriceOfLoyaltyObject(tile.ObjectType))
            {
                throw new BinaryFormatException(
                    $"'{filePath}' is a The Price of Loyalty map: tile {index} places {tile.ObjectType}, which the original game does not know.");
            }

            tiles[index] = tile;
        }

        // Continue after the add-on array: castle table, capturable objects, obelisk counter, blocks.
        reader.Position = addonsEnd;
        long tablesEnd = addonsEnd + ((long)CastleCount * CastlePositionSize) + ((long)CaptureObjectCount * CaptureObjectPositionSize);

        if (tablesEnd > totalSize)
        {
            throw new BinaryFormatException(
                $"'{filePath}' is truncated: the castle and capturable object tables do not fit into the file.");
        }

        List<Mp2CastlePosition> castles = ReadCastles(reader);
        List<Mp2CaptureObject> captureObjects = ReadCaptureObjects(reader);
        int obeliskCount = reader.ReadByte();

        int infoBlockCount = ReadInfoBlockCount(reader, totalSize);
        List<Mp2InfoBlock> infoBlocks = ReadInfoBlocks(reader, infoBlockCount, totalSize);

        // The reference asserts that only the four byte UID counter is left; a map with trailing
        // garbage is accepted as long as every structure could be read.
        return new Mp2Map
        {
            Info = info,
            Tiles = tiles,
            Addons = addons,
            Castles = castles,
            CaptureObjects = captureObjects,
            Heroes = FindHeroes(tiles, info.Width),
            ObeliskCount = obeliskCount,
            InfoBlocks = infoBlocks,
            LastObjectUid = lastObjectUid,
        };
    }

    /// <summary>
    /// Collects the heroes of the map. Their position and sprite live on the tile, so this needs no
    /// information block: everything else about a hero (army, spells, artifacts) does.
    /// </summary>
    private static List<Mp2MapHero> FindHeroes(IReadOnlyList<Mp2TileInfo> tiles, int width)
    {
        List<Mp2MapHero> heroes = [];
        for (int index = 0; index < tiles.Count; ++index)
        {
            if (Mp2MapHeroes.TryRead(tiles[index], index % width, index / width, out Mp2MapHero hero))
            {
                heroes.Add(hero);
            }
        }

        return heroes;
    }

    /// <summary>Same as <see cref="Read"/> but returns <see langword="false"/> on any read failure.</summary>
    public static bool TryRead(string filePath, out Mp2Map? map, bool rejectPriceOfLoyaltyObjects = false)
    {
        try
        {
            map = Read(filePath, rejectPriceOfLoyaltyObjects);
            return true;
        }
        catch (Exception exception) when (
            exception is BinaryFormatException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            map = null;
            return false;
        }
    }

    /// <summary>Short human readable summary, used by <c>AssetProbe map</c> and by the tests.</summary>
    public string GetSummary()
    {
        int groundObjects = 0;
        int topObjects = 0;
        int chainedTiles = 0;

        foreach (Mp2TileInfo tile in Tiles)
        {
            if (tile.HasGroundObject)
            {
                ++groundObjects;
            }

            if (tile.HasTopObject)
            {
                ++topObjects;
            }

            if (tile.NextAddonIndex > 0)
            {
                ++chainedTiles;
            }
        }

        return $"Map {Info.Name}: {Width}x{Height} tiles, {Tiles.Count} tiles, " +
               $"{Addons.Count} add-ons ({chainedTiles} tiles with add-ons), " +
               $"ground objects {groundObjects}, top objects {topObjects}, " +
               $"castles {Castles.Count}, capturable {CaptureObjects.Count}, " +
               $"obelisks {ObeliskCount}, information blocks {InfoBlocks.Count}, " +
               $"last object UID {LastObjectUid}.";
    }

    private static bool IsPriceOfLoyaltyObject(Mp2ObjectType type) => type
        is Mp2ObjectType.Barrier
        or Mp2ObjectType.ExpansionDwelling
        or Mp2ObjectType.ExpansionObject
        or Mp2ObjectType.Jail
        or Mp2ObjectType.TravellerTent;

    /// <summary>
    /// The original editor writes the "Thatched Hut" as object type 65 (and 193 for the action
    /// variant) although it is the same object as the Peasant Hut; the reference rewrites both.
    /// </summary>
    private static Mp2TileInfo FixLegacyPeasantHut(Mp2TileInfo tile) => tile.ObjectType switch
    {
        (Mp2ObjectType)65 => tile with { ObjectType = Mp2ObjectType.PeasantHut },
        (Mp2ObjectType)193 => tile with { ObjectType = Mp2ObjectType.ActionPeasantHut },
        _ => tile,
    };

    private static uint ReadLastObjectUid(LittleEndianReader reader, long totalSize)
    {
        reader.Position = totalSize - ObjectUidSize;
        return reader.ReadUInt32();
    }

    private static List<Mp2CastlePosition> ReadCastles(LittleEndianReader reader)
    {
        List<Mp2CastlePosition> castles = new(CastleCount);

        for (int index = 0; index < CastleCount; ++index)
        {
            int x = reader.ReadByte();
            int y = reader.ReadByte();
            int rawType = reader.ReadByte();

            if (x == 0xFF && y == 0xFF)
            {
                continue;
            }

            int raceIndex = rawType & 0x7F;
            MapRace race = raceIndex < CastleRaces.Length ? CastleRaces[raceIndex] : MapRace.None;

            castles.Add(new Mp2CastlePosition(x, y, rawType, race));
        }

        return castles;
    }

    private static List<Mp2CaptureObject> ReadCaptureObjects(LittleEndianReader reader)
    {
        List<Mp2CaptureObject> objects = new(CaptureObjectCount);

        for (int index = 0; index < CaptureObjectCount; ++index)
        {
            int x = reader.ReadByte();
            int y = reader.ReadByte();
            int rawType = reader.ReadByte();

            if (x == 0xFF && y == 0xFF)
            {
                continue;
            }

            Mp2ObjectType objectType = rawType switch
            {
                0x00 => Mp2ObjectType.Sawmill,
                0x01 => Mp2ObjectType.AlchemistLab,
                0x02 or 0x03 or 0x04 or 0x05 or 0x06 => Mp2ObjectType.Mine,
                0x64 => Mp2ObjectType.Lighthouse,
                0x65 => Mp2ObjectType.DragonCity,
                0x67 => Mp2ObjectType.AbandonedMine,
                _ => Mp2ObjectType.None,
            };

            objects.Add(new Mp2CaptureObject(x, y, rawType, objectType));
        }

        return objects;
    }

    /// <summary>
    /// Reads the two byte pairs that precede the information blocks. Only the last non zero pair
    /// matters: it holds the block count plus one, which is the version marker of the format.
    /// </summary>
    private static int ReadInfoBlockCount(LittleEndianReader reader, long totalSize)
    {
        int count = 0;

        while (true)
        {
            if (reader.Remaining < 2)
            {
                throw new BinaryFormatException("The map file ends inside the information block count list.");
            }

            int low = reader.ReadByte();
            int high = reader.ReadByte();

            if (reader.Position == totalSize)
            {
                throw new BinaryFormatException("The map file ends before the information block count is terminated.");
            }

            if (low == 0 && high == 0)
            {
                return count;
            }

            count = ((256 * high) + low) - 1;
        }
    }

    private static List<Mp2InfoBlock> ReadInfoBlocks(LittleEndianReader reader, int count, long totalSize)
    {
        List<Mp2InfoBlock> blocks = new(Math.Max(count, 0));

        for (int index = 0; index < count; ++index)
        {
            // A zero size means "empty block": the reference logs it and moves on without consuming a payload.
            if (reader.Remaining < 2)
            {
                throw new BinaryFormatException($"The map file ends before information block {index + 1}.");
            }

            int size = reader.ReadUInt16();
            if (size == 0)
            {
                continue;
            }

            if (reader.Remaining < size)
            {
                throw new BinaryFormatException(
                    $"Information block {index + 1} claims {size} bytes but only {reader.Remaining} are left in the file.");
            }

            blocks.Add(new Mp2InfoBlock(index, reader.ReadBytes(size)));
        }

        return blocks;
    }
}
