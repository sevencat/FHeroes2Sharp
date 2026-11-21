using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The "Kingdom Income" box. Only the paths that do not need the bitmap fonts are exercised here:
/// <see cref="ResourceDialogElement"/> measures its amount with <c>FHeroes2.Game.Gui.Text</c>, which needs the real
/// archive, so a box with a non zero amount is covered by the game tests instead.
/// </summary>
/// <remarks>
/// The box is drawn in a <see cref="FrameBox"/>, which reads <c>BUYBUILD.ICN</c> itself, so creating one
/// needs the installed archives; the tests return when the machine has none.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
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
    public void Create_WithoutIncomeIsATextOnlyBox() => Assert.True(TryWithArchives(
        () => KingdomIncome.Create(default, CreateIcons(), Font, Font),
        box =>
        {
            Assert.Equal(KingdomIncome.Header, box.Header);
            Assert.Equal(KingdomIncome.Body, box.Body);
            Assert.Empty(box.Elements);
            Assert.Empty(box.ElementAreas);

            // The smallest box: the 25 pixel title block plus the 25 pixel body block do not reach the
            // 70 pixel minimum.
            Assert.Equal(70, box.Area.Height);
        }));

    [Fact]
    public void Create_AcceptsTranslatedText() => Assert.True(TryWithArchives(
        () => KingdomIncome.Create(default, CreateIcons(), Font, Font, "入", "每天"),
        box =>
        {
            Assert.Equal("入", box.Header);
            Assert.Equal("每天", box.Body);
        }));

    [Fact]
    public void Create_RejectsIncompleteResourceArtwork()
    {
        List<Sprite> icons = CreateIcons();
        icons.RemoveAt(0);

        Assert.Throws<ArgumentException>(() => KingdomIncome.Create(default, icons, Font, Font));
    }

    [Fact]
    public void Create_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KingdomIncome.Create(default, null!, Font, Font));
        Assert.Throws<ArgumentNullException>(() =>
            KingdomIncome.Create(default, CreateIcons(), null!, Font));
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

    /// <summary>Opens the game data the message frame is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Builds a box against the real frame artwork.</summary>
    /// <param name="create">Builds the box.</param>
    /// <param name="assert">Checks it.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArchives(Func<MessageBox> create, Action<MessageBox> assert)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            assert(create());
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
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
