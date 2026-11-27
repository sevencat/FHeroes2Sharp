using FHeroes2.Core.Geometry;
using FHeroes2.Game.Editor;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the page geometry of the map specifications window (ADR-303):
/// <c>editor_map_specs_window.cpp:2043-2210</c>.
/// </summary>
public sealed class MapSpecsPageLayoutTests
{
    private static readonly Rect Screen = new(0, 0, 640, 480);

    [Fact]
    public void TheNameFrameIsCentredTenPixelsUnderTheTopEdge()
    {
        MapSpecsPageLayout layout = Create();

        Assert.Equal(new Rect(256, 10, 128, 32), layout.ScenarioBoxRoi);

        // 名称文本区是框内缩进 6 / 5 / 6 / 6。
        Assert.Equal(new Rect(262, 15, 116, 21), layout.MapNameRoi);
    }

    [Fact]
    public void ThePlayerIconsAreCentredInOneRowOfEightyPixelSteps()
    {
        MapSpecsPageLayout layout = Create();

        // 两个图标：2 * 80 = 160 宽，( 640 - 160 ) / 2 = 240，纵向在名称框下 10 像素。
        Assert.Equal([new Rect(240, 52, 34, 30), new Rect(320, 52, 34, 30)], layout.PlayerRois);
        Assert.Equal([new Rect(235, 55, 34, 30), new Rect(315, 55, 34, 30)], layout.PlayerShadowRois);
    }

    [Fact]
    public void SixPlayerIconsStillFitTheRow()
    {
        MapSpecsPageLayout layout = MapSpecsPageLayout.Create(
            Screen,
            new Size(128, 32),
            [new Size(34, 30), new Size(34, 30), new Size(34, 30), new Size(34, 30), new Size(34, 30), new Size(34, 30)],
            difficultyIconSideLength: 30,
            dropListButton: new Size(16, 19),
            dropListBackground: new Size(100, 19));

        Assert.Equal(6, layout.PlayerRois.Count);
        Assert.Equal(80, layout.PlayerRois[0].X);
        Assert.Equal(80 * 5 + 80, layout.PlayerRois[5].X);
    }

    [Fact]
    public void NoPlayerIconsMeansNoRects()
    {
        MapSpecsPageLayout layout = MapSpecsPageLayout.Create(
            Screen,
            new Size(128, 32),
            [],
            difficultyIconSideLength: 30,
            dropListButton: new Size(16, 19),
            dropListBackground: new Size(100, 19));

        Assert.Empty(layout.PlayerRois);
        Assert.Empty(layout.PlayerShadowRois);
    }

    [Fact]
    public void TheDifficultyIconsSitFifteenPixelsFromTheLeftEdgeAndAreShiftedByThree()
    {
        MapSpecsPageLayout layout = Create();

        // 标题中轴 = 15 + 77 * 4 / 2 = 169，纵坐标 = 名称框底 + 65。
        Assert.Equal(169, layout.DifficultyTitleCenterX);
        Assert.Equal(107, layout.DifficultyTitleY);

        // 四个格子再下移 23（标题 + 23），整块往左上挪 3 给光标留位。
        Assert.Equal(
            [
                new Rect(12, 127, 30, 30),
                new Rect(89, 127, 30, 30),
                new Rect(166, 127, 30, 30),
                new Rect(243, 127, 30, 30),
            ],
            layout.DifficultyRois);

        // 贴图时还在原处：+3 回到 (15, 130) 起。
        Assert.Equal(new Rect(15, 130, 30, 30), layout.DifficultyIconRois[0]);
    }

    [Fact]
    public void TheDescriptionBoxSitsOnTheRightEdgeWithTheSameTitleHeight()
    {
        MapSpecsPageLayout layout = Create();

        Assert.Equal(333 + 146, layout.DescriptionTitleCenterX);
        Assert.Equal(107, layout.DescriptionTitleY);

        // 标题下 25 像素是文本区，292x90。
        Assert.Equal(new Rect(333, 132, 292, 90), layout.DescriptionTextRoi);

        // 底纹比文本区每边多 6 像素。
        Assert.Equal(new Rect(327, 126, 304, 102), layout.DescriptionShadingRoi);
    }

    [Fact]
    public void TheVictoryConditionIsDroppedInTheLeftQuarter()
    {
        MapSpecsPageLayout layout = Create();

        // 标题中轴是页面四分之一处，纵坐标在描述框下 20。
        Assert.Equal(160, layout.VictoryTitleCenterX);
        Assert.Equal(242, layout.VictoryTitleY);

        // 背景 = 标题下 20，宽高取 DROPLISL 的 0 号帧；文本区在背景内缩 2 / 3 / 2 / 2。
        Assert.Equal(new Rect(99, 262, 100, 19), layout.VictoryBackgroundRoi);
        Assert.Equal(new Rect(101, 265, 96, 14), layout.VictoryTextRoi);

        // 按钮贴在背景右侧，点击范围是文本区与按钮的并集。
        Assert.Equal(new Rect(199, 262, 16, 19), layout.VictoryButtonRoi);
        Assert.Equal(new Rect(101, 262, 114, 19), layout.VictoryDropRoi);

        // 面板在背景下 30，宽度跟随点击范围。
        Assert.Equal(new Rect(99, 292, 114, 150), layout.VictoryPanelRoi);
    }

    [Fact]
    public void TheLossConditionIsTheVictoryOneShiftedHalfAPage()
    {
        MapSpecsPageLayout layout = Create();

        Assert.Equal(480, layout.LossTitleCenterX);
        Assert.Equal(242, layout.LossTitleY);

        Assert.Equal(new Rect(419, 262, 100, 19), layout.LossBackgroundRoi);
        Assert.Equal(new Rect(421, 265, 96, 14), layout.LossTextRoi);
        Assert.Equal(new Rect(519, 262, 16, 19), layout.LossButtonRoi);
        Assert.Equal(new Rect(421, 262, 114, 19), layout.LossDropRoi);
        Assert.Equal(new Rect(419, 292, 114, 150), layout.LossPanelRoi);
    }

    [Fact]
    public void TheLossColumnIsAlwaysHalfAPageToTheRightOfTheVictoryColumn()
    {
        MapSpecsPageLayout layout = Create();

        Assert.Equal(layout.VictoryPanelRoi.X + 320, layout.LossPanelRoi.X);
        Assert.Equal(layout.VictoryPanelRoi.Y, layout.LossPanelRoi.Y);
        Assert.Equal(layout.VictoryPanelRoi.Width, layout.LossPanelRoi.Width);
    }

    [Fact]
    public void TwoPanelsEndWellAboveTheBottomEdge()
    {
        MapSpecsPageLayout layout = Create();

        // 150 高的面板到 442 结束，底部 38 像素留给按钮。
        Assert.Equal(442, layout.VictoryPanelRoi.Bottom);
        Assert.Equal(442, layout.LossPanelRoi.Bottom);
    }

    private static MapSpecsPageLayout Create() => MapSpecsPageLayout.Create(
        Screen,
        new Size(128, 32),
        [new Size(34, 30), new Size(34, 30)],
        difficultyIconSideLength: 30,
        dropListButton: new Size(16, 19),
        dropListBackground: new Size(100, 19));
}
