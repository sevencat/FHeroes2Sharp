using System;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The recolouring palettes of the reference engine: every table maps a palette index
/// onto another one, so applying it to an indexed image swaps its colours without
/// touching the geometry.
/// </summary>
/// <remarks>
/// <para>
/// The tables are the ones <c>fheroes2</c> keeps in <c>src/engine/pal.cpp</c>
/// (<c>PAL::GetPalette</c>). They are what makes text yellow or gray - the bitmap font is
/// always the same <c>FONT.ICN</c> sprite table, only the palette changes - and they
/// produce disabled buttons, the petrified unit colour, the red Blood Lust flash, the
/// purple random object variants and the Evil interface out of the Good one.
/// </para>
/// <para>
/// <see cref="PaletteId.Standard"/> is the identity table <c>PAL::GetPalette</c> builds at
/// run time; the other entries are verbatim copies of the literals. The data is stored as
/// Base64 so that a diff of this file stays reviewable. Regenerate it with
/// <c>tools/scripts/GeneratePaletteTable.ps1</c>; <c>PaletteTableTests</c> pins the SHA-256
/// of the decoded data.
/// </para>
/// </remarks>
public static class PaletteTable
{
    /// <summary>Number of palette entries a table converts.</summary>
    public const int Size = 256;

    /// <summary>Number of tables, one per <see cref="PaletteId"/> value.</summary>
    public const int Count = 14;

    private const string EncodedData =
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElK" +
        "S0xNTk9QUVJTVFVWV1hZWltcXV5fYGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6e3x9fn+AgYKDhIWGh4iJiouMjY6PkJGSk5SV" +
        "lpeYmZqbnJ2en6ChoqOkpaanqKmqq6ytrq+wsbKztLW2t7i5uru8vb6/wMHCw8TFxsfIycrLzM3Oz9DR0tPU1dbX2Nna29zd3t/g" +
        "4eLj5OXm5+jp6uvs7e7v8PHy8/T19vf4+fr7/P3+/wAAAAAAAAAAAAByc3N0dXV2d3d4eXl6e3t8fX1+f3+AgYGCgoIAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACgsM" +
        "DQ4PEBESExQVFhcYGRobHB0eHyAhIiMkAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA8QERITFBUWFxgZGhscHR4fICEiIyQkJCQkJAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMXFxcXFxcXFxcW0tri60NHS0tPT1NXVxMXF" +
        "xcXFxcXFxcXFxcW0tLa2uLrQ0dHS0tLT08HU1NXVxMXFxcXFxdDR0tPB1NXFxcXFxcXFxcXFxcXFxcXR0tLT1NTVxMXFxcXFxcXF" +
        "xcXFxcXFxbS0tLS0tra3uLnQvL2+0r/TwcHCw8TFtrjQ0dHS09PU1dXExcXFxcXFxcXF0dHS0tPTwdTV1dXExcXFxcXFxcXFxcW0" +
        "tLS2s7S1tre4ubq7vL2+v8DBwsPExcbHyMnKy8zNzs/Q0dLT1NXW19jZu9O0tNe/wMHC1cXFxcXFxcXFxcXFxdPF09TExcXFxcX6" +
        "+/z9xcUkJCQkJCQkJCQkCgsMDQ4PEBESExQVFhcYGRobHB0eHyAhIiMkCgsMDA0ODxAREhITFBQVFhYXGBkaGhscHR8OEBESFBUW" +
        "GBkaHBwdHh8gICEhISIiEBESFBUWFxgZGhscHh8gICEiIyMkJCQLCwsLCwwMDA0NDg8QERITFBUVFhcYGQwNDhAREhMUFRYYGBka" +
        "GxwdHyAgIQsMDA0ODhAQERISExQVFhcYGRobHR4gCgsLDAwMDQ0ODg8PEBAREhMUFRYXGBoKCgsLCwwMDAwOEBESFBYYEQoMDxMK" +
        "Cg8REhQVFhcZGhsbGBUWGhobJAwSGRMVGBobGBUaEQoMDyQkMioqKioyMjIyNSorKywsLS4uLy8wMTIzNDQ1Njc4OTo8Pj4+PiUm" +
        "JygpKissLS4vMDEyMzQ1Njc4OTo7PD0+LS4vMDIzNDY3ODo6Ozw8PT4+Pj4+Pi8wMTIzNDU2Nzk6Ojw8Pj4+Pj4+Pj4+KyssLCwt" +
        "LS0uLzAwMTM0NTY3ODk6OzwsLC0uLi8wMTIzNDU1Njc4OTo8Pj4rKywsLS0uLy8wMTEyMzQ1Njc4OTo8PSssLi8wMTI0NTY3OTo8" +
        "PD4+Pj4+Pj4+LCwtLS4vMDEyMzU2Nzk6OzY6Pj4qLTE3LzEyMzQ1Nzg6MzQ3OTY5OCwwNj4xMzY5MzQ3NjY6Pj4yKtXV1dXV1dXV" +
        "1dXGxsbGxsbGxsbGxsbGyMnLzM7P0NHS09XV1dXGxsbGxsbGxsbGxsbGxsfIycvMzc/P0dLT1MbGxsbGycvNz9DS0tPU1NXV1dXV" +
        "1dXGxsbHycvMzs/Q0dLT1NXV1dXV1dXV1cbGxsbGxsbGxsbGxsbGxsbGyMnLzM7PxsbGxsbGxsbGyMnLzM7P0NHS1NXVxsbGxsbG" +
        "xsbGxsbGx8nLzM7P0NHT1NXGxsbGxsbGxsbGxsbGxsbGxsfJyszOz8bGxsbGxsbGxsbGxsbHys3GxsbGxsbGxsbGxsjLzM/Q0cnL" +
        "z9HO0dDGxs/VxsnO0MnLz87GxsbG1dUAAQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyAhIiMkJSYnKCkqKywtLi8wMTIz" +
        "NDU2Nzg5Ojs8PT4/QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXp7fH1+" +
        "f4CBgoOEhYaHiImKi4yNjo+QkZKTlJWWl5iZmpucnZ6foKGio6SlpqeoqaqrrK2ur7CxsrO0tba3uLm6u7y9vr/AwcLDxMXGx8jJ" +
        "ysvMzc7P0NHS09TV+vv8/XZ2dnbe3+Dh4uPk5eb29/js+OztRUVFRfLz9PX29/j5+vv8/f7/AAECAwQFBgcICQoKCgoKCwwNDg8Q" +
        "ERITFBUWFxgZGhscHR4fICUlJSUlJicoKSorLC0uLzAxMjM0NTY3ODk6Pz8/Pz9AQUJDREVGR0hJSktMTU5PUFVVVVVVVldYWVpb" +
        "XF1eX2BhYmNkZWZnbGxsbGxtbm9wcXJzdHV2d3h5ent8fX6Dg4ODg4SFhoeIiYqLjI2Oj5CRkpOYmJiYmJmam5ydnp+goaKjpKWm" +
        "p6ipqq+vr6+vsLGys7S1tre4ubq7vL2+v8DBxsbGxsbHyMnKy8zNzs/Q0dbX2Nna29zd3t7f4OHi4+Tl5+jp6uvs7e7v8PHy8vP0" +
        "9vf4+fr7/P3+/wAAAAAAAAAAAAAQERITFBUWFxgZGhscHR4fICEiIyQkJCQkJCQrLC0uLzAxMjM0NTY3ODk6Ozw9Pj4+Pj4+PkRF" +
        "RkdISUpLTE1OT1BRUlNUVFRUVFRbXF1eX2BhYmNkZWZnaGlqa2tra2tra3JzdHV2d3h5ent8fX5/gIGCgoKCgoKCiImKi4yNjo+Q" +
        "kZKTlJWWl5eXl5eXnp+goaKjpKWmp6ipqqusra6urq6urq60tba3uLm6u7y9vr/AwcLDxMXFxcXFxcrLzM3Oz9DR0tPU1dXV1dXW" +
        "19jZ2tvc3eHi4+Tl5ubm5klLTU9RTE5KTE5Q9PX19UlLTVH6+/z9AAAAAQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyAh" +
        "IiMkDg8QERESFxcYGRoaGxwdHR4fICAhIiMjJCQ/QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamts" +
        "bW5vcHFyc3R1dnd4eXp7fH1+f4CBgoOEhYaHiImKi4yNjo+QkZKTlJWWl5iZmpucnZ6foKGio6SlpqeoqaqrrK2ur7CxsrO0tba3" +
        "uLm6u7y9vr/AwcLDxMUUFRYXGBkaGxwdHh8gIiMk1tfY2drb3N3e3+Dh4uPk5ebn6Onq6+zt7u/w8fLz9PX29/j5+vv8/f7/AAEC" +
        "AwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJA4PEBEREhMWFRYXGBkaGhscHR4fIDo7PCMjP0BBQkNERUZHSElKS0xN" +
        "Tk9QUVJTVFVWV1hZWltcXV5fYGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6e3x9fn8fgYKDhIWGh4iJiouMjY6PkJGSk5SVlpeY" +
        "mZqbnJ2en6ChoqOkpaanqKmqq6ytrq+wsbKztLW2t7i5uru8vb6/wMHCw8TFxsfIycrLzM3Oz9DR0tPUH9bX2Nna29zd3t/g4eLj" +
        "5OXm5+jp6uvs7e7v8PHy8/T19vf4+fr7/P3+/5eXl5eXl5eXl5eDhIWFhoeIiImKi4uMjY6Oj5CRkZKTlJWVlpeDhIWGhoeIiYqK" +
        "i4yNjo6PkJGSkpOUlZaWl4OEhYaHiImKi4yNjo+QkZKTlJWWl5eDhIWGh4iJiouMjI2Oj5CRkpOUlZaXl4OEhYaHiImKi4yMjY6P" +
        "kJGSk5SVlpeXg4SFhoeIiYqLjI2Oj5CRkpOUlZaXg4SFhoeIiYqLjIyNjo+QkZKTlJWWl5eDhIWGh4iJiouMjI2Oj5CRkpOUlZaX" +
        "l4OEhYeIiYuMjY+QkZOUlZeOjo6OjIyMjIOFh4mNj5OVl4iIiIiIiIiIiIiIjZCUl4iIiIiOjo6Ol5c=";

    private static readonly byte[] Data = Convert.FromBase64String(EncodedData);

    /// <summary>
    /// All <see cref="Count"/> tables, each one <see cref="Size"/> bytes long, in
    /// <see cref="PaletteId"/> order.
    /// </summary>
    public static ReadOnlySpan<byte> All => Data;

    /// <summary>Returns the table of <paramref name="id"/>.</summary>
    public static ReadOnlySpan<byte> Get(PaletteId id)
    {
        if ((uint) id >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown palette table.");
        }

        return Data.AsSpan((int) id * Size, Size);
    }

    /// <summary>Converts <paramref name="color"/> with the table of <paramref name="id"/>.</summary>
    public static byte Apply(PaletteId id, byte color) => Get(id)[color];

    /// <summary>
    /// Returns <c>first</c> followed by <c>second</c> (<c>PAL::CombinePalettes</c>): the result
    /// maps an index through <paramref name="first"/> and then through
    /// <paramref name="second"/>, which is how a disabled icon becomes both gray and dark.
    /// </summary>
    public static byte[] Combine(PaletteId first, PaletteId second) =>
        Combine(Get(first), Get(second));

    /// <summary>Combines two raw tables, see <see cref="Combine(PaletteId, PaletteId)"/>.</summary>
    /// <exception cref="ArgumentException">A table is not <see cref="Size"/> bytes long.</exception>
    public static byte[] Combine(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.Length != Size)
        {
            throw new ArgumentException($"A table must hold {Size} entries.", nameof(first));
        }

        if (second.Length != Size)
        {
            throw new ArgumentException($"A table must hold {Size} entries.", nameof(second));
        }

        byte[] combined = new byte[Size];
        for (int index = 0; index < Size; ++index)
        {
            combined[index] = second[first[index]];
        }

        return combined;
    }
}
