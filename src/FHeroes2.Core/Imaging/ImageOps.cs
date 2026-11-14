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

    /// <summary>
    /// Mirrors <paramref name="source"/> into a new buffer, the port of <c>fheroes2::Flip()</c> as it
    /// is used for the four terrain shape variants of a TIL entry: the second shape bit flips
    /// horizontally, the first one vertically.
    /// </summary>
    public static PixelBuffer Flip(PixelBuffer source, bool horizontal, bool vertical)
    {
        ArgumentNullException.ThrowIfNull(source);

        PixelBuffer result = new(source.Width, source.Height);

        for (int y = 0; y < source.Height; ++y)
        {
            ReadOnlySpan<byte> sourceRow = source.Row(vertical ? source.Height - 1 - y : y);
            Span<byte> destinationRow = result.Row(y);

            if (horizontal)
            {
                for (int x = 0; x < source.Width; ++x)
                {
                    destinationRow[x] = sourceRow[source.Width - 1 - x];
                }
            }
            else
            {
                sourceRow.CopyTo(destinationRow);
            }
        }

        return result;
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

    /// <summary>
    /// Nearest neighbour resampling of <paramref name="source"/> onto <paramref name="target"/>,
    /// the single layer case of <c>fheroes2::Resize( const Image &amp;, Image &amp; )</c>
    /// (<c>engine/image.cpp:3087</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考对「放大」就是对每个目标像素取 <c>x * widthIn / widthOut</c> 处的源像素，没有插值；
    /// 尺寸相同时退化成一次拷贝。
    /// </para>
    /// <para>
    /// 参考还有一条「输出层带透明度、输入层不带」的分支，会按 <c>transformTable</c> 反查调色板变换；
    /// 这里没有 <c>transformTable</c> 的全量数据和「透明像素的调色板变换」这套语义，所以不实现。
    /// </para>
    /// </remarks>
    public static void Resize(PixelBuffer source, PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (source.IsEmpty || target.IsEmpty)
        {
            return;
        }

        if (source.Width == target.Width && source.Height == target.Height)
        {
            source.Pixels.AsSpan(0, source.Height * source.Stride).CopyTo(target.Pixels);

            return;
        }

        int sourceWidth = source.Width;
        int sourceHeight = source.Height;
        int targetWidth = target.Width;
        int targetHeight = target.Height;

        Span<int> column = targetWidth <= 256 ? stackalloc int[targetWidth] : new int[targetWidth];

        for (int x = 0; x < targetWidth; ++x)
        {
            column[x] = (x * sourceWidth) / targetWidth;
        }

        ReadOnlySpan<byte> pixels = source.Pixels;
        int stride = source.Stride;

        for (int y = 0; y < targetHeight; ++y)
        {
            int sourceRow = ((y * sourceHeight) / targetHeight) * stride;
            Span<byte> destinationRow = target.Row(y);

            for (int x = 0; x < targetWidth; ++x)
            {
                destinationRow[x] = pixels[sourceRow + column[x]];
            }
        }
    }

    /// <summary>
    /// Nearest neighbour resampling of a sprite, transparency layer included:
    /// <c>fheroes2::Resize( const Sprite &amp;, Sprite &amp; )</c> (<c>engine/image.cpp:3087</c>).
    /// </summary>
    /// <remarks>
    /// 参考在两层都带透明度时逐层最近邻拷贝，这里照做。<paramref name="source"/> 带透明度层而
    /// <paramref name="target"/> 不带时无法表达结果，直接抛——`null` 的透明度层在 C# 侧表示「整块不透明」，
    /// 静默丢透明信息只会让字形糊成一团。
    /// </remarks>
    public static void Resize(Sprite source, Sprite target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        Resize(source.Image, target.Image);

        if (source.Transform is not { } sourceTransform)
        {
            return;
        }

        if (target.Transform is not { } targetTransform)
        {
            throw new ArgumentException(
                "A source with a transform layer needs a target that has one too.", nameof(target));
        }

        ResizeLayer(
            sourceTransform, source.Width, source.Height, targetTransform, target.Width, target.Height);
    }

    private static void ResizeLayer(
        ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int targetWidth, int targetHeight)
    {
        if (source.Length < sourceWidth * sourceHeight || target.Length < targetWidth * targetHeight)
        {
            throw new ArgumentException("A transform layer must cover the whole sprite.", nameof(source));
        }

        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
        {
            source[..(sourceWidth * sourceHeight)].CopyTo(target);

            return;
        }

        Span<int> column = targetWidth <= 256 ? stackalloc int[targetWidth] : new int[targetWidth];

        for (int x = 0; x < targetWidth; ++x)
        {
            column[x] = (x * sourceWidth) / targetWidth;
        }

        for (int y = 0; y < targetHeight; ++y)
        {
            int sourceRow = ((y * sourceHeight) / targetHeight) * sourceWidth;

            for (int x = 0; x < targetWidth; ++x)
            {
                target[(y * targetWidth) + x] = source[sourceRow + column[x]];
            }
        }
    }
}
