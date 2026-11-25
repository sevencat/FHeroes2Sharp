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
/// The cells the mouse shadow covers while a spell is aimed, <see cref="BattleSpellHighlight"/>, a port of the
/// <c>humanturn_spell</c> branch of <c>Interface::RedrawCover()</c> (<c>battle_interface.cpp:2291-2358</c>).
/// </summary>
public sealed class BattleSpellHighlightTests
{
    /// <summary>A field battle between one peasant on each side.</summary>
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

    /// <summary>An empty cell with all six neighbours, so the area counts are not cut by the border.</summary>
    private static int InteriorCell()
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (BattleBoard.GetAroundIndexes(index).Length == 6)
            {
                return index;
            }
        }

        throw new InvalidOperationException("The board has no interior cell.");
    }

    /// <summary>A single target spell covers the cell under the pointer only.</summary>
    [Theory]
    [InlineData(SpellType.Haste)]
    [InlineData(SpellType.LightningBolt)]
    [InlineData(SpellType.Teleport)]
    public void GetCells_SingleTargetSpells_CoverTheHoveredCell(SpellType spell)
    {
        int cell = InteriorCell();

        IReadOnlyList<int> cells = BattleSpellHighlight.GetCells(
            spell, cell, CursorTheme.CombatNone, null);

        Assert.Equal([cell], cells);
    }

    /// <summary>The exact fireball and meteor shower area is the cell plus its six neighbours, seven in all.</summary>
    [Theory]
    [InlineData(SpellType.Fireball)]
    [InlineData(SpellType.MeteorShower)]
    public void GetCells_AreaSpells_CoverTheCellAndOneRing(SpellType spell)
    {
        int cell = InteriorCell();

        IReadOnlyList<int> cells = BattleSpellHighlight.GetCells(
            spell, cell, CursorTheme.CombatNone, null);

        Assert.Equal(7, cells.Count);
        Assert.Contains(cell, cells);
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    /// <summary>Cold ring hits the ring only, never the cell under the pointer itself.</summary>
    [Fact]
    public void GetCells_ColdRing_SkipsTheCentreCell()
    {
        int cell = InteriorCell();

        IReadOnlyList<int> cells = BattleSpellHighlight.GetCells(
            SpellType.ColdRing, cell, CursorTheme.CombatNone, null);

        Assert.Equal(6, cells.Count);
        Assert.DoesNotContain(cell, cells);
    }

    /// <summary>
    /// Fire blast highlights the cell plus the ring at distance two, thirteen cells on open ground; the ring in
    /// between is left bare because the reference only asks for <c>GetDistanceIndexes( cell, 2 )</c>.
    /// </summary>
    [Fact]
    public void GetCells_FireBlast_CoversTheCellAndTheFarRing()
    {
        int cell = InteriorCell();

        IReadOnlyList<int> cells = BattleSpellHighlight.GetCells(
            SpellType.FireBlast, cell, CursorTheme.CombatNone, null);

        Assert.Equal(13, cells.Count);
        Assert.Contains(cell, cells);
    }

    /// <summary>Once the source is picked, the teleport highlight follows the cell it would land on.</summary>
    [Fact]
    public void GetCells_TeleportDestination_CoversTheLandingCell()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);
        int destination = InteriorCell();

        IReadOnlyList<int> cells = BattleSpellHighlight.GetCells(
            SpellType.Teleport, destination, CursorTheme.SpellTeleport, unit);

        Assert.Equal([destination], cells);
    }

    /// <summary>A pointer off the board highlights nothing.</summary>
    [Fact]
    public void GetCells_OffTheBoard_CoverNothing()
    {
        IReadOnlyList<int> cells = BattleSpellHighlight.GetCells(
            SpellType.Haste, BattleSpellHighlight.NoCell, CursorTheme.CombatNone, null);

        Assert.Empty(cells);
    }
}
