using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// Writes an <see cref="Mp2Map"/> back to disk, in one of two shapes: a patch of the tiles of an
/// existing file (<see cref="Save"/>) or a complete file built from the map (<see cref="WriteAll"/>).
/// </summary>
/// <remarks>
/// <para>
/// The patch is the port of <c>World::saveMapMP2</c>: the original editor only rewrites the tiles it
/// changed and leaves the header, the add-on table, the castle and capturable object tables and the
/// information blocks as they were. It is the shape a save of an opened map uses, because those
/// sections still name the objects the editor never touched.
/// </para>
/// <para>
/// A map created by the editor has no such file to patch, so it is written whole. The header then
/// comes from <see cref="Mp2FileInfo.ToHeaderBytes"/>, which starts from the original image when there
/// is one and from zeros otherwise. Two tables cannot keep a hole: the castle and capturable object
/// tables are written packed, which is only visible on a map whose table had empty slots between used
/// ones, and a save of an opened map never takes this path.
/// </para>
/// <para>
/// The reference implementation of 1.1.18 no longer has an MP2 writer at all (its editor saves its own
/// compressed <c>Map_Format</c>), so there is nothing to compare the output with byte for byte. The
/// property that can be checked, and that the tests do check, is stronger: opening a map and saving it
/// again reproduces the file exactly, after an edit only the twenty bytes of the touched tiles differ,
/// and a map written whole can be read back into an equal map.
/// </para>
/// <para>
/// The target is written through a temporary file in the same directory, so a failure never leaves a
/// half written map behind.
/// </para>
/// </remarks>
public static class Mp2MapWriter
{
    /// <summary>Byte an unused slot of the castle and capturable object tables holds.</summary>
    private const byte EmptyObjectPosition = 0xFF;

    /// <summary>Saves <paramref name="map"/> over <paramref name="filePath"/>.</summary>
    /// <param name="filePath">File to write, created or replaced.</param>
    /// <param name="map">Map to write, normally one returned by <see cref="Mp2Map.Read"/>.</param>
    /// <param name="sourcePath">
    /// File whose untouched sections are kept; the path the map was read from by default.
    /// </param>
    /// <param name="writeHeader">
    /// Also overwrite the leading <see cref="Mp2FileInfo.HeaderSize"/> bytes with
    /// <see cref="Mp2FileInfo.ToHeaderBytes"/>. The editor needs it after its map specifications window
    /// replaced <see cref="Mp2Map.Info"/>; the header it produces still starts from the original image, so
    /// only the modelled fields change.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="BinaryFormatException">
    /// The source is too short for a map of this size, or the map does not hold one tile per position.
    /// </exception>
    public static void Save(string filePath, Mp2Map map, string? sourcePath = null, bool writeHeader = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(map);

        byte[] bytes = File.ReadAllBytes(sourcePath ?? map.Info.FilePath);

        if (writeHeader)
        {
            map.Info.ToHeaderBytes().CopyTo(bytes, 0);
        }

        WriteTiles(bytes, map);
        WriteAtomically(filePath, bytes);
    }

    /// <summary>
    /// Writes the tiles of <paramref name="map"/> into <paramref name="bytes"/>, the image of an MP2
    /// file; everything else in the image is left alone. Public so a test can compare the image
    /// without touching the disk.
    /// </summary>
    /// <param name="bytes">File image to patch in place, at least a header and a tile area long.</param>
    /// <param name="map">Map whose tiles are written.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="BinaryFormatException">The image or the map does not fit the layout.</exception>
    public static void WriteTiles(byte[] bytes, Mp2Map map)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(map);

        int worldSize = map.Width * map.Height;
        long tilesStart = Mp2FileInfo.HeaderSize;
        long tilesEnd = tilesStart + ((long)worldSize * Mp2TileInfo.StructureSize);

        if (map.Tiles.Count != worldSize)
        {
            throw new BinaryFormatException(
                $"The map is {map.Width}x{map.Height} but holds {map.Tiles.Count} tiles instead of {worldSize}.");
        }

        if (bytes.Length < tilesEnd + Mp2Map.AddonCountSize)
        {
            throw new BinaryFormatException(
                $"The file image is {bytes.Length} bytes, but a {map.Width}x{map.Height} map needs at least " +
                $"{tilesEnd + Mp2Map.AddonCountSize}.");
        }

        // The stream writes straight into the caller's buffer, so a save allocates nothing beyond the
        // file image itself.
        using MemoryStream stream = new(bytes, writable: true);
        using LittleEndianWriter writer = new(stream);

        stream.Position = tilesStart;

        foreach (Mp2TileInfo tile in map.Tiles)
        {
            tile.Write(writer);
        }
    }

    /// <summary>
    /// Writes the whole file — header, tiles, add-ons, both object tables, the obelisk count, the
    /// information blocks and the object identifier counter — which is what a map that was never read
    /// from a file needs.
    /// </summary>
    /// <param name="filePath">File to write, created or replaced.</param>
    /// <param name="map">Map to serialize, normally one from <see cref="Mp2Map.CreateBlank"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="BinaryFormatException"><see cref="Serialize"/> rejected the map.</exception>
    public static void WriteAll(string filePath, Mp2Map map)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(map);

        WriteAtomically(filePath, Serialize(map));
    }

    /// <summary>
    /// Builds the complete image of an MP2 file for <paramref name="map"/>. Public so a test can read
    /// the image back without touching the disk.
    /// </summary>
    /// <param name="map">Map to serialize.</param>
    /// <returns>The file, <see cref="Mp2FileInfo.HeaderSize"/> header bytes first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="BinaryFormatException">
    /// The map has no storable size, does not hold one tile per position, has more castles or
    /// capturable objects than the tables hold, has an obelisk count outside a byte, or has an
    /// information block outside the block numbering or longer than 65535 bytes.
    /// </exception>
    public static byte[] Serialize(Mp2Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (!Mp2Map.IsSupportedSize(map.Width, map.Height))
        {
            throw new BinaryFormatException(
                $"A {map.Width}x{map.Height} map cannot be stored: only square maps of 36, 72, 108 or 144 tiles exist.");
        }

        int worldSize = map.Width * map.Height;
        if (map.Tiles.Count != worldSize)
        {
            throw new BinaryFormatException(
                $"The map is {map.Width}x{map.Height} but holds {map.Tiles.Count} tiles instead of {worldSize}.");
        }

        if (map.Castles.Count > Mp2Map.CastleCount)
        {
            throw new BinaryFormatException(
                $"The map has {map.Castles.Count} castles, but the table only holds {Mp2Map.CastleCount}.");
        }

        if (map.CaptureObjects.Count > Mp2Map.CaptureObjectCount)
        {
            throw new BinaryFormatException(
                $"The map has {map.CaptureObjects.Count} capturable objects, but the table only holds {Mp2Map.CaptureObjectCount}.");
        }

        if (map.ObeliskCount is < 0 or > byte.MaxValue)
        {
            throw new BinaryFormatException($"The obelisk count {map.ObeliskCount} does not fit into a byte.");
        }

        (int blockCount, int payloadBytes, Dictionary<int, byte[]> blocks) = PlanInfoBlocks(map.InfoBlocks);

        int length = Mp2FileInfo.HeaderSize
            + (worldSize * Mp2TileInfo.StructureSize)
            + Mp2Map.AddonCountSize
            + (map.Addons.Count * Mp2AddonInfo.StructureSize)
            + (Mp2Map.CastleCount * Mp2Map.CastlePositionSize)
            + (Mp2Map.CaptureObjectCount * Mp2Map.CaptureObjectPositionSize)
            + 1
            + 4
            + payloadBytes
            + Mp2Map.ObjectUidSize;

        byte[] bytes = new byte[length];
        map.Info.ToHeaderBytes().CopyTo(bytes, 0);

        using MemoryStream stream = new(bytes, writable: true);
        using LittleEndianWriter writer = new(stream);

        stream.Position = Mp2FileInfo.HeaderSize;

        foreach (Mp2TileInfo tile in map.Tiles)
        {
            tile.Write(writer);
        }

        writer.WriteUInt32((uint)map.Addons.Count);

        foreach (Mp2AddonInfo addon in map.Addons)
        {
            addon.Write(writer);
        }

        WriteCastleTable(writer, map.Castles);
        WriteCaptureObjectTable(writer, map.CaptureObjects);

        writer.WriteByte((byte)map.ObeliskCount);

        // The reader treats the block count as a running version marker: it keeps the last non zero
        // pair and calls it "count plus one", then a zero pair ends the list. Written whole, one pair
        // is enough - the shipped maps carry a longer ladder of older values, which only matters when
        // their bytes are preserved, and that is what the patch of an opened map does.
        writer.WriteUInt16((ushort)(blockCount + 1));
        writer.WriteUInt16(0);

        for (int index = 0; index < blockCount; ++index)
        {
            if (blocks.TryGetValue(index, out byte[]? data))
            {
                writer.WriteUInt16((ushort)data.Length);
                writer.WriteBytes(data);
            }
            else
            {
                // The reader drops empty blocks but keeps numbering past them, so an index that was
                // dropped has to go back as an empty block or every block after it shifts.
                writer.WriteUInt16(0);
            }
        }

        writer.WriteUInt32(map.LastObjectUid);

        if (stream.Position != length)
        {
            throw new BinaryFormatException(
                $"The serialized map is {stream.Position} bytes, but the layout says {length}.");
        }

        return bytes;
    }

    /// <summary>
    /// Numbers the information blocks and measures their payload. The count is one past the highest
    /// index, because a block the reader dropped keeps its slot as an empty one.
    /// </summary>
    private static (int Count, int PayloadBytes, Dictionary<int, byte[]> Blocks) PlanInfoBlocks(
        IReadOnlyList<Mp2InfoBlock> infoBlocks)
    {
        Dictionary<int, byte[]> blocks = [];
        int count = 0;

        foreach (Mp2InfoBlock block in infoBlocks)
        {
            if (block.Index < 0)
            {
                throw new BinaryFormatException($"Information block {block.Index} cannot be stored.");
            }

            if (block.Data.Length > ushort.MaxValue)
            {
                throw new BinaryFormatException(
                    $"Information block {block.Index} holds {block.Data.Length} bytes, more than a block can store.");
            }

            blocks[block.Index] = block.Data;
            count = Math.Max(count, block.Index + 1);
        }

        int payload = 0;
        for (int index = 0; index < count; ++index)
        {
            payload += 2 + (blocks.TryGetValue(index, out byte[]? data) ? data.Length : 0);
        }

        return (count, payload, blocks);
    }

    private static void WriteCastleTable(LittleEndianWriter writer, IReadOnlyList<Mp2CastlePosition> castles)
    {
        for (int index = 0; index < Mp2Map.CastleCount; ++index)
        {
            if (index < castles.Count)
            {
                Mp2CastlePosition castle = castles[index];
                writer.WriteByte((byte)castle.X);
                writer.WriteByte((byte)castle.Y);
                writer.WriteByte((byte)castle.RawType);
            }
            else
            {
                writer.WriteByte(EmptyObjectPosition);
                writer.WriteByte(EmptyObjectPosition);
                writer.WriteByte(EmptyObjectPosition);
            }
        }
    }

    private static void WriteCaptureObjectTable(
        LittleEndianWriter writer, IReadOnlyList<Mp2CaptureObject> captureObjects)
    {
        for (int index = 0; index < Mp2Map.CaptureObjectCount; ++index)
        {
            if (index < captureObjects.Count)
            {
                Mp2CaptureObject captureObject = captureObjects[index];
                writer.WriteByte((byte)captureObject.X);
                writer.WriteByte((byte)captureObject.Y);
                writer.WriteByte((byte)captureObject.RawType);
            }
            else
            {
                writer.WriteByte(EmptyObjectPosition);
                writer.WriteByte(EmptyObjectPosition);
                writer.WriteByte(EmptyObjectPosition);
            }
        }
    }

    private static void WriteAtomically(string filePath, byte[] bytes)
    {
        string full = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(full) ?? ".";
        string temporary = Path.Combine(directory, $"{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");

        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
