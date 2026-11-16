using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the ownership flag, the boat, the removal of an object from a tile and the tile dump of
/// <c>Maps::Tile</c> (<c>maps_tiles.cpp:440-581</c>, <c>:885-1010</c>, <c>:1185-1303</c>,
/// <c>:1450-1509</c>) plus <c>MapPosition</c> (<c>position.cpp:29-37</c>).
/// </summary>
public sealed class TileOwnershipTests
{
    [Fact]
    public void AMinePaintsItsOwnersFlagOnItself()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionMine, PlayerColor.Blue);

        ObjectPart flag = Assert.Single(tile.TopObjectParts);

        Assert.Equal(Mp2ObjectIcnType.Flag32, flag.IcnType);
        Assert.Equal((byte)14, flag.IcnIndex);
        Assert.Equal(ObjectUid, flag.Uid);
    }

    [Fact]
    public void RepaintingAFlagDoesNotLeaveTheOldSpriteBehind()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionMine, PlayerColor.Blue);
        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionMine, PlayerColor.Red);

        ObjectPart flag = Assert.Single(tile.TopObjectParts);

        Assert.Equal((byte)16, flag.IcnIndex);
    }

    [Fact]
    public void TakingTheFlagDownRemovesIt()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionMine, PlayerColor.Blue);
        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionMine, PlayerColor.None);

        Assert.DoesNotContain(tile.TopObjectParts, part => part.IcnType == Mp2ObjectIcnType.Flag32);
        Assert.DoesNotContain(tile.GroundObjectParts, part => part.IcnType == Mp2ObjectIcnType.Flag32);
    }

    [Fact]
    public void ALighthousePaintsItselfAndItsLeftNeighbour()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionLighthouse, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionLighthouse, PlayerColor.Green);

        Assert.Equal((byte)(49 + 1), Assert.Single(world.GetTile(4).GroundObjectParts).IcnIndex);
        Assert.Equal((byte)(56 + 1), Assert.Single(tile.GroundObjectParts).IcnIndex);
        Assert.Empty(tile.TopObjectParts);
    }

    [Fact]
    public void AnAlchemistLabPaintsItsFlagAboveItself()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionAlchemistLab, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionAlchemistLab, PlayerColor.Yellow);

        Assert.Equal((byte)(21 + 3), Assert.Single(world.GetTile(1).TopObjectParts).IcnIndex);
        Assert.Empty(tile.GroundObjectParts);
        Assert.Empty(tile.TopObjectParts);
    }

    [Fact]
    public void ASawmillPaintsItsFlagOnTheTopRightTile()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionSawmill, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionSawmill, PlayerColor.Orange);

        Assert.Equal((byte)(28 + 4), Assert.Single(world.GetTile(2).TopObjectParts).IcnIndex);
    }

    [Fact]
    public void ANeutralCastleKeepsTheNeutralFlagsOnBothSides()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionCastle, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionCastle, PlayerColor.None);

        Assert.Equal((byte)12, Assert.Single(world.GetTile(4).TopObjectParts).IcnIndex);
        Assert.Equal((byte)13, Assert.Single(world.GetTile(6).TopObjectParts).IcnIndex);
    }

    [Fact]
    public void ACastlePaintsTheTwoHalvesOfTheFlagOfItsOwner()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionCastle, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.ActionCastle, PlayerColor.Purple);

        Assert.Equal((byte)10, Assert.Single(world.GetTile(4).TopObjectParts).IcnIndex);
        Assert.Equal((byte)11, Assert.Single(world.GetTile(6).TopObjectParts).IcnIndex);
    }

    [Fact]
    public void ADwellingIsLeftWithoutAFlag()
    {
        Tile tile = MakeTile(Mp2ObjectType.DragonCity, out World world);

        tile.SetOwnershipFlag(world, Mp2ObjectType.DragonCity, PlayerColor.Blue);

        Assert.Empty(tile.TopObjectParts);
        Assert.Empty(tile.GroundObjectParts);
    }

    [Fact]
    public void ABoatTakesTheTileOverAndKeepsWhatWasThere()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);
        tile.SetTerrain(0, 0);

        Assert.True(tile.IsWater);

        var shore = new ObjectPart(ObjectLayerType.Background, 55, Mp2ObjectIcnType.ObjnHaun, 3);
        tile.SetMainObjectPart(shore);

        tile.SetBoat(world, Direction.Right, PlayerColor.Blue);

        Assert.Equal(Mp2ObjectType.ActionBoat, tile.MainObjectType);
        Assert.Equal(Mp2ObjectIcnType.Boat32, tile.MainObjectPart.IcnType);
        Assert.Equal((byte)18, tile.MainObjectPart.IcnIndex);
        Assert.Equal(PlayerColor.Blue, tile.BoatOwnerColor);

        ObjectPart kept = Assert.Single(tile.GroundObjectParts);

        Assert.Equal(ObjectLayerType.Background, kept.LayerType);
        Assert.Equal(55u, kept.Uid);
    }

    [Theory]
    [InlineData(Direction.Top, 0)]
    [InlineData(Direction.TopRight, 9)]
    [InlineData(Direction.Right, 18)]
    [InlineData(Direction.BottomRight, 27)]
    [InlineData(Direction.Bottom, 36)]
    [InlineData(Direction.BottomLeft, 155)]
    [InlineData(Direction.Left, 146)]
    [InlineData(Direction.TopLeft, 137)]
    public void ABoatDirectionIsReadBackFromItsSprite(Direction direction, byte expectedIndex)
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);
        tile.SetTerrain(0, 0);

        tile.SetBoat(world, direction, PlayerColor.None);

        Assert.Equal(expectedIndex, tile.MainObjectPart.IcnIndex);
        Assert.Equal(direction, tile.GetBoatDirection());
    }

    [Fact]
    public void ATileWithoutABoatHasNoBoatDirection()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out _);

        Assert.Equal(Direction.Unknown, tile.GetBoatDirection());
    }

    [Fact]
    public void TheOwnerOfABoatCanBeForgotten()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);
        tile.SetTerrain(0, 0);
        tile.SetBoat(world, Direction.Top, PlayerColor.Green);

        tile.ResetBoatOwnerColor();

        Assert.Equal(PlayerColor.None, tile.BoatOwnerColor);
    }

    [Fact]
    public void ARemovalByUidTakesEveryPartOfTheSameObject()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Shadow, ObjectUid, Mp2ObjectIcnType.ObjnHaun, 7));
        tile.PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, ObjectUid + 1, Mp2ObjectIcnType.Flag32, 20));

        bool removed = tile.RemoveObjectPartsByUID(world, ObjectUid);

        Assert.True(removed);
        Assert.Equal(Mp2ObjectIcnType.Unknown, tile.MainObjectPart.IcnType);
        Assert.DoesNotContain(tile.GroundObjectParts, part => part.Uid == ObjectUid);

        // Another object on the same tile keeps its part.
        Assert.Contains(tile.TopObjectParts, part => part.Uid == ObjectUid + 1);
    }

    [Fact]
    public void RemovingAnUnknownUidChangesNothing()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);

        bool removed = tile.RemoveObjectPartsByUID(world, 9999);

        Assert.False(removed);
        Assert.Equal(Mp2ObjectIcnType.ObjnHaun, tile.MainObjectPart.IcnType);
        Assert.Equal(ObjectUid, tile.MainObjectPart.Uid);
    }

    [Fact]
    public void TheCentreOfATileIsItsPointOnTheMap()
    {
        Tile tile = MakeTile(Mp2ObjectType.None, out World world);

        Assert.Equal(new Point(1, 1), tile.GetCenter(world));
    }

    [Fact]
    public void AMapPositionConvertsBetweenIndexAndPoint()
    {
        var geometry = new MapGeometry(4, 4);
        var position = new MapPosition();

        position.SetIndex(5, geometry);

        Assert.Equal(new Point(1, 1), position.Center);
        Assert.Equal(5, position.GetIndex(geometry));
        Assert.True(position.IsPosition(new Point(1, 1)));
        Assert.False(position.IsPosition(new Point(2, 2)));
    }

    [Fact]
    public void AnIndexOutsideTheMapClearsThePosition()
    {
        var geometry = new MapGeometry(4, 4);
        var position = new MapPosition(new Point(1, 1));

        position.SetIndex(100, geometry);

        Assert.Equal(MapPosition.Empty, position.Center);
    }

    [Fact]
    public void TheLayerNamesAreTheOnesOfTheReference()
    {
        Assert.Equal("Object layer", Tile.GetObjectLayerName(ObjectLayerType.Object));
        Assert.Equal("Shadow layer", Tile.GetObjectLayerName(ObjectLayerType.Shadow));
        Assert.Equal("Unknown layer", Tile.GetObjectLayerName((ObjectLayerType)9));
    }

    [Fact]
    public void ATopLevelPartOnTheTerrainLayerIsReportedAsInvalid()
    {
        var part = new ObjectPart(ObjectLayerType.Terrain, 7, Mp2ObjectIcnType.ObjnHaun, 0);

        string info = Tile.GetObjectPartInfo(part, false);

        Assert.Contains("!!! INVALID !!!", info);
        Assert.Contains("UID             : 7", info);
    }

    [Fact]
    public void TheDumpOfATileStartsWithItsOwnFacts()
    {
        Tile tile = MakeTile(Mp2ObjectType.ActionMine, out World world);

        string dump = tile.ToDebugString(world);

        Assert.Contains("******* Tile info *******", dump);
        Assert.Contains($"index           : {tile.Index}, point: {tile.GetCenter(world)}", dump);
        Assert.Contains($"MP2 object type : {(int)Mp2ObjectType.ActionMine} (ActionMine)", dump);
    }

    private const uint ObjectUid = 101;

    private static Tile MakeTile(Mp2ObjectType type, out World world)
    {
        world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(5);

        // A generated map leaves the indexes at zero on purpose (the loaders set them); the geometry
        // dependent halves of this file need the real one.
        tile.Index = 5;

        // Any non empty part will do: what the flag and the removal look at is the UID.
        tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, ObjectUid, Mp2ObjectIcnType.ObjnHaun, 0));
        tile.SetMainObjectType(type);

        return tile;
    }
}
