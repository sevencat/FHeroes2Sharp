using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The thieves' guild report: its layout and its painting (<c>dialog_thievesguild.cpp:242-486</c>).
/// </summary>
public sealed class ThievesGuildViewTests
{
    private const byte BackgroundColor = 10;
    private const byte FlagBase = 30;
    private const byte HeroWindowColor = 40;
    private const byte BarColor = 50;
    private const byte PortraitColor = 60;
    private const byte MonsterColor = 70;
    private const byte ExitReleasedColor = 80;
    private const byte ExitPressedColor = 81;
    private const byte SmallTextColor = 90;
    private const byte NormalTextColor = 91;

    private static readonly Size FlagSize = new(14, 18);
    private static readonly Size BarSize = new(640, 30);
    private static readonly Size ExitSize = new(40, 22);
    private static readonly Size PortraitSize = new(30, 40);
    private static readonly Size MonsterSize = new(32, 32);
    private static readonly Size HeroWindowSize = new(20, 30);

    [Fact]
    public void Layout_PrintsTheRowsWhereTheReferenceDoes()
    {
        ThievesGuildLayout layout = CreateLayout();

        Assert.Equal(31, layout.RowLabelY(ThievesGuildRow.Towns));
        Assert.Equal(55, layout.RowLabelY(ThievesGuildRow.Castles));
        Assert.Equal(247, layout.RowLabelY(ThievesGuildRow.Income));

        Assert.Equal(new Point(264, 31), layout.FlagsAnchor(ThievesGuildRow.Towns));
        Assert.Equal(new Point(264, 103), layout.FlagsAnchor(ThievesGuildRow.Gold));
        Assert.Equal(new Point(207 - 11, 103), layout.RowLabelAnchor(ThievesGuildRow.Gold, 11));

        Assert.Equal(new Point(264, 3), layout.OrdinalCentre(0));
        Assert.Equal(new Point(400, 3), layout.OrdinalCentre(2));
    }

    [Fact]
    public void Layout_CentresTheColumnsAndShiftsEveryOtherColourName()
    {
        ThievesGuildLayout layout = CreateLayout();

        Assert.Equal(new Point(264, 271), layout.ColorNameCentre(0));
        Assert.Equal(new Point(332, 286), layout.ColorNameCentre(1));
        Assert.Equal(new Point(400, 271), layout.ColorNameCentre(2));

        Assert.Equal(new Point(265, 305), layout.HeroIconCentre(0));
        Assert.Equal(new Point(239, 339), layout.HeroStatsAnchor(0));
        Assert.Equal(new Point(264, 397), layout.PersonalityCentre(0));

        // Half a normal line (17 / 2) below the label.
        Assert.Equal(new Point(264, 437), layout.MonsterCentre(0));

        Assert.Equal(new Point(259, 31), ThievesGuildLayout.Centre(new Point(264, 31), 11));
    }

    [Fact]
    public void Layout_CutsTheBarAroundTheExitButton()
    {
        ThievesGuildLayout layout = CreateLayout();

        Assert.Equal(new Rect(0, 461, 320, 30), layout.BarLeft);
        Assert.Equal(new Rect(320, 461, 320 - ExitSize.Width + 1, 30), layout.BarRight);
        Assert.Equal(359, layout.BarRightSourceX);
        Assert.Equal(new Rect(600, 461, 40, 22), layout.Exit);
        Assert.Equal(new Point(250, 464), layout.TitleAnchor(100));
    }

    [Fact]
    public void Layout_RejectsImpossibleArtwork()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThievesGuildLayout(
            new Rect(0, 0, 640, 480), new Size(640, 0), ExitSize, 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThievesGuildLayout(
            new Rect(0, 0, 640, 480), BarSize, new Size(0, 22), 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThievesGuildLayout(
            new Rect(0, 0, 640, 480), BarSize, ExitSize, 0));
    }

    [Fact]
    public void Layout_CentresTheReportOnALargerScreen()
    {
        ThievesGuildLayout layout = new(new Rect(0, 0, 800, 600), BarSize, ExitSize, 17);

        Assert.Equal(new Rect(80, 60, 640, 480), layout.Dialog);
    }

    [Fact]
    public void TryCreate_ReturnsNullWithoutUsableArtwork()
    {
        ThievesGuildReport report = CreateReport(guilds: 1);

        Assert.Null(ThievesGuildView.TryCreate(report, null, CreateFonts()));
        Assert.Null(ThievesGuildView.TryCreate(CreateReport(guilds: 1, players: 0), CreateArtwork(), CreateFonts()));
        Assert.Null(ThievesGuildView.TryCreate(report, CreateArtwork(flagFrames: 3), CreateFonts()));
        Assert.Null(ThievesGuildView.TryCreate(report, CreateArtwork(barWidth: 320), CreateFonts()));
        Assert.Null(ThievesGuildView.TryCreate(report, CreateArtwork(backgroundWidth: 320), CreateFonts()));
    }

    [Fact]
    public void TryCreate_RejectsMissingArguments()
    {
        ThievesGuildReport report = CreateReport(guilds: 1);

        Assert.Throws<ArgumentNullException>(() => ThievesGuildView.TryCreate(null!, CreateArtwork(), CreateFonts()));
        Assert.Throws<ArgumentNullException>(() => ThievesGuildView.TryCreate(report, CreateArtwork(), null!));
    }

    [Fact]
    public void Draw_PaintsThePlateTheBarTheOrdinalsAndTheExitButton()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: 1));
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        Assert.Equal(BackgroundColor, Pixel(target, 5, 5));
        Assert.Equal(BarColor, Pixel(target, 5, 466));
        Assert.Equal(BarColor, Pixel(target, 590, 466));
        Assert.Equal(ExitReleasedColor, Pixel(target, view.ExitButton.ReleasedArea.X + 20, view.ExitButton.ReleasedArea.Y + 11));
        Assert.Equal(NormalTextColor, Pixel(target, view.Layout.OrdinalCentre(0).X, view.Layout.OrdinalCentre(0).Y + 8));
    }

    [Fact]
    public void Draw_ShowsTheFirstThreeRowsEvenWithoutAGuild()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: 0));
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        // The town row is a single flag per player, so the first one starts at 264 - 7 + 3.
        Assert.Equal(FlagColor(PlayerColor.Blue), Pixel(target, 264 + 3, 31 - 4 + 9));
        Assert.Equal(BackgroundColor, Pixel(target, 264 + 3, 103 - 4 + 9));
    }

    [Fact]
    public void Draw_OpensTheRowsOneGuildAtATime()
    {
        PixelBuffer target = new(640, 480);

        ThievesGuildView none = CreateView(CreateReport(guilds: 0));
        none.Draw(target);
        Assert.Equal(BackgroundColor, Pixel(target, none.Layout.HeroIconCentre(0).X, none.Layout.HeroIconCentre(0).Y + 5));

        ThievesGuildView one = CreateView(CreateReport(guilds: 1));
        one.Draw(target);
        Assert.Equal(PortraitColor, Pixel(target, one.Layout.HeroIconCentre(0).X, one.Layout.HeroIconCentre(0).Y + 5));

        ThievesGuildView two = CreateView(CreateReport(guilds: 2));
        two.Draw(target);
        Assert.Equal(SmallTextColor, Pixel(target, two.Layout.HeroStatsAnchor(0).X + 2, two.Layout.HeroStatsAnchor(0).Y + 3));

        ThievesGuildView three = CreateView(CreateReport(guilds: 3));
        three.Draw(target);
        Assert.Equal(SmallTextColor, Pixel(target, three.Layout.PersonalityCentre(0).X + 3, three.Layout.PersonalityCentre(0).Y + 3));

        ThievesGuildView four = CreateView(CreateReport(guilds: 4));
        four.Draw(target);
        Assert.Equal(MonsterColor, Pixel(target, four.Layout.MonsterCentre(0).X, four.Layout.MonsterCentre(0).Y));
    }

    [Fact]
    public void Draw_KeepsTheRowsTheKingdomCannotAffordEmpty()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: 2));
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        // Two guilds reveal gold and wood but not the personalities or the best monster.
        Assert.Equal(FlagColor(PlayerColor.Blue), Pixel(target, 264 + 3 - 7, 103 - 4 + 9));
        Assert.Equal(BackgroundColor, Pixel(target, view.Layout.PersonalityCentre(0).X + 3, view.Layout.PersonalityCentre(0).Y + 3));
        Assert.Equal(BackgroundColor, Pixel(target, view.Layout.MonsterCentre(0).X, view.Layout.MonsterCentre(0).Y));
    }

    [Fact]
    public void Draw_PaintsTheHeroWindowPortraitAndStatistics()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: 2));
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        Point centre = view.Layout.HeroIconCentre(0);

        Assert.Equal(HeroWindowColor, Pixel(target, centre.X - 5, centre.Y - 1));
        Assert.Equal(PortraitColor, Pixel(target, centre.X, centre.Y + 10));
        Assert.Equal(SmallTextColor, Pixel(target, view.Layout.HeroStatsAnchor(0).X + 2, view.Layout.HeroStatsAnchor(0).Y + 3));
    }

    [Fact]
    public void Draw_PutsTheOracleTitleOnTheBar()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: ThievesGuildReport.OracleGuilds));
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        // The title is centred in the room the exit button leaves and is drawn on row 464.
        Assert.Equal(NormalTextColor, Pixel(target, 234, 464 + 3));
    }

    [Fact]
    public void Draw_RejectsAMissingTarget() =>
        Assert.Throws<ArgumentNullException>(() => CreateView(CreateReport(guilds: 1)).Draw(null!));

    [Fact]
    public void ProcessEvents_RejectsMissingEvents() =>
        Assert.Throws<ArgumentNullException>(() => CreateView(CreateReport(guilds: 1)).ProcessEvents(null!));

    [Fact]
    public void ClickOnTheExitButton_ClosesTheReport()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: 1));
        LocalEvent events = CreateEvents();

        Click(events, Center(view.ExitButton.ReleasedArea));

        Assert.Equal(ThievesGuildAction.Exit, view.ProcessEvents(events));
    }

    [Fact]
    public void RightClickOnTheExitButton_AsksForTheHelpMessage()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: 1));
        LocalEvent events = CreateEvents();

        events.OnMouseButton(true, MouseButtonType.Right, Center(view.ExitButton.ReleasedArea));

        Assert.Equal(ThievesGuildAction.ExitHelp, view.ProcessEvents(events));
    }

    [Fact]
    public void ClickAnywhereElse_DoesNothing()
    {
        ThievesGuildView view = CreateView(CreateReport(guilds: 1));
        LocalEvent events = CreateEvents();

        Click(events, new Point(100, 200));

        Assert.Equal(ThievesGuildAction.None, view.ProcessEvents(events));
    }

    private static ThievesGuildLayout CreateLayout() =>
        new(new Rect(0, 0, 640, 480), BarSize, ExitSize, 17);

    private static byte Pixel(PixelBuffer buffer, int x, int y) => buffer[x, y];

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static LocalEvent CreateEvents() => new(() => 0);

    private static void Click(LocalEvent events, Point point)
    {
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);
    }

    private static byte FlagColor(PlayerColor color) => (byte)(FlagBase + PlayerColors.IndexOf(color));

    private static ThievesGuildView CreateView(ThievesGuildReport report) =>
        ThievesGuildView.TryCreate(report, CreateArtwork(), CreateFonts())!;

    private static ThievesGuildReport CreateReport(int guilds, int players = 2)
    {
        ThievesGuildPlayer[] columns = new ThievesGuildPlayer[players];

        for (int index = 0; index < players; ++index)
        {
            columns[index] = new ThievesGuildPlayer(
                index == 0 ? PlayerColor.Blue : PlayerColor.Red,
                Towns: 2 - index,
                Castles: 0,
                Heroes: 1,
                Gold: 100,
                WoodAndOre: 0,
                RareResources: 0,
                Obelisks: 0,
                Artifacts: 0,
                ArmyStrength: 0,
                Income: 0,
                Personality: "Human",
                BestHero: new ThievesGuildHero(1, 1, 2, 3, 4),
                StrongestMonster: MonsterType.Peasant);
        }

        return ThievesGuildReport.Create(columns, guilds);
    }

    private static ThievesGuildFonts CreateFonts() => new(
        new BlockGlyphSource(11, SmallTextColor),
        new BlockGlyphSource(17, NormalTextColor));

    private static ThievesGuildArtwork CreateArtwork(
        int flagFrames = ThievesGuildArtwork.FlagFrames,
        int barWidth = 640,
        int backgroundWidth = 640)
    {
        Sprite[] background = [new Sprite(Filled(new Size(backgroundWidth, 480), BackgroundColor))];

        Sprite[] townwind = new Sprite[ThievesGuildArtwork.FirstFlagFrame + flagFrames];

        for (int index = 0; index < townwind.Length; ++index)
        {
            byte color = index < ThievesGuildArtwork.FirstFlagFrame
                ? BackgroundColor
                : (byte)(FlagBase + index - ThievesGuildArtwork.FirstFlagFrame);
            townwind[index] = new Sprite(Filled(FlagSize, color));
        }

        Sprite[] locators = new Sprite[ThievesGuildArtwork.HeroWindowFrame + 1];

        for (int index = 0; index < locators.Length; ++index)
        {
            locators[index] = new Sprite(Filled(HeroWindowSize, HeroWindowColor));
        }

        Sprite[] bar = [new Sprite(Filled(new Size(barWidth, BarSize.Height), BarColor))];

        Sprite[] portraits = new Sprite[5];

        for (int index = 0; index < portraits.Length; ++index)
        {
            portraits[index] = new Sprite(Filled(PortraitSize, PortraitColor));
        }

        Sprite[] monsters = new Sprite[5];

        for (int index = 0; index < monsters.Length; ++index)
        {
            monsters[index] = new Sprite(Filled(MonsterSize, MonsterColor));
        }

        return ThievesGuildArtwork.TryCreate(
            background,
            townwind,
            locators,
            bar,
            portraits,
            monsters,
            [
                new Sprite(Filled(ExitSize, ExitReleasedColor)),
                new Sprite(Filled(ExitSize, ExitPressedColor), 1, 1),
            ])!;
    }

    private static PixelBuffer Filled(Size size, byte color)
    {
        PixelBuffer buffer = new(size.Width, size.Height);
        buffer.Fill(color);

        return buffer;
    }

    /// <summary>A solid block font, enough to see where a line went.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockGlyphSource(int lineHeight, byte color)
        {
            LineHeight = lineHeight;
            _glyph = new Sprite(Filled(new Size(6, lineHeight), color));
        }

        public int LineHeight { get; }

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
