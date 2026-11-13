using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The <c>fheroes2::Image</c> helpers of <c>src/engine/image.cpp</c> that write <i>both</i> layers of
/// an image, which is why they take a <see cref="Sprite"/> and not a bare <see cref="PixelBuffer"/>.
/// </summary>
/// <remarks>
/// The split exists for a reason: <see cref="ImageOps.Blit"/> and its siblings only ever touch the
/// colour layer, so they are what a decoded ICN is drawn with, while <see cref="Copy"/>,
/// <see cref="FillSprite"/>, <see cref="SetPixel"/> and <see cref="DrawLine"/> are used to
/// <i>compose</i> sprites at run time. When such a helper writes a colour it also clears the
/// transform byte to <c>0</c> (copy verbatim), which is what keeps an assembled sprite drawable.
/// </remarks>
public static partial class ImageOps
{
    /// <summary>
    /// Copies <paramref name="sourceArea"/> of <paramref name="source"/> into
    /// <paramref name="destination"/>, the equivalent of
    /// <c>fheroes2::Copy( in, inX, inY, out, outX, outY, width, height )</c>.
    /// </summary>
    /// <remarks>
    /// The transform layer follows the reference rules: a single layer destination receives the
    /// colour only, a multi layer destination receives <c>0</c> where the source has no transform
    /// layer, and two multi layer images copy both layers verbatim.
    /// </remarks>
    public static void Copy(Sprite source, in Rect sourceArea, Sprite destination, Point destinationOffset)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        Rect sourceRect = sourceArea.Intersect(source.Image.Bounds);
        if (sourceRect.IsEmpty)
        {
            return;
        }

        // Keep the clipped part of the source at the position it would have had in the destination.
        Point destinationOrigin = new(
            destinationOffset.X + (sourceRect.X - sourceArea.X),
            destinationOffset.Y + (sourceRect.Y - sourceArea.Y));

        Rect destinationRect = new(destinationOrigin, sourceRect.Size);
        Rect clipped = destinationRect.Intersect(destination.Image.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        int skipX = clipped.X - destinationRect.X;
        int skipY = clipped.Y - destinationRect.Y;
        int sourceX = sourceRect.X + skipX;
        int sourceY = sourceRect.Y + skipY;

        byte[]? sourceTransform = source.Transform;
        byte[]? destinationTransform = destination.Transform;
        int destinationStride = destination.Image.Stride;

        for (int y = 0; y < clipped.Height; ++y)
        {
            source.Image.Row(sourceY + y).Slice(sourceX, clipped.Width).CopyTo(
                destination.Image.Row(clipped.Y + y).Slice(clipped.X, clipped.Width));

            if (destinationTransform is null)
            {
                continue;
            }

            int destinationIndex = ((clipped.Y + y) * destinationStride) + clipped.X;
            Span<byte> transformRow = destinationTransform.AsSpan(destinationIndex, clipped.Width);

            if (sourceTransform is null)
            {
                transformRow.Fill(0);
            }
            else
            {
                int sourceIndex = ((sourceY + y) * source.Image.Stride) + sourceX;
                sourceTransform.AsSpan(sourceIndex, clipped.Width).CopyTo(transformRow);
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="area"/> with <paramref name="color"/>, the equivalent of
    /// <c>fheroes2::Fill( Image &amp;, x, y, width, height, color )</c>: the transform bytes of the
    /// filled pixels become <c>0</c>.
    /// </summary>
    public static void FillSprite(Sprite target, in Rect area, byte color)
    {
        ArgumentNullException.ThrowIfNull(target);

        Rect clipped = area.Intersect(target.Image.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        for (int y = 0; y < clipped.Height; ++y)
        {
            target.Image.Row(clipped.Y + y).Slice(clipped.X, clipped.Width).Fill(color);

            if (target.Transform is { } transform)
            {
                int index = ((clipped.Y + y) * target.Image.Stride) + clipped.X;
                transform.AsSpan(index, clipped.Width).Fill(0);
            }
        }
    }

    /// <summary>
    /// Writes one pixel, the equivalent of <c>fheroes2::SetPixel</c>. Coordinates outside the
    /// sprite are ignored, and a written pixel always becomes visible (transform <c>0</c>).
    /// </summary>
    public static void SetPixel(Sprite target, int x, int y, byte color)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (x < 0 || y < 0 || x >= target.Width || y >= target.Height)
        {
            return;
        }

        target.Image[x, y] = color;

        if (target.Transform is { } transform)
        {
            transform[(y * target.Image.Stride) + x] = 0;
        }
    }

    /// <summary>
    /// Draws a line from <paramref name="from"/> to <paramref name="to"/>, both ends included, the
    /// equivalent of <c>fheroes2::DrawLine</c>: the same integer Bresenham, so the shine
    /// decorations of a button come out pixel for pixel like the reference.
    /// </summary>
    public static void DrawLine(Sprite target, Point from, Point to, byte color)
    {
        ArgumentNullException.ThrowIfNull(target);

        int x = from.X;
        int y = from.Y;
        int deltaX = Math.Abs(to.X - x);
        int deltaY = Math.Abs(to.Y - y);

        if (deltaX >= deltaY)
        {
            int error = deltaX / 2;

            for (int i = 0; i <= deltaX; ++i)
            {
                SetPixel(target, x, y, color);
                x += x < to.X ? 1 : -1;

                error -= deltaY;
                if (error < 0)
                {
                    y += y < to.Y ? 1 : -1;
                    error += deltaX;
                }
            }

            return;
        }

        int minorError = deltaY / 2;

        for (int i = 0; i <= deltaY; ++i)
        {
            SetPixel(target, x, y, color);
            y += y < to.Y ? 1 : -1;

            minorError -= deltaX;
            if (minorError < 0)
            {
                x += x < to.X ? 1 : -1;
                minorError += deltaY;
            }
        }
    }
}
