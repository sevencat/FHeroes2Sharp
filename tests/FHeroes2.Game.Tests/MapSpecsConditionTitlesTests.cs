using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of <see cref="MapSpecsConditionTitles"/>, the captions of the selected rows of the condition panel
/// (<c>editor_map_specs_window.cpp:193-241</c>).
/// </summary>
public sealed class MapSpecsConditionTitlesTests
{
    [Fact]
    public void ANamedHeroShowsNameRaceAndPosition()
    {
        // 13 宽的地图，第 84 块 = (6, 6)。
        Assert.Equal("[6, 6]: Rangar, Barbarian hero",
            MapSpecsConditionTitles.GetHeroTitle("Rangar", MapRace.Barbarian, tileIndex: 84, mapWidth: 13));
    }

    [Fact]
    public void AnUnnamedHeroShowsOnlyRaceAndPosition()
    {
        // 参考对空名字换成另一条句式，不加逗号（:208-214）。
        Assert.Equal("[1, 0]: Knight hero",
            MapSpecsConditionTitles.GetHeroTitle(string.Empty, MapRace.Knight, tileIndex: 1, mapWidth: 13));
    }

    [Theory]
    [InlineData(true, "[2, 1]: Midgard, Sorceress town")]
    [InlineData(false, "[2, 1]: Midgard, Sorceress castle")]
    public void ATownRowUsesTheNounOfItsKind(bool isTown, string expected)
    {
        Assert.Equal(expected,
            MapSpecsConditionTitles.GetTownTitle("Midgard", MapRace.Sorceress, isTown, tileIndex: 15, mapWidth: 13));
    }

    [Fact]
    public void AnUnnamedCastleShowsOnlyRaceAndPosition()
    {
        Assert.Equal("[0, 3]: Necromancer castle",
            MapSpecsConditionTitles.GetTownTitle(string.Empty, MapRace.Necromancer, isTown: false, tileIndex: 39, mapWidth: 13));
    }

    [Fact]
    public void ThePositionIsColumnThenRow()
    {
        // replacePosition（:195）：x = index % width、y = index / width。
        Assert.Equal("5, 4", MapSpecsConditionTitles.FormatPosition(tileIndex: 57, mapWidth: 13));
        Assert.Equal("0, 0", MapSpecsConditionTitles.FormatPosition(tileIndex: 0, mapWidth: 72));
    }

    [Fact]
    public void AnEmptyTitleNeedsANameString()
    {
        Assert.Throws<ArgumentNullException>(() => MapSpecsConditionTitles.GetHeroTitle(null!, MapRace.Knight, 0, 13));
        Assert.Throws<ArgumentNullException>(() => MapSpecsConditionTitles.GetTownTitle(null!, MapRace.Knight, true, 0, 13));
    }

    [Fact]
    public void APositionNeedsAMapWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapSpecsConditionTitles.FormatPosition(0, 0));
    }
}
