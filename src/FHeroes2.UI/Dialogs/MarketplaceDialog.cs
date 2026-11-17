using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The marketplace, <c>Dialog::Marketplace()</c> (<c>dialog.h:131</c>, <c>dialog_marketplace.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window has no answer of its own: every trade it performs changes the funds of the kingdom on the spot, which
/// is why the reference takes the kingdom by reference and returns nothing. The port keeps the same shape - the model
/// holds the kingdom and the scene lets the window change it.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs only; layout comes with the widget layer.
/// </para>
/// </remarks>
public sealed class MarketplaceDialog
{
    private MarketplaceDialog(GameKingdom kingdom, bool isFromTradingPost)
    {
        Kingdom = kingdom;
        IsFromTradingPost = isFromTradingPost;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::Marketplace( Kingdom &amp;, const bool fromTradingPost )</c>.
    /// </summary>
    /// <param name="kingdom">The kingdom whose resources are traded; the dialog modifies it.</param>
    /// <param name="isFromTradingPost">
    /// Whether the window was opened from a trading post, the <c>fromTradingPost</c> of the reference, which decides
    /// whether the hero's army can be reached from it.
    /// </param>
    /// <returns>The model.</returns>
    public static MarketplaceDialog Create(GameKingdom kingdom, bool isFromTradingPost) =>
        new(kingdom, isFromTradingPost);

    /// <summary>The kingdom whose resources are traded.</summary>
    public GameKingdom Kingdom { get; }

    /// <summary>Whether the window was opened from a trading post.</summary>
    public bool IsFromTradingPost { get; }
}
