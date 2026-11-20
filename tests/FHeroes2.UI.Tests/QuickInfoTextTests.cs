using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The ported half of <c>getQuickInfoText()</c> (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:455-595</c>):
/// the ground branch, the resource pile branch and the object name the reference falls back to.
/// </summary>
public sealed class QuickInfoTextTests
{
    [Fact]
    public void BareGround_IsNamedAfterItsGround()
    {
        Tile tile = TileOf(Mp2ObjectType.None);

        Assert.Equal(tile.Ground.ToString(), QuickInfoText.ForTile(tile));
    }

    [Fact]
    public void Road_IsNamedRoad()
    {
        Tile tile = new();
        tile.Init(TileInfo(groundIcn: Mp2ObjectIcnType.Road, groundIndex: 0));

        Assert.True(tile.IsRoad);
        Assert.Equal("Road", QuickInfoText.DescribeGround(tile));
    }

    [Fact]
    public void Coast_IsNamedCoast()
    {
        // showGroundInfo() short-circuits the coast before looking at the road bit.
        Tile tile = TileOf(Mp2ObjectType.Coast);

        Assert.Equal(Mp2ObjectName.Of(Mp2ObjectType.Coast), QuickInfoText.DescribeGround(tile));
    }

    [Fact]
    public void ResourcePile_IsNamedAfterItsSprite()
    {
        // Sprite 1 of the resource pile is wood, the way Resource::FromIndexSprite() decodes it.
        Tile tile = new();
        tile.SetMainObjectType(Mp2ObjectType.Resource);
        tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnRsrc, 1));

        Assert.Equal(1, tile.MainObjectPart.IcnIndex);
        Assert.Equal("Wood", QuickInfoText.ForTile(tile));
    }

    [Fact]
    public void AnyOtherObject_FallsBackToItsObjectName()
    {
        Tile mine = TileOf(Mp2ObjectType.Mine);

        Assert.Equal(Mp2ObjectName.Of(Mp2ObjectType.Mine), QuickInfoText.ForTile(mine));
    }

    [Fact]
    public void ForTile_RejectsAMissingTile()
    {
        Assert.Throws<ArgumentNullException>(() => QuickInfoText.ForTile(null!));
        Assert.Throws<ArgumentNullException>(() => QuickInfoText.DescribeGround(null!));
    }

    private static Tile TileOf(Mp2ObjectType objectType)
    {
        Tile tile = new();
        tile.SetMainObjectType(objectType);

        return tile;
    }

    private static Mp2TileInfo TileInfo(
        Mp2ObjectIcnType groundIcn = Mp2ObjectIcnType.Unknown,
        byte groundIndex = 255,
        Mp2ObjectIcnType topIcn = Mp2ObjectIcnType.Unknown,
        byte topIndex = 255,
        Mp2ObjectType objectType = Mp2ObjectType.None) => new()
        {
            TerrainImageIndex = 0,
            TerrainFlags = 0,
            ObjectType = objectType,
            ObjectName1 = (byte)((int)groundIcn << 2),
            ObjectName2 = (byte)((int)topIcn << 2),
            BottomIcnImageIndex = groundIndex,
            TopIcnImageIndex = topIndex,
            Quantity1 = 0,
            Quantity2 = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
            NextAddonIndex = 0,
        };
}
