using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the first slice of <c>Maps::Tile</c> (<c>src/fheroes2/maps/maps_tiles.cpp</c>): the object
/// parts, the object type cache, the terrain fields, the road bit and the passability mask. Every
/// expectation is read off the reference, so a future refactor of the tile cannot quietly change which
/// sprite ends up as the main object of a tile.
/// </summary>
public sealed class TileTests
{
    private static readonly uint GroundUid = 100;
    private static readonly uint TopUid = 200;

    [Fact]
    public void ObjectPartDefaultsMatchTheReference()
    {
        var part = new ObjectPart();

        Assert.Equal(0U, part.Uid);
        Assert.Equal(ObjectLayerType.Object, part.LayerType);
        Assert.Equal(Mp2ObjectIcnType.Unknown, part.IcnType);
        Assert.Equal(255, part.IcnIndex);
    }

    [Theory]
    [InlineData(ObjectLayerType.Shadow, true)]
    [InlineData(ObjectLayerType.Terrain, true)]
    [InlineData(ObjectLayerType.Object, false)]
    [InlineData(ObjectLayerType.Background, false)]
    public void ObjectPartIsPassabilityTransparentOnlyOnTheShadowAndTerrainLayers(ObjectLayerType layer, bool transparent) =>
        Assert.Equal(transparent, new ObjectPart(layer, 1, Mp2ObjectIcnType.Road, 0).IsPassabilityTransparent);

    [Fact]
    public void ObjectPartComparesByValue()
    {
        var part = new ObjectPart(ObjectLayerType.Object, 7, Mp2ObjectIcnType.Road, 3);

        Assert.Equal(part, new ObjectPart(ObjectLayerType.Object, 7, Mp2ObjectIcnType.Road, 3));
        Assert.NotEqual(part, new ObjectPart(ObjectLayerType.Object, 7, Mp2ObjectIcnType.Road, 4));
        Assert.NotEqual(part, new ObjectPart(ObjectLayerType.Terrain, 7, Mp2ObjectIcnType.Road, 3));
    }

    [Fact]
    public void InitCopiesMetadataAndObjectType()
    {
        var tile = new Tile();

        tile.Init(TileInfo(quantity1: 0, quantity2: 1, objectType: Mp2ObjectType.Mine));

        // metadata = ((( quantity2 << 8 ) + quantity1 ) >> 3) of MP2TileInfo.
        Assert.Equal(32U, tile.Metadata[0]);
        Assert.Equal(Mp2ObjectType.Mine, tile.MainObjectType);

        // The reference does not touch the terrain here: world_loadmap.cpp calls setTerrain separately.
        Assert.Equal(0, tile.TerrainImageIndex);
        Assert.Empty(tile.GroundObjectParts);
        Assert.Empty(tile.TopObjectParts);
    }

    [Fact]
    public void InitPushesTheGroundPartWithTheLayerOfItsLowBits()
    {
        var tile = new Tile();

        tile.Init(TileInfo(groundIcn: Mp2ObjectIcnType.ObjnCrck, groundIndex: 4, groundUid: GroundUid, groundLayer: 2));

        ObjectPart part = Assert.Single(tile.GroundObjectParts);
        Assert.Equal(GroundUid, part.Uid);
        Assert.Equal(Mp2ObjectIcnType.ObjnCrck, part.IcnType);
        Assert.Equal(4, part.IcnIndex);
        Assert.Equal(ObjectLayerType.Shadow, part.LayerType);
    }

    [Fact]
    public void InitPushesTheTopPartOnTheObjectLayer()
    {
        var tile = new Tile();

        tile.Init(TileInfo(topIcn: Mp2ObjectIcnType.ObjnTown, topIndex: 45, topUid: TopUid));

        ObjectPart part = Assert.Single(tile.TopObjectParts);
        Assert.Equal(TopUid, part.Uid);
        Assert.Equal(ObjectLayerType.Object, part.LayerType);
        Assert.Empty(tile.GroundObjectParts);
    }

    [Fact]
    public void InitKeepsTheCrackSpriteOnTheGroundLayer()
    {
        // The reference moves OBJNCRCK sprite 226 to the ground layer with the terrain layer type to
        // reproduce the original game's rendering bug (maps_tiles.cpp:420).
        var tile = new Tile();

        tile.Init(TileInfo(topIcn: Mp2ObjectIcnType.ObjnCrck, topIndex: 226, topUid: TopUid));

        Assert.Empty(tile.TopObjectParts);
        ObjectPart part = Assert.Single(tile.GroundObjectParts);
        Assert.Equal(ObjectLayerType.Terrain, part.LayerType);
        Assert.Equal(TopUid, part.Uid);
        Assert.Equal(226, part.IcnIndex);
    }

    [Fact]
    public void InitSetsTheRoadBitFromTheGroundSprite()
    {
        Tile road = new();
        road.Init(TileInfo(groundIcn: Mp2ObjectIcnType.Road, groundIndex: 0));
        Assert.True(road.IsRoad);

        Tile notARoad = new();
        notARoad.Init(TileInfo(groundIcn: Mp2ObjectIcnType.Road, groundIndex: 1));
        Assert.False(notARoad.IsRoad);
    }

    [Theory]
    [InlineData(Mp2ObjectIcnType.Road, 0, true)]
    [InlineData(Mp2ObjectIcnType.Road, 1, false)]
    [InlineData(Mp2ObjectIcnType.Road, 48, true)]
    [InlineData(Mp2ObjectIcnType.Road, 49, false)]
    [InlineData(Mp2ObjectIcnType.ObjnTown, 13, true)]
    [InlineData(Mp2ObjectIcnType.ObjnTown, 29, true)]
    [InlineData(Mp2ObjectIcnType.ObjnTown, 28, false)]
    [InlineData(Mp2ObjectIcnType.ObjnTwrd, 29, true)]
    [InlineData(Mp2ObjectIcnType.ObjnTwrd, 30, false)]
    [InlineData(Mp2ObjectIcnType.ObjnCrck, 0, false)]
    public void IsSpriteRoadFollowsTheThreeAllowedSets(Mp2ObjectIcnType icnType, byte imageIndex, bool road) =>
        Assert.Equal(road, Tile.IsSpriteRoad(icnType, imageIndex));

    [Fact]
    public void ATileStartsWithEveryDirectionPassable()
    {
        var tile = new Tile();

        Assert.Equal(Directions.All, tile.PassableDirections);

        foreach (Direction direction in Directions.AllNeighboringDirections)
        {
            Assert.True(tile.IsPassableFrom(direction));
            Assert.True(tile.IsPassableTo(direction));
        }

        // CENTER is not part of DIRECTION_AROUND but is part of DIRECTION_ALL, like the reference.
        Assert.True(tile.IsPassableFrom(Direction.Center));
    }

    [Fact]
    public void GroundComesFromTheTerrainImageIndex()
    {
        var water = new Tile();
        water.SetTerrain(0, 0);
        Assert.Equal(MapGround.Water, water.Ground);
        Assert.True(water.IsWater);

        var grass = new Tile();
        grass.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        Assert.Equal(MapGround.Grass, grass.Ground);
        Assert.False(grass.IsWater);
    }

    [Fact]
    public void SortObjectPartsPromotesTheObjectLayerPartAndKeepsTheDescendingOrder()
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 1));
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 2, Mp2ObjectIcnType.ObjnCrck, 2));
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Background, 3, Mp2ObjectIcnType.ObjnCrck, 3));

        tile.SortObjectParts();

        // The reference sorts by layer descending and then walks the list backwards, so the promoted
        // part is the one with the lowest layer value — the object layer when there is one.
        Assert.Equal(1U, tile.MainObjectPart.Uid);
        Assert.Equal(2, tile.GroundObjectParts.Count);
        Assert.Equal(2U, tile.GroundObjectParts[0].Uid);
        Assert.Equal(3U, tile.GroundObjectParts[1].Uid);
    }

    [Fact]
    public void SortObjectPartsNeverPromotesAFlag()
    {
        // A flag belongs to another object, so it can stay on the ground level even on the top layer.
        var onlyFlag = new Tile();
        onlyFlag.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 9, Mp2ObjectIcnType.Flag32, 17));

        onlyFlag.SortObjectParts();

        Assert.Equal(Mp2ObjectIcnType.Unknown, onlyFlag.MainObjectPart.IcnType);
        Assert.Single(onlyFlag.GroundObjectParts);

        // A flag on the highest layer is skipped while the object under it becomes the main part.
        var flagAndObject = new Tile();
        flagAndObject.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnCrck, 1));
        flagAndObject.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 9, Mp2ObjectIcnType.Flag32, 17));

        flagAndObject.SortObjectParts();

        Assert.Equal(1U, flagAndObject.MainObjectPart.Uid);
        Assert.Equal(9U, Assert.Single(flagAndObject.GroundObjectParts).Uid);
    }

    [Fact]
    public void MoveMainObjectPartToGroundLevelEmptiesTheMainPart()
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 5, Mp2ObjectIcnType.ObjnCrck, 5));
        tile.SortObjectParts();

        tile.MoveMainObjectPartToGroundLevel();

        Assert.Equal(Mp2ObjectIcnType.Unknown, tile.MainObjectPart.IcnType);
        Assert.Equal(5U, Assert.Single(tile.GroundObjectParts).Uid);

        // An unknown main part is not moved, so a second call adds nothing.
        tile.MoveMainObjectPartToGroundLevel();
        Assert.Single(tile.GroundObjectParts);
    }

    [Fact]
    public void RemoveObjectsDropsTheMatchingPartsAndRefreshesTheRoadBit()
    {
        var tile = new Tile();
        tile.Init(TileInfo(groundIcn: Mp2ObjectIcnType.Road, groundIndex: 0, topIcn: Mp2ObjectIcnType.ObjnCrck, topIndex: 7));
        tile.SortObjectParts();
        Assert.True(tile.IsRoad);

        tile.RemoveObjects(Mp2ObjectIcnType.Road);

        Assert.False(tile.IsRoad);
        Assert.Empty(tile.GroundObjectParts);
        Assert.Equal(Mp2ObjectIcnType.Unknown, tile.MainObjectPart.IcnType);
        Assert.Single(tile.TopObjectParts);
    }

    [Fact]
    public void ReplaceObjectRewritesTheSpriteOfOnePart()
    {
        var tile = new Tile();
        tile.PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, TopUid, Mp2ObjectIcnType.ObjnCrck, 7));

        tile.ReplaceObject(TopUid, Mp2ObjectIcnType.ObjnCrck, Mp2ObjectIcnType.ObjnTown, 7, 8);
        Assert.Equal(Mp2ObjectIcnType.ObjnTown, tile.TopObjectParts[0].IcnType);
        Assert.Equal(8, tile.TopObjectParts[0].IcnIndex);

        // A part that does not carry the expected sprite is left alone.
        tile.ReplaceObject(TopUid, Mp2ObjectIcnType.ObjnCrck, Mp2ObjectIcnType.Road, 7, 0);
        Assert.Equal(Mp2ObjectIcnType.ObjnTown, tile.TopObjectParts[0].IcnType);
    }

    [Fact]
    public void UpdateObjectImageIndexShiftsTheSprite()
    {
        var tile = new Tile();
        tile.PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, TopUid, Mp2ObjectIcnType.ObjnCrck, 7));

        tile.UpdateObjectImageIndex(TopUid, Mp2ObjectIcnType.ObjnCrck, 2);
        Assert.Equal(9, tile.TopObjectParts[0].IcnIndex);

        // The reference asserts on the range instead of wrapping around.
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.UpdateObjectImageIndex(TopUid, Mp2ObjectIcnType.ObjnCrck, 250));
    }

    [Fact]
    public void UpdateTileObjectIcnIndexSetsTheGroundSpriteAndRefreshesTheRoadBit()
    {
        var tile = new Tile();
        tile.Init(TileInfo(groundIcn: Mp2ObjectIcnType.Road, groundIndex: 1));
        Assert.False(tile.IsRoad);

        Tile.UpdateTileObjectIcnIndex(tile, 0, 0);

        Assert.Equal(0, tile.GroundObjectParts[0].IcnIndex);
        Assert.True(tile.IsRoad);
    }

    [Fact]
    public void LookupHelpersSeeEveryContainer()
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 4, Mp2ObjectIcnType.ObjnCrck, 4));
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 1, Mp2ObjectIcnType.Road, 0));
        tile.SortObjectParts();
        tile.PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, 2, Mp2ObjectIcnType.ObjnTown, 45));

        // The object layer part wins the promotion, the terrain layer road stays on the ground.
        Assert.Equal(4U, tile.GetObjectIdByObjectIcnType(Mp2ObjectIcnType.ObjnCrck));
        Assert.Equal(2U, tile.GetObjectIdByObjectIcnType(Mp2ObjectIcnType.ObjnTown));
        Assert.Equal(1U, tile.GetObjectIdByObjectIcnType(Mp2ObjectIcnType.Road));
        Assert.Equal(0U, tile.GetObjectIdByObjectIcnType(Mp2ObjectIcnType.ObjnTwrd));

        Assert.Equal([Mp2ObjectIcnType.ObjnCrck, Mp2ObjectIcnType.Road, Mp2ObjectIcnType.ObjnTown], tile.GetValidObjectIcnTypes());
        Assert.True(tile.ContainsSprite(Mp2ObjectIcnType.ObjnTown, 45));
        Assert.False(tile.ContainsSprite(Mp2ObjectIcnType.ObjnTown, 46));
        Assert.True(tile.ContainsAnyObjectIcnType([Mp2ObjectIcnType.ObjnTwrd, Mp2ObjectIcnType.ObjnTown]));
        Assert.False(tile.ContainsAnyObjectIcnType([Mp2ObjectIcnType.ObjnTwrd, Mp2ObjectIcnType.Stream]));

        Assert.Equal(1U, tile.FindGroundObjectPart(1)?.Uid);
        Assert.Null(tile.FindGroundObjectPart(4));
        Assert.Equal(2U, tile.FindTopObjectPart(2)?.Uid);
        Assert.Null(tile.FindTopObjectPart(1));
    }

    [Fact]
    public void DoesObjectExistOnlyLooksAtTheMainAndGroundParts()
    {
        var tile = new Tile();
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 3, Mp2ObjectIcnType.ObjnCrck, 3));
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 4, Mp2ObjectIcnType.ObjnCrck, 4));
        tile.PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, 5, Mp2ObjectIcnType.ObjnTown, 45));

        // A terrain layer part never blocks, and top level parts are not consulted at all.
        Assert.False(tile.DoesObjectExist(3));
        Assert.True(tile.DoesObjectExist(4));
        Assert.False(tile.DoesObjectExist(5));
        Assert.False(tile.DoesObjectExist(6));
    }

    [Fact]
    public void IsPassabilityTransparentKeepsTheReferenceQuirkForATileWithoutObjects()
    {
        // The default main part sits on the object layer, so even an empty tile is not transparent.
        var tile = new Tile();
        Assert.False(tile.IsPassabilityTransparent);

        // Top level parts are not consulted.
        tile.PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnTown, 45));
        Assert.False(tile.IsPassabilityTransparent);

        // A terrain layer part as the main part is transparent, an object layer part is not.
        var terrainOnly = new Tile();
        terrainOnly.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Terrain, 2, Mp2ObjectIcnType.ObjnCrck, 2));
        terrainOnly.SortObjectParts();
        Assert.True(terrainOnly.IsPassabilityTransparent);

        terrainOnly.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 3, Mp2ObjectIcnType.ObjnCrck, 3));
        Assert.False(terrainOnly.IsPassabilityTransparent);
    }

    private static Mp2TileInfo TileInfo(
        Mp2ObjectIcnType groundIcn = Mp2ObjectIcnType.Unknown,
        byte groundIndex = 255,
        uint groundUid = 0,
        byte groundLayer = 0,
        Mp2ObjectIcnType topIcn = Mp2ObjectIcnType.Unknown,
        byte topIndex = 255,
        uint topUid = 0,
        ushort terrainImage = 0,
        byte terrainFlags = 0,
        Mp2ObjectType objectType = Mp2ObjectType.None,
        byte quantity1 = 0,
        byte quantity2 = 0) => new()
        {
            TerrainImageIndex = terrainImage,
            TerrainFlags = terrainFlags,
            ObjectType = objectType,
            ObjectName1 = (byte)((int)groundIcn << 2),
            ObjectName2 = (byte)((int)topIcn << 2),
            BottomIcnImageIndex = groundIndex,
            TopIcnImageIndex = topIndex,
            Quantity1 = (byte)((quantity1 & 0xFC) | (groundLayer & 0x03)),
            Quantity2 = quantity2,
            Level1ObjectUid = groundUid,
            Level2ObjectUid = topUid,
            NextAddonIndex = 0,
        };
}
