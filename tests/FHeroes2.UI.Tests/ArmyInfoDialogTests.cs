using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The layout, the text and the painting of the unit information window
/// (<c>dialog_armyinfo.cpp:126-284</c> and <c>515-572</c>).
/// </summary>
public sealed class ArmyInfoDialogTests
{
    private const int NormalLineHeight = 15;
    private const int SmallLineHeight = 10;

    [Fact]
    public void CreateLayout_PutsTheWindowInTheMiddleOfTheScreen()
    {
        ArmyInfoDialog dialog = ArmyInfoDialog.Create(new Troop(MonsterType.Peasant, 10));
        ArmyInfoLayout layout = dialog.CreateLayout(
            new Rect(0, 0, 640, 480),
            CreateArtwork(),
            new Size(40, 20),
            windowOffsetY: 0,
            new FakeGlyphSource(NormalLineHeight),
            new FakeGlyphSource(SmallLineHeight));

        Assert.Equal(new Rect(60, 112, 520, 256), layout.Window);
        Assert.Equal(new Rect(62, 114, 520, 256), layout.Shadow);
        Assert.Equal(layout.Window, layout.Dialog);

        // pos_rt + ( 400, 37 ), ( 395, 184 ) and ( 520 / 4 + 16, 175 ).
        Assert.Equal(new Point(460, 149), layout.Stats);
        Assert.Equal(new Point(455, 296), layout.BattleStats);
        Assert.Equal(new Point(206, 287), layout.Monster);

        Assert.Equal(new Rect(89, 149, 227, NormalLineHeight), layout.Name);
        Assert.Equal(new Rect(97, 297, 210, 3 * SmallLineHeight), layout.Description);
        Assert.Equal(new Rect(140, 335, 125, 23), layout.Amount);

        // pos_rt.x + width - 58 - exitWidth + 18 on the good interface.
        Assert.Equal(new Rect(500, 333, 40, 20), layout.ExitButton);

        // No upgrade or dismiss flags, so no buttons: the reference would not even load their sprites.
        Assert.Null(layout.UpgradeButton);
        Assert.Null(layout.DismissButton);
    }

    [Fact]
    public void CreateLayout_ShiftsTheEvilWindowDownAndRight()
    {
        ArmyInfoDialog dialog = ArmyInfoDialog.Create(
            new Troop(MonsterType.Peasant, 10),
            DialogButton.Ok | DialogButton.Upgrade | DialogButton.Dismiss);

        ArmyInfoArtwork artwork = CreateArtwork(
            evilInterface: true,
            upgrade: new Size(30, 20),
            dismiss: new Size(30, 20));

        ArmyInfoLayout layout = dialog.CreateLayout(
            new Rect(0, 0, 640, 480),
            artwork,
            new Size(40, 20),
            windowOffsetY: 4,
            new FakeGlyphSource(NormalLineHeight),
            new FakeGlyphSource(SmallLineHeight));

        // The window sprite goes down 3 + 4 pixels and pos_rt is then pushed by ( 9, -1 ).
        Assert.Equal(new Rect(60, 119, 520, 256), layout.Window);
        Assert.Equal(new Rect(69, 118, 520, 256), layout.Dialog);
        Assert.Equal(new Rect(98, 155, 227, NormalLineHeight), layout.Name);

        // The evil exit button has no 18 pixel allowance, and it is measured from pos_rt, which moved right.
        Assert.Equal(new Rect(491, 339, 40, 20), layout.ExitButton);

        Assert.Equal(new Rect(349, 310, 30, 20), layout.UpgradeButton);
        Assert.Equal(new Rect(349, 339, 30, 20), layout.DismissButton);
    }

    [Fact]
    public void CreateLayout_GivesAGoodUpgradeButtonTheSameOffsets()
    {
        ArmyInfoDialog dialog = ArmyInfoDialog.Create(
            new Troop(MonsterType.Archer, 10),
            DialogButton.Ok | DialogButton.Upgrade);

        ArmyInfoLayout layout = dialog.CreateLayout(
            new Rect(0, 0, 640, 480),
            CreateArtwork(upgrade: new Size(30, 20)),
            new Size(40, 20),
            windowOffsetY: 0,
            new FakeGlyphSource(NormalLineHeight),
            new FakeGlyphSource(SmallLineHeight));

        Assert.Equal(new Rect(340, 304, 30, 20), layout.UpgradeButton);
        Assert.Null(layout.DismissButton);
    }

    [Fact]
    public void GetStatLines_PrintsTheSingleCreaturesDamageNotTheStacks()
    {
        // Damage and hit points describe one archer; the stack of 10 is only in the amount box.
        List<ArmyInfoDialog.StatLine> lines =
            ArmyInfoDialog.Create(new Troop(MonsterType.Archer, 10)).GetStatLines();

        Assert.Equal(
            ["Attack Skill", "Defense Skill", "Shots", "Damage", "Hit Points", "Speed", "Morale", "Luck"],
            lines.Select(line => line.Label));

        Assert.Equal("2-3", lines[3].Value);
        Assert.Equal("10", lines[4].Value);
        Assert.Equal("Very Slow (2)", lines[5].Value);
        Assert.Equal("Normal", lines[6].Value);
        Assert.Equal("Normal", lines[7].Value);
    }

    [Fact]
    public void GetStatLines_DropsTheShotsLineForANonShooter()
    {
        List<ArmyInfoDialog.StatLine> lines =
            ArmyInfoDialog.Create(new Troop(MonsterType.Peasant, 5)).GetStatLines();

        Assert.Equal(
            ["Attack Skill", "Defense Skill", "Damage", "Hit Points", "Speed", "Morale", "Luck"],
            lines.Select(line => line.Label));

        // The peasant hits for one, so the reference prints one number and not "1-1".
        Assert.Equal("1", lines[2].Value);
    }

    [Fact]
    public void GetStatLines_PrintsTheMoraleAndLuckItWasGiven()
    {
        List<ArmyInfoDialog.StatLine> lines = ArmyInfoDialog.Create(
            new Troop(MonsterType.Peasant, 5),
            moraleValue: 3,
            luckValue: -1).GetStatLines();

        Assert.Equal("Blood!", lines[5].Value);
        Assert.Equal("Bad", lines[6].Value);
    }

    [Fact]
    public void GetDescriptionLayout_BottomAlignsTheBlock()
    {
        // Whatever shape the block takes, its last line sits on the last of the three rows.
        FakeGlyphSource font = new(SmallLineHeight);
        (int startRow, List<string> lines) = ArmyInfoDialog.Create(new Troop(MonsterType.Paladin, 1))
            .GetDescriptionLayout(font);

        Assert.InRange(lines.Count, 1, 3);
        Assert.Equal(3 - lines.Count, startRow);
        Assert.All(lines, line => Assert.True(GlyphText.Measure(font, line) <= 210));
    }

    [Fact]
    public void GetDescriptionLayout_WrapsWhenASentenceIsWiderThanTheColumn()
    {
        // The earth elemental's second sentence is wider than 210 pixels, so the three are joined and wrapped
        // instead of each taking a line; the wrapped block still ends on the last row.
        FakeGlyphSource font = new(SmallLineHeight);
        (int startRow, List<string> lines) = ArmyInfoDialog.Create(new Troop(MonsterType.EarthElement, 1))
            .GetDescriptionLayout(font);

        Assert.Equal(3 - lines.Count, startRow);
        Assert.True(lines.Count > 1);
        Assert.StartsWith("No Morale.", lines[0]);

        string joined = string.Join(' ', lines);
        Assert.Contains("Immune to Lightning", joined);
        Assert.Contains("Meteor Shower", joined);
    }

    [Fact]
    public void GetDescriptionLayout_IsEmptyForACreatureWithoutProperties()
    {
        (int startRow, List<string> lines) =
            ArmyInfoDialog.Create(new Troop(MonsterType.Peasant, 1)).GetDescriptionLayout(
                new FakeGlyphSource(SmallLineHeight));

        Assert.Equal(0, startRow);
        Assert.Empty(lines);
    }

    [Fact]
    public void Draw_PaintsTheWindowTheNameAndTheAmount()
    {
        ArmyInfoDialog dialog = ArmyInfoDialog.Create(new Troop(MonsterType.Archer, 7));
        ArmyInfoLayout layout = dialog.CreateLayout(
            new Rect(0, 0, 640, 480),
            CreateArtwork(),
            new Size(40, 20),
            windowOffsetY: 0,
            new FakeGlyphSource(NormalLineHeight),
            new FakeGlyphSource(SmallLineHeight));

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        dialog.Draw(
            target,
            layout,
            CreateArtwork(),
            new FakeGlyphSource(AttributeColor),
            new FakeGlyphSource(DescriptionColor),
            new FakeGlyphSource(NameColor));

        // The window and its shadow arrive; the shadow is only visible past the window's bottom right corner,
        // because the window is painted over it.
        Assert.Equal(ChromeColor, target[layout.Window.X, layout.Window.Y]);
        Assert.Equal(ShadowColor, target[layout.Shadow.X + 519, layout.Shadow.Y + 255]);

        // The name is centred in its 227 pixel band, two pixels down like the reference: "Archer" is 36 pixels
        // wide, so it starts 95 pixels in and this pixel is inside its second letter.
        Assert.Equal(NameColor, target[layout.Name.X + 113, layout.Name.Y + 2]);

        // Nothing of the amount box is painted outside the box.
        Assert.NotEqual(AttributeColor, target[layout.Amount.X - 1, layout.Amount.Y + 11]);
    }

    [Fact]
    public void Draw_LeavesTheAmountBoxEmptyForAnEmptyStack()
    {
        ArmyInfoDialog dialog = ArmyInfoDialog.Create(new Troop(MonsterType.Archer, 0));
        ArmyInfoLayout layout = dialog.CreateLayout(
            new Rect(0, 0, 640, 480),
            CreateArtwork(),
            new Size(40, 20),
            windowOffsetY: 0,
            new FakeGlyphSource(NormalLineHeight),
            new FakeGlyphSource(SmallLineHeight));

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        dialog.Draw(
            target,
            layout,
            CreateArtwork(),
            new FakeGlyphSource(AttributeColor),
            new FakeGlyphSource(DescriptionColor));

        // The box interior stays the window colour: no digits were drawn into it.
        Assert.Equal(ChromeColor, target[layout.Amount.X + 20, layout.Amount.Y + 11]);
        Assert.Equal(ChromeColor, target[layout.Amount.X + 60, layout.Amount.Y + 11]);
    }

    [Fact]
    public void Draw_FlipsTheUnitWhenAsked()
    {
        Sprite unit = CreateSplitSprite();
        ArmyInfoArtwork artwork = CreateArtwork();
        FakeGlyphSource font = new(SmallLineHeight);

        ArmyInfoDialog straight = ArmyInfoDialog.Create(new Troop(MonsterType.Archer, 1));
        ArmyInfoDialog mirrored = ArmyInfoDialog.Create(new Troop(MonsterType.Archer, 1), isReflected: true);

        ArmyInfoLayout layout = straight.CreateLayout(
            new Rect(0, 0, 640, 480), artwork, new Size(40, 20), 0, font, font);

        PixelBuffer straightTarget = new(640, 480);
        PixelBuffer mirroredTarget = new(640, 480);
        straightTarget.Fill(0);
        mirroredTarget.Fill(0);

        straight.Draw(straightTarget, layout, artwork, font, font, unitSprite: unit);
        mirrored.Draw(mirroredTarget, layout, artwork, font, font, unitSprite: unit);

        // The sprite is anchored at its feet: its top left corner is its anchor here, because the anchors are zero.
        Assert.Equal(UnitColor, straightTarget[layout.Monster.X, layout.Monster.Y]);
        Assert.Equal(OtherColor, straightTarget[layout.Monster.X + 7, layout.Monster.Y]);

        // The flip mirrors the two halves around the vertical axis and leaves the anchor alone.
        Assert.Equal(OtherColor, mirroredTarget[layout.Monster.X, layout.Monster.Y]);
        Assert.Equal(UnitColor, mirroredTarget[layout.Monster.X + 7, layout.Monster.Y]);
    }

    [Fact]
    public void CreateButtons_ListsUpgradeDismissAndExitInThatOrder()
    {
        ArmyInfoDialog dialog = ArmyInfoDialog.Create(
            new Troop(MonsterType.Archer, 1),
            DialogButton.Ok | DialogButton.Upgrade | DialogButton.Dismiss);

        ArmyInfoArtwork artwork = CreateArtwork(
            upgrade: new Size(30, 20),
            dismiss: new Size(30, 20),
            exit: new Size(40, 20));

        ArmyInfoLayout layout = dialog.CreateLayout(
            new Rect(0, 0, 640, 480),
            artwork,
            new Size(40, 20),
            windowOffsetY: 0,
            new FakeGlyphSource(NormalLineHeight),
            new FakeGlyphSource(SmallLineHeight));

        List<ButtonSprite> buttons = dialog.CreateButtons(layout, artwork);

        Assert.Equal(3, buttons.Count);
        Assert.Equal(layout.UpgradeButton!.Value.Position, buttons[0].Area.Position);
        Assert.Equal(layout.DismissButton!.Value.Position, buttons[1].Area.Position);
        Assert.Equal(layout.ExitButton.Position, buttons[2].Area.Position);
    }

    [Fact]
    public void CreateButtons_LeavesOutTheButtonsTheFlagsDoNotAskFor()
    {
        ArmyInfoDialog dialog = ArmyInfoDialog.Create(new Troop(MonsterType.Archer, 1), DialogButton.Ok);
        ArmyInfoArtwork artwork = CreateArtwork(upgrade: new Size(30, 20), dismiss: new Size(30, 20));

        ArmyInfoLayout layout = dialog.CreateLayout(
            new Rect(0, 0, 640, 480),
            artwork,
            new Size(40, 20),
            windowOffsetY: 0,
            new FakeGlyphSource(NormalLineHeight),
            new FakeGlyphSource(SmallLineHeight));

        List<ButtonSprite> buttons = dialog.CreateButtons(layout, artwork);

        Assert.Single(buttons);
    }

    [Fact]
    public void TryCreate_RefusesAnArchiveWithoutTheShadowFrame()
    {
        Sprite[] frames = new Sprite[6];
        for (int index = 0; index < frames.Length; ++index)
        {
            frames[index] = CreateSprite(4, 4, ChromeColor);
        }

        Assert.Null(ArmyInfoArtwork.TryCreate(frames));
        Assert.Null(ArmyInfoArtwork.TryCreate(null));
        Assert.NotNull(ArmyInfoArtwork.TryCreate(CreateFrames(8)));
    }

    private static ArmyInfoArtwork CreateArtwork(
        bool evilInterface = false,
        Size? upgrade = null,
        Size? dismiss = null,
        Size? exit = null) =>
        ArmyInfoArtwork.TryCreate(
            CreateFrames(8),
            evilInterface,
            exit is { } exitSize ? new DialogButtonArtwork(CreateSprite(exitSize, 1), CreateSprite(exitSize, 2)) : null,
            upgrade is { } upgradeSize
                ? new DialogButtonArtwork(CreateSprite(upgradeSize, 1), CreateSprite(upgradeSize, 2))
                : null,
            dismiss is { } dismissSize
                ? new DialogButtonArtwork(CreateSprite(dismissSize, 1), CreateSprite(dismissSize, 2))
                : null)!;

    private static Sprite[] CreateFrames(int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int index = 0; index < count; ++index)
        {
            frames[index] = index == ArmyInfoArtwork.ShadowFrame
                ? CreateSprite(520, 256, ShadowColor, offsetX: 2, offsetY: 2)
                : CreateSprite(520, 256, ChromeColor);
        }

        return frames;
    }

    private static Sprite CreateSprite(int width, int height, byte color, int offsetX = 0, int offsetY = 0) =>
        CreateSprite(new Size(width, height), color, offsetX, offsetY);

    private static Sprite CreateSprite(Size size, byte color, int offsetX = 0, int offsetY = 0)
    {
        PixelBuffer image = new(size.Width, size.Height);
        image.Fill(color);
        return new Sprite(image, offsetX, offsetY);
    }

    /// <summary>A left half and a right half in two colours, so a mirror can be seen.</summary>
    private static Sprite CreateSplitSprite()
    {
        PixelBuffer image = new(8, 8);
        for (int y = 0; y < 8; ++y)
        {
            Span<byte> row = image.Row(y);
            row[..4].Fill(UnitColor);
            row[4..].Fill(OtherColor);
        }

        return new Sprite(image, 0, 0);
    }

    private const byte ChromeColor = 10;
    private const byte ShadowColor = 20;
    private const byte AttributeColor = 30;
    private const byte DescriptionColor = 40;
    private const byte NameColor = 50;
    private const byte UnitColor = 60;
    private const byte OtherColor = 70;

    /// <summary>
    /// A glyph source with fixed metrics whose glyphs are solid blocks, so a test can tell one text block from
    /// another by colour alone.
    /// </summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
            : this(color, NormalLineHeight)
        {
        }

        public FakeGlyphSource(int lineHeight)
            : this(AttributeColor, lineHeight)
        {
        }

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

        public int GetAdvance(char character) => character == GlyphText.SpaceCharacter ? SpaceWidth : 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
