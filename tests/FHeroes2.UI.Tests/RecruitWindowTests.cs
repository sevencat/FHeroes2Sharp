using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The recruit window, <c>Dialog::RecruitMonster()</c> (<c>dialog_recruit.cpp:253-668</c>).
/// </summary>
/// <remarks>
/// The screen is the usual 640x480, so the 299x272 window taken from it starts at (170,104) and every offset of the
/// reference is readable in the expected numbers below. Peasants cost 20 gold and rangers 250 with archers at 150, so
/// the treasury decides the maximum.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class RecruitWindowTests
{
    private const byte BackgroundColour = 7;
    private const byte TitleColour = 11;
    private const byte TextColour = 60;
    private const byte ShadowColour = 5;
    private const byte MonsterColour = 33;
    private const byte ButtonColour = 21;

    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TheWindowIsTheReferenceSizeCentredOnTheScreen()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 1000, available: 20));

        Assert.Equal(new Rect(170, 104, 299, 272), view.Window.ActiveArea);
        Assert.Equal(170 + 118, view.Layout.CountBoxPosition.X);
        Assert.Equal(104 + 143, view.Layout.CountBoxPosition.Y);
    }

    [Fact]
    public void TheWindowOffsetPushesTheWindowDown()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 1000, available: 20), windowOffsetY: 12);

        Assert.Equal(104 + 12, view.Window.ActiveArea.Y);
    }

    [Fact]
    public void EveryElementSitsWhereTheReferencePutsIt()
    {
        RecruitWindowLayout layout = CreateView(CreateDialog(gold: 1000, available: 20)).Layout;

        Assert.Equal(new Rect(288, 247, 68, 19), layout.CountBoxArea);
        Assert.Equal(new Point(302, 142), layout.CostFramePosition);
        Assert.Equal(new Point(188, 337), layout.OkayButtonPosition);
        Assert.Equal(new Point(411, 337), layout.CancelButtonPosition(40));
        Assert.Equal(new Point(408, 244), layout.MaxMinButtonPosition(30));
        Assert.Equal(new Point(359, 242), layout.UpButtonPosition);
        Assert.Equal(new Point(359, 257), layout.DownButtonPosition);
        Assert.Equal(new Rect(284, 243, 100, 30), layout.WheelArea);
        Assert.Equal(new Point(176, 168), layout.SwitchLeftPosition);
        Assert.Equal(new Point(275, 168), layout.SwitchRightPosition);
        Assert.Equal(new Rect(194, 123, 75, 95), layout.MonsterArea);
    }

    [Fact]
    public void TheTitleIsCentredAndTheStockLineMovesDownForTallUnits()
    {
        RecruitWindowLayout layout = CreateView(CreateDialog(gold: 1000, available: 20)).Layout;

        Assert.Equal(new Point(294, 115), layout.TitlePosition(50));
        Assert.Equal(new Point(234, 226), layout.AvailableCentre(90));
        Assert.Equal(new Point(234, 248), layout.AvailableCentre(120));
        Assert.Equal(new Point(217, 253), layout.NumberToBuyPosition(60));
    }

    [Fact]
    public void TheMonsterIsAnchoredOnItsBaselineAndNudgedForWideOnes()
    {
        RecruitWindowLayout layout = CreateView(CreateDialog(gold: 1000, available: 20)).Layout;
        Sprite sprite = new(new PixelBuffer(75, 90), 0, 0);

        Assert.Equal(0, RecruitWindowLayout.MonsterExtraOffsetY(90));
        Assert.Equal(24, RecruitWindowLayout.MonsterExtraOffsetY(120));
        Assert.Equal(new Point(234, 133), layout.MonsterSpritePosition(sprite, isWide: false, isChampion: false));
        Assert.Equal(new Point(212, 133), layout.MonsterSpritePosition(sprite, isWide: true, isChampion: false));
        Assert.Equal(new Point(235, 133), layout.MonsterSpritePosition(sprite, isWide: false, isChampion: true));
    }

    [Fact]
    public void ThePriceAndLabelLinesAreAnchoredUnderTheCount()
    {
        RecruitWindowLayout layout = CreateView(CreateDialog(gold: 1000, available: 20)).Layout;

        Assert.Equal(new Point(314, 318), layout.GoldLineCentre(withSecondResource: false));
        Assert.Equal(new Point(287, 318), layout.GoldLineCentre(withSecondResource: true));
        Assert.Equal(new Point(349, 318), layout.ExtraLineCentre);
        Assert.Equal(new Point(321, 270), layout.LabelCentre);
        Assert.Equal(new Point(321, 251), layout.CountTextCentre);
    }

    [Fact]
    public void TheCountBoxIsSlicedOutOfTheRecruitBackground()
    {
        RecruitWindowArtwork artwork = CreateArtwork();

        Assert.Equal(68, artwork.CountBox.Width);
        Assert.Equal(19, artwork.CountBox.Height);

        // The slice starts at (134,159) of the frame, so its own (6,6) is the frame's (140,165).
        Assert.Equal(CreateBackground().Image[140, 165], artwork.CountBox.Image[6, 6]);
    }

    [Fact]
    public void TheCostFrameIsStitchedFromTheTitleAndTheBorders()
    {
        RecruitWindowArtwork artwork = CreateArtwork();

        Assert.Equal(RecruitWindowArtwork.CostFrameWidth, artwork.CostFrame.Width);
        Assert.Equal(RecruitWindowArtwork.CostFrameHeight, artwork.CostFrame.Height);

        // The left border comes from (138,54) of the background, the header from (3,58) of the title frame.
        Assert.Equal(CreateBackground().Image[138, 54], artwork.CostFrame.Image[0, 0]);
        Assert.Equal(CreateTitleBackground().Image[3, 58], artwork.CostFrame.Image[3, 0]);
        Assert.Equal(CreateTitleBackground().Image[234, 58], artwork.CostFrame.Image[66, 0]);
    }

    [Fact]
    public void TheCostFrameCarriesItsCaption()
    {
        RecruitWindowArtwork artwork = CreateArtwork();
        int coloured = 0;

        for (int y = 0; y < artwork.CostFrame.Height; ++y)
        {
            for (int x = 0; x < artwork.CostFrame.Width; ++x)
            {
                if (artwork.CostFrame.Image[x, y] == TextColour)
                {
                    ++coloured;
                }
            }
        }

        Assert.True(coloured > 0);
    }

    [Fact]
    public void TryCreate_RejectsMissingArtworkAndArguments()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        Assert.Null(RecruitWindowView.TryCreate(dialog, null, CreateFonts(), Screen));
        Assert.Null(RecruitWindowView.TryCreate(dialog, CreateArtwork(arrows: 2), CreateFonts(), Screen));
        Assert.Throws<ArgumentNullException>(
            () => RecruitWindowView.TryCreate(null!, CreateArtwork(), CreateFonts(), Screen));
        Assert.Throws<ArgumentNullException>(
            () => RecruitWindowView.TryCreate(dialog, CreateArtwork(), null!, Screen));
    }

    [Fact]
    public void TheCountIsPaintedInItsBox()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));
        PixelBuffer target = new(Screen.Width, Screen.Height);

        Assert.True(DrawWithTheWindowArtwork(view, target));

        Assert.True(CountColour(target, TextColour, view.Layout.CountBoxArea) > 0);
        Assert.Equal("5", view.CountText);
    }

    [Fact]
    public void ThePriceLinesAreThePaymentAndWhatIsLeft()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));

        Assert.Equal("100 (0)", view.GoldLine);
        Assert.Null(view.ExtraResourceLine);

        RecruitDialog phoenix = RecruitDialog.Create(MonsterType.Phoenix, 5, CreateKingdom(gold: 3000, mercury: 2));
        RecruitWindowView priced = CreateView(phoenix);

        Assert.Equal(2u, priced.Dialog.Count);
        Assert.Equal("3000 (0)", priced.GoldLine);
        Assert.Equal("2 (0)", priced.ExtraResourceLine);
    }

    [Fact]
    public void ClickOnCancel_LeavesWithoutAnArmy()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));
        LocalEvent events = CreateEvents();

        Click(events, MouseButtonType.Left, Center(view.CancelButton.Area));

        Assert.Equal(RecruitWindowAction.Cancel, view.ProcessEvents(events));
        Assert.Null(view.Dialog.Result);
    }

    [Fact]
    public void ClickOnOkay_HandsBackTheChosenStack()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));
        view.Dialog.SetCount(3);

        LocalEvent events = CreateEvents();
        Click(events, MouseButtonType.Left, Center(view.OkayButton.Area));

        Assert.Equal(RecruitWindowAction.Okay, view.ProcessEvents(events));
        Assert.Equal(MonsterType.Peasant, view.Dialog.Result?.Monster);
        Assert.Equal(3u, view.Dialog.Result?.Count);
    }

    [Fact]
    public void OkayDoesNothingWhileNothingIsChosen()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));
        view.Dialog.SetCount(0);

        LocalEvent events = CreateEvents();
        Click(events, MouseButtonType.Left, Center(view.OkayButton.Area));

        Assert.Equal(RecruitWindowAction.None, view.ProcessEvents(events));
        Assert.False(view.OkayButton.IsEnabled);
    }

    [Fact]
    public void TheArrowsStepTheCount()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 1000, available: 20));

        LocalEvent down = CreateEvents();
        Click(down, MouseButtonType.Left, Center(view.DownButton.Area));
        view.ProcessEvents(down);

        Assert.Equal(19u, view.Dialog.Count);

        LocalEvent up = CreateEvents();
        Click(up, MouseButtonType.Left, Center(view.UpButton.Area));
        view.ProcessEvents(up);

        Assert.Equal(20u, view.Dialog.Count);
    }

    [Fact]
    public void TheWheelStepsTheCountInsideItsAreaOnly()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 1000, available: 20));

        LocalEvent outside = CreateEvents();
        outside.OnMouseMotion(new Point(10, 10));
        outside.OnMouseWheel(new Point(0, -1));
        view.ProcessEvents(outside);

        Assert.Equal(20u, view.Dialog.Count);

        LocalEvent inside = CreateEvents();
        inside.OnMouseMotion(Center(view.Layout.WheelArea));
        inside.OnMouseWheel(new Point(0, -1));
        view.ProcessEvents(inside);

        Assert.Equal(19u, view.Dialog.Count);
    }

    [Fact]
    public void ClickOnTheCountBox_AsksForTheNumpad()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));

        LocalEvent events = CreateEvents();
        Click(events, MouseButtonType.Left, Center(view.Layout.CountBoxArea));

        Assert.Equal(RecruitWindowAction.ChooseCount, view.ProcessEvents(events));
    }

    [Fact]
    public void TypedDigitsFillTheBoxAndBackspaceEmptiesIt()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 1000, available: 20));

        LocalEvent first = CreateEvents();
        first.OnKeyboard(Key.Digit1, KeyModifier.None, KeyboardEventState.Down);
        view.ProcessEvents(first);
        Assert.Equal(1u, view.Dialog.Count);

        LocalEvent second = CreateEvents();
        second.OnKeyboard(Key.Digit2, KeyModifier.None, KeyboardEventState.Down);
        view.ProcessEvents(second);
        Assert.Equal(12u, view.Dialog.Count);

        LocalEvent backspace = CreateEvents();
        backspace.OnKeyboard(Key.Backspace, KeyModifier.None, KeyboardEventState.Down);
        view.ProcessEvents(backspace);

        Assert.Equal(0u, view.Dialog.Count);
        Assert.False(view.OkayButton.IsEnabled);
    }

    [Fact]
    public void TheSharedMaxMinPlaceIsMaxThenMin()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));

        // Open at the maximum: MAX is the disabled one, so the place shows MIN, under a label that says "Max".
        Assert.False(view.ShowsMaxButton);
        Assert.False(view.MaxButton.IsEnabled);
        Assert.True(view.MinButton.IsEnabled);
        Assert.Equal(RecruitDialog.MaxTitle, view.Dialog.MaxMinLabel);

        // One step down leaves the buttons alone, so the place still draws MIN - which jumps to one when clicked.
        LocalEvent down = CreateEvents();
        Click(down, MouseButtonType.Left, Center(view.DownButton.Area));
        view.ProcessEvents(down);

        Assert.Equal(4u, view.Dialog.Count);
        Assert.False(view.ShowsMaxButton);

        LocalEvent click = CreateEvents();
        Click(click, MouseButtonType.Left, Center(view.MaxButton.Area));
        view.ProcessEvents(click);

        Assert.Equal(1u, view.Dialog.Count);
        Assert.Equal(RecruitDialog.MinTitle, view.Dialog.MaxMinLabel);

        // At one the place hands over to MAX, which goes back to the maximum.
        Assert.True(view.ShowsMaxButton);
        Assert.True(view.MaxButton.IsEnabled);

        LocalEvent again = CreateEvents();
        Click(again, MouseButtonType.Left, Center(view.MaxButton.Area));
        view.ProcessEvents(again);

        Assert.Equal(5u, view.Dialog.Count);
    }

    [Fact]
    public void AnEmptyTreasuryDrawsTheMaxButtonGreyedOut()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 19, available: 10));

        Assert.True(view.ShowsMaxButton);
        Assert.False(view.MaxButton.IsEnabled);
        Assert.False(view.MinButton.IsEnabled);

        LocalEvent events = CreateEvents();
        Click(events, MouseButtonType.Left, Center(view.MaxButton.Area));

        Assert.Equal(RecruitWindowAction.None, view.ProcessEvents(events));
        Assert.Equal(0u, view.Dialog.Count);
    }

    [Fact]
    public void RightClicksAskForTheTooltips()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));
        view.Dialog.SetCount(2);

        AssertTooltip(view, view.OkayButton, RecruitWindowView.OkayTooltipHeader,
            RecruitWindowView.OkayTooltipBody);
        AssertTooltip(view, view.CancelButton, RecruitWindowView.CancelTooltipHeader,
            RecruitWindowView.CancelTooltipBody);
    }

    [Fact]
    public void TheSharedPlaceExplainsWhicheverButtonIsLive()
    {
        // At the maximum MAX is greyed out and MIN is the live button.
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));
        AssertTooltip(view, view.MaxButton, RecruitWindowView.MinTooltipHeader, RecruitWindowView.MinTooltipBody);

        // At one it is the other way round.
        view.Dialog.SetCount(1);
        view.Refresh();
        AssertTooltip(view, view.MinButton, RecruitWindowView.MaxTooltipHeader, RecruitWindowView.MaxTooltipBody);

        // With an empty treasury neither button is live, so the place says nothing.
        RecruitWindowView empty = CreateView(CreateDialog(gold: 19, available: 10));
        LocalEvent events = CreateEvents();
        RightClick(events, Center(empty.MaxButton.Area));

        Assert.Equal(RecruitWindowAction.None, empty.ProcessEvents(events));
        Assert.Null(empty.Tooltip);
    }

    [Fact]
    public void TheSwitchArrowsOnlyShowForAUnitWithADowngrade()
    {
        RecruitWindowView peasant = CreateView(CreateDialog(gold: 100, available: 10));
        Assert.False(peasant.ShowsSwitchArrows);
        Assert.False(peasant.SwitchLeftButton.IsVisible);

        RecruitWindowView ranger = CreateView(RecruitDialog.Create(MonsterType.Ranger, 10, CreateKingdom(10_000)));
        Assert.True(ranger.ShowsSwitchArrows);
        Assert.True(ranger.SwitchLeftButton.IsVisible);
    }

    [Fact]
    public void TheLeftArrowHiresThePreviousTierAndRecomputesTheMaximum()
    {
        RecruitWindowView view = CreateView(RecruitDialog.Create(MonsterType.Ranger, 10, CreateKingdom(300)));

        Assert.Equal(1u, view.Dialog.Count);

        LocalEvent events = CreateEvents();
        Click(events, MouseButtonType.Left, Center(view.SwitchLeftButton.Area));
        view.ProcessEvents(events);

        Assert.Equal(MonsterType.Archer, view.Dialog.Monster);
        Assert.Equal(2u, view.Dialog.Count);
        Assert.Equal("300 (0)", view.GoldLine);
    }

    [Fact]
    public void ClickOnTheMonsterOpensItsDescription()
    {
        RecruitWindowView view = CreateView(CreateDialog(gold: 100, available: 10));
        Point monster = Center(view.Layout.MonsterArea);

        LocalEvent left = CreateEvents();
        Click(left, MouseButtonType.Left, monster);

        Assert.Equal(RecruitWindowAction.MonsterInfoWithButtons, view.ProcessEvents(left));

        LocalEvent right = CreateEvents();
        RightClick(right, monster);

        Assert.Equal(RecruitWindowAction.MonsterInfo, view.ProcessEvents(right));
    }

    [Fact]
    public void TheWindowNeverSpendsTheMoney()
    {
        GameKingdom kingdom = CreateKingdom(100);
        RecruitWindowView view = CreateView(RecruitDialog.Create(MonsterType.Peasant, 10, kingdom));

        LocalEvent events = CreateEvents();
        Click(events, MouseButtonType.Left, Center(view.OkayButton.Area));
        view.ProcessEvents(events);

        Assert.Equal(100, kingdom.Treasury.Gold);
    }

    private static void AssertTooltip(RecruitWindowView view, ButtonSprite button, string header, string body)
    {
        LocalEvent events = CreateEvents();
        RightClick(events, Center(button.Area));

        Assert.Equal(RecruitWindowAction.Tooltip, view.ProcessEvents(events));
        Assert.Equal(header, view.Tooltip?.Header);
        Assert.Equal(body, view.Tooltip?.Body);
    }

    private static RecruitWindowView CreateView(RecruitDialog dialog, int windowOffsetY = 0) =>
        RecruitWindowView.TryCreate(dialog, CreateArtwork(), CreateFonts(), Screen, windowOffsetY)!;

    private static RecruitDialog CreateDialog(int gold, uint available) =>
        RecruitDialog.Create(MonsterType.Peasant, available, CreateKingdom(gold));

    private static GameKingdom CreateKingdom(int gold = 0, int mercury = 0)
    {
        var kingdom = new GameKingdom();
        kingdom.Treasury.Gold = gold;
        kingdom.Treasury.Mercury = mercury;

        return kingdom;
    }

    private static LocalEvent CreateEvents() => new(() => 0);

    private static void Click(LocalEvent events, MouseButtonType button, Point point)
    {
        events.OnMouseButton(true, button, point);
        events.OnMouseButton(false, button, point);
    }

    /// <summary>The reference tests the right button while it is still held, so no release is sent.</summary>
    private static void RightClick(LocalEvent events, Point point) =>
        events.OnMouseButton(true, MouseButtonType.Right, point);

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static int CountColour(PixelBuffer canvas, byte colour, Rect area)
    {
        int count = 0;

        for (int y = area.Y; y < area.Y + area.Height; ++y)
        {
            for (int x = area.X; x < area.X + area.Width; ++x)
            {
                if (canvas[x, y] == colour)
                {
                    ++count;
                }
            }
        }

        return count;
    }

    /// <summary>Opens the game data the stone frame of the window is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Draws the dialog with the real window artwork.</summary>
    /// <param name="view">Dialog to paint.</param>
    /// <param name="target">Frame buffer to paint into.</param>
    /// <returns><see langword="false"/> when the machine has no game data.</returns>
    private static bool DrawWithTheWindowArtwork(RecruitWindowView view, PixelBuffer target)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return false;
        }

        GameAssets.Current = assets;

        try
        {
            view.Draw(target);
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }

    private static RecruitWindowFonts CreateFonts()
    {
        IGlyphSource font = new BlockFont(15, 6, TextColour);

        return new RecruitWindowFonts(font, font, font);
    }

    private static RecruitWindowArtwork CreateArtwork(int arrows = 4) =>
        RecruitWindowArtwork.TryCreate(
            [CreateBackground()],
            [CreateTitleBackground()],
            [Filled(18, 20, 41), Filled(18, 20, 42)],
            [Filled(18, 20, 43), Filled(18, 20, 44)],
            CreateFrames(arrows, new Size(10, 12), 50),
            new DialogButtonArtwork(Filled(40, 25, 61), Filled(40, 25, 62)),
            new DialogButtonArtwork(Filled(40, 25, 63), Filled(40, 25, 64)),
            new DialogButtonArtwork(Filled(30, 25, 65), Filled(30, 25, 66)),
            new DialogButtonArtwork(Filled(30, 25, 67), Filled(30, 25, 68)),
            CreateFrames(RecruitWindowArtwork.RequiredResourceFrames, new Size(25, 22), 70),
            new Sprite(new PixelBuffer(75, 90), 0, 0),
            new BlockFont(11, 5, TextColour))!;

    /// <summary>
    /// The recruit background: a filled frame whose pixel at (140,165) is recognisable, so the count box slice can be
    /// told apart from the rest of it.
    /// </summary>
    private static Sprite CreateBackground()
    {
        PixelBuffer image = new(300, 250);
        image.Fill(BackgroundColour);
        image[140, 165] = TitleColour;
        image[138, 54] = 9;

        return new Sprite(image, 0, 0);
    }

    private static Sprite CreateTitleBackground()
    {
        PixelBuffer image = new(300, 80);
        image.Fill(TitleColour);
        image[3, 58] = 12;
        image[234, 58] = 13;

        return new Sprite(image, 0, 0);
    }

    private static Sprite[] CreateFrames(int count, Size size, byte firstColour)
    {
        Sprite[] frames = new Sprite[count];

        for (int index = 0; index < count; ++index)
        {
            frames[index] = Filled(size.Width, size.Height, (byte)(firstColour + index));
        }

        return frames;
    }

    private static Sprite Filled(int width, int height, byte colour)
    {
        PixelBuffer image = new(width, height);
        image.Fill(colour);

        return new Sprite(image, 0, 0);
    }

    /// <summary>A glyph source whose glyphs are solid blocks, with the metrics of the small font or of FONT.ICN.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte colour)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;
            _glyph = Filled(advance, lineHeight, colour);
        }

        public int LineHeight { get; }

        public int SpaceWidth { get; }

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
