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
    /// Turns every shadow of <paramref name="target"/> into a skip marker, the
    /// <c>setTransformLayerTransparent()</c> of <c>game_assets.cpp</c>: the tables of the transform
    /// layer are dropped and the copy / skip markers are kept.
    /// </summary>
    /// <remarks>
    /// The editor buttons are assembled from parts of other sprites, and the shadows those parts
    /// carry belong to their own background, not to the button they end up on.
    /// </remarks>
    /// <param name="target">Sprite to change; a single layer sprite is left alone.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void SetTransformLayerTransparent(Sprite target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Transform is not { } transform)
        {
            return;
        }

        int stride = target.Image.Stride;

        for (int y = 0; y < target.Height; ++y)
        {
            for (int x = 0; x < target.Width; ++x)
            {
                int index = (y * stride) + x;
                if (transform[index] > 1)
                {
                    transform[index] = 1;
                }
            }
        }
    }

    /// <summary>
    /// Replaces every pixel that uses <paramref name="transformId"/> by an opaque
    /// <paramref name="colorId"/>, <c>fheroes2::ReplaceTransformIdByColorId()</c>.
    /// </summary>
    /// <param name="target">Sprite to change; a single layer sprite is left alone.</param>
    /// <param name="transformId">Table to replace.</param>
    /// <param name="colorId">Palette index the pixels become.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void ReplaceTransformIdByColorId(Sprite target, byte transformId, byte colorId)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Transform is not { } transform)
        {
            return;
        }

        int stride = target.Image.Stride;

        for (int y = 0; y < target.Height; ++y)
        {
            Span<byte> row = target.Image.Row(y);

            for (int x = 0; x < target.Width; ++x)
            {
                int index = (y * stride) + x;
                if (transform[index] == transformId)
                {
                    transform[index] = 0;
                    row[x] = colorId;
                }
            }
        }
    }

    /// <summary>
    /// Adds a shadow of <paramref name="shadowOffset"/> to the transform layer of
    /// <paramref name="target"/>, the <c>fheroes2::updateShadow()</c> of the engine.
    /// </summary>
    /// <remarks>
    /// Every pixel of the transform layer that still skips and that sits
    /// <paramref name="shadowOffset"/> away from an opaque pixel receives
    /// <paramref name="transformId"/>. With <paramref name="connectCorners"/> a pixel whose two
    /// neighbours along the axes are opaque counts as well, which keeps the diagonals of a one pixel
    /// shadow free of holes. This is what lights the top edge of the editor buttons and darkens the
    /// bottom edge.
    /// </remarks>
    /// <param name="target">Sprite to add the shadow to; a single layer sprite is left alone.</param>
    /// <param name="shadowOffset">Offset of the shadow, in pixels; either component may be negative.</param>
    /// <param name="transformId">Table the shadow pixels use.</param>
    /// <param name="connectCorners">Whether diagonal corners are filled too.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void UpdateShadow(Sprite target, Point shadowOffset, byte transformId, bool connectCorners)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Transform is not { } transform)
        {
            return;
        }

        int stride = target.Image.Stride;
        int width = target.Width;
        int height = target.Height;

        if (Math.Abs(shadowOffset.X) >= width ||
            Math.Abs(shadowOffset.Y) >= height ||
            (shadowOffset.X == 0 && shadowOffset.Y == 0))
        {
            return;
        }

        int span = width - Math.Abs(shadowOffset.X);
        int rows = height - Math.Abs(shadowOffset.Y);

        int sourceStart = Math.Max(-shadowOffset.X, 0) + (stride * Math.Max(-shadowOffset.Y, 0));
        int targetStart = Math.Max(shadowOffset.X, 0) + (stride * Math.Max(shadowOffset.Y, 0));

        int cornerOffsetX = shadowOffset.X > 0 ? 1 : -1;
        int cornerOffsetY = shadowOffset.Y > 0 ? stride : -stride;

        for (int row = 0; row < rows; ++row)
        {
            int sourceRow = sourceStart + (row * stride);
            int targetRow = targetStart + (row * stride);

            for (int column = 0; column < span; ++column)
            {
                int destination = targetRow + column;
                if (transform[destination] != 1)
                {
                    continue;
                }

                int source = sourceRow + column;
                bool isArtwork = transform[source] == 0;

                if (!isArtwork && connectCorners)
                {
                    int alongX = source + cornerOffsetX;
                    int alongY = source + cornerOffsetY;

                    isArtwork =
                        alongX >= 0 && alongX < transform.Length && transform[alongX] == 0 &&
                        alongY >= 0 && alongY < transform.Length && transform[alongY] == 0;
                }

                if (isArtwork)
                {
                    transform[destination] = transformId;
                }
            }
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
