using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// What one resource costs in another at the market place, <c>getTradeCost()</c> (<c>resource_trading.cpp:25-160</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference sorts the resources into three categories and keeps one exchange rate per pair of categories and
/// per number of market places:
/// </para>
/// <list type="bullet">
/// <item><description>common — wood and ore;</description></item>
/// <item><description>rare — mercury, sulfur, crystal and gems;</description></item>
/// <item><description>gold.</description></item>
/// </list>
/// <para>
/// Eight ordered pairs of categories, of which the two within-category pairs share a table, so seven tables of nine
/// entries: the columns are one to nine market places. Every table is in the reference's order and none of them is
/// a formula — the market gives 25 gold for one wood at one market place and 124 at nine, and the curve between
/// those two numbers is the reference's, not a rounded exponential.
/// </para>
/// <para>
/// This class is the whole of <c>resource_trading.cpp</c>: the seven tables and one switch. The window that shows them
/// is <see cref="FHeroes2.UI.Dialogs.MarketplaceDialog"/>.
/// </para>
/// </remarks>
public static class ResourceTrading
{
    /// <summary>The largest number of market places the tables have a column for, <c>maxMarketplaces</c>.</summary>
    /// <remarks>Nine market places is also the most a town can have: one per town, and a map never holds more.</remarks>
    private const uint MaxMarketplaces = 9;

    /// <summary>Common to common, and rare to rare, <c>resourceToResource</c>.</summary>
    private static readonly uint[] ResourceToResource = [10, 7, 5, 4, 4, 3, 3, 3, 2];

    /// <summary>Common to rare, <c>commonToRare</c>.</summary>
    private static readonly uint[] CommonToRare = [20, 14, 10, 8, 7, 6, 5, 5, 4];

    /// <summary>Rare to common, <c>rareToCommon</c>.</summary>
    private static readonly uint[] RareToCommon = [5, 4, 3, 2, 2, 2, 2, 2, 1];

    /// <summary>Common to gold, <c>commonToGold</c>.</summary>
    private static readonly uint[] CommonToGold = [25, 37, 50, 62, 74, 87, 100, 112, 124];

    /// <summary>Rare to gold, <c>rareToGold</c>.</summary>
    private static readonly uint[] RareToGold = [50, 74, 100, 124, 149, 175, 200, 224, 249];

    /// <summary>Gold to common, <c>goldToCommon</c>.</summary>
    private static readonly uint[] GoldToCommon = [2500, 1667, 1250, 1000, 834, 715, 625, 556, 500];

    /// <summary>Gold to rare, <c>goldToRare</c>.</summary>
    private static readonly uint[] GoldToRare = [5000, 3334, 2500, 2000, 1667, 1429, 1250, 1112, 1000];

    /// <summary>
    /// How many units of <paramref name="resourceTo"/> one unit of <paramref name="resourceFrom"/> buys,
    /// <c>getTradeCost()</c> (<c>resource_trading.cpp:59</c>).
    /// </summary>
    /// <param name="marketplaceCount">How many market places the kingdom owns, one to nine.</param>
    /// <param name="resourceFrom">One of the <see cref="Resource"/> flags, the resource given away.</param>
    /// <param name="resourceTo">One of the <see cref="Resource"/> flags, the resource bought.</param>
    /// <returns>The rate, or 0 when the trade is meaningless (the same resource, an unset one, no market).</returns>
    /// <remarks>
    /// <para>
    /// The reference asserts on a market count of zero and on a resource that is not one of the seven, then returns
    /// 0. The port returns 0 without the assert: a dialog that has nothing selected is a normal state, not a bug.
    /// </para>
    /// <para>
    /// More than nine market places is clamped rather than rejected. The reference clamps too, and it matters for
    /// <c>AllCastles</c>: a map can place more towns than the table has columns.
    /// </para>
    /// </remarks>
    public static uint GetTradeCost(uint marketplaceCount, int resourceFrom, int resourceTo)
    {
        if (marketplaceCount == 0)
        {
            return 0;
        }

        // Nothing selected on either side, or the same resource twice: there is nothing to trade.
        if (resourceFrom == resourceTo || resourceFrom == Resource.Unknown || resourceTo == Resource.Unknown)
        {
            return 0;
        }

        if (marketplaceCount > MaxMarketplaces)
        {
            marketplaceCount = MaxMarketplaces;
        }

        // The tables are zero based, the count is not.
        uint column = marketplaceCount - 1;

        switch (resourceFrom)
        {
            case Resource.Gold:
                switch (resourceTo)
                {
                    case Resource.Wood:
                    case Resource.Ore:
                        return GoldToCommon[column];
                    case Resource.Mercury:
                    case Resource.Sulfur:
                    case Resource.Crystal:
                    case Resource.Gems:
                        return GoldToRare[column];
                    default:
                        return 0;
                }

            case Resource.Wood:
            case Resource.Ore:
                switch (resourceTo)
                {
                    case Resource.Wood:
                    case Resource.Ore:
                        return ResourceToResource[column];
                    case Resource.Gold:
                        return CommonToGold[column];
                    case Resource.Mercury:
                    case Resource.Sulfur:
                    case Resource.Crystal:
                    case Resource.Gems:
                        return CommonToRare[column];
                    default:
                        return 0;
                }

            case Resource.Mercury:
            case Resource.Sulfur:
            case Resource.Crystal:
            case Resource.Gems:
                switch (resourceTo)
                {
                    case Resource.Wood:
                    case Resource.Ore:
                        return RareToCommon[column];
                    case Resource.Gold:
                        return RareToGold[column];
                    case Resource.Mercury:
                    case Resource.Sulfur:
                    case Resource.Crystal:
                    case Resource.Gems:
                        return ResourceToResource[column];
                    default:
                        return 0;
                }

            default:
                return 0;
        }
    }
}
