using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The bilinear resampler of the engine, <c>fheroes2::SubpixelResize()</c>
/// (<c>src/engine/image.cpp:2986-3236</c>). Split off from <see cref="ImageOps"/> because it is a
/// self contained algorithm with its own helper types.
/// </summary>
public static partial class ImageOps
{
    /// <summary>
    /// Resizes <paramref name="source"/> into <paramref name="target"/>, a port of
    /// <c>SubpixelResize( const Image &amp; in, Image &amp; out )</c> (<c>image.cpp:2986-2993</c>).
    /// </summary>
    public static void SubpixelResize(Sprite source, Sprite target, Palette gamePalette) =>
        SubpixelResize(
            source,
            new Rect(0, 0, source.Width, source.Height),
            target,
            new Rect(0, 0, target.Width, target.Height),
            gamePalette);

    /// <summary>
    /// Resizes a sprite into a single layer image, the usual direction: a transparent source pixel is
    /// flattened to black because a single layer image cannot carry it.
    /// </summary>
    public static void SubpixelResize(Sprite source, PixelBuffer target, Palette gamePalette) =>
        SubpixelResize(source, new Sprite(target), gamePalette);

    /// <summary>
    /// Resizes a single layer image into another single layer image.
    /// </summary>
    public static void SubpixelResize(PixelBuffer source, PixelBuffer target, Palette gamePalette) =>
        SubpixelResize(new Sprite(source), new Sprite(target), gamePalette);

    /// <summary>
    /// Resizes <paramref name="sourceArea"/> of <paramref name="source"/> into
    /// <paramref name="targetArea"/> of <paramref name="target"/>, a port of
    /// <c>SubpixelResize( in, inX, inY, widthRoiIn, heightRoiIn, out, outX, outY, widthRoiOut, heightRoiOut )</c>
    /// (<c>image.cpp:2995-3236</c>).
    /// </summary>
    /// <param name="source">Image to read, both layers are used when it has a transform layer.</param>
    /// <param name="sourceArea">Region of <paramref name="source"/> to read, in image coordinates.</param>
    /// <param name="target">Image to write, its transform layer is written when it has one.</param>
    /// <param name="targetArea">Region of <paramref name="target"/> to write, in image coordinates.</param>
    /// <param name="gamePalette">
    /// The game palette in video values, <c>getRGBGamePalette()</c> (<see cref="Palette.FromGamePalette"/>).
    /// </param>
    /// <remarks>
    /// <para>
    /// 参考用「角对齐」的双线性插值：源区域的首尾像素分别落在目标区域的首尾像素上，因此放大整数倍时
    /// 边缘不会被重复行重复列糊掉（注释见 <c>image.cpp:3016-3017</c>）。插值出来的颜色再交给
    /// <see cref="PALColorId"/> 落回最近的调色板项，所以它是「按索引存图」的引擎里唯一可用的插值方式。
    /// </para>
    /// <para>
    /// 源图没有变换层时按纯算术加权（参考 <c>in.singleLayer()</c> 分支）；有变换层时只统计变换值为 0 的
    /// 像素，权重合计不足 0.5 的输出像素置为黑并标成「跳过」（变换值 1），其余归一化后按权重合成。
    /// 目标没有变换层时同一块像素只写图像层，也就是把透明像素压成黑——这正是圣言/战场特效需要的效果。
    /// </para>
    /// <para>
    /// 参考的两处早退都照搬：任一区域为空或不完全落在图内（<c>Validate()</c>，<c>image.cpp:207</c>）
    /// 就整块不画；源区域与目标区域同宽同高时退化成逐像素拷贝（只拷图像层，变换层不动）。
    /// </para>
    /// </remarks>
    /// <remarks>
    /// 退化尺寸：参考在本端或目的端只有 1 像素宽/高时会把 <c>step</c> 算成 <c>NaN</c> 或 <c>inf</c>，
    /// 再拿它去转成下标（未定义行为）；端口把非有限的采样位置按 0 处理，于是 1 像素的那一维退化成
    /// 「全部取第一条」。
    /// </remarks>
    public static void SubpixelResize(
        Sprite source,
        in Rect sourceArea,
        Sprite target,
        in Rect targetArea,
        Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (!IsInside(source.Image, sourceArea) || !IsInside(target.Image, targetArea))
        {
            return;
        }

        if (sourceArea.Width == targetArea.Width && sourceArea.Height == targetArea.Height)
        {
            Blit(target.Image, targetArea.Position, source.Image, sourceArea);
            return;
        }

        int widthIn = source.Width;
        int widthOut = target.Width;

        int inputBase = (sourceArea.Y * widthIn) + sourceArea.X;
        int outputBase = (targetArea.Y * widthOut) + targetArea.X;

        double stepX = (double)(sourceArea.Width - 1) / (targetArea.Width - 1);
        double stepY = (double)(sourceArea.Height - 1) / (targetArea.Height - 1);

        int widthRoiOut = targetArea.Width;
        int heightRoiOut = targetArea.Height;

        // Horizontal positions are the same for every row, so they are cached once.
        AxisData[] axisX = new AxisData[widthRoiOut];
        for (int x = 0; x < widthRoiOut; ++x)
        {
            float position = Finite((float)(x * stepX));
            int start = (int)position;

            axisX[x] = new AxisData(position, position - start, start);
        }

        ReadOnlySpan<byte> palette = gamePalette.ToRaw();
        ReadOnlySpan<byte> inputImage = source.Image.AsSpan();
        ReadOnlySpan<byte> inputTransform = source.Transform;
        Span<byte> outputImage = target.Image.AsSpan();
        Span<byte> outputTransform = target.Transform;

        if (!source.HasTransformLayer)
        {
            // A single layer source cannot be transparent, so every output pixel in the area is opaque.
            if (outputTransform.Length > 0)
            {
                for (int y = 0; y < heightRoiOut; ++y)
                {
                    outputTransform.Slice(outputBase + (y * widthOut), widthRoiOut).Clear();
                }
            }

            int preLastOutX = widthRoiOut - 1;
            int preLastOutY = heightRoiOut - 1;

            for (int y = 0; y < preLastOutY; ++y)
            {
                float positionY = Finite((float)(y * stepY));
                int startY = (int)positionY * widthIn;
                float coeffToBottomY = positionY - (int)positionY;
                float coeffToTopY = 1.0F - coeffToBottomY;

                int outputRow = outputBase + (y * widthOut);

                for (int x = 0; x < preLastOutX; ++x)
                {
                    int offsetIn = inputBase + startY + axisX[x].Start;

                    float coeffTopLeft = axisX[x].CoeffToLeft * coeffToTopY;
                    float coeffTopRight = axisX[x].CoeffToRight * coeffToTopY;
                    float coeffBottomLeft = axisX[x].CoeffToLeft * coeffToBottomY;
                    float coeffBottomRight = axisX[x].CoeffToRight * coeffToBottomY;

                    int topLeft = inputImage[offsetIn] * 3;
                    int topRight = inputImage[offsetIn + 1] * 3;
                    int bottomLeft = inputImage[offsetIn + widthIn] * 3;
                    int bottomRight = inputImage[offsetIn + widthIn + 1] * 3;

                    float red = ((palette[topLeft] >> 2) * coeffTopLeft) + ((palette[topRight] >> 2) * coeffTopRight)
                        + ((palette[bottomLeft] >> 2) * coeffBottomLeft) + ((palette[bottomRight] >> 2) * coeffBottomRight) + 0.5F;
                    float green = ((palette[topLeft + 1] >> 2) * coeffTopLeft) + ((palette[topRight + 1] >> 2) * coeffTopRight)
                        + ((palette[bottomLeft + 1] >> 2) * coeffBottomLeft) + ((palette[bottomRight + 1] >> 2) * coeffBottomRight) + 0.5F;
                    float blue = ((palette[topLeft + 2] >> 2) * coeffTopLeft) + ((palette[topRight + 2] >> 2) * coeffTopRight)
                        + ((palette[bottomLeft + 2] >> 2) * coeffBottomLeft) + ((palette[bottomRight + 2] >> 2) * coeffBottomRight) + 0.5F;

                    outputImage[outputRow + x] = PALColorId.Get((byte)red, (byte)green, (byte)blue, palette);
                }

                // The last column of the image: interpolate only vertically.
                {
                    int offsetIn = inputBase + startY + sourceArea.Width - 1;

                    int top = inputImage[offsetIn] * 3;
                    int bottom = inputImage[offsetIn + widthIn] * 3;

                    float red = ((palette[top] >> 2) * coeffToTopY) + ((palette[bottom] >> 2) * coeffToBottomY) + 0.5F;
                    float green = ((palette[top + 1] >> 2) * coeffToTopY) + ((palette[bottom + 1] >> 2) * coeffToBottomY) + 0.5F;
                    float blue = ((palette[top + 2] >> 2) * coeffToTopY) + ((palette[bottom + 2] >> 2) * coeffToBottomY) + 0.5F;

                    outputImage[outputRow + preLastOutX] = PALColorId.Get((byte)red, (byte)green, (byte)blue, palette);
                }
            }

            // The last row of the image: interpolate only horizontally.
            {
                int startY = (int)Finite((float)(preLastOutY * stepY)) * widthIn;
                int outputRow = outputBase + (preLastOutY * widthOut);

                for (int x = 0; x < preLastOutX; ++x)
                {
                    int offsetIn = inputBase + startY + axisX[x].Start;

                    int left = inputImage[offsetIn] * 3;
                    int right = inputImage[offsetIn + 1] * 3;

                    float red = ((palette[left] >> 2) * axisX[x].CoeffToLeft) + ((palette[right] >> 2) * axisX[x].CoeffToRight) + 0.5F;
                    float green = ((palette[left + 1] >> 2) * axisX[x].CoeffToLeft) + ((palette[right + 1] >> 2) * axisX[x].CoeffToRight) + 0.5F;
                    float blue = ((palette[left + 2] >> 2) * axisX[x].CoeffToLeft) + ((palette[right + 2] >> 2) * axisX[x].CoeffToRight) + 0.5F;

                    outputImage[outputRow + x] = PALColorId.Get((byte)red, (byte)green, (byte)blue, palette);
                }

                // The output bottom-right pixel is a copy of the input bottom-right pixel.
                outputImage[outputRow + preLastOutX] = inputImage[inputBase + (widthIn * sourceArea.Height) - 1];
            }

            return;
        }

        // The source is transparent in places, so the result is averaged over the opaque pixels only.
        float maxPositionX = sourceArea.Width - 1;
        float maxPositionY = sourceArea.Height - 1;

        byte blackColor = PALColorId.Get(0, 0, 0, palette);
        bool writeTransform = outputTransform.Length > 0;

        for (int y = 0; y < heightRoiOut; ++y)
        {
            float positionY = Finite((float)(y * stepY));
            int startY = (int)positionY * widthIn;
            float coeffToBottomY = positionY - (int)positionY;
            float coeffToTopY = 1.0F - coeffToBottomY;

            int outputRow = outputBase + (y * widthOut);

            for (int x = 0; x < widthRoiOut; ++x)
            {
                int offsetIn = inputBase + startY + axisX[x].Start;

                // The output weight is the weight of non-transparent image data in the output pixel.
                float outWeight = 0.0F;
                float red = 0.0F;
                float green = 0.0F;
                float blue = 0.0F;

                if (axisX[x].Position < maxPositionX && positionY < maxPositionY)
                {
                    Accumulate(inputImage[offsetIn], inputTransform[offsetIn], axisX[x].CoeffToLeft * coeffToTopY, palette, ref outWeight, ref red, ref green, ref blue);
                    Accumulate(inputImage[offsetIn + 1], inputTransform[offsetIn + 1], axisX[x].CoeffToRight * coeffToTopY, palette, ref outWeight, ref red, ref green, ref blue);
                    Accumulate(inputImage[offsetIn + widthIn], inputTransform[offsetIn + widthIn], axisX[x].CoeffToLeft * coeffToBottomY, palette, ref outWeight, ref red, ref green, ref blue);
                    Accumulate(inputImage[offsetIn + widthIn + 1], inputTransform[offsetIn + widthIn + 1], axisX[x].CoeffToRight * coeffToBottomY, palette, ref outWeight, ref red, ref green, ref blue);
                }
                else if (positionY < maxPositionY)
                {
                    // The last column of the image: interpolate only vertically.
                    Accumulate(inputImage[offsetIn], inputTransform[offsetIn], coeffToTopY, palette, ref outWeight, ref red, ref green, ref blue);
                    Accumulate(inputImage[offsetIn + widthIn], inputTransform[offsetIn + widthIn], coeffToBottomY, palette, ref outWeight, ref red, ref green, ref blue);
                }
                else if (axisX[x].Position < maxPositionX)
                {
                    // The last row of the image: interpolate only horizontally.
                    Accumulate(inputImage[offsetIn], inputTransform[offsetIn], axisX[x].CoeffToLeft, palette, ref outWeight, ref red, ref green, ref blue);
                    Accumulate(inputImage[offsetIn + 1], inputTransform[offsetIn + 1], axisX[x].CoeffToRight, palette, ref outWeight, ref red, ref green, ref blue);
                }
                else
                {
                    // The output bottom-right pixel is a copy of the input bottom-right pixel.
                    Accumulate(inputImage[offsetIn], inputTransform[offsetIn], 1.0F, palette, ref outWeight, ref red, ref green, ref blue);
                }

                // The output pixel is non-transparent if its weight is not less than 0.5.
                if (outWeight >= 0.5F)
                {
                    red = (red / outWeight) + 0.5F;
                    green = (green / outWeight) + 0.5F;
                    blue = (blue / outWeight) + 0.5F;

                    outputImage[outputRow + x] = PALColorId.Get((byte)red, (byte)green, (byte)blue, palette);

                    if (writeTransform)
                    {
                        outputTransform[outputRow + x] = 0;
                    }
                }
                else
                {
                    // Make the output pixel transparent and, for a single layer target, black.
                    outputImage[outputRow + x] = blackColor;

                    if (writeTransform)
                    {
                        outputTransform[outputRow + x] = 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Replaces a non finite sample position by zero. The reference casts the same <c>NaN</c>/<c>inf</c>
    /// straight to <c>int32_t</c>, which is undefined; the one pixel case is the only way to get there.
    /// </summary>
    private static float Finite(float position) => float.IsFinite(position) ? position : 0.0F;

    /// <summary>The <c>Validate()</c> of the engine (<c>image.cpp:207-221</c>): a region must be non empty and inside the image.</summary>
    private static bool IsInside(PixelBuffer image, in Rect area) =>
        !image.IsEmpty && area.Width > 0 && area.Height > 0
        && area.X >= 0 && area.Y >= 0 && area.Right <= image.Width && area.Bottom <= image.Height;

    /// <summary>
    /// Adds one of the four (or two, or one) bilinear taps to the output pixel, the <c>addPixel</c> lambda of
    /// <c>image.cpp:3166-3174</c>.
    /// </summary>
    private static void Accumulate(
        byte pixel,
        byte transform,
        float coefficient,
        ReadOnlySpan<byte> palette,
        ref float outWeight,
        ref float red,
        ref float green,
        ref float blue)
    {
        if (transform != 0 || coefficient <= 0.0F)
        {
            return;
        }

        outWeight += coefficient;

        int offset = pixel * 3;
        red += (palette[offset] >> 2) * coefficient;
        green += (palette[offset + 1] >> 2) * coefficient;
        blue += (palette[offset + 2] >> 2) * coefficient;
    }

    /// <summary>Cached horizontal sample of the resampler, the <c>AxisData</c> of <c>image.cpp:3022-3037</c>.</summary>
    private readonly struct AxisData
    {
        public AxisData(float position, float coeffToRight, int start)
        {
            Position = position;
            CoeffToRight = coeffToRight;
            CoeffToLeft = 1.0F - coeffToRight;
            Start = start;
        }

        public float Position { get; }

        public float CoeffToRight { get; }

        public float CoeffToLeft { get; }

        public int Start { get; }
    }
}
