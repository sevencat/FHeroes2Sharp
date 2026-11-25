using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The <c>MINI_MONSTER_IMAGE</c> and <c>MINI_MONSTER_SHADOW</c> tables of the <c>loadICN</c> switch
/// (<c>src/fheroes2/game/game_assets.cpp:4750-4826</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这两张表都没有归档条目：参考在 <c>loadICN()</c> 里把 <c>MINIMON</c> 解出来的每一帧复制一份，只改 transform 层——
/// 图像表把 <c>&gt;1</c>（阴影、光照等调色板变换）的像素改成 <c>1</c>（不画），阴影表把 <c>0</c>（直接拷贝）
/// 的像素改成 <c>1</c>，于是同一份图像被拆成「只有本体」和「只有影子」两份。地图上的怪物就是两次都画。
/// </para>
/// <para>
/// 图像层按引用共享，不复制像素——参考的复制只是为了让两张表的 transform 各自独立，图像本身之后不会再被改写
/// （唯一例外是下面的牛头王配色修正，参考也是在复制之前改的）。第一遍取哪张表都行：参考把两张表一起造，
/// 这里按文件名各造一次，配色修正用 <c>_minotaurKingFixed</c> 防止减两次。
/// </para>
/// </remarks>
public static class MiniMonsterSprites
{
    /// <summary>The name the <c>MINI_MONSTER_IMAGE</c> table is cached under.</summary>
    public const string ImageFileName = "MINI_MONSTER_IMAGE.ICN";

    /// <summary>The name the <c>MINI_MONSTER_SHADOW</c> table is cached under.</summary>
    public const string ShadowFileName = "MINI_MONSTER_SHADOW.ICN";

    /// <summary>The archive entry both tables are derived from.</summary>
    private const string MonsterFileName = "MINIMON.ICN";

    /// <summary>Blue to gold offset of the Minotaur King armlets (<c>game_assets.cpp:4759</c>).</summary>
    private const int GoldArmletPaletteOffset = 42;

    /// <summary>
    /// The Minotaur King frames of <c>MINIMON</c> whose blue armlets are recolored to gold so that they match
    /// <c>ICN::MINOTAU2</c>, with the width and height the fix only applies to and the flat pixel indices
    /// (<c>game_assets.cpp:4757-4815</c>). The first entry is frame 297.
    /// </summary>
    private static readonly (int Width, int Height, int[] Pixels)[] MinotaurKingArmlets =
    {
        (38, 34, new[] { 599, 600, 601, 638 }),
        (44, 32, new[] { 777, 778, 780, 822, 823, 830, 874, 918 }),
        (44, 32, new[] { 777, 778, 780, 822, 823, 830, 874, 918 }),
        (45, 32, new[] { 795, 796, 798, 841, 842, 849, 894, 939 }),
        (45, 32, new[] { 795, 796, 798, 841, 842, 849, 894, 939 }),
        (45, 32, new[] { 755, 794, 795, 797, 798, 799, 840, 841, 886, 932 }),
        (44, 32, new[] { 777, 778, 822, 823, 867 }),
    };

    /// <summary>Guards the Minotaur King recoloring, which both tables would otherwise apply.</summary>
    private static bool _minotaurKingFixed;

    /// <summary>
    /// Builds one of the two tables out of <c>MINIMON</c>.
    /// </summary>
    /// <param name="fileName">Asset name, one of the constants of this class.</param>
    /// <returns>
    /// One frame per <c>MINIMON</c> frame, or <see langword="null"/> when <paramref name="fileName"/> is
    /// neither table or the archive has no <c>MINIMON</c>.
    /// </returns>
    public static Sprite?[]? Build(string fileName)
    {
        if (string.Equals(fileName, ImageFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildTable(shadowLayerOnly: false);
        }

        if (string.Equals(fileName, ShadowFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildTable(shadowLayerOnly: true);
        }

        return null;
    }

    /// <summary>Derives one table, the two loops at the end of the reference case.</summary>
    /// <param name="shadowLayerOnly">Whether to keep only the shadow pixels instead of only the body ones.</param>
    private static Sprite?[]? BuildTable(bool shadowLayerOnly)
    {
        int count = Assets.GetImageCount(MonsterFileName);

        if (count <= 0)
        {
            return null;
        }

        FixMinotaurKingArmlets(count);

        var frames = new Sprite?[count];

        for (int i = 0; i < count; ++i)
        {
            if (Assets.GetImage(MonsterFileName, i) is not { } source)
            {
                continue;
            }

            frames[i] = new Sprite(source.Image, source.OffsetX, source.OffsetY, BuildTransform(source, shadowLayerOnly));
        }

        return frames;
    }

    /// <summary>Rewrites the transform layer of one frame, or drops it for a body frame that has none.</summary>
    /// <param name="source">Frame of <c>MINIMON</c>.</param>
    /// <param name="shadowLayerOnly">Whether to keep only the shadow pixels.</param>
    /// <returns>
    /// The rewritten layer, <see langword="null"/> for a body frame without a layer (every pixel is copied as is),
    /// or an all <c>1</c> layer for a shadow frame without one (nothing to draw).
    /// </returns>
    private static byte[]? BuildTransform(Sprite source, bool shadowLayerOnly)
    {
        if (source.Transform is not { } transform)
        {
            if (!shadowLayerOnly)
            {
                return null;
            }

            byte[] hidden = new byte[source.Width * source.Height];
            Array.Fill(hidden, (byte)1);
            return hidden;
        }

        byte[] result = (byte[])transform.Clone();

        for (int i = 0; i < result.Length; ++i)
        {
            if (shadowLayerOnly ? result[i] == 0 : result[i] > 1)
            {
                result[i] = 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Recolors the blue armlets of the Minotaur King frames to gold, the first half of the reference case.
    /// </summary>
    /// <param name="count">Number of frames in <c>MINIMON</c>.</param>
    private static void FixMinotaurKingArmlets(int count)
    {
        if (_minotaurKingFixed || count <= 303)
        {
            return;
        }

        _minotaurKingFixed = true;

        for (int i = 0; i < MinotaurKingArmlets.Length; ++i)
        {
            (int width, int height, int[] pixels) = MinotaurKingArmlets[i];

            if (Assets.GetImage(MonsterFileName, 297 + i) is not { } sprite ||
                sprite.Width != width || sprite.Height != height)
            {
                continue;
            }

            foreach (int pixel in pixels)
            {
                sprite.Image.Pixels[pixel] -= GoldArmletPaletteOffset;
            }
        }
    }
}
