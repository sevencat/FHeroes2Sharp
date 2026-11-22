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
    /// Draws <paramref name="sourceArea"/> of <paramref name="sprite"/> so that the area's own top left corner
    /// lands on <paramref name="destinationPoint"/>, with the result clipped to <paramref name="clip"/>: the
    /// equivalent of <c>fheroes2::Blit( in, inPos, inSize, out, outPos )</c> behind a <c>FitToRoi</c>.
    /// </summary>
    /// <remarks>
    /// The <see cref="BlitSprite(PixelBuffer, Point, Sprite, in Rect)"/> overload places the <i>sprite's</i> origin on
    /// the offset, which is the wrong end of the arithmetic when only a slice of a sprite travels somewhere — the
    /// bottom bar and the guild picture both do that.
    /// </remarks>
    /// <param name="destination">The display buffer.</param>
    /// <param name="destinationPoint">Where the source area's top left corner goes, in destination coordinates.</param>
    /// <param name="sprite">The sprite to read.</param>
    /// <param name="sourceArea">The part of the sprite to draw, in sprite local coordinates.</param>
    /// <param name="clip">The only region of <paramref name="destination"/> the drawing may touch.</param>
    public static void BlitSpriteClipped(
        PixelBuffer destination,
        Point destinationPoint,
        Sprite sprite,
        in Rect sourceArea,
        in Rect clip)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sprite);

        Rect area = sourceArea.Intersect(sprite.Image.Bounds);
        if (area.IsEmpty)
        {
            return;
        }

        Rect visible = new Rect(destinationPoint.X, destinationPoint.Y, area.Width, area.Height)
            .Clip(clip)
            .Clip(destination.Bounds);

        if (visible.IsEmpty)
        {
            return;
        }

        Rect source = new(
            area.X + (visible.X - destinationPoint.X),
            area.Y + (visible.Y - destinationPoint.Y),
            visible.Width,
            visible.Height);

        BlitSprite(destination, new Point(visible.X - source.X, visible.Y - source.Y), sprite, source);
    }

    /// <summary>
    /// Draws <paramref name="sourceArea"/> of <paramref name="sprite"/> onto <paramref name="destination"/> with a
    /// constant <paramref name="alpha"/>, clipping the result to <paramref name="clip"/>: the equivalent of
    /// <c>fheroes2::AlphaBlit( in, inX, inY, out, outX, outY, width, height, alpha, flip )</c> behind a
    /// <c>FitToRoi</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The transform layer is honoured exactly as <see cref="BlitSprite(PixelBuffer, Point, Sprite, in Rect)"/> does
    /// it: <c>0</c> draws the pixel, <c>1</c> leaves the destination alone, anything else converts the destination
    /// through <see cref="PaletteTransformTable"/>. The difference is only how a drawn pixel is combined: the
    /// reference mixes the sprite colour with the colour already on the display in proportion to
    /// <paramref name="alpha"/> and looks the result up in <paramref name="gamePalette"/>, which cannot be expressed
    /// as a palette table because the mix depends on the destination pixel.
    /// </para>
    /// <para>
    /// <paramref name="alpha"/> of <c>255</c> degrades to the plain blit and <c>0</c> draws nothing, which is what the
    /// reference does as well. <paramref name="flipHorizontally"/> mirrors the drawn segment of the row, i.e. the
    /// <paramref name="sourceArea"/> columns are visited right to left.
    /// </para>
    /// </remarks>
    /// <param name="destination">The display buffer.</param>
    /// <param name="destinationPoint">Where the sprite's origin goes, in destination coordinates.</param>
    /// <param name="sprite">The sprite to read.</param>
    /// <param name="sourceArea">The part of the sprite to draw, in sprite local coordinates.</param>
    /// <param name="clip">The only region of <paramref name="destination"/> the drawing may touch.</param>
    /// <param name="alpha">Opacity of the sprite, <c>0</c> to <c>255</c>.</param>
    /// <param name="flipHorizontally">Whether the sprite is drawn mirrored along the vertical axis.</param>
    /// <param name="gamePalette">The palette the blended colours are looked up in.</param>
    public static void AlphaBlitSpriteClipped(
        PixelBuffer destination,
        Point destinationPoint,
        Sprite sprite,
        in Rect sourceArea,
        in Rect clip,
        byte alpha,
        bool flipHorizontally,
        Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (alpha == 0)
        {
            return;
        }

        Rect area = sourceArea.Intersect(sprite.Image.Bounds);
        if (area.IsEmpty)
        {
            return;
        }

        Rect visible = new Rect(destinationPoint.X, destinationPoint.Y, area.Width, area.Height)
            .Clip(clip)
            .Clip(destination.Bounds)
            .Intersect(new Rect(destinationPoint.X, destinationPoint.Y, area.Width, area.Height));

        if (visible.IsEmpty)
        {
            return;
        }

        int skipX = visible.X - destinationPoint.X;
        int skipY = visible.Y - destinationPoint.Y;

        ReadOnlySpan<byte> image = sprite.Image.AsSpan();
        byte[]? transform = sprite.Transform;
        int stride = sprite.Image.Stride;

        // Column of the source row the left-most visible pixel reads. The mirrored case walks the area backwards.
        int sourceStartX = flipHorizontally ? area.X + area.Width - 1 - skipX : area.X + skipX;
        int sourceStep = flipHorizontally ? -1 : 1;

        if (alpha == 255)
        {
            for (int y = 0; y < visible.Height; ++y)
            {
                int sourceRow = ((area.Y + skipY + y) * stride) + sourceStartX;
                Span<byte> destinationRow = destination.Row(visible.Y + y);

                for (int x = 0; x < visible.Width; ++x)
                {
                    int sourceIndex = sourceRow + (x * sourceStep);
                    int destinationIndex = visible.X + x;
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

            return;
        }

        ReadOnlySpan<byte> palette = gamePalette.ToRaw();
        int behind = 255 - alpha;

        for (int y = 0; y < visible.Height; ++y)
        {
            int sourceRow = ((area.Y + skipY + y) * stride) + sourceStartX;
            Span<byte> destinationRow = destination.Row(visible.Y + y);

            for (int x = 0; x < visible.Width; ++x)
            {
                int sourceIndex = sourceRow + (x * sourceStep);
                int destinationIndex = visible.X + x;
                byte transformId = transform is null ? (byte)0 : transform[sourceIndex];

                if (transformId == 1)
                {
                    continue;
                }

                byte sourceValue = image[sourceIndex];
                if (transformId > 1 && transformId < PaletteTransformTable.TransformCount)
                {
                    // The reference transforms the colour already on the display, not the sprite pixel.
                    sourceValue = PaletteTransformTable.Apply(transformId, destinationRow[destinationIndex]);
                }

                byte destinationValue = destinationRow[destinationIndex];

                // The palette is stored with eight bits per channel while the lookup works on six, exactly like the
                // reference's RGB palette: the blend happens on the eight bit values and only the result is narrowed.
                int red = (palette[(sourceValue * 3) + 0] * alpha) + (palette[(destinationValue * 3) + 0] * behind);
                int green = (palette[(sourceValue * 3) + 1] * alpha) + (palette[(destinationValue * 3) + 1] * behind);
                int blue = (palette[(sourceValue * 3) + 2] * alpha) + (palette[(destinationValue * 3) + 2] * behind);

                destinationRow[destinationIndex] = PALColorId.Get(
                    (byte)(red / 255),
                    (byte)(green / 255),
                    (byte)(blue / 255),
                    palette);
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
    /// Recolours the pixels of <paramref name="area"/> through <paramref name="palette"/>, the region
    /// form of <c>fheroes2::ApplyPalette( const Image &amp; in, ... Image &amp; out, ... )</c>
    /// (<c>engine/image_tool.cpp</c>).
    /// </summary>
    /// <remarks>
    /// 参考有六个指针参数（输入/输出各一套坐标与尺寸），在端口里输入就是输出、坐标就是区域，
    /// 只剩「按表换色」这一件事。区域先按目标裁一遍，区域之外不动。
    /// 调色板淡化（<c>gui/ui_tool.cpp:395</c> 的 <c>colorFade()</c>）用它把灰阶画面落回游戏调色板的索引。
    /// </remarks>
    /// <exception cref="ArgumentException">The table is not <see cref="PaletteTable.Size"/> bytes long.</exception>
    public static void ApplyPalette(PixelBuffer target, in Rect area, ReadOnlySpan<byte> palette)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (palette.Length != PaletteTable.Size)
        {
            throw new ArgumentException(
                $"A palette table must hold {PaletteTable.Size} entries.", nameof(palette));
        }

        Rect roi = area.Intersect(target.Bounds);
        if (roi.IsEmpty)
        {
            return;
        }

        for (int y = roi.Y; y < roi.Bottom; ++y)
        {
            Span<byte> row = target.Row(y);

            for (int x = roi.X; x < roi.Right; ++x)
            {
                row[x] = palette[row[x]];
            }
        }
    }

    /// <summary>
    /// Blends <paramref name="source"/> into <paramref name="target"/> after darkening every palette
    /// entry by <paramref name="alpha"/>, a port of <c>fheroes2::ApplyAlpha()</c>
    /// (<c>engine/image.cpp:1153-1168</c>).
    /// </summary>
    /// <param name="source">Pixels that get recoloured.</param>
    /// <param name="sourceArea">Region of <paramref name="source"/> to take.</param>
    /// <param name="target">Destination buffer.</param>
    /// <param name="targetPosition">Top left corner of the region in <paramref name="target"/>.</param>
    /// <param name="alpha">Brightness factor, <c>255</c> keeps the colours, <c>0</c> makes them black.</param>
    /// <param name="gamePalette">The game palette in video values, <c>getRGBGamePalette()</c>.</param>
    /// <remarks>
    /// <para>
    /// 参考不是「按 alpha 混合两层」，而是先把调色板每一项的六个颜色通道乘以 <c>alpha / 255</c>，
    /// 再用 <see cref="PALColorId"/> 找回最近的颜色，得到一张「压暗后的调色板」，最后按这张表给源像素换色
    /// 写进目标。所以 <paramref name="alpha"/> 越小，画面越暗。索引 <c>0..9</c> 与 <c>254..255</c>
    /// 在游戏调色板里未定义，参考把它们留在表里的 <c>0</c>（黑色），端口一样处理。
    /// </para>
    /// <para>
    /// 参考用六个坐标参数（输入与输出各一套），端口换成区域加左上角；两边都先裁剪再逐像素换色。
    /// </para>
    /// </remarks>
    public static void ApplyAlpha(
        PixelBuffer source,
        in Rect sourceArea,
        PixelBuffer target,
        in Point targetPosition,
        byte alpha,
        Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(gamePalette);

        Rect sourceRoi = sourceArea.Intersect(source.Bounds);
        if (sourceRoi.IsEmpty)
        {
            return;
        }

        Rect requestedTarget = new(
            targetPosition.X + (sourceRoi.X - sourceArea.X),
            targetPosition.Y + (sourceRoi.Y - sourceArea.Y),
            sourceRoi.Width,
            sourceRoi.Height);

        Rect targetRoi = requestedTarget.Intersect(target.Bounds);

        if (targetRoi.IsEmpty)
        {
            return;
        }

        int width = targetRoi.Width;
        int height = targetRoi.Height;
        int sourceX = sourceRoi.X + (targetRoi.X - targetPosition.X - (sourceRoi.X - sourceArea.X));

        // A palette entry above 253 is undefined in the game palette, just like the first ten:
        // the reference leaves them pointing at index 0, which is black in every game palette.
        Span<byte> conversion = stackalloc byte[PaletteTable.Size];

        ReadOnlySpan<byte> palette = gamePalette.ToRaw();
        for (int id = 10; id < 254; ++id)
        {
            byte red = (byte)((palette[id * 3] >> 2) * alpha / 255);
            byte green = (byte)((palette[(id * 3) + 1] >> 2) * alpha / 255);
            byte blue = (byte)((palette[(id * 3) + 2] >> 2) * alpha / 255);

            conversion[id] = PALColorId.Get(red, green, blue, palette);
        }

        int sourceY = sourceRoi.Y + (targetRoi.Y - targetPosition.Y - (sourceRoi.Y - sourceArea.Y));
        for (int y = 0; y < height; ++y)
        {
            ReadOnlySpan<byte> sourceRow = source.Row(sourceY + y);
            Span<byte> targetRow = target.Row(targetRoi.Y + y);

            for (int x = 0; x < width; ++x)
            {
                targetRow[targetRoi.X + x] = conversion[sourceRow[sourceX + x]];
            }
        }
    }

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
    /// Nearest neighbour resampling of the <paramref name="sourceArea"/> part of <paramref name="source"/> onto
    /// <paramref name="targetArea"/> of <paramref name="target"/>,
    /// <c>fheroes2::Resize( const Image &amp;, int32_t, int32_t, int32_t, int32_t, Image &amp;, int32_t, int32_t, int32_t, int32_t )</c>
    /// (<c>engine/image.cpp:2771</c>).
    /// </summary>
    /// <remarks>
    /// 参考先把两个区域裁到各自图像的范围内，任一为空就直接返回；尺寸相同时退化成逐行拷贝。
    /// 这里同样只搬像素层——参考的这个重载不管透明度层，<c>drawMode</c> 放大图标时就靠「只动像素层」保持边框原样。
    /// </remarks>
    public static void Resize(PixelBuffer source, in Rect sourceArea, PixelBuffer target, in Rect targetArea)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        Rect from = sourceArea.Intersect(source.Bounds);
        Rect to = targetArea.Intersect(target.Bounds);

        if (from.IsEmpty || to.IsEmpty)
        {
            return;
        }

        if (from.Width == to.Width && from.Height == to.Height)
        {
            for (int y = 0; y < to.Height; ++y)
            {
                source.Pixels.AsSpan(((from.Y + y) * source.Stride) + from.X, from.Width)
                    .CopyTo(target.Row(to.Y + y).Slice(to.X, to.Width));
            }

            return;
        }

        ReadOnlySpan<byte> pixels = source.Pixels;
        int stride = source.Stride;

        for (int y = 0; y < to.Height; ++y)
        {
            int sourceRow = ((from.Y + ((y * from.Height) / to.Height)) * stride) + from.X;
            Span<byte> destinationRow = target.Row(to.Y + y);

            for (int x = 0; x < to.Width; ++x)
            {
                destinationRow[to.X + x] = pixels[sourceRow + ((x * from.Width) / to.Width)];
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
