using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Which slice of a unit's sprite the moat hides, <see cref="BattleMoatMasking"/>:
/// <c>Interface::_drawTroopSpriteWithMoatMask()</c> (<c>battle_interface.cpp:2121-2200</c>).
/// </summary>
/// <remarks>
/// 棋盘铺在 <c>(0,0,640,480)</c> 上，所以护城河格的下标与像素可以直接对上：61 号格是
/// <c>x = 89 - 22 + 44*6 = 331</c>、<c>y = 62 + 42*5 = 272</c>，水面在 <c>(334, 306, 41, 10)</c>。
/// </remarks>
public sealed class BattleMoatMaskingTests
{
    private const int MoatCell = 61;

    /// <summary>A castle without a moat cuts nothing away.</summary>
    [Fact]
    public void ACastleWithoutAMoatCutsNothing()
    {
        BattleUnit unit = UnitAtMoat();

        Assert.Null(Mask(unit, hasBuiltMoat: false));
    }

    /// <summary>A flyer in the air passes over the water without being cut.</summary>
    [Fact]
    public void AFlyerInMotionIsNotCut()
    {
        BattleUnit unit = UnitAtMoat();

        Assert.Null(Mask(unit, isFlyingInMotion: true));
    }

    /// <summary>A unit standing away from the moat is drawn whole.</summary>
    [Fact]
    public void AUnitOnDryLandIsDrawnWhole()
    {
        BattleUnit unit = UnitAt(30);

        Assert.Null(Mask(unit));
    }

    /// <summary>The water band of the cell under a standing unit is cut out of its sprite.</summary>
    [Fact]
    public void StandingInTheMoatCutsTheWaterBand()
    {
        BattleUnit unit = UnitAtMoat();

        Assert.Equal(new Rect(334, 306, 41, 10), Mask(unit));
    }

    /// <summary>The water moves with the sprite during a diagonal step, so the feet stay in it.</summary>
    [Fact]
    public void ADiagonalStepCarriesTheWaterAlong()
    {
        BattleUnit unit = UnitAtMoat();

        Assert.Equal(new Rect(334, 306 + 8, 41, 10), Mask(unit, movementDelta: new Point(0, 8)));
    }

    /// <summary>A sprite shorter than the water band plus ten pixels is given up on.</summary>
    [Fact]
    public void AShortSpriteIsGivenUpOn()
    {
        BattleUnit unit = UnitAtMoat();

        // 19 - 10 = 9 < 10
        Assert.Null(Mask(unit, spriteArea: new Rect(320, 290, 60, 19)));
    }

    /// <summary>A sprite that does not reach the water at all is not cut.</summary>
    [Fact]
    public void ASpriteAboveTheWaterIsNotCut()
    {
        BattleUnit unit = UnitAtMoat();

        Assert.Null(Mask(unit, spriteArea: new Rect(320, 200, 60, 90)));
    }

    /// <summary>Leaving the moat diagonally lifts the feet already, so nothing is cut.</summary>
    [Fact]
    public void LeavingTheMoatDiagonallyIsNotCut()
    {
        BattleUnit unit = UnitLeavingMoat(CellDirection.TopRight);

        Assert.Null(Mask(unit, direction: CellDirection.TopRight));
    }

    /// <summary>Entering the moat from the bottom left is cut: the water is in front of the sprite.</summary>
    [Fact]
    public void EnteringTheMoatFromTheBottomLeftIsCut()
    {
        BattleUnit unit = UnitEnteringMoat(CellDirection.BottomLeft);

        Assert.Equal(new Rect(334, 306, 41, 10), Mask(unit, direction: CellDirection.BottomLeft));
    }

    /// <summary>Entering the moat from the top right is not cut: the water is behind the sprite.</summary>
    [Fact]
    public void EnteringTheMoatFromTheTopRightIsNotCut()
    {
        BattleUnit unit = UnitEnteringMoat(CellDirection.TopRight);

        Assert.Null(Mask(unit, direction: CellDirection.TopRight));
    }

    /// <summary>The three visible slices tile the sprite minus the water band.</summary>
    [Fact]
    public void TheVisiblePiecesSurroundTheWater()
    {
        BattleMoatMasking.SpritePieces pieces =
            BattleMoatMasking.GetVisiblePieces(new Rect(320, 240, 60, 90), new Rect(334, 306, 41, 10));

        Assert.Equal(new Rect(0, 0, 60, 66), pieces.Top);
        Assert.Equal(new Rect(0, 66, 14, 10), pieces.Left);
        Assert.Equal(new Rect(55, 66, 5, 10), pieces.Right);

        // 三块面积之和 = 整幅减去水带，再减去水带之下（脚泡在水里）的那一段。
        int visible = pieces.Top.Width * pieces.Top.Height + (pieces.Left.Width * pieces.Left.Height)
            + (pieces.Right.Width * pieces.Right.Height);
        int belowWater = 60 * (90 - 66 - 10);

        Assert.Equal((60 * 90) - (41 * 10) - belowWater, visible);
    }

    /// <summary>Water as wide as the sprite leaves no side strips at all.</summary>
    [Fact]
    public void WaterAsWideAsTheSpriteLeavesNoSideStrips()
    {
        BattleMoatMasking.SpritePieces pieces =
            BattleMoatMasking.GetVisiblePieces(new Rect(334, 300, 41, 60), new Rect(334, 306, 41, 10));

        Assert.Equal(new Rect(0, 0, 41, 6), pieces.Top);
        Assert.True(pieces.Left.IsEmpty);
        Assert.True(pieces.Right.IsEmpty);
    }

    private static BattleUnit UnitAtMoat() => UnitAt(MoatCell);

    /// <summary>A unit on dry land whose next diagonal step lands in the moat.</summary>
    /// <param name="direction">The direction of the step; the caller passes the same one to <see cref="Mask"/>.</param>
    private static BattleUnit UnitEnteringMoat(CellDirection direction)
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (!BattleBoard.IsMoatIndex(index, false, false)
                && BattleBoard.GetIndexDirection(index, direction) == MoatCell)
            {
                return UnitAt(index);
            }
        }

        throw new InvalidOperationException("No dry cell steps into the moat that way.");
    }

    /// <summary>A unit in the moat whose next diagonal step leaves it.</summary>
    /// <param name="direction">The direction of the step; the caller passes the same one to <see cref="Mask"/>.</param>
    private static BattleUnit UnitLeavingMoat(CellDirection direction)
    {
        foreach (int moat in new[] { MoatCell, 72, 84, 95 })
        {
            int next = BattleBoard.GetIndexDirection(moat, direction);

            if (BattleBoard.IsValidIndex(next) && !BattleBoard.IsMoatIndex(next, false, false))
            {
                return UnitAt(moat);
            }
        }

        throw new InvalidOperationException("No moat cell is left that way.");
    }

    private static BattleUnit UnitAt(int head, bool wide = false, bool reflect = false)
    {
        BattleBoardCells board = Board();
        BattlePosition position = new(board, head, wide, reflect);

        return new BattleUnit(new Troop(MonsterType.Peasant, 1), position, reflect, 1);
    }

    private static BattleBoardCells Board()
    {
        BattleBoardCells board = new();
        board.SetArea(new Rect(0, 0, 640, 480));

        return board;
    }

    private static Rect? Mask(
        BattleUnit unit,
        bool hasBuiltMoat = true,
        bool isFlyingInMotion = false,
        CellDirection direction = CellDirection.Center,
        Point? movementDelta = null,
        Rect? spriteArea = null) =>
        BattleMoatMasking.GetMask(
            unit.Position.Board,
            unit,
            bridge: null,
            hasBuiltMoat,
            isFlyingInMotion,
            direction,
            movementDelta ?? Point.Zero,
            spriteArea ?? new Rect(320, 240, 60, 90));
}
