using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The free functions of <c>ui_text.cpp</c>'s anonymous namespace.
/// </summary>
/// <remarks>
/// 参考：<c>src/fheroes2/gui/ui_text.cpp</c> 匿名命名空间——<c>truncationSymbol</c>（43）、
/// <c>getLineWidth</c>（162）、<c>getTextLineArea</c>（194）、<c>getMaxCharacterCount</c>（223）、
/// <c>renderSingleLine</c>（240）、<c>getMaxWordWidth</c>（279）；<c>getTruncationSymbolWidth</c> 在 1409。
/// </remarks>
internal static class TextAlgorithms
{
    /// <summary><c>truncationSymbol</c>: what is appended when a text is cut.</summary>
    internal static ReadOnlySpan<byte> TruncationSymbol => "..."u8;

    private static readonly Dictionary<FontSize, int> TruncationSymbolWidths = [];

    /// <summary><c>getLineWidth</c>: width of a run of characters drawn on one line.</summary>
    /// <param name="text">Bytes of the text.</param>
    /// <param name="charHandler">Glyph metrics of the font.</param>
    /// <param name="keepTrailingSpaces">Whether trailing spaces count.</param>
    /// <returns>Width in pixels.</returns>
    internal static int GetLineWidth(ReadOnlySpan<byte> text, FontCharHandler charHandler, bool keepTrailingSpaces)
    {
        int width = 0;

        if (keepTrailingSpaces)
        {
            foreach (byte character in text)
            {
                width += charHandler.GetWidth(character);
            }

            return width;
        }

        int spaceWidth = 0;

        foreach (byte character in text)
        {
            if (FontCharHandler.IsSpaceChar(character))
            {
                spaceWidth += charHandler.SpaceCharWidth;
            }
            else if (!FontCharHandler.IsLineSeparator(character))
            {
                width += spaceWidth + charHandler.GetWidth(character);
                spaceWidth = 0;
            }
        }

        return width;
    }

    /// <summary><c>getTextLineArea</c>: area covered by the glyphs of one line.</summary>
    /// <param name="text">Bytes of the text.</param>
    /// <param name="charHandler">Glyph metrics of the font.</param>
    /// <returns>The area, relative to the drawing origin.</returns>
    internal static Rect GetTextLineArea(ReadOnlySpan<byte> text, FontCharHandler charHandler)
    {
        Sprite firstSprite = charHandler.GetSprite(text[0]);
        Rect area = new(firstSprite.OffsetX, firstSprite.OffsetY, firstSprite.Width, firstSprite.Height);

        for (int index = 1; index < text.Length; ++index)
        {
            Sprite sprite = charHandler.GetSprite(text[index]);

            if (sprite.OffsetY < area.Y)
            {
                // This glyph is drawn higher than all previous ones: grow the area upwards.
                area = new Rect(area.X, sprite.OffsetY, area.Width, Math.Max(area.Height + (area.Y - sprite.OffsetY), sprite.Height));
            }
            else
            {
                area = new Rect(area.X, area.Y, area.Width, Math.Max(area.Height, sprite.OffsetY - area.Y + sprite.Height));
            }

            area = new Rect(area.X, area.Y, area.Width + sprite.OffsetX + sprite.Width, area.Height);
        }

        return area;
    }

    /// <summary><c>getMaxCharacterCount</c>: how many characters fit into <paramref name="maxWidth"/>.</summary>
    /// <param name="text">Bytes of the text.</param>
    /// <param name="charHandler">Glyph metrics of the font.</param>
    /// <param name="maxWidth">Maximum width in pixels.</param>
    /// <returns>The character count.</returns>
    internal static int GetMaxCharacterCount(ReadOnlySpan<byte> text, FontCharHandler charHandler, int maxWidth)
    {
        int width = 0;

        for (int characterCount = 0; characterCount < text.Length; ++characterCount)
        {
            width += charHandler.GetWidth(text[characterCount]);

            if (width > maxWidth)
            {
                return characterCount;
            }
        }

        return text.Length;
    }

    /// <summary><c>renderSingleLine</c>: draws one line of characters, clipped to a rectangle.</summary>
    /// <param name="text">Bytes of the line.</param>
    /// <param name="x">Left edge of the line origin.</param>
    /// <param name="y">Top edge of the line origin.</param>
    /// <param name="output">Frame buffer.</param>
    /// <param name="imageRoi">Clip rectangle.</param>
    /// <param name="charHandler">Glyph metrics of the font.</param>
    /// <returns>The horizontal offset reached after the last glyph.</returns>
    internal static int RenderSingleLine(ReadOnlySpan<byte> text, int x, int y, PixelBuffer output, in Rect imageRoi, FontCharHandler charHandler)
    {
        int offsetX = x;
        int spaceCharWidth = charHandler.SpaceCharWidth;

        foreach (byte character in text)
        {
            if (FontCharHandler.IsSpaceChar(character))
            {
                offsetX += spaceCharWidth;
                continue;
            }

            if (FontCharHandler.IsLineSeparator(character))
            {
                // A line cannot contain a line separator in its middle, but some UI texts do; ignore it.
                continue;
            }

            Sprite charSprite = charHandler.GetSprite(character);
            Rect charRoi = new(offsetX + charSprite.OffsetX, y + charSprite.OffsetY, charSprite.Width, charSprite.Height);
            Rect overlappedRoi = imageRoi.Intersect(charRoi);

            if (overlappedRoi.Width > 0 && overlappedRoi.Height > 0)
            {
                ImageOps.BlitSprite(output, new Point(overlappedRoi.X, overlappedRoi.Y), charSprite,
                    new Rect(overlappedRoi.X - charRoi.X, overlappedRoi.Y - charRoi.Y, overlappedRoi.Width, overlappedRoi.Height));
            }

            offsetX += charSprite.Width + charSprite.OffsetX;
        }

        return offsetX;
    }

    /// <summary><c>getMaxWordWidth</c>: width of the widest word.</summary>
    /// <param name="text">Bytes of the text.</param>
    /// <param name="fontType">Glyph size and colour.</param>
    /// <returns>Width in pixels, at least 1.</returns>
    internal static int GetMaxWordWidth(ReadOnlySpan<byte> text, in FontType fontType)
    {
        FontCharHandler charHandler = new(fontType);

        int maxWidth = 1;
        int width = 0;

        foreach (byte character in text)
        {
            if (FontCharHandler.IsSpaceChar(character) || FontCharHandler.IsLineSeparator(character))
            {
                if (width == 0 && FontCharHandler.IsSpaceChar(character))
                {
                    // No word yet on this line: use the space width as a lower bound.
                    width = charHandler.GetWidth(character);
                }

                if (maxWidth < width)
                {
                    maxWidth = width;
                }

                width = 0;
            }
            else
            {
                width += charHandler.GetWidth(character);
            }
        }

        return Math.Max(maxWidth, width);
    }

    /// <summary><c>getTruncationSymbolWidth</c>: width of "..." in a font size, cached per size.</summary>
    /// <param name="fontType">Glyph size and colour.</param>
    /// <returns>Width in pixels.</returns>
    internal static int GetTruncationSymbolWidth(in FontType fontType)
    {
        // The symbol width depends on the font size and not on its colour.
        lock (TruncationSymbolWidths)
        {
            if (TruncationSymbolWidths.TryGetValue(fontType.Size, out int cached))
            {
                return cached;
            }

            int width = GetLineWidth(TruncationSymbol, new FontCharHandler(fontType), true);
            TruncationSymbolWidths[fontType.Size] = width;

            return width;
        }
    }
}
