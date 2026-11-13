using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Unit tests for the ICN RLE decoder. Every payload is hand assembled from the control bytes
/// documented in <see cref="IcnSpriteDecoder"/>, so the tests pin the reference behaviour without
/// depending on the game data.
/// </summary>
public sealed class IcnSpriteDecoderTests
{
    [Fact]
    public void Decode_LiteralRun_FillsTheImageLayer()
    {
        Sprite sprite = Decode([0x02, 10, 11, 0x00, 0xC2, 12, 0x80], 2, 2);

        Assert.Equal(new byte[] { 10, 11, 12, 12 }, sprite.Image.Pixels);
    }

    [Fact]
    public void Decode_FullyCoveredSprite_HasNoTransformLayer()
    {
        Sprite sprite = Decode([0x02, 10, 11, 0x00, 0xC2, 12, 0x80], 2, 2);

        Assert.False(sprite.HasTransformLayer);
    }

    [Fact]
    public void Decode_TransparentRun_KeepsTheTransformLayer()
    {
        // 0x81 skips one pixel, then a single literal pixel follows.
        Sprite sprite = Decode([0x81, 0x01, 7, 0x80], 2, 1);

        Assert.True(sprite.HasTransformLayer);
        Assert.Equal(new byte[] { 1, 0 }, sprite.Transform);
        Assert.Equal(new byte[] { 0, 7 }, sprite.Image.Pixels);
    }

    [Fact]
    public void Decode_RowBreak_LeavesTheRestOfTheRowTransparent()
    {
        // Row 0 gets one pixel, then 0x00 ends it early; row 1 gets one pixel as well.
        Sprite sprite = Decode([0x01, 9, 0x00, 0x01, 8, 0x00, 0x80], 3, 2);

        Assert.Equal(new byte[] { 9, 0, 0, 8, 0, 0 }, sprite.Image.Pixels);
        Assert.Equal(new byte[] { 0, 1, 1, 0, 1, 1 }, sprite.Transform);
    }

    [Fact]
    public void Decode_SingleColorRun_EqualsTheReferenceCountRules()
    {
        // 0xC5 is a run of 5 pixels of a single color, 0xC1 reads the count from the next byte.
        Sprite wide = Decode([0xC5, 0x33, 0x80], 5, 1);
        Sprite explicitCount = Decode([0xC1, 0x03, 0x2A, 0x80], 3, 1);

        Assert.Equal(new byte[] { 0x33, 0x33, 0x33, 0x33, 0x33 }, wide.Image.Pixels);
        Assert.Equal(new byte[] { 0x2A, 0x2A, 0x2A }, explicitCount.Image.Pixels);
    }

    [Fact]
    public void Decode_TransformRun_WritesTheTransformLayer()
    {
        // 0xC0 starts a transform run. 0x51 sets 0x40 (use a transformation) and 0x10 in the
        // 0x3C field, which selects table 6; the two lowest bits hold the pixel count of one.
        Sprite sprite = Decode([0xC0, 0x51, 0x80], 3, 1);

        Assert.Equal(6, sprite.Transform![0]);
        Assert.Equal(1, sprite.Transform[1]);
        Assert.Equal(1, sprite.Transform[2]);
    }

    [Fact]
    public void Decode_TransformRun_WithExplicitCount()
    {
        // Bits 0x3C == 0 select the strongest shadow (id 2), the count comes from the next byte.
        Sprite sprite = Decode([0xC0, 0x40, 0x03, 0x80], 3, 1);

        Assert.Equal(new byte[] { 2, 2, 2 }, sprite.Transform);
    }

    [Fact]
    public void Decode_MonochromeSprite_OnlyWritesTheTransformLayer()
    {
        // AnimationFrames bit 0x20 marks a monochromatic sprite: two black pixels, then a row break.
        Sprite sprite = Decode([0x02, 0x00, 0x84, 0x00, 0x80], 4, 2, animationFrames: 0x20);

        Assert.True(sprite.HasTransformLayer);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, sprite.Image.Pixels);
        Assert.Equal(new byte[] { 0, 0, 1, 1, 1, 1, 1, 1 }, sprite.Transform);
    }

    [Fact]
    public void Decode_TruncatedLiteralRun_StopsWithoutThrowing()
    {
        Sprite sprite = Decode([0x05, 0x01], 2, 1);

        Assert.Equal(2, sprite.Width);
        Assert.Equal(new byte[] { 0, 0 }, sprite.Image.Pixels);
    }

    [Fact]
    public void Decode_KeepsTheHeaderOffsets()
    {
        IcnHeader header = new(-3, 4, 1, 1, 0, 0);

        Sprite sprite = IcnSpriteDecoder.Decode([0x01, 5, 0x80], header);

        Assert.Equal(-3, sprite.OffsetX);
        Assert.Equal(4, sprite.OffsetY);
        Assert.Equal(5, sprite.Image[0, 0]);
    }

    private static Sprite Decode(byte[] payload, int width, int height, byte animationFrames = 0)
    {
        IcnHeader header = new(0, 0, (ushort)width, (ushort)height, animationFrames, 0);

        return IcnSpriteDecoder.Decode(payload, header);
    }
}
