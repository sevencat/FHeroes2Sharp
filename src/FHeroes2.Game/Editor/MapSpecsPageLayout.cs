using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The rectangle of every element of the map specifications page, a port of the layout arithmetic of
/// <c>mapSpecificationsDialog()</c> (<c>src/fheroes2/editor/editor_map_specs_window.cpp:2043-2210</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把布局、贴图和事件全写在一个函数里，于是每个矩形都靠 <c>offsetX/offsetY</c> 一路 <c>+=</c> 滚下来。
/// 端口把滚出来的结果收成这张表：一次算完，绘制和命中测试都读它，同一个数字只写一遍。
/// </para>
/// <para>
/// 尺寸参数来自美术件本身——名称框、玩家图标、难度图标、下拉框的帧大小各不相同——所以不在这里写死，
/// 由调用方按 <see cref="Create"/> 的形参传进来；能写死的四个（描述框 292x90、玩家图标步长 80、
/// 难度图标步长 77、条件面板高 150）按参考常量留在 <see cref="MapSpecsPageLayout"/> 上。
/// </para>
/// <para>
/// 标题的横坐标只给出中轴：参考是 <c>offsetX + (宽度 - text.width()) / 2</c>，文字宽度只有绘制时才知道，
/// 所以居中留给绘制方，免得布局为了量一段文字去开字体。
/// </para>
/// </remarks>
public sealed class MapSpecsPageLayout
{
    /// <summary>Width of the description box, <c>descriptionBoxWidth</c> (<c>:78</c>).</summary>
    public const int DescriptionBoxWidth = 292;

    /// <summary>Height of the description box, <c>descriptionBoxHeight</c> (<c>:79</c>).</summary>
    public const int DescriptionBoxHeight = 90;

    /// <summary>Distance between two player icons, <c>playerStepX</c> (<c>:81</c>).</summary>
    public const int PlayerStepX = 80;

    /// <summary>Distance between two difficulty icons, <c>difficultyStepX</c> (<c>:82</c>).</summary>
    public const int DifficultyStepX = 77;

    /// <summary>Height of one condition panel (<c>:2181</c>).</summary>
    public const int ConditionPanelHeight = 150;

    /// <summary>Number of difficulty icons the page offers.</summary>
    public const int DifficultyCount = 4;

    private MapSpecsPageLayout(Rect activeArea, List<Rect> playerRois, List<Rect> difficultyRois)
    {
        ActiveArea = activeArea;
        PlayerRois = playerRois;
        DifficultyRois = difficultyRois;
    }

    /// <summary>The area the page may paint on.</summary>
    public Rect ActiveArea { get; }

    /// <summary>The name frame of the map, <c>scenarioBoxRoi</c>.</summary>
    public Rect ScenarioBoxRoi { get; private init; }

    /// <summary>The text area inside the name frame, <c>mapNameRoi</c> (frame inset by 6 / 5 / 6 / 6).</summary>
    public Rect MapNameRoi { get; private init; }

    /// <summary>The icon of each colour the map uses, left to right.</summary>
    public IReadOnlyList<Rect> PlayerRois { get; }

    /// <summary>Where the shadow of <see cref="PlayerRois"/> is painted, five pixels left and three down (<c>:2089</c>).</summary>
    public IReadOnlyList<Rect> PlayerShadowRois { get; private init; } = [];

    /// <summary>The four difficulty icons, already shifted by -3 for the cursor (<c>:2123</c>).</summary>
    public IReadOnlyList<Rect> DifficultyRois { get; }

    /// <summary>Where the icons of the difficulty row are copied before the shift, i.e. <see cref="DifficultyRois"/> + 3.</summary>
    public IReadOnlyList<Rect> DifficultyIconRois { get; private init; } = [];

    /// <summary>Horizontal centre of the "Map Difficulty" label.</summary>
    public int DifficultyTitleCenterX { get; private init; }

    /// <summary>Vertical position of the "Map Difficulty" label.</summary>
    public int DifficultyTitleY { get; private init; }

    /// <summary>Horizontal centre of the "Map Description" label.</summary>
    public int DescriptionTitleCenterX { get; private init; }

    /// <summary>Vertical position of the "Map Description" label.</summary>
    public int DescriptionTitleY { get; private init; }

    /// <summary>The description text area.</summary>
    public Rect DescriptionTextRoi { get; private init; }

    /// <summary>The shading the description sits on, the text area grown by 6 on every side (<c>:2145</c>).</summary>
    public Rect DescriptionShadingRoi => new(DescriptionTextRoi.X - 6, DescriptionTextRoi.Y - 6, DescriptionTextRoi.Width + 12, DescriptionTextRoi.Height + 12);

    /// <summary>Horizontal centre of the "Special Victory Condition" label.</summary>
    public int VictoryTitleCenterX { get; private init; }

    /// <summary>Vertical position of the "Special Victory Condition" label.</summary>
    public int VictoryTitleY { get; private init; }

    /// <summary>The text area of the selected victory condition.</summary>
    public Rect VictoryTextRoi { get; private init; }

    /// <summary>The background of the selected victory condition (the <c>DROPLISL</c> frame).</summary>
    public Rect VictoryBackgroundRoi { get; private init; }

    /// <summary>The drop button of the victory condition, next to the text area.</summary>
    public Rect VictoryButtonRoi { get; private init; }

    /// <summary>The union of <see cref="VictoryTextRoi"/> and <see cref="VictoryButtonRoi"/>, the click target.</summary>
    public Rect VictoryDropRoi { get; private init; }

    /// <summary>The victory condition panel under the drop box.</summary>
    public Rect VictoryPanelRoi { get; private init; }

    /// <summary>Horizontal centre of the "Special Loss Condition" label.</summary>
    public int LossTitleCenterX { get; private init; }

    /// <summary>Vertical position of the "Special Loss Condition" label.</summary>
    public int LossTitleY { get; private init; }

    /// <summary>The text area of the selected loss condition.</summary>
    public Rect LossTextRoi { get; private init; }

    /// <summary>The background of the selected loss condition.</summary>
    public Rect LossBackgroundRoi { get; private init; }

    /// <summary>The drop button of the loss condition.</summary>
    public Rect LossButtonRoi { get; private init; }

    /// <summary>The union of <see cref="LossTextRoi"/> and <see cref="LossButtonRoi"/>, the click target.</summary>
    public Rect LossDropRoi { get; private init; }

    /// <summary>The loss condition panel under the drop box.</summary>
    public Rect LossPanelRoi { get; private init; }

    /// <summary>
    /// Computes the page, <c>mapSpecificationsDialog()</c>'s layout arithmetic.
    /// </summary>
    /// <param name="activeArea">The area <c>StandardWindow::activeArea()</c> reports.</param>
    /// <param name="scenarioBox">Size of the <c>METALLIC_BORDERED_TEXTBOX</c> frame.</param>
    /// <param name="playerIcons">Size of the <c>NGEXTRA</c> icon of each colour the map uses, in order.</param>
    /// <param name="difficultyIconSideLength">Side of the <c>NGEXTRA</c> 62 cursor, which sizes the difficulty cells.</param>
    /// <param name="dropListButton">Size of the <c>DROPLISL</c> drop button.</param>
    /// <param name="dropListBackground">Size of the <c>DROPLISL</c> 0 frame, the condition text background.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="playerIcons"/> is <see langword="null"/>.</exception>
    public static MapSpecsPageLayout Create(
        Rect activeArea,
        Size scenarioBox,
        IReadOnlyList<Size> playerIcons,
        int difficultyIconSideLength,
        Size dropListButton,
        Size dropListBackground)
    {
        ArgumentNullException.ThrowIfNull(playerIcons);

        // 名称框：水平居中、离页顶 10。
        Rect scenarioBoxRoi = new(activeArea.X + ((activeArea.Width - scenarioBox.Width) / 2), activeArea.Y + 10, scenarioBox.Width, scenarioBox.Height);
        Rect mapNameRoi = new(scenarioBoxRoi.X + 6, scenarioBoxRoi.Y + 5, scenarioBoxRoi.Width - 12, scenarioBoxRoi.Height - 11);

        // 玩家图标：按数量居中排开，每格 PlayerStepX 宽。
        List<Rect> playerRois = new(playerIcons.Count);
        int playerOffsetX = activeArea.X + ((activeArea.Width - (playerIcons.Count * PlayerStepX)) / 2);
        int playerOffsetY = scenarioBoxRoi.Y + scenarioBoxRoi.Height + 10;

        for (int index = 0; index < playerIcons.Count; ++index)
        {
            playerRois.Add(new Rect(playerOffsetX + (index * PlayerStepX), playerOffsetY, playerIcons[index].Width, playerIcons[index].Height));
        }

        // 难度图标：贴着左边界，四个一排；贴图后整块再往左上挪 3 给光标留位置（参考 :2123）。
        int difficultyOffsetX = activeArea.X + 15;
        int difficultyTitleY = scenarioBoxRoi.Y + scenarioBoxRoi.Height + 65;
        int difficultyOffsetY = difficultyTitleY + 23;

        List<Rect> difficultyRois = new(DifficultyCount);
        for (int index = 0; index < DifficultyCount; ++index)
        {
            difficultyRois.Add(new Rect(
                difficultyOffsetX + (DifficultyStepX * index) - 3,
                difficultyOffsetY - 3,
                difficultyIconSideLength,
                difficultyIconSideLength));
        }

        // 描述框：贴着右边界，标题与难度标题同高。
        int descriptionOffsetX = activeArea.X + activeArea.Width - DescriptionBoxWidth - 15;
        Rect descriptionTextRoi = new(descriptionOffsetX, difficultyTitleY + 25, DescriptionBoxWidth, DescriptionBoxHeight);

        // 胜利条件：左半页；下拉框与按钮同高，两者并起来是点击范围。
        int victoryOffsetY = descriptionTextRoi.Y + descriptionTextRoi.Height + 20;
        int itemBackgroundOffsetX = (activeArea.Width / 4) - (dropListBackground.Width / 2) - 11;
        int victoryOffsetX = activeArea.X + itemBackgroundOffsetX;

        Rect victoryBackgroundRoi = new(victoryOffsetX, victoryOffsetY + 20, dropListBackground.Width, dropListBackground.Height);
        Rect victoryTextRoi = new(victoryBackgroundRoi.X + 2, victoryBackgroundRoi.Y + 3, victoryBackgroundRoi.Width - 4, victoryBackgroundRoi.Height - 5);
        Rect victoryButtonRoi = new(victoryBackgroundRoi.Right, victoryBackgroundRoi.Y, dropListButton.Width, dropListButton.Height);
        Rect victoryDropRoi = Boundary(victoryTextRoi, victoryButtonRoi);

        // 失败条件：右半页，与胜利条件同一套偏移，只是整体右移半页。
        int lossOffsetX = activeArea.X + (activeArea.Width / 2) + itemBackgroundOffsetX;

        Rect lossBackgroundRoi = new(lossOffsetX, victoryBackgroundRoi.Y, dropListBackground.Width, dropListBackground.Height);
        Rect lossTextRoi = new(lossBackgroundRoi.X + 2, lossBackgroundRoi.Y + 3, victoryTextRoi.Width, victoryTextRoi.Height);
        Rect lossButtonRoi = new(lossBackgroundRoi.Right, lossBackgroundRoi.Y, dropListButton.Width, dropListButton.Height);
        Rect lossDropRoi = Boundary(lossTextRoi, lossButtonRoi);

        return new MapSpecsPageLayout(activeArea, playerRois, difficultyRois)
        {
            PlayerShadowRois = playerRois.Select(roi => new Rect(roi.X - 5, roi.Y + 3, roi.Width, roi.Height)).ToList(),
            DifficultyIconRois = difficultyRois.Select(roi => new Rect(roi.X + 3, roi.Y + 3, roi.Width, roi.Height)).ToList(),
            ScenarioBoxRoi = scenarioBoxRoi,
            MapNameRoi = mapNameRoi,
            DifficultyTitleCenterX = difficultyOffsetX + ((DifficultyStepX * DifficultyCount) / 2),
            DifficultyTitleY = difficultyTitleY,
            DescriptionTitleCenterX = descriptionOffsetX + (DescriptionBoxWidth / 2),
            DescriptionTitleY = difficultyTitleY,
            DescriptionTextRoi = descriptionTextRoi,
            VictoryTitleCenterX = activeArea.X + (activeArea.Width / 4),
            VictoryTitleY = victoryOffsetY,
            VictoryTextRoi = victoryTextRoi,
            VictoryBackgroundRoi = victoryBackgroundRoi,
            VictoryButtonRoi = victoryButtonRoi,
            VictoryDropRoi = victoryDropRoi,
            VictoryPanelRoi = new Rect(victoryOffsetX, victoryBackgroundRoi.Y + 30, victoryDropRoi.Width, ConditionPanelHeight),
            LossTitleCenterX = activeArea.X + ((3 * activeArea.Width) / 4),
            LossTitleY = victoryOffsetY,
            LossTextRoi = lossTextRoi,
            LossBackgroundRoi = lossBackgroundRoi,
            LossButtonRoi = lossButtonRoi,
            LossDropRoi = lossDropRoi,
            LossPanelRoi = new Rect(lossOffsetX, lossBackgroundRoi.Y + 30, lossDropRoi.Width, ConditionPanelHeight),
        };
    }

    /// <summary>The smallest rectangle holding both, <c>fheroes2::getBoundaryRect()</c>.</summary>
    private static Rect Boundary(Rect first, Rect second)
    {
        int left = Math.Min(first.X, second.X);
        int top = Math.Min(first.Y, second.Y);
        int right = Math.Max(first.Right, second.Right);
        int bottom = Math.Max(first.Bottom, second.Bottom);

        return new Rect(left, top, right - left, bottom - top);
    }
}
