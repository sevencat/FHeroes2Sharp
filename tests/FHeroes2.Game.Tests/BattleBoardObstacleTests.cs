using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the battlefield set-up half of <c>Battle::Board</c>: the ground decoration
/// (<c>Board::SetCovrObjects()</c>) and the obstacle scatter (<c>Board::SetCobjObjects()</c>),
/// <c>battle_board.cpp:416-695</c>.
/// </summary>
public class BattleBoardObstacleTests
{
    private static int[] Objects(BattleBoardCells board)
    {
        int[] objects = new int[BattleBoard.WidthInCells * 9];

        for (int index = 0; index < objects.Length; ++index)
        {
            objects[index] = board.GetCell(index)!.Object;
        }

        return objects;
    }

    [Fact]
    public void ADecorationBlocksTheCellsItCovers()
    {
        BattleBoardCells board = new();

        board.SetCovrObjects(IcnType.Covr0001);

        foreach (int index in new[] { 25, 26, 27, 28, 40, 51 })
        {
            Assert.Equal(0x40, board.GetCell(index)!.Object);
        }

        Assert.Equal(0, board.GetCell(24)!.Object);
        Assert.Equal(0, board.GetCell(41)!.Object);
    }

    [Fact]
    public void AnUnknownDecorationPlacesNothing()
    {
        BattleBoardCells board = new();

        board.SetCovrObjects(IcnType.Unknown);

        Assert.All(Objects(board), value => Assert.Equal(0, value));
    }

    [Fact]
    public void DecorationsDifferPerImage()
    {
        BattleBoardCells first = new();
        BattleBoardCells second = new();

        first.SetCovrObjects(IcnType.Covr0002);
        second.SetCovrObjects(IcnType.Covr0006);

        Assert.Equal(0x40, first.GetCell(47)!.Object);
        Assert.Equal(0, first.GetCell(14)!.Object);

        Assert.Equal(0x40, second.GetCell(14)!.Object);
        Assert.Equal(0, second.GetCell(47)!.Object);
    }

    [Fact]
    public void ObstaclesArePlacedWithTheirSecondCell()
    {
        BattleBoardCells board = new();

        board.SetCobjObjects(Mp2ObjectType.Graveyard, MapGround.Grass, new Pcg32(1234));

        int count = 0;

        for (int index = 0; index < BattleBoard.WidthInCells * 9; ++index)
        {
            if (board.GetCell(index)!.Object >= 0x80)
            {
                ++count;

                // 双格障碍的右格必须被占住，而且障碍头不能落在第 8 列（第 9 列留给部队）。
                IcnType icn = (IcnType)((int)IcnType.Cobj0000 + (board.GetCell(index)!.Object - 0x80));

                if (icn is IcnType.Cobj0004 or IcnType.Cobj0005 or IcnType.Cobj0007 or IcnType.Cobj0011
                    or IcnType.Cobj0014 or IcnType.Cobj0015 or IcnType.Cobj0017 or IcnType.Cobj0018
                    or IcnType.Cobj0019 or IcnType.Cobj0020 or IcnType.Cobj0022 or IcnType.Cobj0030
                    or IcnType.Cobj0031)
                {
                    Assert.True(index % BattleBoard.WidthInCells <= 7, "a two-hex obstacle may not start on column 8");
                    Assert.Equal(0x40, board.GetCell(index + 1)!.Object);
                }
            }
        }

        // 参考的上限是 6 个（没有大障碍时）。
        Assert.InRange(count, 0, 6);
    }

    [Fact]
    public void AGraveyardOnlyUsesItsOwnObjects()
    {
        BattleBoardCells board = new();

        board.SetCobjObjects(Mp2ObjectType.Graveyard, MapGround.Grass, new Pcg32(99));

        for (int index = 0; index < BattleBoard.WidthInCells * 9; ++index)
        {
            int value = board.GetCell(index)!.Object;

            if (value >= 0x80)
            {
                IcnType icn = (IcnType)((int)IcnType.Cobj0000 + (value - 0x80));

                Assert.Contains(icn, new[] { IcnType.Cobj0000, IcnType.Cobj0001, IcnType.Cobj0025 });
            }
        }
    }

    [Fact]
    public void TheSameSeedScattersTheSameObstacles()
    {
        BattleBoardCells first = new();
        BattleBoardCells second = new();

        first.SetCobjObjects(Mp2ObjectType.Graveyard, MapGround.Grass, new Pcg32(2026));
        second.SetCobjObjects(Mp2ObjectType.Graveyard, MapGround.Grass, new Pcg32(2026));

        Assert.Equal(Objects(first), Objects(second));
    }

    [Fact]
    public void ALargeDecorationLeavesFewerObstacles()
    {
        // Covr0005 盖住 15 格，超过 13 格这一档，上限降到 2 个。
        BattleBoardCells board = new();
        board.SetCovrObjects(IcnType.Covr0005);

        board.SetCobjObjects(Mp2ObjectType.Graveyard, MapGround.Grass, new Pcg32(7));

        int count = Objects(board).Count(value => value >= 0x80);
        Assert.InRange(count, 0, 2);
    }
}
