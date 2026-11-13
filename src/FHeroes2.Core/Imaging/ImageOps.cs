using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Clipping aware primitives on <see cref="PixelBuffer"/>. Everything here works on the
/// indexed 8 bit surface; palette lookup happens only when the frame is presented.
/// </summary>
public static partial class ImageOps
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
    public static void BlitSprite(PixelBuffer destination, Point destinationOffset, Sprite sprite) =>
        BlitSprite(destination, destinationOffset, sprite, sprite.Image.Bounds);

    /// <summary>
    /// Draws the <paramref name="sourceArea"/> part of <paramref name="sprite"/> with its transform
    /// layer applied. <paramref name="sourceArea"/> is in sprite local coordinates, which is what
    /// clipping a single character of a bitmap font or an arbitrary sub rectangle needs.
    /// </summary>
    public static void BlitSprite(PixelBuffer destination, Point destinationOffset, Sprite sprite, in Rect sourceArea)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sprite);

        Rect sourceRect = sourceArea.Intersect(sprite.Image.Bounds);
        if (sourceRect.IsEmpty)
        {
            return;
        }

        Rect destinationRect = new(
            destinationOffset.X + sourceRect.X,
            destinationOffset.Y + sourceRect.Y,
            sourceRect.Width,
            sourceRect.Height);

        Rect clipped = destinationRect.Intersect(destination.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        int skipX = clipped.X - destinationRect.X;
        int skipY = clipped.Y - destinationRect.Y;
        int sourceX = sourceRect.X + skipX;
        int sourceY = sourceRect.Y + skipY;

        ReadOnlySpan<byte> image = sprite.Image.AsSpan();
        byte[]? transform = sprite.Transform;

        for (int y = 0; y < clipped.Height; ++y)
        {
            int sourceRow = ((sourceY + y) * sprite.Image.Stride) + sourceX;
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

    /// <summary>
    /// Converts every pixel of <paramref name="target"/> through the recolouring
    /// <paramref name="palette"/>, the equivalent of <c>fheroes2::ApplyPalette( Image &amp;, ... )</c>.
    /// </summary>
    /// <remarks>
    /// The table maps a palette index onto another one (see <see cref="PaletteTable"/>), so the
    /// image keeps its shape and only changes colour: that is how the original engine turns the
    /// white <c>FONT.ICN</c> into the yellow and gray faces, and how a disabled button is drawn.
    /// The transform layer is left alone - it stores operations, not colours.
    /// </remarks>
    /// <exception cref="ArgumentException">The table is not <see cref="PaletteTable.Size"/> bytes long.</exception>
    public static void ApplyPalette(PixelBuffer target, ReadOnlySpan<byte> palette)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (palette.Length != PaletteTable.Size)
        {
            throw new ArgumentException(
                $"A palette table must hold {PaletteTable.Size} entries.", nameof(palette));
        }

        for (int y = 0; y < target.Height; ++y)
        {
            Span<byte> row = target.Row(y);

            for (int x = 0; x < row.Length; ++x)
            {
                row[x] = palette[row[x]];
            }
        }
    }

    /// <summary>Converts <paramref name="target"/> through one of the original palettes.</summary>
    public static void ApplyPalette(PixelBuffer target, PaletteId id) => ApplyPalette(target, PaletteTable.Get(id));
}
