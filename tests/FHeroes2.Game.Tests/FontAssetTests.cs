using FHeroes2.Assets;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game;
using FHeroes2.Game.Gui;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the asset level fixes <c>processICN</c> applies to the fonts
/// (<c>src/fheroes2/game/game_assets.cpp:2461</c>) and of the language probe they depend on.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class FontAssetTests
{
    private const uint GermanCrc = 0x04745D1D;
    private const uint ItalianCrc = 0x219B3124;

    /// <summary>Frames that can be told apart by reference, the width of frame 121 being adjustable.</summary>
    private static Sprite?[] Frames(int count, int widthOf121 = 1)
    {
        Sprite?[] frames = new Sprite?[count];

        for (int index = 0; index < count; ++index)
        {
            frames[index] = new Sprite(index == 121 ? widthOf121 : 1, 1);
        }

        return frames;
    }

    [Fact]
    public void WithoutArchivesTheResourceLanguageIsEnglish()
    {
        Assets.Current = null;

        Assert.Equal(SupportedLanguage.English, ResourceLanguage.Get());
    }

    [Fact]
    public void TheFingerprintIsTheCrcOfTheFontFile()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            byte[]? raw = Assets.TryReadRaw("FONT.ICN");

            Assert.NotNull(raw);
            Assert.Equal(Crc32.Compute(raw!), FontAssetFixes.GetFingerprint("FONT.ICN"));
            Assert.Equal(0u, FontAssetFixes.GetFingerprint("NO SUCH FILE.ICN"));

            // The shipped assets of this repository are neither translated nor Polish, so nothing applies.
            Assert.False(FontAssetFixes.IsPolishSmallFont(FontAssetFixes.GetFingerprint("SMALFONT.ICN")));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void OnlyThePolishSmallFontFingerprintIsRecognised()
    {
        Assert.True(FontAssetFixes.IsPolishSmallFont(0xE9EC7A63));
        Assert.False(FontAssetFixes.IsPolishSmallFont(0xE9EC7A62));
        Assert.False(FontAssetFixes.IsPolishSmallFont(0));
    }

    [Fact]
    public void TheAsteriskIsFrameTen()
    {
        // replacePOLAssetWithSW( id, 42 - 32 ): the reference works in the "frame = ch - 0x20" space.
        Assert.Equal(10, FontAssetFixes.AsteriskFrame);
    }

    [Fact]
    public void MovingUpLiftsTheOffsetAndSharesTheImage()
    {
        Sprite?[] frames = Frames(3);
        frames[1] = null;
        frames[0] = new Sprite(2, 2, 5, 7);

        Sprite?[] shifted = FontAssetFixes.MoveUp(frames);

        Assert.Equal(3, shifted.Length);
        Assert.NotNull(shifted[0]);
        Assert.Equal(5, shifted[0]!.OffsetX);
        Assert.Equal(6, shifted[0]!.OffsetY);
        Assert.Same(frames[0]!.Image, shifted[0]!.Image);
        Assert.Null(shifted[1]);

        // The input is left alone.
        Assert.Equal(7, frames[0]!.OffsetY);
    }

    [Fact]
    public void CyrillicTablesGetTheTwoLettersSwappedBackIn()
    {
        Sprite?[] frames = Frames(162, widthOf121: 19);

        Sprite?[] arranged = FontAssetFixes.Arrange(frames, 0, FontSize.Normal);

        // 162 + 64 inserted - 2 removed, so the table now covers up to character 0x20 + 223.
        Assert.Equal(224, arranged.Length);

        // 64 blank frames go in at 96, so index 192 is the archive's frame 128 (0xA8) ...
        Assert.Same(frames[128], arranged[136]);
        // ... and index 225 is the archive's frame 161 (0xB8).
        Assert.Same(frames[161], arranged[152]);

        Assert.Same(frames[0], arranged[96]);
        Assert.Same(frames[95], arranged[95]);
    }

    [Fact]
    public void CyrillicDetectionChecksTheFontSizeToo()
    {
        // A CP1251 FONT.ICN has 19 pixel wide letters, SMALFONT 12: a Small table of Normal width is not one.
        Sprite?[] normalWidth = Frames(162, widthOf121: 19);
        Assert.Same(normalWidth, FontAssetFixes.Arrange(normalWidth, 0, FontSize.Small));

        Assert.Equal(224, FontAssetFixes.Arrange(Frames(162, widthOf121: 12), 0, FontSize.Small).Length);
    }

    [Fact]
    public void GermanTablesAreMovedToTheCp1252Order()
    {
        Sprite?[] frames = Frames(226);

        Sprite?[] arranged = FontAssetFixes.Arrange(frames, GermanCrc, FontSize.Normal);

        Assert.Equal(221, arranged.Length);
        Assert.Same(frames[96], arranged[196]);
        Assert.Same(frames[97], arranged[214]);
        Assert.Same(frames[98], arranged[220]);
        Assert.Same(frames[100], arranged[164]);
        Assert.Same(frames[101], arranged[182]);
        Assert.Same(frames[102], arranged[188]);
        Assert.Same(frames[99], arranged[191]);
    }

    [Fact]
    public void ItalianTablesBorrowGlyphsFromTheAsciiPart()
    {
        Sprite?[] frames = Frames(226);

        Sprite?[] arranged = FontAssetFixes.Arrange(frames, ItalianCrc, FontSize.Normal);

        Assert.Equal(218, arranged.Length);

        // imageArray[192 - 32] = imageArray[33] and imageArray[249 - 32] = imageArray[100].
        Assert.Same(frames[33], arranged[160]);
        Assert.Same(frames[100], arranged[217]);
        Assert.Same(frames[37], arranged[168]);
    }

    [Fact]
    public void UntouchedTablesAreReturnedAsTheyAre()
    {
        Sprite?[] frames = Frames(96);

        Assert.Same(frames, FontAssetFixes.Arrange(frames, 0, FontSize.Normal));
        Assert.Same(frames, FontAssetFixes.Arrange(frames, 0xDEADBEEF, FontSize.Small));
    }

    [Fact]
    public void FrenchNormalRepairsRedrawThreeSymbolsAndDotTheRest()
    {
        Sprite?[] frames = Frames(96);

        FontFrenchRepairs.ApplyNormal(frames);

        // $ is redrawn 9x12, & 11x12 and * 12x15 anchored at the top left corner.
        Assert.Equal(9, frames[36 - 32]!.Width);
        Assert.Equal(12, frames[36 - 32]!.Height);
        Assert.Equal(11, frames[38 - 32]!.Width);
        Assert.Equal(12, frames[38 - 32]!.Height);
        Assert.Equal(12, frames[42 - 32]!.Width);
        Assert.Equal(15, frames[42 - 32]!.Height);
        Assert.Equal(0, frames[42 - 32]!.OffsetX);
        Assert.Equal(0, frames[42 - 32]!.OffsetY);

        // The remaining printable characters become copies of the '.' glyph.
        Assert.Same(frames[46 - 32], frames[35 - 32]);
        Assert.Same(frames[46 - 32], frames[60 - 32]);
        Assert.Same(frames[46 - 32], frames[127 - 32]);

        // Neighbours are untouched.
        Assert.Equal(1, frames[65 - 32]!.Width);
        Assert.Equal(1, frames[34 - 32]!.Width);
    }

    [Fact]
    public void FrenchSmallRepairsRedrawTwoSymbolsAndDotTheRest()
    {
        Sprite?[] frames = Frames(96);

        FontFrenchRepairs.ApplySmall(frames);

        Assert.Equal(6, frames[36 - 32]!.Width);
        Assert.Equal(10, frames[36 - 32]!.Height);
        Assert.Equal(7, frames[38 - 32]!.Width);
        Assert.Equal(8, frames[38 - 32]!.Height);
        Assert.Equal(0, frames[38 - 32]!.OffsetX);
        Assert.Equal(0, frames[38 - 32]!.OffsetY);

        Assert.Same(frames[46 - 32], frames[35 - 32]);
        Assert.Same(frames[46 - 32], frames[94 - 32]);
        Assert.Same(frames[46 - 32], frames[126 - 32]);
    }
}
