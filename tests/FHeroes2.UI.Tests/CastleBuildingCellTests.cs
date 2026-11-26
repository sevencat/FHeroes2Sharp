using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// One building slot of the editor's castle details window (<c>editor_castle_details_window.cpp:79-307</c>): the
/// upgrade chain behind it, the two levels the author edits, and the clicks that move them.
/// </summary>
/// <remarks>
/// 用例只碰等级与限制这两件事，也就是参考里 <c>BuildingData</c> 的全部语义；把这一格画出来是下一刀的事。链的长度与
/// 每一级是什么都从 <see cref="CastleBuildings.GetUpgradeForBuilding"/> 现算，用例里不写死，免得建筑表一动就全红。
/// </remarks>
public sealed class CastleBuildingCellTests
{
    private static readonly MapRace Knight = MapRace.Knight;

    /// <summary>The mage guild: five levels, the same chain in every town, so the cases do not depend on a race's dwellings.</summary>
    private static readonly BuildingType Chain = BuildingType.MageGuild1;

    [Fact]
    public void AFreshChainStartsWithNothingBuilt()
    {
        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, BuildingType.Nothing);

        Assert.Equal(Levels(Chain), cell.VariantCount);
        Assert.True(cell.VariantCount >= 2, "the mage guild should have at least one upgrade");
        Assert.Equal(-1, cell.BuiltLevel);
        Assert.Equal(-1, cell.RestrictedLevel);
        Assert.Empty(cell.BuiltChain);
        Assert.Equal(Chain, cell.RenderBuilding);
        Assert.Equal(BuildingType.Nothing, cell.RestrictedBuilding);
        Assert.Equal(CastleBuildingInfo.GetName(Knight, Chain), cell.RenderBuildingName);
    }

    [Fact]
    public void TheBuiltMaskPicksTheHighestLevelItHolds()
    {
        IReadOnlyList<BuildingType> chain = ChainOf(Chain);
        BuildingType mask = BuildingType.Nothing;
        foreach (BuildingType building in chain)
        {
            mask |= building;
        }

        CastleBuildingCell cell = new(Chain, Knight, mask, BuildingType.Nothing);

        Assert.Equal(chain.Count - 1, cell.BuiltLevel);
        Assert.Equal(chain, cell.BuiltChain);
        Assert.Equal(chain[^1], cell.RenderBuilding);
        Assert.Equal(CastleBuildingInfo.GetName(Knight, chain[^1]), cell.RenderBuildingName);
    }

    [Fact]
    public void AMaskWithOnlyTheFirstBuildingBuildsOnlyTheFirstLevel()
    {
        CastleBuildingCell cell = new(Chain, Knight, Chain, BuildingType.Nothing);

        Assert.Equal(0, cell.BuiltLevel);
        Assert.Equal([Chain], cell.BuiltChain);
        Assert.Equal(Chain, cell.RenderBuilding);
    }

    [Fact]
    public void TheRestrictedMaskAlsoPicksTheHighestLevelItHolds()
    {
        IReadOnlyList<BuildingType> chain = ChainOf(Chain);
        BuildingType mask = BuildingType.Nothing;
        foreach (BuildingType building in chain)
        {
            mask |= building;
        }

        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, mask);

        Assert.Equal(-1, cell.BuiltLevel);
        Assert.Equal(chain.Count - 1, cell.RestrictedLevel);
        Assert.Equal(chain[^1], cell.RestrictedBuilding);
    }

    [Fact]
    public void BuiltAndRestrictedMasksAreReadFromTheirOwnMasks()
    {
        IReadOnlyList<BuildingType> chain = ChainOf(Chain);

        // 已建第一级、禁掉其余全部：两个等级各归各的，互不影响。
        BuildingType restricted = BuildingType.Nothing;
        for (int i = 1; i < chain.Count; ++i)
        {
            restricted |= chain[i];
        }

        CastleBuildingCell cell = new(Chain, Knight, Chain, restricted);

        // 参考在链上从下往上扫，每遇到一个就覆盖一次，所以一本掩码里禁了好几级时记的是最高那一级（:96-101）。
        Assert.Equal(0, cell.BuiltLevel);
        Assert.Equal(chain.Count - 1, cell.RestrictedLevel);
        Assert.Equal(chain[^1], cell.RestrictedBuilding);
    }

    [Fact]
    public void BuildingWalksDownTheChainAndWrapsToNothing()
    {
        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, BuildingType.Nothing);

        for (int level = 0; level < cell.VariantCount; ++level)
        {
            cell.Cycle(restrictionMode: false);
            Assert.Equal(level, cell.BuiltLevel);
            Assert.Equal(cell.GetLevelBuilding(level), cell.RenderBuilding);
        }

        // 最后一级之后再按一次回到「什么都没建」，这是参考的 ++/回绕写法（:164-169）。
        cell.Cycle(restrictionMode: false);

        Assert.Equal(-1, cell.BuiltLevel);
        Assert.Empty(cell.BuiltChain);
        Assert.Equal(Chain, cell.RenderBuilding);
    }

    [Fact]
    public void BanningWalksUpwardsFromTheLastLevel()
    {
        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, BuildingType.Nothing);

        cell.Cycle(restrictionMode: true);
        Assert.Equal(cell.VariantCount - 1, cell.RestrictedLevel);

        cell.Cycle(restrictionMode: true);
        Assert.Equal(cell.VariantCount - 2, cell.RestrictedLevel);

        // 退到第一级之后再按一次回到「什么都没禁」。
        for (int i = 2; i < cell.VariantCount; ++i)
        {
            cell.Cycle(restrictionMode: true);
        }

        Assert.Equal(0, cell.RestrictedLevel);

        cell.Cycle(restrictionMode: true);
        Assert.Equal(-1, cell.RestrictedLevel);
        Assert.Equal(BuildingType.Nothing, cell.RestrictedBuilding);
    }

    [Fact]
    public void BuildingCannotCrossTheBanAndPushesItUpInstead()
    {
        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, BuildingType.Nothing);

        // 从第一级起就禁：再按建的键时，禁线被顶到第二级，已建停在被禁的下一级之前。
        while (cell.RestrictedLevel != 0)
        {
            cell.Cycle(restrictionMode: true);
        }

        Assert.Equal(0, cell.RestrictedLevel);

        cell.Cycle(restrictionMode: false);

        Assert.Equal(0, cell.BuiltLevel);
        Assert.Equal(1, cell.RestrictedLevel);

        cell.Cycle(restrictionMode: false);

        Assert.Equal(1, cell.BuiltLevel);
        Assert.Equal(2, cell.RestrictedLevel);
    }

    [Fact]
    public void BanningPushesTheBuiltLevelDown()
    {
        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, BuildingType.Nothing);

        // 先建好第一级，再把禁线一级一级收到第一级：已建应被压回「什么都没建」。
        cell.Cycle(restrictionMode: false);
        Assert.Equal(0, cell.BuiltLevel);

        while (cell.RestrictedLevel != 0)
        {
            cell.Cycle(restrictionMode: true);
        }

        Assert.Equal(-1, cell.BuiltLevel);
        Assert.Empty(cell.BuiltChain);
    }

    [Fact]
    public void ResetClearsBothLevels()
    {
        CastleBuildingCell cell = new(Chain, Knight, Chain, Chain);
        Assert.Equal(0, cell.BuiltLevel);

        cell.Reset();

        Assert.Equal(-1, cell.BuiltLevel);
        Assert.Equal(-1, cell.RestrictedLevel);
    }

    [Fact]
    public void TheSlotSplitsIntoABuildHalfAndABanHalf()
    {
        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, BuildingType.Nothing);

        cell.SetPosition(10, 20);

        Assert.Equal(new Rect(10, 20, CastleBuildingCell.Width, CastleBuildingCell.Height), cell.Area);
        Assert.Equal(new Rect(10, 20, CastleBuildingCell.BuildWidth, CastleBuildingCell.Height), cell.BuildArea);
        Assert.Equal(
            new Rect(10 + CastleBuildingCell.BuildWidth, 20, CastleBuildingCell.BanWidth, CastleBuildingCell.Height),
            cell.BanArea);
        Assert.Equal(CastleBuildingCell.Width, CastleBuildingCell.BuildWidth + CastleBuildingCell.BanWidth);
    }

    [Fact]
    public void DrawingRejectsMissingParts()
    {
        CastleBuildingCell cell = new(Chain, Knight, BuildingType.Nothing, BuildingType.Nothing);
        PixelBuffer display = new(200, 100);

        Assert.Throws<ArgumentNullException>(() => cell.Draw(null!, null!, null!, true));
        Assert.Throws<ArgumentNullException>(() => cell.Draw(display, null!, null!, true));
    }

    /// <summary>How many levels the chain of a building has, computed the way the cell computes it.</summary>
    private static int Levels(BuildingType building)
    {
        int levels = 1;
        for (BuildingType upgraded = CastleBuildings.GetUpgradeForBuilding(Knight, building);
             upgraded != building;
             upgraded = CastleBuildings.GetUpgradeForBuilding(Knight, upgraded))
        {
            ++levels;
            building = upgraded;
        }

        return levels;
    }

    /// <summary>The buildings of the chain, from the first one up.</summary>
    private static List<BuildingType> ChainOf(BuildingType building)
    {
        List<BuildingType> chain = [building];

        for (BuildingType upgraded = CastleBuildings.GetUpgradeForBuilding(Knight, building);
             upgraded != building;
             upgraded = CastleBuildings.GetUpgradeForBuilding(Knight, upgraded))
        {
            chain.Add(upgraded);
            building = upgraded;
        }

        return chain;
    }
}
