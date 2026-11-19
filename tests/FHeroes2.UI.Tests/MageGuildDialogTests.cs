using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="MageGuildDialog"/>, <see cref="MageGuildLayout"/>, <see cref="MageGuildArtwork"/> and
/// <see cref="SpellScrollRow"/>: the contract of the mage guild window
/// (<c>castle_mageguild.cpp:56-212</c> and <c>ui_mage_guild.cpp:42-228</c>).
/// </summary>
public sealed class MageGuildDialogTests
{
    private static CastleTown CreateTown(MapRace race = MapRace.Knight)
    {
        CastleTown castle = new(0, race);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.MageGuild1);

        return castle;
    }

    [Fact]
    public void ASingleTownLeavesTheTownButtonsDisabled()
    {
        MageGuildDialog dialog = MageGuildDialog.Create(CreateTown(), heroHasSpellBook: false, castleCount: 1);

        Assert.False(dialog.CanSwitchTowns);
        Assert.True(MageGuildDialog.Create(CreateTown(), heroHasSpellBook: false, castleCount: 2).CanSwitchTowns);
    }

    [Fact]
    public void AHeroWithoutABookIsToldTheSpellsAreOnOffer()
    {
        MageGuildDialog dialog = MageGuildDialog.Create(CreateTown(), heroHasSpellBook: false, castleCount: 1);

        Assert.Equal("The above spells are available here.", dialog.DefaultStatusText);
    }

    [Fact]
    public void AHeroWithABookIsToldItWasFilled()
    {
        MageGuildDialog dialog = MageGuildDialog.Create(CreateTown(), heroHasSpellBook: true, castleCount: 1);

        Assert.Equal("The spells the hero can learn have been added to their book.", dialog.DefaultStatusText);
    }

    [Fact]
    public void TheRowsComeFromTheTownAndGoUpWithTheLevel()
    {
        CastleTown castle = CreateTown();
        MageGuildDialog dialog = MageGuildDialog.Create(castle, heroHasSpellBook: true, castleCount: 3);

        Assert.Equal(5, dialog.Rows.Count);
        Assert.Equal(castle.GetMageGuildSpellRows(), dialog.Rows);
        Assert.True(dialog.CanSwitchTowns);

        // The fifth level is drawn highest: the offset shrinks by RowStepY per level.
        Assert.Equal(MageGuildDialog.FirstRowTopOffset, MageGuildDialog.GetRowTopOffset(0));
        Assert.Equal(MageGuildDialog.RowsTopOffset, MageGuildDialog.GetRowTopOffset(4));
        Assert.Equal(5, MageGuildDialog.RowsTopOffset);

        for (int levelIndex = 1; levelIndex < 5; ++levelIndex)
        {
            Assert.Equal(
                MageGuildDialog.RowStepY,
                MageGuildDialog.GetRowTopOffset(levelIndex - 1) - MageGuildDialog.GetRowTopOffset(levelIndex));
        }
    }

    [Fact]
    public void TheBuildingFrameIsOneLessThanTheGuildLevel()
    {
        Assert.Equal(0, CreateDialog(guildLevel: 0).BuildingFrameIndex);
        Assert.Equal(0, CreateDialog(guildLevel: 1).BuildingFrameIndex);
        Assert.Equal(2, CreateDialog(guildLevel: 3).BuildingFrameIndex);
        Assert.Equal(4, CreateDialog(guildLevel: 5).BuildingFrameIndex);
    }

    [Fact]
    public void CreateLayout_StacksTheBottomStripFromTheScreenEnds()
    {
        MageGuildDialog dialog = CreateDialog();
        MageGuildLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        Assert.Equal(new Rect(0, 0, 640, 480), layout.Dialog);
        Assert.Equal(new Point(0, 461), layout.BottomUi);

        // Previous arrow at the left edge, exit at the right one, next arrow just before the exit.
        Assert.Equal(new Rect(0, 461, 20, 16), layout.PreviousTown);
        Assert.Equal(new Rect(600, 461, 40, 16), layout.Exit);
        Assert.Equal(new Rect(580, 461, 20, 16), layout.NextTown);

        // The bar fills the gap: 640 - 20 - 20 - 40.
        Assert.Equal(new Rect(20, 461, 560, 16), layout.Bar);
        Assert.Equal(280, layout.BarLeftWidth);
        Assert.Equal(280, layout.BarRightWidth);
        Assert.Equal(320, layout.BarSourceRightX);

        // The text region starts 16 pixels into the strip, not into the bar.
        Assert.Equal(new Rect(16, 461, 560, 0), layout.StatusBar);
    }

    [Fact]
    public void CreateLayout_CentresTheWindowOnALargerScreen()
    {
        MageGuildDialog dialog = CreateDialog();
        MageGuildLayout layout = dialog.CreateLayout(new Rect(0, 0, 800, 600), CreateArtwork());

        Assert.Equal(new Rect(80, 60, 640, 480), layout.Dialog);
        Assert.Equal(new Point(80, 521), layout.BottomUi);
        Assert.Equal(new Rect(80, 521, 20, 16), layout.PreviousTown);
        Assert.Equal(new Rect(680, 521, 40, 16), layout.Exit);
        Assert.Equal(new Rect(660, 521, 20, 16), layout.NextTown);
        Assert.Equal(new Rect(100, 521, 560, 16), layout.Bar);
    }

    [Fact]
    public void CreateLayout_GivesAnOddBarTheExtraPixelToTheRightCopy()
    {
        // 640 - 37 - 20 - 20 = 563, which cannot be halved.
        MageGuildArtwork artwork = CreateArtwork(exitWidth: 37);
        MageGuildLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), artwork);

        Assert.Equal(new Rect(603, 461, 37, 16), layout.Exit);
        Assert.Equal(new Rect(583, 461, 20, 16), layout.NextTown);
        Assert.Equal(new Rect(20, 461, 563, 16), layout.Bar);
        Assert.Equal(281, layout.BarLeftWidth);
        Assert.Equal(282, layout.BarRightWidth);

        // The right copy keeps the archive's rounded end: 600 - 282.
        Assert.Equal(318, layout.BarSourceRightX);
    }

    [Fact]
    public void GetRowAnchors_PutsTheFirstLevelLowest()
    {
        MageGuildLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 800, 600), CreateArtwork());

        Assert.Equal(new Point(330, 425), layout.GetRowAnchor(0));
        Assert.Equal(new Point(330, 335), layout.GetRowAnchor(1));
        Assert.Equal(new Point(330, 65), layout.GetRowAnchor(4));
    }

    [Fact]
    public void CreateRows_BuildsFiveRowsAndPositionsThemFromTheDialogCorner()
    {
        MageGuildDialog dialog = CreateDialog([SpellType.Fireball, SpellType.None, SpellType.LightningBolt]);
        MageGuildArtwork artwork = CreateArtwork();
        MageGuildLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), artwork);

        IReadOnlyList<SpellScrollRow> rows = dialog.CreateRows(layout, artwork);

        Assert.Equal(5, rows.Count);
        Assert.Equal(3, rows[0].Spells.Count);

        // Slot 0 is centred on x + 250, a 60 pixel scroll therefore starts at 220.
        Assert.Equal(new Rect(220, 365, 60, 40), rows[0].Coords[0]);

        // The empty slot folds down seven pixels and is narrower.
        Assert.Equal(new Rect(345, 372, 30, 40), rows[0].Coords[1]);
        Assert.Equal(new Rect(440, 365, 60, 40), rows[0].Coords[2]);

        // A level without spells still gets a row, just with nothing in it.
        Assert.Empty(rows[1].Spells);
        Assert.Empty(rows[4].Coords);
    }

    [Fact]
    public void SpellScrollRow_StepsSlotsByOneHundredAndTenPixels()
    {
        SpellScrollRow row = new([SpellType.Fireball, SpellType.Fireball, SpellType.None]);
        row.SetPosition(new Point(250, 365), new Size(60, 40), new Size(30, 40));

        Assert.Equal(new Rect(220, 365, 60, 40), row.Coords[0]);
        Assert.Equal(new Rect(330, 365, 60, 40), row.Coords[1]);
        Assert.Equal(new Rect(455, 372, 30, 40), row.Coords[2]);
        Assert.Equal(SpellScrollRow.SlotStepX, row.Coords[1].X - row.Coords[0].X);
    }

    [Fact]
    public void SpellScrollRow_HitTestsItsScrollsAndTracksTheCurrentSpell()
    {
        SpellScrollRow row = new([SpellType.Fireball, SpellType.LightningBolt]);
        row.SetPosition(new Point(250, 365), new Size(60, 40), new Size(30, 40));

        Assert.Equal(0, row.GetIndexAt(new Point(230, 370)));
        Assert.Equal(1, row.GetIndexAt(new Point(350, 370)));
        Assert.Equal(-1, row.GetIndexAt(new Point(0, 0)));

        Assert.True(row.CheckSpellAndMakeItCurrent(SpellType.LightningBolt));
        Assert.Equal(1, row.CurrentIndex);
        Assert.Equal(SpellType.LightningBolt, row.GetCurrentSpell());

        Assert.False(row.CheckSpellAndMakeItCurrent(SpellType.Haste));
        Assert.Equal(-1, row.CurrentIndex);
        Assert.Equal(SpellType.None, row.GetCurrentSpell());
    }

    [Fact]
    public void SpellScrollRow_ReportsNoSpellForAnEmptyCurrentSlot()
    {
        SpellScrollRow row = new([SpellType.Fireball, SpellType.None]);
        row.SetPosition(new Point(250, 365), new Size(60, 40), new Size(30, 40));

        row.CurrentIndex = 1;
        Assert.Equal(SpellType.None, row.GetCurrentSpell());

        row.CurrentIndex = 7;
        Assert.Equal(SpellType.None, row.GetCurrentSpell());
    }

    [Fact]
    public void SpellScrollRow_PaintsScrollsIconsAndNames()
    {
        SpellScrollRow row = new([SpellType.Fireball, SpellType.None]);
        row.SetPosition(new Point(250, 365), new Size(60, 40), new Size(30, 40));

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        int iconLookups = 0;
        Sprite icon = CreateSprite(24, 24, IconColor);

        row.Draw(
            target,
            CreateSprite(60, 40, ScrollColor),
            CreateSprite(30, 40, FoldedColor),
            new FakeGlyphSource(NameColor, 10),
            spell =>
            {
                ++iconLookups;
                return spell == SpellType.Fireball ? icon : null;
            });

        Assert.Equal(1, iconLookups);

        // Open and folded scrolls land on their rectangles.
        Assert.Equal(ScrollColor, target[220, 365]);
        Assert.Equal(FoldedColor, target[345, 372]);

        // The icon is inset three pixels and centred: 220 + 3 + ( 60 - 24 ) / 2, 365 + 31 - 24 / 2.
        Assert.Equal(IconColor, target[241, 384]);

        // The name starts at 220 + 18, 365 + 57 on the first glyph.
        Assert.Equal(NameColor, target[238, 422]);

        // The empty slot gets neither an icon nor a name.
        Assert.Equal(FoldedColor, target[348, 403]);
    }

    [Fact]
    public void DrawBottomBar_CopiesTheLeftHalfFromColumnOneAndTheRightHalfFromTheEnd()
    {
        MageGuildArtwork artwork = CreateArtwork();
        MageGuildLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), artwork);

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        MageGuildDialog.DrawBottomBar(target, layout, artwork);

        // The first archive column is black in the real SMALLBAR and must not be drawn.
        Assert.Equal(BarColour(1), target[20, 461]);
        Assert.Equal(BarColour(280), target[299, 461]);
        Assert.Equal(BarColour(320), target[300, 461]);
        Assert.Equal(BarColour(599), target[579, 461]);
        Assert.NotEqual(BarColour(0), target[20, 461]);
    }

    [Fact]
    public void DrawGuildBuilding_CentresTheDrawnPartAndClipsItToTwoHundredColumns()
    {
        MageGuildLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        // A 300 pixel wide picture whose drawn box is the whole sprite: it starts at 100 - 150 and the window
        // keeps only its columns 50..249.
        Sprite building = CreateOpaqueSprite(300, 40, BuildingColor);

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        MageGuildDialog.DrawGuildBuilding(target, layout, building, MapRace.Knight);

        // The sprite's bottom edge sits on 290, so its 40 rows run from 250 to 289.
        Assert.Equal(BuildingColor, target[0, 250]);
        Assert.Equal(BuildingColor, target[199, 250]);
        Assert.Equal(0, target[200, 250]);
        Assert.Equal(0, target[0, 249]);
        Assert.Equal(0, target[0, 290]);
    }

    [Fact]
    public void DrawGuildBuilding_StandsTheWholeSpriteBottomOnTheBaseline()
    {
        MageGuildLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        // Only the top ten rows are drawn, so the drawn box is 10 pixels tall, but the sprite is 40 tall: the
        // baseline uses the sprite height, the horizontal centring uses the drawn box.
        Sprite building = CreateSpriteWithInk(40, 40, BuildingColor, inkHeight: 10);

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        MageGuildDialog.DrawGuildBuilding(target, layout, building, MapRace.Knight);

        // x = 100 - 0 - 40 / 2, y = 290 - 40.
        Assert.Equal(BuildingColor, target[80, 250]);
        Assert.Equal(BuildingColor, target[80, 259]);
        Assert.Equal(0, target[80, 260]);
    }

    [Fact]
    public void DrawGuildBuilding_RecoloursTheWizardPictureForARandomRace()
    {
        MageGuildLayout layout = CreateDialog().CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());
        Sprite building = CreateOpaqueSprite(40, 40, WildColor);

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        MageGuildDialog.DrawGuildBuilding(target, layout, building, MapRace.Random);

        byte expected = PaletteTable.Apply(PaletteId.Purple, WildColor);

        Assert.NotEqual(WildColor, expected);
        Assert.Equal(expected, target[80, 250]);

        // The caller's sprite is not repainted in place.
        Assert.Equal(WildColor, building.Image[0, 0]);
    }

    [Fact]
    public void Draw_PaintsThePlateTheGuildTheRowsAndTheBar()
    {
        MageGuildDialog dialog = CreateDialog([SpellType.Fireball]);
        MageGuildArtwork artwork = CreateArtwork();
        MageGuildLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), artwork);

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        dialog.Draw(
            target,
            layout,
            artwork,
            new FakeGlyphSource(NameColor, 10),
            CreateOpaqueSprite(100, 40, BuildingColor),
            dialog.CreateRows(layout, artwork));

        // The plate fills the window; the top right corner is covered by nothing else.
        Assert.Equal(PlateColor, target[639, 0]);

        // The guild picture sits in the left column.
        Assert.Equal(BuildingColor, target[50, 250]);

        // The first row's scroll, and the bar under everything.
        Assert.Equal(ScrollColor, target[220, 365]);
        Assert.Equal(BarColour(1), target[20, 461]);
    }

    [Fact]
    public void CreateButtons_ListsTheArrowsThenTheExitAndDisablesThemForASingleTown()
    {
        MageGuildArtwork artwork = CreateArtwork();

        MageGuildDialog many = CreateDialog(canSwitchTowns: true);
        MageGuildLayout manyLayout = many.CreateLayout(new Rect(0, 0, 640, 480), artwork);
        List<ButtonSprite> manyButtons = [.. many.CreateButtons(manyLayout, artwork)];

        Assert.Equal(3, manyButtons.Count);
        Assert.Equal(manyLayout.PreviousTown.Position, manyButtons[0].Area.Position);
        Assert.Equal(manyLayout.NextTown.Position, manyButtons[1].Area.Position);
        Assert.Equal(manyLayout.Exit.Position, manyButtons[2].Area.Position);
        Assert.All(manyButtons, button => Assert.True(button.IsEnabled));

        MageGuildDialog one = CreateDialog(canSwitchTowns: false);
        MageGuildLayout oneLayout = one.CreateLayout(new Rect(0, 0, 640, 480), artwork);
        List<ButtonSprite> oneButtons = [.. one.CreateButtons(oneLayout, artwork)];

        // Both arrows are drawn but refuse a press, exactly like Button::disable().
        Assert.False(oneButtons[0].IsEnabled);
        Assert.False(oneButtons[1].IsEnabled);
        Assert.True(oneButtons[2].IsEnabled);
        Assert.False(oneButtons[0].Press());
        Assert.True(oneButtons[2].Press());
    }

    [Fact]
    public void GetStatusText_DescribesTheButtonUnderThePointer()
    {
        MageGuildDialog dialog = CreateDialog(canSwitchTowns: true);
        MageGuildLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        Assert.Equal("Exit Mage Guild", dialog.GetStatusText(new Point(610, 465), layout));
        Assert.Equal("Show previous town", dialog.GetStatusText(new Point(5, 465), layout));
        Assert.Equal("Show next town", dialog.GetStatusText(new Point(585, 465), layout));

        // Anywhere else the default line shows.
        Assert.Equal(dialog.DefaultStatusText, dialog.GetStatusText(new Point(300, 100), layout));
    }

    [Fact]
    public void GetStatusText_SaysNothingForADisabledArrow()
    {
        MageGuildDialog dialog = CreateDialog(canSwitchTowns: false);
        MageGuildLayout layout = dialog.CreateLayout(new Rect(0, 0, 640, 480), CreateArtwork());

        Assert.Equal(dialog.DefaultStatusText, dialog.GetStatusText(new Point(5, 465), layout));
        Assert.Equal(dialog.DefaultStatusText, dialog.GetStatusText(new Point(585, 465), layout));
        Assert.Equal("Exit Mage Guild", dialog.GetStatusText(new Point(610, 465), layout));
    }

    [Theory]
    [InlineData(0, MageGuildDialogAction.PreviousTown)]
    [InlineData(1, MageGuildDialogAction.NextTown)]
    [InlineData(2, MageGuildDialogAction.Exit)]
    [InlineData(9, MageGuildDialogAction.Exit)]
    public void FromButton_MapsThePressToTheResult(int index, MageGuildDialogAction expected)
    {
        Assert.Equal(expected, MageGuildDialog.FromButton(index));
    }

    [Fact]
    public void Artwork_PicksTheArchivesOfTheReference()
    {
        Assert.Equal(IcnType.Stonebak, MageGuildArtwork.GetBackgroundIcn(evilInterface: false));
        Assert.Equal(IcnType.StonebakEvil, MageGuildArtwork.GetBackgroundIcn(evilInterface: true));

        Assert.Equal(IcnType.Magegldk, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Knight));
        Assert.Equal(IcnType.Magegldb, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Barbarian));
        Assert.Equal(IcnType.Mageglds, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Sorceress));
        Assert.Equal(IcnType.Magegldw, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Warlock));
        Assert.Equal(IcnType.Magegldz, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Wizard));
        Assert.Equal(IcnType.Magegldn, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Necromancer));

        // The reference draws the wizard's guild for a random race and for the "multiple" one.
        Assert.Equal(IcnType.Magegldz, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Random));
        Assert.Equal(IcnType.Magegldz, MageGuildArtwork.GetGuildBuildingIcn(MapRace.Multiple));
    }

    [Fact]
    public void Artwork_RefusesAnArchiveWithoutTheFramesItNeeds()
    {
        Sprite sprite = CreateSprite(10, 10, PlateColor);
        Sprite[] icons = [sprite];
        Sprite[] background = [sprite];
        Sprite[] exit = [sprite, sprite];

        Assert.Null(MageGuildArtwork.TryCreate(null, CreateBarFrames(), [sprite, sprite], icons, exit));
        Assert.Null(MageGuildArtwork.TryCreate(background, CreateBarFrames()[..4], [sprite, sprite], icons, exit));
        Assert.Null(MageGuildArtwork.TryCreate(background, CreateBarFrames(), [sprite], icons, exit));
        Assert.Null(MageGuildArtwork.TryCreate(background, CreateBarFrames(), [sprite, sprite], null, exit));
        Assert.Null(MageGuildArtwork.TryCreate(background, CreateBarFrames(), [sprite, sprite], icons, [sprite]));
        Assert.NotNull(MageGuildArtwork.TryCreate(background, CreateBarFrames(), [sprite, sprite], icons, exit));
    }

    [Fact]
    public void Artwork_ReturnsNoIconPastTheEndOfTheTable()
    {
        MageGuildArtwork artwork = CreateArtwork();

        // The fake table stops at the fireball icon; Petrification's icon (66) is far past it.
        Assert.NotNull(artwork.GetSpellIcon(SpellType.Fireball));
        Assert.Null(artwork.GetSpellIcon(SpellType.Petrify));
    }

    private static MageGuildDialog CreateDialog(
        IReadOnlyList<SpellType>? firstRow = null,
        bool canSwitchTowns = true,
        int guildLevel = 1)
    {
        List<IReadOnlyList<SpellType>> rows =
        [
            firstRow ?? [SpellType.Fireball],
            [],
            [],
            [],
            [],
        ];

        return MageGuildDialog.Create(rows, heroHasSpellBook: true, canSwitchTowns, MapRace.Knight, guildLevel);
    }

    private static MageGuildArtwork CreateArtwork(int exitWidth = 40)
    {
        return MageGuildArtwork.TryCreate(
            [CreateSprite(640, 480, PlateColor)],
            CreateBarFrames(),
            [CreateSprite(60, 40, ScrollColor), CreateSprite(30, 40, FoldedColor)],
            CreateSpellIcons(),
            [CreateSprite(exitWidth, 16, ExitColor), CreateSprite(exitWidth, 16, ExitPressedColor)])!;
    }

    private static Sprite[] CreateBarFrames()
    {
        Sprite[] frames = new Sprite[MageGuildArtwork.RequiredBarFrames];
        frames[MageGuildArtwork.BarFrame] = CreateBarSprite();

        for (int index = 1; index < frames.Length; ++index)
        {
            frames[index] = CreateSprite(20, 16, index % 2 == 0 ? ArrowPressedColor : ArrowColor);
        }

        return frames;
    }

    /// <summary>A bar whose every column carries a colour that identifies its archive column.</summary>
    private static Sprite CreateBarSprite()
    {
        PixelBuffer image = new(BarSourceWidth, BarHeight);
        image.Fill(0);

        for (int y = 0; y < BarHeight; ++y)
        {
            Span<byte> row = image.Row(y);

            for (int x = 0; x < BarSourceWidth; ++x)
            {
                row[x] = BarColour(x);
            }
        }

        return new Sprite(image);
    }

    private static byte BarColour(int column) => (byte)(5 + (column % 150));

    private static Sprite[] CreateSpellIcons()
    {
        int count = (int)new Spell(SpellType.Fireball).IndexSprite() + 1;
        Sprite[] icons = new Sprite[count];
        Array.Fill(icons, CreateSprite(24, 24, IconColor));

        return icons;
    }

    private static Sprite CreateOpaqueSprite(int width, int height, byte color)
    {
        // Transform 0 everywhere: the whole grid counts as drawn.
        return CreateSpriteWithInk(width, height, color, inkHeight: height);
    }

    private static Sprite CreateSpriteWithInk(int width, int height, byte color, int inkHeight)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);
        byte[] transform = new byte[width * height];
        Array.Fill(transform, (byte)1);

        for (int y = 0; y < inkHeight; ++y)
        {
            Array.Fill(transform, (byte)0, y * width, width);
        }

        return new Sprite(image, 0, 0, transform);
    }

    private static Sprite CreateSprite(int width, int height, byte color)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image);
    }

    private const int BarSourceWidth = 600;
    private const int BarHeight = 16;

    private const byte PlateColor = 200;
    private const byte ScrollColor = 201;
    private const byte FoldedColor = 202;
    private const byte IconColor = 203;
    private const byte NameColor = 204;
    private const byte BuildingColor = 205;
    private const byte ArrowColor = 206;
    private const byte ArrowPressedColor = 207;
    private const byte ExitColor = 208;
    private const byte ExitPressedColor = 209;

    /// <summary>A colour the purple table must change; the recolour test checks that it does.</summary>
    private const byte WildColor = 30;

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
