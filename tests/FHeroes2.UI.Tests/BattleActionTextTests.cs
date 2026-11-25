using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Battle;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The sentences of the battle status line, <see cref="BattleActionText"/>, a port of
/// <c>Interface::RedrawActionSkipStatus()</c> (<c>battle_interface.cpp:4005-4011</c>).
/// </summary>
public sealed class BattleActionTextTests
{
    /// <summary>A field battle where both sides hold one stack of peasants of the given size.</summary>
    private static BattleArena Arena(uint count)
    {
        static GameArmy Army(PlayerColor color, uint count)
        {
            GameArmy army = new()
            {
                Color = color,
            };

            army.GetTroop(0).Set(MonsterType.Peasant, count);

            return army;
        }

        return new BattleArena(Army(PlayerColor.Blue, count), Army(PlayerColor.Red, count), new Pcg32(7));
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

    /// <summary>
    /// A stack of ten takes the plural template and the plural name, and the name loses its capital because it
    /// stands in the middle of the sentence.
    /// </summary>
    [Fact]
    public void Skip_OnAStackOfSeveral_SaysThePeasantsSkipTheirTurn()
    {
        string message = BattleActionText.Skip(FirstUnit(Arena(10)));

        Assert.Equal("The peasants skip their turn.", message);
    }

    /// <summary>A lone creature takes the other template, <c>_n()</c>'s singular branch.</summary>
    [Fact]
    public void Skip_OnASingleCreature_SaysThePeasantSkipsTheirTurn()
    {
        string message = BattleActionText.Skip(FirstUnit(Arena(1)));

        Assert.Equal("The peasant skips their turn.", message);
    }
}
