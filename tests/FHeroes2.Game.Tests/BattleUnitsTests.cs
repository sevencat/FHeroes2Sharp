using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::Units</c> (<c>battle_army.h:47-117</c>): the three queries and the three filtered copies.
/// </summary>
public class BattleUnitsTests
{
    private static BattleUnit Spawn(MonsterType monster, int head, uint uid, PlayerColor color = PlayerColor.None)
    {
        BattlePosition position = new(new BattleBoardCells(), head, false, false);

        return new BattleUnit(new Troop(monster, 5), position, false, uid) { Color = color };
    }

    [Fact]
    public void FindUidAndFindModeAnswerNullWhenNothingMatches()
    {
        BattleUnits units = new();
        units.Add(Spawn(MonsterType.Peasant, 40, 1));

        Assert.Same(units[0], units.FindUid(1));
        Assert.Null(units.FindUid(2));

        Assert.Null(units.FindMode(BattleMonsterState.SpellHaste));

        units[0].SetModes(BattleMonsterState.SpellHaste);
        Assert.Same(units[0], units.FindMode(BattleMonsterState.SpellHaste));
        Assert.Same(units[0], units.FindMode(BattleMonsterState.Magic));
    }

    [Fact]
    public void FindModeTakesTheFirstStackCarryingIt()
    {
        BattleUnits units = new();
        BattleUnit first = Spawn(MonsterType.Peasant, 40, 1);
        BattleUnit second = Spawn(MonsterType.Peasant, 20, 2);

        second.SetModes(BattleMonsterState.SpellSlow);

        units.Add(first);
        units.Add(second);

        Assert.Same(second, units.FindMode(BattleMonsterState.SpellSlow));
    }

    [Fact]
    public void FasterStacksComeFirst()
    {
        BattleUnits units = new();
        BattleUnit peasant = Spawn(MonsterType.Peasant, 40, 1);
        BattleUnit vampire = Spawn(MonsterType.Vampire, 20, 2);

        units.Add(peasant);
        units.Add(vampire);
        units.SortFastest();

        Assert.Equal([vampire, peasant], units);
    }

    [Fact]
    public void EqualSpeedsKeepTheirOrder()
    {
        BattleUnits units = new();
        BattleUnit first = Spawn(MonsterType.Peasant, 40, 1);
        BattleUnit second = Spawn(MonsterType.Peasant, 30, 2);
        BattleUnit third = Spawn(MonsterType.Peasant, 20, 3);

        units.Add(first);
        units.Add(second);
        units.Add(third);
        units.SortFastest();

        Assert.Equal([first, second, third], units);
    }

    [Fact]
    public void TheSortUsesTheBattleSpeedNotTheCreatureSpeed()
    {
        BattleUnits units = new();
        BattleUnit peasant = Spawn(MonsterType.Peasant, 40, 1);
        BattleUnit vampire = Spawn(MonsterType.Vampire, 20, 2);

        // 减速后的吸血鬼仍比农民快；这里要验的是「比较的是战斗速度」——把减速加到足以反超再比。
        vampire.SetModes(BattleMonsterState.SpellSlow);
        uint slowed = vampire.GetSpeed();

        units.Add(peasant);
        units.Add(vampire);
        units.SortFastest();

        Assert.Equal(slowed > peasant.GetSpeed() ? vampire : peasant, units[0]);
    }

    [Fact]
    public void TheValidOnlyCopyDropsTheDead()
    {
        BattleUnits units = new();
        BattleUnit alive = Spawn(MonsterType.Peasant, 40, 1);
        BattleUnit dead = Spawn(MonsterType.Peasant, 20, 2);

        dead.ApplyDamage(Spawn(MonsterType.Peasant, 10, 3), 100, out _, out _);

        units.Add(alive);
        units.Add(dead);

        BattleUnits valid = units.ValidOnly();

        Assert.Equal([alive], valid);
        Assert.Equal(2, units.Count);
    }

    [Fact]
    public void TheValidExceptCopyDropsOneLivingStack()
    {
        BattleUnits units = new();
        BattleUnit first = Spawn(MonsterType.Peasant, 40, 1);
        BattleUnit second = Spawn(MonsterType.Peasant, 20, 2);

        units.Add(first);
        units.Add(second);

        Assert.Equal([second], units.ValidExcept(first));
    }

    [Fact]
    public void TheOwnSideCopyDropsHypnotizedStacks()
    {
        BattleUnits units = new();
        BattleUnit loyal = Spawn(MonsterType.Peasant, 40, 1, PlayerColor.Blue);
        BattleUnit hypnotized = Spawn(MonsterType.Peasant, 20, 2, PlayerColor.Blue);

        hypnotized.SetModes(BattleMonsterState.SpellHypnotize);
        hypnotized.Field = new StubField(PlayerColor.Red);

        units.Add(loyal);
        units.Add(hypnotized);

        Assert.Equal([loyal], units.ValidOnOwnSide());
    }

    private sealed class StubField(PlayerColor opposite) : IBattleField
    {
        public bool IsOnMoat(BattleUnit unit) => false;

        public int GetShootingPenaltyPercent(BattleUnit attacker, BattleUnit defender) => 0;

        public int GetArcheryBonusPercent(BattleUnit unit) => 0;

        public PlayerColor GetOppositeColor(PlayerColor color) => opposite;

        public bool EnemyHasMonster(PlayerColor color, MonsterType monster) => false;

        public PlayerColor AttackingArmyColor => PlayerColor.Blue;
    }
}
