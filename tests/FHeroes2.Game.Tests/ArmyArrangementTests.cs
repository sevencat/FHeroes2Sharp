using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using Xunit;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the shape of an army the player does not fill himself, <c>army.cpp:1040-1192</c>
/// (<c>Army::setFromTile</c>) and <c>army.cpp:1783-1975</c> (the three <c>ArrangeFor*</c> methods).
/// </summary>
/// <remarks>
/// The objects with hand-written guardians are checked by their exact slots, because the slot order is part of
/// the design: the centre stack is the one <c>ArrangeForBattle</c> may upgrade. The generic branches are
/// checked through the totals and the stack count, which is all a seeded generator allows.
/// </remarks>
public sealed class ArmyArrangementTests
{
    [Fact]
    public void APyramidHoldsTenVampireLordsAroundThreeStacksOfMummies()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.Pyramid, world));

        AssertSlots(army,
            (MonsterType.VampireLord, 10),
            (MonsterType.RoyalMummy, 10),
            (MonsterType.RoyalMummy, 10),
            (MonsterType.RoyalMummy, 10),
            (MonsterType.VampireLord, 10));
        Assert.Equal(PlayerColor.None, army.Color);
    }

    [Fact]
    public void AGraveyardIsFiveStacksOfTwentyZombies()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.Graveyard, world));

        AssertSlots(army,
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20));
    }

    [Fact]
    public void TheCityOfTheDeadHoldsItsFiveNamedStacks()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.CityOfDead, world));

        AssertSlots(army,
            (MonsterType.Zombie, 20),
            (MonsterType.VampireLord, 5),
            (MonsterType.PowerLich, 5),
            (MonsterType.VampireLord, 5),
            (MonsterType.Zombie, 20));
    }

    [Fact]
    public void TheTrollBridgeAlternatesTheTwoTrollKinds()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.TrollBridge, world));

        AssertSlots(army,
            (MonsterType.Troll, 4),
            (MonsterType.WarTroll, 4),
            (MonsterType.Troll, 4),
            (MonsterType.WarTroll, 4),
            (MonsterType.Troll, 4));
    }

    [Fact]
    public void TheDragonCityIsThreeGreenDragonsOneRedOneBlack()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.DragonCity, world));

        AssertSlots(army,
            (MonsterType.GreenDragon, 1),
            (MonsterType.GreenDragon, 1),
            (MonsterType.GreenDragon, 1),
            (MonsterType.RedDragon, 1),
            (MonsterType.BlackDragon, 1));
    }

    [Fact]
    public void TheDaemonCaveUsesFourSlotsAndLeavesTheLastOneEmpty()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.DaemonCave, world));

        // The only object of the switch that does not fill all five slots.
        AssertSlots(army,
            (MonsterType.EarthElement, 2),
            (MonsterType.EarthElement, 2),
            (MonsterType.EarthElement, 2),
            (MonsterType.EarthElement, 2));
    }

    [Fact]
    public void TheDerelictShipSpreadsTwoHundredSkeletonsOverItsStacks()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.DerelictShip, world));

        Assert.InRange(army.OccupiedSlotCount, 3u, 5u);
        Assert.Equal(200u, army.GetCountMonsters(MonsterType.Skeleton));
    }

    [Fact]
    public void AWanderingStackIsSpreadOverThreeToFiveStacks()
    {
        World world = MakeWorld();
        Tile tile = MakeTile(Mp2ObjectType.None, world);

        tile.SetMonster(world, MonsterType.Goblin, 20);

        ArmyStack army = new();
        army.SetFromTile(world, tile);

        Assert.InRange(army.OccupiedSlotCount, 3u, 5u);
        Assert.Equal(20u, army.GetCountMonsters(MonsterType.Goblin));
    }

    [Fact]
    public void SetFromTileStartsFromAnEmptyArmy()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.SetFromTile(world, MakeTile(Mp2ObjectType.DaemonCave, world));
        army.SetFromTile(world, MakeTile(Mp2ObjectType.Graveyard, world));

        AssertSlots(army,
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20),
            (MonsterType.MutantZombie, 20));
    }

    [Fact]
    public void TenOverTwoStacksSitsInTheMiddle()
    {
        ArmyStack army = new();

        army.ArrangeForBattle(MonsterType.Peasant, 10, 2);

        // (5 - 2) / 2 = 1 empty slot on each side.
        Assert.False(army.GetTroop(0).IsValid);
        Assert.Equal(5u, army.GetTroop(1).Count);
        Assert.Equal(5u, army.GetTroop(2).Count);
        Assert.False(army.GetTroop(3).IsValid);
    }

    [Fact]
    public void SevenOverThreeStacksPutsTheRemainderLeft()
    {
        ArmyStack army = new();

        army.ArrangeForBattle(MonsterType.Peasant, 7, 3);

        Assert.False(army.GetTroop(0).IsValid);
        Assert.Equal(3u, army.GetTroop(1).Count);
        Assert.Equal(2u, army.GetTroop(2).Count);
        Assert.Equal(2u, army.GetTroop(3).Count);
        Assert.False(army.GetTroop(4).IsValid);
    }

    [Fact]
    public void TwentyOverFiveStacksFillsEverySlot()
    {
        ArmyStack army = new();

        army.ArrangeForBattle(MonsterType.Peasant, 20, 5);

        AssertSlots(army,
            (MonsterType.Peasant, 4),
            (MonsterType.Peasant, 4),
            (MonsterType.Peasant, 4),
            (MonsterType.Peasant, 4),
            (MonsterType.Peasant, 4));
    }

    [Fact]
    public void TwoOverFiveStacksUsesOnlyTwoOfThem()
    {
        ArmyStack army = new();

        army.ArrangeForBattle(MonsterType.Peasant, 2, 5);

        // The reference walks the stack count down until the quotient is positive, so no stack is empty.
        Assert.Equal(1u, army.GetTroop(0).Count);
        Assert.Equal(1u, army.GetTroop(1).Count);
        Assert.Equal(2u, army.OccupiedSlotCount);
    }

    [Fact]
    public void ArchersAlwaysTakeAllFiveStacks()
    {
        World world = MakeWorld();
        ArmyStack army = new();

        army.ArrangeForBattle(MonsterType.Archer, 10, 7, false, world);

        AssertSlots(army,
            (MonsterType.Archer, 2),
            (MonsterType.Archer, 2),
            (MonsterType.Archer, 2),
            (MonsterType.Archer, 2),
            (MonsterType.Archer, 2));
    }

    [Fact]
    public void TheSameTileAlwaysHoldsTheSameGuardians()
    {
        World world = MakeWorld();
        ArmyStack first = new();
        ArmyStack second = new();

        first.ArrangeForBattle(MonsterType.Goblin, 20, 42, true, world);
        second.ArrangeForBattle(MonsterType.Goblin, 20, 42, true, world);

        for (int slot = 0; slot < ArmyStack.MaximumTroopCount; ++slot)
        {
            Assert.Equal(first.GetTroop(slot).Monster, second.GetTroop(slot).Monster);
            Assert.Equal(first.GetTroop(slot).Count, second.GetTroop(slot).Count);
        }
    }

    [Fact]
    public void OnlyTheCentreStackMayBeUpgraded()
    {
        World world = MakeWorld();
        bool sawUpgrade = false;
        bool sawNoUpgrade = false;

        for (int tileIndex = 0; tileIndex < 64; ++tileIndex)
        {
            ArmyStack army = new();

            army.ArrangeForBattle(MonsterType.Archer, 10, tileIndex, true, world);

            for (int slot = 0; slot < ArmyStack.MaximumTroopCount; ++slot)
            {
                MonsterType monster = army.GetTroop(slot).Monster;

                if (slot == ArmyStack.MaximumTroopCount / 2)
                {
                    Assert.True(monster is MonsterType.Archer or MonsterType.Ranger);
                }
                else
                {
                    Assert.Equal(MonsterType.Archer, monster);
                }
            }

            if (army.GetTroop(ArmyStack.MaximumTroopCount / 2).Monster == MonsterType.Ranger)
            {
                sawUpgrade = true;
            }
            else
            {
                sawNoUpgrade = true;
            }
        }

        // Fifty percent per tile, so sixty-four tiles must show both outcomes.
        Assert.True(sawUpgrade);
        Assert.True(sawNoUpgrade);
    }

    [Fact]
    public void NoUpgradeIsAllowedWhenTheCallerSaysSo()
    {
        World world = MakeWorld();

        for (int tileIndex = 0; tileIndex < 64; ++tileIndex)
        {
            ArmyStack army = new();

            army.ArrangeForBattle(MonsterType.Archer, 10, tileIndex, false, world);

            Assert.Equal(MonsterType.Archer, army.GetTroop(ArmyStack.MaximumTroopCount / 2).Monster);
        }
    }

    [Fact]
    public void TheWhirlpoolMergesThenTakesOneCreatureOutOfTheWeakestStack()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 10);
        army.GetTroop(1).Set(MonsterType.Peasant, 10);
        army.GetTroop(2).Set(MonsterType.Archer, 4);
        army.GetTroop(3).Set(MonsterType.GreenDragon, 1);

        army.ArrangeForWhirlpool();

        Assert.Equal(4u, army.OccupiedSlotCount);
        Assert.Equal(19u, army.GetTroop(0).Count);
        Assert.Equal(MonsterType.Archer, army.GetTroop(1).Monster);
        Assert.Equal(4u, army.GetTroop(1).Count);
        Assert.Equal(MonsterType.GreenDragon, army.GetTroop(2).Monster);
        Assert.Equal(MonsterType.Peasant, army.GetTroop(3).Monster);
        Assert.Equal(1u, army.GetTroop(3).Count);
        Assert.Equal(20u, army.GetCountMonsters(MonsterType.Peasant));
    }

    [Fact]
    public void TheWhirlpoolLeavesAFullHouseAlone()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 10);
        army.GetTroop(1).Set(MonsterType.Archer, 5);
        army.GetTroop(2).Set(MonsterType.Pikeman, 3);
        army.GetTroop(3).Set(MonsterType.GreenDragon, 1);
        army.GetTroop(4).Set(MonsterType.Titan, 1);

        army.ArrangeForWhirlpool();

        AssertSlots(army,
            (MonsterType.Peasant, 10),
            (MonsterType.Archer, 5),
            (MonsterType.Pikeman, 3),
            (MonsterType.GreenDragon, 1),
            (MonsterType.Titan, 1));
    }

    [Fact]
    public void TheWhirlpoolDoesNotEmptyAStackOfOne()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 1);
        army.GetTroop(1).Set(MonsterType.GreenDragon, 2);

        army.ArrangeForWhirlpool();

        AssertSlots(army,
            (MonsterType.Peasant, 1),
            (MonsterType.GreenDragon, 2));
    }

    [Fact]
    public void AnAiHeroTakesEveryGarrisonStackStrongestFirst()
    {
        ArmyStack army = new() { Commander = new Hero(1, MapRace.Knight) };
        ArmyStack garrison = new();

        garrison.GetTroop(0).Set(MonsterType.Peasant, 10);
        garrison.GetTroop(1).Set(MonsterType.GreenDragon, 2);

        Assert.True(army.ArrangeForCastleDefense(garrison, isControlAi: true));

        Assert.Equal(2u, army.OccupiedSlotCount);
        Assert.Equal(MonsterType.GreenDragon, army.GetTroop(0).Monster);
        Assert.Equal(2u, army.GetTroop(0).Count);
        Assert.Equal(MonsterType.Peasant, army.GetTroop(1).Monster);
        Assert.Equal(10u, army.GetTroop(1).Count);
        Assert.Equal(0u, garrison.OccupiedSlotCount);
    }

    [Fact]
    public void AnAiHeroWithAFullArmyLeavesTheGarrisonAlone()
    {
        ArmyStack army = new() { Commander = new Hero(1, MapRace.Knight) };

        for (int slot = 0; slot < ArmyStack.MaximumTroopCount; ++slot)
        {
            army.GetTroop(slot).Set((MonsterType)(slot + 2), 1);
        }

        ArmyStack garrison = new();
        garrison.GetTroop(0).Set(MonsterType.Peasant, 5);
        garrison.GetTroop(1).Set(MonsterType.Phoenix, 1);

        // No duplicate to merge, so neither stack can be squeezed in.
        Assert.False(army.ArrangeForCastleDefense(garrison, isControlAi: true));

        Assert.Equal(2u, garrison.OccupiedSlotCount);
    }

    [Fact]
    public void AHumanHeroOnlyReceivesTheStacksThatLandOnTheSameSlot()
    {
        ArmyStack army = new() { Commander = new Hero(1, MapRace.Knight) };
        army.GetTroop(0).Set(MonsterType.Peasant, 10);
        army.GetTroop(2).Set(MonsterType.Archer, 5);

        ArmyStack garrison = new();
        garrison.GetTroop(0).Set(MonsterType.Archer, 3);
        garrison.GetTroop(1).Set(MonsterType.Pikeman, 4);
        garrison.GetTroop(2).Set(MonsterType.Swordsman, 2);

        Assert.True(army.ArrangeForCastleDefense(garrison, isControlAi: false));

        // Slots 0 and 2 of the hero's army were taken, so only slot 1 moved.
        Assert.Equal(MonsterType.Peasant, army.GetTroop(0).Monster);
        Assert.Equal(10u, army.GetTroop(0).Count);
        Assert.Equal(MonsterType.Pikeman, army.GetTroop(1).Monster);
        Assert.Equal(4u, army.GetTroop(1).Count);
        Assert.Equal(MonsterType.Archer, army.GetTroop(2).Monster);
        Assert.Equal(5u, army.GetTroop(2).Count);

        Assert.Equal(3u, garrison.GetTroop(0).Count);
        Assert.False(garrison.GetTroop(1).IsValid);
        Assert.Equal(2u, garrison.GetTroop(2).Count);
    }

    [Fact]
    public void AHumanHeroEnteringAnEmptyGarrisonMovesNothing()
    {
        ArmyStack army = new() { Commander = new Hero(1, MapRace.Knight) };
        army.GetTroop(0).Set(MonsterType.Peasant, 1);

        Assert.False(army.ArrangeForCastleDefense(new ArmyStack(), isControlAi: false));
    }

    private static World MakeWorld()
    {
        World world = new();
        world.GenerateUninitializedMap(4);

        return world;
    }

    private static Tile MakeTile(Mp2ObjectType type, World world)
    {
        Tile tile = world.GetTile(5);

        tile.Index = 5;
        tile.SetMainObjectType(type);

        return tile;
    }

    private static void AssertSlots(ArmyStack army, params (MonsterType Monster, uint Count)[] expected)
    {
        for (int slot = 0; slot < expected.Length; ++slot)
        {
            Assert.True(army.GetTroop(slot).IsValid, $"Slot {slot} should hold a stack.");
            Assert.Equal(expected[slot].Monster, army.GetTroop(slot).Monster);
            Assert.Equal(expected[slot].Count, army.GetTroop(slot).Count);
        }

        for (int slot = expected.Length; slot < ArmyStack.MaximumTroopCount; ++slot)
        {
            Assert.False(army.GetTroop(slot).IsValid, $"Slot {slot} should be empty.");
        }
    }
}
