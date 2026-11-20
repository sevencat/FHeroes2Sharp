using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The gift dialog of <c>Dialog::MakeGiftResource()</c>: geometry, the recipient boxes, the two resource rows, the
/// count requests and the OKAY/CANCEL answers.
/// </summary>
public sealed class GiftWindowTests
{
    private const byte TradpostColorBase = 100;
    private const byte CellwinColorBase = 10;
    private const byte CheckmarkColor = 200;
    private const byte OkColor = 71;
    private const byte CancelColor = 73;
    private const byte CountColor = 60;

    [Fact]
    public void TryCreate_LaysOutTheDialogLikeTheReference()
    {
        GiftWindowView view = CreateView(out _);

        // StandardWindow( 320, 234 ) centres its active area: (640 - 320) / 2 and (480 - 234) / 2.
        Assert.Equal(new Rect(160, 123, 320, 234), view.ActiveArea);

        // The recipient block is centred in the 320 pixel dialog width: 2 * 15 + 22 = 52 pixels wide.
        Assert.Equal(new Rect(294, 151, 15, 15), view.RecipientBoxes[0]);
        Assert.Equal(new Rect(294 + 37, 151, 15, 15), view.RecipientBoxes[1]);

        // The resource run is centred with the cell width added: (320 - (34 + 240)) / 2 = 23.
        Assert.Equal(new Rect(183, 203, 34, 34), view.FundsCells[0]);
        Assert.Equal(new Rect(183 + 240, 203, 34, 34), view.FundsCells[6]);
        Assert.Equal(new Rect(183, 273, 34, 34), view.GiftCells[0]);

        // Both buttons hang 10 pixels above and beside the bottom corners of the active area.
        Assert.Equal(new Rect(170, 325, 46, 22), view.OkArea);
        Assert.Equal(new Rect(424, 325, 46, 22), view.CancelArea);
    }

    [Fact]
    public void TryCreate_WithoutRecipientsHasNoBoxes()
    {
        GiftWindowView view = CreateView(out _, recipientColors: []);

        Assert.Empty(view.RecipientBoxes);
        Assert.Empty(view.RecipientColors);
        Assert.Equal(0, view.RecipientCount);
        Assert.False(view.CanConfirm);
    }

    [Fact]
    public void TryCreate_RejectsTheSenderAndRepeatedColours()
    {
        Assert.Throws<ArgumentException>(() =>
            GiftWindowView.TryCreate(FundsOf(0), PlayerColor.Red, [PlayerColor.Red], CreateChrome(), CreateArtwork(),
                Fonts));
        Assert.Throws<ArgumentException>(() =>
            GiftWindowView.TryCreate(FundsOf(0), PlayerColor.Red, [PlayerColor.Blue, PlayerColor.Blue],
                CreateChrome(), CreateArtwork(), Fonts));
    }

    [Fact]
    public void TryCreate_WithoutArtworkReturnsNull()
    {
        Assert.Null(GiftWindowView.TryCreate(FundsOf(0), PlayerColor.Red, [PlayerColor.Blue], null, CreateArtwork(),
            Fonts));
        Assert.Null(GiftWindowView.TryCreate(FundsOf(0), PlayerColor.Red, [PlayerColor.Blue], CreateChrome(), null,
            Fonts));
        Assert.Null(GiftWindowView.TryCreate(FundsOf(0), PlayerColor.Red, [PlayerColor.Blue], CreateChrome(),
            CreateArtwork(), null));
    }

    [Fact]
    public void SelectRecipient_TogglesAndResetsThePlannedGift()
    {
        GiftWindowView view = CreateView(out _, gold: 1000);

        view.SelectRecipient(PlayerColor.Blue);

        Assert.Equal(PlayerColor.Blue, view.Recipients);
        Assert.Equal(1, view.RecipientCount);

        view.ApplySelection(Resource.Gold, 100);

        Assert.Equal(900, view.YourFunds.Gold);
        Assert.Equal(100, view.PlannedGift.Gold);

        view.SelectRecipient(PlayerColor.Green);

        Assert.Equal(2, view.RecipientCount);
        Assert.Equal(1000, view.YourFunds.Gold);
        Assert.Equal(0, view.PlannedGift.Gold);

        view.SelectRecipient(PlayerColor.Green);

        Assert.Equal(PlayerColor.Blue, view.Recipients);
    }

    [Fact]
    public void ApplySelection_ChargesEveryRecipient()
    {
        GiftWindowView view = CreateView(out _, gold: 1000, recipientColors: [PlayerColor.Blue, PlayerColor.Green]);

        view.SelectRecipient(PlayerColor.Blue);
        view.SelectRecipient(PlayerColor.Green);
        view.ApplySelection(Resource.Gold, 100);

        // The row shows the amount per recipient, so the treasury loses 100 for each of the two, and the most a
        // recipient can still get is what the treasury and the planned gift hold together, split in two.
        Assert.Equal(800, view.YourFunds.Gold);
        Assert.Equal(100, view.PlannedGift.Gold);
        Assert.Equal(450, view.MaximumFor(Resource.Gold));
    }

    [Fact]
    public void MaximumFor_SplitsTheTreasuryOverTheRecipients()
    {
        GiftWindowView view = CreateView(out _, gold: 1000, wood: 7,
            recipientColors: [PlayerColor.Blue, PlayerColor.Green]);

        Assert.Equal(1000, view.MaximumFor(Resource.Gold));
        Assert.Equal(0, view.MaximumFor(Resource.Ore));

        view.SelectRecipient(PlayerColor.Blue);
        view.SelectRecipient(PlayerColor.Green);

        Assert.Equal(500, view.MaximumFor(Resource.Gold));
        Assert.Equal(3, view.MaximumFor(Resource.Wood));
    }

    [Fact]
    public void StepFor_CountsGoldByHundreds()
    {
        Assert.Equal(100, GiftWindowView.StepFor(Resource.Gold));
        Assert.Equal(1, GiftWindowView.StepFor(Resource.Wood));
        Assert.Equal(1, GiftWindowView.StepFor(Resource.Gems));
    }

    [Fact]
    public void ProcessEvents_ClickOnABoxTogglesTheRecipient()
    {
        GiftWindowView view = CreateView(out _);
        LocalEvent events = new(() => 0);

        Click(events, Center(view.RecipientBoxes[0]));

        Assert.Equal(GiftWindowActionKind.ToggleRecipient, view.ProcessEvents(events).Kind);
        Assert.Equal(PlayerColor.Blue, view.Recipients);

        LocalEvent again = new(() => 0);
        Click(again, Center(view.RecipientBoxes[0]));
        view.ProcessEvents(again);

        Assert.Equal(PlayerColor.None, view.Recipients);
    }

    [Fact]
    public void ProcessEvents_BeforeARecipientAsksForOne()
    {
        GiftWindowView view = CreateView(out _, gold: 1000);
        LocalEvent events = new(() => 0);

        Click(events, Center(view.GiftCells[6]));

        GiftWindowAction action = view.ProcessEvents(events);

        Assert.Equal(GiftWindowActionKind.Message, action.Kind);
        Assert.Equal(GiftWindowView.NoRecipientsMessage, action.Message);
    }

    [Fact]
    public void ProcessEvents_WithARecipientRequestsTheCount()
    {
        GiftWindowView view = CreateView(out _, gold: 1000);
        LocalEvent select = new(() => 0);

        Click(select, Center(view.RecipientBoxes[0]));
        view.ProcessEvents(select);

        LocalEvent click = new(() => 0);

        Click(click, Center(view.GiftCells[6]));

        GiftWindowAction action = view.ProcessEvents(click);

        Assert.Equal(GiftWindowActionKind.RequestCount, action.Kind);
        Assert.Equal(Resource.Gold, action.Resource);
        Assert.Equal(0, action.Minimum);
        Assert.Equal(1000, action.Maximum);
        Assert.Equal(100, action.Step);
    }

    [Fact]
    public void ProcessEvents_WithAnEmptyTreasuryRefusesTheResource()
    {
        GiftWindowView view = CreateView(out _, gold: 0, wood: 4);
        LocalEvent select = new(() => 0);

        Click(select, Center(view.RecipientBoxes[0]));
        view.ProcessEvents(select);

        LocalEvent click = new(() => 0);

        Click(click, Center(view.GiftCells[6]));

        GiftWindowAction action = view.ProcessEvents(click);

        Assert.Equal(GiftWindowActionKind.Message, action.Kind);
        Assert.Equal("You cannot select Gold!", action.Message);
    }

    [Fact]
    public void ProcessEvents_OkStaysDisabledUntilAGiftIsPlanned()
    {
        GiftWindowView view = CreateView(out _, gold: 1000);
        LocalEvent select = new(() => 0);

        Click(select, Center(view.RecipientBoxes[0]));
        view.ProcessEvents(select);

        LocalEvent early = new(() => 0);
        Click(early, Center(view.OkArea));

        Assert.Equal(GiftWindowActionKind.None, view.ProcessEvents(early).Kind);

        view.ApplySelection(Resource.Gold, 100);

        LocalEvent ready = new(() => 0);
        Click(ready, Center(view.OkArea));

        Assert.Equal(GiftWindowActionKind.Confirm, view.ProcessEvents(ready).Kind);
    }

    [Fact]
    public void ProcessEvents_CancelAlwaysAnswers()
    {
        GiftWindowView view = CreateView(out _);
        LocalEvent events = new(() => 0);

        Click(events, Center(view.CancelArea));

        Assert.Equal(GiftWindowActionKind.Cancel, view.ProcessEvents(events).Kind);
    }

    [Fact]
    public void Draw_PaintsTheBoxesTheCheckmarksAndTheCounts()
    {
        GiftWindowView view = CreateView(out GiftWindowArtwork artwork, gold: 1000, wood: 4);
        LocalEvent select = new(() => 0);

        Click(select, Center(view.RecipientBoxes[1]));
        view.ProcessEvents(select);

        // A planned gift switches OKAY on; a disabled button would be drawn darkened, like the reference's.
        view.ApplySelection(Resource.Gold, 100);

        PixelBuffer target = new(640, 480);
        target.Fill(0);

        view.Draw(target);

        Rect first = view.RecipientBoxes[0];
        Rect second = view.RecipientBoxes[1];

        // The box sprite of the second colour is drawn, the first one stays unchecked.
        Assert.Equal(CellwinColor((PlayerColor)view.RecipientColors[1]), target[second.X, second.Y]);
        Assert.Equal(CellwinColor((PlayerColor)view.RecipientColors[0]), target[first.X, first.Y]);
        Assert.NotEqual(CheckmarkColor, target[first.X + 2, first.Y + 2]);
        Assert.Equal(CheckmarkColor, target[second.X + 2, second.Y + 2]);

        // The wood count sits in the first cell of both rows, a five pixel wide "4" and "0" centred under the icon.
        Rect funds = view.FundsCells[0];
        Rect gift = view.GiftCells[0];
        int centeredX = (34 - 5) / 2;

        Assert.Equal(TradpostColorBase, target[funds.X, funds.Y]);
        Assert.Equal(CountColor, target[funds.X + centeredX, funds.Y + 34 - 10]);
        Assert.Equal(CountColor, target[gift.X + centeredX, gift.Y + 34 - 10]);

        // The buttons are drawn with their own frames; the shadow sits one pixel beside them, so sample the centre.
        Assert.Equal(OkColor, target[view.OkArea.X + 23, view.OkArea.Y + 11]);
        Assert.Equal(CancelColor, target[view.CancelArea.X + 23, view.CancelArea.Y + 11]);
    }

    [Fact]
    public void Draw_PaintsTheYourFundsRowWithoutTheGift()
    {
        GiftWindowView view = CreateView(out _, gold: 1000, wood: 4);
        LocalEvent select = new(() => 0);

        Click(select, Center(view.RecipientBoxes[0]));
        view.ProcessEvents(select);
        view.ApplySelection(Resource.Wood, 3);

        Assert.Equal(1, view.YourFunds.Wood);
        Assert.Equal(3, view.PlannedGift.Wood);

        PixelBuffer target = new(640, 480);
        target.Fill(0);
        view.Draw(target);

        // Both rows still show a glyph where the count goes, and the icon of the treasury row is untouched by the
        // count: the row is drawn from the funds record, not from the selected cell.
        Assert.Equal(CountColor, target[view.GiftCells[0].X + 14, view.GiftCells[0].Y + 24]);
        Assert.Equal(TradpostColorBase + 6, target[view.FundsCells[6].X, view.FundsCells[6].Y]);
    }

    private static byte CellwinColor(PlayerColor color) => (byte)(CellwinColorBase + PlayerColors.IndexOf(color));

    private static GiftWindowView CreateView(out GiftWindowArtwork artwork, int gold = 0, int wood = 0,
        IReadOnlyList<PlayerColor>? recipientColors = null)
    {
        artwork = CreateArtwork();

        GiftWindowView view = GiftWindowView.TryCreate(
            FundsOf(gold, wood),
            PlayerColor.Red,
            recipientColors ?? [PlayerColor.Blue, PlayerColor.Green],
            CreateChrome(),
            artwork,
            Fonts)!;

        return view;
    }

    private static Funds FundsOf(int gold, int wood = 0)
    {
        Funds funds = default;
        funds.Gold = gold;
        funds.Wood = wood;

        return funds;
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static void Click(LocalEvent events, Point point)
    {
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);
    }

    private static IGlyphSource NormalFont { get; } = new BlockFont(15, 6, 90);

    private static IGlyphSource SmallFont { get; } = new BlockFont(11, 5, CountColor);

    private static GiftWindowFonts Fonts { get; } = new(NormalFont, SmallFont);

    private static GiftWindowArtwork CreateArtwork()
    {
        List<Sprite> tradpost = [];

        for (int index = 0; index < GiftWindowArtwork.RequiredTradpostFrames; ++index)
        {
            tradpost.Add(Filled(34, 34, index >= GiftWindowArtwork.FirstCellFrame
                ? (byte)(TradpostColorBase + index - GiftWindowArtwork.FirstCellFrame)
                : (byte)0));
        }

        List<Sprite> cellwin = [];

        for (int index = 0; index < GiftWindowArtwork.RequiredCellwinFrames; ++index)
        {
            if (index == GiftWindowArtwork.CheckmarkFrame)
            {
                cellwin.Add(Filled(4, 4, CheckmarkColor));
            }
            else if (index >= GiftWindowArtwork.FirstBoxFrame)
            {
                cellwin.Add(Filled(15, 15, (byte)(CellwinColorBase + index - GiftWindowArtwork.FirstBoxFrame)));
            }
            else
            {
                cellwin.Add(Filled(15, 15, 7));
            }
        }

        return GiftWindowArtwork.TryCreate(
            tradpost,
            cellwin,
            [Filled(46, 22, OkColor), Filled(46, 22, 0)],
            [Filled(46, 22, CancelColor), Filled(46, 22, 0)])!;
    }

    private static StandardWindowChrome CreateChrome()
    {
        // Only the geometry matters: 20 pixel horizontal, 10 pixel vertical borders and an 8 pixel background.
        return StandardWindowChrome.TryCreate([Filled(20, 10, 1)], [Filled(10, 10, 2)], [Filled(8, 8, 3)])!;
    }

    private static Sprite Filled(int width, int height, byte color)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image, 0, 0);
    }

    /// <summary>A glyph source whose glyphs are solid blocks, with the metrics of the small font or of FONT.ICN.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte color)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
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
