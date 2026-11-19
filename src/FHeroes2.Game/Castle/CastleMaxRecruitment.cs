using System.Text;

using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using ArmyTroops = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Castle;

/// <summary>
/// What the well's MAX button would buy, <c>Castle::_recruitCastleMax()</c>
/// (<c>castle_well.cpp:125-215</c>).
/// </summary>
/// <remarks>
/// <para>
/// The plan walks the dwellings from the strongest creature to the weakest, so the money goes to the best troops
/// first, and asks of each one how many it can afford <i>on top of what is already committed</i>. A dwelling is
/// skipped when neither the castle's garrison nor the visiting hero can take the creature — including the case where
/// the earlier steps filled the last free slot.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163). No dialog and no payment: the caller shows <see cref="Entries"/> with
/// <see cref="Cost"/>, and only then calls <c>Castle.RecruitMonster</c> for each entry. The reference pays with
/// <c>GetKingdom().AddFundsResource()</c>; the port has no kingdom reference inside <see cref="Castle"/>, so the
/// treasury is an argument and the caller keeps the money.
/// </para>
/// <para>
/// The two corps are <b>scratch copies</b>: the plan joins the creatures into them as it goes, exactly like the
/// reference's <c>tempCastleArmy</c>/<c>tempGuestArmy</c>, so that a later dwelling sees the slots the earlier ones
/// took. Pass copies, not the live armies.
/// </para>
/// </remarks>
public sealed class CastleMaxRecruitment
{
    /// <summary>Why nothing could be recruited, <c>Castle::_recruitCastleMax()</c>'s three messages.</summary>
    public enum Refusal
    {
        /// <summary>There is something to buy.</summary>
        None,

        /// <summary>No dwelling has creatures waiting, <i>"No creatures available for purchase."</i>.</summary>
        NoCreatures,

        /// <summary>There is a creature but not enough for a single one of it.</summary>
        NotEnoughResources,

        /// <summary>
        /// There is a creature and it is affordable, so the only thing in the way is room: both corps are full, or
        /// the earlier steps of the plan filled them.
        /// </summary>
        RanksFull,
    }

    /// <summary>One dwelling's share of the purchase.</summary>
    /// <param name="Monster">The creature, already upgraded if the town has the upgrade.</param>
    /// <param name="Count">How many of it the treasury can take.</param>
    public sealed record Entry(MonsterType Monster, uint Count);

    /// <summary>
    /// The six dwellings from the first level to the sixth, the reference's <c>castleDwellings</c>. The plan walks
    /// this array backwards.
    /// </summary>
    private static readonly BuildingType[] Dwellings =
    {
        BuildingType.DwellingMonster1,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingMonster6,
    };

    private CastleMaxRecruitment(IReadOnlyList<Entry> entries, Funds cost, Refusal reason, string summary)
    {
        Entries = entries;
        Cost = cost;
        Reason = reason;
        Summary = summary;
    }

    /// <summary>The creatures to buy, from the strongest to the weakest.</summary>
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>What all of <see cref="Entries"/> costs together.</summary>
    public Funds Cost { get; }

    /// <summary>Why the plan is empty, <see cref="Refusal.None"/> when it is not.</summary>
    public Refusal Reason { get; }

    /// <summary>
    /// One line per entry, <c>"PluralName : count\n"</c>, the text the reference puts in the confirmation dialog.
    /// </summary>
    /// <remarks>
    /// A single creature uses the singular name, since the reference asks
    /// <c>Monster::GetPluralName( count )</c> and that answers the singular for <c>1</c>.
    /// </remarks>
    public string Summary { get; }

    /// <summary>Whether the plan buys anything.</summary>
    public bool HasSomethingToRecruit => Entries.Count > 0;

    /// <summary>
    /// The message the reference shows when the plan is empty; <see langword="null"/> when there is something to buy.
    /// </summary>
    public string? RefusalText => Reason switch
    {
        Refusal.NoCreatures => "No creatures available for purchase.",
        Refusal.NotEnoughResources => "Not enough resources to recruit creatures.",
        Refusal.RanksFull => "You are unable to recruit at this time, your ranks are full.",
        _ => null,
    };

    /// <summary>Plans the purchase for a town, <c>Castle::_recruitCastleMax( currentCastleArmy )</c>.</summary>
    /// <param name="castle">The town whose dwellings are emptied.</param>
    /// <param name="castleArmy">The town's garrison, as a scratch copy; creatures are joined into it as planned.</param>
    /// <param name="guestArmy">The visiting hero's army as a scratch copy, or <see langword="null"/> without a guest.</param>
    /// <param name="treasury">What the kingdom can spend.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> or <paramref name="castleArmy"/> is <see langword="null"/>.</exception>
    public static CastleMaxRecruitment Create(Castle castle, ArmyTroops castleArmy, ArmyTroops? guestArmy, Funds treasury)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(castleArmy);

        List<Entry> entries = new(Dwellings.Length);
        StringBuilder summary = new();
        Funds totalCost = new();

        for (int i = 0; i < Dwellings.Length; ++i)
        {
            BuildingType dwelling = Dwellings[Dwellings.Length - i - 1];

            uint count = HowManyRecruitMonster(castle, castleArmy, guestArmy, dwelling, totalCost, treasury,
                out Funds cost);

            if (count == 0)
            {
                continue;
            }

            MonsterType monster = DwellingMonsters.FromDwelling(castle.Race, castle.GetActualDwelling(dwelling));

            entries.Add(new Entry(monster, count));
            totalCost += cost;

            summary.Append(count == 1 ? MonsterNames.GetSingular(monster) : MonsterNames.GetMulti(monster))
                .Append(" : ")
                .Append(count)
                .Append('\n');
        }

        return entries.Count > 0
            ? new CastleMaxRecruitment(entries, totalCost, Refusal.None, summary.ToString())
            : new CastleMaxRecruitment(entries, totalCost, GetRefusal(castle, treasury), string.Empty);
    }

    /// <summary>
    /// Why nothing can be bought, <c>castle_well.cpp:170-196</c>.
    /// </summary>
    /// <remarks>
    /// Only the <b>first</b> dwelling with creatures is looked at: the reference breaks out of its loop as soon as it
    /// finds one, so a town whose first stocked dwelling is unaffordable is reported as poor even when a cheaper
    /// creature waits further up the chain.
    /// </remarks>
    private static Refusal GetRefusal(Castle castle, Funds treasury)
    {
        foreach (BuildingType dwelling in Dwellings)
        {
            if (castle.GetMonstersInDwelling(dwelling) == 0)
            {
                continue;
            }

            MonsterType monster = DwellingMonsters.FromDwelling(castle.Race, dwelling);

            return treasury >= MonsterStatistics.GetGeneralStats(monster).GetCost()
                ? Refusal.RanksFull
                : Refusal.NotEnoughResources;
        }

        return Refusal.NoCreatures;
    }

    /// <summary>
    /// How many of one dwelling the treasury can take, <c>howManyRecruitMonster()</c>
    /// (<c>castle_well.cpp:66-99</c>).
    /// </summary>
    /// <remarks>
    /// The count walks down from the stock until the payment fits next to <paramref name="alreadySpent"/>, and the
    /// creatures are joined into whichever corps can take them — the garrison first, then the hero, which is the
    /// order the reference's two <c>CanJoinTroop</c> checks use.
    /// </remarks>
    private static uint HowManyRecruitMonster(Castle castle, ArmyTroops castleArmy, ArmyTroops? guestArmy,
        BuildingType dwelling, Funds alreadySpent, Funds treasury, out Funds cost)
    {
        cost = new Funds();

        MonsterType monster = DwellingMonsters.FromDwelling(castle.Race, castle.GetActualDwelling(dwelling));

        bool castleCanJoin = castleArmy.CanJoinTroop(monster);
        bool guestCanJoin = guestArmy is not null && guestArmy.CanJoinTroop(monster);

        if (!castleCanJoin && !guestCanJoin)
        {
            return 0;
        }

        Funds unitCost = MonsterStatistics.GetGeneralStats(monster).GetCost();
        uint count = castle.GetMonstersInDwelling(dwelling);

        while (count > 0)
        {
            Funds payment = unitCost * count;

            if (treasury >= payment + alreadySpent)
            {
                cost = payment;

                break;
            }

            --count;
        }

        if (count == 0)
        {
            return 0;
        }

        if (castleCanJoin)
        {
            castleArmy.JoinTroop(monster, count, false);
        }
        else
        {
            guestArmy!.JoinTroop(monster, count, false);
        }

        return count;
    }
}
