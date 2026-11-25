using FHeroes2.Assets.Maps;
using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The army colours of the battle interface, <see cref="BattleArmyColor"/>:
/// <c>Battle::ArmyColor</c> and <c>GetArmyColorFromPlayerColor()</c>
/// (<c>battle_interface.h:106-113</c>, <c>battle_interface.cpp:723-750</c>).
/// </summary>
public sealed class BattleArmyColorTests
{
    /// <summary>The numeric values are palette indices of <c>KB.PAL</c> and must not drift.</summary>
    [Fact]
    public void TheColoursAreTheReferenceIndices()
    {
        Assert.Equal(0x00, BattleArmyColor.Black);
        Assert.Equal(0x47, BattleArmyColor.Blue);
        Assert.Equal(0x67, BattleArmyColor.Green);
        Assert.Equal(0xBD, BattleArmyColor.Red);
        Assert.Equal(0x70, BattleArmyColor.Yellow);
        Assert.Equal(0xCD, BattleArmyColor.Orange);
        Assert.Equal(0x87, BattleArmyColor.Purple);
        Assert.Equal(0x10, BattleArmyColor.Gray);
    }

    /// <summary>Each playable colour maps onto its own group.</summary>
    [Fact]
    public void EveryPlayerColourMapsToItsOwnGroup()
    {
        Assert.Equal(BattleArmyColor.Blue, BattleArmyColor.GetPaletteIndex(PlayerColor.Blue));
        Assert.Equal(BattleArmyColor.Green, BattleArmyColor.GetPaletteIndex(PlayerColor.Green));
        Assert.Equal(BattleArmyColor.Red, BattleArmyColor.GetPaletteIndex(PlayerColor.Red));
        Assert.Equal(BattleArmyColor.Yellow, BattleArmyColor.GetPaletteIndex(PlayerColor.Yellow));
        Assert.Equal(BattleArmyColor.Orange, BattleArmyColor.GetPaletteIndex(PlayerColor.Orange));
        Assert.Equal(BattleArmyColor.Purple, BattleArmyColor.GetPaletteIndex(PlayerColor.Purple));
    }

    /// <summary>A stack without a player is gray, one under Berserk is black (<c>:723-750</c>).</summary>
    [Fact]
    public void TheTwoSpecialColoursAreGrayAndBlack()
    {
        Assert.Equal(BattleArmyColor.Gray, BattleArmyColor.GetPaletteIndex(PlayerColor.None));
        Assert.Equal(BattleArmyColor.Black, BattleArmyColor.GetPaletteIndex(PlayerColor.Unused));
    }

    /// <summary>An unheard-of colour falls back to gray, where the reference asserts and returns gray.</summary>
    [Fact]
    public void AnUnknownColourFallsBackToGray()
    {
        Assert.Equal(BattleArmyColor.Gray, BattleArmyColor.GetPaletteIndex((PlayerColor)0x40));
    }
}
