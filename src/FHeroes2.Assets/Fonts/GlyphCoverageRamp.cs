using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Fonts;

/// <summary>
/// Translates the coverage of a rasterized outline glyph (0 = not covered, 255 = fully covered)
/// into palette indices, which is how an antialiased vector glyph is drawn into the indexed frame
/// buffer the original engine uses.
/// </summary>
/// <remarks>
/// <para>
/// The Heroes II palette (<c>KB.PAL</c>) stores a 27 step grey ramp in indices 10-36 with
/// <c>10</c> as the brightest value, and <c>FONT.ICN</c> is shaded with exactly those indices
/// (10, 16, 21, 12 and 14 carry almost every lit pixel of the table). Quantizing coverage onto
/// the same ramp keeps a TrueType string visually close to the bitmap font instead of
/// introducing new colours.
/// </para>
/// <para>
/// Coverage below <see cref="MinCoverage"/> is dropped rather than painted with the faintest
/// step: the glyphs are small, and keeping the last few percent of coverage is what makes
/// antialiased text at 12-16 px look blurry.
/// </para>
/// <para>
/// The ramp is also what colours rasterized text: <see cref="WithPalette(PaletteId)"/> converts the
/// indices with one of the original palettes, so the same glyph can be drawn in the yellow of
/// <c>YELLOW_FONT</c> instead of the default grey (ADR-020).
/// </para>
/// </remarks>
public sealed class GlyphCoverageRamp
{
    /// <summary>Coverage of a pixel the outline covers completely.</summary>
    public const byte FullCoverage = 255;

    private readonly byte[] _levels;

    /// <param name="levels">
    /// Palette indices ordered from the faintest to the most solid step. A single level gives a
    /// hard edged (non antialiased) glyph.
    /// </param>
    /// <param name="minCoverage">Lowest coverage that still paints a pixel.</param>
    public GlyphCoverageRamp(IReadOnlyList<byte> levels, byte minCoverage = 32)
    {
        ArgumentNullException.ThrowIfNull(levels);

        if (levels.Count == 0)
        {
            throw new ArgumentException("A coverage ramp needs at least one palette index.", nameof(levels));
        }

        if (minCoverage == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minCoverage), minCoverage, "Zero coverage is never painted.");
        }

        _levels = [.. levels];
        MinCoverage = minCoverage;
    }

    /// <summary>
    /// The ramp measured from the original font: five steps of the <c>KB.PAL</c> grey scale,
    /// faintest (<c>25</c>) to solid (<c>10</c>).
    /// </summary>
    public static GlyphCoverageRamp Heroes2Text { get; } = new([25, 21, 16, 12, 10]);

    /// <summary>Palette indices of the ramp, faintest first.</summary>
    public IReadOnlyList<byte> Levels => _levels;

    /// <summary>Lowest coverage that still paints a pixel.</summary>
    public byte MinCoverage { get; }

    /// <summary>
    /// Returns a ramp whose palette indices were converted by <paramref name="id"/>, which is how
    /// rasterized text gets the colour of a bitmap font without a second rasterizer pass: the
    /// original grey ramp <c>10..36</c> becomes the yellow ramp <c>114..130</c> for
    /// <see cref="PaletteId.YellowFont"/>.
    /// </summary>
    /// <remarks>
    /// Steps that the table maps onto the same index are kept: the glyph is then quantized more
    /// coarsely, which is exactly what the original 16 colour ramps do.
    /// </remarks>
    public GlyphCoverageRamp WithPalette(PaletteId id)
    {
        if (id == PaletteId.Standard)
        {
            return this;
        }

        return WithPalette(PaletteTable.Get(id));
    }

    /// <summary>Converts the ramp with a raw <see cref="PaletteTable"/> entry.</summary>
    /// <exception cref="ArgumentException">
    /// The table is not <see cref="PaletteTable.Size"/> bytes long.
    /// </exception>
    public GlyphCoverageRamp WithPalette(ReadOnlySpan<byte> palette)
    {
        if (palette.Length != PaletteTable.Size)
        {
            throw new ArgumentException(
                $"A palette table must hold {PaletteTable.Size} entries.", nameof(palette));
        }

        byte[] levels = new byte[_levels.Length];
        for (int index = 0; index < _levels.Length; ++index)
        {
            levels[index] = palette[_levels[index]];
        }

        return new GlyphCoverageRamp(levels, MinCoverage);
    }

    /// <summary>
    /// Maps <paramref name="coverage"/> to a palette index. Returns <see langword="false"/> when the
    /// pixel has to stay transparent, in which case <paramref name="paletteIndex"/> is <c>0</c>.
    /// </summary>
    public bool TryMap(byte coverage, out byte paletteIndex)
    {
        if (coverage < MinCoverage)
        {
            paletteIndex = 0;
            return false;
        }

        int span = FullCoverage - MinCoverage + 1;
        int level = ((coverage - MinCoverage) * _levels.Length) / span;
        if (level >= _levels.Length)
        {
            level = _levels.Length - 1;
        }

        paletteIndex = _levels[level];
        return true;
    }
}
