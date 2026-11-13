using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

public sealed class MessageBoxTests
{
    private const byte HeaderColor = 200;
    private const byte BodyColor = 100;
    private const byte ChromeColor = 7;

    [Fact]
    public void Create_MeasuresTheTextLikeGetDialogHeight()
    {
        // Header height adds textOffsetY, the body follows with another textOffsetY between them:
        // 15 + 10 for the title, 15 + 10 for the message, which still fits the smallest box.
        MessageBox box = MessageBox.Create(
            "New Game",
            "Start a single or multi-player game.",
            CreateChrome(),
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor));

        Assert.Equal(new Rect(198, 205, 244, 70), box.Area);
        Assert.Equal(0, box.Frame.MiddleFragmentCount);
    }

    [Fact]
    public void Create_WrapsTheTextToTheAreaWidth()
    {
        // 121 characters of 6 pixels do not fit one 244 pixel line, so they take four of them:
        // 25 (title) + 60 (four body lines) + 10 = 95 pixels, one middle slice of 25.
        string body = new('A', 121);
        FakeGlyphSource font = new(BodyColor);

        MessageBox box = MessageBox.Create("Title", body, CreateChrome(), font, font);

        Assert.Equal(4, GlyphText.WrapLines(font, body, FrameBox.AreaWidth).Count);
        Assert.Equal(95, box.Area.Height);
        Assert.Equal(25, box.Frame.MiddleFragmentHeight);
        Assert.Equal(1, box.Frame.MiddleFragmentCount);
    }

    [Fact]
    public void Create_WithoutHeaderStartsTheBodyAtTheTop()
    {
        MessageBox box = MessageBox.Create(
            string.Empty, "Hello.", CreateChrome(), new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor));

        Assert.Equal(string.Empty, box.Header);
        Assert.Equal(70, box.Area.Height);
    }

    [Fact]
    public void Create_WithoutTextKeepsTheSmallestBox()
    {
        MessageBox box = MessageBox.Create(
            string.Empty, string.Empty, CreateChrome(), new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor));

        Assert.Equal(new Rect(198, 205, 244, 70), box.Area);
        Assert.Equal(new Rect(159, 141, 305, 180), box.Frame.FrameArea);
    }

    [Fact]
    public void Draw_CentersTheHeaderAndPutsTheBodyBelowIt()
    {
        // Two 12 pixel wide words are centered in the 244 pixel area, the body starts one
        // textOffsetY below the title block.
        MessageBox box = MessageBox.Create(
            "AB", "CD", CreateChrome(), new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor));

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        box.Draw(target);

        int x = box.Area.X + ((FrameBox.AreaWidth - 12) / 2);
        int headerTop = box.Area.Y + MessageBox.TextOffsetY;

        Assert.Equal(HeaderColor, target[x, headerTop]);
        Assert.Equal(HeaderColor, target[x + 11, headerTop + 14]);
        Assert.Equal(HeaderColor, target[x + 6, headerTop]);

        // Nothing of the title is drawn outside its centered box.
        Assert.NotEqual(HeaderColor, target[x - 1, headerTop]);
        Assert.NotEqual(HeaderColor, target[x + 12, headerTop]);

        // The body sits 15 (one line of the title) + 10 (the gap) pixels below the title.
        Assert.Equal(BodyColor, target[x, headerTop + 25]);
        Assert.NotEqual(BodyColor, target[x, headerTop + 24]);
    }

    [Fact]
    public void Draw_PutsTheBodyAtTheTopWhenThereIsNoHeader()
    {
        MessageBox box = MessageBox.Create(
            string.Empty, "CD", CreateChrome(), new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor));

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        box.Draw(target);

        int x = box.Area.X + ((FrameBox.AreaWidth - 12) / 2);

        Assert.Equal(BodyColor, target[x, box.Area.Y + MessageBox.TextOffsetY]);
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

    /// <summary>
    /// A glyph source with the metrics of <c>FONT.ICN</c> (15 pixel line, 6 pixel advance) whose
    /// glyphs are solid blocks, so a test can tell the title from the body by colour alone.
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

        public int GetAdvance(char character) => character == GlyphText.SpaceCharacter ? SpaceWidth : 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
