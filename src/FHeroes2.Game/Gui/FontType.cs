namespace FHeroes2.Game.Gui;

/// <summary>
/// Bitmap font sizes, the reference's <c>fheroes2::FontSize</c>.
/// </summary>
/// <remarks>参考：<c>src/fheroes2/gui/ui_text.h:38</c> enum class FontSize。</remarks>
public enum FontSize : byte
{
    /// <summary><c>SMALFONT.ICN</c>.</summary>
    Small,

    /// <summary><c>FONT.ICN</c>, the body font of the dialogs.</summary>
    Normal,

    /// <summary><c>FONT.ICN</c> doubled: the reference's <c>WHITE_LARGE_FONT</c>.</summary>
    /// <remarks>参考 <c>ui_text.cpp:61</c> 给大号只准备白色与两个渐变面，没有黄色和灰色。</remarks>
    Large,

    /// <summary>The good interface button font, grown at start up (not ported).</summary>
    ButtonReleased,

    /// <summary>The evil interface button font, grown at start up (not ported).</summary>
    ButtonPressed,
}

/// <summary>
/// Bitmap font colours, the reference's <c>fheroes2::FontColor</c>.
/// </summary>
/// <remarks>
/// 参考：<c>src/fheroes2/gui/ui_text.h:48</c> enum class FontColor。只有 <see cref="White"/> 在归档里
/// 有真实的 ICN；<see cref="Gray"/> 与 <see cref="Yellow"/> 由 <c>FontGlyphTable</c> 在首次取用时
/// 从白色字形重映射调色板生成（参考的 <c>CopyICNWithPalette</c>），两个渐变面同样由它按
/// <c>generateGradientFont</c> 现画出来。
/// </remarks>
public enum FontColor : byte
{
    /// <summary><c>FONT.ICN</c> / <c>SMALFONT.ICN</c> as they are.</summary>
    White,

    /// <summary>Gray face of the white glyphs.</summary>
    Gray,

    /// <summary>Yellow face of the white glyphs.</summary>
    Yellow,

    /// <summary>Golden gradient face, drawn from the white glyphs.</summary>
    GoldenGradient,

    /// <summary>Silver gradient face, drawn from the white glyphs.</summary>
    SilverGradient,
}

/// <summary>
/// Size plus colour of a text run, the reference's <c>fheroes2::FontType</c>.
/// </summary>
/// <remarks>
/// 参考：<c>src/fheroes2/gui/ui_text.h:57</c> struct FontType（默认 <c>NORMAL</c> + <c>WHITE</c>，
/// 静态工厂 71–99 行）。
/// </remarks>
public struct FontType
{
    /// <summary>Default font: normal size, white.</summary>
    public FontType()
    {
        Size = FontSize.Normal;
        Color = FontColor.White;
    }

    /// <summary>Creates a font of the given size and colour.</summary>
    /// <param name="size">Glyph size.</param>
    /// <param name="color">Glyph colour.</param>
    public FontType(FontSize size, FontColor color)
    {
        Size = size;
        Color = color;
    }

    /// <summary>Glyph size.</summary>
    public FontSize Size { get; set; }

    /// <summary>Glyph colour.</summary>
    public FontColor Color { get; set; }

    /// <summary><c>FontType::normalWhite()</c>.</summary>
    /// <returns>Normal size, white.</returns>
    public static FontType NormalWhite() => new(FontSize.Normal, FontColor.White);

    /// <summary><c>FontType::normalYellow()</c>.</summary>
    /// <returns>Normal size, yellow.</returns>
    public static FontType NormalYellow() => new(FontSize.Normal, FontColor.Yellow);

    /// <summary><c>FontType::smallWhite()</c>.</summary>
    /// <returns>Small size, white.</returns>
    public static FontType SmallWhite() => new(FontSize.Small, FontColor.White);

    /// <summary><c>FontType::smallYellow()</c>.</summary>
    /// <returns>Small size, yellow.</returns>
    public static FontType SmallYellow() => new(FontSize.Small, FontColor.Yellow);

    /// <summary><c>FontType::largeWhite()</c>.</summary>
    /// <returns>Large size, white.</returns>
    public static FontType LargeWhite() => new(FontSize.Large, FontColor.White);

    /// <summary><c>FontType::buttonReleasedWhite()</c>.</summary>
    /// <returns>Released button font, white.</returns>
    public static FontType ButtonReleasedWhite() => new(FontSize.ButtonReleased, FontColor.White);
}

/// <summary>
/// One laid out line of text, the reference's <c>fheroes2::TextLineInfo</c>.
/// </summary>
/// <remarks>参考：<c>src/fheroes2/gui/ui_text.h:102</c> struct TextLineInfo。</remarks>
public struct TextLineInfo
{
    /// <summary>Creates a line description.</summary>
    /// <param name="offsetX">Horizontal shift of the line.</param>
    /// <param name="offsetY">Vertical shift of the line.</param>
    /// <param name="lineWidth">Width of the line, <paramref name="offsetX"/> included.</param>
    /// <param name="characterCount">Characters on the line, the line separator included.</param>
    public TextLineInfo(int offsetX, int offsetY, int lineWidth, int characterCount)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        LineWidth = lineWidth;
        CharacterCount = characterCount;
    }

    /// <summary>Horizontal shift of the line.</summary>
    public int OffsetX { get; set; }

    /// <summary>Vertical shift of the line.</summary>
    public int OffsetY { get; set; }

    /// <summary>Width of the line, <see cref="OffsetX"/> included.</summary>
    public int LineWidth { get; set; }

    /// <summary>Characters on the line, the line separator included.</summary>
    public int CharacterCount { get; set; }
}
