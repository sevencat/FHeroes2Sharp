using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The "Lighthouses" box. The checks that build the whole box need the bitmap fonts and the message frame, which are
/// read from the installed archives, so they return when the machine has none; the validation paths run everywhere.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class LighthouseInfoTests
{
    [Fact]
    public void Create_RejectsIncompleteArtwork()
    {
        // The last frame the reference reads is 73, so an archive that stops there cannot build the tower.
        List<Sprite> frames = CreateFrames(73);

        Assert.Throws<ArgumentException>(() => LighthouseInfo.Create(0, frames, Font, Font));
    }

    [Fact]
    public void Create_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => LighthouseInfo.Create(0, null!, Font, Font));
        Assert.Throws<ArgumentNullException>(() => LighthouseInfo.Create(0, CreateFrames(), null!, Font));
        Assert.Throws<ArgumentNullException>(() => LighthouseInfo.Create(0, CreateFrames(), Font, null!));
    }

    [Fact]
    public void Create_RejectsNonPositiveFrameDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LighthouseInfo.Create(0, CreateFrames(), Font, Font, frameDelayMilliseconds: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LighthouseInfo.Create(0, CreateFrames(), Font, Font, frameDelayMilliseconds: -1));
    }

    [Fact]
    public void Create_PutsTheLighthouseAndTheCountInTheBox() => Assert.True(TryWithArchives(
        () => LighthouseInfo.Create(3, CreateFrames(), Font, Font),
        box =>
        {
            Assert.Equal(LighthouseInfo.Header, box.Header);
            Assert.Equal(LighthouseInfo.Body, box.Body);

            // The tower is a 5x2 tile picture plus the height of its tallest storey, and the count follows it.
            Assert.Equal(2, box.Elements.Count);

            CustomAnimationDialogElement lighthouse = Assert.IsType<CustomAnimationDialogElement>(box.Elements[0]);
            Assert.Equal(5 * LighthouseInfo.TileWidth, lighthouse.Area.Width);
            Assert.Equal((2 * LighthouseInfo.TileWidth) + 32, lighthouse.Area.Height);
            Assert.Equal(0, lighthouse.FrameNumber);

            Assert.IsType<TextDialogElement>(box.Elements[1]);
        }));

    [Fact]
    public void Create_AcceptsTranslatedText() => Assert.True(TryWithArchives(
        () => LighthouseInfo.Create(1, CreateFrames(), Font, Font, "灯塔", "每控制一座灯塔，你的船只每天就能航行得更远。"),
        box =>
        {
            Assert.Equal("灯塔", box.Header);
            Assert.Equal("每控制一座灯塔，你的船只每天就能航行得更远。", box.Body);
        }));

    /// <summary>Builds <c>OBJNMUL2</c> frames of the tiles the lighthouse is assembled from.</summary>
    /// <param name="count">Number of frames; every frame the reference reads is a 32x32 tile.</param>
    private static List<Sprite> CreateFrames(int count = 74)
    {
        List<Sprite> frames = [];

        for (int index = 0; index < count; ++index)
        {
            PixelBuffer image = new(LighthouseInfo.TileWidth, LighthouseInfo.TileWidth);
            image.Fill((byte)(index + 1));
            frames.Add(new Sprite(image, 0, 0));
        }

        return frames;
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
