using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>battle_troop.cpp:100-131</c>: the list of timed spell effects a unit carries.
/// </summary>
public class BattleModesAffectedTests
{
    [Fact]
    public void AddingTheSameModeTwiceOnlyRefreshesItsDuration()
    {
        BattleModesAffected affected = new();

        affected.AddMode(BattleMonsterState.SpellSlow, 3);
        affected.AddMode(BattleMonsterState.SpellSlow, 5);

        Assert.Equal(1, affected.Count);
        Assert.Equal(5u, affected[0].Duration);
        Assert.Equal(5u, affected.GetMode(BattleMonsterState.SpellSlow));
    }

    [Fact]
    public void ALookupByMaskFindsAnyOfItsModes()
    {
        BattleModesAffected affected = new();

        affected.AddMode(BattleMonsterState.SpellBless, 4);

        Assert.Equal(4u, affected.GetMode(BattleMonsterState.GoodMagic));
        Assert.Equal(4u, affected.GetMode(BattleMonsterState.SpellBless));
        Assert.Equal(0u, affected.GetMode(BattleMonsterState.SpellCurse));
        Assert.Equal(0u, affected.GetMode(BattleMonsterState.BadMagic));
    }

    [Fact]
    public void TheDurationBurnsOneTurnAtATimeAndStopsAtZero()
    {
        BattleModesAffected affected = new();

        affected.AddMode(BattleMonsterState.SpellHaste, 2);
        affected.DecreaseDuration();

        Assert.Equal(1u, affected.GetMode(BattleMonsterState.SpellHaste));
        Assert.Equal(0u, affected.FindZeroDuration());

        affected.DecreaseDuration();
        Assert.Equal(0u, affected.GetMode(BattleMonsterState.SpellHaste));
        Assert.Equal(BattleMonsterState.SpellHaste, affected.FindZeroDuration());

        // 参考不会让时长掉成负数。
        affected.DecreaseDuration();
        Assert.Equal(0u, affected.GetMode(BattleMonsterState.SpellHaste));
    }

    [Fact]
    public void TheFirstExpiredModeIsTheOneReported()
    {
        BattleModesAffected affected = new();

        affected.AddMode(BattleMonsterState.SpellShield, 1);
        affected.AddMode(BattleMonsterState.SpellCurse, 1);

        affected.DecreaseDuration();

        Assert.Equal(BattleMonsterState.SpellShield, affected.FindZeroDuration());
    }

    [Fact]
    public void RemovingByMaskDropsEveryMatch()
    {
        BattleModesAffected affected = new();

        affected.AddMode(BattleMonsterState.SpellSlow, 3);
        affected.AddMode(BattleMonsterState.SpellBlind, 2);
        affected.AddMode(BattleMonsterState.SpellBless, 1);

        affected.RemoveMode(BattleMonsterState.BadMagic);

        Assert.Equal(1, affected.Count);
        Assert.Equal(0u, affected.GetMode(BattleMonsterState.SpellSlow));
        Assert.Equal(1u, affected.GetMode(BattleMonsterState.SpellBless));

        affected.Clear();
        Assert.True(affected.IsEmpty);
    }
}
