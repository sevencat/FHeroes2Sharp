using FHeroes2.Core.Geometry;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where the pieces of <c>Dialog::inputString()</c> land, the arithmetic of
/// <c>src/fheroes2/dialog/dialog_selectcount.cpp:227-304</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把这几行算式直接写在对话框体里；端口抽成纯函数，好让「框多高、输入区落在哪、光标能点的范围多大」在没有
/// 素材的情况下也能核对。所有坐标都是 640x480 的逻辑像素。
/// </para>
/// <para>
/// 输入区的图是 <c>BUYBUILE</c>（好界面）或 <c>BUYBUILD</c>（邪恶界面）的第 3 帧——与 <see cref="FrameBox"/>
/// 的外框恰好反着来，参考两处各写了一遍（<c>dialog_box.cpp</c> 用 <c>BUYBUILE</c> 画邪恶外框，
/// <c>dialog_selectcount.cpp:245</c> 用 <c>BUYBUILD</c> 画邪恶输入区），这里不合并。
/// </para>
/// </remarks>
public static class InputStringLayout
{
    /// <summary>Distance from the top of the frame to the title, <c>title.draw( …, y + 12, … )</c>.</summary>
    public const int TitleTop = 12;

    /// <summary>Distance from the top of the frame to the body text.</summary>
    public const int BodyTop = 12;

    /// <summary>The <c>10</c> above the title and the one above the input area.</summary>
    public const int Gap = 10;

    /// <summary>Room left under the input area for the virtual keyboard button, <c>keyBoardButtonExtraHeight</c>.</summary>
    public const int VirtualKeyboardExtraHeight = 20;

    /// <summary>How far the virtual keyboard button sits above the row of OKAY/CANCEL, <c>dst_pt.y -= 30</c>.</summary>
    public const int VirtualKeyboardRise = 30;

    /// <summary>Width of a multi-line input area, <c>inputAreaWidth = 226</c>.</summary>
    public const int MultiLineWidth = 226;

    /// <summary>Height of a multi-line input area, <c>inputAreaHeight = 265</c>.</summary>
    public const int MultiLineHeight = 265;

    /// <summary>
    /// Extra width of the clickable text area, so a click right after a full line still lands at the end
    /// (<c>dialog_selectcount.cpp:280</c>).
    /// </summary>
    public const int ClickAreaExtraWidth = 4;

    /// <summary>Left inset of the text inside the single line input frame, <c>inputAreaOffset.x</c>.</summary>
    public const int SingleLineTextInsetX = 13;

    /// <summary>Top inset of the text inside the single line input frame, <c>inputAreaOffset.y</c>.</summary>
    public const int SingleLineTextInsetY = 1;

    /// <summary>Horizontal room taken by the two ends of the single line input frame.</summary>
    public const int SingleLineTextInsetTotalX = 26;

    /// <summary>Vertical room taken by the bottom of the single line input frame.</summary>
    public const int SingleLineTextInsetBottom = 3;

    /// <summary>Inset of the text inside the multi-line shading, <c>inputAreaOffset = { 5, 5, -10, -10 }</c>.</summary>
    public const int MultiLineTextInset = 5;

    /// <summary>
    /// Height of the frame's text area, <c>frameBoxHeight</c> of the reference before the button row.
    /// </summary>
    /// <param name="titleHeight">Height of the title, or <c>0</c> when there is none.</param>
    /// <param name="bodyHeight">Height of the question text.</param>
    /// <param name="inputHeight">Height of the input area, its artwork or <see cref="MultiLineHeight"/>.</param>
    /// <returns>The height to pass to <see cref="FrameBox.Create"/>.</returns>
    public static int FrameHeight(int titleHeight, int bodyHeight, int inputHeight) =>
        Gap + titleHeight + bodyHeight + Gap + inputHeight + VirtualKeyboardExtraHeight;

    /// <summary>Top left corner of the title, <c>( frameBoxArea.x, frameBoxArea.y + 12 )</c>.</summary>
    /// <param name="area">Text area of the frame, <see cref="FrameBox.Area"/>.</param>
    /// <returns>The corner the title is drawn at, centred over <see cref="FrameBox.AreaWidth"/> pixels.</returns>
    public static Point TitlePosition(in Rect area) => new(area.X, area.Y + TitleTop);

    /// <summary>Top left corner of the body text, <c>( frameBoxArea.x, frameBoxArea.y + 12 + titleHeight )</c>.</summary>
    /// <param name="area">Text area of the frame.</param>
    /// <param name="titleHeight">Height of the title, or <c>0</c> when there is none.</param>
    /// <returns>The corner the body is drawn at.</returns>
    public static Point BodyPosition(in Rect area, int titleHeight) => new(area.X, area.Y + BodyTop + titleHeight);

    /// <summary>
    /// The input area itself, <c>( area.x + ( area.width - inputAreaWidth ) / 2, area.y + 10 + titleHeight + textboxHeight + 10 )</c>.
    /// </summary>
    /// <param name="area">Text area of the frame.</param>
    /// <param name="width">Width of the artwork, or <see cref="MultiLineWidth"/>.</param>
    /// <param name="height">Height of the artwork, or <see cref="MultiLineHeight"/>.</param>
    /// <param name="titleHeight">Height of the title, or <c>0</c>.</param>
    /// <param name="bodyHeight">Height of the question text.</param>
    /// <returns>The area the input artwork or its shading is painted in.</returns>
    public static Rect InputArea(in Rect area, int width, int height, int titleHeight, int bodyHeight) =>
        new(area.X + ((area.Width - width) / 2), area.Y + Gap + titleHeight + bodyHeight + Gap, width, height);

    /// <summary>
    /// The area the text and the caret are drawn in, <c>textInputArea</c>.
    /// </summary>
    /// <param name="inputArea">Area of the input artwork, see <see cref="InputArea"/>.</param>
    /// <param name="isMultiLine">Whether the text wraps and is drawn on the grey shading.</param>
    /// <returns>The area handed to <see cref="TextInputField"/>.</returns>
    public static Rect TextInputArea(in Rect inputArea, bool isMultiLine) => isMultiLine
        ? new Rect(
            inputArea.X + MultiLineTextInset,
            inputArea.Y + MultiLineTextInset,
            inputArea.Width - (MultiLineTextInset * 2),
            inputArea.Height - (MultiLineTextInset * 2))
        : new Rect(
            inputArea.X + SingleLineTextInsetX,
            inputArea.Y + SingleLineTextInsetY,
            inputArea.Width - SingleLineTextInsetTotalX,
            inputArea.Height - SingleLineTextInsetBottom);

    /// <summary>The area a click moves the caret, <c>textEditClickArea</c>.</summary>
    /// <param name="textArea">Area of the text, see <see cref="TextInputArea"/>.</param>
    /// <returns>The text area widened by <see cref="ClickAreaExtraWidth"/> pixels.</returns>
    public static Rect ClickArea(in Rect textArea) =>
        new(textArea.X, textArea.Y, textArea.Width + ClickAreaExtraWidth, textArea.Height);

    /// <summary>
    /// Top left corner of the virtual keyboard button, centred over the frame and
    /// <see cref="VirtualKeyboardRise"/> pixels above the button row.
    /// </summary>
    /// <param name="area">Text area of the frame.</param>
    /// <param name="buttonSize">Size of the button's artwork.</param>
    /// <returns>The corner the button is placed at.</returns>
    public static Point VirtualKeyboardPosition(in Rect area, in Size buttonSize) =>
        new(area.X + ((area.Width - buttonSize.Width) / 2), area.Y + area.Height - buttonSize.Height - VirtualKeyboardRise);
}
