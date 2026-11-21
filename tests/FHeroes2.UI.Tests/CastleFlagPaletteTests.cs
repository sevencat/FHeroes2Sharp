using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The player-coloured flags of the town screen, <c>getModifiedByColorImage()</c>
/// (<c>ui_castle.cpp:42-165</c>).
/// </summary>
public sealed class CastleFlagPaletteTests
{
    [Fact]
    public void TheFlagRampIsSixteenColoursPlusSevenShades()
    {
        Assert.Equal(152, CastleFlagPalette.OriginalStartId);
        Assert.Equal(23, CastleFlagPalette.OriginalLength);
    }

    [Theory]
    [InlineData(PlayerColor.Blue, 63, 84)]
    [InlineData(PlayerColor.Green, 85, 107)]
    [InlineData(PlayerColor.Red, 175, 197)]
    [InlineData(PlayerColor.Yellow, 108, 130)]
    [InlineData(PlayerColor.Orange, 199, 214)]
    [InlineData(PlayerColor.Purple, 132, 152)]
    public void EachColourMapsTheRampOntoItsOwnScale(PlayerColor color, byte first, byte last)
    {
        byte[] palette = CastleFlagPalette.ForColor(color);

        Assert.Equal(first, palette[152]);
        Assert.Equal(last, palette[174]);
    }

    [Theory]
    [InlineData(PlayerColor.Blue)]
    [InlineData(PlayerColor.Green)]
    [InlineData(PlayerColor.Red)]
    [InlineData(PlayerColor.Yellow)]
    [InlineData(PlayerColor.Orange)]
    [InlineData(PlayerColor.Purple)]
    public void NothingOutsideTheRampMoves(PlayerColor color)
    {
        byte[] palette = CastleFlagPalette.ForColor(color);

        for (int i = 0; i < palette.Length; ++i)
        {
            if (i < CastleFlagPalette.OriginalStartId || i >= CastleFlagPalette.OriginalStartId + CastleFlagPalette.OriginalLength)
            {
                Assert.Equal((byte)i, palette[i]);
            }
        }
    }

    [Fact]
    public void ATownWithoutAnOwnerKeepsTheArchiveColours()
    {
        byte[] palette = CastleFlagPalette.ForColor(PlayerColor.None);

        for (int i = 0; i < palette.Length; ++i)
        {
            Assert.Equal((byte)i, palette[i]);
        }
    }

    [Fact]
    public void OnlyTheSixFlagArchivesAreColourDependent()
    {
        Assert.True(CastleFlagPalette.IsColorDependent(IcnType.Twnzcstl));
        Assert.True(CastleFlagPalette.IsColorDependent(IcnType.Twnkcstl));
        Assert.True(CastleFlagPalette.IsColorDependent(IcnType.Twnkdw4));
        Assert.True(CastleFlagPalette.IsColorDependent(IcnType.Twnkup4));
        Assert.True(CastleFlagPalette.IsColorDependent(IcnType.Twnkltur));
        Assert.True(CastleFlagPalette.IsColorDependent(IcnType.Twnkrtur));

        Assert.False(CastleFlagPalette.IsColorDependent(IcnType.Twnbext2));
        Assert.False(CastleFlagPalette.IsColorDependent(IcnType.Twnbext3));
        Assert.Empty(CastleFlagPalette.EffectiveAreas(IcnType.Twnbext2, 0, 640, 480));
    }

    [Fact]
    public void StaticKnightFramesOnlyTouchTheLittleFlags()
    {
        // ui_castle.cpp: frame 0 of the knight's castle carries two 3x3 flags, the animation frames carry the whole frame.
        Assert.Equal(
            [new Rect(127, 36, 3, 3), new Rect(287, 6, 3, 3)],
            CastleFlagPalette.EffectiveAreas(IcnType.Twnkcstl, 0, 300, 200));
        Assert.Equal([new Rect(0, 0, 300, 200)], CastleFlagPalette.EffectiveAreas(IcnType.Twnkcstl, 1, 300, 200));

        Assert.Equal([new Rect(5, 6, 3, 3)], CastleFlagPalette.EffectiveAreas(IcnType.Twnkltur, 0, 64, 64));
        Assert.Equal([new Rect(55, 6, 3, 3)], CastleFlagPalette.EffectiveAreas(IcnType.Twnkrtur, 0, 64, 64));
        Assert.Equal([new Rect(59, 0, 6, 5)], CastleFlagPalette.EffectiveAreas(IcnType.Twnkdw4, 1, 64, 64));
        Assert.Equal([new Rect(59, 0, 6, 5)], CastleFlagPalette.EffectiveAreas(IcnType.Twnkup4, 1, 64, 64));
    }

    [Fact]
    public void TheWizardCastleCarriesThreeFlagsInItsFirstFrame()
    {
        Assert.Equal(
            [new Rect(76, 7, 3, 3), new Rect(96, 7, 3, 3), new Rect(178, 11, 3, 3)],
            CastleFlagPalette.EffectiveAreas(IcnType.Twnzcstl, 0, 300, 200));
    }

    [Fact]
    public void RecolouringOnlyChangesTheFlagPixels()
    {
        Sprite sprite = FlagSprite();

        Sprite recoloured = CastleFlagPalette.Recolor(sprite, IcnType.Twnkltur, 0, PlayerColor.Blue);
        byte[] palette = CastleFlagPalette.ForColor(PlayerColor.Blue);

        Assert.Equal(palette[160], recoloured.Image.Row(6)[5]);
        Assert.Equal(161, recoloured.Image.Row(0)[0]);
        Assert.Equal(160, recoloured.Image.Row(9)[9]);
        Assert.Equal(160, sprite.Image.Row(6)[5]);
    }

    [Fact]
    public void AnAnimatedFrameIsConvertedWhole()
    {
        Sprite sprite = FlagSprite();

        Sprite recoloured = CastleFlagPalette.Recolor(sprite, IcnType.Twnkltur, 2, PlayerColor.Blue);

        Assert.NotEqual(160, recoloured.Image.Row(0)[0]);
    }

    [Fact]
    public void AnArchiveWithoutFlagsIsReturnedAsIs()
    {
        Sprite sprite = FlagSprite();

        Assert.Same(sprite, CastleFlagPalette.Recolor(sprite, IcnType.Twnbext2, 0, PlayerColor.Blue));
    }

    [Fact]
    public void TheCachedFrameIsSharedPerColour()
    {
        CastleFlagPalette.ClearCache();
        Sprite sprite = FlagSprite();

        Sprite blue = CastleFlagPalette.Get(sprite, IcnType.Twnkltur, 0, PlayerColor.Blue);
        Sprite again = CastleFlagPalette.Get(sprite, IcnType.Twnkltur, 0, PlayerColor.Blue);
        Sprite red = CastleFlagPalette.Get(sprite, IcnType.Twnkltur, 0, PlayerColor.Red);

        Assert.Same(blue, again);
        Assert.NotSame(blue, red);
        Assert.Same(sprite, CastleFlagPalette.Get(sprite, IcnType.Twnkltur, 0, PlayerColor.None));
        Assert.Same(sprite, CastleFlagPalette.Get(sprite, IcnType.Twnbext2, 0, PlayerColor.Blue));

        CastleFlagPalette.ClearCache();
    }

    private static Sprite FlagSprite()
    {
        PixelBuffer image = new(10, 10);
        image.Fill(160);
        image.Row(0)[0] = 161;

        return new Sprite(image);
    }
}
