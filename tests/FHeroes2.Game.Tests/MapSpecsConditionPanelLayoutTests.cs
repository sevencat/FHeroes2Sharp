using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Editor;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of <see cref="MapSpecsConditionPanelLayout"/>, the geometry of
/// <c>VictoryConditionUI::render()</c> / <c>LossConditionUI::render()</c> (<c>editor_map_specs_window.cpp:1021-1743</c>).
/// </summary>
public sealed class MapSpecsConditionPanelLayoutTests
{
    /// <summary>The victory panel of a 640x480 page, see <see cref="MapSpecsPageLayoutTests"/>.</summary>
    private static readonly Rect VictoryPanel = new(99, 292, 114, 150);

    /// <summary>The loss panel of a 640x480 page.</summary>
    private static readonly Rect LossPanel = new(419, 292, 114, 150);

    private static readonly MapSpecsConditionPanelSizes Sizes = new(
        SelectRowHeight: 64,
        ArtifactFrameSize: new Size(32, 32),
        ValueAreaSize: new Size(94, 25),
        CheckboxHeight: 15,
        TextHeight: 13);

    [Fact]
    public void DefeatingEveryoneNeedsNoControls()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
            VictoryPanel, MapVictoryCondition.DefeatEveryone, Sizes, isNeutralTownTarget: true);

        AssertNothingIsLaidOut(layout);
    }

    [Fact]
    public void CapturingANeutralTownOffersBothCheckboxes()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
            VictoryPanel, MapVictoryCondition.CaptureTown, Sizes, isNeutralTownTarget: true);

        // 城镇行占满整宽，高由城镇图标决定。
        Assert.Equal(new Rect(99, 292, 114, 64), layout.SelectRowRoi);

        // 两行从左边界内缩 5、宽减 10；AI 在上（+10），标准胜利在下（+35）。
        Assert.Equal(new Rect(104, 366, 104, 15), layout.AllowForAiRoi);
        Assert.Equal(new Rect(104, 391, 104, 15), layout.AllowNormalVictoryRoi);
        Assert.True(layout.ValueAreaRoi.IsEmpty);
        Assert.Empty(layout.AllianceRowRois);
    }

    [Fact]
    public void CapturingAPlayersTownOffersNoAiCheckbox()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
            VictoryPanel, MapVictoryCondition.CaptureTown, Sizes, isNeutralTownTarget: false);

        // 参考只对中立城镇给「also for AI」（:1066）。
        Assert.True(layout.AllowForAiRoi.IsEmpty);
        Assert.Equal(new Rect(104, 391, 104, 15), layout.AllowNormalVictoryRoi);
    }

    [Fact]
    public void KillingAHeroShowsTheRowAlone()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
            VictoryPanel, MapVictoryCondition.KillHero, Sizes, isNeutralTownTarget: false);

        Assert.Equal(new Rect(99, 292, 114, 64), layout.SelectRowRoi);
        Assert.True(layout.AllowNormalVictoryRoi.IsEmpty);
        Assert.True(layout.AllowForAiRoi.IsEmpty);
        Assert.True(layout.ValueAreaRoi.IsEmpty);
    }

    [Fact]
    public void ObtainingAnArtifactCentresTheFrameAndOffersStandardVictory()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
            VictoryPanel, MapVictoryCondition.ObtainArtifact, Sizes, isNeutralTownTarget: false);

        // 居中的 RESOURCE 7 框，往下 4 像素（:1107）。
        Assert.Equal(new Rect(140, 296, 32, 32), layout.SelectRowRoi);

        // 唯一的勾选项在框下 10 像素，不含那 4（:1116）。
        Assert.Equal(new Rect(104, 334, 104, 15), layout.AllowNormalVictoryRoi);
        Assert.True(layout.AllowForAiRoi.IsEmpty);
    }

    [Fact]
    public void DefeatingTheOtherSideStacksTheAllianceRows()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
            VictoryPanel, MapVictoryCondition.DefeatOtherSide, Sizes, isNeutralTownTarget: false);

        // 标题在 +2、高一行；两行联盟从标题下沿起，步长 32（:1136-1161）。
        Assert.Equal(new Rect(99, 294, 114, 13), layout.AllianceTitleRoi);
        Assert.Equal(2, layout.AllianceRowRois.Count);
        Assert.Equal(new Rect(99, 307, 114, 32), layout.AllianceRowRois[0]);
        Assert.Equal(new Rect(99, 339, 114, 32), layout.AllianceRowRois[1]);

        Assert.True(layout.SelectRowRoi.IsEmpty);
        Assert.True(layout.AllowNormalVictoryRoi.IsEmpty);
    }

    [Fact]
    public void CollectingGoldCentresTheNumberElementAndOffersBothCheckboxes()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
            VictoryPanel, MapVictoryCondition.CollectGold, Sizes, isNeutralTownTarget: false);

        // 94 宽的数值区在 114 宽的面板里居中，靠左 10。
        Assert.Equal(new Rect(109, 292, 94, 25), layout.ValueAreaRoi);
        Assert.Equal(new Rect(104, 327, 104, 15), layout.AllowForAiRoi);
        Assert.Equal(new Rect(104, 352, 104, 15), layout.AllowNormalVictoryRoi);
        Assert.True(layout.SelectRowRoi.IsEmpty);
    }

    [Theory]
    [InlineData(MapLossCondition.LoseTown)]
    [InlineData(MapLossCondition.LoseHero)]
    public void LosingATownOrAHeroOnlyNeedsTheRow(MapLossCondition condition)
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateLoss(LossPanel, condition, Sizes);

        Assert.Equal(new Rect(419, 292, 114, 64), layout.SelectRowRoi);
        Assert.True(layout.ValueAreaRoi.IsEmpty);
        Assert.True(layout.DaysDescriptionRoi.IsEmpty);
    }

    [Fact]
    public void RunningOutOfTimeShowsTheNumberAndItsDate()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateLoss(
            LossPanel, MapLossCondition.OutOfTime, Sizes);

        Assert.Equal(new Rect(429, 292, 94, 25), layout.ValueAreaRoi);

        // getDateDescription（:1730-1733）：数值区下 14 像素，整宽一行。
        Assert.Equal(new Rect(419, 331, 114, 13), layout.DaysDescriptionRoi);
        Assert.True(layout.SelectRowRoi.IsEmpty);
    }

    [Fact]
    public void LosingEverythingNeedsNoControls()
    {
        MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateLoss(
            LossPanel, MapLossCondition.Everything, Sizes);

        AssertNothingIsLaidOut(layout);
    }

    [Fact]
    public void EveryConditionStaysInsideItsPanel()
    {
        foreach (MapVictoryCondition condition in Enum.GetValues<MapVictoryCondition>())
        {
            MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateVictory(
                VictoryPanel, condition, Sizes, isNeutralTownTarget: true);

            AssertInside(layout, VictoryPanel, condition.ToString());
        }

        foreach (MapLossCondition condition in Enum.GetValues<MapLossCondition>())
        {
            MapSpecsConditionPanelLayout layout = MapSpecsConditionPanelLayout.CreateLoss(LossPanel, condition, Sizes);

            AssertInside(layout, LossPanel, condition.ToString());
        }
    }

    private static void AssertNothingIsLaidOut(MapSpecsConditionPanelLayout layout)
    {
        Assert.True(layout.SelectRowRoi.IsEmpty);
        Assert.True(layout.ValueAreaRoi.IsEmpty);
        Assert.True(layout.AllowNormalVictoryRoi.IsEmpty);
        Assert.True(layout.AllowForAiRoi.IsEmpty);
        Assert.True(layout.AllianceTitleRoi.IsEmpty);
        Assert.True(layout.DaysDescriptionRoi.IsEmpty);
        Assert.Empty(layout.AllianceRowRois);
    }

    private static void AssertInside(MapSpecsConditionPanelLayout layout, in Rect panel, string condition)
    {
        IEnumerable<Rect> rois = layout.AllianceRowRois
            .Prepend(layout.SelectRowRoi)
            .Prepend(layout.ValueAreaRoi)
            .Prepend(layout.AllowNormalVictoryRoi)
            .Prepend(layout.AllowForAiRoi)
            .Prepend(layout.AllianceTitleRoi)
            .Prepend(layout.DaysDescriptionRoi);

        foreach (Rect roi in rois)
        {
            if (roi.IsEmpty)
            {
                continue;
            }

            Assert.True(roi.X >= panel.X, $"{condition}: left edge {roi.X} is left of the panel.");
            Assert.True(roi.Y >= panel.Y, $"{condition}: top edge {roi.Y} is above the panel.");
            Assert.True(roi.Right <= panel.Right, $"{condition}: right edge {roi.Right} leaves the panel.");
            Assert.True(roi.Bottom <= panel.Bottom, $"{condition}: bottom edge {roi.Bottom} leaves the panel.");
        }
    }
}
