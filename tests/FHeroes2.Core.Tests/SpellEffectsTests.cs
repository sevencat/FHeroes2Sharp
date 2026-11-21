using FHeroes2.Core.Imaging;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <c>CreateDeathWaveEffect()</c> (<c>ui_tool.cpp:485-543</c>), <c>CreateHolyShoutEffect()</c>
/// (<c>:545-619</c>) and <c>createRippleEffect()</c> (<c>:621-651</c>).
/// </summary>
public sealed class SpellEffectsTests
{
    /// <summary>Every column is shifted down by the curve and the surplus rows are dropped.</summary>
    /// <remarks>
    /// 参考第一段循环把曲线以上的像素整体下移 <c>-curve</c> 行（越出下边界的丢掉），
    /// 第二段把曲线以下的 <c>-curve</c> 行按相反顺序填回去。
    /// </remarks>
    [Fact]
    public void CreateDeathWaveEffect_ShiftsColumnsDownByTheCurve()
    {
        PixelBuffer input = BuildGradient(4, 4);

        PixelBuffer output = SpellEffects.CreateDeathWaveEffect(input, 2, new[] { -1, -1 });

        Assert.Equal(2, output.Width);
        Assert.Equal(4, output.Height);

        // Column x keeps the source column x, rows 1..3 move up one row and the last row repeats.
        Assert.Equal(new byte[] { 10, 20, 30, 30 }, Column(output, 0));
        Assert.Equal(new byte[] { 11, 21, 31, 31 }, Column(output, 1));
    }

    /// <summary>A deeper curve also mirrors the rows under the wave.</summary>
    [Fact]
    public void CreateDeathWaveEffect_FlipsBelowTheCurve()
    {
        PixelBuffer input = BuildGradient(4, 4);

        PixelBuffer output = SpellEffects.CreateDeathWaveEffect(input, 1, new[] { -2 });

        Assert.Equal(1, output.Width);
        Assert.Equal(new byte[] { 20, 30, 30, 20 }, Column(output, 0));
    }

    /// <summary>A wave that is completely outside the input draws nothing.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void CreateDeathWaveEffect_ReturnsEmptyWhenOutside(int position)
    {
        PixelBuffer input = BuildGradient(4, 4);

        Assert.True(SpellEffects.CreateDeathWaveEffect(input, position, new[] { -1 }).IsEmpty);
    }

    /// <summary>At the very left edge the wave has no columns yet, and an empty curve draws nothing.</summary>
    [Fact]
    public void CreateDeathWaveEffect_ReturnsEmptyWithoutColumns()
    {
        PixelBuffer input = BuildGradient(4, 4);

        Assert.True(SpellEffects.CreateDeathWaveEffect(input, 0, new[] { -1 }).IsEmpty);
        Assert.True(SpellEffects.CreateDeathWaveEffect(input, 2, Array.Empty<int>()).IsEmpty);
    }

    /// <summary>A single colour image survives the cross average, the nearest colour is the same entry.</summary>
    [Fact]
    public void CreateHolyShoutEffect_KeepsAUniformImage()
    {
        Palette palette = BuildPalette((20, 160, 80, 80));
        PixelBuffer input = BuildUniform(3, 3, 20);

        PixelBuffer output = SpellEffects.CreateHolyShoutEffect(input, 1, 0, palette);

        Assert.Equal(input.Size, output.Size);
        for (int y = 0; y < output.Height; ++y)
        {
            for (int x = 0; x < output.Width; ++x)
            {
                Assert.Equal(20, output[x, y]);
            }
        }
    }

    /// <summary>The average is taken over a cross, so bright pixels only spread horizontally and vertically.</summary>
    [Fact]
    public void CreateHolyShoutEffect_BlursAlongTheCross()
    {
        Palette palette = BuildPalette((20, 160, 80, 80));
        PixelBuffer input = new(3, 3);
        input[1, 1] = 20;

        PixelBuffer output = SpellEffects.CreateHolyShoutEffect(input, 1, 0, palette);

        // The 6 bit colour of entry 20 is (40, 20, 20); three samples average to (40, 20, 20) / 3 per channel.
        byte expected = PALColorId.Get(13, 6, 6, palette);
        byte black = PALColorId.Get(0, 0, 0, palette);

        Assert.Equal(expected, output[1, 0]);
        Assert.Equal(expected, output[1, 1]);
        Assert.Equal(expected, output[1, 2]);
        Assert.Equal(black, output[0, 0]);
        Assert.Equal(black, output[2, 2]);

        // The corners of the image keep their black pixels: the cross never reaches them.
        Assert.Equal(black, output[2, 0]);
        Assert.Equal(black, output[0, 2]);
    }

    /// <summary>The dark red coefficients pull the red channel up and the green/blue ones down.</summary>
    /// <remarks>
    /// 入口色是 6 位灰 <c>(40, 40, 40)</c>（索引 20）。强度 200 时
    /// <c>redCoeff = 4 - 200 / 280 ≈ 3.2857</c>、<c>greenBlueCoeff = 4 - 200 / 80 = 1.5</c>，
    /// 于是通道变成 <c>(131 / 4, 60 / 4, 60 / 4) = (32, 15, 15)</c>，正好是索引 21 的颜色。
    /// </remarks>
    [Fact]
    public void CreateHolyShoutEffect_PushesToDarkRed()
    {
        Palette palette = BuildPalette((20, 160, 160, 160), (21, 128, 60, 60));
        PixelBuffer input = BuildUniform(3, 3, 20);

        Assert.Equal(20, SpellEffects.CreateHolyShoutEffect(input, 1, 0, palette)[1, 1]);
        Assert.Equal(21, SpellEffects.CreateHolyShoutEffect(input, 1, 200, palette)[1, 1]);
    }

    /// <summary>A radius below one returns the input, the reference returns the image by value.</summary>
    [Fact]
    public void CreateHolyShoutEffect_ReturnsACopyWhenTheRadiusIsBelowOne()
    {
        Palette palette = BuildPalette();
        PixelBuffer input = BuildGradient(4, 4);

        PixelBuffer output = SpellEffects.CreateHolyShoutEffect(input, 0, 100, palette);

        Assert.NotSame(input, output);
        Assert.Equal(input.Pixels, output.Pixels);

        output[0, 0] = 99;
        Assert.NotEqual(99, input[0, 0]);
    }

    /// <summary>Every row is moved by the sine of its position, both layers move together.</summary>
    [Fact]
    public void CreateRippleEffect_ShiftsEachRowByTheSine()
    {
        // period 4 and phase 0 give offsets 1, 2, 1 for the three rows of a one pixel amplitude.
        Sprite input = BuildSprite(2, 3, transform: 7);

        Sprite output = SpellEffects.CreateRippleEffect(input, 1, 0, 4);

        Assert.Equal(4, output.Width);
        Assert.Equal(3, output.Height);

        Assert.Equal(new byte[] { 0, 0, 1, 0 }, ImageRow(output, 0));
        Assert.Equal(new byte[] { 0, 0, 10, 11 }, ImageRow(output, 1));
        Assert.Equal(new byte[] { 0, 20, 21, 0 }, ImageRow(output, 2));

        // The pixels the wave left empty keep transform 1, i.e. "leave the destination alone".
        Assert.Equal(new byte[] { 1, 7, 7, 1 }, TransformRow(output, 0));
        Assert.Equal(new byte[] { 1, 1, 7, 7 }, TransformRow(output, 1));
        Assert.Equal(new byte[] { 1, 7, 7, 1 }, TransformRow(output, 2));
    }

    /// <summary>The sprite grows by two amplitudes and the anchor moves left by one.</summary>
    [Fact]
    public void CreateRippleEffect_WidensAndMovesTheAnchor()
    {
        Sprite input = BuildSprite(3, 2, transform: 7, offsetX: 5, offsetY: 6);

        Sprite output = SpellEffects.CreateRippleEffect(input, 2, 0, 16);

        Assert.Equal(input.Width + 4, output.Width);
        Assert.Equal(input.Height, output.Height);
        Assert.Equal(3, output.OffsetX);
        Assert.Equal(6, output.OffsetY);
    }

    /// <summary>Without a transform layer and without an amplitude the input is returned as is.</summary>
    [Fact]
    public void CreateRippleEffect_ReturnsTheInputWhenNothingChanges()
    {
        Sprite singleLayer = new(new PixelBuffer(2, 2));

        Assert.Same(singleLayer, SpellEffects.CreateRippleEffect(singleLayer, 3, 0, 8));

        Sprite withLayer = BuildSprite(2, 2, transform: 7);
        Assert.Same(withLayer, SpellEffects.CreateRippleEffect(withLayer, 0, 0, 8));
    }

    /// <summary>Rows of the input gradient: <c>10 * y + x</c>.</summary>
    private static PixelBuffer BuildGradient(int width, int height)
    {
        PixelBuffer buffer = new(width, height);
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                buffer[x, y] = (byte)((10 * y) + x);
            }
        }

        return buffer;
    }

    private static PixelBuffer BuildUniform(int width, int height, byte color)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill(color);

        return buffer;
    }

    /// <summary>Rows of the sprite image: <c>10 * y + x</c>, every transform byte set to <paramref name="transform"/>.</summary>
    private static Sprite BuildSprite(int width, int height, byte transform, int offsetX = 0, int offsetY = 0)
    {
        byte[] layer = new byte[width * height];
        Array.Fill(layer, transform);

        return new Sprite(BuildGradient(width, height), offsetX, offsetY, layer);
    }

    private static byte[] Column(PixelBuffer image, int x)
    {
        byte[] column = new byte[image.Height];
        for (int y = 0; y < image.Height; ++y)
        {
            column[y] = image[x, y];
        }

        return column;
    }

    private static byte[] ImageRow(Sprite sprite, int y) => sprite.Image.Row(y).ToArray();

    private static byte[] TransformRow(Sprite sprite, int y) => sprite.Transform.AsSpan(y * sprite.Width, sprite.Width).ToArray();

    private static Palette BuildPalette(params (int Id, byte Red, byte Green, byte Blue)[] entries)
    {
        byte[] raw = new byte[PaletteTable.Size * 3];

        foreach ((int id, byte red, byte green, byte blue) in entries)
        {
            raw[id * 3] = red;
            raw[(id * 3) + 1] = green;
            raw[(id * 3) + 2] = blue;
        }

        return new Palette(raw);
    }
}
