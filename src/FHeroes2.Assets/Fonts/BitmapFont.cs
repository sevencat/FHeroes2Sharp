using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Fonts;

/// <summary>
/// The original Heroes II bitmap font: one <see cref="Sprite"/> per character, indexed by the
/// character code, taken from <c>FONT.ICN</c> (normal) or <c>SMALFONT.ICN</c> (small).
/// </summary>
/// <remarks>
/// <para>
/// The layout rules are ported from <c>fheroes2::FontCharHandler</c>
/// (<c>src/fheroes2/gui/ui_text.cpp</c>): a character advances the pen by
/// <c>sprite.OffsetX + sprite.Width</c> pixels, space is a fixed width that depends on the size
/// only, and the sprite offset is applied as is. Text is therefore drawn into the indexed frame
/// buffer and the palette decides the final color, exactly like the original engine.
/// </para>
/// <para>
/// The fonts are single byte: characters above <c>U+00FF</c> cannot be represented and are drawn
/// as <c>?</c>, the same fallback the reference engine uses for characters outside the current
/// code page.
/// </para>
/// </remarks>
public sealed class BitmapFont : IGlyphSource
{
    private const char SpaceCharacter = ' ';
    private const char LineSeparator = '\n';
    private const char InvalidCharacter = '?';

    /// <summary>Lowest character code that has a glyph; everything below it is a control code.</summary>
    private const char FirstGlyph = (char)0x21;

    /// <summary>
    /// Character code of the first sprite of an ICN font table: <c>FONT.ICN</c> and
    /// <c>SMALFONT.ICN</c> start at the space character, so the table index is the character code
    /// minus <c>0x20</c>.
    /// </summary>
    private const int IcnFirstCharacter = ' ';

    private readonly Sprite[] _glyphs;
    private readonly Sprite _fallback;

    private BitmapFont(Sprite[] glyphs, BitmapFontSize size, string sourceName, PaletteId palette)
    {
        _glyphs = glyphs;
        _fallback = FindFallback(glyphs);
        FontSize = size;
        SourceName = sourceName;
        Palette = palette;
    }

    /// <summary>Font size the metrics of <see cref="LineHeight"/> and <see cref="SpaceWidth"/> come from.</summary>
    public BitmapFontSize FontSize { get; }

    /// <summary>Archive entry or description the glyphs were read from, for diagnostics.</summary>
    public string SourceName { get; }

    /// <summary>
    /// Palette the glyph pixels were converted with; <see cref="PaletteId.Standard"/> for a font
    /// read straight from the archive.
    /// </summary>
    /// <remarks>
    /// A <see cref="TrueTypeGlyphSource"/> that fills in the characters this table cannot express
    /// reads the value through <see cref="CompositeGlyphSource.ForBitmapFont"/>, so mixed text gets
    /// one colour without the caller having to pass the palette twice (ADR-020, ADR-021).
    /// </remarks>
    public PaletteId Palette { get; }

    /// <summary>Number of glyphs in the table; also the highest usable character code plus one.</summary>
    public int GlyphCount => _glyphs.Length;

    /// <summary>Horizontal advance of a space, <c>FontCharHandler::_getSpaceCharWidth()</c>.</summary>
    public int SpaceWidth => FontSize switch
    {
        BitmapFontSize.Small => 4,
        BitmapFontSize.Normal => 6,
        _ => 8,
    };

    /// <summary>
    /// Distance between two baselines, <c>fheroes2::getFontHeight()</c>. It is larger than the
    /// glyph height because the original UI leaves room for the shadow and a blank row.
    /// </summary>
    public int LineHeight => FontSize switch
    {
        BitmapFontSize.Small => 8 + 2 + 1,
        BitmapFontSize.Normal => 13 + 3 + 1,
        _ => 26 + 6 + 1,
    };

    /// <summary>Loads <c>SMALFONT.ICN</c> or <c>FONT.ICN</c> from the archives.</summary>
    /// <exception cref="FileNotFoundException">The archive does not contain the requested font.</exception>
    /// <exception cref="NotSupportedException">
    /// <see cref="BitmapFontSize.Large"/> was requested; the original engine scales
    /// <c>FONT.ICN</c> at run time, so use <see cref="Create"/> with the scaled glyphs instead.
    /// </exception>
    public static BitmapFont Load(AssetBundle bundle, BitmapFontSize size)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        if (size == BitmapFontSize.Large)
        {
            throw new NotSupportedException(
                "The large font is generated at run time by scaling FONT.ICN; use BitmapFont.Create with the scaled glyphs.");
        }

        string fileName = size == BitmapFontSize.Small ? "SMALFONT.ICN" : "FONT.ICN";
        byte[] data = bundle.TryRead(fileName)
            ?? throw new FileNotFoundException($"The archive does not contain '{fileName}'.");

        return FromIcn(IcnSpriteFile.Parse(data), size, fileName);
    }

    /// <summary>
    /// Loads a font and recolours it with <paramref name="color"/>, e.g.
    /// <see cref="PaletteId.YellowFont"/> for the yellow menu text of the original game.
    /// </summary>
    /// <exception cref="FileNotFoundException">The archive does not contain the requested font.</exception>
    /// <exception cref="NotSupportedException"><see cref="BitmapFontSize.Large"/> was requested.</exception>
    public static BitmapFont Load(AssetBundle bundle, BitmapFontSize size, PaletteId color) =>
        color == PaletteId.Standard ? Load(bundle, size) : Load(bundle, size).WithPalette(color);

    /// <summary>Wraps an already decoded ICN sprite table into a font.</summary>
    /// <remarks>
    /// The archive stores the table from character <c>0x20</c> onwards, so the sprite index is the
    /// character code minus <c>0x20</c>. <c>processICN()</c> of the reference engine prepends 32
    /// copies of the first sprite to make the index equal to the character code; the same shift
    /// happens here, which is why a loaded table has <c>ICN count + 32</c> glyphs.
    /// </remarks>
    public static BitmapFont FromIcn(IcnSpriteFile file, BitmapFontSize size, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Count == 0)
        {
            throw new ArgumentException("The ICN file does not contain any sprites.", nameof(file));
        }

        Sprite first = file.Sprites[0];
        Sprite[] glyphs = new Sprite[file.Count + IcnFirstCharacter];

        for (int index = 0; index < IcnFirstCharacter; ++index)
        {
            glyphs[index] = first;
        }

        for (int index = 0; index < file.Count; ++index)
        {
            glyphs[index + IcnFirstCharacter] = file.Sprites[index];
        }

        return Create(glyphs, size, sourceName ?? "ICN");
    }

    /// <summary>
    /// Returns a copy of the font recoloured through <paramref name="id"/>: the original engine
    /// keeps a single <c>FONT.ICN</c> table and derives the yellow and gray faces from it by
    /// converting every pixel with a palette of <see cref="PaletteTable"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PaletteId.Standard"/> returns <see langword="this"/>, so a caller that passes a
    /// colour through does not pay for a copy. The metrics, the glyph order and the transform
    /// layers are unchanged; only the image layer is converted.
    /// </remarks>
    public BitmapFont WithPalette(PaletteId id)
    {
        if (id == PaletteId.Standard)
        {
            return this;
        }

        ReadOnlySpan<byte> palette = PaletteTable.Get(id);

        // FONT.ICN shares one sprite between the 32 control codes, so the conversion has to be
        // done once per instance; converting a shared sprite twice would apply the table twice.
        Dictionary<Sprite, Sprite> copies = new(ReferenceEqualityComparer.Instance);
        Sprite[] glyphs = new Sprite[_glyphs.Length];

        for (int index = 0; index < _glyphs.Length; ++index)
        {
            Sprite glyph = _glyphs[index];

            if (!copies.TryGetValue(glyph, out Sprite? copy))
            {
                copy = glyph.Clone();
                ImageOps.ApplyPalette(copy.Image, palette);
                copies.Add(glyph, copy);
            }

            glyphs[index] = copy;
        }

        return Create(glyphs, FontSize, $"{SourceName} ({id})", id);
    }

    /// <summary>
    /// Creates a font from an explicit glyph table. Indices are character codes, so a table built
    /// by scaling <c>FONT.ICN</c> or by rasterizing a TrueType face plugs in here.
    /// </summary>
    /// <param name="glyphs">One sprite per character code.</param>
    /// <param name="size">Metrics of the table.</param>
    /// <param name="sourceName">Description used by diagnostics.</param>
    /// <param name="palette">Palette the glyph pixels were already converted with.</param>
    public static BitmapFont Create(
        IReadOnlyList<Sprite> glyphs, BitmapFontSize size, string sourceName, PaletteId palette = PaletteId.Standard)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        if (glyphs.Count == 0)
        {
            throw new ArgumentException("A bitmap font needs at least one glyph.", nameof(glyphs));
        }

        Sprite[] copy = new Sprite[glyphs.Count];
        for (int index = 0; index < copy.Length; ++index)
        {
            copy[index] = glyphs[index] ?? throw new ArgumentException($"Glyph {index} is null.", nameof(glyphs));
        }

        return new BitmapFont(copy, size, sourceName, palette);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the character can be rendered, including space and the
    /// line separator. Mirrors <c>FontCharHandler::isAvailable()</c>.
    /// </summary>
    public bool IsAvailable(char character) =>
        character == SpaceCharacter || character == LineSeparator || IsValid(character);

    /// <summary>
    /// Returns the glyph to draw. Characters outside the table fall back to <c>?</c>, like
    /// <c>FontCharHandler::getSprite()</c>.
    /// </summary>
    public Sprite GetSprite(char character) =>
        IsValid(character) ? _glyphs[character] : _fallback;

    /// <summary>
    /// Horizontal advance of one character: <c>sprite.OffsetX + sprite.Width</c> for a glyph,
    /// <see cref="SpaceWidth"/> for a space and zero for a line separator.
    /// </summary>
    public int GetWidth(char character)
    {
        if (character == SpaceCharacter)
        {
            return SpaceWidth;
        }

        if (character == LineSeparator)
        {
            return 0;
        }

        Sprite glyph = GetSprite(character);
        return glyph.OffsetX + glyph.Width;
    }

    /// <inheritdoc/>
    int IGlyphSource.GetAdvance(char character) => GetWidth(character);

    /// <inheritdoc/>
    bool IGlyphSource.TryGetGlyph(char character, out Sprite glyph)
    {
        if (IsValid(character))
        {
            glyph = _glyphs[character];
            return true;
        }

        glyph = _fallback;
        return false;
    }

    /// <summary>
    /// Width of a single line, the equivalent of <c>getLineWidth()</c>. Trailing spaces are
    /// ignored unless <paramref name="keepTrailingSpaces"/> is set, which is how multi line text
    /// joins lines without losing the separating space.
    /// </summary>
    public int Measure(string text, bool keepTrailingSpaces = false) =>
        GlyphText.Measure(this, text, keepTrailingSpaces);

    /// <summary>
    /// Draws <paramref name="text"/> as one line and returns the pen position after the last
    /// character, the equivalent of <c>renderSingleLine()</c>. A line separator inside the text is
    /// ignored, as a single line cannot contain one.
    /// </summary>
    public int DrawText(PixelBuffer target, Point position, string text) =>
        DrawText(target, position, text, target.Bounds);

    /// <summary>Draws one line clipped to <paramref name="area"/>.</summary>
    public int DrawText(PixelBuffer target, Point position, string text, in Rect area) =>
        GlyphText.DrawLine(this, target, position, text, area);

    /// <summary>
    /// Draws text with explicit line breaks and optional word wrapping and returns the covered
    /// area. Lines are <see cref="LineHeight"/> apart; with <paramref name="center"/> each line is
    /// centered inside <paramref name="maxWidth"/>.
    /// </summary>
    public Size DrawMultiline(PixelBuffer target, Point position, string text, int maxWidth = 0, bool center = false) =>
        GlyphText.DrawMultiline(this, target, position, text, maxWidth, center);

    private static Sprite FindFallback(Sprite[] glyphs)
    {
        if (glyphs.Length > InvalidCharacter)
        {
            return glyphs[InvalidCharacter];
        }

        // A table without '?' cannot render unknown characters at all; an empty sprite keeps the
        // advance at zero instead of drawing garbage.
        return new Sprite(0, 0);
    }

    private bool IsValid(char character) =>
        character >= FirstGlyph && character < _glyphs.Length;
}
