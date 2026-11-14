using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Writes a complete, structurally valid MP2 map file so the tests do not depend on the real game
/// data. Every section of the file layout described on <see cref="Mp2Map"/> can be controlled; the
/// defaults describe an empty, playable map.
/// </summary>
internal sealed class Mp2MapFileBuilder
{
    /// <summary>Creates a builder for a square map of <paramref name="size"/> tiles per side.</summary>
    public Mp2MapFileBuilder(int size = 36)
    {
        Size = size;
        Tiles = new Mp2TileInfo[size * size];
    }

    /// <summary>Tiles per side; the map is always square.</summary>
    public int Size { get; }

    /// <summary>All tiles, row by row. Defaults to empty tiles.</summary>
    public Mp2TileInfo[] Tiles { get; }

    /// <summary>Add-ons; the first entry is the placeholder that means "no add-on".</summary>
    public List<Mp2AddonInfo> Addons { get; } = [default];

    /// <summary>Castle table entries; the remaining slots are written as empty.</summary>
    public List<(int X, int Y, int Type)> Castles { get; } = [];

    /// <summary>Capturable object table entries; the remaining slots are written as empty.</summary>
    public List<(int X, int Y, int Type)> CaptureObjects { get; } = [];

    /// <summary>Value of the obelisk counter.</summary>
    public int ObeliskCount { get; set; }

    /// <summary>Information blocks, written in this order.</summary>
    public List<byte[]> InfoBlocks { get; } = [];

    /// <summary>Value of the trailing object UID counter.</summary>
    public uint LastObjectUid { get; set; } = 100;

    /// <summary>Serializes the map.</summary>
    public byte[] ToArray()
    {
        List<byte> bytes = [];
        byte[] header = TemporaryMap.CreateContent(name: "Builder Map");
        header[6] = (byte)Size;
        header[7] = (byte)Size;
        bytes.AddRange(header);

        foreach (Mp2TileInfo tile in Tiles)
        {
            WriteUInt16(bytes, tile.TerrainImageIndex);
            bytes.Add(tile.ObjectName1);
            bytes.Add(tile.BottomIcnImageIndex);
            bytes.Add(tile.Quantity1);
            bytes.Add(tile.Quantity2);
            bytes.Add(tile.ObjectName2);
            bytes.Add(tile.TopIcnImageIndex);
            bytes.Add(tile.TerrainFlags);
            bytes.Add((byte)tile.ObjectType);
            WriteUInt16(bytes, tile.NextAddonIndex);
            WriteUInt32(bytes, tile.Level1ObjectUid);
            WriteUInt32(bytes, tile.Level2ObjectUid);
        }

        WriteUInt32(bytes, (uint)Addons.Count);
        foreach (Mp2AddonInfo addon in Addons)
        {
            WriteUInt16(bytes, addon.NextAddonIndex);

            // The reader multiplies the byte by two, so the written value is halved to round-trip.
            bytes.Add((byte)(addon.ObjectNameN1 / 2));
            bytes.Add(addon.BottomIcnImageIndex);
            bytes.Add(addon.QuantityN);
            bytes.Add(addon.ObjectNameN2);
            bytes.Add(addon.TopIcnImageIndex);
            WriteUInt32(bytes, addon.Level1ObjectUid);
            WriteUInt32(bytes, addon.Level2ObjectUid);
        }

        for (int index = 0; index < Mp2Map.CastleCount; ++index)
        {
            WriteTableEntry(bytes, index < Castles.Count ? Castles[index] : (0xFF, 0xFF, 0xFF));
        }

        for (int index = 0; index < Mp2Map.CaptureObjectCount; ++index)
        {
            WriteTableEntry(bytes, index < CaptureObjects.Count ? CaptureObjects[index] : (0xFF, 0xFF, 0xFF));
        }

        bytes.Add((byte)ObeliskCount);

        // The count list stores "number of blocks + 1" and ends with a zero pair.
        WriteUInt16(bytes, (ushort)(InfoBlocks.Count + 1));
        bytes.Add(0);
        bytes.Add(0);

        foreach (byte[] block in InfoBlocks)
        {
            WriteUInt16(bytes, (ushort)block.Length);
            bytes.AddRange(block);
        }

        WriteUInt32(bytes, LastObjectUid);

        return [.. bytes];
    }

    /// <summary>Writes the map to <paramref name="filePath"/> and returns the path.</summary>
    public string Write(string filePath)
    {
        File.WriteAllBytes(filePath, ToArray());
        return filePath;
    }

    private static void WriteTableEntry(List<byte> bytes, (int X, int Y, int Type) entry)
    {
        bytes.Add((byte)entry.X);
        bytes.Add((byte)entry.Y);
        bytes.Add((byte)entry.Type);
    }

    private static void WriteUInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)value);
        bytes.Add((byte)(value >> 8));
    }

    private static void WriteUInt32(List<byte> bytes, uint value)
    {
        bytes.Add((byte)value);
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)(value >> 16));
        bytes.Add((byte)(value >> 24));
    }
}
