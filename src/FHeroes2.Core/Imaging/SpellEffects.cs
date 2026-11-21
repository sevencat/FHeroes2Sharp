using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The three spell animation helpers of <c>src/fheroes2/gui/ui_tool.cpp</c>:
/// <c>CreateDeathWaveEffect()</c> (<c>:485-543</c>), <c>CreateHolyShoutEffect()</c>
/// (<c>:545-619</c>) and <c>createRippleEffect()</c> (<c>:621-651</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把这三个函数放在 <c>gui/ui_tool.cpp</c>，端口放在 Core：调用它们的是战斗界面
/// （<c>battle/battle_interface.cpp:5740</c>、<c>:6381</c>、<c>:6491</c>）与 ICN 资源生成
/// （<c>game/game_assets.cpp:2876-2877</c>、<c>:4359</c>），后者在 Assets 层，而 Assets 只引用 Core，
/// 放进 UI 会被上层反向依赖。
/// </para>
/// <para>
/// 三个函数都只处理索引像素，不需要显示层，也不需要事件循环，所以端口是纯函数：输入进去、新图出来。
/// </para>
/// </remarks>
public static class SpellEffects
{
    /// <summary>
    /// Draws the Death Wave distortion of <paramref name="input"/> at <paramref name="x"/> into a new
    /// image, a port of <c>CreateDeathWaveEffect()</c> (<c>ui_tool.cpp:485-543</c>).
    /// </summary>
    /// <param name="input">Frame the wave is cut out of, the battle field copy of the caller.</param>
    /// <param name="x">
    /// Horizontal position of the wave in <paramref name="input"/> coordinates. The wave occupies
    /// <c>[x - waveLength, x)</c> so it enters from the left of the image.
    /// </param>
    /// <param name="deathWaveCurve">
    /// One negative offset per wave column; <c>-1</c> is no distortion. Values must stay in
    /// <c>(-input.Height, 0)</c>, see <see cref="BuildDeathWaveCurve"/>.
    /// </param>
    /// <returns>
    /// An <c>outWaveWidth x input.Height</c> image, or an empty image when the wave is completely
    /// outside <paramref name="input"/> and nothing has to be drawn.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 参考原地扩容并复用调用方的 <c>Image &amp; out</c>（每帧只画右下角一块），端口每次返回新图，
    /// 因为 <see cref="PixelBuffer"/> 尺寸固定。调用点只取左上 <c>waveWidth x height</c>，而 <c>waveWidth</c>
    /// 与这里的 <c>outWaveWidth</c> 是同一个公式，写到的每一列都从头覆盖，所以两者逐像素相同。
    /// </para>
    /// <para>
    /// 参考在曲线值越界时 <c>assert(0)</c> 后 <c>continue</c>，那一列留旧内容；端口留零。
    /// </para>
    /// </remarks>
    public static PixelBuffer CreateDeathWaveEffect(PixelBuffer input, int x, IReadOnlyList<int> deathWaveCurve)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(deathWaveCurve);

        if (input.IsEmpty)
        {
            return new PixelBuffer(0, 0);
        }

        int inputWidth = input.Width;
        int waveLength = deathWaveCurve.Count;

        // If the death wave curve is outside of the battlefield - return.
        if (x < 0 || (x - waveLength) >= inputWidth || waveLength == 0)
        {
            return new PixelBuffer(0, 0);
        }

        int inputHeight = input.Height;
        int outputWidth = x > waveLength ? (x > inputWidth ? waveLength - x + inputWidth : waveLength) : x;

        if (outputWidth <= 0 || inputHeight <= 0)
        {
            return new PixelBuffer(0, 0);
        }

        PixelBuffer output = new(outputWidth, inputHeight);

        // Where the input image starts and which part of the curve is used.
        int offsetX = x < waveLength ? 0 : x - waveLength;
        int startIndex = x < waveLength ? waveLength - x : 0;
        int endIndex = deathWaveCurve.Count - (x > inputWidth ? x - inputWidth : 0);

        int outputColumn = 0;
        for (int index = startIndex; index < endIndex; ++index, ++outputColumn)
        {
            int curve = deathWaveCurve[index];

            // The death curve should have only negative values and should not be higher than the height of the input.
            if (curve >= 0 || curve <= -inputHeight)
            {
                continue;
            }

            int inputColumn = offsetX + outputColumn;

            // Shift the pixels above the curve down by its offset.
            for (int row = 0; row < inputHeight + curve; ++row)
            {
                output[outputColumn, row] = input[inputColumn, row - curve];
            }

            // Flip the image under the death wave to create the distortion.
            for (int row = inputHeight + curve; row < inputHeight; ++row)
            {
                output[outputColumn, row] = input[inputColumn, (2 * inputHeight) + curve - 1 - row];
            }
        }

        return output;
    }

    /// <summary>
    /// Turns <paramref name="input"/> into the reddish blurred image of the Holy Word / Holy Shout
    /// spell, a port of <c>CreateHolyShoutEffect()</c> (<c>ui_tool.cpp:545-619</c>).
    /// </summary>
    /// <param name="input">Image to blur.</param>
    /// <param name="blurRadius">
    /// Radius of the cross shaped blur. Below <c>1</c> the input is returned unchanged, like the reference.
    /// </param>
    /// <param name="darkredStrength">How far the result is pushed to dark red, <c>0</c> keeps the colours.</param>
    /// <param name="gamePalette">
    /// The game palette in video values, <c>getRGBGamePalette()</c> of the reference
    /// (<see cref="Palette.FromGamePalette"/>).
    /// </param>
    /// <returns>The blurred image, or a clone of <paramref name="input"/> when the radius is below one.</returns>
    /// <remarks>
    /// <para>
    /// 参考不是做高斯模糊，而是对每个像素取「横向一行 + 纵向一列」的十字均值，中心像素只算一次
    /// （<c>roiSize = rangeX + rangeY - 1</c>），再用两个系数把结果拉向暗红：
    /// 红通道 <c>4 - darkredStrength / 280</c>，绿蓝 <c>4 - darkredStrength / 80</c>。
    /// 乘 4 是因为 <c>GetColorId()</c> 内部要除以 4 再查 64³ 表（<c>image.cpp:2660-2663</c>）。
    /// </para>
    /// <para>
    /// 差异：参考的 <c>getRGBGamePalette()</c> 是 6 位色（0..63），端口 <see cref="Palette"/> 存的是
    /// 左移两位后的 0..255，所以这里先取 <c>&gt;&gt; 2</c> 还原，再按参考的系数与截断走，
    /// 结果与参考逐位相同。参考 <c>return in;</c> 返回写时复制的同一张图，端口给克隆。
    /// </para>
    /// </remarks>
    public static PixelBuffer CreateHolyShoutEffect(PixelBuffer input, int blurRadius, byte darkredStrength, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (input.IsEmpty)
        {
            return new PixelBuffer(0, 0);
        }

        if (blurRadius < 1)
        {
            return input.Clone();
        }

        int width = input.Width;
        int height = input.Height;

        // The Holy Word/Shout effect should have some dark + red effect, green and blue are reduced stronger.
        double redCoeff = 4.0 - (darkredStrength / 280.0);
        double greenBlueCoeff = 4.0 - (darkredStrength / 80.0);

        PixelBuffer output = new(width, height);
        ReadOnlySpan<byte> palette = gamePalette.ToRaw();

        for (int y = 0; y < height; ++y)
        {
            int startY = Math.Max(y - blurRadius, 0);
            int rangeY = Math.Min(y + blurRadius + 1, height) - startY;

            for (int x = 0; x < width; ++x)
            {
                int startX = Math.Max(x - blurRadius, 0);
                int rangeX = Math.Min(x + blurRadius + 1, width) - startX;

                uint sumRed = 0;
                uint sumGreen = 0;
                uint sumBlue = 0;

                // Horizontal part of the cross, the centre pixel included.
                for (int sourceX = startX; sourceX < startX + rangeX; ++sourceX)
                {
                    int offset = input[sourceX, y] * 3;
                    sumRed += (uint)(palette[offset] >> 2);
                    sumGreen += (uint)(palette[offset + 1] >> 2);
                    sumBlue += (uint)(palette[offset + 2] >> 2);
                }

                // Vertical part of the cross, the centre pixel skipped as it has been counted above.
                for (int sourceY = startY; sourceY < startY + rangeY; ++sourceY)
                {
                    if (sourceY == y)
                    {
                        continue;
                    }

                    int offset = input[x, sourceY] * 3;
                    sumRed += (uint)(palette[offset] >> 2);
                    sumGreen += (uint)(palette[offset + 1] >> 2);
                    sumBlue += (uint)(palette[offset + 2] >> 2);
                }

                uint roiSize = (uint)(rangeX + rangeY - 1);
                byte red = (byte)(redCoeff * sumRed / roiSize);
                byte green = (byte)(greenBlueCoeff * sumGreen / roiSize);
                byte blue = (byte)(greenBlueCoeff * sumBlue / roiSize);

                // GetColorId() divides by four before it looks the colour up in the 64 x 64 x 64 table.
                output[x, y] = PALColorId.Get((byte)(red / 4), (byte)(green / 4), (byte)(blue / 4), palette);
            }
        }

        return output;
    }

    /// <summary>
    /// Wobbles <paramref name="input"/> horizontally into a wave, a port of <c>createRippleEffect()</c>
    /// (<c>ui_tool.cpp:621-651</c>).
    /// </summary>
    /// <param name="input">Sprite to distort, both layers are carried over.</param>
    /// <param name="amplitudeInPixels">Maximum horizontal shift of a row, in pixels.</param>
    /// <param name="phaseAtImageTop">Phase of the sine at the top row, in radians.</param>
    /// <param name="periodInPixels">Length of one wave along the vertical axis, in pixels.</param>
    /// <returns>
    /// A sprite <c>2 * amplitudeInPixels</c> pixels wider with the wave applied, or
    /// <paramref name="input"/> itself when it has no transform layer or the amplitude is zero.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 每一行按 <c>sin( 2π·y / period + phase )</c> 横向平移，平移量是
    /// <c>round( amplitude · ( sin + 1 ) )</c>，所以整体落在 <c>[0, 2·amplitude]</c>，新图因此宽出两倍幅度；
    /// 图片与变换层一起搬，锚点左移一个幅度。
    /// </para>
    /// <para>
    /// 差异：参考的 <c>std::round</c> 是「四舍五入远离零」，端口用同一套 <see cref="MidpointRounding.AwayFromZero"/>，
    /// 不是默认的银行家舍入。参考允许负幅度（会得到空图）与 <c>periodInPixels == 0</c>（除零），端口直接抛异常。
    /// </para>
    /// </remarks>
    public static Sprite CreateRippleEffect(Sprite input, int amplitudeInPixels, double phaseAtImageTop, int periodInPixels)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegative(amplitudeInPixels);
        ArgumentOutOfRangeException.ThrowIfZero(periodInPixels);

        if (!input.HasTransformLayer || amplitudeInPixels == 0 || input.Width <= 0 || input.Height <= 0)
        {
            return input;
        }

        int inputWidth = input.Width;
        int height = input.Height;
        int outputWidth = inputWidth + (amplitudeInPixels * 2);

        Sprite output = new(
            new PixelBuffer(outputWidth, height),
            input.OffsetX - amplitudeInPixels,
            input.OffsetY,
            new byte[outputWidth * height]);

        // The rows above and below the wave stay transparent.
        output.Reset();

        ReadOnlySpan<byte> inputImage = input.Image.AsSpan();
        ReadOnlySpan<byte> inputTransform = input.Transform;
        Span<byte> outputImage = output.Image.AsSpan();
        Span<byte> outputTransform = output.Transform;

        for (int y = 0; y < height; ++y)
        {
            double sine = Math.Sin((2.0 * Math.PI * y / periodInPixels) + phaseAtImageTop);
            int offset = (int)Math.Round(amplitudeInPixels * (sine + 1.0), MidpointRounding.AwayFromZero);

            inputImage.Slice(y * inputWidth, inputWidth).CopyTo(outputImage.Slice((y * outputWidth) + offset, inputWidth));
            inputTransform.Slice(y * inputWidth, inputWidth).CopyTo(outputTransform.Slice((y * outputWidth) + offset, inputWidth));
        }

        return output;
    }
}
