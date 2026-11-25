namespace FHeroes2.Assets.Video;

/// <summary>
/// A least-significant-bit-first bit reader over a Smacker chunk, <c>smk_bit_t</c> of libsmacker. The tree and audio
/// chunks are packed this way, unlike the most-significant-bit-first PCX family.
/// </summary>
internal ref struct SmkBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;
    private int _bit;

    public SmkBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
        _bit = 0;
    }

    /// <summary>Reads one bit, or <c>-1</c> when the chunk is exhausted.</summary>
    public int ReadBit()
    {
        if (_position >= _data.Length)
        {
            return -1;
        }

        int value = (_data[_position] >> _bit) & 0x01;

        if (_bit >= 7)
        {
            ++_position;
            _bit = 0;
        }
        else
        {
            ++_bit;
        }

        return value;
    }

    /// <summary>Reads eight bits, or <c>-1</c> when the chunk is exhausted.</summary>
    public int ReadByte()
    {
        if (_bit == 0)
        {
            return _position >= _data.Length ? -1 : _data[_position++];
        }

        if (_position + 1 >= _data.Length)
        {
            return -1;
        }

        int value = _data[_position] >> _bit;
        ++_position;
        value |= (_data[_position] << (8 - _bit)) & 0xFF;

        return value;
    }
}

/// <summary>The 8 bit Huffman tree, <c>smk_huff8_t</c>: 511 nodes, a branch marked with the top bit.</summary>
internal sealed class SmkHuff8
{
    private const int MaximumSize = 511;
    private const ushort Branch = 0x8000;
    private const ushort LeafMask = 0x7FFF;

    private readonly ushort[] _tree = new ushort[MaximumSize];

    private int _size;

    /// <summary>Builds the tree from the bit stream, <c>smk_huff8_build()</c>.</summary>
    public bool Build(ref SmkBitReader reader)
    {
        int bit = reader.ReadBit();

        if (bit < 0)
        {
            return false;
        }

        _size = 0;
        Array.Clear(_tree);

        if (bit > 0)
        {
            if (!BuildRecursive(ref reader))
            {
                return false;
            }
        }
        else
        {
            _tree[0] = 0;
        }

        // A tree ends with an unset bit.
        return reader.ReadBit() == 0;
    }

    /// <summary>Walks the tree for the next eight bit value, or <c>-1</c> when the stream runs out.</summary>
    public int Lookup(ref SmkBitReader reader)
    {
        int index = 0;

        while ((_tree[index] & Branch) != 0)
        {
            int bit = reader.ReadBit();

            if (bit < 0)
            {
                return -1;
            }

            index = bit > 0 ? _tree[index] & LeafMask : index + 1;
        }

        return _tree[index];
    }

    private bool BuildRecursive(ref SmkBitReader reader)
    {
        if (_size >= MaximumSize)
        {
            return false;
        }

        int bit = reader.ReadBit();

        if (bit < 0)
        {
            return false;
        }

        if (bit > 0)
        {
            int left = _size++;

            if (!BuildRecursive(ref reader))
            {
                return false;
            }

            _tree[left] = (ushort)(Branch | _size);

            return BuildRecursive(ref reader);
        }

        int value = reader.ReadByte();

        if (value < 0)
        {
            return false;
        }

        _tree[_size++] = (ushort)value;

        return true;
    }
}

/// <summary>
/// The 16 bit Huffman tree, <c>smk_huff16_t</c>. Its leaves hold a value built from a low and a high byte tree plus a
/// cache of the three most recently used values.
/// </summary>
internal sealed class SmkHuff16
{
    private const uint Branch = 0x80000000;
    private const uint Cache = 0x40000000;
    private const uint LeafMask = 0x3FFFFFFF;

    private uint[] _tree = [0];
    private readonly ushort[] _cache = new ushort[3];

    private int _size;

    /// <summary>Builds the tree from the bit stream, <c>smk_huff16_build()</c>.</summary>
    /// <param name="reader">The bit stream of the Huffman tree chunk.</param>
    /// <param name="allocSize">The allocated size of the tree from the file header.</param>
    public bool Build(ref SmkBitReader reader, int allocSize)
    {
        int bit = reader.ReadBit();

        if (bit < 0)
        {
            return false;
        }

        _size = 0;
        Array.Clear(_cache);

        if (bit == 0)
        {
            _tree = [0];
            Array.Clear(_cache);

            return true;
        }

        SmkHuff8 low = new();
        SmkHuff8 high = new();

        if (!low.Build(ref reader) || !high.Build(ref reader))
        {
            return false;
        }

        for (int i = 0; i < 3; ++i)
        {
            int value = reader.ReadByte();

            if (value < 0)
            {
                return false;
            }

            _cache[i] = (ushort)value;

            value = reader.ReadByte();

            if (value < 0)
            {
                return false;
            }

            _cache[i] |= (ushort)(value << 8);
        }

        if (allocSize < 12 || allocSize % 4 != 0)
        {
            return false;
        }

        int limit = (allocSize - 12) / 4;

        _tree = new uint[limit];

        if (!BuildRecursive(ref reader, low, high, limit))
        {
            return false;
        }

        // The tree ends with an unset bit, like the 8 bit ones.
        return reader.ReadBit() == 0;
    }

    /// <summary>Walks the tree for the next sixteen bit value and refreshes the recent-value cache.</summary>
    public int Lookup(ref SmkBitReader reader)
    {
        int index = 0;

        while ((_tree[index] & Branch) != 0)
        {
            int bit = reader.ReadBit();

            if (bit < 0)
            {
                return -1;
            }

            index = bit > 0 ? (int)(_tree[index] & LeafMask) : index + 1;
        }

        uint value = _tree[index];

        if ((value & Cache) != 0)
        {
            value = _cache[value & LeafMask];
        }

        if (_cache[0] != value)
        {
            _cache[2] = _cache[1];
            _cache[1] = _cache[0];
            _cache[0] = (ushort)value;
        }

        return (int)value;
    }

    private bool BuildRecursive(ref SmkBitReader reader, SmkHuff8 low, SmkHuff8 high, int limit)
    {
        if (_size >= limit)
        {
            return false;
        }

        int bit = reader.ReadBit();

        if (bit < 0)
        {
            return false;
        }

        if (bit > 0)
        {
            int left = _size++;

            if (!BuildRecursive(ref reader, low, high, limit))
            {
                return false;
            }

            _tree[left] = Branch | (uint)_size;

            return BuildRecursive(ref reader, low, high, limit);
        }

        int value = low.Lookup(ref reader);

        if (value < 0)
        {
            return false;
        }

        _tree[_size] = (uint)value;

        value = high.Lookup(ref reader);

        if (value < 0)
        {
            return false;
        }

        _tree[_size] |= (uint)(value << 8);

        // Leaves that equal one of the three recent values are stored as a reference to the cache slot.
        if (_tree[_size] == _cache[0])
        {
            _tree[_size] = Cache;
        }
        else if (_tree[_size] == _cache[1])
        {
            _tree[_size] = Cache | 1;
        }
        else if (_tree[_size] == _cache[2])
        {
            _tree[_size] = Cache | 2;
        }

        ++_size;

        return true;
    }
}
