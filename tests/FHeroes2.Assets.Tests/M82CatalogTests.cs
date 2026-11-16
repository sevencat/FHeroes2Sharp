using FHeroes2.Assets.Audio;
using Xunit;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Pins <c>m82map</c> (<c>src/fheroes2/agg/m82.cpp:26</c>) and <c>M82::GetString</c>
/// (<c>m82.cpp:100</c>): the names are spelled out one by one, and every out-of-range id is <c>UNKNOWN</c>.
/// </summary>
public class M82CatalogTests
{
    [Theory]
    [InlineData(M82Sound.Aelmattk, "AELMATTK.82M")]
    [InlineData(M82Sound.Aelmkill, "AELMKILL.82M")]
    [InlineData(M82Sound.Zombwnce, "ZOMBWNCE.82M")]
    public void ASoundMapsToItsArchiveEntry(M82Sound sound, string expected)
    {
        Assert.Equal(expected, M82Catalog.GetFileName(sound));
    }

    [Fact]
    public void ThePatchedMeteorEntryKeepsItsPatchSuffix()
    {
        // 参考的表里唯一一条带补丁后缀的名字。
        Assert.Equal("METEOR~1.82M", M82Catalog.GetFileName(M82Sound.Meteor));
    }

    [Fact]
    public void AnUnknownSoundMapsToTheUnknownEntry()
    {
        // 参考：m82.cpp:100 的三元表达式，越界与 UNKNOWN 走同一条路。
        Assert.Equal("UNKNOWN", M82Catalog.GetFileName(M82Sound.Unknown));
        Assert.Equal("UNKNOWN", M82Catalog.GetFileName((M82Sound)(-1)));
        Assert.Equal("UNKNOWN", M82Catalog.GetFileName((M82Sound)100000));
    }

    [Fact]
    public void EverySoundOfTheTableHasAnEntryName()
    {
        Assert.DoesNotContain(M82Catalog.AllSounds, sound => M82Catalog.GetFileName(sound) == "UNKNOWN");

        // AllSounds 是端口为了让测试遍历全表而加的，它必须与枚举里的有效区间等长。
        Assert.Equal((int)M82Sound.Unknown - (int)M82Sound.Aelmattk, M82Catalog.AllSounds.Count);
    }
}
