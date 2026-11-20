using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The "Kingdom Income" box. Only the paths that do not need the bitmap fonts are exercised here:
/// <see cref="ResourceDialogElement"/> measures its amount with <c>FHeroes2.Game.Gui.Text</c>, which needs the real
/// archive, so a box with a non zero amount is covered by the game tests instead.
/// </summary>
public sealed class KingdomIncomeTests
{
    [Fact]
    public void Items_PutsGoldFirstAndSkipsResourcesThatEarnNothing()
    {
        // getResourceDialogElements() walks gold, wood, mercury, ore, sulfur, crystal, gems and leaves out the zeroes,
        // so three earning resources come out gold first whatever order the funds were filled in.
        Funds income = default;
        income.Wood = 5;
        income.Gold = 1000;
        income.Gems = 2;

        List<KeyValuePair<int, int>> items = KingdomIncome.Items(income);

        Assert.Equal([Resource.Gold, Resource.Wood, Resource.Gems], items.Select(item => item.Key));
        Assert.Equal([1000, 5, 2], items.Select(item => item.Value));
    }

    [Fact]
    public void Items_OfAnEmptyIncomeIsEmpty()
    {
        Assert.Empty(KingdomIncome.Items(default));
    }

    [Fact]
    public void Create_WithoutIncomeIsATextOnlyBox()
    {
        MessageBox box = KingdomIncome.Create(default, CreateIcons(), CreateChrome(), Font, Font);

        Assert.Equal(KingdomIncome.Header, box.Header);
        Assert.Equal(KingdomIncome.Body, box.Body);
        Assert.Empty(box.Elements);
        Assert.Empty(box.ElementAreas);

        // The smallest box: the 25 pixel title block plus the 25 pixel body block do not reach the 70 pixel minimum.
        Assert.Equal(70, box.Area.Height);
    }

    [Fact]
    public void Create_AcceptsTranslatedText()
    {
        MessageBox box = KingdomIncome.Create(default, CreateIcons(), CreateChrome(), Font, Font, "入", "每天");

        Assert.Equal("入", box.Header);
        Assert.Equal("每天", box.Body);
    }

    [Fact]
    public void Create_RejectsIncompleteResourceArtwork()
    {
        List<Sprite> icons = CreateIcons();
        icons.RemoveAt(0);

        Assert.Throws<ArgumentException>(() => KingdomIncome.Create(default, icons, CreateChrome(), Font, Font));
    }

    [Fact]
    public void Create_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KingdomIncome.Create(default, null!, CreateChrome(), Font, Font));
        Assert.Throws<ArgumentNullException>(() =>
            KingdomIncome.Create(default, CreateIcons(), null!, Font, Font));
        Assert.Throws<ArgumentNullException>(() =>
            KingdomIncome.Create(default, CreateIcons(), CreateChrome(), null!, Font));
    }

    private static List<Sprite> CreateIcons()
    {
        List<Sprite> icons = [];

        for (int index = 0; index < ResourceIconIndex.Count; ++index)
        {
            PixelBuffer image = new(30, 30);
            image.Fill((byte)(index + 1));
            icons.Add(new Sprite(image, 0, 0));
        }

        return icons;
    }

    private static FrameBoxChrome CreateChrome()
    {
        // Only the geometry of BUYBUILD.ICN matters here, so every piece is a flat colour.
        Size[] sizes =
        [
            new(144, 99), new(145, 45), new(144, 81), new(251, 20),
            new(160, 99), new(161, 45), new(161, 81),
        ];

        Sprite[] pieces = new Sprite[sizes.Length];
        for (int index = 0; index < sizes.Length; ++index)
        {
            PixelBuffer image = new(sizes[index].Width, sizes[index].Height);
            image.Fill(7);
            pieces[index] = new Sprite(image, 0, 0);
        }

        return FrameBoxChrome.TryCreate(pieces)!;
    }

    /// <summary>A glyph source with the metrics of <c>FONT.ICN</c> whose glyphs are solid blocks.</summary>
    private static IGlyphSource Font { get; } = new BlockFont();

    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont()
        {
            PixelBuffer image = new(6, 15);
            image.Fill(90);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight => 15;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
