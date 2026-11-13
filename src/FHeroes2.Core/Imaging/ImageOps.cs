using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Clipping aware primitives on <see cref="PixelBuffer"/>. Everything here works on the
/// indexed 8 bit surface; palette lookup happens only when the frame is presented.
/// </summary>
public static class ImageOps
{
    public static void FillRect(PixelBuffer target, in Rect area, byte color)
    {
        ArgumentNullException.ThrowIfNull(target);

        Rect clipped = area.Intersect(target.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        for (int y = clipped.Y; y < clipped.Bottom; ++y)
        {
            target.Row(y)[clipped.X..clipped.Right].Fill(color);
        }
    }

    public static void Clear(PixelBuffer target, byte color) => FillRect(target, target.Bounds, color);

    public static void HLine(PixelBuffer target, int x, int y, int length, byte color)
    {
        if (length <= 0)
        {
            return;
        }

        FillRect(target, new Rect(x, y, length, 1), color);
    }

    public static void VLine(PixelBuffer target, int x, int y, int length, byte color)
    {
        if (length <= 0)
        {
            return;
        }

        FillRect(target, new Rect(x, y, 1, length), color);
    }

    /// <summary>Draws a one pixel thick rectangle outline inside <paramref name="area"/>.</summary>
    public static void DrawRect(PixelBuffer target, in Rect area, byte color)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        HLine(target, area.X, area.Y, area.Width, color);
        HLine(target, area.X, area.Bottom - 1, area.Width, color);
        VLine(target, area.X, area.Y, area.Height, color);
        VLine(target, area.Right - 1, area.Y, area.Height, color);
    }

    /// <summary>
    /// Copies <paramref name="sourceArea"/> of <paramref name="source"/> to <paramref name="destinationOffset"/>.
    /// Both the source rectangle and the destination are clipped; no palette transformation is applied.
    /// </summary>
    public static void Blit(PixelBuffer destination, Point destinationOffset, PixelBuffer source, in Rect sourceArea)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);

        Rect sourceRect = sourceArea.Intersect(source.Bounds);
        if (sourceRect.IsEmpty)
        {
            return;
        }

        Rect destinationRect = new(destinationOffset, sourceRect.Size);
        Rect clipped = destinationRect.Intersect(destination.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        int skipX = clipped.X - destinationRect.X;
        int skipY = clipped.Y - destinationRect.Y;

        for (int y = 0; y < clipped.Height; ++y)
        {
            ReadOnlySpan<byte> sourceRow = source.Row(sourceRect.Y + skipY + y);
            Span<byte> destinationRow = destination.Row(clipped.Y + y);
            sourceRow.Slice(sourceRect.X + skipX, clipped.Width).CopyTo(destinationRow[clipped.X..]);
        }
    }

    /// <summary>Draws <paramref name="sprite"/> at its own <see cref="Sprite.OffsetX"/>/<see cref="Sprite.OffsetY"/>.</summary>
    public static void BlitSprite(PixelBuffer destination, Sprite sprite) =>
        BlitSprite(destination, new Point(sprite.OffsetX, sprite.OffsetY), sprite);

    /// <summary>
    /// Draws <paramref name="sprite"/> with its transform layer applied, the equivalent of
    /// <c>fheroes2::Blit( const Sprite &amp; in, Image &amp; out )</c> on a single layer destination.
    /// </summary>
    /// <remarks>
    /// A transform value of <c>0</c> copies the image layer, <c>1</c> keeps the destination pixel
    /// untouched and any other value converts the <i>destination</i> color through
    /// <see cref="PaletteTransformTable"/>, which is what makes shadows and light effects work.
    /// </remarks>
    public static void BlitSprite(PixelBuffer destination, Point destinationOffset, Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sprite);

        Rect destinationRect = new(destinationOffset, sprite.Image.Size);
        Rect clipped = destinationRect.Intersect(destination.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        int skipX = clipped.X - destinationRect.X;
        int skipY = clipped.Y - destinationRect.Y;

        ReadOnlySpan<byte> image = sprite.Image.AsSpan();
        byte[]? transform = sprite.Transform;

        for (int y = 0; y < clipped.Height; ++y)
        {
            int sourceRow = ((skipY + y) * sprite.Image.Stride) + skipX;
            Span<byte> destinationRow = destination.Row(clipped.Y + y);

            for (int x = 0; x < clipped.Width; ++x)
            {
                int sourceIndex = sourceRow + x;
                int destinationIndex = clipped.X + x;
                byte transformId = transform is null ? (byte)0 : transform[sourceIndex];

                if (transformId == 0)
                {
                    destinationRow[destinationIndex] = image[sourceIndex];
                }
                else if (transformId != 1 && transformId < PaletteTransformTable.TransformCount)
                {
                    destinationRow[destinationIndex] =
                        PaletteTransformTable.Apply(transformId, destinationRow[destinationIndex]);
                }
            }
        }
    }
}
