using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The condition panel of the map specifications window: <c>VictoryConditionUI::render()</c>
/// (<c>editor_map_specs_window.cpp:1021-1217</c>) and <c>LossConditionUI::render()</c> (<c>:1668-1743</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的两个 <c>render</c> 是同一个套路：先按条件算出要画的区域（端口放在
/// <see cref="MapSpecsConditionPanelLayout"/>，尺寸从外面量好传进去），再把行、勾选框、数值区拼上去。
/// 这里只做拼接与测量；行本身由 <see cref="MapSpecsConditionTownRow"/>、<see cref="MapSpecsConditionHeroRow"/>、
/// <see cref="MapSpecsConditionArtifactRow"/>、<see cref="MapSpecsConditionCheckboxRow"/> 与
/// <see cref="MapSpecsConditionAllianceBlock"/> 负责。
/// </para>
/// <para>
/// 与参考不同的一点：参考的 <c>render( renderEverything )</c> 有「只重画某一块」的增量路径，靠
/// <c>_getFlags()</c> 与 <c>Restorer</c> 记住哪块变脏；端口每帧整块重画，因此没有 <c>renderEverything</c> 参数。
/// 数值控件也由调用方持有（参考是成员），金与天数两个条件只把它摆到布局给的位置上画一遍：调用方先取
/// <see cref="GetVictoryLayout"/>／<see cref="GetLossLayout"/>，再用 <c>ValueAreaRoi</c> 的左上角创建控件。
/// </para>
/// <para>
/// 缺美术件的行为与参考一致：行、神器、勾选框各自的绘制失败就跳过；只有测量尺寸
/// （<c>CELLWIN</c>、<c>TOWNWIND</c>、<c>RESOURCE</c>、字体）缺件时抛
/// <see cref="InvalidOperationException"/>，因为那时连布局都算不出来。
/// </para>
/// </remarks>
public static class MapSpecsConditionPanelRenderer
{
    /// <summary>Label of the "standard victory" checkbox, <c>_( "Allow standard victory conditions" )</c>.</summary>
    public const string AllowNormalVictoryLabel = "Allow standard victory conditions";

    /// <summary>Label of the AI checkbox, <c>_( "Allow this condition also for AI" )</c> (<c>:1072</c>).</summary>
    public const string AllowForAiLabel = "Allow this condition also for AI";

    /// <summary>Label left of the gold element, <c>_( "Gold:" )</c> (<c>:1177</c>).</summary>
    public const string GoldLabel = "Gold:";

    /// <summary>Label left of the days element, <c>_( "Days:" )</c> (<c>:1727</c>).</summary>
    public const string DaysLabel = "Days:";

    /// <summary>Gap between the label and the number element, <c>uiOffset.x - text.width() - 5</c>.</summary>
    public const int LabelGap = 5;

    /// <summary>Vertical nudge of the label, <c>+ 2</c> (<c>:1178</c>, <c>:1728</c>).</summary>
    public const int LabelOffsetY = 2;

    /// <summary>Frame of <c>CELLWIN</c> that is the checkbox background, <c>ICN::CELLWIN, 1</c>.</summary>
    public const int CheckboxBackgroundFrame = 1;

    private const string CheckboxFile = "CELLWIN.ICN";
    private const string ResourceFile = "RESOURCE.ICN";
    private const string ValueFile = "TOWNWIND.ICN";

    /// <summary>
    /// Works out the areas of a victory panel without drawing anything, for hit testing and for placing the number element.
    /// </summary>
    /// <param name="view">The condition and its data.</param>
    /// <param name="roi">The area the window reserved for the panel.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork is used; both variants have the same heights.</param>
    /// <returns>The areas the panel will use.</returns>
    /// <exception cref="InvalidOperationException">A frame needed to measure the panel is missing from the archives.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="view"/> has no positive map width.</exception>
    public static MapSpecsConditionPanelLayout GetVictoryLayout(in MapSpecsVictoryPanelView view, in Rect roi,
        bool isEvilInterface = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(view.MapWidth);
        ValidateTarget(view);

        return MapSpecsConditionPanelLayout.CreateVictory(
            roi, view.Condition, MeasureVictorySizes(view, isEvilInterface), view.IsNeutralTownTarget);
    }

    /// <summary>
    /// Works out the areas of a loss panel without drawing anything.
    /// </summary>
    /// <param name="view">The condition and its data.</param>
    /// <param name="roi">The area the window reserved for the panel.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork is used.</param>
    /// <returns>The areas the panel will use.</returns>
    /// <exception cref="InvalidOperationException">A frame needed to measure the panel is missing from the archives.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="view"/> has no positive map width.</exception>
    public static MapSpecsConditionPanelLayout GetLossLayout(in MapSpecsLossPanelView view, in Rect roi,
        bool isEvilInterface = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(view.MapWidth);
        ValidateTarget(view);

        return MapSpecsConditionPanelLayout.CreateLoss(roi, view.Condition, MeasureLossSizes(view, isEvilInterface));
    }

    /// <summary>
    /// Paints the victory panel.
    /// </summary>
    /// <param name="view">The condition and its data.</param>
    /// <param name="roi">The area the window reserved for the panel.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork is used.</param>
    /// <param name="gamePalette">Palette the race portrait of a hero row needs.</param>
    /// <param name="value">
    /// The number element of the gold condition, already sitting at <c>GetVictoryLayout( view, roi ).ValueAreaRoi</c>;
    /// ignored by every other condition.
    /// </param>
    /// <returns>The areas and the checkboxes the panel used.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> or <paramref name="gamePalette"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is <see langword="null"/> but the condition shows a number, or the view has no town/hero
    /// for a condition that needs one.
    /// </exception>
    /// <exception cref="InvalidOperationException">A frame needed by the panel is missing from the archives.</exception>
    public static MapSpecsConditionPanel DrawVictory(in MapSpecsVictoryPanelView view, in Rect roi, PixelBuffer output,
        bool isEvilInterface, Palette gamePalette, ValueSelectionElement? value = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(gamePalette);

        MapSpecsConditionPanelLayout layout = GetVictoryLayout(view, roi, isEvilInterface);
        IReadOnlyList<List<Checkbox>> alliances = [];

        switch (view.Condition)
        {
        case MapVictoryCondition.CaptureTown:
            MapSpecsConditionTownRow.Draw(RequireTown(view), view.MapWidth, roi, output, isEvilInterface);
            DrawVictoryCheckboxes(view, layout, output, isEvilInterface);
            break;

        case MapVictoryCondition.KillHero:
            if (MapSpecsConditionHeroRow.TryDraw(
                    RequireHero(view), view.MapWidth, layout.SelectRowRoi, output, gamePalette))
            {
                DrawVictoryCheckboxes(view, layout, output, isEvilInterface);
            }

            break;

        case MapVictoryCondition.ObtainArtifact:
            MapSpecsConditionArtifactRow.TryDraw(view.Artifact, layout.SelectRowRoi, output);
            DrawVictoryCheckboxes(view, layout, output, isEvilInterface);
            break;

        case MapVictoryCondition.DefeatOtherSide:
            alliances = MapSpecsConditionAllianceBlock.Draw(
                view.AvailableColors, view.Alliances ?? [], layout.AllianceTitleRoi, layout.AllianceRowRois, output);
            break;

        case MapVictoryCondition.CollectGold:
            DrawNumber(value, layout.ValueAreaRoi, GoldLabel, output);
            DrawVictoryCheckboxes(view, layout, output, isEvilInterface);
            break;

        case MapVictoryCondition.DefeatEveryone:
            break;
        }

        return new MapSpecsConditionPanel(layout, alliances);
    }

    /// <summary>
    /// Paints the loss panel.
    /// </summary>
    /// <param name="view">The condition and its data.</param>
    /// <param name="roi">The area the window reserved for the panel.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork is used.</param>
    /// <param name="gamePalette">Palette the race portrait of a hero row needs.</param>
    /// <param name="value">
    /// The number element of the out-of-time condition, already sitting at <c>GetLossLayout( view, roi ).ValueAreaRoi</c>;
    /// ignored by every other condition.
    /// </param>
    /// <returns>The areas the panel used; it has no checkboxes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> or <paramref name="gamePalette"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is <see langword="null"/> but the condition shows a number, or the view has no town/hero
    /// for a condition that needs one.
    /// </exception>
    /// <exception cref="InvalidOperationException">A frame needed by the panel is missing from the archives.</exception>
    public static MapSpecsConditionPanel DrawLoss(in MapSpecsLossPanelView view, in Rect roi, PixelBuffer output,
        bool isEvilInterface, Palette gamePalette, ValueSelectionElement? value = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(gamePalette);

        MapSpecsConditionPanelLayout layout = GetLossLayout(view, roi, isEvilInterface);

        switch (view.Condition)
        {
        case MapLossCondition.LoseTown:
            MapSpecsConditionTownRow.Draw(RequireTown(view), view.MapWidth, roi, output, isEvilInterface);
            break;

        case MapLossCondition.LoseHero:
            MapSpecsConditionHeroRow.TryDraw(
                RequireHero(view), view.MapWidth, layout.SelectRowRoi, output, gamePalette);
            break;

        case MapLossCondition.OutOfTime:
        {
            ValueSelectionElement days = value
                ?? throw new ArgumentException("The out-of-time condition needs a number element to draw.", nameof(value));

            DrawNumber(days, layout.ValueAreaRoi, DaysLabel, output);

            // 1730-1733 行：天数描述画在数值区下方 14 像素，按 roi 宽裁剪。
            Rect description = layout.DaysDescriptionRoi;
            new Text(GameDate.Describe(days.Value), FontType.NormalWhite()).DrawInRoi(
                description.X, description.Y, description.Width, output, roi);
            break;
        }

        case MapLossCondition.Everything:
            break;
        }

        return new MapSpecsConditionPanel(layout);
    }

    private static void DrawVictoryCheckboxes(in MapSpecsVictoryPanelView view,
        in MapSpecsConditionPanelLayout layout, PixelBuffer output, bool isEvilInterface)
    {
        // 布局用 Rect.Empty 表示某个框这个条件不提供：杀英雄没有任何勾选项，中立城镇与金才有「也给 AI」的那个
        // （1066、1178 行）。空矩形不能拿去画，否则会贴着缓冲区原点画出半个框。
        if (!layout.AllowForAiRoi.IsEmpty)
        {
            MapSpecsConditionCheckboxRow.Draw(
                AllowForAiLabel, view.AllowForAi, layout.AllowForAiRoi, output, isEvilInterface);
        }

        if (!layout.AllowNormalVictoryRoi.IsEmpty)
        {
            MapSpecsConditionCheckboxRow.Draw(
                AllowNormalVictoryLabel, view.AllowNormalVictory, layout.AllowNormalVictoryRoi, output, isEvilInterface);
        }
    }

    private static void DrawNumber(ValueSelectionElement? value, in Rect area, string label, PixelBuffer output)
    {
        ValueSelectionElement element = value
            ?? throw new ArgumentException("The condition needs a number element to draw.", nameof(value));

        element.Draw(output);

        // 参考：text.draw( uiOffset.x - text.width() - 5, roi.y + ( h - text.height() ) / 2 + 2, output )。
        Text text = new(label, FontType.NormalWhite());
        text.Draw(area.X - text.Width() - LabelGap, area.Y + ((area.Height - text.Height()) / 2) + LabelOffsetY, output);
    }

    private static MapSpecsConditionPanelSizes MeasureVictorySizes(in MapSpecsVictoryPanelView view,
        bool isEvilInterface)
    {
        int selectRowHeight = MapSpecsConditionHeroRow.FrameHeight;
        Size artifactFrame = Size.Empty;
        Size valueArea = Size.Empty;
        int checkboxHeight = 0;
        int textHeight = 0;

        switch (view.Condition)
        {
        case MapVictoryCondition.CaptureTown:
            selectRowHeight = MeasureTownRowHeight(RequireTown(view), isEvilInterface);
            checkboxHeight = MeasureCheckboxHeight();
            break;

        case MapVictoryCondition.KillHero:
            checkboxHeight = MeasureCheckboxHeight();
            break;

        case MapVictoryCondition.ObtainArtifact:
            artifactFrame = MeasureArtifactFrame();
            checkboxHeight = MeasureCheckboxHeight();
            break;

        case MapVictoryCondition.DefeatOtherSide:
            textHeight = MeasureTextHeight();
            break;

        case MapVictoryCondition.CollectGold:
            valueArea = MeasureValueArea();
            checkboxHeight = MeasureCheckboxHeight();
            break;
        }

        return new MapSpecsConditionPanelSizes(selectRowHeight, artifactFrame, valueArea, checkboxHeight, textHeight);
    }

    private static MapSpecsConditionPanelSizes MeasureLossSizes(in MapSpecsLossPanelView view, bool isEvilInterface)
    {
        int selectRowHeight = MapSpecsConditionHeroRow.FrameHeight;
        Size valueArea = Size.Empty;
        int textHeight = 0;

        switch (view.Condition)
        {
        case MapLossCondition.LoseTown:
            selectRowHeight = MeasureTownRowHeight(RequireTown(view), isEvilInterface);
            break;

        case MapLossCondition.OutOfTime:
            valueArea = MeasureValueArea();
            textHeight = MeasureTextHeight();
            break;
        }

        return new MapSpecsConditionPanelSizes(
            selectRowHeight, Size.Empty, valueArea, CheckboxHeight: 0, textHeight);
    }

    private static int MeasureTownRowHeight(in MapSpecsConditionTownRowData town, bool isEvilInterface)
    {
        Sprite? icon = MapSpecsConditionIcons.BuildTownIcon(town.Race, town.Color, town.HasFort, isEvilInterface);

        return icon?.Height ?? 0;
    }

    private static Size MeasureArtifactFrame()
    {
        Sprite frame = RequireImage(ResourceFile, MapSpecsConditionArtifactRow.FrameId);

        return new Size(frame.Width, frame.Height);
    }

    private static Size MeasureValueArea() => ValueSelectionElement.GetArea(
        RequireImage(ValueFile, ValueSelectionElement.EditBoxFrame),
        RequireImage(ValueFile, ValueSelectionElement.UpReleasedFrame));

    private static int MeasureCheckboxHeight() => RequireImage(CheckboxFile, CheckboxBackgroundFrame).Height;

    private static int MeasureTextHeight() => new Text("X", FontType.NormalWhite()).Height();

    private static Sprite RequireImage(string fileName, int frameId) =>
        GameAssets.GetImage(fileName, frameId)
            ?? throw new InvalidOperationException($"{fileName} frame {frameId} is missing from the archives.");

    /// <summary>
    /// Fails early when the condition needs a target the view does not carry, before any frame is measured.
    /// </summary>
    /// <exception cref="ArgumentException">The view has no town or hero for a condition that needs one.</exception>
    private static void ValidateTarget(in MapSpecsVictoryPanelView view)
    {
        switch (view.Condition)
        {
        case MapVictoryCondition.CaptureTown:
            RequireTown(view);
            break;

        case MapVictoryCondition.KillHero:
            RequireHero(view);
            break;
        }
    }

    private static void ValidateTarget(in MapSpecsLossPanelView view)
    {
        switch (view.Condition)
        {
        case MapLossCondition.LoseTown:
            RequireTown(view);
            break;

        case MapLossCondition.LoseHero:
            RequireHero(view);
            break;
        }
    }

    private static MapSpecsConditionTownRowData RequireTown(in MapSpecsVictoryPanelView view) =>
        view.Town ?? throw new ArgumentException("The condition needs the town it applies to.", nameof(view));

    private static MapSpecsConditionTownRowData RequireTown(in MapSpecsLossPanelView view) =>
        view.Town ?? throw new ArgumentException("The condition needs the town it applies to.", nameof(view));

    private static MapSpecsConditionHeroRowData RequireHero(in MapSpecsVictoryPanelView view) =>
        view.Hero ?? throw new ArgumentException("The condition needs the hero it applies to.", nameof(view));

    private static MapSpecsConditionHeroRowData RequireHero(in MapSpecsLossPanelView view) =>
        view.Hero ?? throw new ArgumentException("The condition needs the hero it applies to.", nameof(view));
}
