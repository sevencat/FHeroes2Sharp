using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Agg;

/// <summary>
/// Computes the 32 bit file name hash used by the Heroes II AGG container.
/// Byte-exact port of <c>calculateAggFilenameHash</c> from <c>src/engine/agg_file.cpp</c>.
/// </summary>
public static class AggFileNameHash
{
    /// <summary>Maximum number of characters a name may occupy in the AGG name table.</summary>
    public const int MaxFileNameLength = LittleEndianReader.AggFileNameLength;

    public static uint Compute(ReadOnlySpan<char> fileName)
    {
        uint hash = 0;
        uint sum = 0;

        for (int i = fileName.Length - 1; i >= 0; --i)
        {
            uint character = ToUpperAscii(fileName[i]);

            hash = unchecked((hash << 5) + (hash >> 25));
            sum = unchecked(sum + character);
            hash = unchecked(hash + sum + character);
        }

        return hash;
    }

    /// <summary>Same computation for names that are still stored as raw ASCII bytes.</summary>
    public static uint ComputeAscii(ReadOnlySpan<byte> fileName)
    {
        uint hash = 0;
        uint sum = 0;

        for (int i = fileName.Length - 1; i >= 0; --i)
        {
            uint character = ToUpperAscii((char)fileName[i]);

            hash = unchecked((hash << 5) + (hash >> 25));
            sum = unchecked(sum + character);
            hash = unchecked(hash + sum + character);
        }

        return hash;
    }

    private static uint ToUpperAscii(char value) => value switch
    {
        >= 'a' and <= 'z' => (uint)(value - 'a' + 'A'),
        _ => value,
    };
}
