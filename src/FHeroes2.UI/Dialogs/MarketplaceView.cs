using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the marketplace window asks its caller to do, the branches of <c>Dialog::Marketplace()</c> that leave the
/// loop (<c>dialog_marketplace.cpp:385-685</c>).
/// </summary>
public enum MarketplaceAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>The exit button was pressed.</summary>
    Exit,

    /// <summary>A trade went through; the treasury already changed.</summary>
    Trade,

    /// <summary>The gift button was pressed; the gift window is the caller's business.</summary>
    Gift,

    /// <summary>A "your resources" cell was right clicked; the caller shows the income window.</summary>
    ResourceIncome,

    /// <summary>The market place icon was clicked; the caller shows the exchange rate sentence.</summary>
    ExchangeRate,
}

/// <summary>The three text faces of the marketplace window.</summary>
/// <param name="SmallWhite">Resource counts, costs, "Max", "Min", "Qty to trade", "x N" and the two amount lines.</param>
/// <param name="NormalWhite">The shopkeeper's two sentences.</param>
/// <param name="NormalYellow">The window title.</param>
public sealed record MarketplaceFonts(IGlyphSource SmallWhite, IGlyphSource NormalWhite, IGlyphSource NormalYellow);

/// <summary>
/// The marketplace window, <c>Dialog::Marketplace()</c> and <c>TradeWindowGUI</c>
/// (<c>dialog_marketplace.cpp:118-685</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is one modal loop that keeps the display up to date by blitting over the parts that changed. The port
/// composes the whole window every frame instead: <see cref="Draw"/> reads the state of <see cref="MarketplaceDialog"/>
/// and <see cref="ProcessEvents"/> forwards the clicks to it, so the only state here is the widgets that need one - the
/// six buttons, the scroll bar and the two repeat timers.
/// </para>
/// <para>
/// <b>Deviations from the reference</b>, all of them in the drawing only:
/// </para>
/// <list type="bullet">
/// <item>
/// "Max" and "Min" are <c>smallYellow</c> in the reference; the port has no small yellow face yet, so it draws them
/// in <see cref="MarketplaceFonts.SmallWhite"/>. The hit boxes are the ones the reference computes, and the text is
/// still two pixels below them.
/// </item>
/// <item>
/// The exit button is a <c>UNIFORM_*_EXIT_BUTTON</c> the reference builds from the button font at start-up; the
/// caller passes its two sprites here.
/// </item>
/// <item>
/// The reference restores the saved trade-area band before painting it, which is how it erases the previous contents.
/// The port starts from a clean frame every draw and needs no restorer.
/// </item>
/// </list>
/// </remarks>
public sealed class MarketplaceView
{
    /// <summary>Title of the window, <c>"Marketplace"</c>.</summary>
    public const string HeaderText = "Marketplace";

    /// <summary>Label over the left row, <c>"Your Resources"</c>.</summary>
    public const string FromTitleText = "Your Resources";

    /// <summary>Label over the right row, <c>"Available Trades"</c>.</summary>
    public const string ToTitleText = "Available Trades";

    /// <summary>The "Max" label of the trade area.</summary>
    public const string MaxText = "Max";

    /// <summary>The "Min" label of the trade area.</summary>
    public const string MinText = "Min";

    /// <summary>The "Qty to trade" label of the trade area.</summary>
    public const string QuantityText = "Qty to trade";

    /// <summary>Row of a cell's number inside the cell, <c>py + 23</c> (<c>dialog_marketplace.cpp:330</c>).</summary>
    public const int CellValueOffsetY = 23;

    /// <summary>The number the exchange rate line shows once the count passes nine, <c>"x 9+"</c>.</summary>
    public const string PeakedCurrencyText = "x 9+";

    /// <summary>Offset the reference applies to every label it draws, <c>text.draw( x, y + 2 )</c>.</summary>
    public const int TextOffsetY = 2;

    private readonly MarketplaceDialog _dialog;
    private readonly MarketplaceLayout _layout;
    private readonly MarketplaceArtwork _artwork;
    private readonly MarketplaceFonts _fonts;
    private readonly FrameBox _frame;
    private readonly ButtonSprite _tradeButton;
    private readonly ButtonSprite _giftButton;
    private readonly ButtonSprite _exitButton;
    private readonly ButtonSprite _leftButton;
    private readonly ButtonSprite _rightButton;
    private readonly ScrollBar _slider = new();
    private readonly TimedEventValidator _leftRepeat = new();
    private readonly TimedEventValidator _rightRepeat = new();

    private int _sliderMaximum = -1;

    private MarketplaceView(
        MarketplaceDialog dialog,
        MarketplaceLayout layout,
        MarketplaceArtwork artwork,
        MarketplaceFonts fonts,
        FrameBox frame)
    {
        _dialog = dialog;
        _layout = layout;
        _artwork = artwork;
        _fonts = fonts;
        _frame = frame;

        _tradeButton = new ButtonSprite(artwork.TradeButton.Released, artwork.TradeButton.Pressed);
        _giftButton = new ButtonSprite(artwork.GiftButton.Released, artwork.GiftButton.Pressed);
        _exitButton = new ButtonSprite(artwork.ExitButton.Released, artwork.ExitButton.Pressed);
        _leftButton = new ButtonSprite(artwork.Tradpost[MarketplaceArtwork.LeftArrowReleasedFrame],
            artwork.Tradpost[MarketplaceArtwork.LeftArrowPressedFrame]);
        _rightButton = new ButtonSprite(artwork.Tradpost[MarketplaceArtwork.RightArrowReleasedFrame],
            artwork.Tradpost[MarketplaceArtwork.RightArrowPressedFrame]);

        Point trade = layout.TradeButton(SizeOf(artwork.TradeButton.Released));
        _tradeButton.SetPosition(trade.X, trade.Y);

        Point gift = layout.GiftButton(SizeOf(artwork.GiftButton.Released));
        _giftButton.SetPosition(gift.X, gift.Y);

        Point exit = layout.ExitButton(SizeOf(artwork.ExitButton.Released));
        _exitButton.SetPosition(exit.X, exit.Y);

        Point left = layout.LeftArrow;
        _leftButton.SetPosition(left.X, left.Y);

        Point right = layout.RightArrow;
        _rightButton.SetPosition(right.X, right.Y);

        MaxLabel = LabelRect(layout.MaxLabel(GlyphText.Measure(fonts.SmallWhite, MaxText)), MaxText, fonts);
        MinLabel = LabelRect(layout.MinLabel(GlyphText.Measure(fonts.SmallWhite, MinText)), MinText, fonts);
        QuantityLabel = LabelRect(layout.QuantityLabel(GlyphText.Measure(fonts.SmallWhite, QuantityText)), QuantityText, fonts);

        // The groove of the scroll bar depends on the artwork alone, so it is known from the start: a caller may read
        // Slider.Area before the trade area is even shown, which is what a hit test of the arrows needs.
        _slider.Area = layout.SliderArea(SizeOf(artwork.Bar));

        Refresh();
    }

    /// <summary>The state this window shows.</summary>
    public MarketplaceDialog Dialog => _dialog;

    /// <summary>Where the parts of the window go.</summary>
    public MarketplaceLayout Layout => _layout;

    /// <summary>The frame the window is drawn in.</summary>
    public FrameBox Frame => _frame;

    /// <summary>The trade button, <c>BUTTON_SMALL_TRADE_GOOD</c>.</summary>
    public ButtonSprite TradeButton => _tradeButton;

    /// <summary>The gift button, which the reference disables with fewer than two players.</summary>
    public ButtonSprite GiftButton => _giftButton;

    /// <summary>The exit button.</summary>
    public ButtonSprite ExitButton => _exitButton;

    /// <summary>The "step down" arrow left of the trade bar.</summary>
    public ButtonSprite LeftButton => _leftButton;

    /// <summary>The "step up" arrow right of the trade bar.</summary>
    public ButtonSprite RightButton => _rightButton;

    /// <summary>The scroll bar of the trade area.</summary>
    public ScrollBar Slider => _slider;

    /// <summary>The hit box of "Max"; only honoured while the trade area is up.</summary>
    public Rect MaxLabel { get; }

    /// <summary>The hit box of "Min"; only honoured while the trade area is up.</summary>
    public Rect MinLabel { get; }

    /// <summary>The hit box of "Qty to trade", which is only drawn.</summary>
    public Rect QuantityLabel { get; }

    /// <summary>Builds the window.</summary>
    /// <param name="dialog">The state to show and to forward clicks to.</param>
    /// <param name="chrome">Pieces of <c>BUYBUILD.ICN</c>.</param>
    /// <param name="artwork">The sprites.</param>
    /// <param name="fonts">The three text faces.</param>
    /// <param name="screen">Screen the frame is centered on.</param>
    /// <returns>The window, or <see langword="null"/> when the artwork is incomplete.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static MarketplaceView? TryCreate(
        MarketplaceDialog dialog,
        FrameBoxChrome chrome,
        MarketplaceArtwork? artwork,
        MarketplaceFonts fonts,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(fonts);

        if (artwork is null)
        {
            return null;
        }

        MarketplaceLayout layout = MarketplaceLayout.Create(chrome, screen);
        FrameBox frame = FrameBox.Create(chrome, 297, showButtons: true, startY: -1, screen);

        return new MarketplaceView(dialog, layout, artwork, fonts, frame);
    }

    /// <summary>
    /// Paints the whole window, the sum of the reference's constructor draw plus its every redraw
    /// (<c>dialog_marketplace.cpp:153-171</c>, <c>:400-450</c>, <c>:194-293</c>).
    /// </summary>
    /// <param name="target">The buffer to draw on, usually the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Refresh();

        Rect area = _layout.Area;

        _frame.Draw(target);

        DrawCentred(_fonts.NormalYellow, target, new Point(area.X, area.Y + TextOffsetY), area.Width, HeaderText);

        bool showCosts = _dialog.ResourceFrom != Resource.Unknown;

        for (int index = 0; index < MarketplaceLayout.ResourceCount; ++index)
        {
            Rect cell = _layout.FromCell(index);

            ImageOps.BlitSprite(target, cell.Position, _artwork.Cell(index));
            DrawCentred(
                _fonts.SmallWhite,
                target,
                new Point(cell.X, cell.Y + CellValueOffsetY),
                MarketplaceLayout.CellSize,
                Describe(_dialog.Kingdom.Treasury.Get(ResourceIconIndex.ToResource(index))));
        }

        for (int index = 0; index < MarketplaceLayout.ResourceCount; ++index)
        {
            Rect cell = _layout.ToCell(index);
            int resource = ResourceIconIndex.ToResource(index);

            ImageOps.BlitSprite(target, cell.Position, _artwork.Cell(index));
            DrawCentred(
                _fonts.SmallWhite,
                target,
                new Point(cell.X, cell.Y + CellValueOffsetY),
                MarketplaceLayout.CellSize,
                showCosts
                    ? _dialog.CostText(_dialog.ResourceFrom, resource)
                    : Describe(_dialog.Kingdom.Treasury.Get(resource)));
        }

        DrawCentred(_fonts.SmallWhite, target, _layout.FromTitle, MarketplaceLayout.TitleWidth, FromTitleText);
        DrawCentred(_fonts.SmallWhite, target, _layout.ToTitle, MarketplaceLayout.TitleWidth, ToTitleText);

        DrawExchangeRate(target);

        if (_dialog.ResourceFrom != Resource.Unknown)
        {
            Point cursor = CursorPosition(_layout.FromCell(ResourceIconIndex.Of(_dialog.ResourceFrom)));
            ImageOps.BlitSprite(target, cursor, _artwork.Cursor);
        }

        if (_dialog.ResourceTo != Resource.Unknown)
        {
            Point cursor = CursorPosition(_layout.ToCell(ResourceIconIndex.Of(_dialog.ResourceTo)));
            ImageOps.BlitSprite(target, cursor, _artwork.Cursor);
        }

        if (_dialog.IsTradeAreaVisible)
        {
            DrawTradeArea(target);
        }

        // The shopkeeper's sentence is the trade area's own line while the trade area is up, dialog_marketplace.cpp:251.
        GlyphText.DrawMultiline(
            _fonts.NormalWhite,
            target,
            new Point(area.X, area.Y + MarketplaceLayout.TradeMessageOffsetY),
            _dialog.Message,
            area.Width,
            center: true);

        if (_dialog.IsTradeAreaVisible)
        {
            _tradeButton.Draw(target);
            _leftButton.Draw(target);
            _rightButton.Draw(target);
        }

        _giftButton.Draw(target);
        _exitButton.Draw(target);
    }

    /// <summary>
    /// Reads the frame's input, <c>dialog_marketplace.cpp:536-683</c>, and forwards it to
    /// <see cref="Dialog"/>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The action that leaves the window, if any.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public MarketplaceAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Refresh();

        _tradeButton.UpdatePressState(events);
        _giftButton.UpdatePressState(events);
        _exitButton.UpdatePressState(events);
        _leftButton.UpdatePressState(events);
        _rightButton.UpdatePressState(events);

        if (events.MouseClickLeft(_exitButton.Area))
        {
            return MarketplaceAction.Exit;
        }

        if (_giftButton.IsEnabled && events.MouseClickLeft(_giftButton.Area))
        {
            return MarketplaceAction.Gift;
        }

        if (events.MouseClickLeft(ExchangeRateArea()))
        {
            return MarketplaceAction.ExchangeRate;
        }

        bool visible = _dialog.IsTradeAreaVisible;

        if (visible && _tradeButton.IsEnabled && events.MouseClickLeft(_tradeButton.Area) && _dialog.CanTrade)
        {
            return _dialog.Trade() ? MarketplaceAction.Trade : MarketplaceAction.None;
        }

        if (visible && _slider.MaxIndex > 0 && events.IsMouseLeftButtonPressedInArea(_slider.Area))
        {
            _slider.MoveToPosition(events.CursorPosition);
            _dialog.SetSliderIndex(_slider.CurrentIndex);
        }

        if (visible && _slider.MaxIndex > 0)
        {
            if (events.IsMouseWheelDownInArea(_slider.Area))
            {
                _dialog.DecreaseTrade();
            }
            else if (events.IsMouseWheelUpInArea(_slider.Area))
            {
                _dialog.IncreaseTrade();
            }
        }

        if (visible && !MaxLabel.IsEmpty && events.MouseClickLeft(MaxLabel))
        {
            _dialog.ChooseMaxTrade();
        }

        if (visible && !MinLabel.IsEmpty && events.MouseClickLeft(MinLabel))
        {
            _dialog.ChooseMinTrade();
        }

        bool stepDown = visible && (events.MouseClickLeft(_leftButton.Area) || _leftRepeat.IsDelayPassed(_leftButton.IsPressed));
        bool stepUp = visible && (events.MouseClickLeft(_rightButton.Area) || _rightRepeat.IsDelayPassed(_rightButton.IsPressed));

        if (stepDown)
        {
            _dialog.DecreaseTrade();
        }

        if (stepUp)
        {
            _dialog.IncreaseTrade();
        }

        for (int index = 0; index < MarketplaceLayout.ResourceCount; ++index)
        {
            Rect cell = _layout.FromCell(index);

            if (events.MouseClickLeft(cell))
            {
                _dialog.SelectFrom(ResourceIconIndex.ToResource(index));

                return MarketplaceAction.None;
            }

            if (events.IsMouseRightButtonPressedInArea(cell))
            {
                return MarketplaceAction.ResourceIncome;
            }
        }

        for (int index = 0; index < MarketplaceLayout.ResourceCount; ++index)
        {
            if (events.MouseClickLeft(_layout.ToCell(index)))
            {
                _dialog.SelectTo(ResourceIconIndex.ToResource(index));

                return MarketplaceAction.None;
            }
        }

        return MarketplaceAction.None;
    }

    /// <summary>The exchange rate icon and its "x N" line, <c>renderExchangeRate()</c> (<c>dialog_marketplace.cpp:102-116</c>).</summary>
    public Rect ExchangeRateArea()
    {
        Sprite icon = _artwork.ExchangeRateIcon;
        Point centre = _layout.ExchangeRateCentre;

        int width = icon.Width;
        int height = icon.Height + MarketplaceLayout.ExchangeRateSpacing
                     + _fonts.SmallWhite.LineHeight;

        return new Rect(centre.X - (width / 2), centre.Y + MarketplaceLayout.ExchangeRateSpacing, width, height);
    }

    /// <summary>The exchange rate line, <c>"x N"</c> or <c>"x 9+"</c>.</summary>
    /// <returns>The line the window prints.</returns>
    public string ExchangeRateLine() => _dialog.Marketplaces > 9
        ? PeakedCurrencyText
        : $"x {Describe(_dialog.Marketplaces)}";

    /// <summary>Where the "-N (M)" line goes under the resource given away.</summary>
    /// <returns>The top left corner.</returns>
    public Point SellTextPosition() => _layout.SellText(GlyphText.Measure(_fonts.SmallWhite, _dialog.SellText));

    /// <summary>Where the "+N (M)" line goes under the resource asked for.</summary>
    /// <returns>The top left corner.</returns>
    public Point BuyTextPosition() => _layout.BuyText(GlyphText.Measure(_fonts.SmallWhite, _dialog.BuyText));

    private static Size SizeOf(Sprite sprite) => new(sprite.Width, sprite.Height);

    private static Point CursorPosition(in Rect cell) => new(cell.X - 2, cell.Y - 2);

    private static string Describe(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Describe(uint value) => value.ToString(CultureInfo.InvariantCulture);

    private static void DrawCentred(IGlyphSource font, PixelBuffer target, Point origin, int width, string text) =>
        GlyphText.DrawLine(font, target, new Point(origin.X + ((width - GlyphText.Measure(font, text)) / 2), origin.Y), text);

    private static Rect LabelRect(Point origin, string text, MarketplaceFonts fonts) =>
        new(origin, new Size(GlyphText.Measure(fonts.SmallWhite, text), fonts.SmallWhite.LineHeight));

    private void DrawExchangeRate(PixelBuffer target)
    {
        Sprite icon = _artwork.ExchangeRateIcon;
        Point centre = _layout.ExchangeRateCentre;

        int iconX = centre.X - (icon.Width / 2);
        int iconY = centre.Y + MarketplaceLayout.ExchangeRateSpacing;

        ImageOps.BlitSprite(target, new Point(iconX, iconY), icon);

        GlyphText.DrawLine(
            _fonts.SmallWhite,
            target,
            new Point(centre.X - (GlyphText.Measure(_fonts.SmallWhite, ExchangeRateLine()) / 2),
                iconY + icon.Height + MarketplaceLayout.ExchangeRateSpacing),
            ExchangeRateLine());
    }

    private void DrawTradeArea(PixelBuffer target)
    {
        Rect area = _layout.Area;
        Size barSize = SizeOf(_artwork.Bar);

        ImageOps.BlitSprite(target, _layout.TradeBar(barSize), _artwork.Bar);

        if (_slider.Slider is { } thumb)
        {
            ImageOps.BlitSprite(target, _slider.Position, thumb);
        }

        if (_artwork.ResourceIcon(_dialog.ResourceFrom) is { } fromIcon)
        {
            ImageOps.BlitSprite(target, _layout.TradeIcon(SizeOf(fromIcon), isTo: false), fromIcon);
        }

        if (_artwork.ResourceIcon(_dialog.ResourceTo) is { } toIcon)
        {
            ImageOps.BlitSprite(target, _layout.TradeIcon(SizeOf(toIcon), isTo: true), toIcon);
        }

        ImageOps.BlitSprite(target, _layout.FromTo(SizeOf(_artwork.FromTo)), _artwork.FromTo);

        DrawLabel(MaxText, _layout.MaxLabel(GlyphText.Measure(_fonts.SmallWhite, MaxText)));
        DrawLabel(MinText, _layout.MinLabel(GlyphText.Measure(_fonts.SmallWhite, MinText)));

        GlyphText.DrawLine(
            _fonts.SmallWhite,
            target,
            new Point(_layout.QuantityLabel(GlyphText.Measure(_fonts.SmallWhite, QuantityText)).X,
                area.Y + MarketplaceLayout.QuantityOffsetY + TextOffsetY),
            QuantityText);

        GlyphText.DrawLine(_fonts.SmallWhite, target, SellTextPosition(), _dialog.SellText);
        GlyphText.DrawLine(_fonts.SmallWhite, target, BuyTextPosition(), _dialog.BuyText);

        void DrawLabel(string text, Point origin) =>
            GlyphText.DrawLine(_fonts.SmallWhite, target, new Point(origin.X, origin.Y + TextOffsetY), text);
    }

    /// <summary>
    /// Brings the widgets in line with <see cref="Dialog"/>: enables and hides the trade area's buttons, rebuilds the
    /// thumb when the range changed and moves it to <see cref="MarketplaceDialog.SliderIndex"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Draw"/> and <see cref="ProcessEvents"/> call this themselves, so a caller that only lives the usual
    /// frame loop never needs it. It is public because a caller that changed <see cref="Dialog"/> on its own — a test,
    /// or a scene that keeps the window up while something else trades — reads <see cref="TradeButton"/>,
    /// <see cref="Slider"/> and <see cref="MaxLabel"/> in their refreshed state only after calling it.
    /// </remarks>
    public void Refresh()
    {
        if (_dialog.IsGiftEnabled)
        {
            _giftButton.Enable();
        }
        else
        {
            _giftButton.Disable();
        }

        if (!_dialog.IsTradeAreaVisible)
        {
            _tradeButton.Hide();
            _tradeButton.Disable();
            _leftButton.Hide();
            _leftButton.Disable();
            _rightButton.Hide();
            _rightButton.Disable();

            return;
        }

        _tradeButton.Show();
        _tradeButton.Enable();
        _leftButton.Show();
        _leftButton.Enable();
        _rightButton.Show();
        _rightButton.Enable();

        int maximum = _dialog.SliderMaxIndex;

        if (maximum != _sliderMaximum)
        {
            _sliderMaximum = maximum;

            Sprite source = _artwork.ScrollSliderSource;
            Rect start = new(0, 0, 2, source.Height);
            Rect middle = new(2, 0, 8, source.Height);

            _slider.SetSlider(ScrollSlider.Create(
                source,
                horizontal: true,
                MarketplaceLayout.SliderWidth,
                visibleCount: 1,
                maximum + 1,
                start,
                middle));

            _slider.SetRange(0, maximum);
        }

        _slider.MoveToIndex(_dialog.SliderIndex);
    }
}
