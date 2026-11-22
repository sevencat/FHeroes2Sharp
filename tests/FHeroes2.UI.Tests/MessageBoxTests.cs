using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="MessageBox"/>: the text measurement of <c>GetDialogHeight()</c>, the element
/// packing and the drawing order.
/// </summary>
/// <remarks>
/// The box is drawn in a <see cref="FrameBox"/>, which reads <c>BUYBUILD.ICN</c> itself, so every test
/// that creates one needs the installed archives and returns when the machine has none. The expected
/// numbers were derived from that archive.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MessageBoxTests
{
    private const byte HeaderColor = 200;
    private const byte BodyColor = 100;
    private const byte ElementColor = 50;

    [Fact]
    public void Create_MeasuresTheTextLikeGetDialogHeight() => Assert.True(TryWithArchives(
        () => MessageBox.Create(
            "New Game",
            "Start a single or multi-player game.",
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor)),
        box =>
        {
            // Header height adds textOffsetY, the body follows with another textOffsetY between them:
            // 15 + 10 for the title, 15 + 10 for the message, which still fits the smallest box.
            Assert.Equal(new Rect(198, 205, 244, 70), box.Area);
            Assert.Equal(0, box.Frame.MiddleFragmentCount);
        }));

    [Fact]
    public void Create_WrapsTheTextToTheAreaWidth() => Assert.True(TryWithArchives(
        () =>
        {
            FakeGlyphSource font = new(BodyColor);

            return MessageBox.Create("Title", new string('A', 121), font, font);
        },
        box =>
        {
            // 121 characters of 6 pixels do not fit one 244 pixel line, so they take four of them:
            // 25 (title) + 60 (four body lines) + 10 = 95 pixels, one middle slice of 25.
            FakeGlyphSource font = new(BodyColor);

            Assert.Equal(4, GlyphText.WrapLines(font, new string('A', 121), FrameBox.AreaWidth).Count);
            Assert.Equal(95, box.Area.Height);
            Assert.Equal(25, box.Frame.MiddleFragmentHeight);
            Assert.Equal(1, box.Frame.MiddleFragmentCount);
        }));

    [Fact]
    public void Create_WithoutHeaderStartsTheBodyAtTheTop() => Assert.True(TryWithArchives(
        () => MessageBox.Create(string.Empty, "Hello.", new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor)),
        box =>
        {
            Assert.Equal(string.Empty, box.Header);
            Assert.Equal(70, box.Area.Height);
        }));

    [Fact]
    public void Create_WithoutTextKeepsTheSmallestBox() => Assert.True(TryWithArchives(
        () => MessageBox.Create(string.Empty, string.Empty, new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor)),
        box =>
        {
            Assert.Equal(new Rect(198, 205, 244, 70), box.Area);
            Assert.Equal(new Rect(159, 141, 305, 180), box.Frame.FrameArea);
        }));

    [Fact]
    public void Draw_CentersTheHeaderAndPutsTheBodyBelowIt() => Assert.True(TryWithArchives(
        () => MessageBox.Create("AB", "CD", new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor)),
        box =>
        {
            // Two 12 pixel wide words are centered in the 244 pixel area, the body starts one
            // textOffsetY below the title block.
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
        }));

    [Fact]
    public void Draw_PutsTheBodyAtTheTopWhenThereIsNoHeader() => Assert.True(TryWithArchives(
        () => MessageBox.Create(string.Empty, "CD", new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor)),
        box =>
        {
            PixelBuffer target = new(640, 480);
            target.Fill(0);

            box.Draw(target);

            int x = box.Area.X + ((FrameBox.AreaWidth - 12) / 2);

            Assert.Equal(BodyColor, target[x, box.Area.Y + MessageBox.TextOffsetY]);
        }));

    [Fact]
    public void Create_PacksElementsIntoRowsOfTheAreaWidth() => Assert.True(TryWithArchives(
        () => MessageBox.Create(
            "AB",
            "CD",
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor),
            [new FakeDialogElement(100, 40), new FakeDialogElement(100, 40), new FakeDialogElement(100, 40)]),
        box =>
        {
            // 100 pixel wide elements: two fit ((100 + 10) * 2 = 220 <= 244), the third does not (330), so the box is
            // 50 pixels of text + two rows of 40 with the 10 pixel gap each + one more gap below the body = 160.
            Assert.Equal(160, box.Area.Height);
            Assert.Equal(3, box.ElementAreas.Count);

            // Row one: 44 free pixels split into three 14 pixel gaps, so the first element starts at 14 and the second
            // one element plus a gap further; the lone element of row two gets the whole 144 pixels split in two, i.e.
            // 72 a side, and starts one gap plus one row below the first.
            Assert.Equal(new Rect(box.Area.X + 14, box.Area.Y + 70, 100, 40), box.ElementAreas[0]);
            Assert.Equal(new Rect(box.Area.X + 128, box.Area.Y + 70, 100, 40), box.ElementAreas[1]);
            Assert.Equal(new Rect(box.Area.X + 72, box.Area.Y + 120, 100, 40), box.ElementAreas[2]);
        }));

    [Fact]
    public void Create_BottomAlignsNarrowElementsInsideTheirRow() => Assert.True(TryWithArchives(
        () => MessageBox.Create(
            string.Empty,
            "CD",
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor),
            [new FakeDialogElement(100, 40), new FakeDialogElement(50, 20)]),
        box =>
        {
            // A 40 pixel tall row holds a 100x40 and a 100x20 element; the short one sits at the bottom of the row and
            // is centred inside its own slot rather than the row. The row starts at the 25 pixel text block plus two
            // gaps and the second slot starts at 14 + 100 + 14, so the 50 pixel element is centred 25 pixels further.
            Assert.Equal(new Rect(box.Area.X + 153, box.Area.Y + 65, 50, 20), box.ElementAreas[1]);
        }));

    [Fact]
    public void Draw_DrawsTheElementsWhereItPlacedThem() => Assert.True(TryWithArchives(
        () => MessageBox.Create(
            string.Empty,
            "CD",
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor),
            [new FakeDialogElement(100, 40, ElementColor)]),
        box =>
        {
            PixelBuffer target = new(640, 480);
            target.Fill(0);

            box.Draw(target);

            Rect rect = box.ElementAreas[0];

            Assert.Equal(ElementColor, target[rect.X, rect.Y]);
            Assert.Equal(ElementColor, target[rect.X + rect.Width - 1, rect.Y + rect.Height - 1]);
            Assert.NotEqual(ElementColor, target[rect.X - 1, rect.Y]);
            Assert.NotEqual(ElementColor, target[rect.X + rect.Width, rect.Y + rect.Height - 1]);
        }));

    [Fact]
    public void Create_RejectsNullElements()
    {
        // The check runs before the frame is composed, so the test does not need the archives.
        Assert.Throws<ArgumentException>(() => MessageBox.Create(
            "AB",
            "CD",
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor),
            [null!]));
    }

    [Fact]
    public void Show_ThrowsWhenNoEngineIsAttached() => Assert.True(TryWithArchives(
        () => MessageBox.Create("Title", "Body", new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor)),
        _ => Assert.Throws<InvalidOperationException>(() => MessageBox.Show(
            new PixelBuffer(640, 480),
            "Title",
            "Body",
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor),
            events: new LocalEvent(() => 0)))));

    [Fact]
    public void Show_ReturnsNoneWhenTheEngineStops() => Assert.True(TryWithArchives(
        () => MessageBox.Create("Title", "Body", new FakeGlyphSource(HeaderColor), new FakeGlyphSource(BodyColor)),
        _ =>
        {
            LocalEvent events = new(() => 0) { Engine = new StoppingEngine() };

            Assert.Equal(
                DialogButton.None,
                MessageBox.Show(
                    new PixelBuffer(640, 480),
                    "Title",
                    "Body",
                    new FakeGlyphSource(HeaderColor),
                    new FakeGlyphSource(BodyColor),
                    events: events));
        }));

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

    /// <summary>An event engine that has nothing to poll and asks the loop to stop.</summary>
    private sealed class StoppingEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            return false;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    /// <summary>An element of a fixed size that paints every pixel of its area with one colour.</summary>
    private sealed class FakeDialogElement : DialogElement
    {
        private readonly byte _color;

        public FakeDialogElement(int width, int height, byte color = ElementColor)
        {
            Area = new Size(width, height);
            _color = color;
        }

        public override void Draw(PixelBuffer target, Point offset)
        {
            for (int y = 0; y < Area.Height; ++y)
            {
                for (int x = 0; x < Area.Width; ++x)
                {
                    target[offset.X + x, offset.Y + y] = _color;
                }
            }
        }
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
