using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The ASCII shape of the button font, the body of <c>generateGoodButtonFontBaseShape</c>
/// (<c>ui_font.cpp:4948</c>): one flat list of <c>DrawLine</c> / <c>SetPixel</c> / <c>Fill</c> calls
/// per letter.
/// </summary>
/// <remarks>
/// <para>
/// 归档里没有按钮字体——<c>BUTTON_*_FONT</c> 不是 ICN 条目，参考就是从零画出来的。这张表是「数据」不是算法：
/// 变量与常量一一对应（<c>offset</c> → <see cref="Offset"/>、<c>buttonGoodReleasedColor</c> →
/// <see cref="GoodReleasedColour"/>），所以能和参考逐行并排比对，改起来也不容易走样。
/// </para>
/// <para>
/// 参考的 <c>released[N].resize( w + offset * 2, h + offset * 2 )</c> 后面总跟着 <c>reset()</c>，两者合并成
/// <see cref="Create"/>；每个字母处理完再写回数组，所以生成代码里是一个 <c>{ ... }</c> 块加一个
/// <c>released[N] = letter;</c>。小写字母与 <c>$</c>、<c>@</c> 等参考本来就不画，留着 1×1 透明图。
/// </para>
/// <para>
/// 各代码页的补充字形（<c>generateCP1250GoodButtonFont</c> … <c>generateCP1254GoodButtonFont</c>，
/// <c>ui_font.cpp:5604</c> 起）在 <c>ButtonFont.Cp*.cs</c>，本文件只画 ASCII 那段。
/// </para>
/// </remarks>
internal static partial class ButtonFont
{
    /// <summary><c>generateGoodButtonFontBaseShape</c>.</summary>
    /// <returns>
    /// <see cref="AsciiCharSetSize"/> frames indexed by ASCII code; the ones the game never shows stay
    /// 1×1 transparent, exactly like the reference.
    /// </returns>
    private static Sprite[] CreateBaseShape()
    {
        Sprite[] released = new Sprite[AsciiCharSetSize];

        // 每个字母四周留 2 像素给阴影和描边；整体左移 1 像素对齐。
        for (int index = 0; index < released.Length; ++index)
        {
            released[index] = new Sprite(1, 1, FontOffset.X, FontOffset.Y);
            released[index].Reset();
        }
        // Address symbols that should have even less space to neighboring symbols.
        released[33] = Moved(released[33], FontOffset.X - 1, FontOffset.Y);
        released[45] = Moved(released[45], FontOffset.X - 2, FontOffset.Y);
        released[46] = Moved(released[46], FontOffset.X - 1, FontOffset.Y);
        released[58] = Moved(released[58], FontOffset.X - 2, FontOffset.Y);
        released[59] = Moved(released[59], FontOffset.X - 1, FontOffset.Y);
        released[63] = Moved(released[63], FontOffset.X - 1, FontOffset.Y);
        released[65] = Moved(released[65], FontOffset.X - 1, FontOffset.Y);
        released[86] = Moved(released[86], FontOffset.X - 1, FontOffset.Y);
        released[89] = Moved(released[89], FontOffset.X - 1, FontOffset.Y);
        // !
        {
            Sprite letter = Create(2 + Offset * 2, 10 + Offset * 2, released[33]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 0, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 1, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 8), new Point(Offset + 0, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 8), new Point(Offset + 1, Offset + 9), GoodReleasedColour);
            released[33] = letter;
        }
        // "
        {
            Sprite letter = Create(6 + Offset * 2, 10 + Offset * 2, released[34]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 1, Offset + 1), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 0), new Point(Offset + 5, Offset + 1), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 0, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 4, Offset + 0, GoodReleasedColour);
            released[34] = letter;
        }
        // #
        {
            Sprite letter = Create(10 + Offset * 2, 10 + Offset * 2, released[35]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 3), new Point(Offset + 9, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 6), new Point(Offset + 8, Offset + 6), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 0), new Point(Offset + 2, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 0), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            released[35] = letter;
        }
        // %
        {
            Sprite letter = Create(9 + Offset * 2, 10 + Offset * 2, released[37]);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 0), new Point(Offset + 2, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 2, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 3), new Point(Offset + 2, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 1), new Point(Offset + 3, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 9), new Point(Offset + 6, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 6), new Point(Offset + 6, Offset + 6), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 8), new Point(Offset + 8, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 8), new Point(Offset + 5, Offset + 7), GoodReleasedColour);
            released[37] = letter;
        }
        // &
        {
            Sprite letter = Create(8 + Offset * 2, 10 + Offset * 2, released[38]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 3, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 1), new Point(Offset + 1, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 1), new Point(Offset + 4, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 3), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 3), new Point(Offset + 1, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 6), new Point(Offset + 0, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 8), new Point(Offset + 7, Offset + 5), GoodReleasedColour);
            released[38] = letter;
        }
        // '
        {
            Sprite letter = Create(2 + Offset * 2, 10 + Offset * 2, released[39]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 1, Offset + 1), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 0, GoodReleasedColour);
            released[39] = letter;
        }
        // (
        {
            Sprite letter = Create(3 + Offset * 2, 10 + Offset * 2, released[40]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 3), new Point(Offset + 0, Offset + 6), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 1), new Point(Offset + 1, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 7), new Point(Offset + 1, Offset + 8), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 2, Offset + 0, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 2, Offset + 9, GoodReleasedColour);
            released[40] = letter;
        }
        // )
        {
            Sprite letter = Create(3 + Offset * 2, 10 + Offset * 2, released[41]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 3), new Point(Offset + 2, Offset + 6), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 1), new Point(Offset + 1, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 7), new Point(Offset + 1, Offset + 8), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 0, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 9, GoodReleasedColour);
            released[41] = letter;
        }
        //*
        {
            Sprite letter = Create(5 + Offset * 2, 10 + Offset * 2, released[42]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 2, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 4, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 4), new Point(Offset + 4, Offset + 1), GoodReleasedColour);
            released[42] = letter;
        }
        // +
        {
            Sprite letter = Create(5 + Offset * 2, 10 + Offset * 2, released[43]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 5), new Point(Offset + 4, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 3), new Point(Offset + 2, Offset + 7), GoodReleasedColour);
            released[43] = letter;
        }
        // ,
        {
            Sprite letter = Create(3 + Offset * 2, 11 + Offset * 2, released[44]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 8), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 2, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 10), new Point(Offset + 1, Offset + 10), GoodReleasedColour);
            released[44] = letter;
        }
        // -
        {
            Sprite letter = Create(6 + Offset * 2, 6 + Offset * 2, released[45]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 5), new Point(Offset + 5, Offset + 5), GoodReleasedColour);
            released[45] = letter;
        }
        // .
        {
            Sprite letter = Create(2 + Offset * 2, 10 + Offset * 2, released[46]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 8), new Point(Offset + 1, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 1, Offset + 9), GoodReleasedColour);
            released[46] = letter;
        }
        // /
        {
            Sprite letter = Create(4 + Offset * 2, 10 + Offset * 2, released[47]);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 0), new Point(Offset + 0, Offset + 9), GoodReleasedColour);
            released[47] = letter;
        }
        // 0
        {
            Sprite letter = Create(9 + Offset * 2, 10 + Offset * 2, released[48]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 6, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 2), new Point(Offset + 0, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 9), new Point(Offset + 6, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 2), new Point(Offset + 8, Offset + 7), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 7, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 7, Offset + 8, GoodReleasedColour);
            released[48] = letter;
        }
        // 1
        {
            Sprite letter = Create(5 + Offset * 2, 10 + Offset * 2, released[49]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 2, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 2, GoodReleasedColour);
            released[49] = letter;
        }
        // 2
        {
            Sprite letter = Create(7 + Offset * 2, 10 + Offset * 2, released[50]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 5, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 1), new Point(Offset + 6, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 4), new Point(Offset + 0, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 6, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 2), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 8, GoodReleasedColour);
            released[50] = letter;
        }
        // 3
        {
            Sprite letter = Create(7 + Offset * 2, 10 + Offset * 2, released[51]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 5, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 1), new Point(Offset + 6, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 4), new Point(Offset + 5, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 5), new Point(Offset + 6, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 8, GoodReleasedColour);
            released[51] = letter;
        }
        // 4
        {
            Sprite letter = Create(8 + Offset * 2, 10 + Offset * 2, released[52]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 4), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 0), new Point(Offset + 5, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 6), new Point(Offset + 5, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 5), new Point(Offset + 7, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            released[52] = letter;
        }
        // 5
        {
            Sprite letter = Create(7 + Offset * 2, 10 + Offset * 2, released[53]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 6, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 4), new Point(Offset + 5, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 5), new Point(Offset + 6, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 8, GoodReleasedColour);
            released[53] = letter;
        }
        // 6
        {
            Sprite letter = Create(7 + Offset * 2, 10 + Offset * 2, released[54]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 5, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 4), new Point(Offset + 5, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 5), new Point(Offset + 6, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 1, GoodReleasedColour);
            released[54] = letter;
        }
        // 7
        {
            Sprite letter = Create(7 + Offset * 2, 10 + Offset * 2, released[55]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 5, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 0), new Point(Offset + 2, Offset + 9), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 9, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 3, Offset + 9, GoodReleasedColour);
            released[55] = letter;
        }
        // 8
        {
            Sprite letter = Create(7 + Offset * 2, 10 + Offset * 2, released[56]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 5, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 1), new Point(Offset + 6, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 4), new Point(Offset + 5, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 5), new Point(Offset + 0, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 5), new Point(Offset + 6, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            released[56] = letter;
        }
        // 9
        {
            Sprite letter = Create(7 + Offset * 2, 10 + Offset * 2, released[57]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 5, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 1), new Point(Offset + 6, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 5), new Point(Offset + 5, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 8, GoodReleasedColour);
            released[57] = letter;
        }
        // :
        {
            Sprite letter = Create(2 + Offset * 2, 10 + Offset * 2, released[58]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 3), new Point(Offset + 1, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 4), new Point(Offset + 1, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 8), new Point(Offset + 1, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 1, Offset + 9), GoodReleasedColour);
            released[58] = letter;
        }
        // ;
        {
            Sprite letter = Create(3 + Offset * 2, 11 + Offset * 2, released[59]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 3), new Point(Offset + 2, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 4), new Point(Offset + 2, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 8), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 2, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 10), new Point(Offset + 1, Offset + 10), GoodReleasedColour);
            released[59] = letter;
        }
        // <
        {
            Sprite letter = Create(4 + Offset * 2, 10 + Offset * 2, released[60]);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 2), new Point(Offset + 0, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 8), new Point(Offset + 1, Offset + 6), GoodReleasedColour);
            released[60] = letter;
        }
        // =
        {
            Sprite letter = Create(6 + Offset * 2, 8 + Offset * 2, released[61]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 3), new Point(Offset + 5, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 7), new Point(Offset + 5, Offset + 7), GoodReleasedColour);
            released[61] = letter;
        }
        // >
        {
            Sprite letter = Create(4 + Offset * 2, 10 + Offset * 2, released[62]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 2), new Point(Offset + 3, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 8), new Point(Offset + 2, Offset + 6), GoodReleasedColour);
            released[62] = letter;
        }
        // ?
        {
            Sprite letter = Create(6 + Offset * 2, 10 + Offset * 2, released[63]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 1), new Point(Offset + 5, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 3), new Point(Offset + 3, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 8), new Point(Offset + 3, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 9), new Point(Offset + 3, Offset + 9), GoodReleasedColour);
            released[63] = letter;
        }
        // A
        {
            Sprite letter = Create(13 + Offset * 2, 10 + Offset * 2, released[65]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 9), new Point(Offset + 12, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 5), new Point(Offset + 8, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 8), new Point(Offset + 4, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 1), new Point(Offset + 10, Offset + 8), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 4, Offset + 4, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 5, Offset + 3, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 5, Offset + 2, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 0, GoodReleasedColour);
            released[65] = letter;
        }
        // B
        {
            Sprite letter = Create(11 + Offset * 2, 10 + Offset * 2, released[66]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 8, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 8, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 5), new Point(Offset + 9, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 2), new Point(Offset + 10, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 6), new Point(Offset + 10, Offset + 7), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 9, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 9, Offset + 1, GoodReleasedColour);
            released[66] = letter;
        }
        // C
        {
            Sprite letter = Create(10 + Offset * 2, 10 + Offset * 2, released[67]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 7, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 2), new Point(Offset + 0, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 0), new Point(Offset + 9, Offset + 2), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 9, Offset + 7, GoodReleasedColour);
            released[67] = letter;
        }
        // D
        {
            Sprite letter = Create(11 + Offset * 2, 10 + Offset * 2, released[68]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 8, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 8, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 2), new Point(Offset + 10, Offset + 7), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 9, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 9, Offset + 8, GoodReleasedColour);
            released[68] = letter;
        }
        // E
        {
            Sprite letter = Create(9 + Offset * 2, 10 + Offset * 2, released[69]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 8, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 8, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 4), new Point(Offset + 6, Offset + 4), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 3, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 5, GoodReleasedColour);
            released[69] = letter;
        }
        // F
        {
            Sprite letter = Create(9 + Offset * 2, 10 + Offset * 2, released[70]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 8, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 3, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 4), new Point(Offset + 6, Offset + 4), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 3, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 6, Offset + 5, GoodReleasedColour);
            released[70] = letter;
        }
        // G
        {
            Sprite letter = Create(11 + Offset * 2, 10 + Offset * 2, released[71]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 7, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 2), new Point(Offset + 0, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 5), new Point(Offset + 10, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 0), new Point(Offset + 9, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 6), new Point(Offset + 9, Offset + 7), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 8, GoodReleasedColour);
            released[71] = letter;
        }
        // H
        {
            Sprite letter = Create(13 + Offset * 2, 10 + Offset * 2, released[72]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 0), new Point(Offset + 12, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 9), new Point(Offset + 12, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 1), new Point(Offset + 10, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 5), new Point(Offset + 9, Offset + 5), GoodReleasedColour);
            released[72] = letter;
        }
        // I
        {
            Sprite letter = Create(5 + Offset * 2, 10 + Offset * 2, released[73]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            released[73] = letter;
        }
        // J
        {
            Sprite letter = Create(8 + Offset * 2, 10 + Offset * 2, released[74]);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 0), new Point(Offset + 7, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 1), new Point(Offset + 5, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 7), new Point(Offset + 0, Offset + 8), GoodReleasedColour);
            released[74] = letter;
        }
        // K
        {
            Sprite letter = Create(12 + Offset * 2, 10 + Offset * 2, released[75]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 4), new Point(Offset + 5, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 3), new Point(Offset + 8, Offset + 1), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 5), new Point(Offset + 9, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 0), new Point(Offset + 10, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 9), new Point(Offset + 11, Offset + 9), GoodReleasedColour);
            released[75] = letter;
        }
        // L
        {
            Sprite letter = Create(9 + Offset * 2, 10 + Offset * 2, released[76]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 8, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 8, GoodReleasedColour);
            released[76] = letter;
        }
        // M
        {
            Sprite letter = Create(15 + Offset * 2, 10 + Offset * 2, released[77]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 2, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 1), new Point(Offset + 7, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 4), new Point(Offset + 11, Offset + 1), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 12, Offset + 1), new Point(Offset + 12, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 12, Offset + 0), new Point(Offset + 14, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 9), new Point(Offset + 14, Offset + 9), GoodReleasedColour);
            released[77] = letter;
        }
        // N
        {
            Sprite letter = Create(14 + Offset * 2, 10 + Offset * 2, released[78]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 1, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 1), new Point(Offset + 10, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 0), new Point(Offset + 13, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 11, Offset + 0), new Point(Offset + 11, Offset + 9), GoodReleasedColour);
            released[78] = letter;
        }
        // O
        {
            Sprite letter = Create(10 + Offset * 2, 10 + Offset * 2, released[79]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 7, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 2), new Point(Offset + 0, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 2), new Point(Offset + 9, Offset + 7), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 8, GoodReleasedColour);
            released[79] = letter;
        }
        // P
        {
            Sprite letter = Create(11 + Offset * 2, 10 + Offset * 2, released[80]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 9, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 5), new Point(Offset + 9, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 1), new Point(Offset + 10, Offset + 4), GoodReleasedColour);
            released[80] = letter;
        }
        // Q
        {
            Sprite letter = Create(11 + Offset * 2, 11 + Offset * 2, released[81]);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 0), new Point(Offset + 7, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 2), new Point(Offset + 0, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 2), new Point(Offset + 9, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 7), new Point(Offset + 5, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 7), new Point(Offset + 9, Offset + 10), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 10), new Point(Offset + 11, Offset + 10), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 1, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 12, Offset + 9, GoodReleasedColour);
            released[81] = letter;
        }
        // R
        {
            Sprite letter = Create(12 + Offset * 2, 10 + Offset * 2, released[82]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 9, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 9), new Point(Offset + 11, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 5), new Point(Offset + 9, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 1), new Point(Offset + 10, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 6), new Point(Offset + 9, Offset + 8), GoodReleasedColour);
            released[82] = letter;
        }
        // S
        {
            Sprite letter = Create(9 + Offset * 2, 10 + Offset * 2, released[83]);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 7, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 4), new Point(Offset + 7, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 5), new Point(Offset + 8, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 8), new Point(Offset + 1, Offset + 8), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 1, GoodReleasedColour);
            released[83] = letter;
        }
        // T
        {
            Sprite letter = Create(11 + Offset * 2, 10 + Offset * 2, released[84]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 10, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 1), new Point(Offset + 5, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 1), new Point(Offset + 0, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 1), new Point(Offset + 10, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 9), new Point(Offset + 6, Offset + 9), GoodReleasedColour);
            released[84] = letter;
        }
        // U
        {
            Sprite letter = Create(13 + Offset * 2, 10 + Offset * 2, released[85]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 0), new Point(Offset + 12, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 2, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 1), new Point(Offset + 10, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 9), new Point(Offset + 8, Offset + 9), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 3, Offset + 8, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 9, Offset + 8, GoodReleasedColour);
            released[85] = letter;
        }
        // V
        {
            Sprite letter = Create(11 + Offset * 2, 10 + Offset * 2, released[86]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 0), new Point(Offset + 10, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 1), new Point(Offset + 6, Offset + 7), GoodReleasedColour);
            released[86] = letter;
        }
        // W
        {
            Sprite letter = Create(17 + Offset * 2, 10 + Offset * 2, released[87]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 4, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 0), new Point(Offset + 9, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 12, Offset + 0), new Point(Offset + 16, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 5, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 1), new Point(Offset + 6, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 3), new Point(Offset + 10, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 14, Offset + 1), new Point(Offset + 11, Offset + 9), GoodReleasedColour);
            released[87] = letter;
        }
        // X
        {
            Sprite letter = Create(12 + Offset * 2, 10 + Offset * 2, released[88]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 3, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 0), new Point(Offset + 11, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 3, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 9), new Point(Offset + 11, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 9, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 8), new Point(Offset + 9, Offset + 1), GoodReleasedColour);
            released[88] = letter;
        }
        // Y
        {
            Sprite letter = Create(11 + Offset * 2, 10 + Offset * 2, released[89]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 3, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 0), new Point(Offset + 10, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 1), new Point(Offset + 4, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 3), new Point(Offset + 8, Offset + 1), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 4), new Point(Offset + 5, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            released[89] = letter;
        }
        // Z
        {
            Sprite letter = Create(9 + Offset * 2, 10 + Offset * 2, released[90]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 8, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 8, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 1), new Point(Offset + 0, Offset + 8), GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 1, GoodReleasedColour);
            ImageOps.SetPixel(letter, Offset + 8, Offset + 8, GoodReleasedColour);
            released[90] = letter;
        }
        // [
        {
            Sprite letter = Create(4 + Offset * 2, 10 + Offset * 2, released[91]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 0, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 0), new Point(Offset + 3, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 3, Offset + 9), GoodReleasedColour);
            released[91] = letter;
        }
        // '\'
        {
            Sprite letter = Create(4 + Offset * 2, 10 + Offset * 2, released[92]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 3, Offset + 9), GoodReleasedColour);
            released[92] = letter;
        }
        // ]
        {
            Sprite letter = Create(4 + Offset * 2, 10 + Offset * 2, released[93]);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 0), new Point(Offset + 3, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 0), new Point(Offset + 2, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 9), new Point(Offset + 2, Offset + 9), GoodReleasedColour);
            released[93] = letter;
        }
        // ^
        {
            Sprite letter = Create(5 + Offset * 2, 3 + Offset * 2, released[94]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 2), new Point(Offset + 2, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 1), new Point(Offset + 4, Offset + 2), GoodReleasedColour);
            released[94] = letter;
        }
        // _
        {
            Sprite letter = Create(8 + Offset * 2, 11 + Offset * 2, released[95]);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 10), new Point(Offset + 7, Offset + 10), GoodReleasedColour);
            released[95] = letter;
        }
        // | - replaced with Caps Lock symbol for virtual keyboard
        // TODO: put the Caps Lock symbol to a special font to not replace any other ASCII character.
        {
            Sprite letter = Create(11 + Offset * 2, 11 + Offset * 2, released[124]);
            ImageOps.SetPixel(letter, Offset + 5, Offset + 0, GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 1), new Point(Offset + 6, Offset + 1), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 2), new Point(Offset + 7, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 3), new Point(Offset + 8, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 4), new Point(Offset + 9, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 5), new Point(Offset + 10, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 6), new Point(Offset + 7, Offset + 6), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 7), new Point(Offset + 7, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 9), new Point(Offset + 7, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 10), new Point(Offset + 7, Offset + 10), GoodReleasedColour);
            released[124] = letter;
        }
        // ~ - replaced with Backspace symbol (<x]) for virtual keyboard
        // TODO: put the Backspace symbol to a special font to not replace any other ASCII character.
        {
            Sprite letter = Create(17 + Offset * 2, 9 + Offset * 2, released[126]);
            ImageOps.SetPixel(letter, Offset + 0, Offset + 4, GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 3), new Point(Offset + 1, Offset + 5), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 2, Offset + 2), new Point(Offset + 2, Offset + 6), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 3, Offset + 1), new Point(Offset + 3, Offset + 7), GoodReleasedColour);
            ImageOps.FillSprite(letter, new Rect(Offset + 4, Offset + 0, 13, 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 6, Offset + 1), new Point(Offset + 12, Offset + 7), 12);
            ImageOps.DrawLine(letter, new Point(Offset + 7, Offset + 1), new Point(Offset + 13, Offset + 7), 12);
            ImageOps.DrawLine(letter, new Point(Offset + 12, Offset + 1), new Point(Offset + 6, Offset + 7), 12);
            ImageOps.DrawLine(letter, new Point(Offset + 13, Offset + 1), new Point(Offset + 7, Offset + 7), 12);
            released[126] = letter;
        }
        // Replaced with Change Language symbol for virtual keyboard
        // TODO: put the Change Language symbol to a special font to not replace any other ASCII character.
        {
            Sprite letter = Create(14 + Offset * 2, 13 + Offset * 2, released[127]);
            letter = Moved(letter, FontOffset.X, FontOffset.Y - 2);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 0), new Point(Offset + 9, Offset + 0), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 2), new Point(Offset + 2, Offset + 1), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 3), new Point(Offset + 3, Offset + 1), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 1), new Point(Offset + 12, Offset + 3), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 11, Offset + 1), new Point(Offset + 12, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 4), new Point(Offset + 13, Offset + 4), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 8), new Point(Offset + 13, Offset + 8), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 9), new Point(Offset + 3, Offset + 11), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 1, Offset + 10), new Point(Offset + 2, Offset + 11), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 12), new Point(Offset + 9, Offset + 12), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 10, Offset + 11), new Point(Offset + 12, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 11, Offset + 11), new Point(Offset + 12, Offset + 10), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 4, Offset + 3), new Point(Offset + 4, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 9, Offset + 3), new Point(Offset + 9, Offset + 9), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 1), new Point(Offset + 5, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 1), new Point(Offset + 8, Offset + 2), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 5, Offset + 10), new Point(Offset + 5, Offset + 11), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 8, Offset + 10), new Point(Offset + 8, Offset + 11), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 0, Offset + 5), new Point(Offset + 0, Offset + 7), GoodReleasedColour);
            ImageOps.DrawLine(letter, new Point(Offset + 13, Offset + 5), new Point(Offset + 13, Offset + 7), GoodReleasedColour);
            released[127] = letter;
        }

        return released;
    }
}