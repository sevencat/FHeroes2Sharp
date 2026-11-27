using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The sizes a condition panel needs from outside: the rows and controls it hosts are built from archive frames,
/// so their real size is only known when the artwork is loaded.
/// </summary>
/// <param name="SelectRowHeight">Height of the town or hero row painted at the top of the panel.</param>
/// <param name="ArtifactFrameSize">Size of the <c>RESOURCE</c> frame 7 the artifact victory condition centres.</param>
/// <param name="ValueAreaSize">Size of the number element, <c>ValueSelectionDialogElement::getArea()</c>.</param>
/// <param name="CheckboxHeight">Height of one <c>CELLWIN</c> checkbox row.</param>
/// <param name="TextHeight">Height of one line of <c>FontType::normalWhite()</c>.</param>
/// <remarks>
/// 参考里这些尺寸全在绘制时现取——<c>_selectConditionRoi.height</c> 来自刚画的图标、
/// <c>ValueSelectionDialogElement::getArea()</c> 是固定值、勾选框高来自 <c>CELLWIN</c>、文字高来自字体。
/// 端口把它们提出来当参数，布局才能不碰美术件与字体，和 <see cref="MapSpecsPageLayout"/> 的做法一致。
/// </remarks>
public readonly record struct MapSpecsConditionPanelSizes(
    int SelectRowHeight,
    Size ArtifactFrameSize,
    Size ValueAreaSize,
    int CheckboxHeight,
    int TextHeight);
