using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using ArmyTroops = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Castle;

/// <summary>
/// What the town is worth and what it would cost an enemy to take: <c>castle.cpp:643-729</c> (the recruiting
/// estimate), <c>castle.cpp:976-1002</c> (may a hero be hired) and <c>castle.cpp:2065-2123</c> (the garrison).
/// </summary>
/// <remarks>
/// <para>
/// These four are the castle's contribution to the AI's opinion of the map. The reference reads three globals to
/// answer them — <c>world</c> for the hero standing in the town, <c>GetKingdom()</c> for the treasury and
/// <c>PaymentConditions</c> for the prices — and the port takes the first two as arguments, the same substitution
/// the rest of <see cref="Castle"/> makes.
/// </para>
/// <para>
/// <b>What is missing</b>: the cost of <c>ActionNewMonth</c>'s castle-upkeep and tax terms, which need a kingdom round
/// to spend against. It is called out at the method that needs it.
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>
    /// The army that defends the town, <c>Castle::GetActualArmy()</c> (<c>castle.cpp:2065-2075</c>): the garrison
    /// unless a hero is standing in the town, in which case his.
    /// </summary>
    /// <param name="guestHero">The hero in the town, or <see langword="null"/> for none.</param>
    /// <returns>The army the town fights with.</returns>
    public ArmyTroops GetActualArmy(Hero? guestHero) => guestHero?.Army ?? _army;

    /// <summary>
    /// The strength of the army defending the town, <c>Castle::GetGarrisonStrength()</c>
    /// (<c>castle.cpp:2077-2123</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The castle's defensive value: the army, then the multipliers every besieging army has to beat — the moat and
    /// the walls, with a heavier penalty for an attacker who has no ballistics and leads melee troops. The two
    /// numbers the reference multiplies by (1.15/1.2 and 1.25/1.45) are what makes a castle worth more than its
    /// garrison, which is the whole point of <c>CheckBuyBuilding</c> allowing the walls to be bought.
    /// </para>
    /// <para>
    /// A hero in the town is merged with the garrison first, exactly as <see cref="ActionPreBattle"/> merges it
    /// before a real battle: the garrison stacks move into the hero's free slots, and the combined army answers.
    /// The merge works on a copy, so neither the hero's army nor the garrison is disturbed.
    /// </para>
    /// <para>
    /// The reference's merge asks the combined army for <c>isControlAI()</c>, i.e. for the guest hero's control: an
    /// AI hero squeezes in as many garrison stacks as will fit, a human one only moves them onto matching slots and
    /// may leave some behind. The port has no player object, so <paramref name="guestIsControlAi"/> carries the
    /// answer. It defaults to <see langword="true"/> — the conservative side for the only caller this exists for,
    /// the AI sizing up a target, where the larger of the two numbers is the safe one.
    /// </para>
    /// <para>
    /// The keep's archers are added to the army before the multipliers: a whole <c>towerStr</c> for the keep plus half
    /// of it for each turret that was built. The reference asks a real <c>Battle::Tower</c> for that number, so the
    /// port does too, with the count and the mage-guild attack bonus the tower would have in a battle.
    /// </para>
    /// </remarks>
    /// <param name="guestHero">The hero in the town, or <see langword="null"/> for none.</param>
    /// <param name="attackingHero">The hero who would attack, or <see langword="null"/> when unknown.</param>
    /// <param name="guestIsControlAi">
    /// Whether <paramref name="guestHero"/> is an AI hero, which decides how the garrison is merged.
    /// </param>
    /// <returns>The strength the attacker has to beat; zero for an empty town.</returns>
    public double GetGarrisonStrength(
        Hero? guestHero = null,
        Hero? attackingHero = null,
        bool guestIsControlAi = true)
    {
        double totalStrength;

        if (guestHero is not null)
        {
            // The merge needs the hero as the commander of the combined army (the reference constructs it that
            // way), and it needs a copy of the garrison because the merge empties what it moves.
            ArmyTroops garrisonArmy = new(Race);
            garrisonArmy.Assign(_army.Slots);

            ArmyTroops combinedArmy = new(Race) { Commander = guestHero };
            combinedArmy.Assign(guestHero.Army.Slots);
            combinedArmy.ArrangeForCastleDefense(garrisonArmy, guestIsControlAi);

            totalStrength = combinedArmy.GetStrength();
        }
        else
        {
            totalStrength = _army.GetStrength();
        }

        if (IsCastle && totalStrength > 0.1)
        {
            // The keep's archers are worth a full tower, and each turret half of that again. Only the center tower is
            // asked for, so all three get the same count and the same mage-guild attack bonus.
            BattleTower centerTower = new(this, BattleTowerType.Center, 0, guestHero);
            double towerStrength = centerTower.GetStrengthWithBonus((int)centerTower.AttackBonus, 0);

            totalStrength += towerStrength;

            if (IsBuild(BuildingType.LeftTurret))
            {
                totalStrength += towerStrength / 2;
            }

            if (IsBuild(BuildingType.RightTurret))
            {
                totalStrength += towerStrength / 2;
            }

            bool isWeakAgainstMissileFire = attackingHero is not null
                && attackingHero.GetLevelSkill(SecondarySkill.Ballistics) == SkillLevel.None
                && attackingHero.Army.IsMeleeDominantArmy();

            totalStrength *= isWeakAgainstMissileFire
                ? IsBuild(BuildingType.Moat) ? 1.45 : 1.25
                : IsBuild(BuildingType.Moat) ? 1.2 : 1.15;
        }

        return totalStrength;
    }

    /// <summary>
    /// The army the town could put together with a given budget, <c>Castle::getAvailableArmy()</c>
    /// (<c>castle.cpp:643-662</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The garrison plus everything the money would buy, one dwelling at a time from the sixth down to the first.
    /// The order matters: the expensive dwellings are bought first, so a budget that runs out leaves the cheap
    /// creatures unbought — the reference's own bias, and one an AI that prefers strong stacks would have anyway.
    /// </para>
    /// <para>
    /// The budget is passed in rather than read from the kingdom for the reason
    /// <see cref="GetRecruitLimit"/> gives: the reference spends a local copy as it goes, and a method that read
    /// <c>GetKingdom().GetFunds()</c> would spend the same money twice.
    /// </para>
    /// </remarks>
    /// <param name="potentialBudget">What may be spent, reduced by every stack added.</param>
    /// <returns>A new army: the garrison's stacks plus the ones the budget buys.</returns>
    public ArmyTroops GetAvailableArmy(Funds potentialBudget)
    {
        ArmyTroops reinforcement = new(Race);
        reinforcement.Assign(_army.Slots);

        // The reference walks the bitmask downwards, DWELLING_MONSTER6 to DWELLING_MONSTER1, which is also the order
        // the budget is spent in.
        for (uint bits = (uint)BuildingType.DwellingMonster6;
             bits >= (uint)BuildingType.DwellingMonster1;
             bits >>= 1)
        {
            BuildingType dwelling = (BuildingType)bits;

            if (!IsBuild(dwelling))
            {
                continue;
            }

            MonsterType monster = DwellingMonsters.FromDwelling(Race, GetActualDwelling(dwelling));
            uint available = GetMonstersInDwelling(dwelling);

            uint couldRecruit = unchecked((uint)potentialBudget.GetLowestQuotient(MonsterStatistics.GetGeneralStats(monster).GetCost()));

            if (available < couldRecruit)
            {
                couldRecruit = available;
            }

            if (couldRecruit > 0)
            {
                potentialBudget -= MonsterStatistics.GetGeneralStats(monster).GetCost() * couldRecruit;
                reinforcement.JoinTroop(monster, couldRecruit);
            }
        }

        return reinforcement;
    }

    /// <summary>
    /// What an enemy would have to fear from the town, <c>Castle::getArmyRecruitmentValue()</c>
    /// (<c>castle.cpp:669-672</c>): the strength of everything it could still buy.
    /// </summary>
    /// <param name="kingdomFunds">The owner's treasury, <c>GetKingdom().GetFunds()</c>.</param>
    /// <returns>The strength of <see cref="GetAvailableArmy"/> with that budget.</returns>
    public double GetArmyRecruitmentValue(in Funds kingdomFunds) => GetAvailableArmy(kingdomFunds).GetStrength();

    /// <summary>
    /// What a hero would gain by visiting the town, <c>Castle::getVisitValue()</c>
    /// (<c>castle.cpp:674-729</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three gains, added up: the spells the guild would teach him (and the spell book he would have to buy first,
    /// worth a flat 50 on top), the upgrades the town's dwellings would allow, and the creatures the money left over
    /// would buy. The money is spent in that order, which is why the upgrades are checked against what is left after
    /// the book.
    /// </para>
    /// <para>
    /// <b>One deliberate difference.</b> The reference copies the hero's army with <c>Troops futureArmy( heroArmy )</c>,
    /// a copy of the pointers, and then calls <c>Troop::Upgrade()</c> on them — which upgrades the hero's own troops
    /// as a side effect of evaluating a visit. The port copies the stacks instead
    /// (<see cref="ArmyTroops.Assign"/> by value), so looking at a town does not upgrade an army. The returned
    /// number is the same; only the side effect is gone.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero who would visit, <c>getVisitValue( hero )</c>.</param>
    /// <param name="kingdomFunds">The treasury of the hero's kingdom, <c>GetKingdom().GetFunds()</c>.</param>
    /// <returns>The strength the visit would add, never negative.</returns>
    public double GetVisitValue(Hero hero, in Funds kingdomFunds)
    {
        ArmyTroops futureArmy = new(hero.Army.Race);
        futureArmy.Assign(hero.Army.Slots);

        double heroArmyStrength = futureArmy.GetStrength();

        Funds potentialFunds = kingdomFunds;
        double spellValue = 0;

        if (GetLevelMageGuild() > 0)
        {
            int spellPower = hero.GetPower();
            SpellStorage guildSpells = _mageGuild.GetSpells(GetLevelMageGuild(), IsLibraryBuilt);

            foreach (Spell spell in guildSpells)
            {
                if (hero.CanLearnSpell(spell) && !hero.HasSpell(spell, true))
                {
                    spellValue += spell.GetStrategicValue(heroArmyStrength, hero.GetMaxSpellPoints(), spellPower);
                }
            }

            if (!hero.HasSpellBook && spellValue > 0)
            {
                Funds payment = PaymentConditions.BuySpellBook();

                if (potentialFunds < payment || hero.BagArtifacts.IsFull)
                {
                    // Without a book the spells are worthless, and a full bag means he cannot buy one.
                    spellValue = 0;
                }
                else
                {
                    potentialFunds -= payment;
                    spellValue += 50;
                }
            }
        }

        foreach (Troop troop in futureArmy)
        {
            if (!troop.IsValid || !troop.IsAllowUpgrade || Race != troop.GetRace())
            {
                continue;
            }

            if (!IsBuild(DwellingMonsters.GetDwelling(troop.GetUpgrade())))
            {
                continue;
            }

            Funds payment = troop.GetTotalUpgradeCost();

            if (potentialFunds >= payment)
            {
                potentialFunds -= payment;
                troop.Upgrade();
            }
        }

        double upgradeStrength = futureArmy.GetStrength() - heroArmyStrength;

        return spellValue + upgradeStrength + futureArmy.GetReinforcementValue(GetAvailableArmy(potentialFunds));
    }

    /// <summary>
    /// Whether a hero may be hired here, <c>Castle::AllowBuyHero()</c> (<c>castle.cpp:976-1002</c>).
    /// </summary>
    /// <remarks>
    /// Two refusals: a hero is already in the town, or the kingdom may not take another. The reference's second
    /// check is the pairing of <c>Kingdom::AllowRecruitHero( false )</c> (the limit) and
    /// <c>AllowRecruitHero( true )</c> (the price) — the port keeps them separate for the same reason, so that a
    /// full kingdom is told it is full before it is told it is poor.
    /// <para>
    /// The explanation the reference writes into <c>msg</c> is translated text and is not ported.
    /// </para>
    /// </remarks>
    /// <param name="guestHero">The hero in the town, or <see langword="null"/> for none.</param>
    /// <param name="kingdom">The owner's kingdom.</param>
    /// <returns>Whether a hero may be hired.</returns>
    public bool AllowBuyHero(Hero? guestHero, Kingdom.Kingdom kingdom)
    {
        if (guestHero is not null)
        {
            return false;
        }

        return kingdom.AllowRecruitHero(false) && kingdom.AllowRecruitHero(true);
    }
}
