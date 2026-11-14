namespace FHeroes2.Core.Io;

/// <summary>
/// CRC-32 (ISO 3309 / zlib), the checksum the reference computes with <c>fheroes2::calculateCRC32</c>
/// (<c>src/engine/tools.cpp:125</c>, a plain <c>crc32( 0, data, length )</c> call).
/// </summary>
/// <remarks>
/// <para>
/// 两个用处：PNG 分块校验（<see cref="Imaging.PngWriter"/> / <see cref="Imaging.PngReader"/>），
/// 以及按文件内容识别资材版本——参考拿 <c>FONT.ICN</c> 的 CRC 判断这是哪国语言版本
/// （<c>ui_language.cpp:88</c> <c>getResourceLanguage</c>），拿 <c>SMALFONT.ICN</c> 的 CRC 判断是不是
/// 波兰 GoG 版那个整体下移一像素的字体（<c>game_assets.cpp:2489</c>）。所以它是公开的，
/// 不再只是 PNG 的私用工具。
/// </para>
/// <para>
/// 多项式、初值（<c>0xFFFFFFFF</c>）、末尾取反都是 zlib 的标准做法，结果与参考逐字节一致。
/// </para>
/// </remarks>
public static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;

    private static readonly uint[] Table = CreateTable();

    /// <summary>Computes the CRC of <paramref name="first"/> followed by <paramref name="second"/>.</summary>
    /// <param name="first">Leading bytes.</param>
    /// <param name="second">Trailing bytes; empty by default.</param>
    /// <returns>The CRC of the concatenation.</returns>
    public static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second = default) =>
        ~Append(Append(0xFFFFFFFFu, first), second);

    /// <summary>Extends a running (non complemented) CRC with <paramref name="data"/>.</summary>
    /// <param name="crc">Running state as returned by this method, or <c>0xFFFFFFFF</c> to start over.</param>
    /// <param name="data">Bytes to append.</param>
    /// <returns>The updated, still non complemented, state.</returns>
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
