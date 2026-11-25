using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Interface;

/// <summary>
/// What the adventure spells of this screen ask the player, the port's stand-in for the modal calls inside
/// <c>heroes_spell.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The screen is the only place that has both the dialog layer and the scrolled map, so it plays the host itself
/// through a nested class: opening View World needs <see cref="AdventureMapDialogs.OpenViewWorld"/> plus the map
/// view that <see cref="AttachView"/> handed over, and neither is reachable from the game layer.
/// </para>
/// <para>
/// The town list of Town Portal is <see cref="AdventureMapDialogs.SelectKingdomCastle"/>, the
/// <c>dialog_selectitems.cpp</c> list box with the castle icons. The Dimension Door square is an interactive mode of
/// this screen and the Visions dialogs already draw the creature on the stone plate, <see cref="MonsterPlateArtwork"/>.
/// </para>
/// </remarks>
public sealed partial class AdventureMap
{
    /// <summary>
    /// The host the spell caster talks to, <see cref="IHeroSpellCastHost"/>.
    /// </summary>
    private readonly IHeroSpellCastHost _spellCastHost;

    /// <summary>
    /// The View World window on the mode of a view spell, <c>ViewWorld::ViewWorldWindow()</c>.
    /// </summary>
    /// <remarks>
    /// Same path as <see cref="EventViewWorld"/>: the dialog layer runs the window and hands back the zoom level it was
    /// left at. Without an attached view there is nothing to show, exactly like the menu entry.
    /// </remarks>
    /// <param name="mode">What the window reveals.</param>
    private void OpenSpellViewWorld(ViewWorldMode mode)
    {
        if (_view is not { } view)
        {
            return;
        }

        _viewWorldZoom = _dialogs.OpenViewWorld(mode, view, _viewWorldZoom, _viewport);
    }

    /// <summary>
    /// The town Town Portal sends the hero to, <c>Dialog::selectKingdomCastle()</c>
    /// (<c>heroes_spell.cpp:861-880</c>).
    /// </summary>
    /// <remarks>
    /// The reference asks with the fixed texts <c>_( "Town Portal" )</c> and <c>_( "Select town to port to." )</c> and
    /// lets the dialog layer filter and draw the list; the hero's own town comes first because the caller keeps the
    /// kingdom's order.
    /// </remarks>
    /// <param name="castles">The towns the hero may port to, already without the ones a hero stands in.</param>
    /// <returns>The tile index of the chosen town's gate, or -1 when the player cancelled.</returns>
    private int SelectTownPortalTown(IReadOnlyList<CastleTown> castles) =>
        _dialogs.SelectKingdomCastle(castles, "Town Portal", "Select town to port to.");

    /// <summary>
    /// One stack the Visions spell shows, <c>heroes_spell.cpp:284-321</c>.
    /// </summary>
    /// <remarks>
    /// The reference header is <c>"%{count} plural-name"</c> and the body is chosen by the joining reason; the
    /// creature goes onto the stone plate under the text, <c>fheroes2::MonsterDialogElement</c>.
    /// </remarks>
    /// <param name="troop">The stack on the tile.</param>
    /// <param name="join">What the stack would do.</param>
    private void ShowSpellVisions(Troop troop, in NeutralMonsterJoiningCondition join)
    {
        string header = $"{troop.Count} {MonsterNames.GetPluralName(troop.Monster, troop.Count)}";

        string body = join.Reason switch
        {
            NeutralMonsterJoiningReason.Free or NeutralMonsterJoiningReason.Alliance =>
                "The creatures are willing to join us!",
            NeutralMonsterJoiningReason.ForMoney => (join.MonsterCount == troop.Count
                    ? "All the creatures will join us..."
                    : $"{join.MonsterCount} of the creatures will join us...")
                + $"\n\n for a fee of {troop.GetTotalCost().Gold} gold.",
            NeutralMonsterJoiningReason.RunAway or NeutralMonsterJoiningReason.Bane =>
                "These weak creatures will surely flee before us.",
            _ => "I fear these creatures are in the mood for a fight.",
        };

        _dialogs.ShowMonsterMessage(header, body, troop.Monster);
    }

    /// <summary>
    /// The screen as a <see cref="IHeroSpellCastHost"/>; it reads the private dialog layer and map view of the screen.
    /// </summary>
    /// <param name="screen">The screen that owns it.</param>
    private sealed class SpellCastHost(AdventureMap screen) : IHeroSpellCastHost
    {
        /// <inheritdoc />
        public int SelectDimensionDoorDestination(int sourceIndex, int distance, bool isWater) =>
            screen.SelectDimensionDoorDestination(sourceIndex, distance, isWater);

        /// <inheritdoc />
        public int SelectTownPortalTown(IReadOnlyList<CastleTown> castles) => screen.SelectTownPortalTown(castles);

        /// <inheritdoc />
        public void OpenViewWorld(ViewWorldMode mode) => screen.OpenSpellViewWorld(mode);

        /// <inheritdoc />
        public void ShowMonsterVision(Troop troop, in NeutralMonsterJoiningCondition join) =>
            screen.ShowSpellVisions(troop, join);
    }
}
