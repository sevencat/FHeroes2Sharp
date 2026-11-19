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
    /// Clears <paramref name="area"/> and turns it into the <paramref name="transformId"/> state, the
    /// equivalent of <c>fheroes2::FillTransform()</c> (<c>engine/image.cpp:2273</c>).
    /// </summary>
    /// <remarks>
    /// 参考的名字只提变换层，其实颜色也一起清成 <c>0</c>：这块区域变成「透明 + 指定变换表」。
    /// 字体修补用它擦掉字形里多余的白线，之后 <see cref="UpdateShadow"/> 再按新轮廓补阴影。
    /// 单层图整段不动（参考也是直接 return）。
    /// </remarks>
    /// <param name="target">Sprite to change.</param>
    /// <param name="area">Area to clear, clipped to the sprite.</param>
    /// <param name="transformId">Transform id the cleared pixels get.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void FillTransform(Sprite target, in Rect area, byte transformId)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Transform is not { } transform)
        {
            return;
        }

        Rect clipped = area.Intersect(target.Image.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        for (int y = 0; y < clipped.Height; ++y)
        {
            target.Image.Row(clipped.Y + y).Slice(clipped.X, clipped.Width).Clear();
            transform.AsSpan(((clipped.Y + y) * target.Image.Stride) + clipped.X, clipped.Width).Fill(transformId);
        }
    }

    /// <summary>
    /// Recolours every pixel of <paramref name="colorId"/>, the equivalent of
    /// <c>fheroes2::ReplaceColorId()</c> (<c>engine/image.cpp:2707</c>).
    /// </summary>
    /// <remarks>
    /// 只看颜色层，不看变换层——批量换色（按下的按钮字面就是这么从绿色变深、从绿变红的），
    /// 半透明像素虽然画不出来，底色也一起换掉，和参考一致。
    /// </remarks>
    /// <param name="target">Sprite to change.</param>
    /// <param name="colorId">Palette index to replace.</param>
    /// <param name="newColorId">Palette index to write instead.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void ReplaceColorId(Sprite target, byte colorId, byte newColorId)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (colorId == newColorId)
        {
            return;
        }

        for (int y = 0; y < target.Height; ++y)
        {
            Span<byte> row = target.Image.Row(y);

            for (int x = 0; x < row.Length; ++x)
            {
                if (row[x] == colorId)
                {
                    row[x] = newColorId;
                }
            }
        }
    }

    /// <summary>
    /// Hides every pixel whose colour is <paramref name="colorId"/> behind
    /// <paramref name="transformId"/>, <c>fheroes2::ReplaceColorIdByTransformId()</c>
    /// (<c>engine/image.cpp:2724</c>).
    /// </summary>
    /// <remarks>
    /// 只改已经可见的像素（transform <c>0</c>），颜色本身留着不管——变换一旦不是 <c>0</c> 就不会以原色画出。
    /// 归档里的 <c>FONT.ICN</c> 有 3 处像素误用了调色板索引 50，参考就是这么把它们修成阴影的。
    /// </remarks>
    /// <param name="target">Sprite to change; a single layer sprite is left alone.</param>
    /// <param name="colorId">Palette index to hide.</param>
    /// <param name="transformId">Table the hidden pixels use; above <c>15</c> the call does nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void ReplaceColorIdByTransformId(Sprite target, byte colorId, byte transformId)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (transformId > 15 || target.Transform is not { } transform)
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

                if (transform[index] == 0 && row[x] == colorId)
                {
                    transform[index] = transformId;
                }
            }
        }
    }

    /// <summary>
    /// Draws <paramref name="source"/> onto a <i>two layer</i> destination, the equivalent of
    /// <c>fheroes2::Blit( const Image &amp; in, Image &amp; out )</c> when <c>out</c> keeps its
    /// transform layer (<c>engine/image.cpp:1328</c>).
    /// </summary>
    /// <remarks>
    /// Same rules as the <see cref="PixelBuffer"/> variant, plus one step: a pixel copied verbatim
    /// copies its transform id too, so transparency travels with it. A transform over a destination
    /// pixel that is already visible (id <c>0</c>) only recolours that destination pixel, which is how
    /// shadows and light effects are stamped onto an assembled sprite.
    /// </remarks>
    /// <param name="destination">Sprite to draw on. Without a transform layer this is the single layer case.</param>
    /// <param name="destinationOffset">Where the source rectangle lands.</param>
    /// <param name="source">Sprite to draw.</param>
    public static void BlitSprite(Sprite destination, Point destinationOffset, Sprite source)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);

        if (destination.Transform is not { } destinationTransform)
        {
            BlitSprite(destination.Image, destinationOffset, source);

            return;
        }

        Rect destinationRect = new(destinationOffset, source.Image.Size);
        Rect clipped = destinationRect.Intersect(destination.Image.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        int skipX = clipped.X - destinationRect.X;
        int skipY = clipped.Y - destinationRect.Y;
        int sourceX = source.Image.Bounds.X + skipX;
        int sourceY = source.Image.Bounds.Y + skipY;

        ReadOnlySpan<byte> image = source.Image.AsSpan();
        byte[]? sourceTransform = source.Transform;
        int destinationStride = destination.Image.Stride;

        for (int y = 0; y < clipped.Height; ++y)
        {
            int sourceRow = ((sourceY + y) * source.Image.Stride) + sourceX;
            Span<byte> destinationRow = destination.Image.Row(clipped.Y + y);
            int transformRow = ((clipped.Y + y) * destinationStride) + clipped.X;

            for (int x = 0; x < clipped.Width; ++x)
            {
                int sourceIndex = sourceRow + x;
                byte transformId = sourceTransform is null ? (byte)0 : sourceTransform[sourceIndex];

                // 1 means "leave the destination alone"; everything else writes.
                if (transformId == 1)
                {
                    continue;
                }

                int destinationIndex = clipped.X + x;

                if (transformId > 0 && transformId < PaletteTransformTable.TransformCount
                    && destinationTransform[transformRow + x] == 0)
                {
                    destinationRow[destinationIndex] =
                        PaletteTransformTable.Apply(transformId, destinationRow[destinationIndex]);
                }
                else
                {
                    destinationRow[destinationIndex] = image[sourceIndex];
                    destinationTransform[transformRow + x] = transformId;
                }
            }
        }
    }

    /// <summary>
    /// Draws <paramref name="source"/> at the top left corner of a two layer
    /// <paramref name="destination"/>, the <c>fheroes2::Blit( in, out )</c> overload that ignores the
    /// sprite anchors.
    /// </summary>
    public static void BlitSprite(Sprite destination, Sprite source) =>
        BlitSprite(destination, Point.Zero, source);

    /// <summary>
    /// The bounding box of the drawn pixels of <paramref name="sprite"/>, the equivalent of
    /// <c>fheroes2::GetActiveROI( const Image &amp;, uint8_t )</c> (<c>engine/image.cpp:2574</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Drawn" is read from the transform layer, not from the image layer: a pixel counts when its
    /// transform value is <c>0</c> (copy) or at least <paramref name="minTransformValue"/>, and the
    /// values in between are the skip / shadow / light operations that leave the destination alone
    /// or merely tint it.
    /// </para>
    /// <para>
    /// A sprite without a transform layer returns <see cref="Rect.Empty"/>, exactly like the reference:
    /// a single layer image has no notion of which of its pixels are transparent, so the caller has to
    /// fall back to the full bounds.
    /// </para>
    /// </remarks>
    /// <param name="sprite">The sprite to measure.</param>
    /// <param name="minTransformValue">
    /// Lowest transform value that still counts as drawn, <c>6</c> by default.
    /// </param>
    /// <returns>The box, or <see cref="Rect.Empty"/> when nothing is drawn.</returns>
    public static Rect GetActiveRoi(Sprite sprite, byte minTransformValue = 6)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        if (sprite.Transform is not { } transform || sprite.Image.IsEmpty)
        {
            return Rect.Empty;
        }

        int width = sprite.Width;
        int height = sprite.Height;

        static bool IsDrawn(byte value, byte minValue) => value == 0 || value >= minValue;

        int top = -1;
        for (int y = 0; y < height && top < 0; ++y)
        {
            int row = y * width;
            for (int x = 0; x < width; ++x)
            {
                if (IsDrawn(transform[row + x], minTransformValue))
                {
                    top = y;
                    break;
                }
            }
        }

        if (top < 0)
        {
            return Rect.Empty;
        }

        int bottom = top;
        for (int y = height - 1; y > top; --y)
        {
            int row = y * width;
            bool drawn = false;
            for (int x = 0; x < width; ++x)
            {
                if (IsDrawn(transform[row + x], minTransformValue))
                {
                    drawn = true;
                    break;
                }
            }

            if (drawn)
            {
                bottom = y;
                break;
            }
        }

        int boxHeight = bottom - top + 1;

        int left = 0;
        bool leftFound = false;
        for (int x = 0; x < width && !leftFound; ++x)
        {
            for (int y = 0; y < boxHeight; ++y)
            {
                if (IsDrawn(transform[((top + y) * width) + x], minTransformValue))
                {
                    left = x;
                    leftFound = true;
                    break;
                }
            }
        }

        int right = left;
        bool rightFound = false;
        for (int x = width - 1; x >= left && !rightFound; --x)
        {
            for (int y = 0; y < boxHeight; ++y)
            {
                if (IsDrawn(transform[((top + y) * width) + x], minTransformValue))
                {
                    right = x;
                    rightFound = true;
                    break;
                }
            }
        }

        return new Rect(left, top, right - left + 1, boxHeight);
    }

    /// <summary>
    /// Builds the one pixel outline around the visible pixels of <paramref name="source"/>, the
    /// equivalent of <c>fheroes2::CreateContour( const Image &amp;, uint8_t )</c>
    /// (<c>engine/image.cpp:1490</c>).
    /// </summary>
    /// <remarks>
    /// A pixel of the contour is placed on every pixel that is not visible (transform <c>1</c>) or that
    /// is a shadow (<c>2</c>..<c>5</c>) and that touches a visible pixel (<c>0</c>) on one of its four
    /// sides. The result is a fresh sprite with everything else transparent, so it is meant to be
    /// drawn <i>under or over</i> the source with <see cref="BlitSprite(Sprite, Sprite)"/> - that is how
    /// the gradient font gets its two colour outline.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> is empty or has no transform layer, which the reference treats as a
    /// programming error (<c>assert( 0 )</c>).
    /// </exception>
    public static Sprite CreateContour(Sprite source, byte value)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Image.IsEmpty || source.Transform is not { } transform)
        {
            throw new ArgumentException(
                "A contour can only be built for a non-empty image with a transform layer.", nameof(source));
        }

        Sprite contour = new(source.Width, source.Height);
        contour.Reset();

        int width = source.Width;
        int height = source.Height;

        if (width < 2 || height < 2)
        {
            return contour;
        }

        byte[] image = contour.Image.Pixels;
        byte[] contourTransform = contour.Transform!;
        int reducedWidth = width - 1;
        int reducedHeight = height - 1;

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int index = (y * width) + x;
                byte transformId = transform[index];

                // Visible pixels are already part of the shape; only the gap and the shadow rings grow.
                if (transformId == 0 || transformId > 5)
                {
                    continue;
                }

                if ((x > 0 && transform[index - 1] == 0)
                    || (x < reducedWidth && transform[index + 1] == 0)
                    || (y > 0 && transform[index - width] == 0)
                    || (y < reducedHeight && transform[index + width] == 0))
                {
                    image[index] = value;
                    contourTransform[index] = 0;
                }
            }
        }

        return contour;
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
