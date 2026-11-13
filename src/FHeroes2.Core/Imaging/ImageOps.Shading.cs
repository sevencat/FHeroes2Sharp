using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Shadows and dithered transitions: the <c>image.cpp</c> helpers the dialogs are built from.
/// </summary>
/// <remarks>
/// <para>
/// The original engine pads every window with a gradient shadow and never draws the border of a
/// dialog as a hard rectangle - the artwork is repeated and the seam between two copies is broken up
/// by a dithered transition, a checkerboard whose period doubles towards the middle of the overlap.
/// Both effects work on palette indices, so <see cref="ApplyTransform"/> converts a destination pixel
/// through one of the <see cref="PaletteTransformTable"/> tables instead of mixing colours.
/// </para>
/// <para>
/// Every method ports the reference verbatim, including the clipping rules of its internal
/// <c>Verify()</c> helpers, the pointer walking of <c>CreateDitheringTransition()</c> and the shadow
/// line of <c>addGradientShadow()</c>. The destination is always a single layer surface
/// (<see cref="PixelBuffer"/>), which is what the reference passes when it renders a dialog onto the
/// display. <c>tools/scripts/CompareEffectsWithReference.ps1</c> compares this file with the C++
/// implementation byte for byte.
/// </para>
/// </remarks>
public static partial class ImageOps
{
    /// <summary>
    /// Applies the palette conversion table <paramref name="transformId"/> to
    /// <paramref name="area"/> of <paramref name="target"/>, <c>fheroes2::ApplyTransform()</c>.
    /// </summary>
    /// <param name="target">Indexed surface to convert in place.</param>
    /// <param name="area">Area to convert; the part outside <paramref name="target"/> is ignored.</param>
    /// <param name="transformId">Index of the table inside <see cref="PaletteTransformTable"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="transformId"/> is not a table index.
    /// </exception>
    public static void ApplyTransform(PixelBuffer target, in Rect area, int transformId) =>
        ApplyTransform(target, area.X, area.Y, area.Width, area.Height, transformId);

    /// <summary>
    /// Applies the palette conversion table <paramref name="transformId"/> to a rectangle of
    /// <paramref name="target"/>, <c>fheroes2::ApplyTransform()</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="transformId"/> is not a table index.
    /// </exception>
    public static void ApplyTransform(PixelBuffer target, int x, int y, int width, int height, int transformId)
    {
        ArgumentNullException.ThrowIfNull(target);

        ReadOnlySpan<byte> table = PaletteTransformTable.Get(transformId);

        if (!TryClip(target, ref x, ref y, ref width, ref height))
        {
            return;
        }

        for (int row = y; row < y + height; ++row)
        {
            Span<byte> pixels = target.Row(row).Slice(x, width);

            for (int column = 0; column < pixels.Length; ++column)
            {
                pixels[column] = table[pixels[column]];
            }
        }
    }

    /// <summary>
    /// Draws the gradient shadow of <paramref name="source"/> into <paramref name="target"/>,
    /// <c>fheroes2::addGradientShadow()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shadow is not a blurred copy of the silhouette: every transparent pixel next to the
    /// artwork is darkened by one of the four shadow tables (2 is the strongest, 5 the weakest) and
    /// the strength grows as the pixel gets closer to the source, which is how the result gets a soft
    /// edge without any alpha channel.
    /// </para>
    /// <para>
    /// A source without a transform layer counts as fully opaque and therefore casts no shadow - the
    /// reference behaves the same way because its <c>isTransparent()</c> lambda answers
    /// <see langword="false"/> for single layer images - so the method returns early in that case.
    /// </para>
    /// </remarks>
    /// <param name="source">Sprite whose silhouette casts the shadow.</param>
    /// <param name="target">Indexed surface the shadow is drawn onto.</param>
    /// <param name="targetPosition">Top left corner the shadow is placed at.</param>
    /// <param name="shadowOffset">Direction and length of the shadow, e.g. <c>(-5, 5)</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public static void AddGradientShadow(Sprite source, PixelBuffer target, Point targetPosition, Point shadowOffset)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (source.Image.IsEmpty || target.IsEmpty || (shadowOffset.X == 0 && shadowOffset.Y == 0) ||
            targetPosition.X < 0 || targetPosition.Y < 0 || source.Transform is null)
        {
            return;
        }

        int targetWidth = target.Width;
        int sourceWidth = source.Width;
        if (targetWidth < sourceWidth + targetPosition.X + Math.Max(shadowOffset.X, 0))
        {
            sourceWidth = targetWidth - (targetPosition.X + Math.Max(shadowOffset.X, 0));
        }

        int sourceHeight = source.Height;
        if (target.Height < sourceHeight + targetPosition.Y + Math.Max(shadowOffset.Y, 0))
        {
            sourceHeight = target.Height - (targetPosition.Y + Math.Max(shadowOffset.Y, 0));
        }

        if (sourceWidth <= 1 || sourceHeight <= 1)
        {
            return;
        }

        int shadowOffsetX = Math.Min(shadowOffset.X, 0);
        int shadowOffsetY = Math.Min(shadowOffset.Y, 0);
        int targetStart = targetPosition.X + shadowOffsetX + source.OffsetX +
            ((targetPosition.Y + shadowOffsetY + source.OffsetY) * targetWidth);

        if (targetStart < 0)
        {
            // The shadow would start outside of the target; the reference asserts instead.
            return;
        }

        int absOffsetX = Math.Abs(shadowOffset.X);
        int absOffsetY = Math.Abs(shadowOffset.Y);

        List<Point> shadowLine = BuildShadowLine(shadowOffset, absOffsetX, absOffsetY);

        int maxX = sourceWidth + absOffsetX;
        int maxY = sourceHeight + absOffsetY;

        byte[] sourcePixels = source.Image.Pixels;
        byte[] sourceTransform = source.Transform;
        byte[] targetPixels = target.Pixels;

        bool IsTransparent(int offsetX, int offsetY)
        {
            if (offsetX < 0 || offsetY < 0 || offsetX >= sourceWidth || offsetY >= sourceHeight)
            {
                // The space outside of the source is transparent.
                return true;
            }

            return sourceTransform[(offsetY * sourceWidth) + offsetX] == 1;
        }

        for (int y = 0; y < maxY; ++y)
        {
            int offsetY = y + shadowOffsetY;

            for (int x = 0; x < maxX; ++x)
            {
                int offsetX = x + shadowOffsetX;

                if (!IsTransparent(offsetX, offsetY))
                {
                    // Only pixels that are not part of the artwork can receive a shadow.
                    continue;
                }

                // Four tables are used: 2, 3, 4 and 5. The strongest shadow is table 2.
                int transformTableId = 6;

                foreach (Point line in shadowLine)
                {
                    if (IsTransparent(offsetX - line.X, offsetY - line.Y))
                    {
                        continue;
                    }

                    // The artwork is closer along the shadow direction, so the shadow gets stronger.
                    --transformTableId;

                    if (transformTableId == 2)
                    {
                        break;
                    }
                }

                if (transformTableId == 6)
                {
                    continue;
                }

                int targetIndex = targetStart + x + (y * targetWidth);
                targetPixels[targetIndex] = PaletteTransformTable.Apply(transformTableId, targetPixels[targetIndex]);
            }
        }
    }

    /// <summary>Builds the line of offsets <c>addGradientShadow()</c> projects the silhouette along.</summary>
    private static List<Point> BuildShadowLine(Point shadowOffset, int absOffsetX, int absOffsetY)
    {
        List<Point> shadowLine = new(Math.Max(absOffsetX, absOffsetY) + 1);

        int shadowOffsetX = Math.Min(shadowOffset.X, 0);
        int shadowOffsetY = Math.Min(shadowOffset.Y, 0);

        if (shadowOffset.X == 0)
        {
            int maxY = absOffsetY + shadowOffsetY;
            for (int y = shadowOffsetY; y <= maxY; ++y)
            {
                shadowLine.Add(new Point(0, y));
            }

            return shadowLine;
        }

        double slopeFactor = (double)shadowOffset.Y / shadowOffset.X;

        if (absOffsetX >= absOffsetY)
        {
            int maxX = absOffsetX + shadowOffsetX;
            for (int x = shadowOffsetX; x <= maxX; ++x)
            {
                shadowLine.Add(new Point(x, RoundLikeReference(x * slopeFactor)));
            }
        }
        else
        {
            int maxY = absOffsetY + shadowOffsetY;
            for (int y = shadowOffsetY; y <= maxY; ++y)
            {
                shadowLine.Add(new Point(RoundLikeReference(y / slopeFactor), y));
            }
        }

        return shadowLine;
    }

    /// <summary>Rounds like <c>std::round()</c>: a half goes away from zero, not to the even neighbour.</summary>
    private static int RoundLikeReference(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Draws the gradient shadow of a rectangle, <c>fheroes2::addGradientShadowForArea()</c>.
    /// </summary>
    /// <remarks>
    /// A window is not a sprite, so its shadow comes from its size: nested lines on the left and on
    /// the bottom edge are converted with the tables 5, 4, 3 and 2. Together they form the 16 pixel
    /// wide ramp every original dialog is padded with.
    /// </remarks>
    /// <param name="target">Indexed surface the shadow is drawn onto.</param>
    /// <param name="position">Top left corner of the area that casts the shadow.</param>
    /// <param name="width">Width of the area.</param>
    /// <param name="height">Height of the area.</param>
    /// <param name="shadowOffset">Width of the shadow ramp, 1 or more.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void AddGradientShadowForArea(PixelBuffer target, Point position, int width, int height, int shadowOffset)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsEmpty || position.X < 0 || position.Y < 0 || shadowOffset < 1)
        {
            return;
        }

        // Shadow on the left side of the area.
        int offsetY = position.Y + shadowOffset;
        ApplyTransform(target, position.X - shadowOffset, offsetY, shadowOffset, 1, 5);
        ++offsetY;
        ApplyTransform(target, position.X - shadowOffset, offsetY, 1, height, 5);
        ApplyTransform(target, position.X - shadowOffset + 1, offsetY, shadowOffset - 1, 1, 4);
        ++offsetY;
        ApplyTransform(target, position.X - shadowOffset + 1, offsetY, 1, height - 4, 4);
        ApplyTransform(target, position.X - shadowOffset + 2, offsetY, shadowOffset - 2, 1, 3);
        ++offsetY;
        ApplyTransform(target, position.X - shadowOffset + 2, offsetY, 1, height - 6, 3);
        ApplyTransform(target, position.X - shadowOffset + 3, offsetY, shadowOffset - 3, height - shadowOffset - 3, 2);

        // Shadow on the bottom side of the area.
        offsetY = position.Y + height;
        int shadowBottomEdge = position.Y + height + shadowOffset;
        ApplyTransform(target, position.X - shadowOffset + 3, offsetY, width - 6, shadowOffset - 3, 2);
        ApplyTransform(target, position.X - shadowOffset + 2, shadowBottomEdge - 3, width - 4, 1, 3);
        ApplyTransform(target, position.X - shadowOffset + width - 3, offsetY, 1, shadowOffset - 3, 3);
        ApplyTransform(target, position.X - shadowOffset + 1, shadowBottomEdge - 2, width - 2, 1, 4);
        ApplyTransform(target, position.X - shadowOffset + width - 2, offsetY, 1, shadowOffset - 2, 4);
        ApplyTransform(target, position.X - shadowOffset, shadowBottomEdge - 1, width, 1, 5);
        ApplyTransform(target, position.X - shadowOffset + width - 1, offsetY, 1, shadowOffset, 5);
    }

    /// <summary>
    /// Blends the edge of <paramref name="source"/> into <paramref name="target"/> with a dithered
    /// pattern, <c>fheroes2::CreateDitheringTransition()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pattern is a symmetric checkerboard: the outermost column is copied on one of two rows,
    /// the next pair on one of four rows and so on, so the period doubles towards the middle of the
    /// blended band. That is how the original hides the seam between two copies of the same border
    /// piece without a single blended pixel.
    /// </para>
    /// <para>
    /// <paramref name="vertical"/> selects the axis of the pattern: vertical blends the left and the
    /// right half of the band, horizontal the top and the bottom one. An odd size shifts one half by
    /// a pixel, which is the asymmetry of the reference and is pinned by the tests.
    /// </para>
    /// </remarks>
    /// <param name="source">Sprite the pixels are copied from.</param>
    /// <param name="sourcePosition">Top left corner of the band inside <paramref name="source"/>.</param>
    /// <param name="target">Indexed surface the band is blended into.</param>
    /// <param name="targetPosition">Top left corner of the band inside <paramref name="target"/>.</param>
    /// <param name="size">Width and height of the band.</param>
    /// <param name="vertical">Blend across the width of the band instead of across its height.</param>
    /// <param name="reverse">Swap the two halves of the pattern.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public static void CreateDitheringTransition(Sprite source, Point sourcePosition, PixelBuffer target, Point targetPosition,
        Size size, bool vertical, bool reverse)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        int sourceX = sourcePosition.X;
        int sourceY = sourcePosition.Y;
        int targetX = targetPosition.X;
        int targetY = targetPosition.Y;
        int width = size.Width;
        int height = size.Height;

        if (!TryClip(source.Image, ref sourceX, ref sourceY, target, ref targetX, ref targetY, ref width, ref height))
        {
            return;
        }

        int sourceWidth = source.Image.Width;
        int sourceStart = (sourceY * sourceWidth) + sourceX;
        byte[] sourcePixels = source.Image.Pixels;
        byte[]? sourceTransform = source.Transform;

        int targetWidth = target.Width;
        int targetStart = (targetY * targetWidth) + targetX;
        byte[] targetPixels = target.Pixels;

        void Copy(int sourceIndex, int targetIndex)
        {
            if (sourceTransform is not null && sourceTransform[sourceIndex] == 1)
            {
                // A transparent pixel of the source does not cover the destination.
                return;
            }

            targetPixels[targetIndex] = sourcePixels[sourceIndex];
        }

        if (vertical)
        {
            int halfWidth = width / 2;

            // The band is mirrored: 'left' walks to the right while 'right' walks to the left.
            int sourceLeft = sourceStart;
            int sourceRight = sourceStart + width - 1;
            int targetLeft = targetStart;
            int targetRight = targetStart + width - 1;

            if ((width % 2) == 1)
            {
                // An odd width shifts the half that keeps the center column, like the reference.
                if (reverse)
                {
                    --sourceRight;
                    --targetRight;
                }
                else
                {
                    ++sourceLeft;
                    ++targetLeft;
                }
            }

            for (int x = 0; x < halfWidth; ++x)
            {
                // The period doubles every second column and is 1 in the center.
                int stepPower = Math.Min(30, ((halfWidth - x) / 2) + 1);
                int stepY = 1 << stepPower;
                int patternPoint = (stepY / 2) * ((x + halfWidth) % 2);

                for (int y = 0; y < height; ++y)
                {
                    int sourceRow = y * sourceWidth;
                    int targetRow = y * targetWidth;

                    if (reverse == (patternPoint != (y % stepY)))
                    {
                        Copy(sourceLeft + sourceRow, targetLeft + targetRow);
                    }
                    else
                    {
                        Copy(sourceRight + sourceRow, targetRight + targetRow);
                    }
                }

                ++targetLeft;
                --targetRight;
                ++sourceLeft;
                --sourceRight;
            }

            return;
        }

        int halfHeight = height / 2;

        int sourceTop = sourceStart;
        int sourceBottom = sourceStart + ((height - 1) * sourceWidth);
        int targetTop = targetStart;
        int targetBottom = targetStart + ((height - 1) * targetWidth);

        if ((height % 2) == 1)
        {
            if (reverse)
            {
                sourceBottom -= sourceWidth;
                targetBottom -= targetWidth;
            }
            else
            {
                sourceTop += sourceWidth;
                targetTop += targetWidth;
            }
        }

        for (int y = 0; y < halfHeight; ++y)
        {
            // The period doubles every second row and is 1 in the center.
            int stepPower = Math.Min(30, ((halfHeight - y) / 2) + 1);
            int stepX = 1 << stepPower;
            int patternPoint = (stepX / 2) * ((y + halfHeight) % 2);

            for (int x = 0; x < width; ++x)
            {
                if (reverse == (patternPoint != (x % stepX)))
                {
                    Copy(sourceTop + x, targetTop + x);
                }
                else
                {
                    Copy(sourceBottom + x, targetBottom + x);
                }
            }

            targetTop += targetWidth;
            targetBottom -= targetWidth;
            sourceTop += sourceWidth;
            sourceBottom -= sourceWidth;
        }
    }

    /// <summary>
    /// Clips a rectangle to <paramref name="image"/>, the <c>Verify( Image &amp;, ... )</c> helper of
    /// the reference. Returns <see langword="false"/> when there is nothing left to draw.
    /// </summary>
    private static bool TryClip(PixelBuffer image, ref int x, ref int y, ref int width, ref int height)
    {
        if (image.IsEmpty || width <= 0 || height <= 0)
        {
            return false;
        }

        int imageWidth = image.Width;
        int imageHeight = image.Height;

        if (x < 0)
        {
            int offsetX = -x;
            if (offsetX >= width)
            {
                return false;
            }

            x = 0;
            width -= offsetX;
        }

        if (y < 0)
        {
            int offsetY = -y;
            if (offsetY >= height)
            {
                return false;
            }

            y = 0;
            height -= offsetY;
        }

        if (x > imageWidth || y > imageHeight)
        {
            return false;
        }

        if (x + width > imageWidth)
        {
            int offsetX = x + width - imageWidth;
            if (offsetX >= width)
            {
                return false;
            }

            width -= offsetX;
        }

        if (y + height > imageHeight)
        {
            int offsetY = y + height - imageHeight;
            if (offsetY >= height)
            {
                return false;
            }

            height -= offsetY;
        }

        return true;
    }

    /// <summary>
    /// Clips a band of <paramref name="source"/> and its destination, the
    /// <c>Verify( inX, inY, outX, outY, ... )</c> helper of the reference. Both rectangles shrink by
    /// the same amount so the band stays aligned, and the source position follows the destination
    /// when it is cut off.
    /// </summary>
    private static bool TryClip(PixelBuffer source, ref int sourceX, ref int sourceY, PixelBuffer target,
        ref int targetX, ref int targetY, ref int width, ref int height)
    {
        if (source.IsEmpty || target.IsEmpty || width <= 0 || height <= 0)
        {
            return false;
        }

        int sourceWidth = source.Width;
        int sourceHeight = source.Height;
        int targetWidth = target.Width;
        int targetHeight = target.Height;

        if (sourceX < 0 || sourceY < 0 || sourceX > sourceWidth || sourceY > sourceHeight)
        {
            return false;
        }

        if (targetX < 0)
        {
            int offsetX = -targetX;
            if (offsetX >= width)
            {
                return false;
            }

            sourceX += offsetX;
            targetX = 0;
            width -= offsetX;
        }

        if (targetY < 0)
        {
            int offsetY = -targetY;
            if (offsetY >= height)
            {
                return false;
            }

            sourceY += offsetY;
            targetY = 0;
            height -= offsetY;
        }

        if (targetX > targetWidth || targetY > targetHeight)
        {
            return false;
        }

        if (sourceX + width > sourceWidth)
        {
            int offsetX = sourceX + width - sourceWidth;
            if (offsetX >= width)
            {
                return false;
            }

            width -= offsetX;
        }

        if (sourceY + height > sourceHeight)
        {
            int offsetY = sourceY + height - sourceHeight;
            if (offsetY >= height)
            {
                return false;
            }

            height -= offsetY;
        }

        if (targetX + width > targetWidth)
        {
            int offsetX = targetX + width - targetWidth;
            if (offsetX >= width)
            {
                return false;
            }

            width -= offsetX;
        }

        if (targetY + height > targetHeight)
        {
            int offsetY = targetY + height - targetHeight;
            if (offsetY >= height)
            {
                return false;
            }

            height -= offsetY;
        }

        return true;
    }
}
