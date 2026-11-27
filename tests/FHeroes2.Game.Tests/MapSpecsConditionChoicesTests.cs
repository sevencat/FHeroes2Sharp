using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The victory / loss conditions <c>showWinLoseList()</c> offers for a given map
/// (<c>editor_map_specs_window.cpp:1881-1921</c>).
/// </summary>
public sealed class MapSpecsConditionChoicesTests
{
    [Fact]
    public void AFlatMapOffersOnlyTheConditionsItHasThePiecesFor()
    {
        MapSpecsState state = CreateState();

        // 没有城堡、没有「电脑独有的英雄」，但六种颜色都在：只剩「打败所有敌人」「拿到神器」「击败另一方」「攒够金币」。
        Assert.Equal(
        [
            MapVictoryCondition.DefeatEveryone,
            MapVictoryCondition.ObtainArtifact,
            MapVictoryCondition.DefeatOtherSide,
            MapVictoryCondition.CollectGold,
        ], MapSpecsConditionChoices.Victory(state).ToList());

        Assert.Equal(
        [
            MapLossCondition.Everything,
            MapLossCondition.OutOfTime,
        ], MapSpecsConditionChoices.Loss(state).ToList());
    }

    [Fact]
    public void ComputerHeroesAndTownsUnlockTheOtherVictoryConditions()
    {
        // 蓝色只给人、红色只给电脑，红色英雄才算「电脑独有的英雄」。城堡按端口的口径一律算数。
        MapSpecsState state = CreateState(
            humans: PlayerColor.Blue,
            computers: PlayerColors.All,
            heroes: [Hero(PlayerColor.Red)],
            castles: [new Mp2CastlePosition(1, 1, 1, MapRace.Knight)]);

        Assert.Equal(MapSpecsConditionChoices.SupportedVictoryConditions.ToList(),
            MapSpecsConditionChoices.Victory(state).ToList());
    }

    [Fact]
    public void HumanHeroesAndTownsUnlockTheOtherLossConditions()
    {
        MapSpecsState state = CreateState(
            humans: PlayerColor.Blue,
            computers: PlayerColor.Red,
            heroes: [Hero(PlayerColor.Blue)],
            castles: [new Mp2CastlePosition(1, 1, 1, MapRace.Knight)]);

        Assert.Equal(MapSpecsConditionChoices.SupportedLossConditions.ToList(),
            MapSpecsConditionChoices.Loss(state).ToList());
    }

    [Fact]
    public void TheTwoSidesUseTheirOwnHeroes()
    {
        // 红色只有电脑在玩：可以「击败英雄」，但不会解锁「失去英雄」。
        MapSpecsState state = CreateState(
            humans: PlayerColor.Blue,
            computers: PlayerColors.All,
            heroes: [Hero(PlayerColor.Red)]);

        Assert.Contains(MapVictoryCondition.KillHero, MapSpecsConditionChoices.Victory(state));
        Assert.DoesNotContain(MapLossCondition.LoseHero, MapSpecsConditionChoices.Loss(state));
    }

    [Fact]
    public void TheAllianceNeedsThreeColours()
    {
        // 参考要 Color::Count( availablePlayerColors ) >= 3 才给「击败另一方」。
        MapSpecsState twoColours = CreateState(kingdom: PlayerColor.Blue | PlayerColor.Red);

        Assert.DoesNotContain(MapVictoryCondition.DefeatOtherSide, MapSpecsConditionChoices.Victory(twoColours));

        MapSpecsState threeColours = CreateState(kingdom: PlayerColor.Blue | PlayerColor.Red | PlayerColor.Green);

        Assert.Contains(MapVictoryCondition.DefeatOtherSide, MapSpecsConditionChoices.Victory(threeColours));
    }

    [Fact]
    public void TheOrderIsTheReferenceOrder()
    {
        Assert.Equal(
        [
            MapVictoryCondition.DefeatEveryone,
            MapVictoryCondition.CaptureTown,
            MapVictoryCondition.KillHero,
            MapVictoryCondition.ObtainArtifact,
            MapVictoryCondition.DefeatOtherSide,
            MapVictoryCondition.CollectGold,
        ], MapSpecsConditionChoices.SupportedVictoryConditions.ToList());

        Assert.Equal(
        [
            MapLossCondition.Everything,
            MapLossCondition.LoseTown,
            MapLossCondition.LoseHero,
            MapLossCondition.OutOfTime,
        ], MapSpecsConditionChoices.SupportedLossConditions.ToList());
    }

    private static MapSpecsState CreateState(PlayerColor? humans = null, PlayerColor? computers = null,
        PlayerColor? kingdom = null, IReadOnlyList<Mp2MapHero>? heroes = null,
        IReadOnlyList<Mp2CastlePosition>? castles = null)
    {
        Mp2Map map = SyntheticMap.Create(8, 8);
        map.Info = map.Info with
        {
            KingdomColors = kingdom ?? PlayerColors.All,
            HumanPlayers = humans ?? PlayerColors.All,
            ComputerPlayers = computers ?? PlayerColors.All,
        };

        return new MapSpecsState(map with
        {
            Heroes = heroes ?? [],
            Castles = castles ?? [],
        });
    }

    private static Mp2MapHero Hero(PlayerColor color)
    {
        int order = ((IList<PlayerColor>)PlayerColors.Ordered).IndexOf(color);

        return Mp2MapHeroes.FromSpriteIndex(order * Mp2MapHeroes.SpritesPerColor, 0, 0);
    }
}
