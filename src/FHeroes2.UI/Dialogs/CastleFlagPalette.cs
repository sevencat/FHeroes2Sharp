using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The player-coloured flags of the town screen, <c>getModifiedByColorImage()</c>
/// (<c>ui_castle.cpp:42-165</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考只给六张图换调色板：骑士的城堡、左塔、右塔、五级住宅（<c>TWNKDW_4</c>）与它的升级版、术士的城堡。
/// 这几张图上的旗帜都画在调色板 152~174 这一段连续色阶里（十六色加七级阴影），把整段线性映射到该玩家色的
/// 十六色色阶上，旗子就换色了，其它像素原样保留。
/// </para>
/// <para>
/// 改哪些像素照参考分帧：<b>动画帧整帧都可能是旗帜</b>，静态帧只改那几面三乘三的小旗。无主的城
/// （<see cref="PlayerColor.None"/>）返回原图——参考在这种输入上断言失败，但正式版本会退化成恒等映射，
/// 端口直接走恒等。
/// </para>
/// <para>
/// 参考每画一次就新建一张图；端口按「图 + 帧 + 颜色」缓存一份，因为归档里的精灵是只读的、同一帧每帧都要画。
/// 缓存的图与归档里的原图一样不能改。
/// </para>
/// </remarks>
public static class CastleFlagPalette
{
    /// <summary>First palette index of the castle flags, <c>originalCastleFlagStartColorId</c>.</summary>
    public const int OriginalStartId = 152;

    /// <summary>How many palette indices the castle flags use, <c>16 + 7</c>.</summary>
    public const int OriginalLength = 16 + 7;

    private static readonly Dictionary<(IcnType Icn, int Frame, PlayerColor Color), Sprite> Cache = [];

    /// <summary>
    /// Whether an archive has player-coloured flags, <c>isImagePlayerColorDependent()</c>
    /// (<c>ui_castle.cpp:150-165</c>).
    /// </summary>
    /// <param name="icn">The archive.</param>
    /// <returns><see langword="true"/> for the six flag-bearing archives.</returns>
    public static bool IsColorDependent(IcnType icn) => icn
        is IcnType.Twnzcstl or IcnType.Twnkcstl or IcnType.Twnkdw4 or IcnType.Twnkup4
        or IcnType.Twnkltur or IcnType.Twnkrtur;

    /// <summary>
    /// The recolouring table of a player colour, <c>getModifiedPaletteByPlayerColor()</c>
    /// (<c>ui_castle.cpp:61-83</c>).
    /// </summary>
    /// <param name="color">The owner's colour.</param>
    /// <returns>A 256-entry table that only changes the flag range.</returns>
    public static byte[] ForColor(PlayerColor color)
    {
        (int start, int length) = color switch
        {
            PlayerColor.Blue => (63, 16 + 6),
            PlayerColor.Green => (85, 16 + 7),
            PlayerColor.Red => (175, 16 + 7),
            PlayerColor.Yellow => (108, 16 + 7),
            PlayerColor.Orange => (199, 16),
            PlayerColor.Purple => (132, 16 + 5),
            _ => (OriginalStartId, OriginalLength),
        };

        return BuildTable(OriginalStartId, OriginalLength, start, length);
    }

    /// <summary>
    /// The part of a frame that carries flags, <c>getColorEffectiveAreas()</c> (<c>ui_castle.cpp:85-134</c>).
    /// </summary>
    /// <param name="icn">The archive.</param>
    /// <param name="frame">The frame inside it.</param>
    /// <param name="width">Width of the frame, used for the whole-frame answer.</param>
    /// <param name="height">Height of the frame.</param>
    /// <returns>The rectangles to convert, empty for an archive without flags.</returns>
    public static IReadOnlyList<Rect> EffectiveAreas(IcnType icn, int frame, int width, int height)
    {
        Rect whole = new(0, 0, width, height);

        return icn switch
        {
            IcnType.Twnzcstl => frame == 0
                ? [new Rect(76, 7, 3, 3), new Rect(96, 7, 3, 3), new Rect(178, 11, 3, 3)]
                : [whole],
            IcnType.Twnkcstl => frame == 0
                ? [new Rect(127, 36, 3, 3), new Rect(287, 6, 3, 3)]
                : [whole],
            IcnType.Twnkdw4 or IcnType.Twnkup4 => frame == 0
                ? [new Rect(61, 3, 1, 1)]
                : [new Rect(59, 0, 6, 5)],
            IcnType.Twnkltur => frame == 0 ? [new Rect(5, 6, 3, 3)] : [whole],
            IcnType.Twnkrtur => frame == 0 ? [new Rect(55, 6, 3, 3)] : [whole],
            _ => [],
        };
    }

    /// <summary>
    /// A copy of <paramref name="sprite"/> with its flags in the owner's colour, without caching.
    /// </summary>
    /// <param name="sprite">The frame as it comes from the archives; it is not modified.</param>
    /// <param name="icn">The archive the frame belongs to.</param>
    /// <param name="frame">The frame index.</param>
    /// <param name="color">The owner's colour.</param>
    /// <returns>The recoloured copy, or <paramref name="sprite"/> itself when there is nothing to recolour.</returns>
    public static Sprite Recolor(Sprite sprite, IcnType icn, int frame, PlayerColor color)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        IReadOnlyList<Rect> areas = EffectiveAreas(icn, frame, sprite.Width, sprite.Height);
        if (areas.Count == 0)
        {
            return sprite;
        }

        byte[] palette = ForColor(color);
        Sprite copy = sprite.Clone();

        foreach (Rect area in areas)
        {
            ApplyPalette(copy.Image, area, palette);
        }

        return copy;
    }

    /// <summary>
    /// The recoloured frame, cached by archive, frame and colour.
    /// </summary>
    /// <param name="sprite">The frame as it comes from the archives.</param>
    /// <param name="icn">The archive the frame belongs to.</param>
    /// <param name="frame">The frame index.</param>
    /// <param name="color">The owner's colour; <see cref="PlayerColor.None"/> returns the original frame.</param>
    /// <returns>A frame that is safe to blit but must not be modified.</returns>
    public static Sprite Get(Sprite sprite, IcnType icn, int frame, PlayerColor color)
    {
        if (color == PlayerColor.None || !IsColorDependent(icn))
        {
            return sprite;
        }

        (IcnType, int, PlayerColor) key = (icn, frame, color);

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out Sprite? cached))
            {
                return cached;
            }
        }

        Sprite recoloured = Recolor(sprite, icn, frame, color);

        lock (Cache)
        {
            Cache[key] = recoloured;
        }

        return recoloured;
    }

    /// <summary>Drops every cached frame; meant for tests that load their own sprites.</summary>
    public static void ClearCache()
    {
        lock (Cache)
        {
            Cache.Clear();
        }
    }

    /// <summary>The identity table with the flag range mapped onto another one, <c>getModifiedPalette()</c>.</summary>
    private static byte[] BuildTable(int inputStart, int inputLength, int outputStart, int outputLength)
    {
        byte[] table = new byte[PaletteTable.Size];

        for (int i = 0; i < table.Length; ++i)
        {
            table[i] = (byte)i;
        }

        for (int i = 0; i < inputLength; ++i)
        {
            table[inputStart + i] = (byte)(outputStart + (i * outputLength / inputLength));
        }

        return table;
    }

    /// <summary>Converts one rectangle of an image through <paramref name="palette"/>, <c>ApplyPalette( roi )</c>.</summary>
    private static void ApplyPalette(PixelBuffer image, Rect area, ReadOnlySpan<byte> palette)
    {
        Rect clipped = area.Clip(image.Bounds);

        for (int y = clipped.Y; y < clipped.Bottom; ++y)
        {
            Span<byte> row = image.Row(y).Slice(clipped.X, clipped.Width);

            for (int x = 0; x < row.Length; ++x)
            {
                row[x] = palette[row[x]];
            }
        }
    }
}
