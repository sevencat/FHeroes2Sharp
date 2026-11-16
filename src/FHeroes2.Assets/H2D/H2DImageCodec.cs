using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.H2D;

/// <summary>
/// Sprite bodies of an H2D archive, <c>readImageFromH2D</c> and <c>writeImageToH2D</c> of
/// <c>src/engine/h2d_file.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The body of an image is a small header followed by the pixels and, when the image keeps a second layer, the
/// transparency of every pixel:
/// </para>
/// <code>
///   int32 width, int32 height   pixels
///   int32 x, int32 y            offsets, what <c>setPosition</c> applies later
///   uint8 singleLayer           1 when the transform layer is not stored
///   width * height bytes        the image
///   width * height bytes        the transform, only when singleLayer is 0
/// </code>
/// <para>
/// The length has to match exactly, so a truncated or oversized body is rejected instead of being half applied — the
/// reference compares the same expression with the size of the entry.
/// </para>
/// </remarks>
public static class H2DImageCodec
{
    /// <summary>Bytes before the pixels: width, height, x, y and the single layer flag.</summary>
    public const int HeaderLength = 4 + 4 + 4 + 4 + 1;

    /// <summary>Reads one sprite out of an archive, <c>readImageFromH2D</c>.</summary>
    /// <param name="archive">The archive to read from.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="image">The sprite that was built.</param>
    /// <returns>False when the entry is missing, too short or its header does not describe the body.</returns>
    public static bool TryRead(H2DArchive archive, string name, [NotNullWhen(true)] out Sprite? image)
    {
        ArgumentNullException.ThrowIfNull(archive);

        return TryDecode(archive.TryRead(name), out image);
    }

    /// <summary>Decodes one decompressed body, the second half of <c>readImageFromH2D</c>.</summary>
    /// <param name="data">The body, or <see langword="null"/> when the entry is missing.</param>
    /// <param name="image">The sprite that was built.</param>
    /// <returns>False when the body is too short or its header does not describe it.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> data, [NotNullWhen(true)] out Sprite? image)
    {
        image = null;

        if (data.Length <= HeaderLength)
        {
            return false;
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(data);
        int height = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        int x = BinaryPrimitives.ReadInt32LittleEndian(data[8..]);
        int y = BinaryPrimitives.ReadInt32LittleEndian(data[12..]);
        bool isSingleLayer = data[16] != 0;

        // The reference multiplies the raw values as size_t, so a negative dimension turns into a size that cannot
        // match the body either; rejecting it here keeps the same outcome without the overflow.
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        int pixelCount = checked(width * height);
        if (data.Length != HeaderLength + (isSingleLayer ? pixelCount : pixelCount * 2))
        {
            return false;
        }

        PixelBuffer buffer = new(width, height);
        data.Slice(HeaderLength, pixelCount).CopyTo(buffer.Pixels);

        byte[]? transform = null;
        if (!isSingleLayer)
        {
            transform = new byte[pixelCount];
            data.Slice(HeaderLength + pixelCount, pixelCount).CopyTo(transform);
        }

        image = new Sprite(buffer, x, y, transform);
        return true;
    }

    /// <summary>Reads one sprite and throws when the entry cannot be decoded.</summary>
    /// <param name="archive">The archive to read from.</param>
    /// <param name="name">The entry name.</param>
    /// <returns>The sprite.</returns>
    /// <exception cref="BinaryFormatException">The entry is missing or does not hold a sprite.</exception>
    public static Sprite Read(H2DArchive archive, string name) =>
        TryRead(archive, name, out Sprite? image)
            ? image
            : throw new BinaryFormatException($"Entry '{name}' of '{archive.Path}' does not hold a readable sprite.");

    /// <summary>Adds one sprite, <c>writeImageToH2D</c>.</summary>
    /// <param name="writer">The archive being built.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="image">The sprite to store; must not be empty.</param>
    /// <param name="extraInfo">Free form note stored next to the name.</param>
    /// <returns>False when the sprite is empty, which the reference asserts against.</returns>
    public static bool Add(H2DWriter writer, string name, Sprite image, string extraInfo = "")
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(image);

        if (image.Width == 0 || image.Height == 0)
        {
            return false;
        }

        int pixelCount = image.Width * image.Height;
        bool isSingleLayer = !image.HasTransformLayer;

        using MemoryStream body = new();
        using (LittleEndianWriter littleEndian = new(body, leaveOpen: true))
        {
            littleEndian.WriteInt32(image.Width);
            littleEndian.WriteInt32(image.Height);
            littleEndian.WriteInt32(image.OffsetX);
            littleEndian.WriteInt32(image.OffsetY);
            littleEndian.WriteByte((byte)(isSingleLayer ? 1 : 0));
            littleEndian.WriteBytes(image.Image.Pixels.AsSpan(0, pixelCount));

            if (!isSingleLayer && image.Transform is byte[] transform)
            {
                littleEndian.WriteBytes(transform.AsSpan(0, pixelCount));
            }
        }

        return writer.Add(name, body.GetBuffer().AsSpan(0, checked((int)body.Length)), extraInfo);
    }
}
