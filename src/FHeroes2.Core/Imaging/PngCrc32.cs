namespace FHeroes2.Core.Imaging;

/// <summary>
/// CRC-32 (ISO 3309) used by the PNG chunk framing, shared by <see cref="PngWriter"/> and
/// <see cref="PngReader"/>. A single implementation guarantees that every file we write is
/// accepted by viewers and that a corrupted chunk is rejected when it is read back.
/// </summary>
internal static class PngCrc32
{
    private const uint Polynomial = 0xEDB88320u;

    private static readonly uint[] Table = CreateTable();

    /// <summary>Computes the CRC of <paramref name="first"/> followed by <paramref name="second"/>.</summary>
    public static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second = default) =>
        ~Append(Append(0xFFFFFFFFu, first), second);

    /// <summary>Extends a running (non complemented) CRC with <paramref name="data"/>.</summary>
    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];

        for (int i = 0; i < table.Length; ++i)
        {
            uint value = (uint)i;
            for (int bit = 0; bit < 8; ++bit)
            {
                value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
