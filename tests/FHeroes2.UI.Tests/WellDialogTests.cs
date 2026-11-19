using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="WellDialog"/>, <see cref="WellLayout"/> and <see cref="WellArtwork"/>: the contract of the well window
/// (<c>castle_well.cpp:61-548</c>).
/// </summary>
public sealed class WellDialogTests
{
    [Fact]
    public void CreateLayout_PutsTheButtonsOnTheEndsOfTheBar()
    {
        WellLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        Assert.Equal(new Rect(0, 0, 640, 480), layout.Dialog);

        // MAX starts at the left edge, EXIT ends at the right one, and the bar fills what is between them.
        Assert.Equal(new Rect(0, 461, MaxWidth, BarHeight), layout.Max);
        Assert.Equal(new Rect(640 - ExitWidth, 461, ExitWidth, BarHeight), layout.Exit);
        Assert.Equal(new Rect(MaxWidth, 461, 320 - MaxWidth, BarHeight), layout.BarLeft);
        Assert.Equal(new Rect(320, 461, 321 - ExitWidth, BarHeight), layout.BarRight);

        // The right piece keeps the archive's rounded end: 600 - 320 + 40 - 1.
        Assert.Equal(319, layout.BarRightSourceX);

        // The original background has one transparent column on the right.
        Assert.Equal(new Rect(639, 461, 1, BarHeight), layout.FillColumn);
        Assert.Equal(new Rect(0, 461, 640, BarHeight - 1), layout.TitleRoi);
        Assert.Equal(BarHeight, layout.BarHeight);
    }

    [Fact]
    public void CreateLayout_CentresTheWindowOnALargerScreen()
    {
        WellLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 800, 600), CreateArtwork());

        Assert.Equal(new Rect(80, 60, 640, 480), layout.Dialog);
        Assert.Equal(new Rect(80, 521, MaxWidth, BarHeight), layout.Max);
        Assert.Equal(new Rect(680, 521, ExitWidth, BarHeight), layout.Exit);
        Assert.Equal(new Rect(80 + MaxWidth, 521, 320 - MaxWidth, BarHeight), layout.BarLeft);
        Assert.Equal(new Rect(400, 521, 321 - ExitWidth, BarHeight), layout.BarRight);

        // The filled column is the rightmost one of the window, 80 + 640 - 1.
        Assert.Equal(719, layout.FillColumn.X);
    }

    [Fact]
    public void CellAnchors_GoDownTheLeftColumnThenTheRightOne()
    {
        WellLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        Assert.Equal(new Point(0, 1), layout.GetCellOrigin(1));
        Assert.Equal(new Point(0, 151), layout.GetCellOrigin(2));
        Assert.Equal(new Point(0, 301), layout.GetCellOrigin(3));
        Assert.Equal(new Point(314, 1), layout.GetCellOrigin(4));
        Assert.Equal(new Point(314, 151), layout.GetCellOrigin(5));
        Assert.Equal(new Point(314, 301), layout.GetCellOrigin(6));

        Assert.Equal(new Point(21, 36), layout.GetPictureOrigin(1));
        Assert.Equal(new Rect(20, 18, 288, 124), layout.GetMonsterArea(1));
        Assert.Equal(new Rect(334, 318, 288, 124), layout.GetMonsterArea(6));
        Assert.Equal(new Point(334, 271), layout.GetAvailableOrigin(5));
        Assert.Equal(new Point(86, 255), layout.GetDwellingNameAnchor(2));
        Assert.Equal(new Point(436, 20), layout.GetMonsterNameAnchor(4));
        Assert.Equal(new Point(269, 323), layout.GetStatsAnchor(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void GetCellOrigin_RefusesALevelOutsideTheSix(int level)
    {
        WellLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.GetCellOrigin(level));
    }

    [Fact]
    public void Centre_HalvesTheTextWidth()
    {
        Assert.Equal(new Point(95, 10), WellLayout.Centre(new Point(100, 10), 10));
        Assert.Equal(new Point(100, 10), WellLayout.Centre(new Point(100, 10), 1));
    }

    [Theory]
    [InlineData(1, BuildingType.DwellingMonster1, 19)]
    [InlineData(2, BuildingType.DwellingMonster2, 20)]
    [InlineData(2, BuildingType.DwellingUpgrade2, 25)]
    [InlineData(3, BuildingType.DwellingMonster3, 21)]
    [InlineData(3, BuildingType.DwellingUpgrade3, 26)]
    [InlineData(4, BuildingType.DwellingMonster4, 22)]
    [InlineData(4, BuildingType.DwellingUpgrade4, 27)]
    [InlineData(5, BuildingType.DwellingMonster5, 23)]
    [InlineData(5, BuildingType.DwellingUpgrade5, 28)]
    [InlineData(6, BuildingType.DwellingMonster6, 24)]
    [InlineData(6, BuildingType.DwellingUpgrade6, 29)]
    [InlineData(6, BuildingType.DwellingUpgrade7, 30)]
    public void Artwork_PicksTheDwellingFrameOfTheReference(int level, BuildingType actual, int expected)
    {
        Assert.Equal(expected, WellArtwork.GetDwellingFrame(level, actual));
    }

    [Fact]
    public void Artwork_PicksTheArchivesOfTheReference()
    {
        Assert.Equal(IcnType.Wellbkg, WellArtwork.GetBackgroundIcn(evilInterface: false));
        Assert.Equal(IcnType.WellbkgEvil, WellArtwork.GetBackgroundIcn(evilInterface: true));

        Assert.Equal(IcnType.Cstlkngt, WellArtwork.GetDwellingIcn(MapRace.Knight));
        Assert.Equal(IcnType.Cstlbarb, WellArtwork.GetDwellingIcn(MapRace.Barbarian));
        Assert.Equal(IcnType.Cstlsorc, WellArtwork.GetDwellingIcn(MapRace.Sorceress));
        Assert.Equal(IcnType.Cstlwrlk, WellArtwork.GetDwellingIcn(MapRace.Warlock));
        Assert.Equal(IcnType.Cstlwzrd, WellArtwork.GetDwellingIcn(MapRace.Wizard));
        Assert.Equal(IcnType.Cstlnecr, WellArtwork.GetDwellingIcn(MapRace.Necromancer));

        // The reference has no building archive for a mixed town and answers UNKNOWN.
        Assert.Equal(IcnType.Unknown, WellArtwork.GetDwellingIcn(MapRace.Multiple));
    }

    [Fact]
    public void Artwork_ReturnsTheDwellingSpriteOfAFrame()
    {
        WellArtwork artwork = CreateArtwork();

        Sprite plain = artwork.GetDwellingSprite(2, BuildingType.DwellingMonster2);
        Sprite upgraded = artwork.GetDwellingSprite(2, BuildingType.DwellingUpgrade2);

        Assert.Equal(FrameColour(20), plain.Image[0, 0]);
        Assert.Equal(FrameColour(25), upgraded.Image[0, 0]);
    }

    [Fact]
    public void Artwork_RefusesAnArchiveWithoutTheFramesItNeeds()
    {
        Sprite sprite = CreateSprite(10, 10, BackgroundColor);
        Sprite[] frames = CreateDwellingFrames();
        DialogButtonArtwork button = new(CreateSprite(10, 10, ButtonColor), CreateSprite(10, 10, ButtonColor));

        Assert.Null(WellArtwork.TryCreate(CreateSprite(0, 0, BackgroundColor), CreateBarSprite(), button, button, frames));
        Assert.Null(WellArtwork.TryCreate(sprite, CreateSprite(0, 0, BackgroundColor), button, button, frames));
        Assert.Null(WellArtwork.TryCreate(
            sprite,
            CreateBarSprite(),
            new DialogButtonArtwork(CreateSprite(10, 10, ButtonColor), CreateSprite(0, 0, ButtonColor)),
            button,
            frames));

        // A button archive that loaded nothing at all is a short archive too, not a crash.
        Assert.Null(WellArtwork.TryCreate(sprite, CreateBarSprite(), default, button, frames));
        Assert.Null(WellArtwork.TryCreate(sprite, CreateBarSprite(), button, default, frames));

        Assert.Null(WellArtwork.TryCreate(sprite, CreateBarSprite(), button, button, frames[..^1]));
        Assert.Null(WellArtwork.TryCreate(sprite, CreateBarSprite(), button, button, [.. frames[..^1], CreateSprite(0, 0, 0)]));
        Assert.NotNull(WellArtwork.TryCreate(sprite, CreateBarSprite(), button, button, frames));
    }

    [Fact]
    public void DescribeStats_PrintsTheLinesOfTheReference()
    {
        CastleWellDwelling row = CreateRow(level: 2, built: true, growth: 9);
        MonsterBattleStats stats = row.Stats;
        IReadOnlyList<string> lines = WellDialog.DescribeStats(row);

        Assert.Equal(10, lines.Count);
        Assert.Equal($"Attack: {stats.Attack}", lines[0]);
        Assert.Equal($"Defense: {stats.Defense}", lines[1]);
        Assert.NotEqual(stats.DamageMin, stats.DamageMax);
        Assert.Equal($"Damage: {stats.DamageMin}-{stats.DamageMax}", lines[2]);
        Assert.Equal($"HP: {stats.HitPoints}", lines[3]);

        // The blank lines are the reference's "skip a line" and still take a line height.
        Assert.Equal(string.Empty, lines[4]);
        Assert.Equal("Speed:", lines[5]);
        Assert.Equal(Speed.GetName((uint)stats.Speed), lines[6]);
        Assert.Equal(string.Empty, lines[7]);
        Assert.Equal("Growth", lines[8]);
        Assert.Equal("+ 9 / week", lines[9]);
    }

    [Fact]
    public void DescribeStats_LeavesOutTheGrowthOfAnUnbuiltDwelling()
    {
        IReadOnlyList<string> lines = WellDialog.DescribeStats(CreateRow(level: 1, built: false));

        Assert.Equal(8, lines.Count);
        Assert.DoesNotContain("Growth", lines);
    }

    [Fact]
    public void DescribeStats_PrintsASingleNumberWhenTheDamageIsFixed()
    {
        // The peasant hits for one, so there is no range to print.
        IReadOnlyList<string> lines = WellDialog.DescribeStats(CreateRow(level: 1));

        Assert.Equal("Damage: 1", lines[2]);
    }

    [Fact]
    public void Draw_CopiesTheArtworkAndFillsTheBarPieces()
    {
        WellDialog dialog = CreateDialog();
        WellArtwork artwork = CreateArtwork();
        WellLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), artwork);
        PixelBuffer target = new(640, 480);

        dialog.Draw(target, layout, artwork, CreateFonts());

        // Background above the bar, from the archive's own columns.
        Assert.Equal(BackgroundColor, target[5, 5]);
        Assert.Equal(BackgroundColor, target[639, 460]);

        // The left piece starts at the MAX button and reads from column 1 of the archive.
        Assert.Equal(BarColour(1 + (30 - MaxWidth)), target[30, 470]);
        Assert.Equal(BarColour(1), target[MaxWidth, 470]);

        // The right piece reads from the archive's own rounded end; the samples stay right of the title, which
        // covers the middle of the bar.
        Assert.Equal(BarColour(319 + 180), target[500, 470]);
        Assert.Equal(BarColour(319 + 240), target[560, 470]);

        // The transparent column of the original artwork is painted black.
        Assert.Equal(0, target[639, 470]);

        // The bar ends at row 476; the three rows below it are left alone, as in the reference.
        Assert.Equal(0, target[639, 477]);
    }

    [Fact]
    public void Draw_PutsEveryDwellingPictureAndNameInItsCell()
    {
        WellDialog dialog = CreateDialog();
        WellArtwork artwork = CreateArtwork();
        WellLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), artwork);
        PixelBuffer target = new(640, 480);
        WellFonts fonts = CreateFonts();

        dialog.Draw(target, layout, artwork, fonts);

        // The sixth cell shows the second upgrade's picture, frame 30.
        Point origin = layout.GetPictureOrigin(6);
        Assert.Equal(FrameColour(30), target[origin.X + 2, origin.Y + 2]);

        // The title is centred on column 315 of the bar and drawn one row below its top.
        int titleWidth = GlyphText.Measure(fonts.NormalWhite, WellDialog.Title);
        Assert.Equal(TitleColor, target[315 - (titleWidth / 2) + 2, 461 + 3 + 2]);

        // The first statistics line sits at the cell's anchor, centred on column 269.
        int attackWidth = GlyphText.Measure(fonts.SmallWhite, "Attack: 1");
        Assert.Equal(TextColor, target[269 - (attackWidth / 2) + 2, 1 + 22 + 2]);
    }

    [Fact]
    public void DrawAvailable_PrintsThePopulationInYellowUnderTheWord()
    {
        WellDialog dialog = CreateDialog();
        WellLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());
        PixelBuffer target = new(640, 480);
        WellFonts fonts = CreateFonts();

        CastleWellDwelling row = dialog.Dwellings[4];
        WellDialog.DrawAvailable(target, layout, fonts, row);

        Point origin = layout.GetAvailableOrigin(5);
        Assert.Equal(PopulationColor, target[origin.X + 109 - 3 + 2, origin.Y + 2]);
        Assert.Equal(TextColor, target[origin.X + (106 / 2) - 30 + 2, origin.Y + 2 + 2]);
    }

    [Fact]
    public void DrawAvailable_LeavesAnUnbuiltDwellingBlank()
    {
        WellDialog dialog = WellDialog.Create(
            [.. Enumerable.Range(1, 6).Select(level => CreateRow(level, built: false))],
            MapRace.Knight);

        WellLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());
        PixelBuffer target = new(640, 480);

        WellDialog.DrawAvailable(target, layout, CreateFonts(), dialog.Dwellings[0]);

        Assert.Equal(0, target[126, 123]);
    }

    [Fact]
    public void CreateButtons_ListsMaxThenExit()
    {
        WellDialog dialog = CreateDialog();
        WellLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        IReadOnlyList<ButtonSprite> buttons = WellDialog.CreateButtons(layout, CreateArtwork());

        Assert.Equal(2, buttons.Count);
        Assert.Equal(layout.Max.Position, buttons[0].Area.Position);
        Assert.Equal(layout.Exit.Position, buttons[1].Area.Position);
        Assert.All(buttons, button => Assert.True(button.IsEnabled));
    }

    [Theory]
    [InlineData(0, WellDialogButton.Max)]
    [InlineData(1, WellDialogButton.Exit)]
    [InlineData(9, WellDialogButton.Exit)]
    public void FromButton_MapsThePressToTheButton(int index, WellDialogButton expected)
    {
        Assert.Equal(expected, WellDialog.FromButton(index));
    }

    [Fact]
    public void GetTooltip_SaysWhatTheButtonDoes()
    {
        Assert.Equal(new WellTooltip("Max", "Hire all creatures in the town."), WellDialog.GetTooltip(WellDialogButton.Max));
        Assert.Equal(new WellTooltip("Exit", "Exit this menu."), WellDialog.GetTooltip(WellDialogButton.Exit));
    }

    [Fact]
    public void GetDwellingAt_OnlyHitsABuiltDwelling()
    {
        WellDialog dialog = WellDialog.Create(
            [.. Enumerable.Range(1, 6).Select(level => CreateRow(level, built: level == 3))],
            MapRace.Knight);

        WellLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        Assert.Equal(3, dialog.GetDwellingAt(new Point(100, 350), layout));
        Assert.Equal(0, dialog.GetDwellingAt(new Point(100, 50), layout));
        Assert.Equal(0, dialog.GetDwellingAt(new Point(630, 470), layout));
    }

    [Fact]
    public void Create_TakesTheRowsOfATown()
    {
        CastleTown castle = new(0, MapRace.Sorceress);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.DwellingMonster1);

        WellDialog dialog = WellDialog.Create(castle);

        Assert.Equal(MapRace.Sorceress, dialog.Race);
        Assert.Equal(castle.GetWellDwellings(), dialog.Dwellings);
        Assert.True(dialog.Dwellings[0].IsBuilt);
    }

    [Fact]
    public void Create_RefusesAnythingButSixRows()
    {
        Assert.Throws<ArgumentException>(() => WellDialog.Create(new[] { CreateRow(1) }, MapRace.Knight));
        Assert.Throws<ArgumentNullException>(() => WellDialog.Create((IReadOnlyList<CastleWellDwelling>)null!, MapRace.Knight));
        Assert.Throws<ArgumentNullException>(() => WellDialog.Create((CastleTown)null!));
    }

    [Fact]
    public void DescribeDwelling_NamesTheUpgradedDwelling()
    {
        WellDialog dialog = WellDialog.Create(
            [.. Enumerable.Range(1, 6).Select(level => CreateRow(level, actual: level == 2 ? BuildingType.DwellingUpgrade2 : null))],
            MapRace.Knight);

        // The reference asks for the name of the dwelling that stands, not of the base one.
        Assert.Equal(
            CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.DwellingUpgrade2),
            dialog.DescribeDwelling(dialog.Dwellings[1]));
        Assert.NotEqual(
            CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.DwellingMonster2),
            dialog.DescribeDwelling(dialog.Dwellings[1]));
        Assert.Equal(
            CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.DwellingMonster1),
            dialog.DescribeDwelling(dialog.Dwellings[0]));
    }

    [Fact]
    public void DescribePopulation_PrintsTheNumber()
    {
        Assert.Equal("0", WellDialog.DescribePopulation(0));
        Assert.Equal("12345", WellDialog.DescribePopulation(12345));
    }

    private static BuildingType BaseDwelling(int level) => level switch
    {
        1 => BuildingType.DwellingMonster1,
        2 => BuildingType.DwellingMonster2,
        3 => BuildingType.DwellingMonster3,
        4 => BuildingType.DwellingMonster4,
        5 => BuildingType.DwellingMonster5,
        _ => BuildingType.DwellingMonster6,
    };

    private static CastleWellDwelling CreateRow(
        int level,
        bool built = true,
        uint available = 7,
        uint growth = 9,
        BuildingType? actual = null)
    {
        BuildingType baseDwelling = BaseDwelling(level);
        BuildingType actualDwelling = actual ?? baseDwelling;

        return new CastleWellDwelling(
            level,
            baseDwelling,
            actualDwelling,
            DwellingMonsters.FromDwelling(MapRace.Knight, actualDwelling),
            built,
            available,
            growth);
    }

    private static WellDialog CreateDialog()
    {
        // The sixth dwelling carries its second upgrade, so the picture tests can tell three frames apart.
        return WellDialog.Create(
            [.. Enumerable.Range(1, 6).Select(level => CreateRow(level, actual: level == 6 ? BuildingType.DwellingUpgrade7 : null))],
            MapRace.Knight);
    }

    private static WellArtwork CreateArtwork() => WellArtwork.TryCreate(
        CreateSprite(640, 480, BackgroundColor),
        CreateBarSprite(),
        new DialogButtonArtwork(CreateSprite(MaxWidth, BarHeight, ButtonColor), CreateSprite(MaxWidth, BarHeight, ButtonPressedColor)),
        new DialogButtonArtwork(CreateSprite(ExitWidth, BarHeight, ButtonColor), CreateSprite(ExitWidth, BarHeight, ButtonPressedColor)),
        CreateDwellingFrames())!;

    /// <summary>A bar whose every column carries a colour that identifies its archive column.</summary>
    private static Sprite CreateBarSprite()
    {
        PixelBuffer image = new(BarWidth, BarHeight);

        for (int y = 0; y < BarHeight; ++y)
        {
            Span<byte> row = image.Row(y);

            for (int x = 0; x < BarWidth; ++x)
            {
                row[x] = BarColour(x);
            }
        }

        return new Sprite(image);
    }

    private static byte BarColour(int column) => (byte)(5 + (column % 150));

    private static Sprite[] CreateDwellingFrames()
    {
        Sprite[] frames = new Sprite[WellArtwork.DwellingFrameCount];

        for (int index = 0; index < frames.Length; ++index)
        {
            frames[index] = CreateSprite(40, 30, FrameColour(index + WellArtwork.FirstDwellingFrame));
        }

        return frames;
    }

    private static byte FrameColour(int frame) => (byte)(160 + (frame - WellArtwork.FirstDwellingFrame));

    private static Sprite CreateSprite(int width, int height, byte color)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image);
    }

    private static WellFonts CreateFonts() => new(
        new FakeGlyphSource(TextColor, lineHeight: 11),
        new FakeGlyphSource(TitleColor, lineHeight: 17),
        new FakeGlyphSource(PopulationColor, lineHeight: 17));

    private const int BarWidth = 600;
    private const int BarHeight = 16;
    private const int MaxWidth = 24;
    private const int ExitWidth = 40;

    private const byte BackgroundColor = 210;
    private const byte ButtonColor = 211;
    private const byte ButtonPressedColor = 212;
    private const byte TextColor = 213;
    private const byte TitleColor = 214;
    private const byte PopulationColor = 215;

    /// <summary>
    /// A glyph source with fixed metrics whose glyphs are solid blocks, so a test can tell one text block from
    /// another by colour alone.
    /// </summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color, int lineHeight)
        {
            PixelBuffer image = new(6, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

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
