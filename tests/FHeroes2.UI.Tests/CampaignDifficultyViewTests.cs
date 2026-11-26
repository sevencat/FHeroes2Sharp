using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Campaign;
using FHeroes2.UI.Dialogs;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

[Collection(GlobalArchiveCollection.Name)]
public sealed class CampaignDifficultyViewTests
{
    private static readonly Size Screen = new(640, 480);

    private const int IconSize = CampaignDifficultyView.IconSize;
    private const int SelectionSize = 71;
    private const int OkayWidth = 60;
    private const int OkayHeight = 25;
    private const int LineHeight = 12;

    private const byte TextColor = 40;
    private const byte CaptionColor = 41;
    private const byte IconColor = 90;
    private const byte SelectionColor = 91;
    private const byte OkayColor = 92;

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheArtwork()
    {
        Assert.Null(Create(icons: [Icon(), Icon()]));
        Assert.Null(TryCreate(selection: null));
        Assert.Null(TryCreate(okayReleased: null));
        Assert.Null(TryCreate(okayPressed: null));
    }

    [Fact]
    public void TryCreate_KeepsTheBaseWidthForShortNames()
    {
        // "Decreased" is 54 pixels wide in the fake 6 pixel font, well inside the 85 pixels an icon keeps.
        CampaignDifficultyView view = Create()!;

        Assert.Equal(CampaignDifficultyView.BaseWidth, view.DialogWidth);
        Assert.Equal(CampaignDifficultyView.BaseWidth, view.ActiveArea.Width);
        Assert.Equal(ExpectActiveArea(ExpectedHeight(NarrowFont())).Width, view.ActiveArea.Width);
    }

    [Fact]
    public void TryCreate_WidensTheWindowForWideNames()
    {
        // 20 pixels per character makes "Decreased" 180 wide: 95 pixels too wide, three times over.
        FakeGlyphSource wide = new(TextColor, LineHeight, advance: 20);

        CampaignDifficultyView view = Create(font: wide)!;

        int widest = Math.Max(
            GlyphText.Measure(wide, CampaignDifficultyView.DecreasedName),
            Math.Max(
                GlyphText.Measure(wide, CampaignDifficultyView.DefaultName),
                GlyphText.Measure(wide, CampaignDifficultyView.IncreasedName)));

        Assert.Equal(180, widest);
        Assert.Equal(300 + ((widest - 85) * 3), view.DialogWidth);
        Assert.Equal(585, view.DialogWidth);
    }

    [Fact]
    public void TryCreate_MakesTheWindowAsTallAsTheLongestDescription()
    {
        CampaignDifficultyView view = Create()!;

        Assert.Equal(ExpectedHeight(NarrowFont()), view.ActiveArea.Height);
        Assert.Equal(ExpectActiveArea(ExpectedHeight(NarrowFont())).Height, view.ActiveArea.Height);
    }

    [Fact]
    public void TryCreate_CentresTheWindowOnTheScreen()
    {
        CampaignDifficultyView view = Create()!;

        Assert.Equal(ExpectActiveArea(ExpectedHeight(NarrowFont())), view.ActiveArea);
    }

    [Fact]
    public void TryCreate_SpreadsTheIconsByAThirdOfTheWidth()
    {
        CampaignDifficultyView view = Create()!;
        Rect roi = view.ActiveArea;
        int centre = roi.X + ((roi.Width - IconSize) / 2);
        int third = roi.Width / 3;

        Assert.Equal(new Point(centre - third, roi.Y + 40), IconPosition(view, 0));
        Assert.Equal(new Point(centre, roi.Y + 40), IconPosition(view, 1));
        Assert.Equal(new Point(centre + third, roi.Y + 40), IconPosition(view, 2));
    }

    [Fact]
    public void TryCreate_PutsTheSelectionBoxAroundTheIcons()
    {
        CampaignDifficultyView view = Create()!;

        for (int index = 0; index < 3; ++index)
        {
            Point icon = IconPosition(view, index);

            Assert.Equal(new Rect(icon.X - 3, icon.Y - 3, SelectionSize, SelectionSize),
                view.DifficultyAreas[index]);
        }
    }

    [Fact]
    public void TryCreate_PutsTheDescriptionUnderTheIcons()
    {
        CampaignDifficultyView view = Create()!;

        Assert.Equal(new Point(view.ActiveArea.X + 8, view.ActiveArea.Y + 140), view.DescriptionPosition);
        Assert.Equal(CampaignDifficultyView.BaseWidth - 16, view.DescriptionWidth);
    }

    [Fact]
    public void TryCreate_PutsTheOkayButtonAtTheBottomCentre()
    {
        CampaignDifficultyView view = Create()!;
        Rect roi = view.ActiveArea;

        Assert.Equal(new Rect(roi.X + ((roi.Width - OkayWidth) / 2), roi.Y + roi.Height - OkayHeight - 5,
            OkayWidth, OkayHeight), view.OkayArea);
    }

    [Fact]
    public void TryCreate_OpensOnTheGivenDifficulty()
    {
        Assert.Equal(CampaignDifficulty.Decreased,
            Create(currentDifficulty: CampaignDifficulty.Decreased)!.CurrentDifficulty);
        Assert.Equal(CampaignDifficulty.Increased,
            Create(currentDifficulty: CampaignDifficulty.Increased)!.CurrentDifficulty);
    }

    [Theory]
    [InlineData(CampaignDifficulty.Decreased, "Decreased")]
    [InlineData(CampaignDifficulty.Default, "Default")]
    [InlineData(CampaignDifficulty.Increased, "Increased")]
    public void GetName_AnswersTheThreeDifficulties(int difficulty, string name)
    {
        Assert.Equal(name, CampaignDifficultyView.GetName(difficulty));
        Assert.NotEmpty(CampaignDifficultyView.GetDescription(difficulty));
    }

    [Fact]
    public void GetName_IsEmptyForAnUnknownDifficulty()
    {
        Assert.Empty(CampaignDifficultyView.GetName(7));
        Assert.Empty(CampaignDifficultyView.GetDescription(7));
    }

    [Fact]
    public void SetLeftButton_PicksADifficultyWithoutClosing()
    {
        CampaignDifficultyView view = Create(currentDifficulty: CampaignDifficulty.Default)!;

        CampaignDifficultyChoice choice = Click(view, view.DifficultyAreas[0]);

        Assert.Equal(CampaignDifficultyAction.SelectDecreased, choice.Action);
        Assert.Equal(CampaignDifficulty.Decreased, choice.Difficulty);
        Assert.Equal(CampaignDifficulty.Decreased, view.CurrentDifficulty);

        Assert.Equal(CampaignDifficultyAction.SelectIncreased,
            Click(view, view.DifficultyAreas[2]).Action);
        Assert.Equal(CampaignDifficulty.Increased, view.CurrentDifficulty);
    }

    [Fact]
    public void SetLeftButton_AnswersOkay()
    {
        CampaignDifficultyView view = Create(currentDifficulty: CampaignDifficulty.Increased)!;

        view.SetLeftButton(true, CenterOf(view.OkayArea));

        CampaignDifficultyChoice choice = view.SetLeftButton(false, CenterOf(view.OkayArea));

        Assert.Equal(CampaignDifficultyAction.Okay, choice.Action);
        Assert.Equal(CampaignDifficulty.Increased, choice.Difficulty);
    }

    [Fact]
    public void SetLeftButton_IgnoresAReleaseOutsideTheArea()
    {
        CampaignDifficultyView view = Create()!;

        view.SetLeftButton(true, CenterOf(view.DifficultyAreas[0]));

        Assert.Equal(CampaignDifficultyAction.None,
            view.SetLeftButton(false, CenterOf(view.DifficultyAreas[2])).Action);
        Assert.Equal(CampaignDifficulty.Default, view.CurrentDifficulty);

        view.SetLeftButton(true, CenterOf(view.OkayArea));

        Assert.Equal(CampaignDifficultyAction.None,
            view.SetLeftButton(false, CenterOf(view.DifficultyAreas[1])).Action);
    }

    [Fact]
    public void ProcessRightClick_NamesTheBlocks()
    {
        CampaignDifficultyView view = Create()!;

        Assert.Equal(CampaignDifficultyHelpTarget.Okay, view.ProcessRightClick(CenterOf(view.OkayArea)));
        Assert.Equal(CampaignDifficultyHelpTarget.Decreased,
            view.ProcessRightClick(CenterOf(view.DifficultyAreas[0])));
        Assert.Equal(CampaignDifficultyHelpTarget.Default,
            view.ProcessRightClick(CenterOf(view.DifficultyAreas[1])));
        Assert.Equal(CampaignDifficultyHelpTarget.Increased,
            view.ProcessRightClick(CenterOf(view.DifficultyAreas[2])));
        Assert.Equal(CampaignDifficultyHelpTarget.None, view.ProcessRightClick(new Point(0, 0)));
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        Assert.Throws<ArgumentNullException>(() => Create()!.Draw(null!));
    }

    [Fact]
    public void Draw_PaintsTheIconsTheSelectionAndTheOkayButton()
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        CampaignDifficultyView view = Create()!;
        PixelBuffer canvas = new(Screen.Width, Screen.Height);
        canvas.Fill(0);

        GameAssets.Current = assets;

        try
        {
            view.Draw(canvas);
        }
        finally
        {
            GameAssets.Current = null;
        }

        // The selection box is a transparent framed sprite, so the icon under it stays visible.
        Point icon = IconPosition(view, 1);

        Assert.Equal(IconColor, canvas[icon.X + 32, icon.Y + 32]);
        Assert.Equal(SelectionColor, canvas[view.DifficultyAreas[1].X, view.DifficultyAreas[1].Y + 20]);
        Assert.Equal(OkayColor, canvas[view.OkayArea.X + 30, view.OkayArea.Y + 12]);

        // The title is centred in the window, ten pixels down.
        int captionWidth = GlyphText.Measure(new FakeGlyphSource(CaptionColor, LineHeight),
            CampaignDifficultyView.Title);

        Assert.Equal(CaptionColor, canvas[view.ActiveArea.X + ((view.ActiveArea.Width - captionWidth) / 2) + 1,
            view.ActiveArea.Y + 10 + 2]);
    }

    private static FakeGlyphSource NarrowFont() => new(TextColor, LineHeight);

    private static int ExpectedHeight(IGlyphSource font)
    {
        int width = CampaignDifficultyView.BaseWidth - 16;
        int tallest = Math.Max(
            DescriptionHeight(font, CampaignDifficultyView.DecreasedDescription, width),
            Math.Max(
                DescriptionHeight(font, CampaignDifficultyView.DefaultDescription, width),
                DescriptionHeight(font, CampaignDifficultyView.IncreasedDescription, width)));

        return tallest + CampaignDifficultyView.DescriptionOffsetY + CampaignDifficultyView.BottomMargin;
    }

    private static int DescriptionHeight(IGlyphSource font, string text, int width) =>
        GlyphText.WrapLines(font, text, width).Count * font.LineHeight;

    private static Rect ExpectActiveArea(int height) => new(
        (Screen.Width - CampaignDifficultyView.BaseWidth) / 2, (Screen.Height - height) / 2,
        CampaignDifficultyView.BaseWidth, height);

    private static CampaignDifficultyView? Create(
        IGlyphSource? font = null,
        IReadOnlyList<Sprite>? icons = null,
        int currentDifficulty = CampaignDifficulty.Default) =>
        CampaignDifficultyView.TryCreate(
            Screen,
            font ?? NarrowFont(),
            CaptionFont(),
            icons ?? [Icon(), Icon(), Icon()],
            Selection(),
            OkayFrame(),
            OkayFrame(),
            currentDifficulty);

    /// <summary>Composes the window with no fallbacks, so a test can hand in a missing piece.</summary>
    private static CampaignDifficultyView? TryCreate(
        IGlyphSource? font = null,
        IReadOnlyList<Sprite>? icons = null,
        Sprite? selection = null,
        Sprite? okayReleased = null,
        Sprite? okayPressed = null,
        int currentDifficulty = CampaignDifficulty.Default) =>
        CampaignDifficultyView.TryCreate(
            Screen, font ?? NarrowFont(), CaptionFont(), icons, selection,
            okayReleased ?? OkayFrame(), okayPressed ?? OkayFrame(), currentDifficulty);

    private static FakeGlyphSource CaptionFont() => new(CaptionColor, LineHeight);

    private static Point IconPosition(CampaignDifficultyView view, int index) =>
        new(view.DifficultyAreas[index].X + CampaignDifficultyView.SelectionOverhang,
            view.DifficultyAreas[index].Y + CampaignDifficultyView.SelectionOverhang);

    private static Point CenterOf(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static CampaignDifficultyChoice Click(CampaignDifficultyView view, Rect area)
    {
        view.SetLeftButton(true, CenterOf(area));

        return view.SetLeftButton(false, CenterOf(area));
    }

    private static Sprite Icon()
    {
        PixelBuffer image = new(IconSize, IconSize);
        image.Fill(IconColor);

        return new Sprite(image, 0, 0);
    }

    /// <summary>A framed selection box: the interior is transparent, so the icon shows through.</summary>
    private static Sprite Selection()
    {
        PixelBuffer image = new(SelectionSize, SelectionSize);
        image.Fill(SelectionColor);

        // A transform id of 1 is the reference's "leave the destination alone", which is how a two layer
        // sprite says "transparent" - a single layer one has no way to skip a pixel.
        byte[] transform = new byte[SelectionSize * SelectionSize];
        Array.Fill(transform, (byte)1);

        for (int x = 0; x < SelectionSize; ++x)
        {
            transform[x] = 0;
            transform[((SelectionSize - 1) * SelectionSize) + x] = 0;
        }

        for (int y = 0; y < SelectionSize; ++y)
        {
            transform[y * SelectionSize] = 0;
            transform[(y * SelectionSize) + SelectionSize - 1] = 0;
        }

        return new Sprite(image, 0, 0, transform);
    }

    private static Sprite OkayFrame()
    {
        PixelBuffer image = new(OkayWidth, OkayHeight);
        image.Fill(OkayColor);

        return new Sprite(image, 0, 0);
    }

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>
    /// A glyph source with fixed metrics whose glyphs are solid blocks, so a test can tell one text block from
    /// another by colour alone.
    /// </summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;
        private readonly int _advance;

        public FakeGlyphSource(byte color, int lineHeight, int advance = 6)
        {
            _advance = advance;
            LineHeight = lineHeight;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight { get; }

        public int SpaceWidth => _advance;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => _advance;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
