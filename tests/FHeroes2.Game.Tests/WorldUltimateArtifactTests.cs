using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// 测试 digging for the ultimate artifact：<c>World::DiggingForUltimateArtifact()</c>（<c>world.cpp:850</c>）
/// 与它用到的坑贴图表（<c>MP2::getDiggingHoleSprite()</c>，<c>mp2.cpp:937</c>）。
/// </summary>
/// <remarks>
/// 地图故意做得极小：挖宝只看一格的地面，8×8 的草地地图已经够 <see cref="World.LoadMap"/> 建出世界。
/// 神器本身不靠装载（端口还没有放它的那一步），测试直接 <see cref="UltimateArtifact.Set"/> 埋下去——
/// 这正是装载器将来要做的事，挖掘这一半因此可以独立验证。
/// </remarks>
public sealed class WorldUltimateArtifactTests
{
    /// <summary>边的格子数；地图是正方形，MP2 只接受 36/72/108/144。</summary>
    private const int MapSize = 36;

    /// <summary>埋宝的格子：第 4 列第 4 行。</summary>
    private const int BuriedTileIndex = (4 * MapSize) + 4;

    /// <summary>埋宝的坐标，与 <see cref="BuriedTileIndex"/> 是同一格。</summary>
    private static readonly Point BuriedTile = new(4, 4);

    /// <summary>装载一张全是同一种地面的地图。</summary>
    /// <param name="terrainImageIndex">地面范围的第一张图，决定整张地图的地面。</param>
    /// <returns>The loaded world.</returns>
    private static World LoadMap(ushort terrainImageIndex = MapGrounds.GrassStartImageIndex)
    {
        World world = new();
        world.LoadMap(Mp2Map.CreateBlank(MapSize, terrainImageIndex));

        // 装载会自己埋一个随机神器（setUltimateArtifact，world_loadmap.cpp:1627）；这一批测试挖的是自己埋的那一个，
        // 自动埋宝由 PlaceUltimateArtifact 的测试单独覆盖。
        world.UltimateArtifact.Reset();

        return world;
    }

    [Fact]
    public void ADigLeavesAHoleBehind()
    {
        World world = LoadMap();
        uint lastUid = world.LastObjectUid;

        Assert.False(world.DiggingForUltimateArtifact(BuriedTile));

        Tile tile = world.GetTile(BuriedTileIndex);
        ObjectPart part = Assert.Single(tile.GroundObjectParts);

        Assert.Equal(ObjectLayerType.Terrain, part.LayerType);
        Assert.True(DiggingHole.IsSprite(MapGround.Grass, part.IcnType, part.IcnIndex));
        Assert.True(part.Uid > lastUid, $"the hole must get a fresh UID, got {part.Uid} after {lastUid}");
    }

    [Fact]
    public void TheArtifactUnderTheHoleIsFoundOnceAndOnlyOnce()
    {
        World world = LoadMap();

        Assert.False(world.UltimateArtifact.IsFound);
        Assert.Equal(-1, world.UltimateArtifact.Position);

        world.UltimateArtifact.Set(BuriedTileIndex, ArtifactType.UltimateBook, MapVersion.SuccessionWars);

        Assert.True(world.DiggingForUltimateArtifact(BuriedTile));
        Assert.True(world.UltimateArtifact.IsFound);

        // 第二次照挖不误：坑还在，但神器已经挖走，什么都没有。
        Assert.False(world.DiggingForUltimateArtifact(BuriedTile));
        Assert.Equal(2, world.GetTile(BuriedTileIndex).GroundObjectParts.Count);
    }

    [Fact]
    public void DiggingElsewhereDoesNotFindAnything()
    {
        World world = LoadMap();
        world.UltimateArtifact.Set(BuriedTileIndex, ArtifactType.UltimateBook, MapVersion.SuccessionWars);

        Assert.False(world.DiggingForUltimateArtifact(new Point(1, 1)));
        Assert.False(world.UltimateArtifact.IsFound);
        Assert.Single(world.GetTile(1, 1).GroundObjectParts);
    }

    [Fact]
    public void WaterCannotBeDug()
    {
        // 参考在这里 assert；端口不加坑、也不报错。
        World world = LoadMap(MapGrounds.WaterStartImageIndex);

        Assert.Equal(MapGround.Water, world.GetTile(BuriedTileIndex).Ground);
        Assert.False(world.DiggingForUltimateArtifact(BuriedTile));
        Assert.Empty(world.GetTile(BuriedTileIndex).GroundObjectParts);
    }

    [Fact]
    public void LoadingAnotherMapForgetsTheArtifact()
    {
        World world = LoadMap();
        world.UltimateArtifact.Set(BuriedTileIndex, ArtifactType.UltimateBook, MapVersion.SuccessionWars);
        world.UltimateArtifact.MarkAsFound();

        world.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        // 新地图自己又埋了一个：位置不再是我埋的那一格，也不算已找到。这里不假设它埋在哪，只要求不是旧的。
        Assert.False(world.UltimateArtifact.IsFound);
        Assert.NotEqual(BuriedTileIndex, world.UltimateArtifact.Position);
        Assert.True(world.UltimateArtifact.Position >= 0);
    }

    [Fact]
    public void TheDiggingHoleTableCoversEveryGroundButWater()
    {
        Assert.True(DiggingHole.TryGetSprite(MapGround.Grass, out _, out _));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Beach, out _, out _));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Desert, out _, out _));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Snow, out _, out _));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Swamp, out _, out _));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Wasteland, out _, out _));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Lava, out _, out _));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Dirt, out _, out _));

        Assert.False(DiggingHole.TryGetSprite(MapGround.Water, out _, out _));

        // 沙滩借草地那一张：两张必须对得上，否则同一格会因为地面不同而认不出坑。
        Assert.True(DiggingHole.TryGetSprite(MapGround.Beach, out Mp2ObjectIcnType beachIcn, out byte beachIndex));
        Assert.True(DiggingHole.TryGetSprite(MapGround.Grass, out Mp2ObjectIcnType grassIcn, out byte grassIndex));
        Assert.Equal(beachIcn, grassIcn);
        Assert.Equal(beachIndex, grassIndex);

        Assert.True(DiggingHole.IsSprite(MapGround.Grass, grassIcn, grassIndex));
        Assert.False(DiggingHole.IsSprite(MapGround.Desert, grassIcn, grassIndex));
    }

    [Fact]
    public void AnyKingdomVisitedAsksEveryKingdom()
    {
        World world = LoadMap();

        Assert.False(world.IsAnyKingdomVisited(Mp2ObjectType.TreasureChest, BuriedTileIndex));

        world.GetKingdom(PlayerColor.Red).SetVisited(BuriedTileIndex, Mp2ObjectType.TreasureChest);

        Assert.True(world.IsAnyKingdomVisited(Mp2ObjectType.TreasureChest, BuriedTileIndex));

        // 同一条记录只对同一种对象有效。
        Assert.False(world.IsAnyKingdomVisited(Mp2ObjectType.Obelisk, BuriedTileIndex));
    }
}
