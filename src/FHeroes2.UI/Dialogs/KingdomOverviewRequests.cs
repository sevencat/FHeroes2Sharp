using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// A bar of the overview that swallowed the frame and has to be finished by the owner of the window, the
/// <c>QueueEventProcessing()</c> calls of <c>kingdom_overview.cpp:296-323</c> and <c>:568-591</c>.
/// </summary>
/// <remarks>
/// The reference finishes the event on the spot: the bar opens its troop window, the exchange is applied to the army and
/// the dialog runs to completion inside the click. The port's bars answer what the frame asked for
/// (<see cref="ArmyBarEvent"/>) and the scene applies it, so the row only says which bar it was.
/// </remarks>
/// <param name="Bar">The bar the frame landed on.</param>
/// <param name="Other">The other army bar of the same row, when the two may exchange troops.</param>
/// <param name="Event">What the frame asked for.</param>
public readonly record struct KingdomArmyRequest(ArmyBar Bar, ArmyBar? Other, ArmyBarEvent Event);

/// <summary>
/// What the kingdom overview asks its owner to open: the <c>Game::OpenHeroesDialog</c>,
/// <c>Game::OpenCastleDialog</c>, <c>Dialog::QuickInfoWithIndicationOnRadar</c>, <c>fheroes2::showKingdomIncome</c> and
/// <c>showLighthouseInfo</c> calls of <c>kingdom_overview.cpp</c>.
/// </summary>
/// <remarks>
/// The reference opens the dialogs from inside the click and blocks there; the port collects the requests of one frame
/// and lets the scene open them, which is how every other ported screen here keeps drawing and modal loops apart.
/// </remarks>
public sealed class KingdomOverviewRequests
{
    /// <summary>Troop interactions the army bars of the rows asked for, in the order they happened.</summary>
    public List<KingdomArmyRequest> Army { get; } = [];

    /// <summary>The hero whose dialog was asked for, <c>Game::OpenHeroesDialog</c> (<c>:284</c>).</summary>
    public Hero? HeroDialog { get; set; }

    /// <summary>The town whose dialog was asked for, <c>Game::OpenCastleDialog</c> (<c>:547</c>).</summary>
    public CastleTown? CastleDialog { get; set; }

    /// <summary>The hero a right click asks a quick info about (<c>:292</c>).</summary>
    public Hero? HeroInfo { get; set; }

    /// <summary>The town a right click asks a quick info about (<c>:558</c>).</summary>
    public CastleTown? CastleInfo { get; set; }

    /// <summary>The captain a right click asks a quick info about (<c>:571</c>).</summary>
    public Captain? CaptainInfo { get; set; }

    /// <summary>Whether the income dialog was asked for, <c>fheroes2::showKingdomIncome()</c> (<c>:860</c>, <c>:899</c>).</summary>
    public bool Income { get; set; }

    /// <summary>Whether the lighthouse dialog was asked for, <c>fheroes2::showLighthouseInfo()</c> (<c>:863</c>, <c>:903</c>).</summary>
    public bool Lighthouse { get; set; }

    /// <summary>
    /// The standard message a right click on one of the three buttons asked for, <c>:850-858</c>.
    /// </summary>
    public DialogPopup? Popup { get; set; }

    /// <summary>Whether the whole window has to fade in again, <c>needFadeIn</c> (<c>:285</c>, <c>:555</c>).</summary>
    public bool NeedFadeIn { get; set; }

    /// <summary>Whether a dialog was asked for, which is what stops the click from being handled any further.</summary>
    public bool HasDialog =>
        HeroDialog is not null || CastleDialog is not null || HeroInfo is not null || CastleInfo is not null ||
        CaptainInfo is not null || Income || Lighthouse || Popup is not null;

    /// <summary>Whether anything at all was asked for in the last frame.</summary>
    public bool IsEmpty => Army.Count == 0 && !HasDialog && !NeedFadeIn;

    /// <summary>Forgets everything that was collected, ready for the next frame.</summary>
    public void Clear()
    {
        Army.Clear();
        HeroDialog = null;
        CastleDialog = null;
        HeroInfo = null;
        CastleInfo = null;
        CaptainInfo = null;
        Income = false;
        Lighthouse = false;
        Popup = null;
        NeedFadeIn = false;
    }
}
