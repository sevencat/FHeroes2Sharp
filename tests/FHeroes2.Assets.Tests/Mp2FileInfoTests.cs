using System.Buffers.Binary;
using System.Text;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="Mp2FileInfo"/> and <see cref="MapCatalog"/>, the port of
/// <c>Maps::FileInfo::readMP2Map</c> and <c>Maps::getAllMapFileInfos</c>. The synthetic headers below
/// exercise every field of the 428 byte information block; the tests that need the real game data
/// return early when it is missing (see docs/MISC.md).
/// </summary>
public sealed class Mp2FileInfoTests
{
    [Fact]
    public void Read_ParsesTheMapInformationBlock()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2");

        Mp2FileInfo info = Mp2FileInfo.Read(map.FilePath);

        Assert.Equal(map.FilePath, info.FilePath);
        Assert.Equal("Test Map", info.Name);
        Assert.Equal("A map made for the port.", info.Description);
        Assert.Equal(72, info.Width);
        Assert.Equal(72, info.Height);
        Assert.Equal(GameDifficulty.Normal, info.Difficulty);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, info.KingdomColors);
        Assert.Equal(PlayerColor.Blue, info.HumanPlayers);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, info.ComputerPlayers);
        Assert.Equal(PlayerColor.Green, info.RandomRacePlayers);
        Assert.Equal(MapRace.Knight, info.RaceOf(PlayerColor.Blue));
        Assert.Equal(MapRace.Random, info.RaceOf(PlayerColor.Green));
        Assert.Equal(MapRace.Necromancer, info.RaceOf(PlayerColor.Purple));
        Assert.Equal(MapVictoryCondition.CaptureTown, info.VictoryCondition);
        Assert.Equal(MapLossCondition.OutOfTime, info.LossCondition);
        Assert.True(info.ComputerAlsoWins);
        Assert.True(info.AllowNormalVictory);
        Assert.Equal([12, 34], info.VictoryConditionParameters);
        Assert.Equal([30, 21], info.LossConditionParameters);
        Assert.True(info.StartWithHeroInFirstCastle);
        Assert.Equal(MapVersion.SuccessionWars, info.Version);
    }

    [Fact]
    public void Read_DerivesTheVersionFromTheExtension()
    {
        using TemporaryMap map = TemporaryMap.Create(".mx2");

        Assert.Equal(MapVersion.PriceOfLoyalty, Mp2FileInfo.Read(map.FilePath).Version);
    }

    [Fact]
    public void Read_ComputesTheColorSubsets()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2");

        Mp2FileInfo info = Mp2FileInfo.Read(map.FilePath);

        Assert.Equal(PlayerColor.None, info.HumanOnlyColors);
        Assert.Equal(PlayerColor.Red, info.ComputerOnlyColors);
        Assert.Equal(PlayerColor.Blue, info.SharedColors);
        Assert.Equal(1, info.HumanPlayerCount);
        Assert.Equal(2, info.ComputerPlayerCount);
        Assert.Equal(2, info.PlayerCount);
    }

    [Fact]
    public void Read_ReportsAnEmptyDescription()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2", header => header.AsSpan(Mp2FileInfo.DescriptionFieldOffset, Mp2FileInfo.DescriptionFieldLength).Clear());

        Assert.Equal(string.Empty, Mp2FileInfo.Read(map.FilePath).Description);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Read_RejectsAnUnsupportedDifficulty(int difficulty)
    {
        using TemporaryMap map = TemporaryMap.Create(
            ".mp2",
            header => BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), (ushort)difficulty));

        Assert.Throws<BinaryFormatException>(() => Mp2FileInfo.Read(map.FilePath));
    }

    [Fact]
    public void Read_RejectsAForeignFile()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2", header => header[0] = 0x00);

        Assert.Throws<BinaryFormatException>(() => Mp2FileInfo.Read(map.FilePath));
    }

    [Fact]
    public void Read_RejectsAMapWithoutHumanPlayers()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2", header => header.AsSpan(14, 6).Clear());

        Assert.Throws<BinaryFormatException>(() => Mp2FileInfo.Read(map.FilePath));
    }

    [Fact]
    public void Read_AcceptsAMapWithoutHumanPlayersForTheEditor()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2", header => header.AsSpan(14, 6).Clear());

        Mp2FileInfo info = Mp2FileInfo.Read(map.FilePath, forEditor: true);

        Assert.Equal(PlayerColor.None, info.HumanPlayers);
        Assert.Equal("Test Map", info.Name);
    }

    [Fact]
    public void Read_RejectsAMapWithoutName()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2", header => header.AsSpan(Mp2FileInfo.NameFieldOffset, Mp2FileInfo.NameFieldLength).Clear());

        Assert.Throws<BinaryFormatException>(() => Mp2FileInfo.Read(map.FilePath));
    }

    [Fact]
    public void TryRead_ReturnsFalseForATruncatedFile()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2", header => Array.Clear(header));

        Assert.False(Mp2FileInfo.TryRead(map.FilePath, out Mp2FileInfo? info));
        Assert.Null(info);
    }

    [Fact]
    public void TryRead_ReturnsFalseForAMissingFile()
    {
        string missing = Path.Combine(Path.GetTempPath(), "fh2-missing-" + Guid.NewGuid().ToString("N") + ".mp2");

        Assert.False(Mp2FileInfo.TryRead(missing, out Mp2FileInfo? info));
        Assert.Null(info);
    }

    [Fact]
    public void RestrictHumanColorsTo_NarrowsTheHumanPlayers()
    {
        using TemporaryMap map = TemporaryMap.Create(".mp2");

        Mp2FileInfo info = Mp2FileInfo.Read(map.FilePath).RestrictHumanColorsTo(PlayerColor.Red | PlayerColor.Purple);

        Assert.Equal(PlayerColor.Red | PlayerColor.Purple, info.HumanPlayers);
        Assert.Equal(2, info.HumanPlayerCount);
    }
}
