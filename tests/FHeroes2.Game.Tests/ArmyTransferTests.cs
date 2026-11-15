using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the two-army operations: <c>Troops::JoinStrongest</c> (<c>army.cpp:702-832</c>),
/// <c>Army::JoinStrongestFromArmy</c>/<c>MoveTroops</c>/<c>SwapTroops</c> (<c>army.cpp:1426-1540</c>),
/// the troop swap of <c>army.cpp:1753</c> and <c>Army::SaveLastTroop</c> (<c>army.cpp:1758</c>).
/// </summary>
/// <remarks>
/// Besides its own point, every test here checks that troops are conserved: these methods move, merge or hand
/// back a stack, but must never make creatures appear or vanish. The strongest-five rule and the hero's last
/// stack are the two places where a port typically loses them.
/// </remarks>
public sealed class ArmyTransferTests
{
    private static ArmyStack NewArmy() => new();

    private static ArmyStack NewHeroArmy() => new() { Commander = new Hero(1, MapRace.Knight) };

    private static uint CountIn(ArmyStack left, ArmyStack right, MonsterType monster) =>
        left.GetCountMonsters(monster) + right.GetCountMonsters(monster);

    [Fact]
    public void TheSameSlotTakesAStackOrMergesIt()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 5);

        ArmyStack giver = NewArmy();
        giver.JoinTroop(MonsterType.Peasant, 7);
        giver.JoinTroop(MonsterType.Archer, 3);

        taker.JoinStrongest(giver, false);

        // Slot 0 held a peasant on both sides, so they merged; slot 1 was empty on the taker's side, so the
        // archers simply moved over.
        Assert.Equal(MonsterType.Peasant, taker.Slots[0].Monster);
        Assert.Equal(12u, taker.Slots[0].Count);
        Assert.Equal(MonsterType.Archer, taker.Slots[1].Monster);
        Assert.Equal(3u, taker.Slots[1].Count);
        Assert.Equal(0u, giver.OccupiedSlotCount);
    }

    [Fact]
    public void AStackInADifferentSlotIsMovedNotDropped()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 5);

        ArmyStack giver = NewArmy();
        giver.JoinTroop(MonsterType.Archer, 3);

        taker.JoinStrongest(giver, false);

        // The giver's stack sits in slot 0, where the taker has a peasant, so nothing merges and the archers
        // go to the first free slot instead of being lost.
        Assert.Equal(5u, taker.Slots[0].Count);
        Assert.Equal(MonsterType.Archer, taker.Slots[1].Monster);
        Assert.Equal(0u, giver.OccupiedSlotCount);
    }

    [Fact]
    public void SevenStacksBecomeFiveStrongestOnes()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 100);
        taker.JoinTroop(MonsterType.Archer, 100);
        taker.JoinTroop(MonsterType.Pikeman, 100);
        taker.JoinTroop(MonsterType.Swordsman, 100);

        ArmyStack giver = NewArmy();
        giver.JoinTroop(MonsterType.GreenDragon, 1);
        giver.JoinTroop(MonsterType.Mage, 10);
        giver.JoinTroop(MonsterType.Cavalry, 10);

        MonsterType[] monsters =
        [
            MonsterType.Peasant, MonsterType.Archer, MonsterType.Pikeman, MonsterType.Swordsman,
            MonsterType.GreenDragon, MonsterType.Mage, MonsterType.Cavalry
        ];

        Dictionary<MonsterType, uint> before = [];

        foreach (MonsterType monster in monsters)
        {
            before[monster] = CountIn(taker, giver, monster);
        }

        taker.JoinStrongest(giver, false);

        Assert.Equal(5u, taker.OccupiedSlotCount);
        Assert.Equal(2u, giver.OccupiedSlotCount);

        foreach (MonsterType monster in monsters)
        {
            Assert.Equal(before[monster], CountIn(taker, giver, monster));
        }

        // "The strongest five stay", so the weakest stack that stayed is not weaker than the strongest stack
        // that was handed back.
        Assert.True(
            taker.GetWeakestTroop()!.GetStrength() >= giver.GetStrongestTroop()!.GetStrength(),
            "a stack that was handed back is stronger than one that stayed");
    }

    [Fact]
    public void TheHeroGiverKeepsAShareOfItsLastStack()
    {
        ArmyStack taker = NewArmy();

        ArmyStack giver = NewHeroArmy();
        giver.JoinTroop(MonsterType.Peasant, 100);

        taker.JoinStrongestFromArmy(giver);

        // The whole army is one stack, so it is worth more than a twentieth of the joined army and only a
        // twentieth is handed back: a hundred peasants in, five back, ninety-five kept.
        Assert.Equal(95u, taker.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(5u, giver.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, giver.OccupiedSlotCount);
    }

    [Fact]
    public void AWeakStackIsHandedBackWhole()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.GreenDragon, 10);

        ArmyStack giver = NewHeroArmy();
        giver.JoinTroop(MonsterType.Peasant, 1);

        taker.JoinStrongestFromArmy(giver);

        // One peasant among ten dragons is far below the twentieth-of-the-army limit, so it goes back as it is
        // rather than being split.
        Assert.Equal(1u, giver.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(10u, taker.GetCountMonsters(MonsterType.GreenDragon));
        Assert.Equal(1u, taker.OccupiedSlotCount);
    }

    [Fact]
    public void AGarrisonGiverMayBeEmptied()
    {
        ArmyStack taker = NewArmy();

        ArmyStack giver = NewArmy();
        giver.JoinTroop(MonsterType.Peasant, 100);

        taker.JoinStrongestFromArmy(giver);

        // No commander: the giver is a garrison and has nothing to lose by being emptied.
        Assert.Equal(100u, taker.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(0u, giver.OccupiedSlotCount);
    }

    [Fact]
    public void ASingleCreatureOfAHeroIsNotSplitInTwo()
    {
        // The reference chain: a twentieth of a one-creature army is nothing, floored to one, capped at half of
        // the stack which is zero, floored to one again. So the creature goes back to the hero and the taker is
        // left empty — the flag protects the giver, not the taker.
        ArmyStack taker = NewArmy();

        ArmyStack giver = NewHeroArmy();
        giver.JoinTroop(MonsterType.Peasant, 1);

        taker.JoinStrongestFromArmy(giver);

        Assert.Equal(0u, taker.OccupiedSlotCount);
        Assert.Equal(1u, giver.GetCountMonsters(MonsterType.Peasant));
    }

    [Fact]
    public void JoiningAnArmyWithItselfDoesNothing()
    {
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Peasant, 5);

        army.JoinStrongest(army, true);

        Assert.Equal(5u, army.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, army.OccupiedSlotCount);
    }

    [Fact]
    public void AStrongerGiverStackReplacesOurWeakest()
    {
        // The taker is full of scraps, so nothing can be moved — only the swap of step 3 can get the dragon in,
        // and the scrap it displaces has to be handed back rather than vanish.
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 1);
        taker.JoinTroop(MonsterType.Archer, 1);
        taker.JoinTroop(MonsterType.Pikeman, 1);
        taker.JoinTroop(MonsterType.Swordsman, 1);
        taker.JoinTroop(MonsterType.Cavalry, 1);

        ArmyStack giver = NewArmy();
        giver.JoinTroop(MonsterType.GreenDragon, 10);

        taker.JoinStrongest(giver, false);

        Assert.Equal(5u, taker.OccupiedSlotCount);
        Assert.Equal(10u, taker.GetCountMonsters(MonsterType.GreenDragon));
        Assert.Equal(0u, giver.GetCountMonsters(MonsterType.GreenDragon));
        Assert.Equal(1u, giver.OccupiedSlotCount);
    }

    [Fact]
    public void MovingTroopsTakesEverythingThatFits()
    {
        ArmyStack taker = NewArmy();

        ArmyStack from = NewArmy();
        from.JoinTroop(MonsterType.Peasant, 5);
        from.JoinTroop(MonsterType.Archer, 3);

        taker.MoveTroops(from);

        Assert.Equal(5u, taker.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(3u, taker.GetCountMonsters(MonsterType.Archer));
        Assert.Equal(0u, from.OccupiedSlotCount);
    }

    [Fact]
    public void MovingTroopsLeavesAHerosLastStackWithOneCreature()
    {
        ArmyStack taker = NewArmy();

        ArmyStack from = NewHeroArmy();
        from.JoinTroop(MonsterType.Peasant, 10);

        taker.MoveTroops(from);

        // A hero who gives away his last stack loses the game, so the last one keeps a single creature — the
        // reference's "all but one" rule, note the odd spelling of "all but one" it produces for a stack of one.
        Assert.Equal(9u, taker.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, from.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, from.OccupiedSlotCount);
    }

    [Fact]
    public void MovingTroopsMergesTheReceiversDuplicatesFirstToMakeRoom()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 5);
        taker.JoinTroop(MonsterType.Peasant, 5);
        taker.JoinTroop(MonsterType.Archer, 5);
        taker.JoinTroop(MonsterType.Pikeman, 5);
        taker.JoinTroop(MonsterType.Swordsman, 5);

        Assert.Equal(5u, taker.OccupiedSlotCount);

        ArmyStack from = NewArmy();
        from.JoinTroop(MonsterType.GreenDragon, 1);

        taker.MoveTroops(from);

        // The two peasant stacks become one, and the freed slot takes the dragon — otherwise the dragon would
        // have had nowhere to go even though the army held only four kinds of creatures.
        Assert.Equal(5u, taker.OccupiedSlotCount);
        Assert.Equal(10u, taker.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, taker.GetCountMonsters(MonsterType.GreenDragon));
        Assert.Equal(0u, from.OccupiedSlotCount);
    }

    [Fact]
    public void MovingTroopsStopsWhenTheReceiverIsFull()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 5);
        taker.JoinTroop(MonsterType.Archer, 5);
        taker.JoinTroop(MonsterType.Pikeman, 5);
        taker.JoinTroop(MonsterType.Swordsman, 5);
        taker.JoinTroop(MonsterType.Cavalry, 5);

        ArmyStack from = NewArmy();
        from.JoinTroop(MonsterType.GreenDragon, 1);

        taker.MoveTroops(from);

        Assert.Equal(0u, taker.GetCountMonsters(MonsterType.GreenDragon));
        Assert.Equal(1u, from.GetCountMonsters(MonsterType.GreenDragon));
    }

    [Fact]
    public void TheMonsterToKeepIsExchangedWithoutACreatureChangingSides()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 5);
        taker.JoinTroop(MonsterType.Archer, 5);
        taker.JoinTroop(MonsterType.Pikeman, 5);
        taker.JoinTroop(MonsterType.Swordsman, 5);
        taker.JoinTroop(MonsterType.Cavalry, 5);

        ArmyStack from = NewArmy();
        from.JoinTroop(MonsterType.GreenDragon, 1);

        taker.MoveTroops(from, MonsterType.Peasant);

        // The receiver is full, so the only way to move anything is the exchange the reference does first: the
        // peasant stack leaves the taker for the source, the dragon takes its slot. Then the second pass finds
        // nothing that fits and the peasant stays where the castle asked it to stay.
        Assert.Equal(5u, taker.OccupiedSlotCount);
        Assert.Equal(1u, taker.GetCountMonsters(MonsterType.GreenDragon));
        Assert.Equal(0u, taker.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(5u, from.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, from.OccupiedSlotCount);
    }

    [Fact]
    public void TheMonsterToKeepIsMovedOnceThereIsRoom()
    {
        ArmyStack taker = NewArmy();
        taker.JoinTroop(MonsterType.Peasant, 5);

        ArmyStack from = NewArmy();
        from.JoinTroop(MonsterType.GreenDragon, 1);

        taker.MoveTroops(from, MonsterType.Peasant);

        // The exchange runs even though there is room: keeping the monster is a wish, not a promise. The
        // peasant goes to the source first, then the second pass brings it back into a free slot.
        Assert.Equal(5u, taker.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, taker.GetCountMonsters(MonsterType.GreenDragon));
        Assert.Equal(0u, from.OccupiedSlotCount);
    }

    [Fact]
    public void MovingTroopsFromItselfIsRefused()
    {
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Peasant, 5);

        Assert.Throws<ArgumentException>(() => army.MoveTroops(army));
    }

    [Fact]
    public void SwappingArmiesSwapsTheStacksAndNothingElse()
    {
        ArmyStack left = new(MapRace.Knight);
        left.JoinTroop(MonsterType.Peasant, 5);

        ArmyStack right = new(MapRace.Wizard);
        right.JoinTroop(MonsterType.Archer, 3);
        right.JoinTroop(MonsterType.Swordsman, 2);

        left.SwapTroops(right);

        // The reference's Assign copies the five stacks, so the race, the colour and the commander stay put.
        Assert.Equal(MapRace.Knight, left.Race);
        Assert.Equal(MapRace.Wizard, right.Race);

        Assert.Equal(2u, left.OccupiedSlotCount);
        Assert.Equal(3u, left.GetCountMonsters(MonsterType.Archer));
        Assert.Equal(2u, left.GetCountMonsters(MonsterType.Swordsman));
        Assert.Equal(0u, left.GetCountMonsters(MonsterType.Peasant));

        Assert.Equal(1u, right.OccupiedSlotCount);
        Assert.Equal(5u, right.GetCountMonsters(MonsterType.Peasant));
    }

    [Fact]
    public void SwappingTwoStacksExchangesBothFields()
    {
        Troop left = new(MonsterType.Peasant, 5);
        Troop right = new(MonsterType.Archer, 3);

        ArmyStack.SwapTroops(left, right);

        Assert.Equal(MonsterType.Archer, left.Monster);
        Assert.Equal(3u, left.Count);
        Assert.Equal(MonsterType.Peasant, right.Monster);
        Assert.Equal(5u, right.Count);
    }

    [Fact]
    public void OnlyAHeroWithOneStackSavesIt()
    {
        ArmyStack army = NewArmy();

        Assert.False(army.SaveLastTroop());

        army.JoinTroop(MonsterType.Peasant, 5);
        Assert.False(army.SaveLastTroop());

        army.Commander = new Hero(1, MapRace.Knight);
        Assert.True(army.SaveLastTroop());

        army.JoinTroop(MonsterType.Archer, 5);
        Assert.False(army.SaveLastTroop());

        // A garrison of one stack is not a hero's last stack.
        army.Slots[1].Reset();
        army.Commander = null;
        Assert.False(army.SaveLastTroop());
    }
}
