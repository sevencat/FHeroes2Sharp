using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Tile::getTileIndependentPassability()</c> and <c>Tile::setInitialPassability()</c>
/// (<c>src/fheroes2/maps/maps_tiles.cpp:583</c>). The expectations are the reference's: an object on the
/// object layer blocks from the middle row down, terrain and shadow parts never block, action objects
/// use their action direction, and reefs are completely impassable.
/// </summary>
public sealed class TilePassabilityTests
{
    [Fact]
    public void ATileWithoutObjectsIsFullyPassable()
    {
        var tile = new Tile();

        Assert.Equal(Directions.All, tile.GetTileIndependentPassability());
    }

    [Theory]
    [InlineData(Mp2ObjectIcnType.Road, 0)]
    [InlineData(Mp2ObjectIcnType.Road, 12)]
    [InlineData(Mp2ObjectIcnType.Stream, 0)]
    [InlineData(Mp2ObjectIcnType.Flag32, 17)]
    public void RoadsStreamsAndFlagsAreFullyPassable(Mp2ObjectIcnType icnType, byte icnIndex)
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, icnType, icnIndex));

        Assert.Equal(Directions.All, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void AnObjectPartOnTheObjectLayerBlocksTheMiddleAndBottomRows()
    {
        var tile = new Tile();

        // Sprite 255 of OBJNCRCK carries no object, so only the "is it a shadow" test decides.
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 255));

        Assert.Equal(Directions.CenterRow | Directions.BottomRow, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void TheMainObjectPartIsCheckedAsWell()
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 255));
        tile.SortObjectParts();

        Assert.Equal(1U, tile.MainObjectPart.Uid);
        Assert.Empty(tile.GroundObjectParts);
        Assert.Equal(Directions.CenterRow | Directions.BottomRow, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void TerrainLayerPartsNeverBlock()
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 1, Mp2ObjectIcnType.ObjnCrck, 255));

        Assert.Equal(Directions.All, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void ShadowSpritesNeverBlockEvenOnTheObjectLayer()
    {
        var tile = new Tile();

        // OBJNCRCK sprite 2 is a shadow sprite; the object tables store it on the shadow layer.
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 2));

        Assert.Equal(Directions.All, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void ActionObjectsUseTheirActionDirection()
    {
        // OBJNRSRC sprite 1 is the wood resource: an action object that can be picked up from any side.
        var passable = new Tile();
        passable.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnRsrc, 1));
        Assert.Equal(Directions.All, passable.GetTileIndependentPassability());

        // A mine can only be entered from the bottom, which is the same mask as a blocking sprite, so the
        // interesting part of this test is that the action object branch is taken at all: sprite 5 of
        // OBJNMULT is the mine's shadow and carries no object, sprite 4 belongs to the mine.
        var mine = new Tile();
        mine.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnMult, 4));
        Assert.Equal(Directions.CenterRow | Directions.BottomRow, mine.GetTileIndependentPassability());
    }

    [Fact]
    public void TheMainPartWinsOverTheGroundParts()
    {
        var tile = new Tile();

        // The blocking part is added first, so the object layer part pushed last is promoted to the main
        // part. The action object is found there and ends the walk: the blocking part is never consulted.
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 255));
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 2, Mp2ObjectIcnType.ObjnRsrc, 1));
        tile.SortObjectParts();

        Assert.Equal(2U, tile.MainObjectPart.Uid);
        Assert.Equal(Directions.All, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void ReefsAreCompletelyImpassable()
    {
        var tile = new Tile();

        // X_LOC2 sprite 113 is the first reef sprite of the object tables.
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Background, 1, Mp2ObjectIcnType.XLoc2, 113));

        Assert.Equal(Direction.Unknown, tile.GetTileIndependentPassability());
    }

    [Fact]
    public void SetInitialPassabilityStoresTheTileIndependentMask()
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 255));

        Assert.Equal(Directions.All, tile.PassableDirections);

        tile.SetInitialPassability();

        Assert.Equal(Directions.CenterRow | Directions.BottomRow, tile.PassableDirections);
        Assert.True(tile.IsPassableFrom(Direction.Bottom));
        Assert.False(tile.IsPassableFrom(Direction.Top));
    }

    [Fact]
    public void IsStreamFollowsTheTwoSpriteRanges()
    {
        var stream = new Tile();
        stream.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 1, Mp2ObjectIcnType.Stream, 0));
        Assert.True(stream.IsStream);

        // The original editor stored a few water pieces of OBJNMUL2 as streams.
        var editorWater = new Tile();
        editorWater.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 1, Mp2ObjectIcnType.ObjnMul2, 5));
        Assert.True(editorWater.IsStream);

        var editorWaterHigh = new Tile();
        editorWaterHigh.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 1, Mp2ObjectIcnType.ObjnMul2, 220));
        Assert.True(editorWaterHigh.IsStream);

        var notStream = new Tile();
        notStream.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 1, Mp2ObjectIcnType.ObjnMul2, 20));
        Assert.False(notStream.IsStream);
    }

    [Fact]
    public void IsShadowNeedsEveryPartToBeAShadow()
    {
        var shadowsOnly = new Tile();
        shadowsOnly.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Shadow, 1, Mp2ObjectIcnType.ObjnCrck, 2));
        shadowsOnly.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Shadow, 2, Mp2ObjectIcnType.ObjnGras, 4));
        shadowsOnly.SortObjectParts();

        Assert.True(shadowsOnly.IsShadow);

        shadowsOnly.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 3, Mp2ObjectIcnType.ObjnCrck, 255));
        Assert.False(shadowsOnly.IsShadow);
    }
}
