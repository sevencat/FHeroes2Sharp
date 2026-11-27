using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// Builds the evil-interface variants of ordinary archive artwork, the <c>convertToEvilInterface()</c> path of
/// <c>game_assets.cpp:737-746</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考里 <c>STONEBAK_EVIL</c>、<c>DROPLISL_EVIL</c> 这些 id 排在 <c>LAST_VALID_FILE_ICN</c> 之后，归档里没有条目：
/// 它们在 <c>loadICN()</c> 里由对应的好界面图调色板变换而来——石头底走 <c>GOOD_TO_EVIL_INTERFACE</c>，
/// 下拉框还要再叠一层灰（<c>CombinePalettes( GOOD_TO_EVIL_INTERFACE, GRAY )</c>）。
/// </para>
/// <para>
/// 目前只造地图规格窗要用的这两张（<see cref="StonebakEvilFileName"/>、<see cref="DroplislEvilFileName"/>）；
/// 其余 evil 变体（按钮、滚动条、列表…）等有窗口真正用到时再加，别在这里猜。
/// </para>
/// <para>
/// 取图走 <see cref="Assets.GetImage(string, int)"/>，所以缓存、递归保护与「缺图返回 <see langword="null"/>」
/// 都由那一层负责；本类只返回新造的帧，不持有任何东西。
/// </para>
/// </remarks>
public static class EvilInterfaceImages
{
    /// <summary>The <c>STONEBAK_EVIL</c> table, the evil stone background.</summary>
    public const string StonebakEvilFileName = "STONEBAK_EVIL.ICN";

    /// <summary>The <c>DROPLISL_EVIL</c> table, the evil drop-list frames.</summary>
    public const string DroplislEvilFileName = "DROPLISL_EVIL.ICN";

    /// <summary>
    /// Builds the frames of an evil-interface table.
    /// </summary>
    /// <param name="fileName">Asset name, for example <c>STONEBAK_EVIL.ICN</c>.</param>
    /// <returns>The frames, or <see langword="null"/> when <paramref name="fileName"/> is not built here.</returns>
    public static Sprite?[]? Build(string fileName)
    {
        if (string.Equals(fileName, StonebakEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Recolour(IcnType.Stonebak, PaletteTable.Get(PaletteId.GoodToEvilInterface));
        }

        if (string.Equals(fileName, DroplislEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Recolour(
                IcnType.Droplisl,
                PaletteTable.Combine(PaletteId.GoodToEvilInterface, PaletteId.Gray));
        }

        return null;
    }

    /// <summary>Colours every frame of a good-interface table through <paramref name="palette"/>.</summary>
    private static Sprite?[]? Recolour(IcnType goodType, ReadOnlySpan<byte> palette)
    {
        string goodFileName = Assets.GetFileName(goodType);
        int count = Assets.GetImageCount(goodFileName);

        if (count == 0)
        {
            return null;
        }

        Sprite?[] frames = new Sprite?[count];

        for (int frameIndex = 0; frameIndex < count; ++frameIndex)
        {
            Sprite? good = Assets.GetImage(goodFileName, frameIndex);

            if (good is null)
            {
                continue;
            }

            // 克隆再上色：Assets 缓存的帧是共享的，不能被就地改掉。
            Sprite evil = good.Clone();
            ImageOps.ApplyPalette(evil.Image, palette);
            frames[frameIndex] = evil;
        }

        return frames;
    }
}
