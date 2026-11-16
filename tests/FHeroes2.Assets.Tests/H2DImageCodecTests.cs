using System.Buffers.Binary;
using FHeroes2.Assets.H2D;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests for the sprite body of an H2D entry: the 17 byte header, the exact length check and the transform layer that
/// the single layer flag turns off.
/// </summary>
public sealed class H2DImageCodecTests
{
    [Fact]
    public void TryDecode_ReadsThePixelsAndTheTransform()
    {
        Sprite original = BuildSprite(2, 2, 3, -4, withTransform: true);

        Assert.True(H2DImageCodec.TryDecode(BuildBody(original), out Sprite? image));

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(3, image.OffsetX);
        Assert.Equal(-4, image.OffsetY);
        Assert.True(image.HasTransformLayer);
        Assert.Equal(original.Image.Pixels, image.Image.Pixels);
        Assert.Equal(original.Transform, image.Transform);
    }

    [Fact]
    public void TryDecode_WithoutATransformLayer()
    {
        Sprite original = BuildSprite(2, 2, 0, 0, withTransform: false);

        Assert.True(H2DImageCodec.TryDecode(BuildBody(original), out Sprite? image));

        Assert.False(image.HasTransformLayer);
        Assert.Equal(original.Image.Pixels, image.Image.Pixels);
    }

    [Fact]
    public void TryDecode_RejectsATruncatedBody()
    {
        Sprite original = BuildSprite(2, 2, 0, 0, withTransform: true);
        byte[] body = BuildBody(original);

        Assert.False(H2DImageCodec.TryDecode(body[..^1], out _));
    }

    [Fact]
    public void TryDecode_RejectsAnOversizedBody()
    {
        Sprite original = BuildSprite(2, 2, 0, 0, withTransform: false);

        Assert.False(H2DImageCodec.TryDecode([.. BuildBody(original), 0], out _));
    }

    [Fact]
    public void TryDecode_RejectsNonPositiveDimensions()
    {
        byte[] body = new byte[18];
        BinaryPrimitives.WriteInt32LittleEndian(body, 0);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(4), 4);

        Assert.False(H2DImageCodec.TryDecode(body, out _));

        BinaryPrimitives.WriteInt32LittleEndian(body, -1);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(4), -1);

        Assert.False(H2DImageCodec.TryDecode(body, out _));
    }

    [Fact]
    public void TryDecode_RejectsAnEmptyBody()
    {
        Assert.False(H2DImageCodec.TryDecode([], out _));
        Assert.False(H2DImageCodec.TryDecode(new byte[H2DImageCodec.HeaderLength], out _));
    }

    [Fact]
    public void Add_RoundTripsThroughTheArchive()
    {
        Sprite original = BuildSprite(3, 2, -7, 5, withTransform: true);

        H2DWriter writer = new();
        Assert.True(H2DImageCodec.Add(writer, "sprite.image", original));

        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}.h2d");
        try
        {
            Assert.True(writer.Write(path));

            using H2DArchive archive = H2DArchive.Open(path);
            Assert.True(H2DImageCodec.TryRead(archive, "sprite.image", out Sprite? image));

            Assert.Equal(original.Width, image.Width);
            Assert.Equal(original.Height, image.Height);
            Assert.Equal(original.OffsetX, image.OffsetX);
            Assert.Equal(original.OffsetY, image.OffsetY);
            Assert.Equal(original.Image.Pixels, image.Image.Pixels);
            Assert.Equal(original.Transform, image.Transform);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Add_MarksASingleLayerSprite()
    {
        H2DWriter writer = new();
        H2DImageCodec.Add(writer, "flat.image", BuildSprite(1, 1, 0, 0, withTransform: false));

        using H2DArchive archive = H2DArchive.Open(WriteTempArchive(writer.Serialize()!));

        byte[] body = archive.Read("flat.image");
        Assert.Equal(H2DImageCodec.HeaderLength + 1, body.Length);
        Assert.Equal(1, body[H2DImageCodec.HeaderLength - 1]);
    }

    [Fact]
    public void Add_RejectsAnEmptySprite()
    {
        H2DWriter writer = new();

        Assert.False(H2DImageCodec.Add(writer, "empty.image", new Sprite(0, 0)));
        Assert.Empty(writer.FileNames);
    }

    [Fact]
    public void TryRead_ReturnsFalseWhenTheEntryIsMissing()
    {
        using H2DArchive archive = H2DArchive.Open(
            WriteTempArchive(H2DFileBuilder.Build(("a.image", "", H2DFileBuilder.Compress([1])))));

        Assert.False(H2DImageCodec.TryRead(archive, "b.image", out Sprite? image));
        Assert.Null(image);
        Assert.Throws<BinaryFormatException>(() => H2DImageCodec.Read(archive, "b.image"));
    }

    private static Sprite BuildSprite(int width, int height, int offsetX, int offsetY, bool withTransform)
    {
        PixelBuffer buffer = new(width, height);
        for (int index = 0; index < buffer.Pixels.Length; ++index)
        {
            buffer.Pixels[index] = (byte)(index + 1);
        }

        byte[]? transform = null;
        if (withTransform)
        {
            transform = new byte[width * height];
            for (int index = 0; index < transform.Length; ++index)
            {
                transform[index] = (byte)(index % 2);
            }
        }

        return new Sprite(buffer, offsetX, offsetY, transform);
    }

    /// <summary>Serialises a sprite exactly the way <c>writeImageToH2D</c> does.</summary>
    private static byte[] BuildBody(Sprite image)
    {
        int pixelCount = image.Width * image.Height;
        byte[] body = new byte[H2DImageCodec.HeaderLength + (image.HasTransformLayer ? pixelCount * 2 : pixelCount)];

        BinaryPrimitives.WriteInt32LittleEndian(body, image.Width);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(4), image.Height);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(8), image.OffsetX);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(12), image.OffsetY);
        body[H2DImageCodec.HeaderLength - 1] = (byte)(image.HasTransformLayer ? 0 : 1);

        image.Image.Pixels.AsSpan(0, pixelCount).CopyTo(body.AsSpan(H2DImageCodec.HeaderLength));

        if (image.Transform is byte[] transform)
        {
            transform.AsSpan(0, pixelCount).CopyTo(body.AsSpan(H2DImageCodec.HeaderLength + pixelCount));
        }

        return body;
    }

    private static string WriteTempArchive(byte[] content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-test-{Guid.NewGuid():N}.h2d");
        File.WriteAllBytes(path, content);
        return path;
    }
}
