using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The <c>Set alliances:</c> block of the <c>DefeatOtherSide</c> victory condition, a port of
/// <c>editor_map_specs_window.cpp:1136-1161</c>.
/// </summary>
/// <remarks>
/// <para>
/// 标题居中画在面板顶部 <c>+2</c> 处（<c>:1136-1139</c>），下面每行是「1st alliance:」这类序数文本加一排彩色勾选框
/// （<c>:1147-1161</c>）：每行步进 32（<c>:1144</c>），整排勾选框的水平中心是
/// <c>roi.x + ( roi.width - 颜色数 * 32 + 12 ) / 2</c>（<c>:1145</c>），文本右移 5 像素后从勾选框中心往左摆，
/// 勾选框紧接文本右端（<c>:1152-1157</c>）。
/// </para>
/// <para>
/// 参考把勾选框留在 <c>_alliancesCheckboxes</c> 里等 <c>processEvents()</c> 换勾；端口把每行的框列表返回给调用方，
/// 并且因为勾是 <c>MovableSprite</c>、没有 Display 立刻贴图，这里顺手把已勾的勾画上
/// （<see cref="MapSpecsConditionCheckboxRow.DrawMark"/>）。
/// </para>
/// </remarks>
public static class MapSpecsConditionAllianceBlock
{
    /// <summary>The title of the block, <c>_( "Set alliances:" )</c> (<c>:1138</c>).</summary>
    public const string Title = "Set alliances:";

    /// <summary>Horizontal step between two colour boxes, <c>:1144-1157</c>.</summary>
    public const int CheckboxStep = 32;

    /// <summary>Slack the reference adds when centring the row of boxes, <c>:1145</c>.</summary>
    public const int BoxesSlack = 12;

    /// <summary>Extra width the row text is given before the boxes start, <c>text.width() + 5</c> (<c>:1152</c>).</summary>
    public const int TextGap = 5;

    /// <summary>Vertical offset of the row text inside its band, <c>offsetY + 4</c> (<c>:1154</c>).</summary>
    public const int TextOffsetY = 4;

    /// <summary>
    /// Paints the title and every alliance row.
    /// </summary>
    /// <param name="availableColors">Colours the map allows, one box is drawn per colour.</param>
    /// <param name="selectedColors">
    /// Colours that start checked, one entry per row; shorter lists leave the remaining rows unchecked.
    /// </param>
    /// <param name="titleRoi">The band of the title, the layout's <see cref="MapSpecsConditionPanelLayout.AllianceTitleRoi"/>.</param>
    /// <param name="rowRois">The bands of the rows, the layout's <see cref="MapSpecsConditionPanelLayout.AllianceRowRois"/>.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <returns>The boxes of every row, in row order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="selectedColors"/>, <paramref name="rowRois"/> or <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The archive has no <c>CELLWIN</c> frame.</exception>
    public static IReadOnlyList<List<Checkbox>> Draw(PlayerColor availableColors,
        IReadOnlyList<PlayerColor> selectedColors, in Rect titleRoi, IReadOnlyList<Rect> rowRois, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(selectedColors);
        ArgumentNullException.ThrowIfNull(rowRois);
        ArgumentNullException.ThrowIfNull(output);

        Text title = new(Title, FontType.NormalWhite());
        title.Draw(titleRoi.X + ((titleRoi.Width - title.Width()) / 2), titleRoi.Y, output);

        int colorCount = PlayerColors.ToList(availableColors).Count;
        int boxesOffsetX = titleRoi.X + (((titleRoi.Width - (colorCount * CheckboxStep)) + BoxesSlack) / 2);

        List<Checkbox>[] rows = new List<Checkbox>[rowRois.Count];

        for (int index = 0; index < rowRois.Count; ++index)
        {
            // 参考 1148-1150 行：_( "{%number} alliance: " ) 里的序数占位符换成 getAllianceOrderText( i )。
            Text rowText = new($"{MapSpecsConditionRows.GetAllianceOrderText(index)} alliance: ", FontType.NormalWhite());
            int textWidth = rowText.Width() + TextGap;
            int posX = boxesOffsetX - (textWidth / 2);
            Rect row = rowRois[index];

            rowText.Draw(posX, row.Y + TextOffsetY, output);

            List<Checkbox> boxes = [];
            PlayerColor selected = index < selectedColors.Count ? selectedColors[index] : PlayerColor.None;
            EditorUiHelper.CreateColorCheckboxes(boxes, availableColors, selected, posX + textWidth, row.Y, output);

            foreach (Checkbox box in boxes)
            {
                MapSpecsConditionCheckboxRow.DrawMark(box.Checkmark, output);
            }

            rows[index] = boxes;
        }

        return rows;
    }
}
