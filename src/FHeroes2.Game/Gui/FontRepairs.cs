using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The repairs the reference applies to the freshly loaded base fonts: <c>ui_font.cpp:6852</c>
/// <c>modifyBaseNormalFont</c>, <c>ui_font.cpp:7046</c> <c>modifyBaseSmallFont</c>, and the stray colour
/// index fix of <c>game_assets.cpp:2498</c>.
/// </summary>
/// <remarks>
/// <para>
/// 归档里的英文字形有一批历史问题：<c>$</c> 与 <c>&amp;</c> 的位置不对、<c>%</c> 上多一道白线、
/// 小写 <c>k</c> 画得不像 <c>k</c>，而 <c>\</c>（两种字号都有）和普通号的 <c>|</c> 干脆不存在。
/// 参考在加载 <c>FONT.ICN</c> / <c>SMALFONT.ICN</c> 之后就地改一遍，改好的表再拿去派生黄/灰/大号/渐变面，
/// 所以修补必须发生在派生之前。
/// </para>
/// <para>
/// 帧号沿用参考的 <c>frames[ch - 32]</c>。参考改的是 <c>_icnVsSprite</c> 里那份 sprites，
/// C# 侧归档缓存是所有调用方共享的，所以这里先逐帧克隆，之后只动自己的副本；顺带也让「帧之间共享同一张图」
/// 这件事消失——派生面不必再去重。
/// </para>
/// <para>
/// 未搬（同一个加载分支里，但需要本项目还没有的东西）：<c>replacePOLAssetWithSW</c>（波兰语资材的星号）、
/// 小号字体的 CRC 平移修补（<c>game_assets.cpp:2489</c>，要原始 ICN 数据算 CRC32）、
/// CP1251 字体的帧重排（<c>game_assets.cpp:2512</c>），以及两个函数末尾那段
/// <c>if ( getResourceLanguage() == SupportedLanguage::French )</c>（<c>ui_font.cpp:6897</c>、<c>7091</c>）——
/// 法语资材把 <c>$</c>、<c>&amp;</c>、<c>*</c> 换成了 CP1252 符号，那里要按参考的 <c>DrawLine</c> 重画，
/// 触发条件是资材语言探测，C# 资产层还没有这个概念。
/// </para>
/// </remarks>
internal static class FontRepairs
{
    /// <summary>
    /// Below this frame count the loaded font is damaged or comes from a pirated translation, and the
    /// reference skips the repairs altogether.
    /// </summary>
    private const int MinimumFrameCount = 96;

    /// <summary>Colour used for the hand drawn glyphs, the reference's literal <c>10</c>.</summary>
    private const byte HandDrawnColour = 10;

    /// <summary>Frame index of <paramref name="character"/>, the reference's <c>ch - 32</c>.</summary>
    private static int Frame(char character) => character - ' ';

    /// <summary>
    /// Returns an owned copy of the base frames with the repairs of <paramref name="size"/> and
    /// <paramref name="language"/> applied.
    /// </summary>
    /// <param name="size">Small or normal font; the large face is enlarged from the normal one.</param>
    /// <param name="frames">Frames as they came out of the archive; they are not modified.</param>
    /// <param name="language">Language version of the assets, <see cref="ResourceLanguage.Get"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not a base size.</exception>
    public static Sprite?[] Apply(FontSize size, Sprite?[] frames, SupportedLanguage language)
    {
        Sprite?[] owned = new Sprite?[frames.Length];

        for (int index = 0; index < frames.Length; ++index)
        {
            owned[index] = frames[index]?.Clone();
        }

        if (owned.Length < MinimumFrameCount)
        {
            return owned;
        }

        switch (size)
        {
            case FontSize.Small:
                ModifySmall(owned);

                if (language == SupportedLanguage.French)
                {
                    FontFrenchRepairs.ApplySmall(owned);
                }

                break;

            case FontSize.Normal:
                // 字形里有 3 个像素用了调色板索引 50（正好是 UTF-8 里 '2' 的码），本该是阴影。
                foreach (Sprite? glyph in owned)
                {
                    if (glyph is not null)
                    {
                        ImageOps.ReplaceColorIdByTransformId(glyph, 50, FontGlyph.ShadowTransformId);
                    }
                }

                ModifyNormal(owned);

                if (language == SupportedLanguage.French)
                {
                    FontFrenchRepairs.ApplyNormal(owned);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(size), size, "Only the small and normal fonts have repairs.");
        }

        return owned;
    }

    /// <summary><c>modifyBaseNormalFont</c> (<c>ui_font.cpp:6852</c>).</summary>
    private static void ModifyNormal(Sprite?[] frames)
    {
        // $ (dollar sign)：位置太低。
        frames[Frame('$')] = Moved(frames[Frame('$')], 0, 2);

        // % 左上方那两道多余的白线：先擦掉，再按新轮廓重算阴影。
        Erase(frames[Frame('%')], 5, 0, 5, 1);
        Erase(frames[Frame('%')], 6, 2, 2, 1);
        FontGlyph.NormalShadow(frames[Frame('%')]);

        // & (ampersand)：位置太低。
        frames[Frame('&')] = Moved(frames[Frame('&')], 0, 1);

        // 减号再往下挪一像素，才落在文字的中线上。
        if (frames[Frame('-')] is { } hyphen)
        {
            frames[Frame('-')] = Moved(hyphen, hyphen.OffsetX, hyphen.OffsetY + 1);
            FontGlyph.NormalShadow(frames[Frame('-')]);
        }

        // 补上 '\'：把 '/' 水平镜像过来。参考用带 flip 的 Blit，这里逐列镜像 Copy，效果一样。
        if (frames[Frame('/')] is { } slash)
        {
            Sprite backslash = new(8, 14, slash.OffsetX, slash.OffsetY);
            backslash.Reset();

            for (int x = 0; x < 7; ++x)
            {
                CopyFrom(slash, x, 0, backslash, 1 + (6 - x), 0, 1, 12);
            }

            FontGlyph.NormalShadow(backslash);
            frames[Frame('\\')] = backslash;
        }

        // 小写 k 重拼：先擦掉原来那一坨，竖干和右下的斜线都从 'K' 借。
        if (frames[Frame('k')] is { } lowerK && frames[Frame('K')] is { } upperK)
        {
            Erase(lowerK, 4, 1, 5, 8);
            CopyFrom(upperK, 6, 5, lowerK, 4, 7, 3, 1);
            CopyFrom(upperK, 6, 4, lowerK, 4, 6, 4, 1);
            CopyFrom(upperK, 7, 4, lowerK, 6, 5, 3, 1);
            CopyFrom(upperK, 7, 4, lowerK, 7, 4, 2, 1);
            CopyFrom(upperK, 6, 6, lowerK, 4, 8, 4, 1);
            FontGlyph.NormalShadow(lowerK);
        }

        // 补上 '|'：文字输入的光标用的也是它。上段抄 '[' 的竖笔，下段抄它带钩的尾巴。
        if (frames[Frame('[')] is { } bracket)
        {
            int height = bracket.Height;
            Sprite bar = new(3, height + 3, bracket.OffsetX, bracket.OffsetY);
            bar.Reset();
            CopyFrom(bracket, 0, 0, bar, 0, 0, 3, height - 4);
            CopyFrom(bracket, 0, height - 7, bar, 0, height - 4, 3, 7);
            frames[Frame('|')] = bar;
        }
    }

    /// <summary><c>modifyBaseSmallFont</c> (<c>ui_font.cpp:7046</c>).</summary>
    private static void ModifySmall(Sprite?[] frames)
    {
        // $：位置太高。
        frames[Frame('$')] = Moved(frames[Frame('$')], 0, -1);

        // % 上的白线。注意参考这里调的是普通字号那套阴影参数（-1,2 / false），不是小号的。
        Erase(frames[Frame('%')], 3, 0, 4, 1);
        Erase(frames[Frame('%')], 4, 1, 2, 1);
        FontGlyph.NormalShadow(frames[Frame('%')]);

        // 补上 '*'：小号字体在归档里根本没有星号，只能画出来。
        {
            Sprite asterisk = new(6, 8);
            asterisk.Reset();

            ImageOps.DrawLine(asterisk, new Point(3, 0), new Point(3, 6), HandDrawnColour);
            ImageOps.DrawLine(asterisk, new Point(1, 1), new Point(5, 5), HandDrawnColour);
            ImageOps.DrawLine(asterisk, new Point(5, 1), new Point(1, 5), HandDrawnColour);

            FontGlyph.SmallShadow(asterisk);
            frames[Frame('*')] = asterisk;
        }

        // 补上 '\'：把 '/' 的竖笔一段段斜着抄过来。
        if (frames[Frame('/')] is { } slash)
        {
            Sprite backslash = new(5, 9, slash.OffsetX, slash.OffsetY);
            backslash.Reset();

            for (int step = 0; step < 4; ++step)
            {
                CopyFrom(slash, 4, 0, backslash, 1 + step, step * 2, 1, 2);
            }


            FontGlyph.SmallShadow(backslash);
            frames[Frame('\\')] = backslash;
        }

        // 小写 k 重拼：竖干抄 'l'，右下两笔抄 'X'，竖干右上那一点抄 'a'。
        if (frames[Frame('k')] is { } oldK
            && frames[Frame('l')] is { } stem
            && frames[Frame('X')] is { } upperX
            && frames[Frame('a')] is { } bowl)
        {
            Sprite lowerK = new(6, 8, oldK.OffsetX, oldK.OffsetY);
            lowerK.Reset();

            CopyFrom(stem, 1, 0, lowerK, 1, 0, 2, 7);
            CopyFrom(stem, 1, 0, lowerK, 1, 6, 1, 1);
            CopyFrom(upperX, 6, 0, lowerK, 3, 2, 3, 3);
            CopyFrom(bowl, 2, bowl.Height - 2, lowerK, 5, 6, 2, 1);
            CopyFrom(bowl, 2, 0, lowerK, 4, 5, 1, 1);

            FontGlyph.SmallShadow(lowerK);
            frames[Frame('k')] = lowerK;
        }
    }

    /// <summary>
    /// Same sprite content, different anchor, the reference's <c>setPosition</c>. The buffers are shared,
    /// which is fine because the frames handed to <see cref="Apply"/> are private copies.
    /// </summary>
    private static Sprite? Moved(Sprite? glyph, int offsetX, int offsetY) =>
        glyph is null ? null : new Sprite(glyph.Image, offsetX, offsetY, glyph.Transform);

    /// <summary>Erases an area, the reference's <c>FillTransform( letter, ..., 1 )</c>.</summary>
    private static void Erase(Sprite? glyph, int x, int y, int width, int height)
    {
        if (glyph is not null)
        {
            ImageOps.FillTransform(glyph, new Rect(x, y, width, height), 1);
        }
    }

    /// <summary>Copies a rectangle, the reference's <c>Copy( in, inX, inY, out, outX, outY, w, h )</c>.</summary>
    private static void CopyFrom(Sprite? source, int sourceX, int sourceY, Sprite destination, int destinationX, int destinationY, int width, int height)
    {
        if (source is not null)
        {
            ImageOps.Copy(source, new Rect(sourceX, sourceY, width, height), destination, new Point(destinationX, destinationY));
        }
    }

}
