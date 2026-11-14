using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// Resolves single characters into glyphs and glyph widths, the reference's
/// <c>fheroes2::FontCharHandler</c> plus the font helpers of <c>ui_text.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_text.h:383</c> class FontCharHandler（<c>isAvailable</c> 388、
/// <c>getSprite</c> 394、<c>getWidth</c> 397/400、<c>getSpaceCharWidth</c> 403、<c>_isValid</c> 406、
/// <c>_getSpaceCharWidth</c> 409、成员 412–415）。
/// </para>
/// <para>
/// 同文件搬来的自由函数：<c>isLineSeparator</c>（<c>ui_text.cpp:105</c>）、<c>isSpaceChar</c>（110）、
/// <c>getFontHeight</c>（114）、<c>getCharacterLimit</c>（143）、<c>getChar</c>（244）。
/// </para>
/// <para>
/// 差异：参考对未生成的字体走 <c>assert( 0 )</c> 再返回 <c>errorImage</c>，这里抛
/// <see cref="NotSupportedException"/>，因为那些字形来自还没移植的启动期生成器；缺帧时仍和参考一样
/// 退化成空图。
/// </para>
/// <para>
/// 字形就是 ICN 的逐帧图像，全部经由 <see cref="FontGlyphTable"/> 取：白色是修补过的原始表，
/// 黄色和灰色由它重映射调色板得到（参考的 <c>CopyICNWithPalette</c>），大号是它放大两倍，
/// 两个渐变面则是启动期画出来的。
/// </para>
/// </remarks>
public sealed class FontCharHandler
{
    /// <summary>The hyphen a line is allowed to break after.</summary>
    public const byte HyphenChar = (byte)'-';

    /// <summary>The character drawn for bytes outside the font.</summary>
    public const byte InvalidChar = (byte)'?';

    /// <summary>Frame 0 of a font ICN holds the glyph of <c>' '</c> (0x20).</summary>
    /// <remarks>参考在加载时往前补 32 个空帧，让「字符码」直接当「帧号」用；C# 侧直接按 <c>character - GlyphFrameOffset</c> 查原始帧。</remarks>
    public const uint GlyphFrameOffset = 0x20;

    private static readonly Sprite ErrorImage = new(0, 0);

    private readonly FontType _fontType;
    private readonly uint _charLimit;
    private readonly int _spaceCharWidth;

    /// <summary>Creates a handler for the given font.</summary>
    /// <param name="fontType">Glyph size and colour.</param>
    public FontCharHandler(in FontType fontType)
    {
        _fontType = fontType;
        _charLimit = GetCharacterLimit(fontType.Size);
        _spaceCharWidth = GetSpaceCharWidth();
    }

    /// <summary>Width of a space in this font.</summary>
    public int SpaceCharWidth => _spaceCharWidth;

    /// <summary>Whether <paramref name="character"/> can be drawn.</summary>
    /// <param name="character">A byte of the text.</param>
    /// <returns><see langword="true"/> for spaces, line separators and bytes inside the font.</returns>
    public bool IsAvailable(byte character) =>
        IsSpaceChar(character) || IsValid(character) || IsLineSeparator(character);

    /// <summary>Glyph of <paramref name="character"/>; bytes outside the font give the '?' glyph.</summary>
    /// <param name="character">A byte of the text.</param>
    /// <returns>The glyph; empty when the archive has no such frame.</returns>
    public Sprite GetSprite(byte character) => GetChar(IsValid(character) ? character : InvalidChar, _fontType);

    /// <summary>Advance width of one character.</summary>
    /// <param name="character">A byte of the text.</param>
    /// <returns>Width in pixels; 0 for a line separator and for a byte the archive has no glyph for.</returns>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1352</c> <c>FontCharHandler::getWidth( uint8_t )</c>。</remarks>
    public int GetWidth(byte character)
    {
        if (IsSpaceChar(character))
        {
            return _spaceCharWidth;
        }

        if (IsLineSeparator(character))
        {
            return 0;
        }

        Sprite sprite = GetChar(character, _fontType);

        return sprite.OffsetX + sprite.Width;
    }

    /// <summary>Width of a run of characters.</summary>
    /// <param name="text">Bytes of the text.</param>
    /// <returns>Width in pixels.</returns>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1369</c> <c>FontCharHandler::getWidth( std::string_view )</c>。</remarks>
    public int GetWidth(ReadOnlySpan<byte> text)
    {
        int width = 0;

        foreach (byte character in text)
        {
            width += GetWidth(character);
        }

        return width;
    }

    /// <summary>Width of a run of characters, the reference's <c>getWidth( text, characterWidthFallback )</c>.</summary>
    /// <param name="text">Bytes of the text.</param>
    /// <param name="characterWidthFallback">Width to use for a character the font cannot measure.</param>
    /// <returns>Width in pixels, at least 1.</returns>
    public int GetWidth(ReadOnlySpan<byte> text, int characterWidthFallback)
    {
        int width = 0;

        foreach (byte character in text)
        {
            width += IsSpaceChar(character) ? _spaceCharWidth : Math.Max(1, characterWidthFallback);
        }

        return width > 0 ? width : 1;
    }

    private bool IsValid(byte character) => character >= 0x21 && character <= _charLimit;

    private int GetSpaceCharWidth()
    {
        if (_charLimit == 0)
        {
            return 0;
        }

        // The space has no glyph of its own: it is a third of the letter M, rounded down.
        Sprite charM = GetChar((byte)'M', _fontType);

        if (charM.Width > 0)
        {
            return (charM.Width - 1) / 3;
        }

        return 0;
    }

    /// <summary><c>isLineSeparator</c>: only '\n' breaks a line.</summary>
    /// <param name="character">A byte of the text.</param>
    /// <returns><see langword="true"/> for '\n'.</returns>
    public static bool IsLineSeparator(byte character) => character == '\n';

    /// <summary><c>isSpaceChar</c>.</summary>
    /// <param name="character">A byte of the text.</param>
    /// <returns><see langword="true"/> for ' '.</returns>
    public static bool IsSpaceChar(byte character) => character == ' ';

    /// <summary><c>getFontHeight</c>.</summary>
    /// <param name="size">Font size.</param>
    /// <returns>Line height in pixels.</returns>
    public static int GetFontHeight(FontSize size)
    {
        switch (size)
        {
            case FontSize.Small:
                return 13;

            case FontSize.Normal:
            case FontSize.Large:
                return 15;

            case FontSize.ButtonReleased:
            case FontSize.ButtonPressed:
                return 16;

            default:
                return 0;
        }
    }

    /// <summary><c>getCharacterLimit</c>: the highest byte the font has a glyph for.</summary>
    /// <param name="size">Font size.</param>
    /// <returns>The highest character code the font covers, a space included.</returns>
    /// <remarks>
    /// 这是「这张表有多长」，所以要问已经排好的表：译版资材的帧序和 ASCII 对不上，加载期会重排
    /// （俄语 CP1251 有 224 帧、德语 CP1252 有 221 帧），拿归档里的原始帧数会算错。
    /// 参考实现见 <c>FontGlyphTable.GetCharacterLimit</c>。
    /// </remarks>
    public static uint GetCharacterLimit(FontSize size) => FontGlyphTable.GetCharacterLimit(size);

    /// <summary><c>getChar</c>: the glyph of a character in a font.</summary>
    /// <param name="character">The byte of the text.</param>
    /// <param name="fontType">Glyph size and colour.</param>
    /// <returns>The glyph, or an empty sprite when the face has none.</returns>
    /// <exception cref="NotSupportedException">The font colour is generated at start up and not ported.</exception>
    /// <remarks>
    /// <para>
    /// 参考的 <c>Assets::getImage( ICN::FONT, character )</c> 里 <c>character</c> 就是 ASCII 码，因为加载时前置了
    /// 32 个空帧；归档里的原始帧 0 才是 <c>' '</c>，所以位图字体要减去 <see cref="GlyphFrameOffset"/>。
    /// 按钮字体是另画的整张 ASCII 表，下标就是字符本身，不减。
    /// </para>
    /// <para>
    /// 四种字号都交给 <see cref="FontGlyphTable"/> 决定该拿哪张表：参考的
    /// <c>ICN::YELLOW_FONT</c> / <c>GRAY_FONT</c> 是白色表过一遍调色板，
    /// <c>ICN::WHITE_LARGE_FONT</c> 是白色表放大两倍，两个渐变面另起一套画法，
    /// 按钮字体是启动期画出来的。哪种字号配哪种颜色合法也由那张表管（参考 <c>ui_text.cpp:61</c> 的
    /// <c>getChar</c> 只对大号开放白、金渐变、银渐变，对按钮只开放白和灰）。
    /// </para>
    /// </remarks>
    public static Sprite GetChar(byte character, in FontType fontType)
    {
        int frame = fontType.Size is FontSize.ButtonReleased or FontSize.ButtonPressed
            ? character
            : character - (int)GlyphFrameOffset;

        return FontGlyphTable.Get(fontType.Size, fontType.Color, frame) ?? ErrorImage;
    }
}
