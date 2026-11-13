namespace FHeroes2.Core.Imaging;

/// <summary>
/// A 256 entry indexed palette. The original engine stores palettes as raw 768 byte
/// RGB triples (for example <c>KB.PAL</c> inside <c>heroes2.agg</c>).
/// </summary>
public sealed class Palette
{
    public const int ColorCount = 256;

    private const int RawSize = ColorCount * 3;

    private readonly RgbaColor[] _colors = new RgbaColor[ColorCount];

    /// <summary>Creates a palette filled with a grayscale ramp, used when no game data is available.</summary>
    public Palette()
    {
        for (int i = 0; i < ColorCount; ++i)
        {
            byte value = (byte)i;
            _colors[i] = new RgbaColor(value, value, value);
        }
    }

    /// <summary>Creates a palette from a raw 768 byte RGB triple table.</summary>
    public Palette(ReadOnlySpan<byte> rawRgb)
    {
        Load(rawRgb);
    }

    public ReadOnlySpan<RgbaColor> Colors => _colors;

    public static int RawPaletteSize => RawSize;

    public RgbaColor this[int index]
    {
        get => _colors[index];
        set => _colors[index] = value;
    }

    public static Palette FromRaw(ReadOnlySpan<byte> rawRgb) => new(rawRgb);

    /// <summary>
    /// Loads a palette exactly the way <c>fheroes2::setGamePalette</c> does for <c>KB.PAL</c>:
    /// the file stores 6 bit VGA components (0..63) which are shifted left by two bits, and the
    /// color cycling entries are duplicated into their non-cycling slots (water 231..233/235 ->
    /// 246..249, lava 214..217 -> 250..253) so images render identically without cycling.
    /// </summary>
    public static Palette FromGamePalette(ReadOnlySpan<byte> rawVga)
    {
        if (rawVga.Length < RawSize)
        {
            throw new ArgumentException(
                $"A palette needs at least {RawSize} bytes but only {rawVga.Length} were provided.",
                nameof(rawVga));
        }

        Palette palette = new();

        for (int i = 0; i < ColorCount; ++i)
        {
            palette._colors[i] = new RgbaColor(
                (byte)(rawVga[i * 3] << 2),
                (byte)(rawVga[(i * 3) + 1] << 2),
                (byte)(rawVga[(i * 3) + 2] << 2));
        }

        CopyColor(palette, 231, 246);
        CopyColor(palette, 232, 247);
        CopyColor(palette, 233, 248);
        CopyColor(palette, 235, 249);

        for (int i = 0; i < 4; ++i)
        {
            CopyColor(palette, 214 + i, 250 + i);
        }

        return palette;
    }

    private static void CopyColor(Palette palette, int source, int destination) =>
        palette._colors[destination] = palette._colors[source];

    public void Load(ReadOnlySpan<byte> rawRgb)
    {
        if (rawRgb.Length < RawSize)
        {
            throw new ArgumentException(
                $"A palette needs at least {RawSize} bytes but only {rawRgb.Length} were provided.",
                nameof(rawRgb));
        }

        for (int i = 0; i < ColorCount; ++i)
        {
            _colors[i] = new RgbaColor(rawRgb[i * 3], rawRgb[(i * 3) + 1], rawRgb[(i * 3) + 2]);
        }
    }

    public byte[] ToRaw()
    {
        byte[] raw = new byte[RawSize];
        for (int i = 0; i < ColorCount; ++i)
        {
            raw[i * 3] = _colors[i].R;
            raw[(i * 3) + 1] = _colors[i].G;
            raw[(i * 3) + 2] = _colors[i].B;
        }

        return raw;
    }

    public Palette Clone()
    {
        Palette copy = new();
        _colors.CopyTo(copy._colors, 0);
        return copy;
    }
}
