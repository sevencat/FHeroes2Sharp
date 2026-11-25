using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Battle;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The cursor and the hint of an aim, <see cref="BattleSpellCursor"/>, a port of <c>Interface::GetBattleSpellCursor()</c>
/// (<c>battle_interface.cpp:3066-3117</c>).
/// </summary>
public sealed class BattleSpellCursorTests
{
    /// <summary>A field battle between one peasant on each side, so a unit and free cells are both at hand.</summary>
    private static BattleArena Arena()
    {
        static GameArmy Army(PlayerColor color)
        {
            GameArmy army = new()
            {
                Color = color,
            };

            army.GetTroop(0).Set(MonsterType.Peasant, 10);

            return army;
        }

        return new BattleArena(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(7));
    }

    /// <summary>The first stack on the board, in cell order.</summary>
    private static BattleUnit FirstUnit(BattleArena arena)
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (arena.Board.GetCell(index)?.Unit is { } unit)
            {
                return unit;
            }
        }

        throw new InvalidOperationException("The arena placed no unit.");
    }

    /// <summary>An empty cell with all six neighbours, so area highlights are not cut by the border.</summary>
    private static int InteriorEmptyCell(BattleArena arena)
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (BattleBoard.GetAroundIndexes(index).Length == 6
                && arena.Board.GetCell(index) is { Unit: null } cell && cell.IsPassable(false))
            {
                return index;
            }
        }

        throw new InvalidOperationException("The arena has no interior empty cell.");
    }

    /// <summary><c>getCursorForSpell()</c> maps spells onto the <c>SPELCO</c> frames the table lists.</summary>
    [Theory]
    [InlineData(SpellType.Slow, CursorTheme.SpellSlow)]
    [InlineData(SpellType.ColdRing, CursorTheme.SpellColdRing)]
    [InlineData(SpellType.Fireball, CursorTheme.SpellFireball)]
    [InlineData(SpellType.FireBlast, CursorTheme.SpellFireblast)]
    [InlineData(SpellType.Teleport, CursorTheme.SpellTeleport)]
    [InlineData(SpellType.MirrorImage, CursorTheme.SpellMirrorImage)]
    [InlineData(SpellType.Earthquake, CursorTheme.SpellEarthquake)]
    [InlineData(SpellType.Arrow, CursorTheme.SpellArrow)]
    public void ThemeFor_MapsSingleTargetSpells(SpellType spell, CursorTheme expected) =>
        Assert.Equal(expected, BattleSpellCursor.ThemeFor(spell));

    /// <summary>
    /// The mass spells and the summons are missing from the reference's table too, so they keep the arrow pointer;
    /// they never enter an aim anyway, because they are cast without a focus object.
    /// </summary>
    [Theory]
    [InlineData(SpellType.None)]
    [InlineData(SpellType.MassHaste)]
    [InlineData(SpellType.MassBless)]
    public void ThemeFor_LeavesUnlistedSpellsOnThePointer(SpellType spell) =>
        Assert.Equal(CursorTheme.CombatNone, BattleSpellCursor.ThemeFor(spell));

    /// <summary>A friendly spell over one of its own stacks is legal, and the hint names the creature in lower case.</summary>
    [Fact]
    public void Describe_OnAUnitThatTakesTheSpell_ShowsTheSpellCursor()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);

        BattleSpellHover hover = BattleSpellCursor.Describe(
            arena, unit, new Spell(SpellType.Haste), unit.HeadIndex, BattleSpellHighlight.NoCell);

        Assert.Equal(CursorTheme.SpellHaste, hover.Theme);
        Assert.Equal("Cast Haste on peasant", hover.Status);
    }

    /// <summary>A damage spell with no troop restriction aims at the ground, so an empty cell is a legal target.</summary>
    [Fact]
    public void Describe_OnEmptyGroundForAnUntargetedSpell_ShowsTheSpellCursor()
    {
        BattleArena arena = Arena();
        int cell = InteriorEmptyCell(arena);

        BattleSpellHover hover = BattleSpellCursor.Describe(
            arena, FirstUnit(arena), new Spell(SpellType.Fireball), cell, BattleSpellHighlight.NoCell);

        Assert.Equal(CursorTheme.SpellFireball, hover.Theme);
        Assert.Equal("Cast Fireball", hover.Status);
    }

    /// <summary>A friendly spell over empty ground has nothing to land on, so the pointer stays an arrow.</summary>
    [Fact]
    public void Describe_OnEmptyGroundForAFriendlySpell_ShowsThePointer()
    {
        BattleArena arena = Arena();
        int cell = InteriorEmptyCell(arena);

        BattleSpellHover hover = BattleSpellCursor.Describe(
            arena, FirstUnit(arena), new Spell(SpellType.Haste), cell, BattleSpellHighlight.NoCell);

        Assert.Equal(CursorTheme.CombatNone, hover.Theme);
        Assert.Equal(BattleSpellCursor.SelectTargetStatus, hover.Status);
    }

    /// <summary>With the source picked, a free cell is the destination and the hint says so.</summary>
    [Fact]
    public void Describe_OnAFreeDestinationOfATeleport_ShowsTheTeleportCursor()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);
        int destination = InteriorEmptyCell(arena);

        BattleSpellHover hover = BattleSpellCursor.Describe(
            arena, unit, new Spell(SpellType.Teleport), destination, unit.HeadIndex);

        Assert.Equal(CursorTheme.SpellTeleport, hover.Theme);
        Assert.Equal(BattleSpellCursor.TeleportHereStatus, hover.Status);
    }

    /// <summary>A teleport onto a stack is refused before the spell is cast.</summary>
    [Fact]
    public void Describe_OnATakenDestinationOfATeleport_ShowsThePointer()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);
        int occupied = InteriorEmptyCell(arena);

        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (arena.Board.GetCell(index)?.Unit is { } other && other != unit)
            {
                occupied = index;

                break;
            }
        }

        BattleSpellHover hover = BattleSpellCursor.Describe(
            arena, unit, new Spell(SpellType.Teleport), occupied, unit.HeadIndex);

        Assert.Equal(CursorTheme.CombatNone, hover.Theme);
        Assert.Equal(BattleSpellCursor.InvalidTeleportStatus, hover.Status);
    }

    /// <summary>Without a spell there is nothing to aim, and the pointer stays an arrow with the generic hint.</summary>
    [Fact]
    public void Describe_WithoutASpell_ShowsThePointer()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);

        BattleSpellHover hover = BattleSpellCursor.Describe(
            arena, unit, new Spell(SpellType.None), unit.HeadIndex, BattleSpellHighlight.NoCell);

        Assert.Equal(CursorTheme.CombatNone, hover.Theme);
        Assert.Equal(BattleSpellCursor.SelectTargetStatus, hover.Status);
    }
}
