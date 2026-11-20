using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="MarketplaceDialog"/>, the state of <c>Dialog::Marketplace()</c>
/// (<c>dialog_marketplace.cpp:385-685</c>).
/// </summary>
public sealed class MarketplaceDialogTests
{
    [Fact]
    public void Create_RejectsNullKingdom()
    {
        Assert.Throws<ArgumentNullException>(() => MarketplaceDialog.Create(null!, true));
    }

    [Fact]
    public void Create_RejectsNegativePlayerCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MarketplaceDialog.Create(new GameKingdom(), true, -1));
    }

    [Fact]
    public void TradingPost_IsWorthThreeMarketplaces()
    {
        // A trading post ignores the towns: the reference substitutes tradingPostValue = 3.
        MarketplaceDialog dialog = CreateTradingPost();

        Assert.Equal(3, dialog.Marketplaces);
        Assert.Equal(3, MarketplaceDialog.TradingPostMarketplaces);
    }

    [Fact]
    public void WithoutTowns_TheRateIsZero()
    {
        // No market place, no rate: the seven tables have no column for zero.
        var dialog = MarketplaceDialog.Create(new GameKingdom(), false);

        Assert.Equal(0, dialog.Marketplaces);
        Assert.Equal(0u, dialog.TradeCost);

        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);

        Assert.Equal(0u, dialog.TradeCost);
        Assert.Equal(0u, dialog.MaxBuy);
        Assert.False(dialog.IsTradeAreaVisible);
    }

    [Fact]
    public void NothingSelected_ShowsTheFirstSentence()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);

        Assert.Equal(Resource.Unknown, dialog.ResourceFrom);
        Assert.Equal(Resource.Unknown, dialog.ResourceTo);
        Assert.Equal(0u, dialog.TradeCost);
        Assert.Equal(MarketplaceDialog.FirstExchangeMessage, dialog.Message);
    }

    [Fact]
    public void GoldTarget_MultipliesInsteadOfDividing()
    {
        // Three market places: one wood is worth 50 gold (commonToGold[2]).
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);

        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);

        Assert.Equal(10u, dialog.MaxSell);
        Assert.Equal(500u, dialog.MaxBuy);
        Assert.Equal(10, dialog.SliderMaxIndex);
        Assert.Equal("I can offer you 50 for 1 unit of Wood.", dialog.Message);
    }

    [Fact]
    public void GoldTarget_SliderIndexIsTheAmountSold()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);

        Assert.True(dialog.SetSliderIndex(4));

        Assert.Equal(4u, dialog.CountSell);
        Assert.Equal(200u, dialog.CountBuy);
        Assert.Equal("-4 (6)", dialog.SellText);
        Assert.Equal("+200 (200)", dialog.BuyText);
        Assert.True(dialog.CanTrade);
    }

    [Fact]
    public void ResourceTarget_DividesAndRoundsDown()
    {
        // Wood to ore is common to common: 5 wood per ore, so 10 wood buys 2 ore and the slider stops at 2.
        MarketplaceDialog dialog = CreateTradingPost(wood: 10, ore: 3);

        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Ore);

        Assert.Equal(2u, dialog.MaxBuy);
        Assert.Equal(2, dialog.SliderMaxIndex);
        Assert.Equal("I can offer you 1 unit of Ore for 5 units of Wood.", dialog.Message);

        Assert.True(dialog.SetSliderIndex(2));

        Assert.Equal(10u, dialog.CountSell);
        Assert.Equal(2u, dialog.CountBuy);
        Assert.Equal("-10 (0)", dialog.SellText);
        Assert.Equal("+2 (5)", dialog.BuyText);
    }

    [Fact]
    public void Slider_ClampsToItsRange()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);

        Assert.True(dialog.SetSliderIndex(999));
        Assert.Equal(10, dialog.SliderIndex);

        Assert.True(dialog.SetSliderIndex(-5));
        Assert.Equal(0, dialog.SliderIndex);
    }

    [Fact]
    public void Arrows_StopAtTheEnds()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 2);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);

        Assert.True(dialog.IncreaseTrade());
        Assert.True(dialog.IncreaseTrade());
        Assert.False(dialog.IncreaseTrade());
        Assert.Equal(2, dialog.SliderIndex);

        Assert.True(dialog.DecreaseTrade());
        Assert.True(dialog.DecreaseTrade());
        Assert.False(dialog.DecreaseTrade());
        Assert.Equal(0, dialog.SliderIndex);
    }

    [Fact]
    public void ArrowsAndSlider_DoNothingWhileTheTradeAreaIsHidden()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 3);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Ore);

        // Three wood cannot buy a single ore, so the reference hides the whole trade area.
        Assert.False(dialog.IsTradeAreaVisible);
        Assert.False(dialog.SetSliderIndex(1));
        Assert.False(dialog.IncreaseTrade());
        Assert.False(dialog.DecreaseTrade());
    }

    [Fact]
    public void SameResourceTwice_HidesTheTradeArea()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);

        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Wood);

        Assert.False(dialog.IsTradeAreaVisible);
        Assert.Equal(0u, dialog.TradeCost);
        Assert.False(dialog.CanTrade);
        Assert.False(dialog.Trade());
    }

    [Fact]
    public void GoldTrade_MovesTheTreasury()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10, gold: 100);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);
        dialog.SetSliderIndex(4);

        Assert.True(dialog.Trade());

        Assert.Equal(6, dialog.Kingdom.Treasury.Wood);
        Assert.Equal(300, dialog.Kingdom.Treasury.Gold);
        Assert.False(dialog.IsFirstExchange);
        Assert.Equal(Resource.Unknown, dialog.ResourceFrom);
        Assert.Equal(Resource.Unknown, dialog.ResourceTo);
        Assert.Equal(MarketplaceDialog.RepeatExchangeMessage, dialog.Message);
        Assert.False(dialog.Trade());
    }

    [Fact]
    public void ResourceTrade_MovesBothSides()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10, ore: 3);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Ore);
        dialog.SetSliderIndex(2);

        Assert.True(dialog.Trade());

        Assert.Equal(0, dialog.Kingdom.Treasury.Wood);
        Assert.Equal(5, dialog.Kingdom.Treasury.Ore);
    }

    [Fact]
    public void Trade_NeedsANonZeroQuantity()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10, gold: 100);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);

        Assert.True(dialog.IsTradeAreaVisible);
        Assert.False(dialog.CanTrade);
        Assert.False(dialog.Trade());
        Assert.Equal(10, dialog.Kingdom.Treasury.Wood);
    }

    [Fact]
    public void EmptyTreasury_HidesTheTradeArea()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);
        dialog.SetSliderIndex(3);

        dialog.Kingdom.Treasury.Wood = 0;

        Assert.False(dialog.IsTradeAreaVisible);
        Assert.False(dialog.CanTrade);

        // The reference picks the "fine wares" sentence only when the two sides are the same resource or nothing
        // can be bought; a treasury emptied underneath the selection therefore shows the "bargain" sentence without
        // a trade having happened, and the port keeps that.
        Assert.Equal(MarketplaceDialog.RepeatExchangeMessage, dialog.Message);
    }

    [Fact]
    public void StaleMaxBuy_IsKeptWhenTheRateBecomesZero()
    {
        // The reference only recomputes max_buy when the rate is non-zero, so picking the same resource twice
        // leaves the previous value. It is unobservable - the trade area is hidden - but the model keeps it.
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);

        Assert.Equal(500u, dialog.MaxBuy);

        dialog.SelectTo(Resource.Wood);

        Assert.Equal(500u, dialog.MaxBuy);
        Assert.False(dialog.IsTradeAreaVisible);
    }

    [Fact]
    public void ClearSelection_ResetsEverything()
    {
        MarketplaceDialog dialog = CreateTradingPost(wood: 10);
        dialog.SelectFrom(Resource.Wood);
        dialog.SelectTo(Resource.Gold);
        dialog.SetSliderIndex(5);

        dialog.ClearSelection();

        Assert.Equal(Resource.Unknown, dialog.ResourceFrom);
        Assert.Equal(Resource.Unknown, dialog.ResourceTo);
        Assert.Equal(0u, dialog.MaxSell);
        Assert.Equal(0u, dialog.MaxBuy);
        Assert.Equal(0, dialog.SliderIndex);
        Assert.Equal(0u, dialog.CountSell);
        Assert.Equal(0u, dialog.CountBuy);
    }

    [Fact]
    public void Gift_IsEnabledWithTwoPlayers()
    {
        Assert.False(CreateTradingPost(players: 1).IsGiftEnabled);
        Assert.True(CreateTradingPost(players: 2).IsGiftEnabled);
        Assert.True(CreateTradingPost(players: 4).IsGiftEnabled);
    }

    [Fact]
    public void ExchangeRateText_NamesTheTradingPost()
    {
        // The trading post sentence does not depend on how many towns the kingdom holds.
        MarketplaceDialog dialog = CreateTradingPost();

        Assert.Equal(
            "The Trading Post provides exchange rates equivalent to 3 marketplaces.",
            dialog.ExchangeRateText);
    }

    [Fact]
    public void ExchangeRateText_CountsTheMarketplaces()
    {
        Assert.Equal(
            "You currently control 0 marketplaces. The more marketplaces you control, the better the exchange rate.",
            MarketplaceDialog.ExchangeRateTextFor(fromTradingPost: false, marketplaces: 0));
        Assert.Equal(
            "You currently control 1 marketplace. The more marketplaces you control, the better the exchange rate.",
            MarketplaceDialog.ExchangeRateTextFor(fromTradingPost: false, marketplaces: 1));
        Assert.Equal(
            "You currently control 4 marketplaces. The more marketplaces you control, the better the exchange rate.",
            MarketplaceDialog.ExchangeRateTextFor(fromTradingPost: false, marketplaces: 4));
    }

    [Fact]
    public void ExchangeRateText_SaysWhenTheRateHasPeaked()
    {
        // Nine is the last column of the tables; the reference says the rate has peaked from there on.
        Assert.Equal(9, MarketplaceDialog.PeakedMarketplaces);

        string peaked = MarketplaceDialog.ExchangeRateTextFor(fromTradingPost: false, marketplaces: 9);

        Assert.Equal(
            "You currently control enough marketplaces that the exchange rate has peaked. " +
            "Controlling additional marketplaces will not improve the exchange rate.",
            peaked);
        Assert.Equal(peaked, MarketplaceDialog.ExchangeRateTextFor(fromTradingPost: false, marketplaces: 10));
        Assert.NotEqual(peaked, MarketplaceDialog.ExchangeRateTextFor(fromTradingPost: false, marketplaces: 8));
    }

    private static MarketplaceDialog CreateTradingPost(int wood = 0, int gold = 0, int ore = 0, int players = 1)
    {
        var kingdom = new GameKingdom();
        kingdom.Treasury.Wood = wood;
        kingdom.Treasury.Gold = gold;
        kingdom.Treasury.Ore = ore;

        return MarketplaceDialog.Create(kingdom, true, players);
    }
}
