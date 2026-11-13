using System.Buffers.Binary;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Icn;

/// <summary>
/// An ICN file: an array of sprites stored inside an AGG entry. The layout mirrors
/// <c>fheroes2::readIcnFromAgg()</c> (<c>src/fheroes2/game/game_assets.cpp</c>):
/// a 16 bit sprite count, a 32 bit size of the data block, then <c>count</c> 13 byte
/// headers followed by the RLE payloads. Every <c>OffsetData</c> is relative to the end of the
/// 6 byte file header, so the payload of a sprite starts at <c>6 + OffsetData</c>.
/// </summary>
public sealed class IcnSpriteFile
{
    /// <summary>Size of the file header: a 16 bit count and a 32 bit data block size.</summary>
    public const int HeaderSize = 6;

    private readonly IcnHeader[] _headers;
    private readonly Sprite[] _sprites;

    private IcnSpriteFile(IcnHeader[] headers, Sprite[] sprites)
    {
        _headers = headers;
        _sprites = sprites;
    }

    /// <summary>Decoded headers, in file order.</summary>
    public IReadOnlyList<IcnHeader> Headers => _headers;

    /// <summary>Decoded sprites, in file order.</summary>
    public IReadOnlyList<Sprite> Sprites => _sprites;

    public int Count => _sprites.Length;

    /// <summary>
    /// Total size of the data block as stored in the file header. For a well formed file
    /// <c>HeaderSize + DataBlockSize</c> equals the length of the entry.
    /// </summary>
    public int DataBlockSize { get; private init; }

    /// <summary>Parses an ICN entry and decodes every sprite it contains.</summary>
    /// <exception cref="BinaryFormatException">The entry is not a valid ICN.</exception>
    public static IcnSpriteFile Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new BinaryFormatException(
                $"An ICN file needs at least {HeaderSize} bytes but only {data.Length} were provided.");
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(data);
        uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(data[2..]);

        if (count == 0 || blockSize == 0)
        {
            throw new BinaryFormatException("The ICN file is empty: the sprite count or the data block size is zero.");
        }

        long headerTableEnd = HeaderSize + ((long)count * IcnHeader.Size);
        if (headerTableEnd > data.Length)
        {
            throw new BinaryFormatException(
                $"The ICN file claims {count} sprites but {data.Length} bytes cannot even hold the header table.");
        }

        IcnHeader[] headers = new IcnHeader[count];
        Sprite[] sprites = new Sprite[count];

        for (int index = 0; index < count; ++index)
        {
            IcnHeader header = IcnHeader.Read(data.Slice(HeaderSize + (index * IcnHeader.Size), IcnHeader.Size));
            headers[index] = header;

            long payloadSize;
            if (index + 1 != count)
            {
                IcnHeader next = IcnHeader.Read(
                    data.Slice(HeaderSize + ((index + 1) * IcnHeader.Size), IcnHeader.Size));
                payloadSize = (long)next.OffsetData - header.OffsetData;
            }
            else
            {
                payloadSize = (long)blockSize - header.OffsetData;
            }

            if (payloadSize < 0 || HeaderSize + header.OffsetData + payloadSize > data.Length)
            {
                throw new BinaryFormatException(
                    $"Sprite {index} of the ICN file is corrupted: its payload does not fit into the entry.");
            }

            ReadOnlySpan<byte> payload = data.Slice(HeaderSize + (int)header.OffsetData, (int)payloadSize);
            sprites[index] = IcnSpriteDecoder.Decode(payload, header);
        }

        return new IcnSpriteFile(headers, sprites) { DataBlockSize = (int)blockSize };
    }
}
