using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The town's texts and the hiring of a hero — <c>castle.cpp:976-1023</c> and <c>castle.cpp:2527-2570</c> — plus the
/// handful of reference members that belong to systems the port does not have yet, kept here as documented gaps
/// rather than silently missing.
/// </summary>
/// <remarks>
/// <para>
/// The gaps are the battle (<c>ActionPreBattle</c>, <c>ActionAfterBattle</c>), the sea (the four boat methods) and
/// the town screen (<c>DrawImageCastle</c>, <c>GetICNBuilding</c>, <c>GetICNBoat</c>). Each one needs a whole system
/// the port has not reached: <c>Battle::Tower</c>, the world's boat objects, and the AGG/ICN renderer of a castle
/// screen. They are listed here so that a later batch can find them by name.
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>
    /// The description the town screen shows for a building, <c>Castle::GetDescriptionBuilding()</c>
    /// (<c>castle.cpp:2527-2570</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The texts come from <see cref="CastleBuildingInfo.GetDescription"/> with one or two numbers still missing:
    /// the well's growth, the landmark's growth, the statue's gold, the special building's gold and the castle's
    /// gold per day. The reference substitutes them with <c>StringReplace</c> on the <c>%{count}</c> placeholder,
    /// which is why one text serves six towns.
    /// </para>
    /// <para>
    /// <b>Two paragraphs are missing</b> from the castle's text: the towers' and the moat's combat descriptions,
    /// which are <c>Battle::Tower::GetInfo()</c> and <c>Battle::Board::GetMoatInfo()</c> — text that belongs to the
    /// battle system. The numbers in the rest of the text are the town's own and are substituted here.
    /// </para>
    /// </remarks>
    /// <param name="buildingType">The building to describe.</param>
    /// <returns>The description, with the placeholders replaced.</returns>
    public string GetDescriptionBuilding(BuildingType buildingType)
    {
        string description = CastleBuildingInfo.GetDescription(Race, buildingType);

        switch (buildingType)
        {
        case BuildingType.Well:
            description = description.Replace("%{count}", GrownWell.ToString(), StringComparison.Ordinal);
            break;

        case BuildingType.Well2:
            description = description.Replace("%{count}", GrownWel2.ToString(), StringComparison.Ordinal);
            break;

        case BuildingType.Castle:
            description = description.Replace(
                "%{count}",
                ProfitConditions.FromBuilding(BuildingType.Castle, Race).Gold.ToString(),
                StringComparison.Ordinal);
            break;

        case BuildingType.Moat:
            description = description.Replace(
                "%{count}",
                GameStatic.BattleMoatReduceDefense.ToString(),
                StringComparison.Ordinal);
            break;

        case BuildingType.Spec:
        case BuildingType.Statue:
            description = description.Replace(
                "%{count}",
                ProfitConditions.FromBuilding(buildingType, Race).Gold.ToString(),
                StringComparison.Ordinal);
            break;

        default:
            break;
        }

        return description;
    }

    /// <summary>
    /// Hires a hero into the town, <c>Castle::RecruitHero()</c> (<c>castle.cpp:1004-1023</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The checks are the reference's and in its order: the hiring rules of <see cref="AllowBuyHero"/>, then that
    /// nobody is in the town, then the hero's own <see cref="Hero.Recruit"/>, and only after that is the price
    /// taken — a hero who could not be placed costs nothing.
    /// </para>
    /// <para>
    /// The reference mutates the world's hero and returns the pointer; the port returns the same hero or
    /// <see langword="null"/>, and takes the kingdom explicitly because <see cref="Castle"/> has no owner back
    /// pointer.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero to hire, <c>recruitHero( hero )</c>.</param>
    /// <param name="kingdom">The owner's kingdom, which pays and counts the heroes.</param>
    /// <param name="world">The map the hero is placed on.</param>
    /// <param name="guestHero">The hero already in the town, or <see langword="null"/> for none.</param>
    /// <returns>The hired hero, or <see langword="null"/> when the town refused.</returns>
    public Hero? RecruitHero(Hero? hero, Kingdom.Kingdom kingdom, World world, Hero? guestHero = null)
    {
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(world);

        if (hero is null || !AllowBuyHero(guestHero, kingdom) || !hero.Recruit(kingdom, TileIndex, world))
        {
            return null;
        }

        kingdom.OddFundsResource(PaymentConditions.RecruitHero());

        return hero;
    }

    /// <summary>
    /// Whether the town may launch a boat, <c>Castle::AllowBuyBoat()</c> (<c>castle.cpp:2125-2140</c>).
    /// </summary>
    /// <remarks>
    /// <b>Not ported.</b> Two of the three conditions need the map: <c>HasBoatNearby()</c> scans the water around
    /// the town for a boat object, and <c>getTileIndexToPlaceBoat()</c> picks the tile the new boat would appear on.
    /// The port has no boat objects in <see cref="World"/>, so the answer is always <see langword="false"/> — which
    /// is also the reference's answer for a town without a shipyard, and therefore harmless until the sea lands.
    /// </remarks>
    /// <param name="checkPayment">Ignored while the method is a gap.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool AllowBuyBoat(bool checkPayment = true) => false;

    /// <summary>
    /// Launches a boat, <c>Castle::BuyBoat()</c> (<c>castle.cpp:2142-2165</c>).
    /// </summary>
    /// <remarks>
    /// <b>Not ported</b>, see <see cref="AllowBuyBoat"/>: it needs <c>Tile::setBoat()</c> and the boat object on the
    /// map. Nothing is charged, because nothing is bought.
    /// </remarks>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool BuyBoat() => false;

    /// <summary>
    /// Whether a boat already waits next to the town, <c>Castle::HasBoatNearby()</c> (<c>castle.cpp:1862-1894</c>).
    /// </summary>
    /// <remarks><b>Not ported</b>: the port has no boat objects to look for. Always <see langword="false"/>.</remarks>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool HasBoatNearby() => false;

    /// <summary>
    /// The tile a new boat would appear on, <c>Castle::getTileIndexToPlaceBoat()</c> (<c>castle.cpp:1896-1929</c>).
    /// </summary>
    /// <remarks><b>Not ported</b>: it scans the map's water around the town. Always -1, the reference's "nowhere".</remarks>
    /// <returns>Always -1.</returns>
    public int GetTileIndexToPlaceBoat() => -1;

    /// <summary>
    /// What the town does before a battle, <c>Castle::ActionPreBattle()</c> (<c>castle.cpp:2249-2263</c>).
    /// </summary>
    /// <remarks>
    /// <b>Not ported</b>: the reference counts the defending side's castle objects — the walls, the towers, the
    /// keep — and sets them up in <c>Battle::Arena</c>. The port has no battle, so there is nothing to set up.
    /// </remarks>
    public void ActionPreBattle()
    {
    }

    /// <summary>
    /// What the town does after a battle, <c>Castle::ActionAfterBattle()</c> (<c>castle.cpp:2265-2290</c>).
    /// </summary>
    /// <remarks>
    /// <b>Not ported</b>: the reference removes the defenders' special abilities (the moat's, the towers') and, when
    /// the attacker won, destroys buildings and turns the town over. Both need the battle and the map.
    /// </remarks>
    /// <param name="attackerWins">Whether the attacker won, <c>attackerWins</c>.</param>
    public void ActionAfterBattle(bool attackerWins)
    {
    }

    /// <summary>
    /// Draws the town in the town screen, <c>Castle::DrawImageCastle()</c> (<c>castle.cpp:1326-1426</c>).
    /// </summary>
    /// <remarks>
    /// <b>Not ported</b>: it walks <see cref="CastleBuildingInfo.GetDrawingPriorities"/> and blits every built
    /// building out of the race's <c>TOWN*</c> ICN, which needs the castle screen's renderer.
    /// </remarks>
    public void DrawImageCastle()
    {
    }

    /// <summary>
    /// The ICN index of a building's sprite, <c>Castle::GetICNBuilding()</c> (<c>castle.cpp:1451-1860</c>).
    /// </summary>
    /// <remarks>
    /// <b>Not ported.</b> Despite the name it is not <see cref="CastleBuildingInfo.GetSpriteIndex"/>: that one is the
    /// offset inside a town's own icon set, while this one is a full <c>ICN::</c> id of the town screen — a
    /// four-hundred-line table that is only meaningful once the renderer can index ICN files by id. The port's
    /// asset layer addresses ICNs by file name, so the table would have to be rewritten rather than translated.
    /// </remarks>
    /// <param name="buildingType">The building.</param>
    /// <param name="race">The town's race, <c>static</c> in the reference as well.</param>
    /// <returns>Always 0, the reference's <c>ICN::UNKNOWN</c>.</returns>
    public static int GetICNBuilding(BuildingType buildingType, MapRace race) => 0;

    /// <summary>
    /// The ICN index of a race's boat, <c>Castle::GetICNBoat()</c> (<c>castle.cpp:1428-1449</c>).
    /// </summary>
    /// <remarks><b>Not ported</b>, see <see cref="GetICNBuilding"/>. Always 0.</remarks>
    /// <param name="race">The town's race.</param>
    /// <returns>Always 0.</returns>
    public static int GetICNBoat(MapRace race) => 0;

    /// <summary>
    /// Repairs the French accents of a town name read from an MP2 file, <c>Castle::fixFrenchCharactersInName()</c>
    /// (<c>castle.cpp:664-667</c>).
    /// </summary>
    /// <remarks>
    /// <b>Not ported</b>: it is <c>fheroes2::fixFrenchCharactersForMP2Map()</c>, the table that maps the
    /// DOS-era code page onto Latin-1, and it belongs with <see cref="MapTextCodec"/> rather than with the town. A
    /// town loaded from a French map therefore keeps the raw bytes of its name.
    /// </remarks>
    public void FixFrenchCharactersInName()
    {
    }
}
