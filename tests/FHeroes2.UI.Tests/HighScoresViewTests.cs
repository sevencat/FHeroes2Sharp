using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Highscores;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The high score screen, <c>redrawHighScoreScreen()</c> and <c>redrawMonstersAnimation()</c>
/// (<c>game_highscores.cpp:93-243</c>).
/// </summary>
public sealed class HighScoresViewTests
{
    private static readonly Size Screen = new(640, 480);
    private static readonly Size BackgroundSize = new(640, 480);
    private static readonly Size TitleSize = new(200, 20);
    private static readonly Size ButtonSize = new(28, 60);
    private static readonly Size MonsterSize = new(32, 32);

    private const int LineHeight = 12;
    private const int Advance = 6;

    private const byte BackgroundColor = 10;
    private const byte StandardTitleColor = 11;
    private const byte CampaignTitleColor = 12;
    private const byte OtherColor = 13;
    private const byte OtherPressedColor = 14;
    private const byte CampaignButtonColor = 15;
    private const byte CampaignButtonPressedColor = 16;
    private const byte ExitColor = 17;
    private const byte ExitPressedColor = 18;
    private const byte TextColor = 20;
    private const byte YellowColor = 21;

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheArtwork()
    {
        Assert.Null(HighScoresView.TryCreate(
            HighScoreMode.Standard, [], null, Fonts(), screen: Screen));

        Assert.Null(HighScoresView.TryCreate(
            HighScoreMode.Standard, [], HighScoresArtwork.TryCreate(null, null, null), Fonts(), screen: Screen));
    }

    [Fact]
    public void Area_IsTheBackgroundCentredOnTheScreen()
    {
        HighScoresView view = Create();

        Assert.Equal(new Rect(0, 0, 640, 480), view.Area);

        HighScoresView wide = Create(screen: new Size(800, 600));

        Assert.Equal(new Rect(80, 60, 640, 480), wide.Area);
    }

    [Fact]
    public void Draw_PaintsTheBackgroundAndTheTitle()
    {
        HighScoresView view = Create();
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        Assert.Equal(BackgroundColor, canvas[5, 5]);

        // 标题在 top + 50 / top + 31，参考不把标题 sprite 自己的偏移算进去。
        Assert.Equal(
            StandardTitleColor,
            canvas[HighScoresLayout.TitleOffsetX + 1, HighScoresLayout.TitleOffsetY + 1]);
    }

    [Fact]
    public void Draw_UsesTheCampaignTitleForTheCampaignTable()
    {
        HighScoresView view = Create(mode: HighScoreMode.Campaign, entries: CampaignEntries());
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        Assert.Equal(
            CampaignTitleColor,
            canvas[HighScoresLayout.TitleOffsetX + 1, HighScoresLayout.TitleOffsetY + 1]);
    }

    [Fact]
    public void Draw_PutsTheFourColumnsAtTheReferenceOffsets()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        Assert.Equal(TextColor, canvas[HighScoresLayout.PlayerNameOffsetX + 1, HighScoresLayout.InitialEntryOffsetY + 2]);
        Assert.Equal(TextColor, canvas[HighScoresLayout.ScenarioNameOffsetX + 1, HighScoresLayout.InitialEntryOffsetY + 2]);
        Assert.Equal(TextColor, canvas[HighScoresLayout.DayCountOffsetX + 1, HighScoresLayout.InitialEntryOffsetY + 2]);
        Assert.Equal(TextColor, canvas[HighScoresLayout.RatingOffsetX + 1, HighScoresLayout.InitialEntryOffsetY + 2]);
    }

    [Fact]
    public void Draw_WalksTheRowsByTheReferenceStep()
    {
        HighScoresView view = Create(entries:
        [
            Row("First", "One", 10, 150),
            Row("Second", "Two", 20, 140),
        ]);
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        int second = HighScoresLayout.InitialEntryOffsetY + HighScoresLayout.EntryStepY;

        Assert.Equal(TextColor, canvas[HighScoresLayout.PlayerNameOffsetX + 1, HighScoresLayout.InitialEntryOffsetY + 2]);
        Assert.Equal(TextColor, canvas[HighScoresLayout.PlayerNameOffsetX + 1, second + 2]);
    }

    [Fact]
    public void Draw_UsesTheYellowFaceForTheRegisteredRow()
    {
        HighScoresView view = Create(
            entries: [Row("Roland", "Beltway", 70, 150), Row("Tsabu", "Deathgate", 80, 140)],
            selectedIndex: 0);
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        int second = HighScoresLayout.InitialEntryOffsetY + HighScoresLayout.EntryStepY;

        Assert.Equal(YellowColor, canvas[HighScoresLayout.PlayerNameOffsetX + 1, HighScoresLayout.InitialEntryOffsetY + 2]);
        Assert.Equal(TextColor, canvas[HighScoresLayout.PlayerNameOffsetX + 1, second + 2]);
    }

    [Fact]
    public void Draw_PutsTheCreatureOfTheScoreInTheLastColumn()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        Assert.Equal(MonsterColor(150), canvas[
            HighScoresLayout.MonsterOffsetX + (MonsterSize.Width / 2),
            HighScoresLayout.MonsterOffsetY + (MonsterSize.Height / 2)]);
    }

    [Fact]
    public void Draw_UsesTheDayTableForCampaigns()
    {
        HighScoresView view = Create(mode: HighScoreMode.Campaign, entries: [Row("Astra", "Roland", 700, 700)]);
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        Assert.Equal(MonsterColor(700, HighScoreMode.Campaign), canvas[
            HighScoresLayout.MonsterOffsetX + (MonsterSize.Width / 2),
            HighScoresLayout.MonsterOffsetY + (MonsterSize.Height / 2)]);
    }

    [Fact]
    public void Draw_PutsTheTwoButtonsAtTheirOffsets()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);
        PixelBuffer canvas = Canvas();

        view.Draw(canvas);

        Assert.Equal(OtherColor, canvas[HighScoresLayout.OtherButtonOffsetX + 2, HighScoresLayout.ButtonOffsetY + 2]);

        int exitX = BackgroundSize.Width - HighScoresLayout.ExitButtonRightMargin;

        Assert.Equal(ExitColor, canvas[exitX + 2, HighScoresLayout.ButtonOffsetY + 2]);
    }

    [Fact]
    public void TheSwitchButtonSwapsItsArtworkWithTheTable()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);
        PixelBuffer canvas = Canvas();

        // 参考的按钮写的是当前榜的名字：标准榜配 STANDARD，战役榜配 CAMPAIGN。
        view.Draw(canvas);
        Assert.Equal(OtherColor, canvas[HighScoresLayout.OtherButtonOffsetX + 2, HighScoresLayout.ButtonOffsetY + 2]);

        view.SetTable(HighScoreMode.Campaign, CampaignEntries());

        canvas = Canvas();
        view.Draw(canvas);

        Assert.Equal(CampaignButtonColor, canvas[HighScoresLayout.OtherButtonOffsetX + 2, HighScoresLayout.ButtonOffsetY + 2]);
        Assert.Equal(HighScoreMode.Campaign, view.Mode);
    }

    [Fact]
    public void ClickingTheExitButtonAsksTheCallerToClose()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);
        Point exit = Center(view.ExitButton.Area);

        Assert.Equal(HighScoresAction.None, view.SetLeftButton(true, exit));
        Assert.Equal(HighScoresAction.Exit, view.SetLeftButton(false, exit));
    }

    [Fact]
    public void ClickingTheSwitchButtonAsksForTheOtherTable()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);
        Point other = Center(view.OtherButton.Area);

        Assert.Equal(HighScoresAction.None, view.SetLeftButton(true, other));
        Assert.Equal(HighScoresAction.SwitchMode, view.SetLeftButton(false, other));
    }

    [Fact]
    public void AClickThatStartsOnOneButtonAndEndsOnAnotherDoesNothing()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);

        view.SetLeftButton(true, Center(view.ExitButton.Area));

        Assert.Equal(HighScoresAction.None, view.SetLeftButton(false, Center(view.OtherButton.Area)));
    }

    [Fact]
    public void TheSwitchButtonIsDisabledWithoutTheCampaigns()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)], campaignAvailable: false);
        Point other = Center(view.OtherButton.Area);

        Assert.False(view.OtherButton.IsEnabled);
        Assert.Equal(HighScoresAction.None, view.SetLeftButton(true, other));
        Assert.Equal(HighScoresAction.None, view.SetLeftButton(false, other));
        Assert.Equal(HighScoresHelpTarget.None, view.ProcessRightClick(other));
    }

    [Fact]
    public void ARightClickOnAButtonAsksForItsHelp()
    {
        HighScoresView view = Create(entries: [Row("Roland", "Beltway", 70, 150)]);

        Assert.Equal(HighScoresHelpTarget.Exit, view.ProcessRightClick(Center(view.ExitButton.Area)));
        Assert.Equal(HighScoresHelpTarget.Other, view.ProcessRightClick(Center(view.OtherButton.Area)));
        Assert.Equal(HighScoresHelpTarget.None, view.ProcessRightClick(new Point(5, 5)));
    }

    [Fact]
    public void AnInfiniteNameIsCutToOneColumn()
    {
        IGlyphSource font = new FakeGlyphSource(TextColor, LineHeight);
        string name = new('W', 40);

        string fitted = HighScoresView.FitToOneRow(font, name, HighScoresLayout.PlayerNameWidth);

        Assert.Equal(26, fitted.Length);
        Assert.EndsWith(GlyphText.TruncationSymbol, fitted);
        Assert.True(GlyphText.Measure(font, fitted) <= HighScoresLayout.PlayerNameWidth);
    }

    [Fact]
    public void TheCreatureFramesStartNineApart()
    {
        Assert.Equal(0, HighScoresView.MonsterFrameBase(MonsterType.Peasant));
        Assert.Equal(HighScoresArtwork.FramesPerMonster, HighScoresView.MonsterFrameBase(MonsterType.Archer));

        // 冠军是兵种表里的第九个，索引八，于是第一帧是 8 × 9。
        Assert.Equal(8 * HighScoresArtwork.FramesPerMonster, HighScoresView.MonsterFrameBase(MonsterType.Champion));
    }

    [Fact]
    public void TheAnimationFrameFollowsTheReferenceExpression()
    {
        byte expected = MonsterAnimationSequence.Frames[(0 + 0 + (3 * HighScoresLayout.EntryStepY) + 700 + 4) % 15];

        Assert.Equal(expected, HighScoresView.AnimationFrame(0, 0, 3, 700, 4));

        byte shifted = MonsterAnimationSequence.Frames[(10 + 20 + (0 * HighScoresLayout.EntryStepY) + 1 + 1) % 15];

        Assert.Equal(shifted, HighScoresView.AnimationFrame(10, 20, 0, 1, 1));
    }

    private static HighScoresView Create(
        HighScoreMode mode = HighScoreMode.Standard,
        IReadOnlyList<HighscoreData>? entries = null,
        int selectedIndex = -1,
        bool campaignAvailable = true,
        Size? screen = null) =>
        HighScoresView.TryCreate(
            mode,
            entries ?? [Row("Roland", "Beltway", 70, 150)],
            Artwork(),
            Fonts(),
            selectedIndex,
            campaignAvailable,
            screen ?? Screen)!;

    private static HighscoreData Row(string playerName, string scenarioName, uint dayCount, uint rating) =>
        new(HighscoreData.EnglishLanguageAbbreviation, playerName, scenarioName, 1000, dayCount, rating, 0);

    private static List<HighscoreData> CampaignEntries() => [Row("Antoine", "Roland", 600, 600)];

    private static PixelBuffer Canvas()
    {
        PixelBuffer canvas = new(Screen.Width, Screen.Height);
        canvas.Fill(0);

        return canvas;
    }

    private static HighScoresFonts Fonts() =>
        new(new FakeGlyphSource(TextColor, LineHeight), new FakeGlyphSource(YellowColor, LineHeight));

    /// <summary>Builds the three archives so that every creature and every frame has a colour of its own.</summary>
    private static HighScoresArtwork Artwork()
    {
        List<Sprite> hiscore = [];
        List<Sprite> monsters = [];

        hiscore.Add(Block(ButtonSize, CampaignButtonColor));
        hiscore.Add(Block(ButtonSize, CampaignButtonPressedColor));
        hiscore.Add(Block(ButtonSize, OtherColor));
        hiscore.Add(Block(ButtonSize, OtherPressedColor));
        hiscore.Add(Block(ButtonSize, ExitColor));
        hiscore.Add(Block(ButtonSize, ExitPressedColor));
        hiscore.Add(Block(TitleSize, StandardTitleColor));
        hiscore.Add(Block(TitleSize, CampaignTitleColor));

        for (int group = 0; group < 66; ++group)
        {
            for (int frame = 0; frame < HighScoresArtwork.FramesPerMonster; ++frame)
            {
                // 同一只生物的九帧同色：一行画的是哪只生物，看颜色就知道；具体帧号另有测试。
                monsters.Add(Block(MonsterSize, (byte)(1 + group)));
            }
        }

        return HighScoresArtwork.TryCreate([Block(BackgroundSize, BackgroundColor)], hiscore, monsters)!;
    }

    private static byte MonsterColor(uint rating, HighScoreMode mode = HighScoreMode.Standard)
    {
        MonsterType monster = HighScoresView.MonsterFor(mode, rating);

        return (byte)(1 + (HighScoresView.MonsterFrameBase(monster) / HighScoresArtwork.FramesPerMonster));
    }

    private static Sprite Block(Size size, byte color)
    {
        PixelBuffer image = new(size.Width, size.Height);
        image.Fill(color);

        return new Sprite(image, 0, 0);
    }

    private static Point Center(in Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    /// <summary>
    /// A glyph source with fixed metrics whose glyphs are solid blocks, so a test can tell one text block from
    /// another by colour alone.
    /// </summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;
        private readonly int _advance;

        public FakeGlyphSource(byte color, int lineHeight, int advance = Advance)
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
