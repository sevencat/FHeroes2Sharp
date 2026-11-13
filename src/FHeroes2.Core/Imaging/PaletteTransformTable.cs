using System;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The palette conversion tables used by the transform layer of a sprite.
/// </summary>
/// <remarks>
/// <para>
/// A sprite has an optional second byte layer, the <i>transform layer</i>. Each of its
/// entries selects how a pixel is combined with what is already on the destination:
/// <c>0</c> copies the image layer, <c>1</c> skips the pixel and every other value picks one
/// of the 16 tables below to convert the <i>destination</i> color, e.g. a shadow over a tile.
/// </para>
/// <para>
/// The data is a verbatim copy of the <c>transformTable</c> array from fheroes2
/// <c>src/engine/image.cpp</c>. Tables 0 and 1 are zero placeholders that no code references,
/// tables 2 to 5 are the shadow ramp (2 being the strongest, see <c>image.h</c>), tables 6 to 9
/// are the lightening ramp used by spell effects and table 15 maps the cycling color ranges
/// onto their static duplicates so that an image can be drawn without color cycling.
/// </para>
/// <para>
/// The array is stored as Base64 so that a diff of this file stays reviewable. Regenerate it
/// with <c>tools/scripts/GeneratePaletteTransformTable.ps1</c>;
/// <c>PaletteTransformTableTests</c> pins the SHA-256 of the decoded data.
/// </para>
/// </remarks>
public static class PaletteTransformTable
{
    /// <summary>Number of conversion tables.</summary>
    public const int TransformCount = 16;

    /// <summary>Number of palette entries handled by a single table.</summary>
    public const int TransformSize = 256;

    private const string EncodedData =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAEBESExQVFhcYGRobHB0eHyAhIiMkJCQkJCQkKywtLi8wMTIzNDU2Nzg5Ojs8PT4+Pj4+Pj5E" +
        "RUZHSElKS0xNTk9QUVJTVFRUVFRUW1xdXl9gYWJjZGVmZ2hpamtra2tra2tyc3R1dnd4eXp7fH1+f4CBgoKCgoKCgoiJiouM" +
        "jY6PkJGSk5SVlpeXl5eXl56foKGio6SlpqeoqaqrrK2urq6urq6utLW2t7i5uru8vb6/wMHCw8TFxcXFxcXKy8zNzs/Q0dLT" +
        "1NXV1dXV1tfY2drb3N3h4uPk5ebm5uZJS01PUUxOSkxOUPT19fVJS01R+vv8/QAAAAAAAAAAAAAAAA4PEBESExQVFhcYGRob" +
        "HB0eHyAhIiMkJCQkJCkqKywtLi8wMTIzNDU2Nzg5Ojs8PT4+Pj4+QkNERUZHSElKS0xNTk9QUVJTVFRUVFlaW1xdXl9gYWJj" +
        "ZGVmZ2hpamtra2trcHFyc3R1dnd4eXp7fH1+f4CBgoKCgoKGh4iJiouMjY6PkJGSk5SVlpeXl5ecnZ6foKGio6Slpqeoqaqr" +
        "rK2urq6urrKztLW2t7i5uru8vb6/wMHCw8TFxcXFycrLzM3Oz9DR0tPU1dXV1dbX2Nna29zd4OHi4+Tl5ubmTExMTExMTExM" +
        "TE709fX1TExMTPr7/P0AAAAAAAAAAAAAAAAMDQ4PEBESExQVFhcYGRobHB0eHyAhIiMkJCQnKCkqKywtLi8wMTIzNDU2Nzg5" +
        "Ojs8PT4+PkFCQ0RFRkdISUpLTE1OT1BRUlNUVFRXWFlaW1xdXl9gYWJjZGVmZ2hpamtra25vcHFyc3R1dnd4eXp7fH1+f4CB" +
        "goKChYaHiImKi4yNjo+QkZKTlJWWl5eXmpucnZ6foKGio6SlpqeoqaqrrK2urq6xsrO0tba3uLm6u7y9vr/AwcLDxMXFxcjJ" +
        "ysvMzc7P0NHS09TV1dXW19jZ2tvc3d/g4eLj5OXm5kxMTExMTExMTExM8/T19UxMTEz6+/z9AAAAAAAAAAAAAAAACwwNDg8Q" +
        "ERITFBUWFxgZGhscHR4fICEiIyQkJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj5AQUJDREVGR0hJSktMTU5PUFFSU1RUVldY" +
        "WVpbXF1eX2BhYmNkZWZnaGlqa2ttbm9wcXJzdHV2d3h5ent8fX5/gIGCgoSFhoeIiYqLjI2Oj5CRkpOUlZaXl5mam5ydnp+g" +
        "oaKjpKWmp6ipqqusra6usLGys7S1tre4ubq7vL2+v8DBwsPExcXHyMnKy8zNzs/Q0dLT1NXV1tfY2drb3N3f4OHi4+Tl5uZL" +
        "S0tLS0tLS0tLS/P09fVLS0tL+vv8/QAAAAAAAAAAAAAAAAoKCwsLDA0NDQ4ODw8PEBERERISExMUFBQVFQslJSUmJicnJygo" +
        "KSkpKSoqEyoUFBQUFBQVDIM/Pz9AQEBBQUFBQfLy8vLy8vLy8g0ODw8QVRFVVVVVE1YUFBQVFRUVFRUVCmxsbW1tbm5ubsco" +
        "KSkpKSkqKioqFBQLC4ODhISEhYWGhoaHhxKIExMUFBQKCwsLDAwNDQ0ODw8PEBERERISExMUFAuvr7CwJrGxsrKys7Ozs7S0" +
        "tLS0tBUVbGwmbSZtJygoKSkpKioqFMeztLRubigqbm5WVlZWEhITQUFBQkFCQZibQfIPEBETQUFBQceztLQAAAAAAAAAAAAA" +
        "AAAKCwsMDA0NDg8PEBARERITFBQVFRYWFxgYGRklJSYmJycoKSkpKiorKywsLS0uLhcYGBgYGIM/P0BAQUFCQvJDQ0RE8/Pz" +
        "8/Pz8/MPD1VVVVVWVldXWFhYWFkYWhkZGRkZGSVsbW1ubm9vyMjJySorKywsLC0tLi4uC4OEhISFhYaHh4jyiYmK8/Pz8/MY" +
        "mJiZmZqam5ycnZ6enxITExQUFRYWFxglr7CwsbGys7O0tLS1tbW2trYuLy8wGWxtbW3Gx8fJySorKywtLi7Jtba3b2/KLW9v" +
        "V1hYWFgVFkJCRERDRESYnUJFEBIUFUJCREPJtba3AAAAAAAAAAAAAAAACgsLDA0ODg8QERESExQUFRYXGBgZGhobHB0dJSUm" +
        "JygoKSoqKywsLS4uLy8wMDEyMhscHBw/P0BBQUJDQ0RFRUVGRkb0R/T09PT1EFVVVldXWFhZWlpbW1tcXV1dHR0dHR0lbW1u" +
        "b29wcXBwy8vLLC0uLy8vMDAxMoODhIWFhoeIiImJi4uLjY2Nj4/19ZiYmZqbm5ydnp6foKGio6OkpaWmGhobrw2wsbKys7S1" +
        "tba2t7e3uLi5uTIyNDRtbcbHyMnJysosLS4vMDAxzM25uXBwzC9wcVhZW1xdXRlCREVFREVFmZ9ERxLy8xhCREVEzM25uQAA" +
        "AAAAAAAAAAAAAAoLDA0NDg8QERETExQVFhcYGBoaGxwcHh4fICUmJycoKSorKywtLi4vMDEyMjM0NDU2Nh4fP0BAQUJDREVF" +
        "RkdHR0hISElJSaioqFVVVldYWFlaW1tcXV1eX19gYGAfICAgbG3Gbm9wcXFxdHV2d3h5LzAyMjMzNDSDhISFhoeIiYmKi4yN" +
        "jY+PkJGSkx6YmZmam5ydnp6foKGio6SlpaanqKkcHa+wsbGys7S1tra3uLm5ubq6uzI0NDY3bcbHyMnKyszMzc8vMTIzNM7O" +
        "u7xxcXYx3t7f4OHiX+PkQ0RGR0VHRplBRUnyFvP0Q0RGRc7Ou7wAAAAAAAAAAAAAAAAKCwsMDA0ODg8QEBESEhMU8vIWFhfz" +
        "8xn09PQLJSYmJygoKbISExQUFRUWFhYWFhcXFxcY9D8/QEBBQUJCQ0NERERFRUVFRUVGRkYPDxBVVhITExSfFRWhFqOjoxcX" +
        "paX09CVsJm1tbsfIxygpKioqKyssFhYXFxcXg4OEhIWFhoaHh4iIiYmKiouLjI30mJiZmZqam5ucnJ2enp/yn6Gh8/Pz86QL" +
        "r7CwsbGys7O0tLW1tra2trcWFxcXF2wmJicnKCgpsrQqLC0XFxe0tbW3bsgqLVVWV1dXFRYWF0JCQ0RDRESZnkNGQEHy80JC" +
        "Q0O0tbW3nwoAAAAAAAAAAAAACgsLDA0ODg8QEBESExMU8hYW8/Pz9PT09PX1JSUmsCexKSkqsxS0LRYXFxgYGBgZGRkZGho/" +
        "P0BAQUJDQ0RERUVGRkZGRkdHR0dHD1VVVlZXFFhZFqGio6OkpKWlpqanp6clbG1tbsdvb8jJKSsrKywsLi4YGBkZGYODhISF" +
        "hoeHiIiJioqLi4yMjY2Oj5iYmZmam5ucnJ2enp+foaGioqOk9PT0r6+wsbGys7O0tbW2tre3t7i4uBkZGRlsbW0nKCkpKior" +
        "Ky0uLxkZtba3uW9vKi5vV1hYWBYXGBlCQ0RFREVFmQBER0Hy8vNCQ0REtba3uQAKAAAAAAAAAAAAAAoLCw0NDg8QEBESExQV" +
        "FRYX8xn09PQc9fX1HyUmJicoKSkqKrQtLi4vLxkaGhoaGxsbGxv1Pz9AQUJCQ0RFRUZGR0dISEhISElJSRBVVVZXWFhaWltb" +
        "o6Skpaamp6eoqamqJWxtxsdvcHDJysorLC0tLi4vMBobGxuDg4SFhoeHiImJiouLjI2Njo+PkJGYmJmam5ucnJ6en6CgoaKj" +
        "pKSlpqanp6+wsLGysrO0tba2t7i4uLm6ujIzGxsbbW1tKCgpKisrLC0uLzEbG7a3uLtwcCsvcFdZWltbGBoaQ0RFRkVGRpkA" +
        "AElB8vP0Q0RFRba3uLsACgAAAAAAAAAAAAAKCwwNDQ4PEBESExQVFRfzGBn09Bv19R+qlZUlJiYnKCkqKiwtLi4vMDEyMxwc" +
        "HB0dHR0dHj9AQUFCQ0RFRkZHSEhJSUpKS0tMTExVVVZXWFlaW1tcXV2mpmCoqKmqqqurqyVtbW7Ib3BxysrLLC0uLi8wMTIz" +
        "NB0dg4SFhYaHiImKi4uMjY6OkJCRkZKTmJmZmpucnZ6fn6ChoqOkpKWmp6ioqKmvsLGxsrO0tba2t7i5urq7u729wcGSkm1t" +
        "xsfJyckszS0uLzAyNB23ubq9cHDNMd5YWVtcXRobHENERkdFR0eaAABL8vLz9ENERkW3ubq9AAoAAQIDBAUGBwgJCgoKCgoL" +
        "DA0ODxAREhMUFRYXGBkaGxwdHh8gJSUlJSUmJygpKissLS4vMDEyMzQ1Njc4OTo/Pz8/P0BBQkNERUZHSElKS0xNTk9QVVVV" +
        "VVVWV1hZWltcXV5fYGFiY2RlZmdsbGxsbG1ub3BxcnN0dXZ3eHl6e3x9foODg4ODhIWGh4iJiouMjY6PkJGSk5iYmJiYmZqb" +
        "nJ2en6ChoqOkpaanqKmqr6+vr6+wsbKztLW2t7i5uru8vb6/wMHGxsbGxsfIycrLzM3Oz9DR1tfY2drb3N3e3t/g4eLj5OXn" +
        "6Onq6+zt7u/w8fLy8/T29/j5+vv8/f7/AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4v" +
        "MDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJTVFVWV1hZWltcXV5fYGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3" +
        "eHl6e3x9fn+AgYKDhIWGh4iJiouMjY6PkJGSk5SVlpeYmZqbnJ2en6ChoqOkpaanqKmqq6ytrq+wsbKztLW2t7i5uru8vb6/" +
        "wMHCw8TFxsfIycrLzM3Oz9DR0tPU1fr7/P12dnZ23t/g4eLj5OXm9vf47Pjs7UVFRUXy8/T19vf4+fr7/P3+/w==";

    private static readonly byte[] Data = Convert.FromBase64String(EncodedData);

    /// <summary>All <see cref="TransformCount"/> tables, each one <see cref="TransformSize"/> bytes long.</summary>
    public static ReadOnlySpan<byte> All => Data;

    /// <summary>Returns the table for <paramref name="transformId"/>.</summary>
    public static ReadOnlySpan<byte> Get(int transformId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(transformId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(transformId, TransformCount);

        return Data.AsSpan(transformId * TransformSize, TransformSize);
    }

    /// <summary>Converts <paramref name="color"/> with the table selected by <paramref name="transformId"/>.</summary>
    public static byte Apply(int transformId, byte color) => Get(transformId)[color];
}
