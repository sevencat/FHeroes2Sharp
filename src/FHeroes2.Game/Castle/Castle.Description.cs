using FHeroes2.Assets.Maps;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The town's texts and the hiring of a hero — <c>castle.cpp:976-1023</c> and <c>castle.cpp:2527-2570</c> — plus the
/// handful of reference members that still belong to systems the port does not have, kept here as documented gaps
/// rather than silently missing.
/// </summary>
/// <remarks>
/// <para>
/// The remaining gaps are the town screen (<see cref="DrawImageCastle"/>) and the MP2 name fix-up
/// (<see cref="FixFrenchCharactersInName"/>). The first needs the castle screen's AGG/ICN renderer; the second
/// belongs with <see cref="MapTextCodec"/>. The battle hooks and the boats used to live here and now have their own
/// files, <c>Castle.Battle.cs</c> and <c>Castle.Boats.cs</c>; the ICN tables are in <c>Castle.IcnTables.cs</c>.
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
    /// The keep's text is followed by two paragraphs that belong to the battle system rather than to the town's table:
    /// the towers' shooting (<c>Battle::Tower::GetInfo()</c>, always when the keep stands) and the moat
    /// (<c>Battle::Board::GetMoatInfo()</c>, when the moat is built). Outside combat no tower is damaged, so the
    /// paragraphs describe the full complement. The numbers in the rest of the text are the town's own and are
    /// substituted here.
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

            // The keep's own text is followed by the battle parameters of what it brings: the towers' shooting and
            // the moat. Both are the battle system's, so they are asked for rather than kept in the town's table.
            if (IsBuild(BuildingType.Castle))
            {
                description += "\n\n" + BattleTower.GetInfo(this);
            }

            if (IsBuild(BuildingType.Moat))
            {
                description += "\n\n" + BattleBoard.GetMoatInfo();
            }

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
