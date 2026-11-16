using FHeroes2.Core.Random;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Kingdom;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the obelisk jigsaw, <see cref="Puzzle"/> (<c>puzzle.h:33-55</c>, <c>puzzle.cpp:303-339</c>), and the kingdom
/// that owns one (<c>kingdom.cpp:134</c>).
/// </summary>
public sealed class PuzzleTests
{
    /// <summary>The reference's <c>zone3_order</c> and <c>zone4_order</c> merged (<c>puzzle.h:43-44</c>).</summary>
    private static readonly int[] InnerBlocks = [14, 15, 32, 33, 20, 21, 26, 27];

    [Fact]
    public void AFreshPuzzleHidesEverything()
    {
        Puzzle puzzle = new(new Pcg32(1));

        Assert.Equal(0, puzzle.Count);

        for (int tile = 0; tile < Puzzle.NumOfPuzzleTiles; ++tile)
        {
            Assert.False(puzzle[tile]);
        }
    }

    [Fact]
    public void NoObelisksOpenNothing()
    {
        Puzzle puzzle = new(new Pcg32(1));

        puzzle.Update(0, 6);
        Assert.Equal(0, puzzle.Count);

        // A map without obelisks divides by zero in the reference; the port has nothing to uncover.
        puzzle.Update(5, 0);
        Assert.Equal(0, puzzle.Count);
    }

    [Fact]
    public void AllTheObelisksOpenTheWholePicture()
    {
        Puzzle puzzle = new(new Pcg32(1));

        puzzle.Update(6, 6);

        Assert.Equal(Puzzle.NumOfPuzzleTiles, puzzle.Count);
    }

    [Fact]
    public void TheShareIsTruncatedAndCountedFromWhatIsAlreadyVisible()
    {
        // 1 * 48 / 48 = 1 tile; 1 * 48 / 5 = 9 (truncated from 9.6); 49 * 48 / 48 is clamped by the picture itself.
        Puzzle puzzle = new(new Pcg32(1));
        puzzle.Update(1, 48);
        Assert.Equal(1, puzzle.Count);

        // Visiting the same obelisk again asks for the same share, so nothing is added.
        puzzle.Update(1, 48);
        Assert.Equal(1, puzzle.Count);

        Puzzle other = new(new Pcg32(1));
        other.Update(1, 5);
        Assert.Equal(9, other.Count);

        other.Update(49, 48);
        Assert.Equal(Puzzle.NumOfPuzzleTiles, other.Count);
    }

    [Fact]
    public void TheFrameAppearsBeforeTheMiddle()
    {
        // 24 of the 48 tiles form zone 1, so half the obelisks must fill exactly the frame and leave the two inner
        // 2×2 blocks hidden (puzzle.h:41-44). Which tiles of the frame appear is the shuffle's business; that they
        // are 24 and that none of the inner blocks is touched is the algorithm's.
        Puzzle puzzle = new(new Pcg32(7));

        puzzle.Update(24, 48);

        Assert.Equal(24, puzzle.Count);

        foreach (int tile in InnerBlocks)
        {
            Assert.False(puzzle[tile]);
        }
    }

    [Fact]
    public void LosingAnObeliskDoesNotTakeThePictureBack()
    {
        Puzzle puzzle = new(new Pcg32(3));
        puzzle.Update(30, 48);
        int earned = puzzle.Count;

        Assert.Equal(30, earned);

        puzzle.Update(10, 48);

        Assert.Equal(earned, puzzle.Count);
    }

    [Fact]
    public void ResetHidesEverythingAndKeepsTheLayout()
    {
        // Kingdom::clear() resets the bitset only (kingdom.cpp:134); the four zone orders keep their shuffle, so the
        // same obelisk count uncovers the same tiles again.
        Puzzle puzzle = new(new Pcg32(11));
        puzzle.Update(5, 12);

        bool[] before = new bool[Puzzle.NumOfPuzzleTiles];
        for (int tile = 0; tile < Puzzle.NumOfPuzzleTiles; ++tile)
        {
            before[tile] = puzzle[tile];
        }

        puzzle.Reset();
        Assert.Equal(0, puzzle.Count);

        puzzle.Update(5, 12);

        for (int tile = 0; tile < Puzzle.NumOfPuzzleTiles; ++tile)
        {
            Assert.Equal(before[tile], puzzle[tile]);
        }
    }

    [Fact]
    public void AKingdomKeepsAHiddenPuzzleAndForgetsItOnClear()
    {
        PlayerKingdom kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        Assert.Equal(0, kingdom.GetPuzzleMaps().Count);

        kingdom.GetPuzzleMaps().Update(48, 48);
        Assert.Equal(Puzzle.NumOfPuzzleTiles, kingdom.GetPuzzleMaps().Count);

        kingdom.Clear();
        Assert.Equal(0, kingdom.GetPuzzleMaps().Count);
    }
}
