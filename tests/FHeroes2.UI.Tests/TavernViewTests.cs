using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="TavernView"/>: the barkeep of <c>TAVWIN.ICN</c> over the rumor of the week and one OK button,
/// <c>Castle::_openTavern()</c> (<c>castle_tavern.cpp:39-54</c>).
/// </summary>
public sealed class TavernViewTests
{
    private const byte HeaderColor = 90;
    private const byte BodyColor = 200;
    private const byte ChromeColor = 7;

    private static readonly LocalizedString Rumor = new(World.TruthRumor);

    [Fact]
    public void TheDelayAndTheFramesAreTheOnesOfTheReference()
    {
        // Game::DelayType::CASTLE_TAVERN_DELAY (game_delays.cpp:52) and the { 0, 1 } backgrounds of the animation.
        Assert.Equal(75, TavernView.FrameDelayMilliseconds);
        Assert.Equal(new[] { 0, 1 }, TavernView.BackgroundFrames);
        Assert.Equal(2, TavernView.FirstAnimatedFrame);
        Assert.Equal(20, TavernView.AnimatedFrameCount);
    }

    [Fact]
    public void AFileWithoutAnimationFramesIsRefused()
    {
        // Only the two backgrounds: the reference's ICN::getAnimatedIcnIndex() would ask for frame 2 and up.
        Assert.Null(TavernView.TryCreate(
            CreateDialog(), CreateTavwin(2), CreateChrome(), new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor), new FakeArtwork()));
        Assert.Null(TavernView.TryCreate(
            CreateDialog(), [], CreateChrome(), new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor), new FakeArtwork()));
    }

    [Fact]
    public void TheBoxHoldsTheBarkeepAndTheTextUnderTheTitle()
    {
        MessageBox box = TavernView.TryCreate(
            CreateDialog(), CreateTavwin(22), CreateChrome(), new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor), new FakeArtwork())!;

        Assert.Equal(TavernDialog.Title, box.Header);
        Assert.Equal(string.Empty, box.Body);
        Assert.Equal(2, box.Elements.Count);

        // The barkeep is the 188x100 background of frame 0, not the smaller 182x94 of frame 1.
        AnimationDialogElement barkeep = Assert.IsType<AnimationDialogElement>(box.Elements[0]);
        Assert.Equal(new Size(188, 100), barkeep.Area);
        Assert.IsType<TextDialogElement>(box.Elements[1]);

        // The reference passes both paragraphs as one element, so the element fills the 244 pixel area.
        Assert.Equal(FrameBox.AreaWidth, box.Elements[1].Area.Width);
    }

    [Fact]
    public void TheOnlyButtonIsACentredOkay()
    {
        MessageBox box = TavernView.TryCreate(
            CreateDialog(), CreateTavwin(22), CreateChrome(), new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor), new FakeArtwork())!;

        Assert.NotNull(box.Buttons);
        Assert.Equal(1, box.Buttons.Count);
        Assert.Equal(DialogButton.Ok, box.Buttons.ValueOf(0));

        Rect button = box.Buttons[0].Area;

        Assert.Equal(box.Area.X + ((box.Area.Width - button.Width) / 2), button.X);
        Assert.Equal(box.Area.Y + box.Area.Height - button.Height, button.Y);
    }

    [Fact]
    public void APressAndReleaseOnOkayAnswersTheButton()
    {
        MessageBox box = TavernView.TryCreate(
            CreateDialog(), CreateTavwin(22), CreateChrome(), new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor), new FakeArtwork())!;

        Rect button = box.Buttons![0].Area;
        var centre = new Point(button.X + (button.Width / 2), button.Y + (button.Height / 2));

        box.SetCursor(centre);

        Assert.Equal(DialogButton.None, box.SetLeftButton(true));
        Assert.Equal(DialogButton.Ok, box.SetLeftButton(false));
    }

    [Fact]
    public void TheBarkeepMovesOnAfterOneDelay()
    {
        MessageBox box = TavernView.TryCreate(
            CreateDialog(), CreateTavwin(22), CreateChrome(), new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor), new FakeArtwork())!;

        var barkeep = (AnimationDialogElement)box.Elements[0];
        PixelBuffer target = new(640, 480);

        Assert.Equal(0, barkeep.FrameNumber);
        Assert.False(box.Update(target, TimeSpan.FromMilliseconds(TavernView.FrameDelayMilliseconds - 1)));
        Assert.Equal(0, barkeep.FrameNumber);

        Assert.True(box.Update(target, TimeSpan.FromMilliseconds(1)));
        Assert.Equal(1, barkeep.FrameNumber);
    }

    [Fact]
    public void EveryArgumentIsRequired()
    {
        TavernDialog dialog = CreateDialog();
        IReadOnlyList<Sprite> tavwin = CreateTavwin(22);
        FrameBoxChrome chrome = CreateChrome();
        IGlyphSource font = new FakeGlyphSource(BodyColor);
        FakeArtwork artwork = new();

        Assert.Throws<ArgumentNullException>(() =>
            TavernView.TryCreate(null!, tavwin, chrome, font, font, artwork));
        Assert.Throws<ArgumentNullException>(() =>
            TavernView.TryCreate(dialog, null!, chrome, font, font, artwork));
        Assert.Throws<ArgumentNullException>(() =>
            TavernView.TryCreate(dialog, tavwin, null!, font, font, artwork));
        Assert.Throws<ArgumentNullException>(() =>
            TavernView.TryCreate(dialog, tavwin, chrome, null!, font, artwork));
        Assert.Throws<ArgumentNullException>(() =>
            TavernView.TryCreate(dialog, tavwin, chrome, font, null!, artwork));
        Assert.Throws<ArgumentNullException>(() =>
            TavernView.TryCreate(dialog, tavwin, chrome, font, font, null!));
    }

    private static TavernDialog CreateDialog() => TavernDialog.Create(Rumor);

    /// <summary>
    /// <c>TAVWIN.ICN</c> stand-ins: frame 0 and 1 are the backgrounds of the reference's sizes, the rest are
    /// 20 animated frames of the barkeep.
    /// </summary>
    private static IReadOnlyList<Sprite> CreateTavwin(int frameCount)
    {
        Sprite[] sprites = new Sprite[frameCount];

        for (int index = 0; index < frameCount; ++index)
        {
            Size size = index switch
            {
                0 => new Size(188, 100),
                1 => new Size(182, 94),
                _ => new Size(178, 88),
            };

            sprites[index] = new Sprite(size.Width, size.Height);
        }

        return sprites;
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
            image.Fill(ChromeColor);
            pieces[index] = new Sprite(image, 0, 0);
        }

        return FrameBoxChrome.TryCreate(pieces)!;
    }

    /// <summary>The OK button is 60x25 in the test, so its placement is easy to check.</summary>
    private sealed class FakeArtwork : IDialogButtonArtworkSource
    {
        public DialogButtonArtwork Get(DialogButton button) =>
            new(new Sprite(60, 25), new Sprite(60, 25));
    }

    /// <summary>
    /// A glyph source with the metrics of <c>FONT.ICN</c> (15 pixel line, 6 pixel advance) whose glyphs are
    /// solid blocks, so every character measures the same and the layout is predictable.
    /// </summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
        {
            PixelBuffer image = new(6, LineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight => 15;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
