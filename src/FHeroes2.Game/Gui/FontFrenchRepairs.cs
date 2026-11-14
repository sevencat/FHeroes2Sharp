using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The letters of the French assets that <c>modifyBaseNormalFont</c> / <c>modifyBaseSmallFont</c> redraw
/// (<c>ui_font.cpp:6897</c> and <c>ui_font.cpp:7091</c>).
/// </summary>
/// <remarks>
/// <para>
/// 法语资材把 <c>$</c>、<c>&amp;</c>、<c>*</c> 换成了 CP1252 的货币/装饰符号，剩下 <c>#</c>、<c>&lt;</c>、<c>&gt;</c>、
/// <c>@</c>、<c>^</c>、<c>`</c>、<c>{</c>、<c>}</c>、<c>~</c>、DEL 十个干脆画成小圆点。这些字母参考是从零画的，
/// 和按钮字体一样是「数据」不是算法，所以由脚本从参考源码转写，符号一一对应，能和参考并排比对。
/// </para>
/// <para>
/// 触发条件是 <c>getResourceLanguage() == French</c>，也就是 <c>FONT.ICN</c> 的 CRC32 命中法语资材那一条
/// （见 <see cref="ResourceLanguage"/>）；英文资材走不到这里。
/// </para>
/// </remarks>
internal static class FontFrenchRepairs
{
    /// <summary><c>modifyBaseNormalFont</c> 的法语分支。</summary>
    public static void ApplyNormal(Sprite?[] frames)
    {
        // $ (dollar sign).
        {
            Sprite letter = FontGlyph.Create(frames[36 - 32], 9, 12);
            ImageOps.SetPixel(letter, 3, 0, 23);
            ImageOps.SetPixel(letter, 4, 0, 18);
            ImageOps.SetPixel(letter, 5, 0, 12);
            ImageOps.SetPixel(letter, 6, 0, 23);
            ImageOps.SetPixel(letter, 1, 1, 21);
            ImageOps.DrawLine(letter, new Point(2, 1), new Point(7, 1), 10);
            ImageOps.SetPixel(letter, 8, 1, 21);
            ImageOps.DrawLine(letter, new Point(1, 2), new Point(1, 3), 10);
            ImageOps.DrawLine(letter, new Point(2, 2), new Point(2, 3), 16);
            ImageOps.SetPixel(letter, 3, 2, 21);
            ImageOps.DrawLine(letter, new Point(4, 2), new Point(4, 3), 16);
            ImageOps.DrawLine(letter, new Point(5, 2), new Point(5, 7), 10);
            ImageOps.SetPixel(letter, 6, 2, 21);
            ImageOps.SetPixel(letter, 7, 2, 16);
            ImageOps.SetPixel(letter, 8, 2, 10);
            ImageOps.SetPixel(letter, 1, 4, 21);
            ImageOps.DrawLine(letter, new Point(2, 4), new Point(7, 4), 10);
            ImageOps.SetPixel(letter, 8, 4, 21);
            ImageOps.SetPixel(letter, 2, 5, 21);
            ImageOps.SetPixel(letter, 3, 5, 16);
            ImageOps.DrawLine(letter, new Point(4, 5), new Point(4, 7), 16);
            ImageOps.SetPixel(letter, 1, 7, 10);
            ImageOps.SetPixel(letter, 2, 7, 16);
            ImageOps.SetPixel(letter, 3, 7, 21);
            ImageOps.Copy(letter, new Rect(3, 5, 3, 3), letter, new Point(6, 5));
            ImageOps.SetPixel(letter, 1, 8, 23);
            ImageOps.DrawLine(letter, new Point(2, 8), new Point(7, 8), 12);
            ImageOps.SetPixel(letter, 8, 8, 23);
            ImageOps.SetPixel(letter, 3, 9, 25);
            ImageOps.SetPixel(letter, 4, 9, 20);
            ImageOps.SetPixel(letter, 5, 9, 14);
            ImageOps.SetPixel(letter, 6, 9, 25);
            FontGlyph.NormalShadow(letter);
            frames[36 - 32] = letter;
        }
        // & (ampersand).
        {
            Sprite letter = FontGlyph.Create(frames[38 - 32], 11, 12);
            ImageOps.SetPixel(letter, 1, 0, 25);
            ImageOps.SetPixel(letter, 2, 0, 18);
            ImageOps.DrawLine(letter, new Point(3, 0), new Point(5, 0), 12);
            ImageOps.SetPixel(letter, 6, 0, 25);
            ImageOps.DrawLine(letter, new Point(1, 1), new Point(1, 3), 16);
            ImageOps.DrawLine(letter, new Point(2, 1), new Point(2, 3), 10);
            ImageOps.DrawLine(letter, new Point(3, 1), new Point(4, 1), 21);
            ImageOps.DrawLine(letter, new Point(5, 1), new Point(5, 2), 16);
            ImageOps.DrawLine(letter, new Point(6, 1), new Point(6, 2), 10);
            ImageOps.DrawLine(letter, new Point(3, 3), new Point(3, 5), 10);
            ImageOps.SetPixel(letter, 5, 3, 10);
            ImageOps.SetPixel(letter, 6, 3, 21);
            ImageOps.DrawLine(letter, new Point(2, 4), new Point(1, 5), 21);
            ImageOps.DrawLine(letter, new Point(4, 4), new Point(7, 7), 10);
            ImageOps.SetPixel(letter, 5, 4, 21);
            ImageOps.SetPixel(letter, 8, 4, 16);
            ImageOps.DrawLine(letter, new Point(9, 4), new Point(10, 4), 10);
            ImageOps.DrawLine(letter, new Point(2, 5), new Point(2, 7), 10);
            ImageOps.SetPixel(letter, 4, 5, 16);
            ImageOps.SetPixel(letter, 6, 5, 21);
            ImageOps.SetPixel(letter, 7, 5, 16);
            ImageOps.SetPixel(letter, 8, 5, 10);
            ImageOps.DrawLine(letter, new Point(9, 5), new Point(10, 5), 21);
            ImageOps.DrawLine(letter, new Point(1, 6), new Point(1, 7), 16);
            ImageOps.SetPixel(letter, 5, 6, 16);
            ImageOps.SetPixel(letter, 7, 6, 10);
            ImageOps.SetPixel(letter, 6, 7, 16);
            ImageOps.SetPixel(letter, 8, 7, 21);
            ImageOps.SetPixel(letter, 1, 8, 18);
            ImageOps.SetPixel(letter, 2, 8, 12);
            ImageOps.DrawLine(letter, new Point(3, 8), new Point(5, 8), 23);
            ImageOps.SetPixel(letter, 6, 8, 18);
            ImageOps.DrawLine(letter, new Point(7, 8), new Point(8, 8), 12);
            ImageOps.SetPixel(letter, 9, 8, 23);
            ImageOps.SetPixel(letter, 1, 9, 25);
            ImageOps.SetPixel(letter, 2, 9, 20);
            ImageOps.DrawLine(letter, new Point(3, 9), new Point(6, 9), 14);
            ImageOps.SetPixel(letter, 7, 9, 25);
            ImageOps.SetPixel(letter, 8, 9, 20);
            ImageOps.SetPixel(letter, 9, 9, 14);
            FontGlyph.NormalShadow(letter);
            frames[38 - 32] = letter;
        }
        // * (asterisk).
        {
            Sprite letter = FontGlyph.Create(frames[42 - 32], 12, 15);
            letter = FontGlyph.Moved(letter, 0, 0);
            ImageOps.SetPixel(letter, 6, 0, 16);
            ImageOps.SetPixel(letter, 7, 0, 21);
            ImageOps.DrawLine(letter, new Point(6, 1), new Point(6, 11), 10);
            ImageOps.SetPixel(letter, 7, 1, 19);
            ImageOps.DrawLine(letter, new Point(1, 2), new Point(2, 2), 12);
            ImageOps.SetPixel(letter, 7, 2, 18);
            ImageOps.SetPixel(letter, 10, 2, 18);
            ImageOps.SetPixel(letter, 11, 2, 12);
            ImageOps.DrawLine(letter, new Point(1, 3), new Point(4, 6), 21);
            ImageOps.DrawLine(letter, new Point(2, 3), new Point(4, 5), 16);
            ImageOps.DrawLine(letter, new Point(3, 3), new Point(8, 8), 10);
            ImageOps.DrawLine(letter, new Point(7, 3), new Point(7, 4), 16);
            ImageOps.DrawLine(letter, new Point(9, 3), new Point(8, 4), 16);
            ImageOps.DrawLine(letter, new Point(10, 3), new Point(7, 6), 10);
            ImageOps.DrawLine(letter, new Point(11, 3), new Point(8, 6), 21);
            ImageOps.SetPixel(letter, 7, 5, 10);
            ImageOps.SetPixel(letter, 5, 6, 10);
            ImageOps.DrawLine(letter, new Point(4, 7), new Point(2, 9), 16);
            ImageOps.DrawLine(letter, new Point(5, 7), new Point(3, 9), 10);
            ImageOps.DrawLine(letter, new Point(8, 7), new Point(9, 8), 16);
            ImageOps.DrawLine(letter, new Point(5, 8), new Point(4, 9), 21);
            ImageOps.DrawLine(letter, new Point(7, 8), new Point(7, 9), 16);
            ImageOps.SetPixel(letter, 8, 9, 23);
            ImageOps.SetPixel(letter, 9, 9, 12);
            ImageOps.SetPixel(letter, 10, 9, 18);
            ImageOps.SetPixel(letter, 11, 9, 23);
            ImageOps.DrawLine(letter, new Point(1, 10), new Point(2, 10), 14);
            ImageOps.SetPixel(letter, 3, 10, 25);
            ImageOps.SetPixel(letter, 7, 10, 18);
            ImageOps.SetPixel(letter, 9, 10, 25);
            ImageOps.DrawLine(letter, new Point(10, 10), new Point(11, 10), 14);
            ImageOps.SetPixel(letter, 7, 11, 19);
            ImageOps.SetPixel(letter, 6, 12, 16);
            ImageOps.SetPixel(letter, 7, 12, 21);
            FontGlyph.NormalShadow(letter);
            frames[42 - 32] = letter;
        }

        // 其余被换掉的 ASCII 一律画成小圆点，和英文资材的做法一致。
        // TODO: 参考自己也留着这个 TODO，将来会给这些字符画真正的字形。
        Sprite? dot = frames[46 - 32];

        frames[35 - 32] = dot; // #
        frames[60 - 32] = dot; // <
        frames[62 - 32] = dot; // >
        frames[64 - 32] = dot; // @
        frames[94 - 32] = dot; // ^
        frames[96 - 32] = dot; // `
        frames[123 - 32] = dot; // {
        frames[125 - 32] = dot; // }
        frames[126 - 32] = dot; // ~
        frames[127 - 32] = dot; // DEL
    }

    /// <summary><c>modifyBaseSmallFont</c> 的法语分支。</summary>
    public static void ApplySmall(Sprite?[] frames)
    {
        // $ (dollar sign).
        {
            Sprite letter = FontGlyph.Create(frames[36 - 32], 6, 10);
            ImageOps.DrawLine(letter, new Point(3, 0), new Point(3, 8), 10);
            ImageOps.SetPixel(letter, 2, 1, 10);
            ImageOps.SetPixel(letter, 4, 1, 10);
            ImageOps.DrawLine(letter, new Point(1, 2), new Point(1, 3), 10);
            ImageOps.SetPixel(letter, 5, 2, 10);
            ImageOps.SetPixel(letter, 2, 4, 10);
            ImageOps.SetPixel(letter, 4, 4, 10);
            ImageOps.DrawLine(letter, new Point(5, 5), new Point(5, 6), 10);
            ImageOps.SetPixel(letter, 1, 6, 10);
            ImageOps.SetPixel(letter, 2, 7, 10);
            ImageOps.SetPixel(letter, 4, 7, 10);
            FontGlyph.SmallShadow(letter);
            frames[36 - 32] = letter;
        }
        // & (ampersand).
        {
            Sprite letter = FontGlyph.Create(frames[38 - 32], 7, 8);
            letter = FontGlyph.Moved(letter, 0, 0);
            ImageOps.SetPixel(letter, 3, 0, 10);
            ImageOps.DrawLine(letter, new Point(2, 1), new Point(2, 2), 10);
            ImageOps.DrawLine(letter, new Point(4, 1), new Point(4, 2), 10);
            ImageOps.SetPixel(letter, 3, 3, 10);
            ImageOps.SetPixel(letter, 2, 4, 10);
            ImageOps.SetPixel(letter, 4, 4, 10);
            ImageOps.SetPixel(letter, 6, 4, 10);
            ImageOps.SetPixel(letter, 1, 5, 10);
            ImageOps.SetPixel(letter, 5, 5, 10);
            ImageOps.DrawLine(letter, new Point(2, 6), new Point(4, 6), 10);
            ImageOps.SetPixel(letter, 6, 6, 10);
            FontGlyph.SmallShadow(letter);
            frames[38 - 32] = letter;
        }

        // 其余被换掉的 ASCII 一律画成小圆点，和英文资材的做法一致。
        // TODO: 参考自己也留着这个 TODO，将来会给这些字符画真正的字形。
        Sprite? dot = frames[46 - 32];

        frames[35 - 32] = dot; // #
        frames[60 - 32] = dot; // <
        frames[62 - 32] = dot; // >
        frames[64 - 32] = dot; // @
        frames[94 - 32] = dot; // ^
        frames[96 - 32] = dot; // `
        frames[123 - 32] = dot; // {
        frames[125 - 32] = dot; // }
        frames[126 - 32] = dot; // ~
        frames[127 - 32] = dot; // DEL
    }

}