using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using CastleTown = FHeroes2.Game.Castle.Castle;
using KingdomState = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The last rules of <c>castle.cpp</c> that do not need a screen: the footprint, the owner's control, the first
/// town of the table, the two battle hooks and the garrison strength of a defended town.
/// </summary>
public sealed class CastleRulesTests
{
    [Fact]
    public void TheFootprintIsTheFiveTilesBelowTheGate()
    {
        CastleTown castle = new(44, MapRace.Knight);
        var geometry = new MapGeometry(10, 10);

        // The town stands on (4, 4).
        Assert.True(castle.IsPosition(geometry, new Point(4, 4)));
        Assert.True(castle.IsPosition(geometry, new Point(3, 4)));
        Assert.True(castle.IsPosition(geometry, new Point(5, 4)));

        // The two wings of the same row.
        Assert.True(castle.IsPosition(geometry, new Point(2, 4)));
        Assert.True(castle.IsPosition(geometry, new Point(6, 4)));

        // The row above is the roof.
        Assert.True(castle.IsPosition(geometry, new Point(3, 3)));
        Assert.True(castle.IsPosition(geometry, new Point(4, 3)));
        Assert.True(castle.IsPosition(geometry, new Point(5, 3)));

        // Everything else belongs to the map: the gate, the shadow, the corners.
        Assert.False(castle.IsPosition(geometry, new Point(4, 5)));
        Assert.False(castle.IsPosition(geometry, new Point(3, 5)));
        Assert.False(castle.IsPosition(geometry, new Point(4, 2)));
        Assert.False(castle.IsPosition(geometry, new Point(6, 3)));
        Assert.False(castle.IsPosition(geometry, new Point(7, 4)));
        Assert.False(castle.IsPosition(geometry, new Point(2, 3)));
    }

    [Fact]
    public void TheOwnersControlIsTheKingdoms()
    {
        KingdomState human = new();
        human.Init(PlayerColor.Red, GameDifficulty.Normal, isAiControlled: false);

        KingdomState ai = new();
        ai.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: true);

        Assert.False(CastleTown.IsControlAi(human));
        Assert.True(CastleTown.IsControlAi(ai));
    }

    [Fact]
    public void TheFirstTownOfAnEmptyTableIsMinusOne()
    {
        var table = new AllCastles(10);

        Assert.Equal(-1, table.FirstCastle);

        table.Add(new CastleTown(44, MapRace.Knight));

        Assert.Equal(0, table.FirstCastle);
        Assert.Single(table);
    }

    [Fact]
    public void TheGarrisonJoinsTheHeroBeforeABattle()
    {
        CastleTown castle = new(44, MapRace.Knight);
        Hero hero = new(Hero.Celia, MapRace.Knight);

        castle.Army.Assign([new Troop(MonsterType.Peasant, 10), new Troop(MonsterType.Archer, 5), new Troop(MonsterType.Pikeman, 3)]);
        hero.Army.Assign([new Troop(MonsterType.Swordsman, 20)]);

        castle.ActionPreBattle(hero, isControlAi: false);

        // The hero's first slot was taken, so the peasant stays in the garrison; the other two move to their own
        // slots in the hero's army (the human rule keeps slot numbers).
        Assert.Equal(MonsterType.Swordsman, hero.Army.GetTroop(0).Monster);
        Assert.Equal(MonsterType.Archer, hero.Army.GetTroop(1).Monster);
        Assert.Equal(MonsterType.Pikeman, hero.Army.GetTroop(2).Monster);

        Assert.Equal(MonsterType.Peasant, castle.Army.GetTroop(0).Monster);
        Assert.False(castle.Army.GetTroop(1).IsValid);
        Assert.False(castle.Army.GetTroop(2).IsValid);
    }

    [Fact]
    public void AnEmptyTownHasNothingToMerge()
    {
        CastleTown castle = new(44, MapRace.Knight);
        Hero hero = new(Hero.Celia, MapRace.Knight);

        hero.Army.Assign([new Troop(MonsterType.Swordsman, 20)]);

        castle.ActionPreBattle(hero, isControlAi: false);

        Assert.Equal(1u, hero.Army.OccupiedSlotCount);
        Assert.Equal(0u, castle.Army.OccupiedSlotCount);
    }

    [Fact]
    public void TheAiKeepsTheGarrisonToItself()
    {
        CastleTown castle = new(44, MapRace.Knight);
        Hero hero = new(Hero.Celia, MapRace.Knight);

        // The AI's pre-battle planning is a separate system; the hook must not move anything for it.
        castle.Army.Assign([new Troop(MonsterType.Peasant, 10)]);

        castle.ActionPreBattle(hero, isControlAi: true);

        Assert.Equal(1u, castle.Army.OccupiedSlotCount);
        Assert.Equal(0u, hero.Army.OccupiedSlotCount);
    }

    [Fact]
    public void ALostDefenceWipesTheGarrison()
    {
        CastleTown castle = new(44, MapRace.Knight);
        castle.Army.Assign([new Troop(MonsterType.Peasant, 10), new Troop(MonsterType.Archer, 5)]);

        castle.ActionAfterBattle(attackerWins: false);

        Assert.Equal(2u, castle.Army.OccupiedSlotCount);

        castle.ActionAfterBattle(attackerWins: true);

        Assert.Equal(0u, castle.Army.OccupiedSlotCount);
        Assert.False(castle.HasCustomArmy);
    }

    [Fact]
    public void TheStrengthOfADefendedTownIncludesTheHeroAndTheGarrison()
    {
        CastleTown castle = new(44, MapRace.Knight);
        Hero hero = new(Hero.Celia, MapRace.Knight);

        castle.Army.Assign([new Troop(MonsterType.Peasant, 10)]);
        hero.Army.Assign([new Troop(MonsterType.Swordsman, 20)]);

        double alone = hero.Army.GetStrength();
        double combined = castle.GetGarrisonStrength(hero);

        Assert.True(combined > alone, "the garrison adds to the strength of the town's defender");

        // The merge works on copies: the hero keeps his army and the town keeps its garrison.
        Assert.Equal(1u, hero.Army.OccupiedSlotCount);
        Assert.Equal(1u, castle.Army.OccupiedSlotCount);
    }

    [Fact]
    public void AHumanDefenderMayLeaveTheGarrisonBehind()
    {
        CastleTown castle = new(44, MapRace.Knight);
        Hero hero = new(Hero.Celia, MapRace.Knight);

        // The hero's first slot is taken, so the matching garrison stack cannot join him.
        hero.Army.Assign([new Troop(MonsterType.Swordsman, 20)]);
        castle.Army.Assign([new Troop(MonsterType.Archer, 50), new Troop(MonsterType.Pikeman, 5)]);

        double ai = castle.GetGarrisonStrength(hero);
        double human = castle.GetGarrisonStrength(hero, guestIsControlAi: false);

        Assert.True(ai > human, "the AI squeezes in the stack the human leaves at home");
    }
}
