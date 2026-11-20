using FHeroes2.Game.Resources;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using ResourceTrading = FHeroes2.Game.Kingdom.ResourceTrading;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The state of the marketplace, <c>Dialog::Marketplace()</c> (<c>dialog.h:131</c>,
/// <c>dialog_marketplace.cpp:385-685</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is one modal function: a <c>while</c> loop that reads clicks, keeps the two selected resources and
/// the slider position in locals, and trades into the kingdom on the spot. The port splits that loop the way it split
/// <see cref="SelectCountDialog"/> from <see cref="SelectCountView"/>: this class is every mutation and every string
/// the loop performs, so it can be driven from a test without a display, and a later view only has to draw
/// <see cref="ResourceFrom"/>, <see cref="Message"/> and <see cref="SliderIndex"/> and forward clicks here.
/// </para>
/// <para>
/// The single piece of state the reference keeps that the port does not is the slider position: the reference keeps
/// <c>count_buy</c>/<c>count_sell</c> and moves the scrollbar alongside them, the port keeps the index and derives the
/// two counts from it. Both are the same value - <c>count_buy = index * ( gold ? cost : 1 )</c> and
/// <c>count_sell = index * ( gold ? 1 : cost )</c> - and deriving them removes the possibility of the two drifting
/// apart.
/// </para>
/// <para>
/// <b>Not ported</b>: the gift window (<c>Dialog::MakeGiftResource()</c>, <c>dialog_giftresources.cpp</c>) and the
/// kingdom income window behind a right click on a resource (<c>showKingdomIncome()</c>). The gift button itself is
/// modelled - <see cref="IsGiftEnabled"/> is the reference's "are there two playing players" test - so a view can
/// draw it in the right state; pressing it is the view's caller's business.
/// </para>
/// </remarks>
public sealed class MarketplaceDialog
{
    /// <summary>The exchange rate a trading post gives, <c>tradingPostValue</c> of the reference.</summary>
    /// <remarks>
    /// A trading post is worth three market places no matter how many the kingdom owns
    /// (<c>dialog_marketplace.cpp:58-63</c>).
    /// </remarks>
    public const int TradingPostMarketplaces = 3;

    /// <summary>The sentence shown until the first trade of the visit is made.</summary>
    public const string FirstExchangeMessage =
        "Please inspect our fine wares. If you feel like offering a trade, click on the items you wish to trade with and for.";

    /// <summary>The sentence shown after the first trade is made.</summary>
    public const string RepeatExchangeMessage =
        "You have received quite a bargain. I expect to make no profit on the deal. Can I interest you in any of my other wares?";

    /// <summary>The most market places the rate table has a column for.</summary>
    /// <remarks><c>resource_trading.cpp:59</c> peaks at nine; beyond that the text says "peaked".</remarks>
    public const int PeakedMarketplaces = 9;

    private readonly GameKingdom _kingdom;
    private readonly bool _fromTradingPost;
    private readonly int _activePlayers;

    private int _resourceFrom;
    private int _resourceTo;
    private uint _maxSell;
    private uint _maxBuy;
    private int _sliderIndex;
    private bool _isFirstExchange = true;

    private MarketplaceDialog(GameKingdom kingdom, bool isFromTradingPost, int activePlayers)
    {
        _kingdom = kingdom;
        _fromTradingPost = isFromTradingPost;
        _activePlayers = activePlayers;
    }

    /// <summary>
    /// Builds the state of the window, <c>Dialog::Marketplace( Kingdom &amp;, const bool fromTradingPost )</c>.
    /// </summary>
    /// <param name="kingdom">The kingdom whose resources are traded; the dialog modifies its treasury.</param>
    /// <param name="isFromTradingPost">
    /// Whether the window was opened from a trading post, which replaces the kingdom's market place count with
    /// <see cref="TradingPostMarketplaces"/>.
    /// </param>
    /// <param name="activePlayers">
    /// How many players of the game are still playing, the reference's count over <c>Settings::Get().GetPlayers()</c>
    /// that decides whether the gift button is usable. One - the default - disables the gift.
    /// </param>
    /// <returns>The state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="kingdom"/> is <see langword="null"/>.</exception>
    public static MarketplaceDialog Create(GameKingdom kingdom, bool isFromTradingPost, int activePlayers = 1)
    {
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentOutOfRangeException.ThrowIfNegative(activePlayers);

        return new MarketplaceDialog(kingdom, isFromTradingPost, activePlayers);
    }

    /// <summary>The kingdom whose resources are traded.</summary>
    public GameKingdom Kingdom => _kingdom;

    /// <summary>Whether the window was opened from a trading post.</summary>
    public bool IsFromTradingPost => _fromTradingPost;

    /// <summary>
    /// The market place count the rates are taken from, <c>kingdom.GetCountMarketplace()</c> or the trading post's
    /// three (<c>getTradeCosts()</c>, <c>dialog_marketplace.cpp:60-63</c>).
    /// </summary>
    public int Marketplaces => _fromTradingPost ? TradingPostMarketplaces : _kingdom.CountMarketplaces();

    /// <summary>How many players are still playing, the input of the gift button's enabled state.</summary>
    public int ActivePlayers => _activePlayers;

    /// <summary>
    /// Whether the gift button can be pressed, the reference's "at least two players are playing" test
    /// (<c>dialog_marketplace.cpp:159-170</c>).
    /// </summary>
    public bool IsGiftEnabled => _activePlayers >= 2;

    /// <summary>The resource given away, one of the <see cref="Resource"/> flags; <see cref="Resource.Unknown"/>
    /// until the player picks one.</summary>
    public int ResourceFrom => _resourceFrom;

    /// <summary>The resource asked for, one of the <see cref="Resource"/> flags; <see cref="Resource.Unknown"/>
    /// until the player picks one.</summary>
    public int ResourceTo => _resourceTo;

    /// <summary>How much of <see cref="ResourceFrom"/> the kingdom had when it was picked, <c>max_sell</c>.</summary>
    public uint MaxSell => _maxSell;

    /// <summary>
    /// How much of <see cref="ResourceTo"/> that much buys, <c>max_buy</c>.
    /// </summary>
    /// <remarks>
    /// For gold the reference multiplies instead of dividing - <c>max_sell * rate</c>, one unit of wood is worth
    /// several gold - and the port keeps the unsigned arithmetic, so the division of the other direction drops the
    /// remainder exactly like the reference.
    /// </remarks>
    public uint MaxBuy => _maxBuy;

    /// <summary>Position of the slider between <see cref="MinSliderIndex"/> and <see cref="SliderMaxIndex"/>.</summary>
    public int SliderIndex => _sliderIndex;

    /// <summary>Lowest slider position, <c>0</c>: nothing is traded.</summary>
    public const int MinSliderIndex = 0;

    /// <summary>
    /// Highest slider position, the <c>maximumValue</c> of the reference's scrollbar range
    /// (<c>dialog_marketplace.cpp:227</c>): the amount sold for a gold target, the amount bought otherwise.
    /// </summary>
    public int SliderMaxIndex => (int)(_resourceTo == Resource.Gold ? _maxSell : _maxBuy);

    /// <summary>Whether the first trade of the visit has not been made yet, <c>firstExchange</c>.</summary>
    public bool IsFirstExchange => _isFirstExchange;

    /// <summary>
    /// How many units of <see cref="ResourceFrom"/> the current trade gives away,
    /// <c>count_sell</c> of the reference.
    /// </summary>
    public uint CountSell => _resourceTo == Resource.Gold ? (uint)_sliderIndex : (uint)_sliderIndex * TradeCost;

    /// <summary>
    /// How many units of <see cref="ResourceTo"/> the current trade brings in, <c>count_buy</c> of the reference.
    /// </summary>
    public uint CountBuy => _resourceTo == Resource.Gold ? (uint)_sliderIndex * TradeCost : (uint)_sliderIndex;

    /// <summary>
    /// The rate between the two selected resources, <see cref="ResourceTrading.GetTradeCost"/> with the market place
    /// count of <see cref="Marketplaces"/>; 0 while nothing is selected, or while both sides are the same resource.
    /// </summary>
    public uint TradeCost => ResourceTrading.GetTradeCost((uint)Marketplaces, _resourceFrom, _resourceTo);

    /// <summary>
    /// Whether the middle of the window shows the trade slider, the <c>else</c> branch of
    /// <c>ShowTradeArea()</c> (<c>dialog_marketplace.cpp:194-219</c>).
    /// </summary>
    /// <remarks>
    /// The reference hides the trade area when the kingdom has none of the resource given away, when both sides are
    /// the same resource, and when the target is not gold and the kingdom cannot afford a single unit of it.
    /// </remarks>
    public bool IsTradeAreaVisible => _kingdom.Treasury.Get(_resourceFrom) > 0 && _resourceFrom != _resourceTo &&
                                      (_resourceTo == Resource.Gold || _maxBuy > 0);

    /// <summary>
    /// Whether the trade button does something when pressed: the reference enables it together with the trade area
    /// and then requires both counts to be non-zero (<c>dialog_marketplace.cpp:641</c>).
    /// </summary>
    public bool CanTrade => IsTradeAreaVisible && CountSell > 0 && CountBuy > 0;

    /// <summary>
    /// The sentence in the middle of the window, the two branches of <c>ShowTradeArea()</c>.
    /// </summary>
    /// <remarks>
    /// The reference's "please inspect our fine wares" is not simply the first sentence of the visit: it is also
    /// required that the two sides are the same resource or that nothing can be bought. A visit whose treasury is
    /// emptied by a gift therefore shows the "bargain" sentence without a trade having happened, and the port keeps
    /// that.
    /// </remarks>
    public string Message
    {
        get
        {
            if (IsTradeAreaVisible)
            {
                return _resourceTo == Resource.Gold
                    ? $"I can offer you {TradeCost} for 1 unit of {ResourceText.Of(_resourceFrom)}."
                    : $"I can offer you 1 unit of {ResourceText.Of(_resourceTo)} for {TradeCost} units of {ResourceText.Of(_resourceFrom)}.";
            }

            bool firstSentence = _isFirstExchange && (_resourceFrom == _resourceTo || _maxBuy == 0);

            return firstSentence ? FirstExchangeMessage : RepeatExchangeMessage;
        }
    }

    /// <summary>
    /// The text under the resource given away, <c>RedrawInfoBuySell()</c> of the reference
    /// (<c>dialog_marketplace.cpp:301-309</c>), e.g. <c>"-3 (7)"</c>.
    /// </summary>
    public string SellText => $"-{CountSell} ({_maxSell - CountSell})";

    /// <summary>
    /// The text under the resource asked for, <c>RedrawInfoBuySell()</c> of the reference
    /// (<c>dialog_marketplace.cpp:311-319</c>), e.g. <c>"+150 (250)"</c>; the number in brackets is what the
    /// treasury would hold after the trade.
    /// </summary>
    public string BuyText => $"+{CountBuy} ({(uint)_kingdom.Treasury.Get(_resourceTo) + CountBuy})";

    /// <summary>
    /// The text of the exchange rate window, <c>getExchangeRateText()</c>
    /// (<c>dialog_marketplace.cpp:80-100</c>).
    /// </summary>
    public string ExchangeRateText => ExchangeRateTextFor(_fromTradingPost, Marketplaces);

    /// <summary>
    /// The text of the exchange rate window for a market place count, <c>getExchangeRateText()</c>.
    /// </summary>
    /// <param name="fromTradingPost">Whether the window was opened from a trading post, which always says three.</param>
    /// <param name="marketplaces">How many market places the kingdom holds; the reference clamps the sentence at
    /// <see cref="PeakedMarketplaces"/>.</param>
    /// <returns>The sentence.</returns>
    /// <remarks>
    /// Static so the sentence can be checked for a count the port cannot build without nine towns; the reference's
    /// four branches do not touch the kingdom either.
    /// </remarks>
    public static string ExchangeRateTextFor(bool fromTradingPost, int marketplaces)
    {
        if (fromTradingPost)
        {
            return $"The Trading Post provides exchange rates equivalent to {TradingPostMarketplaces} marketplaces.";
        }

        return marketplaces switch
        {
            1 => "You currently control 1 marketplace. The more marketplaces you control, the better the exchange rate.",
            >= PeakedMarketplaces =>
                "You currently control enough marketplaces that the exchange rate has peaked. " +
                "Controlling additional marketplaces will not improve the exchange rate.",
            _ => $"You currently control {marketplaces} marketplaces. " +
                 "The more marketplaces you control, the better the exchange rate.",
        };
    }

    /// <summary>
    /// Picks the resource to give away, the "click from" loop of the reference
    /// (<c>dialog_marketplace.cpp:536-569</c>).
    /// </summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags, the first seven of the grid.</param>
    /// <remarks>
    /// The new selection resets the slider to zero and recomputes <see cref="MaxSell"/> and <see cref="MaxBuy"/>. The
    /// rate is only used when it is not zero, as in the reference; the stale <see cref="MaxBuy"/> that leaves behind
    /// is invisible, because the trade area is hidden whenever the rate is zero.
    /// </remarks>
    public void SelectFrom(int resource)
    {
        _resourceFrom = resource;
        _maxSell = (uint)Math.Max(0, _kingdom.Treasury.Get(resource));

        uint rate = TradeCost;
        if (rate != 0)
        {
            _maxBuy = _resourceTo == Resource.Gold ? _maxSell * rate : _maxSell / rate;
        }

        _sliderIndex = MinSliderIndex;
    }

    /// <summary>
    /// Picks the resource to ask for, the "click to" loop of the reference
    /// (<c>dialog_marketplace.cpp:572-598</c>).
    /// </summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags, the first seven of the grid.</param>
    public void SelectTo(int resource)
    {
        _resourceTo = resource;

        uint rate = TradeCost;
        if (rate != 0)
        {
            _maxBuy = _resourceTo == Resource.Gold ? _maxSell * rate : _maxSell / rate;
        }

        _sliderIndex = MinSliderIndex;
    }

    /// <summary>
    /// Drops both selections, what the reference does after a trade and after a gift
    /// (<c>dialog_marketplace.cpp:521-523</c>, <c>:647-649</c>).
    /// </summary>
    /// <remarks>
    /// The reference leaves <c>max_sell</c>/<c>max_buy</c> at their last values; the port clears them too. Nothing
    /// can read them while the trade area is hidden, and both the arrows and the slider are disabled there.
    /// </remarks>
    public void ClearSelection()
    {
        _resourceFrom = Resource.Unknown;
        _resourceTo = Resource.Unknown;
        _maxSell = 0;
        _maxBuy = 0;
        _sliderIndex = MinSliderIndex;
    }

    /// <summary>
    /// Moves the slider, the scrollbar branch of the reference (<c>dialog_marketplace.cpp:601-611</c>).
    /// </summary>
    /// <param name="index">Requested position; clamped into
    /// [<see cref="MinSliderIndex"/>, <see cref="SliderMaxIndex"/>].</param>
    /// <returns><see langword="false"/> when the trade area is hidden, which is when the reference's scrollbar is
    /// hidden as well.</returns>
    public bool SetSliderIndex(int index)
    {
        if (!IsTradeAreaVisible)
        {
            return false;
        }

        _sliderIndex = Math.Clamp(index, MinSliderIndex, SliderMaxIndex);

        return true;
    }

    /// <summary>
    /// Moves the slider one step up, <see cref="SliderMaxIndex"/>; the "Max" label and the right arrow of the
    /// reference (<c>dialog_marketplace.cpp:617-626</c>, <c>:672-683</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the slider moved.</returns>
    public bool IncreaseTrade()
    {
        if (!IsTradeAreaVisible || _sliderIndex >= SliderMaxIndex)
        {
            return false;
        }

        ++_sliderIndex;

        return true;
    }

    /// <summary>
    /// Moves the slider one step down, <see cref="MinSliderIndex"/>; the "Min" label and the left arrow of the
    /// reference (<c>dialog_marketplace.cpp:628-638</c>, <c>:659-670</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the slider moved.</returns>
    public bool DecreaseTrade()
    {
        if (!IsTradeAreaVisible || _sliderIndex <= MinSliderIndex)
        {
            return false;
        }

        --_sliderIndex;

        return true;
    }

    /// <summary>
    /// Jumps to the whole amount, the "Max" label of the reference (<c>dialog_marketplace.cpp:617-626</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the slider moved.</returns>
    public bool ChooseMaxTrade()
    {
        int before = _sliderIndex;

        return SetSliderIndex(SliderMaxIndex) && _sliderIndex != before;
    }

    /// <summary>
    /// Jumps to the smallest amount, the "Min" label of the reference (<c>dialog_marketplace.cpp:628-638</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the slider moved.</returns>
    /// <remarks>
    /// The reference moves the scrollbar to index <c>1</c>, not <c>0</c> - one unit, the least anyone would trade -
    /// and <c>Scrollbar::moveToIndex</c> clamps that into the range, so a range of one or zero ends up at its maximum.
    /// </remarks>
    public bool ChooseMinTrade()
    {
        int before = _sliderIndex;

        return SetSliderIndex(1) && _sliderIndex != before;
    }

    /// <summary>
    /// The exchange rate between two resources without touching the selection,
    /// <c>GetTradeCosts()</c> (<c>dialog_marketplace.cpp:60-63</c>).
    /// </summary>
    /// <remarks>The reference checks both sides and the market place count; this is the market place count part.</remarks>
    /// <param name="from">What is given away.</param>
    /// <param name="to">What is asked for.</param>
    /// <returns>Units of <paramref name="from"/> per unit of <paramref name="to"/>; zero when there is no rate.</returns>
    public uint RateTo(int from, int to) => ResourceTrading.GetTradeCost((uint)Marketplaces, from, to);

    /// <summary>
    /// The cost line drawn over a resource cell, <c>GetStringTradeCosts()</c>
    /// (<c>dialog_marketplace.cpp:65-78</c>).
    /// </summary>
    /// <remarks>
    /// Shown over every target cell as soon as something is selected on the left; the reference writes <c>1/</c> in
    /// front even for gold, whose rate is "how many gold per unit" and not a fraction.
    /// </remarks>
    /// <param name="from">What is given away.</param>
    /// <param name="to">What is asked for.</param>
    /// <returns><c>n/a</c> for the same resource on both sides, <c>1/rate</c> otherwise.</returns>
    public string CostText(int from, int to) => from == to ? "n/a" : $"1/{RateTo(from, to)}";

    /// <summary>
    /// Performs the trade, <c>dialog_marketplace.cpp:641-657</c>: takes <see cref="CountSell"/> of the resource given
    /// away out of the treasury, adds <see cref="CountBuy"/> of the one asked for, and drops both selections.
    /// </summary>
    /// <returns><see langword="true"/> when the treasury changed.</returns>
    /// <remarks>
    /// After a successful trade <see cref="IsFirstExchange"/> stays <see langword="false"/>: the shopkeeper's "please
    /// inspect our fine wares" is gone for the rest of the visit.
    /// </remarks>
    public bool Trade()
    {
        if (!CanTrade)
        {
            return false;
        }

        int from = _resourceFrom;
        int to = _resourceTo;
        uint sell = CountSell;
        uint buy = CountBuy;

        _kingdom.OddFundsResource(new Funds(from, sell));
        _kingdom.AddFundsResource(new Funds(to, buy));

        _isFirstExchange = false;
        ClearSelection();

        return true;
    }
}
