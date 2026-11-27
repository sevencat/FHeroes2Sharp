using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The editor's castle entry, <c>editor_interface.cpp:2232-2246</c>: a click on a town in edit mode opens the castle
/// details window, and only an accepted window writes the metadata back.
/// </summary>
/// <remarks>
/// 窗口本身是模态的，用例不真的起帧循环：<see cref="EditorScene.RunModal"/> 是宿主留的缝，换成一次「按 OKAY」或
/// 「按 EXIT」就够了。地形图块缺失时（机器上没有游戏数据）用例直接返回。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorSceneCastleDetailsTests : IDisposable
{
    private const int MapSize = 36;
    private const uint CastleUid = 7;
    private const int CastleX = 10;
    private const int CastleY = 10;

    private static readonly Size FrameSize = new(640, 480);

    /// <summary>
    /// The archives, kept open for the whole test: the castle window reads its artwork lazily, so a bundle that is
    /// already disposed blows up inside the first frame the modal tries to paint.
    /// </summary>
    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    /// <summary>Publishes the archives, which the synthetic button tables look up through the current bundle.</summary>
    public EditorSceneCastleDetailsTests()
    {
        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }
    }

    /// <summary>Restores the previous bundle and closes the archives the scene borrowed.</summary>
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void ClickingATownOpensItsWindowWithTheBlockContent()
    {
        Mp2Map map = MapWithCastle();
        EditorScene? scene = CreateScene(map);

        if (scene is null)
        {
            return;
        }

        bool opened = false;

        scene.RunModal = screen =>
        {
            Assert.IsType<FHeroes2.App.Modals.EditorCastleDetailsModal>(screen);

            opened = true;

            // 这一层拿不到窗口的内部状态，所以用「按 OKAY 后表里出现条目」来确认它真的开了。
            screen.HotKey(ButtonHotKey.Okay);
        };

        Assert.True(scene.OpenCastleDetails(new Point(CastleX, CastleY)));
        Assert.True(opened);
        Assert.Equal(new Point(CastleX, CastleY), scene.LastCastleDetailsTile);

        Assert.True(scene.Metadata.Castles.TryGetValue(CastleUid, out CastleMetadata? written));

        Assert.Equal("Old Keep", written.CustomName);
        Assert.True(written.CustomBuildings);

        BuildingType built = MapFormatArmy.GetBuiltBuildings(written);

        Assert.True(built.HasFlag(BuildingType.Tavern));
        Assert.True(built.HasFlag(BuildingType.DwellingMonster1));
    }

    [Fact]
    public void CancellingTheWindowLeavesTheTableUntouched()
    {
        Mp2Map map = MapWithCastle();
        EditorScene? scene = CreateScene(map);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen => screen.HotKey(ButtonHotKey.Cancel);

        Assert.True(scene.OpenCastleDetails(new Point(CastleX, CastleY)));
        Assert.Empty(scene.Metadata.Castles);
        Assert.Equal(new Point(CastleX, CastleY), scene.LastCastleDetailsTile);
    }

    [Fact]
    public void APreviouslyAcceptedCastleIsTheOneReopened()
    {
        Mp2Map map = MapWithCastle();
        EditorScene? scene = CreateScene(map);

        if (scene is null)
        {
            return;
        }

        // 第一次打开：表里的名字会被换成作者改的那个。
        scene.Metadata.Castles[CastleUid] = new CastleMetadata { CustomName = "Renamed Keep" };

        scene.RunModal = screen => screen.HotKey(ButtonHotKey.Okay);

        Assert.True(scene.OpenCastleDetails(new Point(CastleX, CastleY)));
        Assert.Equal("Renamed Keep", scene.Metadata.Castles[CastleUid].CustomName);
    }

    [Fact]
    public void APlainTileIsNotACastleEntry()
    {
        EditorScene? scene = CreateScene(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on plain ground");

        Assert.False(scene.OpenCastleDetails(new Point(3, 4)));
        Assert.Null(scene.LastCastleDetailsTile);
        Assert.Empty(scene.Metadata.Castles);
    }

    /// <summary>Opens the editor on the map, or reports the missing data by returning <see langword="null"/>.</summary>
    /// <param name="map">The map to edit.</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(Mp2Map map)
    {
        var frame = new ScreenBuffer(FrameSize, new Palette());

        // 城堡详情窗要字体（标题、按钮），所以这里不能像别处那样传 null。
        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateFromMap(frame, _assets, fonts, map, out _);
    }

    /// <summary>
    /// A map with one town on (10, 10): a castle tile, the castle table entry that gives its race, and the 70-byte
    /// block the window reads.
    /// </summary>
    private static Mp2Map MapWithCastle()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTile(CastleX, CastleY, map.Tile(CastleX, CastleY) with
        {
            ObjectType = Mp2ObjectType.Castle,
            Level1ObjectUid = CastleUid,

            // The block id lives in quantity1/quantity2 three bits up; id 1 is 8.
            Quantity1 = 8,
        });

        byte[] block = TownBlock();

        return map with
        {
            Castles = [new Mp2CastlePosition(CastleX, CastleY, 0x02, MapRace.Sorceress)],
            InfoBlocks = [new Mp2InfoBlock(0, block)],
        };
    }

    /// <summary>
    /// A town block with custom buildings (a tavern and the first dwelling), a name and a red owner.
    /// </summary>
    private static byte[] TownBlock()
    {
        byte[] data = new byte[70];

        data[0] = 2;
        data[1] = 1;
        data[2] = 0x04;
        data[4] = 0x08;
        data[24] = 1;

        byte[] name = MapTextCodec.Encode("Old Keep", 13);

        name.CopyTo(data, 25);

        return data;
    }
}
