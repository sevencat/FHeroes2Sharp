using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Fonts;

/// <summary>
/// Layout and drawing shared by every <see cref="IGlyphSource"/>: it turns a string into glyph
/// sprites plus advances, which is the part that must stay identical whether the pixels come from
/// <c>FONT.ICN</c> or from a TrueType face.
/// </summary>
/// <remarks>
/// The rules are ported from <c>fheroes2::FontCharHandler</c> / <c>renderSingleLine()</c>:
/// a glyph is drawn at <c>pen + sprite.OffsetX</c>, the pen then moves by
/// <see cref="IGlyphSource.GetAdvance"/>, <c>\n</c> breaks the line, wrapping prefers a space and
/// trailing spaces never widen a line unless the caller asks for it.
/// </remarks>
public static class GlyphText
{
    /// <summary>The character that advances the pen by <see cref="IGlyphSource.SpaceWidth"/>.</summary>
    public const char SpaceCharacter = ' ';

    /// <summary>The line separator; it starts a new line in <see cref="DrawMultiline"/>.</summary>
    public const char LineSeparator = '\n';

    /// <summary>
    /// What <see cref="FitToOneRow"/> appends when a text has to be cut, <c>truncationSymbol</c> of
    /// <c>ui_text.cpp</c>.
    /// </summary>
    public const string TruncationSymbol = "...";

    /// <summary>
    /// Width of a single line, the equivalent of <c>getLineWidth()</c>. Trailing spaces are
    /// ignored unless <paramref name="keepTrailingSpaces"/> is set, which is how multi line text
    /// joins lines without losing the separating space.
    /// </summary>
    public static int Measure(IGlyphSource source, string text, bool keepTrailingSpaces = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);

        if (keepTrailingSpaces)
        {
            int total = 0;
            foreach (char character in text)
            {
                total += source.GetAdvance(character);
            }

            return total;
        }

        int width = 0;
        int pendingSpaces = 0;

        foreach (char character in text)
        {
            if (character == SpaceCharacter)
            {
                pendingSpaces += source.SpaceWidth;
            }
            else if (character != LineSeparator)
            {
                width += pendingSpaces + source.GetAdvance(character);
                pendingSpaces = 0;
            }
        }

        return width;
    }

    /// <summary>
    /// Cuts <paramref name="text"/> so that it fits into <paramref name="maxWidth"/> and appends
    /// <see cref="TruncationSymbol"/>, the equivalent of <c>Text::fitToOneRow()</c>
    /// (<c>ui_text.cpp:495-522</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考先量一遍整行：放得下就原样返回；放不下就按「maxWidth 减去省略号宽度」能放下的最大字符数截断，
    /// 再补 <c>"..."</c>。截断点是字符（UTF-16 码元）边界，代理对整对保留，行分隔符不计宽度。
    /// </para>
    /// <para>
    /// 战斗状态栏的上、下两行与战斗日志的每一行都用它——参考在这三处都是同一个调用。
    /// </para>
    /// </remarks>
    /// <param name="source">The font that measures the text.</param>
    /// <param name="text">The text to fit; it is not modified.</param>
    /// <param name="maxWidth">The widest the result may be, in logical pixels.</param>
    /// <returns>The text itself, or a cut version ending in <see cref="TruncationSymbol"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="text"/> is null.</exception>
    public static string FitToOneRow(IGlyphSource source, string text, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);

        if (maxWidth <= 0 || text.Length == 0)
        {
            return text;
        }

        if (Measure(source, text) <= maxWidth)
        {
            return text;
        }

        int budget = maxWidth - Measure(source, TruncationSymbol);
        if (budget <= 0)
        {
            return TruncationSymbol;
        }

        int width = 0;
        int count = 0;

        while (count < text.Length)
        {
            char character = text[count];

            if (character == LineSeparator)
            {
                ++count;
                continue;
            }

            int step = 1;
            int advance;

            if (char.IsHighSurrogate(character) && count + 1 < text.Length && char.IsLowSurrogate(text[count + 1]))
            {
                step = 2;
                advance = source.GetAdvance(character) + source.GetAdvance(text[count + 1]);
            }
            else
            {
                advance = source.GetAdvance(character);
            }

            if (width + advance > budget)
            {
                break;
            }

            width += advance;
            count += step;
        }

        return string.Concat(text.AsSpan(0, count), TruncationSymbol);
    }

    /// <summary>
    /// Draws <paramref name="text"/> as one line and returns the pen position after the last
    /// character, the equivalent of <c>renderSingleLine()</c>. A line separator inside the text is
    /// ignored, as a single line cannot contain one.
    /// </summary>
    public static int DrawLine(IGlyphSource source, PixelBuffer target, Point position, string text) =>
        DrawLine(source, target, position, text, target.Bounds);

    /// <summary>Draws one line clipped to <paramref name="area"/>.</summary>
    public static int DrawLine(
        IGlyphSource source, PixelBuffer target, Point position, string text, in Rect area)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);

        Rect clip = area.Intersect(target.Bounds);
        int offsetX = position.X;

        foreach (char character in text)
        {
            if (character == SpaceCharacter)
            {
                offsetX += source.SpaceWidth;
                continue;
            }

            if (character == LineSeparator)
            {
                continue;
            }

            // A missing character yields the provider's substitute, so the sprite is always usable.
            source.TryGetGlyph(character, out Sprite glyph);
            if (!glyph.Image.IsEmpty)
            {
                int charX = offsetX + glyph.OffsetX;
                int charY = position.Y + glyph.OffsetY;
                Rect charRect = new Rect(charX, charY, glyph.Width, glyph.Height).Intersect(clip);

                if (!charRect.IsEmpty)
                {
                    Rect sourceArea = new(charRect.X - charX, charRect.Y - charY, charRect.Width, charRect.Height);
                    ImageOps.BlitSprite(target, new Point(charX, charY), glyph, sourceArea);
                }
            }

            offsetX += source.GetAdvance(character);
        }

        return offsetX;
    }

    /// <summary>
    /// Draws text with explicit line breaks and optional word wrapping and returns the covered
    /// area. Lines are <see cref="IGlyphSource.LineHeight"/> apart; with <paramref name="center"/>
    /// each line is centered inside <paramref name="maxWidth"/>.
    /// </summary>
    public static Size DrawMultiline(
        IGlyphSource source, PixelBuffer target, Point position, string text, int maxWidth = 0, bool center = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return Size.Empty;
        }

        List<string> lines = WrapLines(source, text, maxWidth);
        int widest = 0;

        for (int index = 0; index < lines.Count; ++index)
        {
            string line = lines[index];
            int lineWidth = Measure(source, line, keepTrailingSpaces: true);
            widest = Math.Max(widest, lineWidth);

            int x = center ? position.X + ((maxWidth - lineWidth) / 2) : position.X;
            DrawLine(source, target, new Point(x, position.Y + (index * source.LineHeight)), line);
        }

        return new Size(widest, lines.Count * source.LineHeight);
    }

    /// <summary>Splits <paramref name="text"/> into lines that fit <paramref name="maxWidth"/>.</summary>
    public static List<string> WrapLines(IGlyphSource source, string text, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);

        List<string> lines = [];

        foreach (string paragraph in text.Split(LineSeparator))
        {
            if (maxWidth <= 0)
            {
                lines.Add(paragraph);
                continue;
            }

            lines.AddRange(WrapParagraph(source, paragraph, maxWidth));
        }

        return lines;
    }

    private static List<string> WrapParagraph(IGlyphSource source, string paragraph, int maxWidth)
    {
        List<string> lines = [];
        int start = 0;

        while (start < paragraph.Length)
        {
            int end = start;
            int width = 0;
            int lastSpace = -1;

            while (end < paragraph.Length)
            {
                int advance = source.GetAdvance(paragraph[end]);
                if (width + advance > maxWidth && end > start)
                {
                    break;
                }

                width += advance;

                if (paragraph[end] == SpaceCharacter)
                {
                    lastSpace = end;
                }

                ++end;
            }

            // Prefer breaking at a space, but never at the one that starts the line. A paragraph
            // without any space therefore breaks between characters, which is what CJK needs.
            if (end < paragraph.Length && lastSpace > start)
            {
                end = lastSpace;
            }

            lines.Add(paragraph[start..end].TrimEnd(SpaceCharacter));

            start = end;
            while (start < paragraph.Length && paragraph[start] == SpaceCharacter)
            {
                ++start;
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }
}
