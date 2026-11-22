using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The data objects on the map — ground events, sphinxes, signs — as a save file sees them.
/// </summary>
/// <remarks>
/// 这一层最容易错的是三处：位置的坐标是两个 <c>int16</c> 而不是 <c>Point</c> 的两个 <c>int32</c>；UID 在外层写一次、
/// 在对象体里又写一次；类型在 1.1.03 之前是四个字节。每条都有对应测试，且断言的是字节而不是往返。
/// </remarks>
public sealed class WorldMapObjectsSaveTests
{
    private const int Width = 10;

    [Fact]
    public void AnEventIsWrittenInTheReferenceOrder()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        WritePosition(stream, 3, 2, uid: 23);
        new Funds(ore: 1, wood: 2, mercury: 3, sulfur: 4, crystal: 5, gems: 6, gold: 7).WriteTo(stream);
        stream.Write((int)ArtifactType.UltimateSword);
        stream.Write(0);
        stream.Write(true);
        stream.Write(false);
        stream.Write((byte)PlayerColor.Red);
        stream.Write("A gift");
        stream.Write((int)SecondarySkill.Leadership);
        stream.Write((int)SkillLevel.Expert);
        stream.Write(250);

        byte[] expected = Written(stream);
        stream.Seek(0);

        MapEvent mapEvent = MapEvent.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(23, mapEvent.TileIndex);
        Assert.Equal(23u, mapEvent.Uid);
        Assert.Equal(1, mapEvent.Resources.Ore);
        Assert.Equal(7, mapEvent.Resources.Gold);
        Assert.Equal(ArtifactType.UltimateSword, mapEvent.Artifact);
        Assert.True(mapEvent.IsComputerPlayerAllowed);
        Assert.False(mapEvent.IsSingleTimeEvent);
        Assert.Equal(PlayerColor.Red, mapEvent.Colors);
        Assert.Equal("A gift", mapEvent.Message);
        Assert.Equal(SecondarySkill.Leadership, mapEvent.SecondarySkill.Skill);
        Assert.Equal(SkillLevel.Expert, mapEvent.SecondarySkill.Level);
        Assert.Equal(250, mapEvent.Experience);

        StreamBuf again = new();
        again.SetBigEndian(true);
        mapEvent.WriteTo(again, new MapGeometry(Width, 10));

        Assert.Equal(expected, Written(again));
    }

    [Fact]
    public void AnEventOfAnOldFormatHasNoSkillAndNoExperience()
    {
        // 1.1.06 之前没有技能与经验；1.1.09 之前颜色是 int32。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        WritePosition(stream, 4, 1, uid: 14);
        new Funds().WriteTo(stream);
        stream.Write(0);
        stream.Write(0);
        stream.Write(false);
        stream.Write(false);
        stream.Write((int)PlayerColor.Green);
        stream.Write("Old");

        stream.Seek(0);

        MapEvent mapEvent = MapEvent.ReadFrom(stream, SaveFileFormat.Release1_1_04, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(PlayerColor.Green, mapEvent.Colors);
        Assert.Equal("Old", mapEvent.Message);
        Assert.False(mapEvent.SecondarySkill.IsValid);
        Assert.Equal(0, mapEvent.Experience);
    }

    [Fact]
    public void ASphinxWritesItsAnswersBehindACount()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        WritePosition(stream, 1, 0, uid: 1);
        new Funds().WriteTo(stream);
        stream.Write(0);
        stream.Write(0);
        stream.Write(2u);
        stream.Write("first");
        stream.Write("second");
        stream.Write("What walks on four legs?");
        stream.Write(true);
        stream.Write(false);

        byte[] expected = Written(stream);
        stream.Seek(0);

        MapSphinx sphinx = MapSphinx.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(["first", "second"], sphinx.Answers);
        Assert.Equal("What walks on four legs?", sphinx.Riddle);
        Assert.True(sphinx.IsValid);
        Assert.False(sphinx.IsTruncatedAnswer);

        StreamBuf again = new();
        again.SetBigEndian(true);
        sphinx.WriteTo(again, new MapGeometry(Width, 10));

        Assert.Equal(expected, Written(again));
    }

    [Fact]
    public void ASphinxOfAnOldFormatComparesOnFourCharacters()
    {
        // 1.1.00 之前没有末尾那个标志，读到就意味着永远是 true，且不从流里取字节。那个版本也比 1.1.03 早，
        // 所以位置仍是「空字 → UID → 坐标」的老序。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(0);
        stream.Write(2u);
        stream.Write((short)2);
        stream.Write((short)0);
        new Funds().WriteTo(stream);
        stream.Write(0);
        stream.Write(0);
        stream.Write(1u);
        stream.Write("answer");
        stream.Write("riddle");
        stream.Write(true);

        stream.Seek(0);

        MapSphinx sphinx = MapSphinx.ReadFrom(stream, SaveFileFormat.PreRelease3_1_1_00, new MapGeometry(Width, 10));

        Assert.True(sphinx.IsTruncatedAnswer);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void ASignOfAnOldFormatHasNoLanguageTag()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        WritePosition(stream, 6, 0, uid: 6);
        stream.Write("On the shore");
        stream.Seek(0);

        MapSign sign = MapSign.ReadFrom(stream, SaveFileFormat.Release1_1_06, new MapGeometry(Width, 10));

        Assert.Equal("On the shore", sign.Message.Text);
        Assert.Null(sign.Message.Language);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void ASignKeepsItsLanguageWhenTheFormatCarriesOne()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        WritePosition(stream, 6, 0, uid: 6);
        new LocalizedString("Le trésor", SupportedLanguage.French).Write(stream);
        stream.Seek(0);

        MapSign sign = MapSign.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal("Le trésor", sign.Message.Text);
        Assert.Equal(SupportedLanguage.French, sign.Message.Language);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void TheTableWritesTheUidTwiceAndTheTypeAsAWord()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(1u);
        stream.Write(6u);
        stream.Write((ushort)Mp2ObjectType.Sign);
        WritePosition(stream, 6, 0, uid: 6);
        stream.Write("Here");

        // 1.1.07 起留言带一个「有语言」标志，这里没有语言。
        stream.Write(false);

        byte[] expected = Written(stream);
        stream.Seek(0);

        MapObjects objects = MapObjects.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(1, objects.Count);
        Assert.Equal("Here", objects.Get<MapSign>(6)!.Message.Text);

        StreamBuf again = new();
        again.SetBigEndian(true);
        objects.WriteTo(again, new MapGeometry(Width, 10));

        Assert.Equal(expected, Written(again));
    }

    [Fact]
    public void TheTableOfAnOldFormatReadsTheTypeAsAFullWord()
    {
        // 1.1.03 之前：类型是 int32，位置也不是第一项——先是 sw 个空字节，然后 UID，然后位置。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(1u);
        stream.Write(6u);
        stream.Write((int)Mp2ObjectType.Sign);
        stream.Write(0);
        stream.Write(6u);
        stream.Write((short)6);
        stream.Write((short)0);
        stream.Write("Here");

        stream.Seek(0);

        MapObjects objects = MapObjects.ReadFrom(stream, SaveFileFormat.PreRelease2_1_1_00, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(1, objects.Count);
        Assert.Equal(6, objects.Get<MapSign>(6)!.TileIndex);
    }

    [Fact]
    public void AnUnknownObjectTypeFailsTheStream()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(1u);
        stream.Write(9u);
        stream.Write((ushort)Mp2ObjectType.Mine);
        stream.Seek(0);

        MapObjects objects = MapObjects.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.True(stream.Fail);
        Assert.Equal(0, objects.Count);
    }

    /// <summary>
    /// Writes what <c>MapBaseObject::operator&lt;&lt;</c> writes: two <c>int16_t</c> and a <c>uint32_t</c>
    /// (<c>maps_objects.cpp:324-328</c>).
    /// </summary>
    private static void WritePosition(StreamBase stream, short x, short y, uint uid)
    {
        stream.Write(x);
        stream.Write(y);
        stream.Write(uid);
    }

    private static byte[] Written(StreamBuf stream) => stream.Written.ToArray();
}
