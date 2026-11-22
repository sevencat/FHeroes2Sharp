using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using KingdomState = FHeroes2.Game.Kingdom.Kingdom;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The seven kingdoms, the puzzle map and the two tavern slots as a save file sees them.
/// </summary>
/// <remarks>
/// 这一层有两处「写对了读错、读对了写错」的陷阱：拼图位图里第一个字符是 47 号瓦片（不是 0 号），以及 1.0.10 之前
/// 英雄 id 整体差一。两条都用手写字节钉住，不靠往返自证。
/// </remarks>
public sealed class KingdomSaveTests
{
    [Fact]
    public void ThePuzzleMapStartsWithTheHighestTile()
    {
        // to_string() 先打高位，所以第一个字符是 47 号瓦片（puzzle.cpp:311-321、365-368）。
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        stream.Write("1" + new string('0', Puzzle.NumOfPuzzleTiles - 1));
        WriteEmptyZones(stream);

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        Puzzle puzzle = new();
        puzzle.ReadFrom(stream);

        Assert.Equal(0, stream.Size);
        Assert.True(puzzle[Puzzle.NumOfPuzzleTiles - 1]);
        Assert.False(puzzle[0]);

        StreamBuf again = new();
        again.SetBigEndian(true);
        puzzle.WriteTo(again);

        Assert.Equal(expected, again.Written.ToArray());
    }

    [Fact]
    public void ThePuzzleMapKeepsItsZoneOrders()
    {
        Puzzle puzzle = new();

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        puzzle.WriteTo(stream);

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        Puzzle restored = new();
        restored.ReadFrom(stream);

        Assert.Equal(0, stream.Size);

        for (int tile = 0; tile < Puzzle.NumOfPuzzleTiles; ++tile)
        {
            Assert.Equal(puzzle[tile], restored[tile]);
        }

        StreamBuf again = new();
        again.SetBigEndian(true);
        restored.WriteTo(again);

        Assert.Equal(expected, again.Written.ToArray());
    }

    [Fact]
    public void TheTavernSlotsAreAnIdAndASurrenderDay()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        stream.Write(5);
        stream.Write(120u);
        stream.Write(Hero.UnknownId);
        stream.Write(0u);
        stream.Seek(0);

        Recruits recruits = new();
        recruits.ReadFrom(stream, SaveFileFormatInfo.Current);

        Assert.Equal(0, stream.Size);
        Assert.True(recruits.First.IsValid);
        Assert.Equal(5, recruits.Id1);
        Assert.Equal(120u, recruits.GetSurrenderDayOfHero1());
        Assert.False(recruits.Second.IsValid);
        Assert.Equal(Hero.UnknownId, recruits.Id2);
    }

    [Fact]
    public void TheTavernSlotsOfAnOldFormatShiftTheirIds()
    {
        // 1.0.10 之前 UNKNOWN 是 72，所有 id 整体小一（heroes_recruits.cpp:115-118）。
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        stream.Write(4);
        stream.Write(0u);
        stream.Write(71);
        stream.Write(0u);
        stream.Seek(0);

        Recruits recruits = new();
        recruits.ReadFrom(stream, SaveFileFormat.Release1_0_09);

        Assert.Equal(5, recruits.Id1);
        Assert.Equal(72, recruits.Id2);
    }

    [Fact]
    public void AKingdomIsWrittenFieldByField()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(0u);
        stream.Write((byte)PlayerColor.Red);
        new Funds().WriteTo(stream);
        stream.Write(0u);
        stream.Write(0u);
        stream.Write(0u);
        stream.Write(0);
        stream.Write(0u);
        stream.Write(0);
        stream.Write(0);
        stream.Write(0u);
        WriteEmptyPuzzle(stream);
        stream.Write(0);
        stream.Write(-1);
        stream.Write(-1);
        stream.Write(0u);

        stream.Seek(0);

        KingdomState kingdom = KingdomState.ReadFrom(stream, SaveFileFormatInfo.Current, new World());

        Assert.Equal(0, stream.Size);
        Assert.Equal(PlayerColor.Red, kingdom.Color);
        Assert.Equal(-1, kingdom.TopCastleInKingdomView);
        Assert.Equal(-1, kingdom.TopHeroInKingdomView);
        Assert.Empty(kingdom.Castles);
        Assert.Empty(kingdom.Heroes);
    }

    [Fact]
    public void AKingdomOfAnOldFormatHasAnIntColourAndAWordOfNothing()
    {
        // 1.1.09 之前颜色是 int32；1.1.00 前第二版之前，两个视图指针前还有一个空字（kingdom.cpp:916-933）。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(0u);
        stream.Write((int)PlayerColor.Green);
        new Funds().WriteTo(stream);
        stream.Write(0u);
        stream.Write(0u);
        stream.Write(0u);
        stream.Write(0);
        stream.Write(0u);
        stream.Write(0);
        stream.Write(0);
        stream.Write(0u);
        WriteEmptyPuzzle(stream);
        stream.Write(0);
        stream.Write(0);
        stream.Write(7);
        stream.Write(3);
        stream.Seek(0);

        KingdomState kingdom = KingdomState.ReadFrom(stream, SaveFileFormat.Release1_0_10, new World());

        Assert.Equal(0, stream.Size);
        Assert.Equal(PlayerColor.Green, kingdom.Color);
        Assert.Equal(7, kingdom.TopCastleInKingdomView);
        Assert.Equal(3, kingdom.TopHeroInKingdomView);
    }

    [Fact]
    public void AHeroOfARealWorldIsFoundAgainById()
    {
        Settings.Reset();
        Settings.Get().Players.Init(PlayerColor.Blue);

        World world = new();
        world.GenerateUninitializedMap(8);
        world.Kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.None);
        world.Heroes.Init(MapVersion.SuccessionWars);

        KingdomState kingdom = world.GetKingdom(PlayerColor.Blue);
        Hero hero = world.Heroes.First(static candidate => candidate is not null)!;
        kingdom.AddHero(hero);

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        kingdom.WriteTo(stream);
        stream.Seek(0);

        KingdomState restored = KingdomState.ReadFrom(stream, SaveFileFormatInfo.Current, world);

        Assert.Equal(0, stream.Size);
        Assert.Equal(PlayerColor.Blue, restored.Color);
        Assert.Equal([hero.Id], restored.Heroes.Select(static candidate => candidate.Id));

        Settings.Reset();
    }

    [Fact]
    public void AKingdomWhoseCastleDoesNotExistFailsTheStream()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(0u);
        stream.Write((byte)PlayerColor.Red);
        new Funds().WriteTo(stream);
        stream.Write(0u);
        stream.Write(1u);
        stream.Write(1234);
        stream.Seek(0);

        KingdomState kingdom = KingdomState.ReadFrom(stream, SaveFileFormatInfo.Current, new World());

        Assert.True(stream.Fail);
        Assert.Empty(kingdom.Castles);
    }

    [Fact]
    public void AKingdomWhoseHeroDoesNotExistFailsTheStream()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(0u);
        stream.Write((byte)PlayerColor.Red);
        new Funds().WriteTo(stream);
        stream.Write(0u);
        stream.Write(0u);
        stream.Write(1u);
        stream.Write(1);
        stream.Seek(0);

        KingdomState kingdom = KingdomState.ReadFrom(stream, SaveFileFormatInfo.Current, new World());

        Assert.True(stream.Fail);
        Assert.Empty(kingdom.Heroes);
    }

    [Fact]
    public void TheSevenKingdomsAreACountAndThatManyKingdoms()
    {
        Kingdoms kingdoms = new();

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        kingdoms.WriteTo(stream);

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        Kingdoms restored = new();
        restored.ReadFrom(stream, SaveFileFormatInfo.Current, new World());

        Assert.Equal(0, stream.Size);

        StreamBuf again = new();
        again.SetBigEndian(true);
        restored.WriteTo(again);

        Assert.Equal(expected, again.Written.ToArray());
    }

    [Fact]
    public void ATableThatIsNotSevenKingdomsFailsTheStream()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        stream.Write(Kingdoms.MaxPlayers);
        stream.Seek(0);

        Kingdoms kingdoms = new();
        kingdoms.ReadFrom(stream, SaveFileFormatInfo.Current, new World());

        Assert.True(stream.Fail);
    }

    /// <summary>Writes an all-closed puzzle map with four empty zone orders.</summary>
    /// <param name="stream">The stream to write to.</param>
    private static void WriteEmptyPuzzle(StreamBuf stream)
    {
        stream.Write(new string('0', Puzzle.NumOfPuzzleTiles));
        WriteEmptyZones(stream);
    }

    /// <summary>Writes the four zero counts of four empty zone orders.</summary>
    /// <param name="stream">The stream to write to.</param>
    private static void WriteEmptyZones(StreamBuf stream)
    {
        stream.Write((byte)0);
        stream.Write((byte)0);
        stream.Write((byte)0);
        stream.Write((byte)0);
    }
}
