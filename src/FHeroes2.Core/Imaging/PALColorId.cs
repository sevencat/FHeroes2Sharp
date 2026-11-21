namespace FHeroes2.Core.Imaging;

/// <summary>
/// Nearest colour of the game palette, a port of <c>GetPALColorId()</c>
/// (<c>src/engine/image.cpp:419-437</c> with the table builder at <c>:355-416</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在编译期生成一张 <c>64 × 64 × 64</c> 的查找表嵌进可执行文件，运行时只做一次数组索引；端口没有这张表，
/// 改为每次按同一套候选与同一套距离公式现算（227 次比较），结果与参考逐位相同，省掉 256 KB 静态数据。
/// 调用方传入的必须是 6 位色（0..63），与参考的索引方式 <c>red + (green &lt;&lt; 6) + (blue &lt;&lt; 12)</c> 一致。
/// </para>
/// <para>
/// 候选是「No cycle」调色板里的 227 个不重复颜色：跳过前 10 与后 10 个未定义色，并排除重复色；
/// 为了仍能用到循环色独有的颜色，末尾补上循环色到其静态副本的连接
/// （水色 231/232/233/235 → 246..249、熔岩色 214..217 → 250..253）。
/// 距离用 Redmean 加权（<c>https://www.compuphase.com/cmetric.htm</c>），公式照抄。
/// </para>
/// <para>
/// 参考最后用 <c>transformTable[ paletteSize * 15 + bestPos ]</c> 把选中的索引换成「No cycle」副本，
/// 端口对应的是 <see cref="PaletteId.NoCycle"/> 那张表。
/// </para>
/// </remarks>
public static class PALColorId
{
    /// <summary>Highest value of a six bit colour channel.</summary>
    public const byte MaxSixBitValue = 63;

    /// <summary>Number of palette entries searched by the reference, <c>227</c>.</summary>
    public const int CandidateCount = 227;

    private static readonly byte[] Candidates = BuildCandidates();

    /// <summary>
    /// Returns the game palette entry closest to the given six bit colour.
    /// </summary>
    /// <param name="red">Red channel, 0..63.</param>
    /// <param name="green">Green channel, 0..63.</param>
    /// <param name="blue">Blue channel, 0..63.</param>
    /// <param name="normalizedGamePalette">
    /// The game palette in video values, <c>getRGBGamePalette()</c> shifted left by two bits
    /// (<c>Palette.FromGamePalette</c>), not the six bit <c>KB.PAL</c> bytes.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A channel is above <see cref="MaxSixBitValue"/>.</exception>
    public static byte Get(byte red, byte green, byte blue, Palette normalizedGamePalette)
    {
        ArgumentNullException.ThrowIfNull(normalizedGamePalette);

        return Get(red, green, blue, normalizedGamePalette.ToRaw());
    }

    /// <summary>
    /// Returns the game palette entry closest to the given six bit colour.
    /// </summary>
    /// <param name="red">Red channel, 0..63.</param>
    /// <param name="green">Green channel, 0..63.</param>
    /// <param name="blue">Blue channel, 0..63.</param>
    /// <param name="normalizedGamePalette">
    /// Raw bytes of the game palette in video values, see <see cref="Palette.FromGamePalette"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A channel is above <see cref="MaxSixBitValue"/>.</exception>
    public static byte Get(byte red, byte green, byte blue, ReadOnlySpan<byte> normalizedGamePalette)
    {
        if (red > MaxSixBitValue)
        {
            throw new ArgumentOutOfRangeException(nameof(red), red, "A six bit channel cannot exceed 63.");
        }

        if (green > MaxSixBitValue)
        {
            throw new ArgumentOutOfRangeException(nameof(green), green, "A six bit channel cannot exceed 63.");
        }

        if (blue > MaxSixBitValue)
        {
            throw new ArgumentOutOfRangeException(nameof(blue), blue, "A six bit channel cannot exceed 63.");
        }

        if (normalizedGamePalette.Length != PaletteTable.Size * 3)
        {
            throw new ArgumentException(
                $"A game palette must hold {PaletteTable.Size * 3} bytes.", nameof(normalizedGamePalette));
        }

        int minDistance = int.MaxValue;
        byte bestPosition = 0;

        for (int i = 0; i < Candidates.Length; ++i)
        {
            int position = Candidates[i];
            int paletteRed = normalizedGamePalette[position * 3] >> 2;
            int paletteGreen = normalizedGamePalette[(position * 3) + 1] >> 2;
            int paletteBlue = normalizedGamePalette[(position * 3) + 2] >> 2;

            int sumRed = paletteRed + red;
            int offsetRed = paletteRed - red;
            int offsetGreen = paletteGreen - green;
            int offsetBlue = paletteBlue - blue;

            // Redmean: a weighted Euclidean distance that is closer to human perception than a plain one.
            int distance = ((2 * 2 * 256) + sumRed) * offsetRed * offsetRed
                           + (4 * 2 * 256 * offsetGreen * offsetGreen)
                           + (((2 * (2 * 256 + 255)) - sumRed) * offsetBlue * offsetBlue);

            if (minDistance > distance)
            {
                minDistance = distance;
                bestPosition = (byte)position;

                if (distance == 0)
                {
                    break;
                }
            }
        }

        return PaletteTable.Apply(PaletteId.NoCycle, bestPosition);
    }

    /// <summary>The 227 positions of the reference's <c>nonCyclingUniqueColorPos</c> array.</summary>
    private static byte[] BuildCandidates()
    {
        List<byte> positions = new(CandidateCount);

        // 10..213: the defined part of the palette, minus the duplicates the reference skips.
        for (int id = 10; id <= 213; ++id)
        {
            positions.Add((byte)id);
        }

        // The remaining entries the reference keeps, in its own order.
        positions.AddRange(new byte[]
        {
            222, 223, 224, 225, 226, 227, 228, 229, 230,
            236, 237, 242, 243, 244, 245,
            231, 232, 233, 235,
            214, 215, 216, 217,
        });

        return positions.ToArray();
    }
}
