using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the player and difficulty icon frames of the map specifications window (ADR-303):
/// <c>editor_map_specs_window.cpp:1957-2025</c>.
/// </summary>
public sealed class MapSpecsPageIconsTests
{
    [Fact]
    public void ThePlayerIconShowsWhoControlsTheColour()
    {
        // 参考 :1966-1979：人类可玩取 9，只给电脑取 3，两边都能取 82。
        Assert.Equal(MapSpecsPageIcons.ComputerPlayerIcon, MapSpecsPageIcons.GetPlayerIconIndex(available: true, human: false, computer: true));
        Assert.Equal(MapSpecsPageIcons.HumanPlayerIcon, MapSpecsPageIcons.GetPlayerIconIndex(available: true, human: true, computer: false));
        Assert.Equal(MapSpecsPageIcons.AnyPlayerIcon, MapSpecsPageIcons.GetPlayerIconIndex(available: true, human: true, computer: true));
    }

    [Fact]
    public void AColourTheMapDoesNotUseIsGreyedOut()
    {
        Assert.Equal(MapSpecsPageIcons.UnavailablePlayerIcon, MapSpecsPageIcons.GetPlayerIconIndex(available: false, human: false, computer: false));

        // 不可用优先：即便这两方都说能用，参考也是先看 availablePlayerColors。
        Assert.Equal(MapSpecsPageIcons.UnavailablePlayerIcon, MapSpecsPageIcons.GetPlayerIconIndex(available: false, human: true, computer: true));
    }

    [Fact]
    public void AnAvailableColourWithNoControllerFallsBackToTheGreyIcon()
    {
        // 参考在这里 assert；端口不崩，退回「不可用」的图标。
        Assert.Equal(MapSpecsPageIcons.UnavailablePlayerIcon, MapSpecsPageIcons.GetPlayerIconIndex(available: true, human: false, computer: false));
    }

    [Fact]
    public void TheFourIconsAndTheFourDifficultiesMatch()
    {
        Assert.Equal(0, MapSpecsPageIcons.GetDifficultyIndex(GameDifficulty.Easy));
        Assert.Equal(1, MapSpecsPageIcons.GetDifficultyIndex(GameDifficulty.Normal));
        Assert.Equal(2, MapSpecsPageIcons.GetDifficultyIndex(GameDifficulty.Hard));
        Assert.Equal(3, MapSpecsPageIcons.GetDifficultyIndex(GameDifficulty.Expert));

        Assert.Equal(GameDifficulty.Easy, MapSpecsPageIcons.GetDifficultyFromIndex(0));
        Assert.Equal(GameDifficulty.Normal, MapSpecsPageIcons.GetDifficultyFromIndex(1));
        Assert.Equal(GameDifficulty.Hard, MapSpecsPageIcons.GetDifficultyFromIndex(2));
        Assert.Equal(GameDifficulty.Expert, MapSpecsPageIcons.GetDifficultyFromIndex(3));
    }

    [Fact]
    public void ImpossibleAndOutOfRangeFallBackToEasy()
    {
        // 编辑器不提供 Impossible（参考 assert 后回到 Easy）。
        Assert.Equal(0, MapSpecsPageIcons.GetDifficultyIndex(GameDifficulty.Impossible));
        Assert.Equal(GameDifficulty.Easy, MapSpecsPageIcons.GetDifficultyFromIndex(-1));
        Assert.Equal(GameDifficulty.Easy, MapSpecsPageIcons.GetDifficultyFromIndex(4));
        Assert.Equal(IcnType.DifficultyIconEasy, MapSpecsPageIcons.GetDifficultyIconType(GameDifficulty.Impossible));
    }

    [Fact]
    public void EachDifficultyHasItsOwnArchive()
    {
        Assert.Equal(IcnType.DifficultyIconEasy, MapSpecsPageIcons.GetDifficultyIconType(GameDifficulty.Easy));
        Assert.Equal(IcnType.DifficultyIconNormal, MapSpecsPageIcons.GetDifficultyIconType(GameDifficulty.Normal));
        Assert.Equal(IcnType.DifficultyIconHard, MapSpecsPageIcons.GetDifficultyIconType(GameDifficulty.Hard));
        Assert.Equal(IcnType.DifficultyIconExpert, MapSpecsPageIcons.GetDifficultyIconType(GameDifficulty.Expert));
    }
}
