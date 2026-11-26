using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The two grid rules of the object phase, <c>markNodeAsType()</c> and <c>canPlaceObject()</c>
/// (<c>map_random_generator_helper.cpp:209-281</c>).
/// </summary>
public sealed class RandomMapObjectRulesTests
{
    private const int Size = 24;
    private const int RegionStart = 8;
    private const int RegionEnd = 16;

    [Fact]
    public void AnObjectFitsInsideItsRegion()
    {
        RandomMapState state = Grid();

        Assert.True(RandomMapObjectRules.CanPlaceObject(
            state, Object(Mp2ObjectType.Trees, (0, 0)), new Point(12, 12)));
    }

    [Fact]
    public void AnUnclaimedTileBlocksTheObject()
    {
        RandomMapState state = Grid();

        Assert.False(RandomMapObjectRules.CanPlaceObject(
            state, Object(Mp2ObjectType.Trees, (0, 0)), new Point(2, 2)));
    }

    [Fact]
    public void AnObjectOffTheMapIsRejected()
    {
        RandomMapState state = Grid();

        Assert.False(RandomMapObjectRules.CanPlaceObject(
            state, Object(Mp2ObjectType.Trees, (0, 0)), new Point(RegionStart, RegionStart - 1)));
    }

    [Fact]
    public void ShadowsAndTerrainDoNotNeedATile()
    {
        RandomMapState state = Grid();

        ObjectInfo shadows = new(Mp2ObjectType.Trees);
        shadows.AddGroundPart(
            Mp2ObjectIcnType.ObjnArti, 0, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Shadow);
        shadows.AddGroundPart(
            Mp2ObjectIcnType.ObjnArti, 1, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Terrain);

        // 这片地方不属于任何区域，但影子与地表层不占格子，所以照样放得下。
        Assert.True(RandomMapObjectRules.CanPlaceObject(state, shadows, new Point(2, 2)));
    }

    [Fact]
    public void AnObstacleBlocksANonActionObject()
    {
        RandomMapState state = Grid();

        state.GetNodeToUpdate(new Point(13, 12)).Type = RandomMapNodeType.Obstacle;

        Assert.False(RandomMapObjectRules.CanPlaceObject(
            state, Object(Mp2ObjectType.Trees, (0, 0), (1, 0)), new Point(12, 12)));
    }

    [Fact]
    public void ABorderTileTakesDecorationButNotAnActionObject()
    {
        RandomMapState state = Grid();

        state.GetNodeToUpdate(new Point(13, 12)).Type = RandomMapNodeType.Border;

        Assert.True(RandomMapObjectRules.CanPlaceObject(
            state, Object(Mp2ObjectType.Trees, (0, 0), (1, 0)), new Point(12, 12)));

        Assert.True(Mp2ObjectTypes.IsInGameActionObject(Mp2ObjectType.ActionTreasureChest));

        // 可交互物件压在边界上会把区域之间的路堵死。
        Assert.False(RandomMapObjectRules.CanPlaceObject(
            state, Object(Mp2ObjectType.ActionTreasureChest, (0, 0), (1, 0)), new Point(12, 12)));
    }

    [Fact]
    public void ActionAndPathTilesAreNeverBuiltOver()
    {
        RandomMapState state = Grid();
        ObjectInfo tree = Object(Mp2ObjectType.Trees, (0, 0));

        state.GetNodeToUpdate(new Point(12, 12)).Type = RandomMapNodeType.Action;

        Assert.False(RandomMapObjectRules.CanPlaceObject(state, tree, new Point(12, 12)));

        state.GetNodeToUpdate(new Point(12, 12)).Type = RandomMapNodeType.Path;

        Assert.False(RandomMapObjectRules.CanPlaceObject(state, tree, new Point(12, 12)));
    }

    [Fact]
    public void AnActionObjectNeedsAnOpenTileBelow()
    {
        RandomMapState state = Grid();
        ObjectInfo chest = Object(Mp2ObjectType.ActionTreasureChest, (0, 0));

        Assert.True(RandomMapObjectRules.CanPlaceObject(state, chest, new Point(12, 12)));

        state.GetNodeToUpdate(new Point(12, 13)).Type = RandomMapNodeType.Path;

        Assert.True(RandomMapObjectRules.CanPlaceObject(state, chest, new Point(12, 12)));

        state.GetNodeToUpdate(new Point(12, 13)).Type = RandomMapNodeType.Obstacle;

        Assert.False(RandomMapObjectRules.CanPlaceObject(state, chest, new Point(12, 12)));
    }

    [Fact]
    public void TheTopLevelPartsOnlyNeedATile()
    {
        RandomMapState state = Grid();

        ObjectInfo tree = new(Mp2ObjectType.Trees);
        tree.AddTopPart(Mp2ObjectIcnType.ObjnArti, 0, new Point(0, 0), Mp2ObjectType.Trees);

        Assert.True(RandomMapObjectRules.CanPlaceObject(state, tree, new Point(12, 12)));
        Assert.False(RandomMapObjectRules.CanPlaceObject(state, tree, new Point(2, 2)));
    }

    [Fact]
    public void MarkingKeepsBordersAndActionTiles()
    {
        RandomMapState state = new(Size, Size);
        Point border = new(5, 5);
        Point action = new(6, 5);

        state.GetNodeToUpdate(border).Type = RandomMapNodeType.Border;
        state.GetNodeToUpdate(action).Type = RandomMapNodeType.Action;

        RandomMapObjectRules.MarkNodeAsType(state, border, RandomMapNodeType.Obstacle);
        RandomMapObjectRules.MarkNodeAsType(state, action, RandomMapNodeType.Obstacle);

        Assert.Equal(RandomMapNodeType.Border, state.GetNode(border).Type);
        Assert.Equal(RandomMapNodeType.Action, state.GetNode(action).Type);
    }

    [Fact]
    public void APathNeverGoesThroughAnObstacleButAnObstacleMayCoverAPath()
    {
        RandomMapState state = new(Size, Size);
        Point obstacle = new(5, 5);
        Point path = new(6, 5);

        state.GetNodeToUpdate(obstacle).Type = RandomMapNodeType.Obstacle;
        state.GetNodeToUpdate(path).Type = RandomMapNodeType.Path;

        RandomMapObjectRules.MarkNodeAsType(state, obstacle, RandomMapNodeType.Path);

        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(obstacle).Type);

        RandomMapObjectRules.MarkNodeAsType(state, path, RandomMapNodeType.Obstacle);

        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(path).Type);
    }

    [Fact]
    public void MarkingOutsideTheMapIsIgnored()
    {
        RandomMapState state = new(Size, Size);

        RandomMapObjectRules.MarkNodeAsType(state, new Point(-1, 3), RandomMapNodeType.Obstacle);
        RandomMapObjectRules.MarkNodeIndexAsType(state, -1, RandomMapNodeType.Obstacle);
        RandomMapObjectRules.MarkNodeIndexAsType(state, Size * Size, RandomMapNodeType.Obstacle);

        Assert.All(
            Enumerable.Range(0, state.Count),
            index => Assert.Equal(RandomMapNodeType.Open, state.GetNode(index).Type));
    }

    [Fact]
    public void TreesAndMountainsFollowTheGroundsBlock()
    {
        Assert.Equal(
            RandomMapDecorations.TreeIndexFor(MapGround.Grass) + 3,
            RandomMapObjectRules.SelectTerrainVariant(ObjectGroup.LandscapeTrees, 3, MapGround.Grass));

        // 雪地那套树的第 3 棵在索引 33，与草地那套的第 3 棵不是同一张图。
        Assert.Equal(33, RandomMapObjectRules.SelectTerrainVariant(ObjectGroup.LandscapeTrees, 3, MapGround.Snow));
        Assert.Equal(3, RandomMapObjectRules.SelectTerrainVariant(ObjectGroup.LandscapeTrees, 3, MapGround.Grass));

        Assert.Equal(
            RandomMapDecorations.MountainIndexFor(MapGround.Wasteland) + 2,
            RandomMapObjectRules.SelectTerrainVariant(ObjectGroup.LandscapeMountains, 2, MapGround.Wasteland));
    }

    [Fact]
    public void ObjectsThatDoNotDifferByGroundKeepTheirIndex()
    {
        // 树只有前六种按地形分块，山只有前八种。
        Assert.Equal(7, RandomMapObjectRules.SelectTerrainVariant(ObjectGroup.LandscapeTrees, 7, MapGround.Snow));
        Assert.Equal(9, RandomMapObjectRules.SelectTerrainVariant(ObjectGroup.LandscapeMountains, 9, MapGround.Lava));

        Assert.Equal(
            RandomMapObjectSets.TreasureIndex,
            RandomMapObjectRules.SelectTerrainVariant(
                ObjectGroup.AdventureTreasures, RandomMapObjectSets.TreasureIndex, MapGround.Snow));
    }

    [Fact]
    public void MarkingAnObjectTakesEveryGroundTile()
    {
        RandomMapState state = Grid();

        RandomMapObjectRules.MarkObjectPlacement(
            state, Object(Mp2ObjectType.Trees, (0, 0), (1, 0)), new Point(12, 12));

        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(new Point(12, 12)).Type);
        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(new Point(13, 12)).Type);
    }

    [Fact]
    public void AnActionObjectMarksItsAnchorAsThePlaceToStand()
    {
        RandomMapState state = Grid();

        RandomMapObjectRules.MarkObjectPlacement(
            state, Object(Mp2ObjectType.ActionTreasureChest, (0, 0)), new Point(12, 12));

        // 动作位留在锚点上，后来者（障碍、路）都盖不掉它。
        Assert.Equal(RandomMapNodeType.Action, state.GetNode(new Point(12, 12)).Type);

        RandomMapObjectRules.MarkNodeAsType(state, new Point(12, 12), RandomMapNodeType.Obstacle);

        Assert.Equal(RandomMapNodeType.Action, state.GetNode(new Point(12, 12)).Type);
    }

    [Fact]
    public void MarkingIgnoresShadows()
    {
        RandomMapState state = Grid();
        ObjectInfo tree = new(Mp2ObjectType.Trees);

        tree.AddGroundPart(
            Mp2ObjectIcnType.ObjnArti, 0, new Point(0, 0), Mp2ObjectType.Trees, ObjectLayerType.Object);
        tree.AddGroundPart(
            Mp2ObjectIcnType.ObjnArti, 1, new Point(1, 0), Mp2ObjectType.Trees, ObjectLayerType.Shadow);

        RandomMapObjectRules.MarkObjectPlacement(state, tree, new Point(12, 12));

        Assert.Equal(RandomMapNodeType.Obstacle, state.GetNode(new Point(12, 12)).Type);
        Assert.Equal(RandomMapNodeType.Open, state.GetNode(new Point(13, 12)).Type);
    }

    [Fact]
    public void APrefabFitsInAnOpenRegion()
    {
        RandomMapState state = Grid();

        Assert.True(RandomMapObjectRules.CanFitObjectSet(
            state, RandomMapObjectSets.Prefabs[0], new Point(10, 10), MapGround.Grass));
    }

    [Fact]
    public void APrefabNeedsItsEntranceOpen()
    {
        RandomMapState state = Grid();

        state.GetNodeToUpdate(new Point(9, 10)).Type = RandomMapNodeType.Obstacle;

        // 入口是 (-1, 0)，锚点 (10, 10) 时落在 (9, 10)：堵住它，奖品就够不着了。
        Assert.False(RandomMapObjectRules.CanFitObjectSet(
            state, RandomMapObjectSets.Prefabs[0], new Point(10, 10), MapGround.Grass));
    }

    [Fact]
    public void APrefabNeedsRoomForEveryPrize()
    {
        RandomMapState state = Grid();

        state.GetNodeToUpdate(new Point(12, 9)).Type = RandomMapNodeType.Obstacle;

        Assert.False(RandomMapObjectRules.CanFitObjectSet(
            state, RandomMapObjectSets.Prefabs[0], new Point(10, 10), MapGround.Grass));
    }

    [Fact]
    public void APrefabDoesNotFitOnTheMapsEdge()
    {
        RandomMapState state = Grid();

        // 锚点挪到区域左上角，障碍就会落到地图外。
        Assert.False(RandomMapObjectRules.CanFitObjectSet(
            state, RandomMapObjectSets.Prefabs[0], new Point(RegionStart, RegionStart), MapGround.Grass));
    }

    [Fact]
    public void ADecorationMayCoverAnObstacleButNotAPath()
    {
        RandomMapState state = Grid();
        ObjectInfo trees = Object(Mp2ObjectType.Trees, (0, 0), (1, 0));
        Point neighbour = new(13, 12);

        state.GetNodeToUpdate(neighbour).Type = RandomMapNodeType.Obstacle;

        // 一丛树可以长在另一丛树上，这正是装饰物铺在区域边缘时的常态。
        Assert.False(RandomMapObjectRules.CanPlaceObject(state, trees, new Point(12, 12)));
        Assert.True(RandomMapObjectRules.CanPlaceBorderObstacle(state, trees, new Point(12, 12)));

        state.GetNodeToUpdate(neighbour).Type = RandomMapNodeType.Path;

        Assert.False(RandomMapObjectRules.CanPlaceBorderObstacle(state, trees, new Point(12, 12)));
    }

    [Fact]
    public void ABorderDecorationStillNeedsItsOwnTiles()
    {
        RandomMapState state = Grid();

        // 区域外的格子没有主人，装饰物也压不上去。
        Assert.False(RandomMapObjectRules.CanPlaceBorderObstacle(
            state, Object(Mp2ObjectType.Trees, (0, 0), (1, 0)), new Point(RegionEnd, 12)));
    }

    [Fact]
    public void AnIndexOutsideTheCatalogueIsRejectedWithoutThrowing()
    {
        RandomMapState state = Grid();
        RandomMapObjectPlacement[] objects =
        [
            new(new Point(0, 0), ObjectGroup.LandscapeTrees, 9999),
        ];

        Assert.False(RandomMapObjectRules.CanPlaceAllObjects(
            state, objects, new Point(12, 12), MapGround.Grass));
    }

    /// <summary>A 24x24 grid whose middle nine by nine tiles belong to region 1.</summary>
    private static RandomMapState Grid()


    {
        RandomMapState state = new(Size, Size);

        for (int y = RegionStart; y <= RegionEnd; ++y)
        {
            for (int x = RegionStart; x <= RegionEnd; ++x)
            {
                state.GetNodeToUpdate(new Point(x, y)).Region = 1;
            }
        }

        return state;
    }

    /// <summary>An object of <paramref name="type"/> whose ground parts cover the given tiles.</summary>
    private static ObjectInfo Object(Mp2ObjectType type, params (int X, int Y)[] tiles)
    {
        ObjectInfo info = new(type);

        foreach ((int x, int y) in tiles)
        {
            info.AddGroundPart(Mp2ObjectIcnType.ObjnArti, 0, new Point(x, y), type, ObjectLayerType.Object);
        }

        return info;
    }
}
