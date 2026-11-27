using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The rectangles of one condition panel, a port of the layout arithmetic of <c>VictoryConditionUI::render()</c>
/// (<c>editor_map_specs_window.cpp:1021-1210</c>) and <c>LossConditionUI::render()</c> (<c>:1668-1743</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把两个面板写成两套几乎一样的类，各自在 <c>render()</c> 里一边贴图一边算坐标。端口只留几何：
/// 按条件类型给出选中行、数值区、两个勾选项、联盟标题与两条联盟行的位置，绘制与命中测试都读它。
/// </para>
/// <para>
/// 条件与目标是不是中立城镇这样的事实由调用方判断后传进来（<c>isNeutralTownTarget</c>）——
/// 「目标城镇是中立城」只有状态知道，布局不该回头去查地图。
/// </para>
/// <para>
/// 勾选项的宽度按整行给（<c>roi.x + 5</c> 起、宽 <c>roi.width - 10</c>）：参考返回的矩形随文字宽度变化，
/// 命中测试只要求「点在这一行上」，所以这里不需要去量文字。
/// </para>
/// <para>
/// 用不到的矩形是 <see cref="Rect.Empty"/>，调用方据此决定画不画、判定不判定；这与参考用
/// <c>_restorer.rect()</c> 之外的 <c>Roi{}</c> 空值表达同一个意思。
/// </para>
/// </remarks>
public sealed class MapSpecsConditionPanelLayout
{
    /// <summary>Left inset of the two checkboxes, <c>roi.x + 5</c> (<c>:1058</c>).</summary>
    public const int CheckboxOffsetX = 5;

    /// <summary>Right inset of the two checkboxes, <c>roi.width - 5</c> (<c>:1058</c>).</summary>
    public const int CheckboxRightInset = 5;

    /// <summary>Gap between the row above and the "also for AI" checkbox (<c>:1072</c>).</summary>
    public const int AllowForAiGap = 10;

    /// <summary>Gap between the row above and the "standard victory" checkbox (<c>:1059</c>).</summary>
    public const int AllowNormalVictoryGap = 35;

    /// <summary>Gap between the two checkboxes of the gold condition, <c>35 - 10</c> (<c>:1188-1190</c>).</summary>
    public const int CheckboxGap = AllowNormalVictoryGap - AllowForAiGap;

    /// <summary>Vertical offset of the centred artifact frame, <c>roi.y + 4</c> (<c>:1107</c>).</summary>
    public const int ArtifactOffsetY = 4;

    /// <summary>Vertical offset of the alliance title, <c>roi.y + 2</c> (<c>:1136</c>).</summary>
    public const int AllianceTitleOffsetY = 2;

    /// <summary>Distance between two alliance rows, <c>stepY = 32</c> (<c>:1144</c>).</summary>
    public const int AllianceRowStep = 32;

    /// <summary>Number of alliance rows, the size of the reference's <c>_alliances</c>.</summary>
    public const int AllianceCount = 2;

    /// <summary>Gap between the number element and the date description (<c>:1730</c>).</summary>
    public const int DaysDescriptionGap = 14;

    private MapSpecsConditionPanelLayout(Rect roi)
    {
        Roi = roi;
    }

    /// <summary>The panel the conditions are laid out in, <c>_restorer.rect()</c>.</summary>
    public Rect Roi { get; }

    /// <summary>
    /// The row that shows the selected town or hero, or the artifact frame; empty for the conditions that have no
    /// such row (<c>_selectConditionRoi</c>). Clicking it opens the selection list.
    /// </summary>
    public Rect SelectRowRoi { get; private init; } = Rect.Empty;

    /// <summary>The number element of the gold and days conditions (<c>_goldAccumulationValue</c>); empty otherwise.</summary>
    public Rect ValueAreaRoi { get; private init; } = Rect.Empty;

    /// <summary>The "allow standard victory conditions" checkbox; empty when the condition does not offer it.</summary>
    public Rect AllowNormalVictoryRoi { get; private init; } = Rect.Empty;

    /// <summary>The "allow this condition also for AI" checkbox; empty when the condition does not offer it.</summary>
    public Rect AllowForAiRoi { get; private init; } = Rect.Empty;

    /// <summary>The band of the "Set alliances:" title, empty unless the condition is <c>DefeatOtherSide</c>.</summary>
    public Rect AllianceTitleRoi { get; private init; } = Rect.Empty;

    /// <summary>The band of each alliance row, in order; empty unless the condition is <c>DefeatOtherSide</c>.</summary>
    public IReadOnlyList<Rect> AllianceRowRois { get; private init; } = [];

    /// <summary>The band of the date description under the days element, empty unless the loss condition is <c>OutOfTime</c>.</summary>
    public Rect DaysDescriptionRoi { get; private init; } = Rect.Empty;

    /// <summary>
    /// Lays out a victory panel.
    /// </summary>
    /// <param name="roi">Panel rectangle, <see cref="MapSpecsPageLayout.VictoryPanelRoi"/> in the page.</param>
    /// <param name="condition">Condition the panel currently shows.</param>
    /// <param name="sizes">Sizes of the rows and controls, see <see cref="MapSpecsConditionPanelSizes"/>.</param>
    /// <param name="isNeutralTownTarget">
    /// Whether the town of <see cref="MapVictoryCondition.CaptureTown"/> belongs to nobody; only then the reference
    /// offers the "also for AI" checkbox (<c>:1066</c>).
    /// </param>
    /// <remarks>
    /// 参考：<c>VICTORY_DEFEAT_EVERYONE</c> 没有界面（<c>:1029-1032</c>）、<c>VICTORY_CAPTURE_TOWN</c> 是城镇行
    /// 加两个勾选项（<c>:1052-1077</c>）、<c>VICTORY_KILL_HERO</c> 只有英雄行（<c>:1098</c>）、
    /// <c>VICTORY_OBTAIN_ARTIFACT</c> 是居中神器框加「标准胜利」勾选项（<c>:1107-1116</c>）、
    /// <c>VICTORY_DEFEAT_OTHER_SIDE</c> 是标题加两条联盟行（<c>:1136-1161</c>）、
    /// <c>VICTORY_COLLECT_ENOUGH_GOLD</c> 是数值区加两个勾选项（<c>:1170-1190</c>）。
    /// </remarks>
    public static MapSpecsConditionPanelLayout CreateVictory(in Rect roi, MapVictoryCondition condition,
        in MapSpecsConditionPanelSizes sizes, bool isNeutralTownTarget)
    {
        Rect selectRow = Rect.Empty;
        Rect valueArea = Rect.Empty;
        Rect allowNormalVictory = Rect.Empty;
        Rect allowForAi = Rect.Empty;
        Rect allianceTitle = Rect.Empty;
        Rect[] allianceRows = [];
        Rect daysDescription = Rect.Empty;

        switch (condition)
        {
        case MapVictoryCondition.DefeatEveryone:
            // 没有界面。
            break;
        case MapVictoryCondition.CaptureTown:
            selectRow = new Rect(roi.X, roi.Y, roi.Width, sizes.SelectRowHeight);
            allowNormalVictory = CreateCheckbox(roi, roi.Y + sizes.SelectRowHeight + AllowNormalVictoryGap, sizes.CheckboxHeight);

            if (isNeutralTownTarget)
            {
                allowForAi = CreateCheckbox(roi, roi.Y + sizes.SelectRowHeight + AllowForAiGap, sizes.CheckboxHeight);
            }

            break;
        case MapVictoryCondition.KillHero:
            // 只有英雄行，参考不给这个条件任何勾选项。
            selectRow = new Rect(roi.X, roi.Y, roi.Width, sizes.SelectRowHeight);
            break;
        case MapVictoryCondition.ObtainArtifact:
            selectRow = new Rect(
                roi.X + ((roi.Width - sizes.ArtifactFrameSize.Width) / 2), roi.Y + ArtifactOffsetY,
                sizes.ArtifactFrameSize.Width, sizes.ArtifactFrameSize.Height);

            // 注意：这里用的是神器框的高度，参考也没把 ArtifactOffsetY 算进去（:1116）。
            allowNormalVictory = CreateCheckbox(roi, roi.Y + sizes.ArtifactFrameSize.Height + AllowForAiGap, sizes.CheckboxHeight);
            break;
        case MapVictoryCondition.DefeatOtherSide:
        {
            allianceTitle = new Rect(roi.X, roi.Y + AllianceTitleOffsetY, roi.Width, sizes.TextHeight);

            int firstRowY = roi.Y + AllianceTitleOffsetY + sizes.TextHeight;
            allianceRows = new Rect[AllianceCount];

            for (int index = 0; index < allianceRows.Length; ++index)
            {
                allianceRows[index] = new Rect(roi.X, firstRowY + (index * AllianceRowStep), roi.Width, AllianceRowStep);
            }

            break;
        }
        case MapVictoryCondition.CollectGold:
            valueArea = CreateValueArea(roi, sizes.ValueAreaSize);
            allowForAi = CreateCheckbox(roi, roi.Y + sizes.ValueAreaSize.Height + AllowForAiGap, sizes.CheckboxHeight);
            allowNormalVictory = CreateCheckbox(roi, roi.Y + sizes.ValueAreaSize.Height + AllowNormalVictoryGap, sizes.CheckboxHeight);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown victory condition.");
        }

        return new MapSpecsConditionPanelLayout(roi)
        {
            SelectRowRoi = selectRow,
            ValueAreaRoi = valueArea,
            AllowNormalVictoryRoi = allowNormalVictory,
            AllowForAiRoi = allowForAi,
            AllianceTitleRoi = allianceTitle,
            AllianceRowRois = allianceRows,
            DaysDescriptionRoi = daysDescription,
        };
    }

    /// <summary>
    /// Lays out a loss panel.
    /// </summary>
    /// <param name="roi">Panel rectangle, <see cref="MapSpecsPageLayout.LossPanelRoi"/> in the page.</param>
    /// <param name="condition">Condition the panel currently shows.</param>
    /// <param name="sizes">Sizes of the rows and controls, see <see cref="MapSpecsConditionPanelSizes"/>.</param>
    /// <remarks>
    /// 参考：<c>LOSS_EVERYTHING</c> 没有界面（<c>:1674-1677</c>）、<c>LOSS_TOWN</c> 只有城镇行（<c>:1695</c>）、
    /// <c>LOSS_HERO</c> 只有英雄行（<c>:1716</c>）、<c>LOSS_OUT_OF_TIME</c> 是数值区加日期描述（<c>:1721-1733</c>）。
    /// 失败条件没有勾选项，也没有联盟行。
    /// </remarks>
    public static MapSpecsConditionPanelLayout CreateLoss(in Rect roi, MapLossCondition condition,
        in MapSpecsConditionPanelSizes sizes)
    {
        Rect selectRow = Rect.Empty;
        Rect valueArea = Rect.Empty;
        Rect daysDescription = Rect.Empty;

        switch (condition)
        {
        case MapLossCondition.Everything:
            // 没有界面。
            break;
        case MapLossCondition.LoseTown:
        case MapLossCondition.LoseHero:
            selectRow = new Rect(roi.X, roi.Y, roi.Width, sizes.SelectRowHeight);
            break;
        case MapLossCondition.OutOfTime:
            valueArea = CreateValueArea(roi, sizes.ValueAreaSize);
            daysDescription = new Rect(
                roi.X, roi.Y + sizes.ValueAreaSize.Height + DaysDescriptionGap, roi.Width, sizes.TextHeight);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown loss condition.");
        }

        return new MapSpecsConditionPanelLayout(roi)
        {
            SelectRowRoi = selectRow,
            ValueAreaRoi = valueArea,
            DaysDescriptionRoi = daysDescription,
        };
    }

    /// <summary>One checkbox row: inset from both sides by <see cref="CheckboxOffsetX"/>.</summary>
    private static Rect CreateCheckbox(in Rect roi, int y, int height) =>
        new(roi.X + CheckboxOffsetX, y, roi.Width - CheckboxOffsetX - CheckboxRightInset, height);

    /// <summary>The number element, centred horizontally at the top of the panel.</summary>
    private static Rect CreateValueArea(in Rect roi, in Size size) =>
        new(roi.X + ((roi.Width - size.Width) / 2), roi.Y, size.Width, size.Height);
}
