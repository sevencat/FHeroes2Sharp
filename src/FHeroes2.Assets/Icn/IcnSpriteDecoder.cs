using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Icn;

/// <summary>
/// Decoder for the RLE compressed sprite payload of an ICN file, a byte exact port of
/// <c>fheroes2::decodeICNSprite()</c> (<c>src/engine/image_tool.cpp</c>).
/// </summary>
/// <remarks>
/// <para>Control bytes of the regular (color) stream:</para>
/// <list type="table">
///   <item><term>0x00</term><description>End of the current row, the rest of it stays transparent.</description></item>
///   <item><term>0x01..0x7F</term><description>Number of following literal image bytes.</description></item>
///   <item><term>0x80</term><description>End of the image.</description></item>
///   <item><term>0x81..0xBF</term><description>Number of transparent pixels plus 0x80.</description></item>
///   <item><term>0xC0</term><description>Transform layer run, see below.</description></item>
///   <item><term>0xC1</term><description>Next byte is the count of following single color pixels.</description></item>
///   <item><term>0xC2..0xFF</term><description>Count of single color pixels plus 0xC0; the next byte is the color.</description></item>
/// </list>
/// <para>
/// A <c>0xC0</c> run is followed by a descriptor byte. Its two lowest bits hold the pixel count,
/// or zero when the count is stored in the next byte. When bit <c>0x40</c> is set, bits
/// <c>0x3C</c> select a palette transformation (darkening 2..5, lightening 6..9) which is
/// applied to whatever is already on the destination. Bit <c>0x80</c> marks the shining contour
/// used by battlefield units; the reference engine only records it as a TODO, and so do we.
/// </para>
/// <para>
/// The monochromatic variant only writes the transform layer: the image layer stays black.
/// </para>
/// </remarks>
public static class IcnSpriteDecoder
{
    /// <summary>Decodes one sprite from the compressed payload described by <paramref name="header"/>.</summary>
    public static Sprite Decode(ReadOnlySpan<byte> data, in IcnHeader header)
    {
        Sprite sprite = new(header.Width, header.Height, header.OffsetX, header.OffsetY);
        sprite.Reset();

        int width = header.Width;

        // The need for a transform layer is only known once the whole payload has been decoded.
        bool noTransformLayer = true;

        byte[] image = sprite.Image.Pixels;
        byte[] transform = sprite.Transform!;

        if (header.IsMonochromatic)
        {
            DecodeMonochrome(data, transform, width, ref noTransformLayer);
        }
        else
        {
            DecodeColor(data, image, transform, width, ref noTransformLayer);
        }

        return noTransformLayer ? sprite.WithoutTransformLayer() : sprite;
    }

    /// <summary>
    /// Decodes a monochromatic sprite: only the transform layer is encoded, the image layer stays
    /// zeroed. Such sprites are shadows and silhouettes.
    /// </summary>
    private static void DecodeMonochrome(ReadOnlySpan<byte> data, Span<byte> transform, int width, ref bool noTransformLayer)
    {
        int position = 0;
        int rowBase = 0;
        int posX = 0;

        while (position < data.Length)
        {
            byte value = data[position];

            if (value == 0x00)
            {
                noTransformLayer &= posX >= width;

                rowBase += width;
                posX = 0;
                ++position;
            }
            else if (value < 0x80)
            {
                // 0x01-0x7F - number of black pixels, the image layer is already zeroed.
                Fill(transform, rowBase + posX, 0, value);

                ++position;
                posX += value;
            }
            else if (value == 0x80)
            {
                break;
            }
            else
            {
                // 0x81-0xFF - number of transparent pixels plus 0x80.
                noTransformLayer = false;

                posX += value - 0x80;
                ++position;
            }
        }
    }

    /// <summary>Decodes the regular color stream, which may also carry transform layer runs.</summary>
    private static void DecodeColor(ReadOnlySpan<byte> data, Span<byte> image, Span<byte> transform, int width, ref bool noTransformLayer)
    {
        int position = 0;
        int rowBase = 0;
        int posX = 0;

        while (position < data.Length)
        {
            byte value = data[position];

            if (value == 0x00)
            {
                // End of the row, every remaining pixel of it stays transparent.
                noTransformLayer &= posX >= width;

                rowBase += width;
                posX = 0;
                ++position;
            }
            else if (value < 0x80)
            {
                // 0x01-0x7F - number of literal image bytes.
                ++position;
                int pixelCount = value;

                if (position + pixelCount > data.Length)
                {
                    // Corrupted payload: stop exactly where the reference engine stops.
                    break;
                }

                Copy(data.Slice(position, pixelCount), image, rowBase + posX);
                Fill(transform, rowBase + posX, 0, pixelCount);

                position += pixelCount;
                posX += pixelCount;
            }
            else if (value == 0x80)
            {
                // End of the image.
                noTransformLayer &= posX >= width;

                break;
            }
            else if (value < 0xC0)
            {
                // 0x81-0xBF - number of transparent pixels plus 0x80.
                noTransformLayer = false;

                posX += value - 0x80;
                ++position;
            }
            else if (value == 0xC0)
            {
                // 0xC0 - transform layer run.
                noTransformLayer = false;

                ++position;
                if (position >= data.Length)
                {
                    break;
                }

                byte transformValue = data[position];

                int pixelCount = transformValue & 0x03;
                if (pixelCount == 0)
                {
                    ++position;
                    if (position >= data.Length)
                    {
                        break;
                    }

                    pixelCount = data[position];
                }

                if ((transformValue & 0x40) != 0)
                {
                    int transformType = ((transformValue & 0x3C) >> 2) + 2;
                    if (transformType < PaletteTransformTable.TransformCount)
                    {
                        Fill(transform, rowBase + posX, (byte)transformType, pixelCount);
                    }
                }

                posX += pixelCount;
                ++position;
            }
            else
            {
                // 0xC1 - the next byte holds the pixel count, 0xC2-0xFF add 0xC0 to it.
                int pixelCount;
                if (value == 0xC1)
                {
                    ++position;
                    if (position >= data.Length)
                    {
                        break;
                    }

                    pixelCount = data[position];
                }
                else
                {
                    pixelCount = value - 0xC0;
                }

                ++position;
                if (position >= data.Length)
                {
                    break;
                }

                byte color = data[position];

                Fill(image, rowBase + posX, color, pixelCount);
                Fill(transform, rowBase + posX, 0, pixelCount);

                posX += pixelCount;
                ++position;
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="count"/> entries at <paramref name="offset"/>, clamped to the plane.
    /// A run may legitimately run past the end of a row (the reference engine advances a flat
    /// pointer), only a run past the end of the whole plane is truncated.
    /// </summary>
    private static void Fill(Span<byte> plane, int offset, byte value, int count)
    {
        if (count <= 0 || offset < 0 || offset >= plane.Length)
        {
            return;
        }

        plane.Slice(offset, Math.Min(count, plane.Length - offset)).Fill(value);
    }

    /// <summary>Copies literal pixels into the image plane with the same clamping as <see cref="Fill"/>.</summary>
    private static void Copy(ReadOnlySpan<byte> source, Span<byte> plane, int offset)
    {
        if (source.IsEmpty || offset < 0 || offset >= plane.Length)
        {
            return;
        }

        source[..Math.Min(source.Length, plane.Length - offset)].CopyTo(plane[offset..]);
    }
}
