using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <c>fheroes2::drawMiniMonsters()</c> (<c>army_ui_helper.cpp:36</c>) and the two army entry points around it
/// (<c>army.cpp:1605-1639</c>).
/// </summary>
/// <remarks>
/// The portraits are 32 by 32 images of one palette index each, so a test can tell which creature was drawn where by
/// the value of the pixel. No archive is needed: <see cref="IMiniMonsterSprites"/> is what the caller supplies.
/// </remarks>
public sealed class MiniMonstersTests
{
    private const byte PeasantColor = 1;
    private const byte ArcherColor = 2;
    private const byte SwordsmanColor = 3;

    [Fact]
    public void ALineOfThreeIsCutIntoThreeEqualChunks()
    {
        ArmyStack army = new();
        army.JoinTroop(MonsterType.Peasant, 10);
        army.JoinTroop(MonsterType.Archer, 20);
        army.JoinTroop(MonsterType.Swordsman, 30);

        PixelBuffer output = new(300, 60);

        // No window and first == 0: the stacks are drawn right to left, so the last one lands on the right.
        MiniMonsters.Draw(army, 20, 0, 300, 0, 0, false, true, false, 0, output, CreateSprites());

        // The line is shifted by half a chunk, then every portrait is centred in its own 100 pixel chunk.
        Assert.Equal(PeasantColor, output[65, 5]);
        Assert.Equal(ArcherColor, output[165, 5]);
        Assert.Equal(SwordsmanColor, output[265, 5]);
    }

    [Fact]
    public void AWindowOfTheLineCountsItsChunksFromTheRight()
    {
        ArmyStack army = new();
        army.JoinTroop(MonsterType.Peasant, 10);
        army.JoinTroop(MonsterType.Archer, 20);
        army.JoinTroop(MonsterType.Swordsman, 30);

        PixelBuffer output = new(300, 60);

        // first == 2, count == 1: the rooms of the first two stacks are empty, and the third keeps its place.
        MiniMonsters.Draw(army, 20, 0, 300, 2, 1, false, true, false, 0, output, CreateSprites());

        // One chunk as wide as the line: the shown stack keeps the right end of it.
        Assert.Equal(0, output[65, 5]);
        Assert.Equal(0, output[265, 5]);
        Assert.Equal(SwordsmanColor, output[170, 5]);
    }

    [Fact]
    public void AnEmptyArmyDrawsNothing()
    {
        PixelBuffer output = new(100, 40);

        MiniMonsters.Draw(new ArmyStack(), 0, 0, 100, 0, 0, false, true, false, 0, output, CreateSprites());

        Assert.DoesNotContain(output.AsSpan().ToArray(), value => value != 0);
    }

    [Fact]
    public void AMissingPortraitSkipsItsStackInsteadOfThrowing()
    {
        ArmyStack army = new();
        army.JoinTroop(MonsterType.Peasant, 10);
        army.JoinTroop(MonsterType.Archer, 20);

        PixelBuffer output = new(200, 40);

        MiniMonsters.Draw(army, 0, 0, 200, 0, 0, false, true, false, 0, output, new FixedSprites());

        Assert.DoesNotContain(output.AsSpan().ToArray(), value => value != 0);
    }

    [Fact]
    public void FourStacksAreDrawnOnTwoLines()
    {
        ArmyStack army = new();
        army.JoinTroop(MonsterType.Peasant, 10);
        army.JoinTroop(MonsterType.Archer, 20);
        army.JoinTroop(MonsterType.Swordsman, 30);
        army.JoinTroop(MonsterType.Cavalry, 40);

        PixelBuffer output = new(320, 100);

        ArmyStack.DrawMultipleMonsterLines(army, 20, 0, 300, false, true, false, 0, output, CreateSprites());

        // The first two stacks share the top line, the rest the one below it.
        Assert.Equal(PeasantColor, output[115, 5]);
        Assert.Equal(ArcherColor, output[215, 5]);
        Assert.Equal(SwordsmanColor, output[65, 50]);
        Assert.Equal(CavalryColor, output[165, 50]);
    }

    [Fact]
    public void TwoStacksStayOnOneLine()
    {
        ArmyStack army = new();
        army.JoinTroop(MonsterType.Peasant, 10);
        army.JoinTroop(MonsterType.Archer, 20);

        PixelBuffer output = new(320, 100);

        ArmyStack.DrawMultipleMonsterLines(army, 20, 0, 300, false, true, false, 0, output, CreateSprites());

        // One line centred: offsetX = 50 and the line is dropped by offsetY / 2 + 1 = 25.
        Assert.Equal(PeasantColor, output[115, 30]);
        Assert.Equal(ArcherColor, output[215, 30]);

        // Nothing is drawn on the second line.
        for (int y = 49; y < 100; ++y)
        {
            Assert.Equal(0, output[10, y]);
        }
    }

    private const byte CavalryColor = 4;

    private static FixedSprites CreateSprites() => new FixedSprites()
        .Add(MonsterType.Peasant, PeasantColor)
        .Add(MonsterType.Archer, ArcherColor)
        .Add(MonsterType.Swordsman, SwordsmanColor)
        .Add(MonsterType.Cavalry, CavalryColor);

    /// <summary>Portraits of a single palette index each, so a test can tell them apart by pixel.</summary>
    private sealed class FixedSprites : IMiniMonsterSprites
    {
        private readonly Dictionary<MonsterType, Sprite> _sprites = [];

        public FixedSprites Add(MonsterType monster, byte color)
        {
            Sprite sprite = new(32, 32);
            ImageOps.FillSprite(sprite, sprite.Image.Bounds, color);
            _sprites[monster] = sprite;

            return this;
        }

        public Sprite? GetMiniMonster(MonsterType monster) =>
            _sprites.TryGetValue(monster, out Sprite? sprite) ? sprite : null;
    }
}
