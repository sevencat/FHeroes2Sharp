namespace FHeroes2.Core.Imaging;

/// <summary>
/// The frame primitives of the reference's <c>image.cpp</c>.
/// </summary>
public static partial class ImageOps
{
    /// <summary>
    /// Draws a frame one pixel wide around an image, <c>fheroes2::DrawBorder()</c>
    /// (<c>image.cpp:1831-2001</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The skip factor is the dotted variant: <c>0</c> and <c>1</c> draw a solid frame, anything larger draws
    /// every <paramref name="skipFactor"/>-th pixel. The counter runs across the four sides without ever being
    /// reset, and the sides are walked clockwise from the top left — so a dotted frame's gaps continue around the
    /// corners instead of restarting at each one, which is what the reference's single <c>counter</c> does.
    /// </para>
    /// <para>
    /// The reference also clears the transform layer of the pixels it paints. A <see cref="PixelBuffer"/> is a
    /// single layer image — the port stores transparency in <see cref="Sprite.Transform"/> — so there is nothing
    /// here to clear.
    /// </para>
    /// </remarks>
    /// <param name="image">The image to frame in place.</param>
    /// <param name="value">The palette index of the frame.</param>
    /// <param name="skipFactor">How many pixels to step between two painted ones; solid below two.</param>
    public static void DrawBorder(PixelBuffer image, byte value, uint skipFactor = 0)
    {
        ArgumentNullException.ThrowIfNull(image);

        // A frame needs a middle: on a one pixel image the four sides would paint the same pixel four times and
        // the reference refuses it outright.
        if (image.IsEmpty || image.Width < 2 || image.Height < 2)
        {
            return;
        }

        int width = image.Width;
        int height = image.Height;
        byte[] pixels = image.Pixels;

        if (skipFactor < 2)
        {
            image.Row(0).Fill(value);
            image.Row(height - 1).Fill(value);

            for (int y = 1; y < height - 1; ++y)
            {
                pixels[y * width] = value;
                pixels[(y * width) + width - 1] = value;
            }

            return;
        }

        // One counter for all four sides: the top edge starts at one, so the very first pixel is always painted.
        uint counter = 1;

        for (int x = 0; x < width; ++x, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                pixels[x] = value;
            }
        }

        for (int y = 1; y < height - 1; ++y, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                pixels[(y * width) + width - 1] = value;
            }
        }

        // The bottom edge walks right to left and stops before the left corner; the left edge then walks up and
        // stops before the top corner, which keeps every pixel counted exactly once.
        for (int x = width - 1; x > 0; --x, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                pixels[((height - 1) * width) + x] = value;
            }
        }

        for (int y = height - 2; y > 0; --y, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                pixels[y * width] = value;
            }
        }
    }
}
