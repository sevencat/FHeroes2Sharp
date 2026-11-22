using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The whole world as a save file, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>World</c>
/// (<c>world.cpp:1448-1515</c>).
/// </summary>
/// <remarks>
/// 世界这一层自己的东西不多，主要风险在顺序与「读回来之后还是不是同一个世界」：宽高与瓦片数打头、迷雾在瓦片里，
/// 以及读档要换掉整个地图而不是叠上去。逐字段的老版本布局在各自的 <c>*SaveTests</c> 里钉过了，这里管调度。
/// </remarks>
public sealed class WorldSaveTests
{
    private const int MapSize = 36;

    /// <summary>Tile the hero of every test stands on; grass and far from the edges.</summary>
    private const int HeroTileIndex = 37;

    private const int HeroColumn = HeroTileIndex % MapSize;
    private const int HeroRow = HeroTileIndex / MapSize;

    [Fact]
    public void TheWorldSurvivesARoundtripByteForByte()
    {
        World world = new();
        world.LoadMap(MapWithHero());

        byte[] written = Save(world);

        World restored = new();
        StreamBuf stream = Read(written);
        restored.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.False(stream.Fail);
        Assert.Equal(MapSize, restored.Width);
        Assert.Equal(MapSize, restored.Height);
        Assert.Equal(MapSize * MapSize, restored.Tiles.Count);
        Assert.Equal(HeroTileIndex, restored.Heroes.GetAtMapIndex(HeroTileIndex)?.MapIndex);
        Assert.Equal(world.DayCount, restored.DayCount);
        Assert.Equal(world.MapSeed, restored.MapSeed);

        // 写回来还是同一串字节：说明读出来的每个算子都放回了它读走的那些位。
        Assert.Equal(written, Save(restored));
    }

    [Fact]
    public void TheFileStartsWithTheWidthTheHeightAndTheTileCount()
    {
        World world = new();
        world.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        byte[] written = Save(world);
        StreamBuf stream = Read(written);

        Assert.Equal(MapSize, stream.ReadInt32());
        Assert.Equal(MapSize, stream.ReadInt32());
        Assert.Equal(MapSize * MapSize, (int)stream.ReadUInt32());
    }

    [Fact]
    public void TheFogComesBackWithTheWorld()
    {
        World world = new();
        world.LoadMap(MapWithHero());
        Assert.True(world.ClearFog(PlayerColor.Blue) > 0);

        World restored = new();
        StreamBuf stream = Read(Save(world));
        restored.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.False(stream.Fail);

        // 蓝方的侦查结果跟着存档走，红方仍然全黑——迷雾是分颜色的，不能读成「谁都看不见」或「谁都看得见」。
        Assert.False(restored.Fog!.IsFogged(HeroColumn, HeroRow, PlayerColor.Blue));
        Assert.True(restored.Fog.IsFogged(HeroColumn, HeroRow, PlayerColor.Red));

        // 方向掩码不在存档里，参考在开局时按玩家重算（game_startgame.cpp:863 / :922），这里也照做一遍。
        restored.Fog.UpdateDirections(PlayerColor.Blue);

        Assert.Equal(world.Fog!.CountFogged(), restored.Fog.CountFogged());
        Assert.True(restored.Fog.CountFogged() < MapSize * MapSize);
    }

    [Fact]
    public void LoadingASaveReplacesTheMapTheWorldAlreadyHad()
    {
        World withHero = new();
        withHero.LoadMap(MapWithHero());
        byte[] heroSave = Save(withHero);

        World blank = new();
        blank.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));
        byte[] blankSave = Save(blank);

        World world = new();
        world.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        StreamBuf stream = Read(heroSave);
        world.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.False(stream.Fail);
        Assert.NotNull(world.Heroes.GetAtMapIndex(HeroTileIndex));

        stream = Read(blankSave);
        world.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.False(stream.Fail);
        Assert.Null(world.Heroes.GetAtMapIndex(HeroTileIndex));
        Assert.Null(world.GetTile(HeroTileIndex).GetHero(world));
    }

    [Fact]
    public void ASaveWithTheWrongTileCountLeavesNoMap()
    {
        World world = new();
        world.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        byte[] written = Save(world);

        // 瓦片数在宽高之后，第三个 int32。
        written[8] = 0;
        written[9] = 0;
        written[10] = 0x05;
        written[11] = 0x0F;

        World restored = new();
        StreamBuf stream = Read(written);
        restored.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.True(stream.Fail);
        Assert.Equal(0, restored.Width);
        Assert.Equal(0, restored.Height);
        Assert.Empty(restored.Tiles);
        Assert.Null(restored.Fog);
    }

    [Fact]
    public void ATruncatedSaveLeavesNoMapEither()
    {
        World world = new();
        world.LoadMap(MapWithHero());

        byte[] written = Save(world);
        byte[] truncated = written[..(written.Length / 2)];

        World restored = new();
        StreamBuf stream = Read(truncated);
        restored.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.True(stream.Fail);
        Assert.Equal(0, restored.Width);
        Assert.Empty(restored.Tiles);
    }

    [Fact]
    public void AWorldWithoutAMapRefusesToBeSaved()
    {
        World world = new();

        Assert.Throws<InvalidOperationException>(() => Save(world));
    }

    private static byte[] Save(World world)
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        world.WriteTo(stream);

        return stream.Written.ToArray();
    }

    private static StreamBuf Read(byte[] bytes)
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        stream.PutRaw(bytes);
        stream.Seek(0);

        return stream;
    }

    /// <summary>
    /// A map with one hero tile and one hero information block, the smallest map that places a hero, the same
    /// fixture the fog tests use (<c>world_loadmap.cpp:528</c>).
    /// </summary>
    private static Mp2Map MapWithHero()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTileIndex, HeroTile(blockId: 1));

        byte[] block = new byte[Mp2HeroInfo.StructureSize];
        block[17] = 1;

        return map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    private static Mp2TileInfo HeroTile(int blockId) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = 0,
        Quantity1 = (byte)((blockId << 3) | 1),
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Hero,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };
}
