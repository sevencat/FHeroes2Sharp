using FHeroes2.Assets;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the generated alphabets, <c>generateAlphabet()</c> and its eight code page helpers
/// (<c>ui_font.cpp:186-4947</c>, <c>ui_language.cpp:291</c>).
/// </summary>
/// <remarks>
/// 生成式的语句是纯数据，这里只钉住骨架的语义：哪些语言要生成、生成在哪个码位、
/// 空白字符的宽度、以及「生成不会改到 ASCII 字形」这条前提。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class FontAlphabetTests
{
    /// <summary>Frames a shipped ASCII font has: characters 0x20 to 0x7F.</summary>
    private const int AsciiFrameCount = 0x80 - FontAlphabet.FirstCharacter;

    /// <summary>An ASCII table shaped like the archive's: square glyphs with a transform layer.</summary>
    private static Sprite?[] AsciiFrames()
    {
        Sprite?[] frames = new Sprite?[AsciiFrameCount];

        for (int index = 0; index < frames.Length; ++index)
        {
            Sprite glyph = new(16, 16, index, 2);
            ImageOps.FillSprite(glyph, glyph.Image.Bounds, (byte)(index + 1));
            frames[index] = glyph;
        }

        return frames;
    }

    /// <summary>Runs <paramref name="body"/> with no archives loaded, so the resources are English.</summary>
    private static void WithoutArchives(Action body)
    {
        AssetBundle? restore = Assets.Current;
        Assets.Current = null;
        try
        {
            body();
        }
        finally
        {
            Assets.Current = restore;
        }
    }

    [Fact]
    public void EnglishWithEnglishResourcesKeepsTheTableItWasGiven()
    {
        WithoutArchives(() =>
        {
            Sprite?[] table = AsciiFrames();

            Assert.Same(table, FontAlphabet.Apply(SupportedLanguage.English, FontSize.Normal, table));
            Assert.Same(table, FontAlphabet.Apply(SupportedLanguage.English, FontSize.Small, table));
        });
    }

    [Fact]
    public void RussianAddsTheCyrillicLettersAndTheSpace()
    {
        WithoutArchives(() =>
        {
            Sprite?[] table = AsciiFrames();

            Sprite?[] built = FontAlphabet.Apply(SupportedLanguage.Russian, FontSize.Normal, table);

            // A code page covers 256 slots, of which the first 32 cannot be drawn.
            Assert.Equal(FontAlphabet.CodePageSize - FontAlphabet.FirstCharacter, built.Length);

            // CP1251 0xC0 ('А') is a copy of the ASCII 'A' (0x41), not the same object.
            Sprite cyrillicA = Assert.IsType<Sprite>(built[0xC0 - FontAlphabet.FirstCharacter]);
            Assert.NotSame(table[0x41 - FontAlphabet.FirstCharacter], cyrillicA);
            Assert.Equal(table[0x41 - FontAlphabet.FirstCharacter]!.Width, cyrillicA.Width);
            Assert.Equal(table[0x41 - FontAlphabet.FirstCharacter]!.Height, cyrillicA.Height);

            // The non-breaking space is a bar as wide as a normal font space (ui_font.cpp:26).
            Sprite noBreakSpace = Assert.IsType<Sprite>(built[0xA0 - FontAlphabet.FirstCharacter]);
            Assert.Equal(6, noBreakSpace.Width);
            Assert.Equal(1, noBreakSpace.Height);

            // The ASCII frames themselves are handed through unchanged.
            Assert.Same(table[0], built[0]);
            Assert.Same(table[0x41 - FontAlphabet.FirstCharacter], built[0x41 - FontAlphabet.FirstCharacter]);
        });
    }

    [Fact]
    public void TheSmallAlphabetUsesTheSmallSpaceWidth()
    {
        WithoutArchives(() =>
        {
            Sprite?[] table = AsciiFrames();

            Sprite?[] built = FontAlphabet.Apply(SupportedLanguage.Russian, FontSize.Small, table);

            Sprite noBreakSpace = Assert.IsType<Sprite>(built[0xA0 - FontAlphabet.FirstCharacter]);
            Assert.Equal(4, noBreakSpace.Width);
            Assert.Equal(1, noBreakSpace.Height);
        });
    }

    [Fact]
    public void PolishGrowsTheLettersItNeeds()
    {
        WithoutArchives(() =>
        {
            Sprite?[] table = AsciiFrames();

            Sprite?[] built = FontAlphabet.Apply(SupportedLanguage.Polish, FontSize.Normal, table);

            // CP1250 0xA5 ('Ą') is the ASCII 'A' with an ogonek added below, so it is three pixels taller
            // (ui_font.cpp:1322-1335).
            Sprite ogonek = Assert.IsType<Sprite>(built[0xA5 - FontAlphabet.FirstCharacter]);
            Assert.Equal(table[0x41 - FontAlphabet.FirstCharacter]!.Width, ogonek.Width);
            Assert.Equal(table[0x41 - FontAlphabet.FirstCharacter]!.Height + 3, ogonek.Height);
        });
    }

    [Fact]
    public void LargeFontsAreLeftAlone()
    {
        WithoutArchives(() =>
        {
            Sprite?[] table = AsciiFrames();

            // The reference generates the normal and the small table only; the large face is grown from
            // the normal one afterwards.
            Assert.Same(table, FontAlphabet.Apply(SupportedLanguage.Russian, FontSize.Large, table));
        });
    }

    [Fact]
    public void GeneratingTwiceLeavesTheAsciiGlyphsAlone()
    {
        WithoutArchives(() =>
        {
            Sprite?[] table = AsciiFrames();
            Sprite asciiA = table[0x41 - FontAlphabet.FirstCharacter]!;
            byte[] pixels = (byte[])asciiA.Image.Pixels.Clone();
            byte[] transform = (byte[])asciiA.Transform!.Clone();

            FontAlphabet.Apply(SupportedLanguage.Russian, FontSize.Normal, table);
            FontAlphabet.Apply(SupportedLanguage.Bulgarian, FontSize.Normal, table);

            Assert.Equal(pixels, asciiA.Image.Pixels);
            Assert.Equal(transform, asciiA.Transform);
        });
    }

    [Fact]
    public void TheShippedFontTakesThePolishAlphabet()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        AssetBundle? previous = Assets.Current;
        Assets.Current = assets;
        try
        {
            int count = Assets.GetImageCount("FONT.ICN");
            Sprite?[] table = new Sprite?[count];

            for (int index = 0; index < count; ++index)
            {
                table[index] = Assets.GetImage("FONT.ICN", index);
            }

            // Real glyph sizes are what the generated statements assume; a synthetic square is no proof.
            Sprite?[] built = FontAlphabet.Apply(SupportedLanguage.Polish, FontSize.Normal, table);

            Assert.Equal(FontAlphabet.CodePageSize - FontAlphabet.FirstCharacter, built.Length);

            // CP1250 0xC9 ('É') is the ASCII 'E' with an acute drawn from the 'J' pixels.
            Assert.NotNull(built[0xC9 - FontAlphabet.FirstCharacter]);
            Assert.NotSame(table[0x45 - FontAlphabet.FirstCharacter], built[0xC9 - FontAlphabet.FirstCharacter]);
        }
        finally
        {
            Assets.Current = previous;
        }
    }
}
