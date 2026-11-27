using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>World::LoadMapMP2()</c> (<c>src/fheroes2/world/world_loadmap.cpp:141</c>) as far as the port
/// can run it: the tile loop, the object type repairs and the final passability pass.
/// </summary>
public sealed class WorldLoadMapTests
{
    /// <summary>
    /// Width of the synthetic maps. <see cref="Mp2Map.CreateBlank(int, ushort, string)"/> only accepts the
    /// four sizes of the original map editor, so the tests use the smallest one.
    /// </summary>
    private const int MapSize = 36;

    /// <summary>Index of a tile of the synthetic map, <c>x + y * width</c>.</summary>
    /// <param name="x">Column.</param>
    /// <param name="y">Row.</param>
    /// <returns>The tile index.</returns>
    private static int At(int x, int y) => x + (y * MapSize);

    /// <summary>
    /// An empty grass tile: no ground object, no top object, no add-ons.
    /// </summary>
    /// <param name="terrainImageIndex">Terrain image; at or above the grass index means land.</param>
    /// <returns>The tile structure.</returns>
    private static Mp2TileInfo EmptyTile(ushort terrainImageIndex = MapGrounds.GrassStartImageIndex) => new()
    {
        TerrainImageIndex = terrainImageIndex,
        ObjectName1 = 0,
        BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        Quantity1 = 0,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.None,
        NextAddonIndex = 0,
        Level1ObjectUid = 0,
        Level2ObjectUid = 0,
    };

    /// <summary>
    /// A tile holding one ground object, the way the map file stores it.
    /// </summary>
    /// <param name="icnType">Object set of the sprite.</param>
    /// <param name="icnIndex">Sprite inside the set.</param>
    /// <param name="objectType">Object type the file claims.</param>
    /// <param name="uid">Object UID.</param>
    /// <param name="layer">Layer the part is drawn on.</param>
    /// <returns>The tile structure.</returns>
    private static Mp2TileInfo GroundObjectTile(
        Mp2ObjectIcnType icnType, byte icnIndex, Mp2ObjectType objectType = Mp2ObjectType.None, uint uid = 1,
        ObjectLayerType layer = ObjectLayerType.Object) => EmptyTile() with
        {
            ObjectName1 = (byte)((int)icnType << 2),
            BottomIcnImageIndex = icnIndex,
            Quantity1 = (byte)layer,
            ObjectType = objectType,
            Level1ObjectUid = uid,
        };

    private static Mp2Map BlankMap() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

    [Fact]
    public void LoadMapCopiesTheTilesAndTheirTerrain()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), EmptyTile(MapGrounds.WaterStartImageIndex));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(MapSize, world.Width);
        Assert.Equal(MapSize, world.Height);
        Assert.Equal(MapSize * MapSize, world.Size);

        for (int i = 0; i < world.Size; ++i)
        {
            Assert.Equal(i, world.Tiles[i].Index);
        }

        Assert.True(world.GetTile(10, 10).IsWater);
        Assert.False(world.GetTile(9, 10).IsWater);
    }

    [Fact]
    public void LoadMapBuildsTheGroundAndTopParts()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), EmptyTile() with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            BottomIcnImageIndex = 5,
            Quantity1 = (byte)ObjectLayerType.Object,
            Level1ObjectUid = 7,
            ObjectName2 = (byte)((int)Mp2ObjectIcnType.ObjnRsrc << 2),
            TopIcnImageIndex = 3,
            Level2ObjectUid = 8,
            ObjectType = Mp2ObjectType.ActionResource,
        });

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(10, 10);

        // The sorting pass promotes the ground part to the main part.
        Assert.Equal(Mp2ObjectIcnType.ObjnCrck, tile.MainObjectPart.IcnType);
        Assert.Equal(5, tile.MainObjectPart.IcnIndex);
        Assert.Equal(7u, tile.MainObjectPart.Uid);
        Assert.Equal(ObjectLayerType.Object, tile.MainObjectPart.LayerType);
        Assert.Empty(tile.GroundObjectParts);

        ObjectPart top = Assert.Single(tile.TopObjectParts);
        Assert.Equal(Mp2ObjectIcnType.ObjnRsrc, top.IcnType);
        Assert.Equal(8u, top.Uid);

        Assert.Equal(Mp2ObjectType.ActionResource, tile.MainObjectType);
    }

    [Fact]
    public void LoadMapBuildsTheAddonChain()
    {
        Mp2Map map = BlankMap();

        int firstAddon = map.AppendAddon(new Mp2AddonInfo
        {
            NextAddonIndex = 0,
            ObjectNameN1 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            BottomIcnImageIndex = 10,
            QuantityN = (byte)ObjectLayerType.Object,
            ObjectNameN2 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            TopIcnImageIndex = 11,
            Level1ObjectUid = 5,
            Level2ObjectUid = 5,
        });

        int secondAddon = map.AppendAddon(new Mp2AddonInfo
        {
            NextAddonIndex = 0,
            ObjectNameN1 = (byte)((int)Mp2ObjectIcnType.ObjnCrck << 2),
            BottomIcnImageIndex = 12,
            QuantityN = (byte)ObjectLayerType.Background,
            ObjectNameN2 = 0,
            TopIcnImageIndex = 0,
            Level1ObjectUid = 6,
            Level2ObjectUid = 0,
        });

        // The chain is built backwards: the tile points at the first add-on, which points at the second.
        map.SetTileAt(firstAddon - 1, EmptyTile() with { NextAddonIndex = (ushort)firstAddon });
        map.SetAddonAt(firstAddon, map.Addons[firstAddon] with { NextAddonIndex = (ushort)secondAddon });

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(firstAddon - 1);

        // The tile had no object of its own, so the sorting pass promotes one of the add-on parts: the
        // object layer one, which is the lowest layer and therefore the last of the sorted list.
        Assert.Equal(10, tile.MainObjectPart.IcnIndex);
        Assert.Equal(ObjectLayerType.Object, tile.MainObjectPart.LayerType);
        Assert.Equal(5u, tile.MainObjectPart.Uid);

        ObjectPart background = Assert.Single(tile.GroundObjectParts);
        Assert.Equal(12, background.IcnIndex);
        Assert.Equal(ObjectLayerType.Background, background.LayerType);
        Assert.Equal(6u, background.Uid);

        // Only the first add-on has a top part.
        ObjectPart top = Assert.Single(tile.TopObjectParts);
        Assert.Equal(11, top.IcnIndex);
    }

    [Fact]
    public void LoadMapFixesTheReefsThatTheOriginalEditorMarkedAsRocks()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.XLoc2, 113, Mp2ObjectType.Rock));

        var world = new World();
        world.LoadMap(map);

        // X_LOC2 sprite 113 is a reef; the reference repairs the type before the passability is computed.
        Assert.Equal(Mp2ObjectType.Reefs, world.GetTile(10, 10).MainObjectType);
        Assert.Equal(Direction.Unknown, world.GetTile(10, 10).PassableDirections);
    }

    [Fact]
    public void LoadMapFixesTheTypeOfARandomMonsterPlaceholder()
    {
        Mp2Map map = BlankMap();

        // The original editor writes the wrong type for the placeholder; the reference trusts the sprite.
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.Mons32, 67, Mp2ObjectType.Trees));

        var world = new World();
        world.LoadMap(map);

        // 认出占位符只是第一步：载入时当场抽成一只真的（updateRandomMonster()），所以帧号换掉了、
        // 类型变成怪物、数量按等级抽出来（占位符本身不写数量，元数据是空的）。
        Tile tile = world.GetTile(10, 10);

        Assert.Equal(Mp2ObjectType.ActionMonster, tile.MainObjectType);
        Assert.NotEqual(67, tile.MainObjectPart.IcnIndex);
        Assert.Equal((MonsterType)(tile.MainObjectPart.IcnIndex + 1), tile.GetMonster(world));
        Assert.True(tile.GetMonsterCount(world) > 0);
    }

    [Fact]
    public void LoadMapRollsARandomResourceIntoARealPile()
    {
        Mp2Map map = BlankMap();

        // 随机资源堆：帧号 0 是占位符，载入时抽成某一种资源，本体和左边那格的影子一起换帧。
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnRsrc, 0, Mp2ObjectType.ActionRandomResource));

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(10, 10);

        Assert.Equal(Mp2ObjectType.ActionResource, tile.MainObjectType);

        uint resource = tile.Metadata[0];

        Assert.Contains(resource, new[]
        {
            (uint)Resource.Wood, (uint)Resource.Mercury, (uint)Resource.Ore, (uint)Resource.Sulfur,
            (uint)Resource.Crystal, (uint)Resource.Gems, (uint)Resource.Gold,
        });

        // 帧号 = 资源种类对应的那一帧，本体和影子是同一件物件（参考 updateRandomResource()）。
        Assert.Equal(ResourceIconIndex.ObjectFrameOf((int)resource), tile.MainObjectPart.IcnIndex);
    }

    [Fact]
    public void LoadMapRollsARandomArtifactIntoARealOne()
    {
        Mp2Map map = BlankMap();

        // 随机神器：帧 163 是占位符，载入时抽成一件普通神器，本体和左边那格的影子一起换帧。
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnArti, 163, Mp2ObjectType.ActionRandomArtifact));

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(10, 10);
        ArtifactType artifact = (ArtifactType)tile.Metadata[0];

        Assert.Equal(Mp2ObjectType.ActionArtifact, tile.MainObjectType);
        Assert.NotEqual(ArtifactType.Unknown, artifact);

        // OBJNARTI 每件神器占两帧：本体是奇数帧，影子小一（参考 updateRandomArtifact()）。
        Assert.Equal(((int)artifact - 1) * 2 + 1, tile.MainObjectPart.IcnIndex);
    }

    [Fact]
    public void LoadMapRollsTheRandomArtifactFromTheMapsOwnVersion()
    {
        Mp2Map map = BlankMap();

        // 地图头说自己是 Price of Loyalty：神器要按这个版本的布局抽，不能看进程里的「当前地图」
        // （Settings.CurrentMapFileInfo 是全进程共享的，并行跑测试时曾被另一条用例换成别的版本，
        // 于是这里抽到的是 SuccessionWars 的帧、按别的版本反推，偶发成了 Unknown）。
        map.Info = map.Info with { Version = MapVersion.PriceOfLoyalty };
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnArti, 163, Mp2ObjectType.ActionRandomArtifact));

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(10, 10);
        ArtifactType artifact = (ArtifactType)tile.Metadata[0];

        Assert.NotEqual(ArtifactType.Unknown, artifact);
        Assert.NotEqual(0u,
            (uint)(ArtifactLevels.Level(artifact, MapVersion.PriceOfLoyalty) & ArtifactLevel.AllNormal));

        // 抽出来的帧按同一版本的布局解回去还是同一件神器（PoL 地图上扩展神器也在候选里）。
        Assert.Equal(artifact,
            ArtifactMapSprites.FromMapIndex(tile.MainObjectPart.IcnIndex, MapVersion.PriceOfLoyalty,
                priceOfLoyaltySupported: true));
    }

    [Fact]
    public void LoadMapMovesTheOasisTopSpritesToTheTopLayer()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnDsrt, 105, Mp2ObjectType.Oasis, 4));

        var world = new World();
        world.LoadMap(map);

        Tile tile = world.GetTile(10, 10);

        // The part belongs to the top level, so it leaves the ground parts and the main part stays empty.
        Assert.Equal(Mp2ObjectIcnType.Unknown, tile.MainObjectPart.IcnType);
        ObjectPart top = Assert.Single(tile.TopObjectParts);
        Assert.Equal(Mp2ObjectIcnType.ObjnDsrt, top.IcnType);
        Assert.Equal(105, top.IcnIndex);
        Assert.Equal(4u, top.Uid);
    }

    [Fact]
    public void LoadMapFixesTheSkeletonOnDesert()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnDsrt, 83, Mp2ObjectType.ActionSkeleton));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(Mp2ObjectType.Skeleton, world.GetTile(10, 10).MainObjectType);
    }

    [Fact]
    public void LoadMapClearsTheCoastTypeOfAWaterTile()
    {
        Mp2Map map = BlankMap();
        map.SetTileAt(
            At(10, 10), EmptyTile(MapGrounds.WaterStartImageIndex) with { ObjectType = Mp2ObjectType.Coast });

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(Mp2ObjectType.None, world.GetTile(10, 10).MainObjectType);
    }

    [Fact]
    public void LoadMapComputesThePassabilityOfEveryTile()
    {
        Mp2Map map = BlankMap();

        // One object spanning two tiles: the upper tile becomes impassable.
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 5));
        map.SetTileAt(At(10, 11), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 5));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(Direction.Unknown, world.GetTile(10, 10).PassableDirections);
        Assert.Equal(Directions.All, world.GetTile(9, 10).PassableDirections);
    }

    [Fact]
    public void LoadMapTakesTheUidCounterFromTheFile()
    {
        Mp2Map map = BlankMap() with { LastObjectUid = 3 };

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(3u, world.LastObjectUid);
        Assert.Equal(4u, world.GetNewObjectUid());
        Assert.Equal(4u, world.LastObjectUid);
    }

    [Fact]
    public void LoadMapRaisesTheUidCounterToTheHighestObjectUid()
    {
        // Some official maps store a counter lower than the UIDs of their own objects.
        Mp2Map map = BlankMap() with { LastObjectUid = 3 };
        map.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 42));

        var world = new World();
        world.LoadMap(map);

        Assert.Equal(42u, world.LastObjectUid);
        Assert.Equal(43u, world.GetNewObjectUid());
    }

    [Fact]
    public void LoadMapDropsWhateverWasLoadedBefore()
    {
        var world = new World();

        Mp2Map first = BlankMap();
        first.SetTileAt(At(10, 10), GroundObjectTile(Mp2ObjectIcnType.ObjnCrck, 255, uid: 9));

        world.LoadMap(first);
        Assert.NotEqual(Mp2ObjectIcnType.Unknown, world.GetTile(10, 10).MainObjectPart.IcnType);

        world.LoadMap(BlankMap());

        Assert.Equal(MapSize, world.Width);
        Assert.Equal(MapSize * MapSize, world.Size);
        Assert.Equal(0u, world.LastObjectUid);
        Assert.Equal(Mp2ObjectIcnType.Unknown, world.GetTile(10, 10).MainObjectPart.IcnType);
        Assert.All(world.Tiles, tile => Assert.Equal(Directions.All, tile.PassableDirections));
    }

    [Fact]
    public void LoadMapRejectsAMapWhoseDimensionsDoNotMatchItsTiles()
    {
        Mp2Map map = BlankMap() with { Info = BlankMap().Info with { Width = MapSize + 1 } };

        var world = new World();

        Assert.Throws<ArgumentException>(() => world.LoadMap(map));
    }

    [Fact]
    public void TryLoadMapRefusesAFileThatIsNotAMap()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fheroes2-not-a-map-{Guid.NewGuid():N}.mp2");
        File.WriteAllBytes(path, new byte[1024]);

        try
        {
            var world = new World();

            Assert.False(world.TryLoadMap(path));
            Assert.False(world.TryLoadMap(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.mp2")));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryLoadMapBuildsTheWorldFromARealMap()
    {
        string? dataDirectory = GameData.TryResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        string? mapFile = MapCatalog.FindFiles(MapCatalog.FindDirectories([dataDirectory]))
            .FirstOrDefault(path => Path.GetFileName(path).Equals("Beltway.mp2", StringComparison.OrdinalIgnoreCase));

        if (mapFile is null)
        {
            return;
        }

        var world = new World();

        Assert.True(world.TryLoadMap(mapFile));
        Assert.Equal(72, world.Width);
        Assert.Equal(72 * 72, world.Size);
        Assert.True(world.LastObjectUid > 0);

        for (int i = 0; i < world.Size; ++i)
        {
            Assert.Equal(i, world.Tiles[i].Index);
        }

        // The passability pass may only take bits away: a tile can never become more walkable than its own
        // sprites allow. This is the property the whole neighbour chain exists to preserve.
        Assert.All(world.Tiles, tile =>
            Assert.Equal(Direction.Unknown, tile.PassableDirections & ~tile.GetTileIndependentPassability()));

        // A real map has both: water and mountains that cannot be entered, and open grass that can.
        Assert.Contains(world.Tiles, tile => tile.PassableDirections == Direction.Unknown);
        Assert.Contains(world.Tiles, tile => tile.PassableDirections == Directions.All);
    }

    [Fact]
    public void TheDifficultyPassedInDecidesTheStartingResources()
    {
        // 参考的 Kingdom::Init( color ) 取 Game::getDifficulty()（kingdom.cpp:103），也就是玩家在剧本信息屏上
        // 挑的那一档；地图头里的难度只参与评分（Game::GetRating）。LoadMap 的第二个参数就是这份选择。
        Mp2Map map = BlankMap();

        var impossible = new World();
        impossible.LoadMap(map, GameDifficulty.Impossible);

        Assert.Equal(0, impossible.GetKingdom(PlayerColor.Blue).Treasury.Gold);

        var easy = new World();
        easy.LoadMap(map, GameDifficulty.Easy);

        Assert.Equal(10_000, easy.GetKingdom(PlayerColor.Blue).Treasury.Gold);

        // 没有玩家的选择时（测试与工具）退回地图头那一档，也就是老口径。
        var header = new World();
        header.LoadMap(map);

        Assert.Equal(global::FHeroes2.Game.Kingdom.Kingdom.GetStartingResources(
                map.Info.Difficulty, isAiControlled: false).Gold,
            header.GetKingdom(PlayerColor.Blue).Treasury.Gold);
    }
}
