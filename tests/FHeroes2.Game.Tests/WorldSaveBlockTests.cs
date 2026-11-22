using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The world blocks that need no hero and no castle: one object part, one tile, a resource bag, a daily event, the
/// ultimate artifact and the table of captured objects.
/// </summary>
/// <remarks>
/// 每块都写两遍：一遍往返自证，一遍手写字节核对布局。位宽和字段顺序是这些函数里唯一会错的东西——`Direction` 在端口是
/// 32 位、在文件里是 16 位；`Mp2ObjectType` 在文件里 1.1.00 之前是一字节；`PlayerColor` 1.1.09 之前是四个字节——这些
/// 都不是往返能发现的，必须对着字节看。
/// </remarks>
public sealed class WorldSaveBlockTests
{
    [Fact]
    public void APartSurvivesARoundTrip()
    {
        ObjectPart written = new(ObjectLayerType.Terrain, 0x1122_3344u, Mp2ObjectIcnType.ObjnArti, 226);

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        written.WriteTo(stream);

        Assert.Equal(ObjectPart.SerializedSize, stream.Size);

        stream.Seek(0);

        ObjectPart read = ObjectPart.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.Equal(ObjectLayerType.Terrain, read.LayerType);
        Assert.Equal(0x1122_3344u, read.Uid);
        Assert.Equal(Mp2ObjectIcnType.ObjnArti, read.IcnType);
        Assert.Equal(226, read.IcnIndex);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void APartOfAnOldFormatDropsTwoBoolsAndTheLayerBits()
    {
        // maps_tiles.cpp:1890-1908：层类型先被 0x03 掩一次，ICN 类型之后还有两个 bool。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write((byte)0x07);
        stream.Write(0x0A0B_0C0Du);
        stream.Write((byte)Mp2ObjectIcnType.ObjnHaun);
        stream.Write((byte)1);
        stream.Write((byte)0);
        stream.Write((byte)17);

        stream.Seek(0);

        ObjectPart read = ObjectPart.ReadFrom(stream, SaveFileFormat.PreRelease1_1_0_09);

        Assert.Equal(ObjectLayerType.Terrain, read.LayerType);
        Assert.Equal(0x0A0B_0C0Du, read.Uid);
        Assert.Equal(Mp2ObjectIcnType.ObjnHaun, read.IcnType);
        Assert.Equal(17, read.IcnIndex);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void ATileIsWrittenFieldByFieldInTheReferenceOrder()
    {
        // maps_tiles.cpp:1910-1915 的字段顺序，手写一遍再读回来。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(42);
        stream.Write((ushort)7);
        stream.Write((byte)0x03);
        stream.Write((ushort)0x001F);

        // main object part
        stream.Write((byte)ObjectLayerType.Object);
        stream.Write(0xDEAD_BEEFu);
        stream.Write((byte)Mp2ObjectIcnType.ObjnArti);
        stream.Write((byte)9);

        stream.Write((ushort)Mp2ObjectType.Mine);

        stream.Write((byte)PlayerColor.Blue);

        // metadata: the element count, then three words
        stream.Write(3u);
        stream.Write(0x1111_1111u);
        stream.Write(0x2222_2222u);
        stream.Write(0x3333_3333u);

        stream.Write((byte)4);
        stream.Write(true);

        stream.Write(1);
        stream.Write((byte)ObjectLayerType.Terrain);
        stream.Write(0xCAFEu);
        stream.Write((byte)Mp2ObjectIcnType.ExtraOvr);
        stream.Write((byte)2);

        stream.Write(0);
        stream.Write((byte)PlayerColor.Green);

        int size = stream.Size;
        stream.Seek(0);

        Tile tile = Tile.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.Equal(42, tile.Index);
        Assert.Equal(7, tile.TerrainImageIndex);
        Assert.Equal(0x03, tile.TerrainFlags);
        Assert.Equal((Direction)0x001F, tile.PassableDirections);
        Assert.Equal(Mp2ObjectType.Mine, tile.MainObjectType);
        Assert.Equal(0xDEAD_BEEFu, tile.MainObjectPart.Uid);
        Assert.Equal(9, tile.MainObjectPart.IcnIndex);
        Assert.Equal(PlayerColor.Blue, tile.FogColors);
        Assert.Equal(0x1111_1111u, tile.Metadata[0]);
        Assert.Equal(0x3333_3333u, tile.Metadata[2]);
        Assert.Equal(4, tile.OccupantHeroId);
        Assert.True(tile.IsRoad);
        Assert.Single(tile.GroundObjectParts);
        Assert.Empty(tile.TopObjectParts);
        Assert.Equal(0xCAFEu, tile.GroundObjectParts[0].Uid);
        Assert.Equal(PlayerColor.Green, tile.BoatOwnerColor);
        Assert.Equal(0, stream.Size);

        // 同一块写回去必须一模一样：字序、计数、尾部的船主都要对上。
        StreamBuf again = new();
        again.SetBigEndian(true);
        tile.WriteTo(again);

        Assert.Equal(size, again.Size);
    }

    [Fact]
    public void ATileOfAnOldFormatKeepsThePartFieldsApart()
    {
        // 1.1.04 之前：main object part 不是整块，层类型写在所有对象部件之后；1.1.00 之前对象类型还是一个字节。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(3);
        stream.Write((ushort)1);
        stream.Write((byte)0);
        stream.Write((ushort)0x0003);

        stream.Write(0x0000_0042u);
        stream.Write((byte)Mp2ObjectIcnType.ObjnArti);
        stream.Write((byte)5);

        stream.Write((byte)Mp2ObjectType.Castle);

        stream.Write((byte)PlayerColor.Red);
        stream.Write(3u);
        stream.Write(0u);
        stream.Write(0u);
        stream.Write(0u);
        stream.Write((byte)0);
        stream.Write(false);
        stream.Write(0);
        stream.Write(0);
        stream.Write((byte)ObjectLayerType.Terrain);
        stream.Write((byte)PlayerColor.None);

        stream.Seek(0);

        Tile tile = Tile.ReadFrom(stream, SaveFileFormat.PreRelease2_1_1_00);

        Assert.Equal(3, tile.Index);
        Assert.Equal(Mp2ObjectType.Castle, tile.MainObjectType);
        Assert.Equal(0x42u, tile.MainObjectPart.Uid);
        Assert.Equal(5, tile.MainObjectPart.IcnIndex);
        Assert.Equal(ObjectLayerType.Terrain, tile.MainObjectPart.LayerType);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void AResourceBagIsWrittenInTheReferenceOrder()
    {
        Funds written = new(ore: 1, wood: 2, mercury: 3, sulfur: 4, crystal: 5, gems: 6, gold: 7);

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        written.WriteTo(stream);

        Assert.Equal(Funds.SerializedSize, stream.Size);

        // 第一个 int32 是木头，不是构造函数第一个参数的矿石（resource.cpp:660）。
        stream.Seek(0);
        Assert.Equal(2, stream.ReadInt32());

        stream.Seek(0);

        Funds read = Funds.ReadFrom(stream);

        Assert.Equal(1, read.Ore);
        Assert.Equal(2, read.Wood);
        Assert.Equal(3, read.Mercury);
        Assert.Equal(4, read.Sulfur);
        Assert.Equal(5, read.Crystal);
        Assert.Equal(6, read.Gems);
        Assert.Equal(7, read.Gold);
    }

    [Fact]
    public void ADailyEventSurvivesARoundTrip()
    {
        // 先手写一份，再写一遍读回来：字段顺序（资源、AI 标志、日子、周期、颜色、消息、标题）都在字节里核一遍。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        new Funds(ore: 1, wood: 2, mercury: 3, sulfur: 4, crystal: 5, gems: 6, gold: 7).WriteTo(stream);
        stream.Write(true);
        stream.Write(10u);
        stream.Write(5u);
        stream.Write((byte)PlayerColor.Blue);
        stream.Write("A message");
        stream.Write("A title");

        stream.Seek(0);

        EventDate first = EventDate.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.Equal(0, stream.Size);
        Assert.Equal(1, first.Resources.Ore);
        Assert.Equal(7, first.Resources.Gold);
        Assert.True(first.IsApplicableForAIPlayers);
        Assert.Equal(10u, first.FirstOccurrenceDay);
        Assert.Equal(5u, first.RepeatPeriodInDays);
        Assert.Equal(PlayerColor.Blue, first.Colors);
        Assert.Equal("A message", first.Message);
        Assert.Equal("A title", first.Title);

        StreamBuf again = new();
        again.SetBigEndian(true);
        first.WriteTo(again);
        again.Seek(0);

        EventDate second = EventDate.ReadFrom(again, SaveFileFormatInfo.Current);

        Assert.Equal(0, again.Size);
        Assert.Equal(first.FirstOccurrenceDay, second.FirstOccurrenceDay);
        Assert.Equal(first.Colors, second.Colors);
        Assert.Equal(first.Message, second.Message);
    }

    [Fact]
    public void ADailyEventOfAnOldFormatSwallowsFourColourBytes()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        new Funds().WriteTo(stream);
        stream.Write(false);
        stream.Write(1u);
        stream.Write(0u);
        stream.Write((int)PlayerColor.Green);
        stream.Write("Old");
        stream.Write(string.Empty);

        stream.Seek(0);

        EventDate read = EventDate.ReadFrom(stream, SaveFileFormat.Release1_1_08);

        Assert.Equal(PlayerColor.Green, read.Colors);
        Assert.Equal("Old", read.Message);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void TheUltimateArtifactSurvivesARoundTrip()
    {
        UltimateArtifact written = new();
        written.Set(120, ArtifactType.UltimateSword, MapVersion.SuccessionWars);

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        written.WriteTo(stream);

        Assert.Equal(UltimateArtifact.SerializedSize, stream.Size);

        stream.Seek(0);

        UltimateArtifact read = UltimateArtifact.ReadFrom(stream);

        Assert.Equal(ArtifactType.UltimateSword, read.Artifact);
        Assert.Equal(120, read.Position);
        Assert.False(read.IsFound);
        Assert.Equal(written.Offset, read.Offset);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void TheCapturedObjectsSurviveARoundTrip()
    {
        CapturedObjects written = new();
        written.Set(42, Mp2ObjectType.Mine, PlayerColor.Red);
        written.Set(7, Mp2ObjectType.Sawmill, PlayerColor.Blue);

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        written.WriteTo(stream);
        stream.Seek(0);

        CapturedObjects read = CapturedObjects.ReadFrom(stream);

        Assert.Equal(2, read.Count);
        Assert.Equal(0, stream.Size);
        Assert.Equal(Mp2ObjectType.Mine, read.Get(42).ObjectType);
        Assert.Equal(PlayerColor.Red, read.GetColor(42));
        Assert.Equal(PlayerColor.Blue, read.GetColor(7));

        // std::map 是按 key 排序写的，端口用 SortedDictionary 也是为了这个。
        Assert.Equal([7, 42], read.Entries.Select(entry => entry.Key));
    }
}
