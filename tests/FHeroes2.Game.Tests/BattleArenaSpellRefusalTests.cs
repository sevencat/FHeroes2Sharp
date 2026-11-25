using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The sentence <c>Arena::isDisableCastSpell( spell, msg )</c> hands to the dialog that refuses a cast
/// (<c>battle_arena.cpp:927-1008</c>).
/// </summary>
/// <remarks>
/// 召唤的两句（已有另一种元素、英雄旁边没有空格）需要摆好的战场，留给召唤的测试；这里覆盖其余四句与
/// 「空法术只是探一探」的那条短路。
/// </remarks>
public class BattleArenaSpellRefusalTests
{
    /// <summary>A commander that answers exactly what a test needs, without a hero or a town.</summary>
    private sealed class Commander : IArmyCommander
    {
        public HeroModes Modes { get; init; } = HeroModes.None;

        public bool IsHeroes => true;

        public ArtifactBag BagArtifacts { get; } = new();

        public int GetAttack() => 2;

        public int GetDefense() => 2;

        public uint GetSecondarySkillValue(SecondarySkill skill) => 0;

        public double GetMagicStrategicValue(double armyStrength) => 0;

        public PlayerControl GetControl() => PlayerControl.Human;

        public PlayerColor GetColor() => PlayerColor.Blue;

        public int GetPower() => 1;

        public int GetMorale() => 0;

        public int GetLuck() => 0;
    }

    private static GameArmy Army(PlayerColor color, IArmyCommander? commander = null)
    {
        GameArmy army = new() { Color = color, Commander = commander };
        army.GetTroop(0).Set(MonsterType.Peasant, 10);

        return army;
    }

    /// <summary>A field without a castle, so the blue commander acts first and <c>Earthquake</c> has no target.</summary>
    private static BattleArena Field(IArmyCommander? blue = null, IArmyCommander? red = null)
    {
        BattleArena arena = new(Army(PlayerColor.Blue, blue), Army(PlayerColor.Red, red), new Pcg32(23));

        // 参考只在有人行动时问「能不能施法」；Turns() 也是先把当前单位定下来再进循环。
        arena.SetCurrentUnit(arena.GetCurrentUnit());

        return arena;
    }

    [Fact]
    public void WithoutACommanderEverySpellIsRefusedForThatReason()
    {
        BattleArena arena = Field();

        Assert.True(arena.IsDisableCastSpell(new Spell(SpellType.Bless), out string? reason));
        Assert.Equal(BattleArena.NoCommanderMessage, reason);
        Assert.Equal("You cannot cast spells without a commanding hero.", reason);
    }

    [Fact]
    public void TheSecondSpellOfTheRoundIsRefusedForThatReason()
    {
        BattleArena arena = Field(new Commander { Modes = HeroModes.Spellcasted });

        Assert.True(arena.IsDisableCastSpell(new Spell(SpellType.Bless), out string? reason));
        Assert.Equal(BattleArena.AlreadyCastMessage, reason);
        Assert.Equal("You have already cast a spell this round.", reason);
    }

    [Fact]
    public void TheSphereOfNegationOutranksTheCommanderCheck()
    {
        Commander holder = new();
        holder.BagArtifacts.Push(ArtifactType.SphereNegation);

        BattleArena arena = Field(holder);

        Assert.True(arena.IsSpellcastDisabled);
        Assert.True(arena.IsDisableCastSpell(new Spell(SpellType.Bless), out string? reason));
        Assert.Equal(BattleArena.SphereOfNegationMessage, reason);
        Assert.Equal("The Sphere of Negation artifact is in effect for this battle, disabling all combat spells.",
            reason);
    }

    [Fact]
    public void EarthquakeWithoutACastleSaysItWouldDoNothing()
    {
        BattleArena arena = Field(new Commander());

        Assert.True(arena.IsDisableCastSpell(new Spell(SpellType.Earthquake), out string? reason));
        Assert.Equal(BattleArena.NoEffectMessage, reason);
        Assert.Equal("That spell will have no effect!", reason);
    }

    [Fact]
    public void TheEmptySpellOnlyAsksWhetherCastingIsPossible()
    {
        BattleArena arena = Field(new Commander());

        Assert.False(arena.IsDisableCastSpell(new Spell(SpellType.None), out string? reason));
        Assert.Null(reason);
    }

    [Fact]
    public void TheFlagOnlyFormAgreesWithTheOutForm()
    {
        BattleArena allowed = Field(new Commander());
        BattleArena refused = Field();

        Assert.Equal(allowed.IsDisableCastSpell(new Spell(SpellType.Bless)),
            allowed.IsDisableCastSpell(new Spell(SpellType.Bless), out _));
        Assert.Equal(refused.IsDisableCastSpell(new Spell(SpellType.Bless)),
            refused.IsDisableCastSpell(new Spell(SpellType.Bless), out _));
    }
}
