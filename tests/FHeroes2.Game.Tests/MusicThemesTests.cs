using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the three queries of <c>MUS</c> (<c>src/fheroes2/agg/mus.cpp:141-211</c>): the ground themes, the
/// castle themes and the random battle theme.
/// </summary>
public class MusicThemesTests
{
    [Theory]
    [InlineData(MapGround.Grass, MusicTrack.Grass)]
    [InlineData(MapGround.Snow, MusicTrack.Snow)]
    [InlineData(MapGround.Swamp, MusicTrack.Swamp)]
    [InlineData(MapGround.Lava, MusicTrack.Lava)]
    [InlineData(MapGround.Desert, MusicTrack.Desert)]
    [InlineData(MapGround.Dirt, MusicTrack.Dirt)]
    [InlineData(MapGround.Wasteland, MusicTrack.Wasteland)]
    [InlineData(MapGround.Water, MusicTrack.Ocean)]
    [InlineData(MapGround.Beach, MusicTrack.Ocean)]
    [InlineData(MapGround.Unknown, MusicTrack.Unknown)]
    public void EveryGroundHasItsTheme(MapGround ground, MusicTrack expected)
    {
        Assert.Equal(expected, MusicThemes.FromGround(ground));
    }

    [Fact]
    public void TheGroundThemesCoverTheWholeEnumeration()
    {
        // 参考的 default 分支走不到（地形是封闭枚举）；端口只有 Unknown 会落到那里。
        foreach (MapGround ground in Enum.GetValues<MapGround>())
        {
            MusicTrack theme = MusicThemes.FromGround(ground);

            Assert.Equal(ground == MapGround.Unknown, theme == MusicTrack.Unknown);
        }
    }

    [Theory]
    [InlineData(MapRace.Knight, MusicTrack.KnightCastle)]
    [InlineData(MapRace.Barbarian, MusicTrack.BarbarianCastle)]
    [InlineData(MapRace.Sorceress, MusicTrack.SorceressCastle)]
    [InlineData(MapRace.Warlock, MusicTrack.WarlockCastle)]
    [InlineData(MapRace.Wizard, MusicTrack.WizardCastle)]
    [InlineData(MapRace.Necromancer, MusicTrack.NecromancerCastle)]
    [InlineData(MapRace.None, MusicTrack.Unknown)]
    [InlineData(MapRace.Multiple, MusicTrack.Unknown)]
    [InlineData(MapRace.Random, MusicTrack.Unknown)]
    public void EveryRaceHasItsCastleTheme(MapRace race, MusicTrack expected)
    {
        Assert.Equal(expected, MusicThemes.FromRace(race));
    }

    [Fact]
    public void TheBattleThemeIsOneOfTheThree()
    {
        // 参考：mus.cpp:195 的 Rand::Get( 1, 3 )。
        for (int draw = 0; draw < 200; ++draw)
        {
            MusicTrack theme = MusicThemes.GetBattleRandom();

            Assert.Contains(theme, new[] { MusicTrack.Battle1, MusicTrack.Battle2, MusicTrack.Battle3 });
        }
    }
}
