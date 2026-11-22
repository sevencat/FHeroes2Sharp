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
/// <b>Not ported yet.</b> The two picks — the Dimension Door square and the town list of Town Portal — are
/// interactive modes of the adventure map, and the Visions dialogs wait for the monster portrait element that the
/// text-only message box cannot draw. Until those arrive the two picks answer "cancelled", which costs no spell
/// points, and Visions only records the stacks in the kingdom.
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
    /// One stack the Visions spell shows, <c>heroes_spell.cpp:284-321</c>.
    /// </summary>
    /// <remarks>
    /// The reference header is <c>"%{count} plural-name"</c> and the body is chosen by the joining reason; the
    /// monster portrait that goes with the message waits for the dialog element.
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

        _dialogs.ShowMessage(header, body);
    }

    /// <summary>
    /// The screen as a <see cref="IHeroSpellCastHost"/>; it reads the private dialog layer and map view of the screen.
    /// </summary>
    /// <param name="screen">The screen that owns it.</param>
    private sealed class SpellCastHost(AdventureMap screen) : IHeroSpellCastHost
    {
        /// <inheritdoc />
        public int SelectDimensionDoorDestination(int sourceIndex, int distance, bool isWater)
        {
            // 目的地选取是冒险地图的一种交互模式（参考里那圈虚线框），随冒险地图一起归 S12。
            return -1;
        }

        /// <inheritdoc />
        public int SelectTownPortalTown(IReadOnlyList<CastleTown> castles)
        {
            // 城列表选取要一个带城堡图标的列表框，同上。
            return -1;
        }

        /// <inheritdoc />
        public void OpenViewWorld(ViewWorldMode mode) => screen.OpenSpellViewWorld(mode);

        /// <inheritdoc />
        public void ShowMonsterVision(Troop troop, in NeutralMonsterJoiningCondition join) =>
            screen.ShowSpellVisions(troop, join);
    }
}
