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
    /// here to clear; the <see cref="Sprite"/> overload does that part.
    /// </para>
    /// </remarks>
    /// <param name="image">The image to frame in place.</param>
    /// <param name="value">The palette index of the frame.</param>
    /// <param name="skipFactor">How many pixels to step between two painted ones; solid below two.</param>
    public static void DrawBorder(PixelBuffer image, byte value, uint skipFactor = 0)
    {
        ArgumentNullException.ThrowIfNull(image);

        byte[] pixels = image.Pixels;

        WalkBorder(image.Width, image.Height, skipFactor, index => pixels[index] = value);
    }

    /// <summary>
    /// Draws a frame one pixel wide around a sprite and clears the transform layer along it, <c>fheroes2::DrawBorder()</c>
    /// of a multi layer image.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>DrawBorder()</c> 画颜色的同时把沿途像素的 transform 清成 <c>0</c>（照抄源图），于是「先
    /// <see cref="Sprite.Reset"/>（整张透明）再画框」才得到只有框可见的精灵。少了这一步：框的像素停在 transform
    /// <c>1</c> 上看不见；没先 <see cref="Sprite.Reset"/> 的精灵则整张不透明，框盖在肖像上就是一整块黑。
    /// </remarks>
    /// <param name="image">The sprite to frame in place.</param>
    /// <param name="value">The palette index of the frame.</param>
    /// <param name="skipFactor">How many pixels to step between two painted ones; solid below two.</param>
    public static void DrawBorder(Sprite image, byte value, uint skipFactor = 0)
    {
        ArgumentNullException.ThrowIfNull(image);

        PixelBuffer pixels = image.Image;
        byte[]? transform = image.Transform;

        WalkBorder(pixels.Width, pixels.Height, skipFactor, index =>
        {
            pixels.Pixels[index] = value;

            if (transform is not null)
            {
                transform[index] = 0;
            }
        });
    }

    /// <summary>Walks the four sides of a <paramref name="width"/> by <paramref name="height"/> frame.</summary>
    /// <param name="width">Width of the image.</param>
    /// <param name="height">Height of the image.</param>
    /// <param name="skipFactor">How many pixels to step between two painted ones; solid below two.</param>
    /// <param name="paint">Paints the pixel with the given flat index.</param>
    private static void WalkBorder(int width, int height, uint skipFactor, Action<int> paint)
    {
        // A frame needs a middle: on a one pixel image the four sides would paint the same pixel four times and
        // the reference refuses it outright.
        if (width < 2 || height < 2)
        {
            return;
        }

        if (skipFactor < 2)
        {
            for (int x = 0; x < width; ++x)
            {
                paint(x);
                paint(((height - 1) * width) + x);
            }

            for (int y = 1; y < height - 1; ++y)
            {
                paint(y * width);
                paint((y * width) + width - 1);
            }

            return;
        }

        // One counter for all four sides: the top edge starts at one, so the very first pixel is always painted.
        uint counter = 1;

        for (int x = 0; x < width; ++x, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                paint(x);
            }
        }

        for (int y = 1; y < height - 1; ++y, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                paint((y * width) + width - 1);
            }
        }

        // The bottom edge walks right to left and stops before the left corner; the left edge then walks up and
        // stops before the top corner, which keeps every pixel counted exactly once.
        for (int x = width - 1; x > 0; --x, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                paint(((height - 1) * width) + x);
            }
        }

        for (int y = height - 2; y > 0; --y, ++counter)
        {
            if (counter % skipFactor != 0)
            {
                paint(y * width);
            }
        }
    }
}
