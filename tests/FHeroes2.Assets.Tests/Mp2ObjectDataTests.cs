using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="Mp2ObjectData"/>: which sprite names a resource, which object keeps its data
/// word and what the information box says about a word the game ignores.
/// </summary>
public sealed class Mp2ObjectDataTests
{
    [Fact]
    public void OnlyTheSecondFrameOfAPileIsAResource()
    {
        Assert.Equal(Mp2ResourceType.None, Mp2ObjectData.ResourceFromSpriteIndex(0));

        Mp2ResourceType[] expected =
        [
            Mp2ResourceType.Wood,
            Mp2ResourceType.Mercury,
            Mp2ResourceType.Ore,
            Mp2ResourceType.Sulfur,
            Mp2ResourceType.Crystal,
            Mp2ResourceType.Gems,
            Mp2ResourceType.Gold,
        ];

        for (int step = 0; step < expected.Length; ++step)
        {
            int frame = (step * 2) + 1;
            Assert.Equal(expected[step], Mp2ObjectData.ResourceFromSpriteIndex(frame));
            Assert.Equal(Mp2ResourceType.None, Mp2ObjectData.ResourceFromSpriteIndex(frame + 1));
        }
    }

    [Fact]
    public void TheResourceOfATileComesFromItsMainSprite()
    {
        Assert.Equal(
            Mp2ResourceType.Gold,
            Mp2ObjectData.ResourceOf(Tile(Mp2ObjectType.ActionResource, top: Mp2ObjectIcnType.ObjnRsrc, topIndex: 13)));

        Assert.Equal(
            Mp2ResourceType.Wood,
            Mp2ObjectData.ResourceOf(Tile(Mp2ObjectType.ActionResource, ground: Mp2ObjectIcnType.ObjnRsrc, groundIndex: 1)));

        // The main object wins over the ground object, the order the reference tries them in.
        Assert.Equal(
            Mp2ResourceType.Gems,
            Mp2ObjectData.ResourceOf(Tile(
                Mp2ObjectType.ActionResource,
                ground: Mp2ObjectIcnType.ObjnRsrc,
                groundIndex: 1,
                top: Mp2ObjectIcnType.ObjnRsrc,
                topIndex: 11)));

        Assert.Equal(
            Mp2ResourceType.None,
            Mp2ObjectData.ResourceOf(Tile(Mp2ObjectType.ActionResource, ground: Mp2ObjectIcnType.MtnMult, groundIndex: 81)));
    }

    [Fact]
    public void AMineIsNamedByItsOverlay()
    {
        Mp2ResourceType[] expected =
        [
            Mp2ResourceType.Ore,
            Mp2ResourceType.Sulfur,
            Mp2ResourceType.Crystal,
            Mp2ResourceType.Gems,
            Mp2ResourceType.Gold,
        ];

        for (int frame = 0; frame < expected.Length; ++frame)
        {
            Assert.Equal(
                expected[frame],
                Mp2ObjectData.MineOf(Tile(Mp2ObjectType.ActionMine, top: Mp2ObjectIcnType.ExtraOvr, topIndex: frame)));
        }

        Assert.Equal(
            Mp2ResourceType.None,
            Mp2ObjectData.MineOf(Tile(Mp2ObjectType.ActionMine, top: Mp2ObjectIcnType.ExtraOvr, topIndex: 5)));

        Assert.Equal(
            Mp2ResourceType.None,
            Mp2ObjectData.MineOf(Tile(Mp2ObjectType.ActionMine, ground: Mp2ObjectIcnType.MtnMult, groundIndex: 81)));
    }

    [Fact]
    public void OnlyMonstersKeepTheWordAsATroopCount()
    {
        Assert.True(Mp2ObjectData.CarriesTroopCount(Mp2ObjectType.ActionMonster));
        Assert.True(Mp2ObjectData.CarriesTroopCount(Mp2ObjectType.ActionRandomMonsterVeryStrong));

        Assert.False(Mp2ObjectData.CarriesTroopCount(Mp2ObjectType.ActionResource));
        Assert.False(Mp2ObjectData.CarriesTroopCount(Mp2ObjectType.ActionMine));
        Assert.False(Mp2ObjectData.CarriesTroopCount(Mp2ObjectType.ActionTreasureChest));
        Assert.False(Mp2ObjectData.CarriesTroopCount(Mp2ObjectType.Monster));
        Assert.False(Mp2ObjectData.CarriesTroopCount(Mp2ObjectType.None));

        // Whatever the reference keeps a word for, the troop count is not among the objects that only
        // carry one incidentally.
        Assert.True(Mp2ObjectTypes.ContainsMetadata(Mp2ObjectType.ActionMonster));
    }

    [Fact]
    public void TheDescriptionOfATroopStackIsItsCount()
    {
        string text = Mp2ObjectData.Describe(
            Tile(Mp2ObjectType.ActionMonster, ground: Mp2ObjectIcnType.Mons32, groundIndex: 4, data: 25));

        Assert.Equal("the word is the troop count: 25 monsters", text);
    }

    [Fact]
    public void TheDescriptionOfAPileComesFromItsSprite()
    {
        Assert.Equal(
            "the sprite shows wood, the amount is rolled when the map starts",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.ActionResource, ground: Mp2ObjectIcnType.ObjnRsrc, groundIndex: 1, data: 300)));

        Assert.StartsWith(
            "the sprite names no resource",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.ActionResource, ground: Mp2ObjectIcnType.ObjnRsrc, groundIndex: 17)),
            StringComparison.Ordinal);

        Assert.StartsWith(
            "a random pile",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.ActionRandomResource, ground: Mp2ObjectIcnType.ObjnRsrc, groundIndex: 17)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheDescriptionOfAMineComesFromItsOverlay()
    {
        Assert.Equal(
            "the overlay shows the ore mine",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.ActionMine, top: Mp2ObjectIcnType.ExtraOvr, topIndex: 0)));

        Assert.StartsWith(
            "no overlay sprite",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.ActionMine, ground: Mp2ObjectIcnType.MtnMult, groundIndex: 81)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheDescriptionOfAChestSaysTheContentsAreRolled()
    {
        Assert.Equal(
            "the gold or the experience is rolled when the map starts",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.ActionTreasureChest, top: Mp2ObjectIcnType.ObjnArti, topIndex: 0)));
    }

    [Fact]
    public void TheDescriptionOfAWordTheGameClearsSaysSo()
    {
        Assert.Equal(
            "the word means nothing to this object, the game clears it",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.Mine, ground: Mp2ObjectIcnType.MtnMult, groundIndex: 81, data: 7)));

        Assert.Equal(
            "the word is metadata this build does not read yet",
            Mp2ObjectData.Describe(Tile(Mp2ObjectType.ActionArtifact, top: Mp2ObjectIcnType.ObjnArti, topIndex: 0, data: 12)));
    }

    [Fact]
    public void TheDescriptionOfBareGroundHasNoWord()
    {
        Assert.Equal("bare ground, no object to carry a word", Mp2ObjectData.Describe(Tile(Mp2ObjectType.None)));
    }

    [Fact]
    public void ResourcesAreNamedTheWayTheReferencePrintsThem()
    {
        Assert.Equal("wood", Mp2ObjectData.Name(Mp2ResourceType.Wood));
        Assert.Equal("mercury", Mp2ObjectData.Name(Mp2ResourceType.Mercury));
        Assert.Equal("ore", Mp2ObjectData.Name(Mp2ResourceType.Ore));
        Assert.Equal("sulfur", Mp2ObjectData.Name(Mp2ResourceType.Sulfur));
        Assert.Equal("crystal", Mp2ObjectData.Name(Mp2ResourceType.Crystal));
        Assert.Equal("gems", Mp2ObjectData.Name(Mp2ResourceType.Gems));
        Assert.Equal("gold", Mp2ObjectData.Name(Mp2ResourceType.Gold));
        Assert.Equal("nothing", Mp2ObjectData.Name(Mp2ResourceType.None));
        Assert.Equal(0x7F, (int)Mp2ResourceType.All);
    }

    /// <summary>Builds the tile a test talks about, with the data word split the way a file splits it.</summary>
    private static Mp2TileInfo Tile(
        Mp2ObjectType type,
        Mp2ObjectIcnType ground = Mp2ObjectIcnType.Unknown,
        int groundIndex = 255,
        Mp2ObjectIcnType top = Mp2ObjectIcnType.Unknown,
        int topIndex = 255,
        int data = 0)
    {
        Assert.InRange(data, 0, 0x1FFF);

        return new Mp2TileInfo
        {
            TerrainImageIndex = 0,
            ObjectName1 = (byte)((int)ground << 2),
            BottomIcnImageIndex = (byte)groundIndex,
            Quantity1 = (byte)((data & 0x1F) << 3),
            Quantity2 = (byte)(data >> 5),
            ObjectName2 = (byte)((int)top << 2),
            TopIcnImageIndex = (byte)topIndex,
            TerrainFlags = 0,
            ObjectType = type,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };
    }
}
