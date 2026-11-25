using FHeroes2.Assets.Maps;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The predicted action order of the turn order bar, <see cref="BattleOrderOfUnits"/>:
/// <c>Battle::UpdateOrderOfUnits()</c> (<c>battle_arena.cpp:223-250</c>).
/// </summary>
public sealed class BattleOrderOfUnitsTests
{
    private static GameArmy MakeArmy(PlayerColor color, params (MonsterType Monster, uint Count)[] troops)
    {
        GameArmy army = new() { Color = color };

        for (int index = 0; index < troops.Length; ++index)
        {
            army.GetTroop(index).Set(troops[index].Monster, troops[index].Count);
        }

        return army;
    }

    private static (BattleForce Attacking, BattleForce Defending) Deploy(GameArmy attackers, GameArmy defenders)
    {
        BattleBoardCells board = new();
        BattleTroopsUidGenerator generator = new();

        return (new BattleForce(attackers, board, false, generator),
            new BattleForce(defenders, board, true, generator));
    }

    /// <summary>The faster side's stack comes first, whatever the tie-break colour is.</summary>
    [Fact]
    public void TheFasterStackComesFirst()
    {
        GameArmy attackers = MakeArmy(PlayerColor.Blue, (MonsterType.Peasant, 5));
        GameArmy defenders = MakeArmy(PlayerColor.Red, (MonsterType.Vampire, 3));
        (BattleForce attacking, BattleForce defending) = Deploy(attackers, defenders);

        BattleUnits order = BattleOrderOfUnits.Predict(
            attacking, defending, null, defending.Color, new BattleUnits());

        Assert.Equal([defending[0], attacking[0]], order);
    }

    /// <summary>Equal speeds go to the side <c>preferredColor</c> names (<c>battle_arena.cpp:169-203</c>).</summary>
    [Fact]
    public void EqualSpeedsFollowThePreferredColour()
    {
        GameArmy attackers = MakeArmy(PlayerColor.Blue, (MonsterType.Peasant, 5));
        GameArmy defenders = MakeArmy(PlayerColor.Red, (MonsterType.Peasant, 5));
        (BattleForce attacking, BattleForce defending) = Deploy(attackers, defenders);

        Assert.Equal([attacking[0], defending[0]], BattleOrderOfUnits.Predict(
            attacking, defending, null, attacking.Color, new BattleUnits()));

        Assert.Equal([defending[0], attacking[0]], BattleOrderOfUnits.Predict(
            attacking, defending, null, defending.Color, new BattleUnits()));
    }

    /// <summary>After one side takes a tie, the other side gets the next one (<c>:236-244</c>).</summary>
    [Fact]
    public void TheTieBreakAlternatesAfterEachStack()
    {
        GameArmy attackers = MakeArmy(PlayerColor.Blue,
            (MonsterType.Peasant, 5), (MonsterType.Peasant, 5));
        GameArmy defenders = MakeArmy(PlayerColor.Red, (MonsterType.Peasant, 5));
        (BattleForce attacking, BattleForce defending) = Deploy(attackers, defenders);

        BattleUnits order = BattleOrderOfUnits.Predict(
            attacking, defending, null, defending.Color, new BattleUnits());

        Assert.Equal([defending[0], attacking[0], attacking[1]], order);
    }

    /// <summary>The stack acting right now is left out — it is no longer「upcoming」(<c>:242-244</c>).</summary>
    [Fact]
    public void TheCurrentStackIsNotListed()
    {
        GameArmy attackers = MakeArmy(PlayerColor.Blue,
            (MonsterType.Vampire, 3), (MonsterType.Peasant, 5));
        GameArmy defenders = MakeArmy(PlayerColor.Red, (MonsterType.Peasant, 5));
        (BattleForce attacking, BattleForce defending) = Deploy(attackers, defenders);

        BattleUnits order = BattleOrderOfUnits.Predict(
            attacking, defending, attacking[0], defending.Color, new BattleUnits());

        Assert.DoesNotContain(attacking[0], order);
        Assert.Equal([defending[0], attacking[1]], order);
    }

    /// <summary>The stacks that already acted keep their place in front of the prediction.</summary>
    [Fact]
    public void TheHistoryComesFirst()
    {
        GameArmy attackers = MakeArmy(PlayerColor.Blue, (MonsterType.Peasant, 5));
        GameArmy defenders = MakeArmy(PlayerColor.Red, (MonsterType.Vampire, 3));
        (BattleForce attacking, BattleForce defending) = Deploy(attackers, defenders);

        // 已行动的单位速度归零，预测循环不会再把它挑出来，所以它只出现一次。
        attacking[0].SetModes(BattleMonsterState.Moved);

        BattleUnits history = new();
        history.Add(attacking[0]);

        BattleUnits order = BattleOrderOfUnits.Predict(
            attacking, defending, null, defending.Color, history);

        Assert.Equal([attacking[0], defending[0]], order);
    }

    /// <summary>An empty battle has an empty order.</summary>
    [Fact]
    public void AnEmptyBattleHasNoOrder()
    {
        GameArmy attackers = MakeArmy(PlayerColor.Blue);
        GameArmy defenders = MakeArmy(PlayerColor.Red);
        (BattleForce attacking, BattleForce defending) = Deploy(attackers, defenders);

        BattleUnits order = BattleOrderOfUnits.Predict(
            attacking, defending, null, PlayerColor.Blue, new BattleUnits());

        Assert.Empty(order);
    }
}
