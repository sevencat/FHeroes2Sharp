using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <c>renderHeroRacePortrait()</c> (<c>src/fheroes2/gui/ui_tool.cpp:841-888</c>).
/// </summary>
public sealed class HeroRacePortraitTests
{
    /// <summary>Every race picks its own hero ICN, its own STRIP background and, for random, the purple tint.</summary>
    [Theory]
    [InlineData(MapRace.Knight, IcnType.Cmbthrok, 4, false)]
    [InlineData(MapRace.Barbarian, IcnType.Cmbthrob, 5, false)]
    [InlineData(MapRace.Sorceress, IcnType.Cmbthros, 6, false)]
    [InlineData(MapRace.Warlock, IcnType.Cmbthrow, 7, false)]
    [InlineData(MapRace.Wizard, IcnType.Cmbthroz, 8, false)]
    [InlineData(MapRace.Necromancer, IcnType.Cmbthron, 9, false)]
    [InlineData(MapRace.Random, IcnType.Cmbthrow, 10, true)]
    public void EachRaceUsesItsOwnArtwork(MapRace race, IcnType heroes, int background, bool applyRandomPalette)
    {
        Assert.True(HeroRacePortrait.TryGetArtwork(race, out HeroRaceArtwork artwork));

        Assert.Equal(heroes, artwork.Heroes);
        Assert.Equal(background, artwork.Background);
        Assert.Equal(applyRandomPalette, artwork.ApplyRandomPalette);
    }

    /// <summary>A race without a portrait is refused instead of hitting the reference <c>assert</c>.</summary>
    [Theory]
    [InlineData(MapRace.None)]
    [InlineData(MapRace.Multiple)]
    public void RacesWithoutAPortraitAreRefused(MapRace race) =>
        Assert.False(HeroRacePortrait.TryGetArtwork(race, out _));

    /// <summary>The scaled background fills the portrait and the figure is centred on it.</summary>
    [Fact]
    public void TheFigureIsCentredOverTheScaledBackground()
    {
        Sprite background = BuildUniformSprite(2, 2, BackgroundIndex);
        Sprite hero = BuildUniformSprite(2, 2, FigureIndex);

        Sprite portrait = HeroRacePortrait.Compose(background, hero, false, new Size(4, 4), BuildPalette());

        // A figure shorter than the portrait sits on the bottom edge, which is outY = 4 - 2 = 2.
        Assert.Equal(new byte[] { BackgroundIndex, BackgroundIndex, BackgroundIndex, BackgroundIndex }, Row(portrait, 0));
        Assert.Equal(new byte[] { BackgroundIndex, BackgroundIndex, BackgroundIndex, BackgroundIndex }, Row(portrait, 1));
        Assert.Equal(new byte[] { BackgroundIndex, FigureIndex, FigureIndex, BackgroundIndex }, Row(portrait, 2));
        Assert.Equal(new byte[] { BackgroundIndex, FigureIndex, FigureIndex, BackgroundIndex }, Row(portrait, 3));
    }

    /// <summary>Transparent pixels of the figure leave the background visible.</summary>
    [Fact]
    public void TheTransparentPartOfTheFigureKeepsTheBackground()
    {
        Sprite background = BuildUniformSprite(2, 2, BackgroundIndex);
        Sprite hero = BuildSprite(2, 2, FigureIndex, new byte[] { 0, 1, 0, 1 });

        Sprite portrait = HeroRacePortrait.Compose(background, hero, false, new Size(4, 4), BuildPalette());

        // Left column of the figure is opaque, right column has transform 1 and must keep the background.
        Assert.Equal(new byte[] { BackgroundIndex, FigureIndex, BackgroundIndex, BackgroundIndex }, Row(portrait, 2));
        Assert.Equal(new byte[] { BackgroundIndex, FigureIndex, BackgroundIndex, BackgroundIndex }, Row(portrait, 3));
    }

    /// <summary>The random race recolours the figure with the purple palette instead of tinting the background.</summary>
    [Fact]
    public void TheRandomRaceAppliesThePurplePaletteToTheFigure()
    {
        byte purple = PaletteTable.Get(PaletteId.Purple)[FigureIndex];
        Assert.NotEqual(FigureIndex, purple);

        Sprite background = BuildUniformSprite(2, 2, BackgroundIndex);
        Sprite hero = BuildUniformSprite(2, 2, FigureIndex);

        Sprite portrait = HeroRacePortrait.Compose(background, hero, true, new Size(4, 4), BuildPalette());

        Assert.Equal(new byte[] { BackgroundIndex, purple, purple, BackgroundIndex }, Row(portrait, 2));
        Assert.Equal(FigureIndex, hero.Image[0, 0]); // the input sprite must not be modified
    }

    /// <summary>The whole chain against the real ICN files, when the game data is available.</summary>
    [Fact]
    public void ThePortraitIsBuiltFromTheGameAssets()
    {
        using AssetBundle? assets = OpenAssets();
        if (assets is null)
        {
            return;
        }

        byte[]? rawPalette = assets.TryRead("KB.PAL");
        if (rawPalette is null)
        {
            return;
        }

        Size size = new(101, 93);
        Sprite? portrait = HeroRacePortrait.TryRender(assets, MapRace.Knight, size, Palette.FromGamePalette(rawPalette));

        Assert.NotNull(portrait);
        Assert.Equal(size.Width, portrait.Width);
        Assert.Equal(size.Height, portrait.Height);
        // The reference builds the portrait in a plain Image, so the result has a single layer.
        Assert.False(portrait.HasTransformLayer);

        // The background is not a flat colour, so a real render has to hold more than one palette index.
        Assert.True(portrait.Image.Pixels.Distinct().Count() > 8, "the portrait was not composed");
    }

    private const byte BackgroundIndex = 30;

    private const byte FigureIndex = 40;

    /// <summary>Opens the game archive, or returns <see langword="null"/> when the machine has no game data.</summary>
    private static AssetBundle? OpenAssets() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static Sprite BuildUniformSprite(int width, int height, byte color) =>
        BuildSprite(width, height, color, new byte[width * height]);

    private static Sprite BuildSprite(int width, int height, byte color, byte[] transform)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image, 0, 0, transform);
    }

    private static byte[] Row(Sprite sprite, int y)
    {
        byte[] row = new byte[sprite.Width];
        for (int x = 0; x < sprite.Width; ++x)
        {
            row[x] = sprite.Image[x, y];
        }

        return row;
    }

    /// <summary>
    /// Entry 30 is white and entry 40 is red, everything else is black, so the resampler maps the two
    /// indices onto themselves and the assertions can name them directly.
    /// </summary>
    private static Palette BuildPalette()
    {
        byte[] raw = new byte[PaletteTable.Size * 3];

        raw[BackgroundIndex * 3] = raw[(BackgroundIndex * 3) + 1] = raw[(BackgroundIndex * 3) + 2] = 255;
        raw[FigureIndex * 3] = 255;

        return new Palette(raw);
    }
}
