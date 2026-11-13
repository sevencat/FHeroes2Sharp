using System.Buffers.Binary;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Til;

/// <summary>
/// A TIL file: a grid of raw indexed tiles used for terrain and the adventure map.
/// The layout follows <c>Assets::GetMaximumTILIndex()</c>
/// (<c>src/fheroes2/game/game_assets.cpp</c>): a 16 bit tile count, a 16 bit width, a 16 bit
/// height and then the uncompressed pixels of every tile.
/// </summary>
public sealed class TilFile
{
    /// <summary>Size of the file header: count, width and height, all 16 bit.</summary>
    public const int HeaderSize = 6;

    private readonly PixelBuffer[] _images;

    private TilFile(PixelBuffer[] images, int width, int height)
    {
        _images = images;
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public int Count => _images.Length;

    /// <summary>The tiles, each one <see cref="Width"/> by <see cref="Height"/> pixels.</summary>
    public IReadOnlyList<PixelBuffer> Images => _images;

    /// <summary>Parses a TIL entry.</summary>
    /// <exception cref="BinaryFormatException">The entry is not a valid TIL.</exception>
    public static TilFile Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new BinaryFormatException(
                $"A TIL file needs at least {HeaderSize} bytes but only {data.Length} were provided.");
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(data);
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);

        if (count < 1 || width < 1 || height < 1)
        {
            throw new BinaryFormatException(
                $"The TIL header is invalid: {count} tiles of {width}x{height} pixels.");
        }

        long expectedSize = HeaderSize + ((long)count * width * height);
        if (expectedSize != data.Length)
        {
            throw new BinaryFormatException(
                $"The TIL file should be {expectedSize} bytes ({count} tiles of {width}x{height}) but is {data.Length} bytes.");
        }

        int tileSize = width * height;
        PixelBuffer[] images = new PixelBuffer[count];

        for (int index = 0; index < count; ++index)
        {
            images[index] = new PixelBuffer(width, height, data.Slice(HeaderSize + (index * tileSize), tileSize).ToArray());
        }

        return new TilFile(images, width, height);
    }
}
