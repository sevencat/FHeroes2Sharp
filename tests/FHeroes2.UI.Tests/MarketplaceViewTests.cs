using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The marketplace window, <c>Dialog::Marketplace()</c> and <c>TradeWindowGUI</c>
/// (<c>dialog_marketplace.cpp:118-685</c>).
/// </summary>
public sealed class MarketplaceViewTests
{
    private const byte Canvas = 0;
    private const byte FrameColor = 33;
    private const byte ButtonColor = 44;
    private const byte BarColor = 60;
    private const byte SliderColor = 61;
    private const byte FromToColor = 62;
    private const byte CursorColor = 63;
    private const byte ExchangeRateColor = 64;
    private const byte ResourceColor = 65;
    private const byte TradeButtonColor = 66;
    private const byte GiftButtonColor = 67;
    private const byte ExitButtonColor = 68;
    private const byte ArrowColor = 69;

    private static readonly Size BarSize = new(236, 30);
    private static readonly Size FromToSize = new(56, 24);
    private static readonly Size ResourceIconSize = new(30, 30);
    private static readonly Size TradeButtonSize = new(60, 25);
    private static readonly Size GiftButtonSize = new(60, 25);

    [Fact]
    public void TryCreate_LaysOutTheFrame()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));

        // Dialog::FrameBox( 297, true ): the 297 pixel text area plus the 40 pixel button row.
        Assert.Equal(FrameBox.AreaWidth, view.Layout.Area.Width);
        Assert.Equal(297 + FrameBox.ButtonAreaHeight, view.Layout.Area.Height);
        Assert.Equal(view.Layout.Area, view.Frame.Area);
    }

    [Fact]
    public void TryCreate_ReturnsNullWithoutArtwork()
    {
        Assert.Null(MarketplaceView.TryCreate(
            CreateTradingPost(), CreateChrome(), null, CreateFonts()));
        Assert.Null(MarketplaceView.TryCreate(
            CreateTradingPost(),
            CreateChrome(),
            MarketplaceArtwork.TryCreate(CreateTradpost(14), CreateIcons(), CreateResources(), CreateButtonFrames(),
                CreateButtonFrames(), CreateButtonFrames()),
            CreateFonts()));
    }

    [Fact]
    public void TryCreate_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(() => MarketplaceView.TryCreate(
            null!, CreateChrome(), CreateArtwork(), CreateFonts()));
        Assert.Throws<ArgumentNullException>(() => MarketplaceView.TryCreate(
            CreateTradingPost(), null!, CreateArtwork(), CreateFonts()));
        Assert.Throws<ArgumentNullException>(() => MarketplaceView.TryCreate(
            CreateTradingPost(), CreateChrome(), CreateArtwork(), null!));
    }

    [Fact]
    public void Cells_AreTwoRowsOfFourAndThree()
    {
        MarketplaceLayout layout = MarketplaceLayout.Create(CreateChrome());
        Rect area = layout.Area;

        Assert.Equal(new Rect(area.X, area.Y + 190, 34, 34), layout.FromCell(0));
        Assert.Equal(new Rect(area.X + (3 * 37), area.Y + 190, 34, 34), layout.FromCell(3));
        Assert.Equal(new Rect(area.X, area.Y + 190 + 37, 34, 34), layout.FromCell(4));
        Assert.Equal(new Rect(area.X + (2 * 37), area.Y + 190 + 37, 34, 34), layout.FromCell(6));

        Assert.Equal(area.X + MarketplaceLayout.ToCellsOffsetX, layout.ToCell(0).X);
        Assert.Equal(layout.FromCell(6).Y, layout.ToCell(6).Y);
    }

    [Fact]
    public void TitlesAndMessage_SitAboveTheRows()
    {
        MarketplaceLayout layout = MarketplaceLayout.Create(CreateChrome());
        Rect area = layout.Area;

        Assert.Equal(new Point(area.X, area.Y + 175), layout.FromTitle);
        Assert.Equal(new Point(area.X + 136, area.Y + 175), layout.ToTitle);
        Assert.Equal(new Rect(area.X, area.Y + 30, 244, 100), layout.Message);
        Assert.Equal(new Point(area.X, area.Y + 32), layout.TradeMessage);

        // The exchange rate icon hangs over the gold cell, which is the first cell of the second row.
        Assert.Equal(new Point(area.X + 122, area.Y + 227), layout.ExchangeRateCentre);
    }

    [Fact]
    public void TradeArea_AnchorsToTheCentredArtwork()
    {
        MarketplaceLayout layout = MarketplaceLayout.Create(CreateChrome());

        Assert.Equal(new Point(layout.Area.X + 2, layout.Area.Y + 128), layout.TradeBar(BarSize));
        Assert.Equal(new Rect(layout.Area.X + 26, layout.Area.Y + 131, 187, 11), layout.SliderArea(BarSize));
        Assert.Equal(new Point(layout.Area.X + 94, layout.Area.Y + 90), layout.FromTo(FromToSize));

        // The two icons are bottom aligned to y + 115 and pushed 70 pixels apart.
        Assert.Equal(new Point(layout.Area.X + 37, layout.Area.Y + 85), layout.TradeIcon(ResourceIconSize, false));
        Assert.Equal(new Point(layout.Area.X + 177, layout.Area.Y + 85), layout.TradeIcon(ResourceIconSize, true));
    }

    [Fact]
    public void Buttons_HangWhereTheReferencePutsThem()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        Rect area = view.Layout.Area;

        Assert.Equal(new Rect(area.X + 92, area.Y + 150, 60, 25), view.TradeButton.Area);
        Assert.Equal(new Rect(area.X - 68 + 92, area.Y + 337 - 25, 60, 25), view.GiftButton.Area);
        Assert.Equal(new Rect(area.X + 68 + 92, area.Y + 337 - 25, 60, 25), view.ExitButton.Area);
        Assert.Equal(new Point(area.X + 11, area.Y + 129), view.LeftButton.ReleasedArea.Position);
        Assert.Equal(new Point(area.X + 220, area.Y + 129), view.RightButton.ReleasedArea.Position);
    }

    [Fact]
    public void TradeArea_IsHiddenUntilATradeIsPossible()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));

        Assert.False(view.TradeButton.IsVisible);
        Assert.False(view.LeftButton.IsVisible);
        Assert.False(view.RightButton.IsVisible);

        view.Dialog.SelectFrom(Resource.Wood);
        view.Refresh();

        Assert.False(view.TradeButton.IsVisible);

        view.Dialog.SelectTo(Resource.Gold);
        view.Refresh();

        Assert.True(view.TradeButton.IsVisible);
        Assert.True(view.TradeButton.IsEnabled);
        Assert.Equal(MarketplaceLayout.SliderWidth, view.Slider.Area.Width);
        Assert.Equal(0, view.Slider.CurrentIndex);
        Assert.Equal(10, view.Slider.MaxIndex);
    }

    [Fact]
    public void Slider_FollowsTheModel()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);

        view.Dialog.SetSliderIndex(4);
        view.Draw(new PixelBuffer(640, 480));

        Assert.Equal(4, view.Slider.CurrentIndex);
    }

    [Fact]
    public void Draw_PaintsTheFrameAndTheCells()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        Assert.Equal(FrameColor, Pixel(target, view.Layout.Area.X, view.Layout.Area.Y + 60));

        for (int index = 0; index < MarketplaceLayout.ResourceCount; ++index)
        {
            Rect cell = view.Layout.FromCell(index);
            Assert.Equal(CellColor(index), Pixel(target, cell.X + 17, cell.Y + 17));
        }
    }

    [Fact]
    public void Draw_MarksTheSelectedCellsWithTheCursor()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        view.Dialog.SelectFrom(Resource.Ore);
        view.Dialog.SelectTo(Resource.Gold);

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        Rect from = view.Layout.FromCell(ResourceIconIndex.Of(Resource.Ore));
        Rect to = view.Layout.ToCell(ResourceIconIndex.Of(Resource.Gold));

        Assert.Equal(CursorColor, Pixel(target, from.X - 2, from.Y - 2));
        Assert.Equal(CursorColor, Pixel(target, to.X - 2, to.Y - 2));
    }

    [Fact]
    public void Draw_PaintsTheTradeArea()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10, ore: 10));
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Ore);

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        Assert.Equal(BarColor, Pixel(target, view.Layout.Area.X + 4, view.Layout.Area.Y + 132));
        Assert.Equal(SliderColor, Pixel(target, view.Slider.Position.X + 1, view.Slider.Position.Y + 1));
        Assert.Equal(FromToColor, Pixel(target, view.Layout.Area.X + 100, view.Layout.Area.Y + 100));
        Assert.Equal(ResourceColor, Pixel(target, view.Layout.Area.X + 40, view.Layout.Area.Y + 100));
        Assert.Equal(TradeButtonColor, Pixel(target, view.TradeButton.Area.X + 30, view.TradeButton.Area.Y + 12));
        Assert.Equal(ExitButtonColor, Pixel(target, view.ExitButton.Area.X + 30, view.ExitButton.Area.Y + 12));
    }

    [Fact]
    public void Draw_PaintsTheGiftButtonOnlyWithTwoPlayers()
    {
        MarketplaceView alone = CreateView(CreateTradingPost(wood: 10));
        PixelBuffer aloneTarget = new(640, 480);
        alone.Draw(aloneTarget);

        // A disabled button is the greyed out pressed artwork, not the released one, so it is not the plain colour.
        Assert.NotEqual(GiftButtonColor, Pixel(aloneTarget,
            alone.GiftButton.Area.X + 30, alone.GiftButton.Area.Y + 12));

        MarketplaceView together = CreateView(CreateTradingPost(wood: 10, players: 2));
        PixelBuffer togetherTarget = new(640, 480);
        together.Draw(togetherTarget);

        Assert.Equal(GiftButtonColor, Pixel(togetherTarget,
            together.GiftButton.Area.X + 30, together.GiftButton.Area.Y + 12));
    }

    [Fact]
    public void Draw_LeavesTheTradeButtonsOutWhileHidden()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        Assert.Equal(FrameColor, Pixel(target, view.TradeButton.ReleasedArea.X + 30, view.TradeButton.ReleasedArea.Y + 12));
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        Assert.Throws<ArgumentNullException>(() => CreateView(CreateTradingPost()).Draw(null!));
    }

    [Fact]
    public void ProcessEvents_RejectsMissingEvents()
    {
        Assert.Throws<ArgumentNullException>(() => CreateView(CreateTradingPost()).ProcessEvents(null!));
    }

    [Fact]
    public void ClickOnAFromCell_SelectsTheResource()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        LocalEvent events = CreateEvents();

        Click(events, Center(view.Layout.FromCell(ResourceIconIndex.Of(Resource.Wood))));

        Assert.Equal(MarketplaceAction.None, view.ProcessEvents(events));
        Assert.Equal(Resource.Wood, view.Dialog.ResourceFrom);
    }

    [Fact]
    public void ClickOnAToCell_SelectsTheTarget()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10, ore: 4));
        view.Dialog.SelectFrom(Resource.Wood);

        LocalEvent events = CreateEvents();
        Click(events, Center(view.Layout.ToCell(ResourceIconIndex.Of(Resource.Ore))));

        Assert.Equal(MarketplaceAction.None, view.ProcessEvents(events));
        Assert.Equal(Resource.Ore, view.Dialog.ResourceTo);
        Assert.True(view.Dialog.IsTradeAreaVisible);
    }

    [Fact]
    public void RightClickOnAFromCell_AsksForTheIncomeWindow()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        LocalEvent events = CreateEvents();

        // The reference asks for the window while the right button is held, dialog_marketplace.cpp:560, so the press
        // alone is what a frame sees; the release does not have to follow inside the same batch.
        events.OnMouseButton(true, MouseButtonType.Right, Center(view.Layout.FromCell(0)));

        Assert.Equal(MarketplaceAction.ResourceIncome, view.ProcessEvents(events));
    }

    [Fact]
    public void ClickOnTheExitButton_ClosesTheWindow()
    {
        MarketplaceView view = CreateView(CreateTradingPost());
        LocalEvent events = CreateEvents();

        Click(events, Center(view.ExitButton.ReleasedArea));

        Assert.Equal(MarketplaceAction.Exit, view.ProcessEvents(events));
    }

    [Fact]
    public void ClickOnTheGiftButton_OnlyWorksWithTwoPlayers()
    {
        MarketplaceView alone = CreateView(CreateTradingPost());
        LocalEvent events = CreateEvents();
        Click(events, Center(alone.GiftButton.ReleasedArea));

        Assert.Equal(MarketplaceAction.None, alone.ProcessEvents(events));

        MarketplaceView together = CreateView(CreateTradingPost(players: 2));
        LocalEvent other = CreateEvents();
        Click(other, Center(together.GiftButton.ReleasedArea));

        Assert.Equal(MarketplaceAction.Gift, together.ProcessEvents(other));
    }

    [Fact]
    public void ClickOnTheExchangeRate_AsksForTheSentence()
    {
        MarketplaceView view = CreateView(CreateTradingPost());
        LocalEvent events = CreateEvents();

        Click(events, Center(view.ExchangeRateArea()));

        Assert.Equal(MarketplaceAction.ExchangeRate, view.ProcessEvents(events));
    }

    [Fact]
    public void ClickOnTheTradeButton_MovesTheResources()
    {
        GameKingdom kingdom = CreateKingdom(wood: 10, gold: 100);
        MarketplaceView view = CreateView(kingdom);
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);
        view.Dialog.SetSliderIndex(3);

        LocalEvent events = CreateEvents();
        Click(events, Center(view.TradeButton.ReleasedArea));

        Assert.Equal(MarketplaceAction.Trade, view.ProcessEvents(events));
        Assert.Equal(7, kingdom.Treasury.Wood);
        Assert.Equal(250, kingdom.Treasury.Gold);

        // The trade cleared the selection, so the trade area goes away on the next frame.
        view.Refresh();
        Assert.False(view.TradeButton.IsVisible);
    }

    [Fact]
    public void ClickOnTheTradeButton_DoesNothingWithoutAQuantity()
    {
        GameKingdom kingdom = CreateKingdom(wood: 10);
        MarketplaceView view = CreateView(kingdom);
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);

        LocalEvent events = CreateEvents();
        Click(events, Center(view.TradeButton.ReleasedArea));

        Assert.Equal(MarketplaceAction.None, view.ProcessEvents(events));
        Assert.Equal(10, kingdom.Treasury.Wood);
    }

    [Fact]
    public void DraggingTheSlider_MovesTheModel()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);

        LocalEvent events = CreateEvents();
        Rect slider = view.Slider.Area;
        Point far = new(slider.X + slider.Width - 1, slider.Y + (slider.Height / 2));

        events.OnMouseButton(true, MouseButtonType.Left, far);
        events.OnMouseMotion(far);
        view.ProcessEvents(events);

        Assert.Equal(10, view.Dialog.SliderIndex);
        Assert.Equal(10, view.Slider.CurrentIndex);
    }

    [Fact]
    public void TheWheel_StepsTheTrade()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);

        Point middle = Center(view.Slider.Area);

        LocalEvent up = CreateEvents();
        up.OnMouseMotion(middle);
        up.OnMouseWheel(new Point(0, 1));
        view.ProcessEvents(up);

        Assert.Equal(1, view.Dialog.SliderIndex);

        LocalEvent down = CreateEvents();
        down.OnMouseMotion(middle);
        down.OnMouseWheel(new Point(0, -1));
        view.ProcessEvents(down);

        Assert.Equal(0, view.Dialog.SliderIndex);
    }

    [Fact]
    public void MaxAndMinLabels_JumpTheTrade()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);
        view.Draw(new PixelBuffer(640, 480));

        LocalEvent max = CreateEvents();
        Click(max, Center(view.MaxLabel));
        view.ProcessEvents(max);

        Assert.Equal(10, view.Dialog.SliderIndex);

        LocalEvent min = CreateEvents();
        Click(min, Center(view.MinLabel));
        view.ProcessEvents(min);

        // The reference's "Min" is one unit, not zero.
        Assert.Equal(1, view.Dialog.SliderIndex);
    }

    [Fact]
    public void Arrows_StepTheTrade()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);
        view.Dialog.SetSliderIndex(4);

        LocalEvent up = CreateEvents();
        Click(up, Center(view.RightButton.ReleasedArea));
        view.ProcessEvents(up);

        Assert.Equal(5, view.Dialog.SliderIndex);

        LocalEvent down = CreateEvents();
        Click(down, Center(view.LeftButton.ReleasedArea));
        view.ProcessEvents(down);

        Assert.Equal(4, view.Dialog.SliderIndex);
    }

    [Fact]
    public void ExchangeRate_ShowsNineAsThePeak()
    {
        MarketplaceView view = CreateView(CreateTradingPost());

        Assert.Equal("x 3", view.ExchangeRateLine());
    }

    [Fact]
    public void AmountLines_FollowTheModel()
    {
        MarketplaceView view = CreateView(CreateTradingPost(wood: 10));
        view.Dialog.SelectFrom(Resource.Wood);
        view.Dialog.SelectTo(Resource.Gold);
        view.Dialog.SetSliderIndex(4);

        Point sell = view.SellTextPosition();
        Point buy = view.BuyTextPosition();

        // Both lines are centered under their icon, 70 pixels either side of the frame's middle.
        Assert.True(sell.X < view.Layout.Area.X + 122);
        Assert.True(buy.X > view.Layout.Area.X + 122);
        Assert.Equal(view.Layout.Area.Y + MarketplaceLayout.AmountsOffsetY, sell.Y);
        Assert.Equal(view.Layout.Area.Y + MarketplaceLayout.AmountsOffsetY, buy.Y);
    }

    private static byte CellColor(int index) => (byte)(40 + index);

    private static byte Pixel(PixelBuffer buffer, int x, int y) => buffer[x, y];

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static LocalEvent CreateEvents() => new(() => 0);

    private static void Click(LocalEvent events, Point point) => Click(events, MouseButtonType.Left, point);

    private static void Click(LocalEvent events, MouseButtonType button, Point point)
    {
        events.OnMouseButton(true, button, point);
        events.OnMouseButton(false, button, point);
    }

    private static MarketplaceView CreateView(GameKingdom kingdom, bool tradingPost = true, int players = 1) =>
        CreateView(MarketplaceDialog.Create(kingdom, tradingPost, players));

    private static MarketplaceView CreateView(MarketplaceDialog dialog) =>
        MarketplaceView.TryCreate(dialog, CreateChrome(), CreateArtwork(), CreateFonts())!;

    private static MarketplaceDialog CreateTradingPost(int wood = 0, int gold = 0, int ore = 0, int players = 1) =>
        MarketplaceDialog.Create(CreateKingdom(wood, gold, ore), true, players);

    private static GameKingdom CreateKingdom(int wood = 0, int gold = 0, int ore = 0)
    {
        var kingdom = new GameKingdom();
        kingdom.Treasury.Wood = wood;
        kingdom.Treasury.Gold = gold;
        kingdom.Treasury.Ore = ore;

        return kingdom;
    }

    private static MarketplaceFonts CreateFonts()
    {
        IGlyphSource font = new BlockGlyphSource();

        return new MarketplaceFonts(font, font, font);
    }

    private static MarketplaceArtwork CreateArtwork() =>
        MarketplaceArtwork.TryCreate(
            CreateTradpost(MarketplaceArtwork.RequiredTradpostFrames),
            CreateIcons(),
            CreateResources(),
            CreateButtonFrames(TradeButtonColor),
            CreateButtonFrames(GiftButtonColor),
            CreateButtonFrames(ExitButtonColor))!;

    private static IReadOnlyList<Sprite> CreateTradpost(int count)
    {
        Sprite[] frames = new Sprite[count];

        for (int index = 0; index < count; ++index)
        {
            frames[index] = index switch
            {
                MarketplaceArtwork.FromToFrame => new Sprite(Filled(FromToSize, FromToColor)),
                MarketplaceArtwork.BarFrame => new Sprite(Filled(BarSize, BarColor)),
                MarketplaceArtwork.ScrollSliderFrame => new Sprite(Filled(new Size(10, 11), SliderColor)),
                MarketplaceArtwork.CursorFrame => new Sprite(Filled(new Size(34, 34), CursorColor)),
                MarketplaceArtwork.LeftArrowReleasedFrame or MarketplaceArtwork.LeftArrowPressedFrame
                    or MarketplaceArtwork.RightArrowReleasedFrame or MarketplaceArtwork.RightArrowPressedFrame =>
                    new Sprite(Filled(new Size(11, 11), ArrowColor)),
                _ => new Sprite(Filled(new Size(34, 34), CellColor(index - MarketplaceArtwork.FirstCellFrame))),
            };
        }

        return frames;
    }

    private static IReadOnlyList<Sprite> CreateIcons() =>
    [
        new Sprite(Filled(new Size(24, 24), ExchangeRateColor)),
        new Sprite(Filled(new Size(24, 24), ExchangeRateColor)),
    ];

    private static IReadOnlyList<Sprite> CreateResources()
    {
        Sprite[] frames = new Sprite[ResourceIconIndex.Count];

        for (int index = 0; index < frames.Length; ++index)
        {
            frames[index] = new Sprite(Filled(ResourceIconSize, ResourceColor));
        }

        return frames;
    }

    private static IReadOnlyList<Sprite> CreateButtonFrames(byte color = TradeButtonColor) =>
    [
        new Sprite(Filled(TradeButtonSize, color)),
        new Sprite(Filled(TradeButtonSize, ButtonColor), 1, 1),
    ];

    private static FrameBoxChrome CreateChrome()
    {
        Sprite[] frames = new Sprite[FrameBoxChrome.RequiredSpriteCount];

        for (int index = 0; index < frames.Length; ++index)
        {
            frames[index] = new Sprite(Filled(new Size(160, index % 3 == 1 ? 35 : 99), FrameColor));
        }

        return FrameBoxChrome.TryCreate(frames)!;
    }

    private static PixelBuffer Filled(Size size, byte color)
    {
        PixelBuffer buffer = new(size.Width, size.Height);
        buffer.Fill(color);

        return buffer;
    }

    /// <summary>A 6x15 solid block font, which is enough for the labels and the counts.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = new(Filled(new Size(6, 15), ButtonColor));

        public int LineHeight => 15;

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
