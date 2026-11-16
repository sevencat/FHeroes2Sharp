using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The fixed prices a kingdom pays for the odds and ends of the game, <c>PaymentConditions</c>
/// (<c>payment.cpp:29-52</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference spells each answer as <c>Funds( Cost{ ... } )</c>, and <c>Cost</c> orders its fields
/// <c>{ gold, wood, mercury, ore, sulfur, crystal, gems }</c> (<c>resource.h:37-46</c>). <see cref="Cost"/> has the
/// same order, so the literals below read exactly like the reference's — a boat costs a thousand gold and ten wood,
/// not a thousand wood and ten gold.
/// </para>
/// <para>
/// <see cref="BuyBuilding"/> is the only one that is not a constant: it forwards to the building price table
/// (<c>buildinginfo.cpp:203-212</c>), which the castle module already owns (ADR-112 / ADR-117).
/// </para>
/// </remarks>
public static class PaymentConditions
{
    /// <summary>
    /// The price of a building, <c>PaymentConditions::BuyBuilding()</c> (<c>payment.cpp:31</c>).
    /// </summary>
    /// <param name="race">The town's race; several buildings cost differently in different towns.</param>
    /// <param name="building">The building.</param>
    /// <returns>The price, or an empty <see cref="Funds"/> when the race cannot build it.</returns>
    /// <remarks>
    /// The parameter order is the reference's — <c>race</c> first. <see cref="CastleBuildings.GetCost"/> takes the
    /// same two values the other way round, which is why this method is not just a type alias.
    /// </remarks>
    public static Funds BuyBuilding(MapRace race, BuildingType building) => CastleBuildings.GetCost(building, race);

    /// <summary>A boat bought at the shipyard, <c>PaymentConditions::BuyBoat()</c> (<c>payment.cpp:36</c>).</summary>
    /// <returns>1000 gold and 10 wood.</returns>
    public static Funds BuyBoat() => new(new Cost { Gold = 1000, Wood = 10 });

    /// <summary>A spell book for a hero, <c>PaymentConditions::BuySpellBook()</c> (<c>payment.cpp:41</c>).</summary>
    /// <returns>500 gold.</returns>
    public static Funds BuySpellBook() => new(new Cost { Gold = 500 });

    /// <summary>A hero picked up in the tavern, <c>PaymentConditions::RecruitHero()</c> (<c>payment.cpp:46</c>).</summary>
    /// <returns>2500 gold.</returns>
    public static Funds RecruitHero() => new(new Cost { Gold = 2500 });

    /// <summary>The alchemist's fee for turning resources into experience, <c>PaymentConditions::ForAlchemist()</c>
    /// (<c>payment.cpp:51</c>).</summary>
    /// <returns>750 gold.</returns>
    public static Funds ForAlchemist() => new(new Cost { Gold = 750 });

    /// <summary>
    /// The artifact merchant's price for Magellan's maps, <c>PaymentConditions::getMagellansMapsPurchasePrice()</c>
    /// (<c>payment.cpp:56</c>).
    /// </summary>
    /// <returns>1000 gold.</returns>
    public static Funds GetMagellansMapsPurchasePrice() => new(new Cost { Gold = 1000 });
}
