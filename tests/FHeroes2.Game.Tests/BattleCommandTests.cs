using FHeroes2.Game.Battle;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>battle_command.cpp</c>: the reversed parameter list, popping the values back out, and the seed mixing
/// that lets the same battle replay identically.
/// </summary>
public class BattleCommandTests
{
    [Fact]
    public void TheParametersAreStoredInReverse()
    {
        BattleCommand command = BattleCommand.Move(unitUid: 7, cellIndex: 42);

        Assert.Equal(BattleCommandType.Move, command.Type);
        Assert.Equal(2, command.Count);
        Assert.Equal([42, 7], command.Parameters);
    }

    [Fact]
    public void PoppingTheValuesGivesThemBackInCallOrder()
    {
        BattleCommand command = BattleCommand.Attack(attackerUid: 3, defenderUid: 9, cellToMove: 20, cellToAttack: 21, (int)AttackDirection.Right);

        Assert.Equal(5, command.Count);
        Assert.Equal(3, command.GetNextValue());
        Assert.Equal(9, command.GetNextValue());
        Assert.Equal(20, command.GetNextValue());
        Assert.Equal(21, command.GetNextValue());
        Assert.Equal((int)AttackDirection.Right, command.GetNextValue());

        // 弹空以后再问，参考答 0 而不是报错。
        Assert.Equal(0, command.GetNextValue());
        Assert.Equal(0, command.Count);
    }

    [Fact]
    public void TheCommandsWithoutParametersStayEmpty()
    {
        Assert.Equal(0, BattleCommand.Catapult().Count);
        Assert.Equal(0, BattleCommand.Retreat().Count);
        Assert.Equal(0, BattleCommand.Surrender().Count);
        Assert.Equal(0, BattleCommand.QuickCombat().Count);
        Assert.Equal(0, BattleCommand.Catapult().GetNextValue());
    }

    [Fact]
    public void ASpellCastTakesATargetOrASourceAndDestination()
    {
        BattleCommand single = BattleCommand.SpellCast(SpellType.LightningBolt, 12);

        Assert.Equal(BattleCommandType.Spellcast, single.Type);
        Assert.Equal([12, (int)SpellType.LightningBolt], single.Parameters);

        BattleCommand teleport = BattleCommand.SpellCast(SpellType.Teleport, 4, 40);

        Assert.Equal(3, teleport.Count);
        Assert.Equal((int)SpellType.Teleport, teleport.GetNextValue());
        Assert.Equal(4, teleport.GetNextValue());
        Assert.Equal(40, teleport.GetNextValue());

        // 参考只允许传送带两个格子。
        Assert.Throws<ArgumentException>(() => BattleCommand.SpellCast(SpellType.Cure, 1, 2));
    }

    [Fact]
    public void TheSeedMixingIsDeterministic()
    {
        ulong seed = 0x0123456789abcdef;

        BattleCommand first = BattleCommand.Move(7, 42);
        BattleCommand second = BattleCommand.Move(7, 42);

        Assert.Equal(first.UpdatePcg32Stream(seed), second.UpdatePcg32Stream(seed));
        Assert.NotEqual(seed, first.UpdatePcg32Stream(seed));
    }

    [Fact]
    public void AnAttackIgnoresTheCellsAndDirectionThatDifferByPlayerType()
    {
        ulong seed = 0x9876;

        BattleCommand byHuman = BattleCommand.Attack(1, 2, 30, 31, (int)AttackDirection.Top);
        BattleCommand byAi = BattleCommand.Attack(1, 2, 30, 40, (int)AttackDirection.BottomLeft);

        // 参考只混入移动格与双方 UID，所以这两种写法落在同一条随机序列上。
        Assert.Equal(byHuman.UpdatePcg32Stream(seed), byAi.UpdatePcg32Stream(seed));
    }

    [Fact]
    public void MovingAUnitElsewhereChangesTheStream()
    {
        ulong seed = 0x9876;

        Assert.NotEqual(
            BattleCommand.Attack(1, 2, 30, 31, (int)AttackDirection.Top).UpdatePcg32Stream(seed),
            BattleCommand.Attack(1, 2, 32, 31, (int)AttackDirection.Top).UpdatePcg32Stream(seed));
    }

    [Fact]
    public void TheAutomationCommandsLeaveTheStreamAlone()
    {
        ulong seed = 0x5555;

        Assert.Equal(seed, BattleCommand.ToggleAutoCombat(1).UpdatePcg32Stream(seed));
        Assert.Equal(seed, BattleCommand.QuickCombat().UpdatePcg32Stream(seed));
    }

    [Fact]
    public void EveryOtherCommandTypeMixesItselfIn()
    {
        ulong seed = 0x1234;

        BattleCommand[] commands =
        [
            BattleCommand.Move(1, 2),
            BattleCommand.SpellCast(SpellType.LightningBolt, 3),
            BattleCommand.Morale(4, 5),
            BattleCommand.Catapult(),
            BattleCommand.Tower(1, 2),
            BattleCommand.Retreat(),
            BattleCommand.Surrender(),
            BattleCommand.Skip(6),
        ];

        List<ulong> streams = [];

        foreach (BattleCommand command in commands)
        {
            ulong mixed = command.UpdatePcg32Stream(seed);

            Assert.NotEqual(seed, mixed);

            streams.Add(mixed);
        }

        // 类型本身参与混合，因此两条参数相同的不同命令不会撞在一起。
        Assert.Equal(streams.Count, streams.Distinct().Count());
    }
}
