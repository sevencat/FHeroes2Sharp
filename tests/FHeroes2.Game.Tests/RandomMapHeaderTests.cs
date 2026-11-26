using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The header a generated map leaves behind: <c>updatePlayerRelatedObjects()</c>, <c>updateMapPlayers()</c> and the
/// name and description of <c>generateMap()</c> (<c>map_format_helper.cpp:1287-1490</c>, <c>map_random_generator.cpp:973-978</c>).
/// </summary>
public sealed class RandomMapHeaderTests : IDisposable
{
    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(), $"fh2-random-header-{Guid.NewGuid():N}.mp2");

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    [Fact]
    public void OnlyThePlayersOfTheConfigurationAreInPlay()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 424242, PlayerCount = 2 });

        Assert.Equal(2, PlayerColors.Count(map.Info.KingdomColors));
        Assert.Equal(map.Info.KingdomColors, map.Info.HumanPlayers);
        Assert.Equal(map.Info.KingdomColors, map.Info.ComputerPlayers);
    }

    [Fact]
    public void EveryPlayerOfTheMapGetsItsRace()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 424242, PlayerCount = 3 });

        int races = map.Info.PlayerRaces.Count(race => race != MapRace.None);

        Assert.Equal(3, races);

        // 英雄用的是每种颜色的「随机种族」精灵（参考也这么放），所以这里允许 Random；其余必须是真实的六族之一。
        Assert.All(
            map.Info.PlayerRaces.Where(race => race != MapRace.None),
            race => Assert.True(
                race == MapRace.Random || MapRaces.Basic.Contains(race), $"{race} is not a playable race"));
    }

    [Fact]
    public void TheNameAndDescriptionCarryTheSeedAndTheConfiguration()
    {
        Mp2Map map = Generate(new RandomMapConfig { Seed = 424242, PlayerCount = 2, WaterPercentage = 20 });

        // MP2 的名字字段只有 16 字节，超出的部分在内存里就先截掉，免得盘上与内存不一致。
        Assert.StartsWith("Random map 4242", map.Info.Name, StringComparison.Ordinal);
        Assert.True(map.Info.Name.Length <= Mp2FileInfo.NameFieldLength - 1);

        Assert.Contains("424242", map.Info.Description, StringComparison.Ordinal);
        Assert.Contains("2 players", map.Info.Description, StringComparison.Ordinal);
        Assert.Contains("20% of water", map.Info.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeaderSurvivesAFileRoundTrip()
    {
        Mp2Map generated = Generate(new RandomMapConfig { Seed = 20261126, PlayerCount = 2 });

        Mp2MapWriter.WriteAll(_filePath, generated);
        Mp2Map loaded = Mp2Map.Read(_filePath);

        Assert.Equal(generated.Info.Name, loaded.Info.Name);
        Assert.Equal(generated.Info.Description, loaded.Info.Description);
        Assert.Equal(generated.Info.KingdomColors, loaded.Info.KingdomColors);

        // 头里六个种族槽总是写满的，不在局中的颜色读回来是「骑士」；只比在局中的那几个。
        foreach (PlayerColor color in PlayerColors.ToList(generated.Info.KingdomColors))
        {
            int index = PlayerColors.IndexOf(color);

            Assert.Equal(generated.Info.PlayerRaces[index], loaded.Info.PlayerRaces[index]);
        }
    }

    private static Mp2Map Generate(RandomMapConfig config) =>
        RandomMapGenerator.Generate(config, 72, 72)
        ?? throw new InvalidOperationException("The map was not generated.");
}
