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

    /// <summary>Name the editor gives a map that was created instead of opened.</summary>
    public const string NewMapName = "Untitled";

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

    /// <summary>
    /// All add-ons; index 0 is a placeholder that means "no add-on". Read only as an interface, but
    /// <see cref="AppendAddon"/> and <see cref="SetAddonAt"/> grow it in place, the same way
    /// <see cref="SetTileAt"/> edits the tiles.
    /// </summary>
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
    public uint LastObjectUid
    {
        get => _lastObjectUid;
        init => _lastObjectUid = value;
    }

    /// <summary>Backing field of <see cref="LastObjectUid"/>, which the editor advances.</summary>
    private uint _lastObjectUid;

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
    /// Whether a map of <paramref name="width"/> by <paramref name="height"/> tiles fits the format,
    /// which only knows four square sizes.
    /// </summary>
    public static bool IsSupportedSize(int width, int height) =>
        width == height && Array.IndexOf(SupportedDimensions, width) >= 0;

    /// <summary>
    /// Creates an empty square map: every tile is the same plain ground image with no object on it,
    /// the castle and capturable object tables are empty and no information block exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editor starts here when it is asked for a new map instead of a file. There is no file to
    /// patch, so such a map can only be written by <see cref="Mp2MapWriter.WriteAll"/>, which builds
    /// the header from the fields above because <see cref="Mp2FileInfo.HeaderBytes"/> is empty.
    /// </para>
    /// <para>
    /// <see cref="Addons"/> holds one zeroed entry instead of none: index 0 is the "no add-on value"
    /// of the format, and the shipped maps all keep the placeholder entry.
    /// </para>
    /// </remarks>
    /// <param name="size">Width and height in tiles, 36, 72, 108 or 144.</param>
    /// <param name="terrainImageIndex">Terrain image every tile starts with, the ground of the map.</param>
    /// <param name="name">Name stored in the header, <see cref="NewMapName"/> by default.</param>
    /// <returns>A map that is not backed by a file; saving it needs a target path.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not a map size.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static Mp2Map CreateBlank(int size, ushort terrainImageIndex, string name = NewMapName)
    {
        if (!IsSupportedSize(size, size))
        {
            throw new ArgumentOutOfRangeException(
                nameof(size), size, "A map is 36, 72, 108 or 144 tiles wide and high.");
        }

        ArgumentNullException.ThrowIfNull(name);

        Mp2TileInfo empty = new()
        {
            TerrainImageIndex = terrainImageIndex,
            ObjectName1 = 0,
            BottomIcnImageIndex = 0,
            Quantity1 = 0,
            Quantity2 = 0,
            ObjectName2 = 0,
            TopIcnImageIndex = 0,
            TerrainFlags = 0,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

        Mp2TileInfo[] tiles = new Mp2TileInfo[size * size];
        Array.Fill(tiles, empty);

        return new Mp2Map
        {
            Info = new Mp2FileInfo
            {
                FilePath = string.Empty,
                Name = name,
                Description = string.Empty,
                Width = size,
                Height = size,
                Difficulty = GameDifficulty.Normal,
                KingdomColors = PlayerColors.All,
                HumanPlayers = PlayerColors.All,
                ComputerPlayers = PlayerColors.All,
                PlayerRaces = [.. Enumerable.Repeat(MapRace.Knight, PlayerColors.Ordered.Count)],
                Version = MapVersion.SuccessionWars,
            },
            Tiles = tiles,
            Addons = new List<Mp2AddonInfo> { default },
            Castles = [],
            CaptureObjects = [],
            Heroes = [],
            ObeliskCount = 0,
            InfoBlocks = [],
            LastObjectUid = 0,
        };
    }

    /// <summary>
    /// Replaces the tile at <paramref name="index"/> and returns the value it had.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one in place mutation of the record, added for the map editor. A brush stroke rewrites a
    /// handful of tiles per frame, so returning a copy of the whole map (<c>width * height * 20</c>
    /// bytes, 233 KB on a 108x108 map) per painted tile would turn a drag into a garbage collector
    /// stutter. The editor keeps the previous value of every tile it touched, which is what makes the
    /// mutation undoable.
    /// </para>
    /// <para>
    /// Two maps that were built around the same tile array alias each other; nothing does that today,
    /// and <see cref="Tiles"/> stays a read only view so only this method can change it.
    /// </para>
    /// </remarks>
    /// <param name="index">Index of the tile, <c>x + y * width</c>.</param>
    /// <param name="tile">The new value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the tile array.</exception>
    /// <exception cref="NotSupportedException">The tile list is not backed by an array.</exception>
    public Mp2TileInfo SetTileAt(int index, Mp2TileInfo tile)
    {
        if (index < 0 || index >= Tiles.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{index} is outside the {Tiles.Count} tiles of the map.");
        }

        if (Tiles is not Mp2TileInfo[] tiles)
        {
            throw new NotSupportedException("The tiles of this map are not backed by an array, so they cannot be edited.");
        }

        Mp2TileInfo previous = tiles[index];
        tiles[index] = tile;
        return previous;
    }

    /// <summary>
    /// Replaces the tile at <paramref name="x"/>, <paramref name="y"/> and returns the value it had,
    /// the indexed form of <see cref="SetTileAt"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The point is outside the map.</exception>
    public Mp2TileInfo SetTile(int x, int y, Mp2TileInfo tile)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}; {y}) is outside the {Width}x{Height} map.");
        }

        return SetTileAt(TileIndex(x, y), tile);
    }

    /// <summary>
    /// Takes the next object UID and advances the counter, the identifier a placed object gets.
    /// </summary>
    /// <remarks>
    /// The counter is the last field of the file, so a map written by <see cref="Mp2MapWriter"/>
    /// carries it back; a map that reuses a UID would make two objects indistinguishable to the game.
    /// The counter starts at 0 on a new map, so the first object is 1, which keeps 0 meaning "no
    /// object" on every tile that does not hold one.
    /// </remarks>
    /// <returns>A UID no other object of this map uses.</returns>
    public uint TakeObjectUid()
    {
        _lastObjectUid = unchecked(_lastObjectUid + 1);
        return _lastObjectUid;
    }

    /// <summary>
    /// Appends an add-on and returns its index, which is what a tile or another add-on stores in
    /// <see cref="Mp2TileInfo.NextAddonIndex"/> or <see cref="Mp2AddonInfo.NextAddonIndex"/>.
    /// </summary>
    /// <remarks>
    /// A map read from a file whose add-on count was 0 has no placeholder entry, so the first append
    /// fills index 0 with the zeroed placeholder the format expects: index 0 means "no add-on" and
    /// must never hold a real piece.
    /// </remarks>
    /// <param name="addon">The piece to add; its <see cref="Mp2AddonInfo.NextAddonIndex"/> is written by the caller.</param>
    /// <returns>Index of the new add-on.</returns>
    /// <exception cref="NotSupportedException">The add-on list of this map cannot be changed.</exception>
    /// <exception cref="InvalidOperationException">The list is full, i.e. holds 65535 real pieces.</exception>
    public int AppendAddon(Mp2AddonInfo addon)
    {
        if (Addons is not List<Mp2AddonInfo> addons)
        {
            throw new NotSupportedException("The add-ons of this map are not backed by a list, so they cannot be edited.");
        }

        if (addons.Count == 0)
        {
            addons.Add(default);
        }

        if (addons.Count >= ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"The map already has {addons.Count} add-ons, which is as many as a 16 bit link can address.");
        }

        addons.Add(addon);
        return addons.Count - 1;
    }

    /// <summary>Replaces the add-on at <paramref name="index"/>, the inverse of the read loop.</summary>
    /// <param name="index">Index into <see cref="Addons"/>, 1 or more: 0 is the placeholder.</param>
    /// <param name="addon">The new value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the list or is 0.</exception>
    /// <exception cref="NotSupportedException">The add-on list of this map cannot be changed.</exception>
    public void SetAddonAt(int index, Mp2AddonInfo addon)
    {
        if (Addons is not List<Mp2AddonInfo> addons)
        {
            throw new NotSupportedException("The add-ons of this map are not backed by a list, so they cannot be edited.");
        }

        if (index <= 0 || index >= addons.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"Add-on indexes run from 1 to {addons.Count - 1}; 0 is the placeholder.");
        }

        addons[index] = addon;
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

        if (!IsSupportedSize(info.Width, info.Height))
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
            Addons = new List<Mp2AddonInfo>(addons),
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
