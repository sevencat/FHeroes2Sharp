using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="Mp2MapHeroes"/>, the rule that turns a tile into a hero, and the collection that
/// <see cref="Mp2Map.Read"/> builds from it.
/// </summary>
public sealed class Mp2MapHeroesTests
{
    [Fact]
    public void SpriteIndex_EncodesKingdomAndRace()
    {
        // The MINIHERO set holds seven frames per kingdom, one per race, and the kingdoms follow the
        // order of the game: blue, green, red, yellow, orange, purple.
        Mp2MapHero hero = Mp2MapHeroes.FromSpriteIndex(0, 3, 4);
        Assert.Equal(PlayerColor.Blue, hero.Color);
        Assert.Equal(MapRace.Knight, hero.Race);
        Assert.Equal(3, hero.X);
        Assert.Equal(4, hero.Y);

        hero = Mp2MapHeroes.FromSpriteIndex((5 * 7) + 5, 0, 0);
        Assert.Equal(PlayerColor.Purple, hero.Color);
        Assert.Equal(MapRace.Necromancer, hero.Race);
    }

    [Theory]
    [InlineData(0, MapRace.Knight)]
    [InlineData(1, MapRace.Barbarian)]
    [InlineData(2, MapRace.Sorceress)]
    [InlineData(3, MapRace.Warlock)]
    [InlineData(4, MapRace.Wizard)]
    [InlineData(5, MapRace.Necromancer)]
    [InlineData(6, MapRace.Random)]
    public void SpriteIndex_SeventhFrameOfAKingdomIsARandomHero(int raceFrame, MapRace expected)
    {
        Assert.Equal(expected, Mp2MapHeroes.FromSpriteIndex(raceFrame, 0, 0).Race);
    }

    [Fact]
    public void SpriteIndex_BeyondTheSixKingdomsIsPurple()
    {
        // The reference clamps with an if-else chain that ends on purple, which is what the editor
        // writes for a hero of a kingdom that was deleted from the map.
        Assert.Equal(PlayerColor.Purple, Mp2MapHeroes.FromSpriteIndex(42, 0, 0).Color);
        Assert.Equal(PlayerColor.Purple, Mp2MapHeroes.FromSpriteIndex(255, 0, 0).Color);
    }

    [Fact]
    public void SpriteIndex_RejectsNegativeFrames()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Mp2MapHeroes.FromSpriteIndex(-1, 0, 0));
    }

    [Theory]
    [InlineData(Mp2ObjectType.Hero)]
    [InlineData(Mp2ObjectType.ActionHero)]
    public void TryRead_AcceptsBothHeroTypes(Mp2ObjectType objectType)
    {
        Mp2TileInfo tile = HeroTile(objectType);

        Assert.True(Mp2MapHeroes.TryRead(tile, 7, 9, out Mp2MapHero hero));
        Assert.Equal(7, hero.X);
        Assert.Equal(9, hero.Y);
        Assert.Equal(PlayerColor.Green, hero.Color);
        Assert.Equal(MapRace.Knight, hero.Race);
    }

    [Fact]
    public void TryRead_RejectsTilesThatAreNotHeroes()
    {
        // The hero is the main object of its tile, so the sprite index alone is not enough.
        Mp2TileInfo tile = HeroTile(Mp2ObjectType.None);

        Assert.False(Mp2MapHeroes.TryRead(tile, 0, 0, out _));
    }

    [Fact]
    public void TryRead_RejectsATileWhoseSpriteIsNotAHero()
    {
        Mp2TileInfo tile = HeroTile(Mp2ObjectType.Hero) with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnGras << 2),
        };

        Assert.False(Mp2MapHeroes.TryRead(tile, 0, 0, out _));
    }

    [Fact]
    public void TryRead_RejectsAnEmptySpriteIndex()
    {
        Mp2TileInfo tile = HeroTile(Mp2ObjectType.Hero) with
        {
            BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        };

        Assert.False(Mp2MapHeroes.TryRead(tile, 0, 0, out _));
    }

    [Fact]
    public void Read_CollectsTheHeroesOfTheMap()
    {
        Mp2MapFileBuilder builder = new(36);
        builder.Tiles[(4 * 36) + 3] = HeroTile(Mp2ObjectType.Hero);
        builder.Tiles[(9 * 36) + 30] = HeroTile(Mp2ObjectType.ActionHero);

        using TemporaryMapFile file = TemporaryMapFile.Create(builder);
        Mp2Map map = Mp2Map.Read(file.FilePath);

        Assert.Equal(2, map.Heroes.Count);

        // The tiles are walked row by row, so the hero of the upper row comes first.
        Assert.Equal(3, map.Heroes[0].X);
        Assert.Equal(4, map.Heroes[0].Y);
        Assert.Equal(30, map.Heroes[1].X);
        Assert.Equal(9, map.Heroes[1].Y);
    }

    /// <summary>A hero tile: the MINIHERO frame 7 is a green knight.</summary>
    private static Mp2TileInfo HeroTile(Mp2ObjectType objectType) =>
        default(Mp2TileInfo) with
        {
            TerrainImageIndex = 1,
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
            BottomIcnImageIndex = 7,
            ObjectType = objectType,
        };
}
