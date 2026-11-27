using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The checkbox rows of the condition panel, a port of the <c>drawCheckboxWithText</c> call sites
/// (<c>editor_map_specs_window.cpp:1058-1190</c>) plus the check mark blit the port has to do itself.
/// </summary>
/// <remarks>
/// <para>
/// 参考的勾本身是 <c>MovableSprite</c>：<c>_allowNormalVictory.show()</c> 会立刻把勾贴到 Display，所以
/// <c>drawCheckboxWithText</c> 只负责摆位置、不管画。端口没有 Display，画面由场景下一帧整体重画，勾只能由
/// 调用方在这张 <see cref="PixelBuffer"/> 上贴出来，这就是 <see cref="DrawMark"/> 存在的理由。
/// </para>
/// <para>
/// 差异：参考把 <c>roi.width - 5</c> 当文字最大宽（勾选框在 <c>roi.x + 5</c>、右边只留 5）；
/// 端口沿用 <see cref="MapSpecsConditionPanelLayout"/> 的勾选行矩形（左右各内缩 5），
/// 因此文字比参考窄 5 像素。
/// </para>
/// </remarks>
public static class MapSpecsConditionCheckboxRow
{
    /// <summary>
    /// Paints one checkbox with its label and the check mark when the box is checked.
    /// </summary>
    /// <param name="text">Label of the box.</param>
    /// <param name="isChecked">Whether the box is checked, which is the value the box stands for.</param>
    /// <param name="row">The row the box is drawn in, from <see cref="MapSpecsConditionPanelLayout"/>.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork is used.</param>
    /// <returns>The area taken by the box and the label.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException"><paramref name="isEvilInterface"/> is set and the evil box generator is not ported.</exception>
    /// <exception cref="InvalidOperationException">The archive has no <c>CELLWIN</c> frame.</exception>
    public static Rect Draw(string text, bool isChecked, in Rect row, PixelBuffer output, bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(output);

        MovableSprite mark = new();
        Rect area = EditorUiHelper.DrawCheckboxWithText(mark, text, output, row.X, row.Y, isEvilInterface, row.Width);

        if (isChecked)
        {
            // MovableSprite 新建时是隐藏的，参考靠 show() 把勾刷到 Display；端口得自己显出来再贴。
            mark.Show();
            DrawMark(mark, output);
        }

        return area;
    }

    /// <summary>
    /// Blits a check mark that is currently shown, the part the reference's <c>MovableSprite::show</c> does on the Display.
    /// </summary>
    /// <param name="mark">The mark; hidden ones are skipped.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mark"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考：<c>_checkmark</c> 由 <c>Checkbox</c> 构造时 <c>setPosition( _area.x + 2, _area.y + 2 )</c>（<c>:47</c>），
    /// 勾的可见性由 <c>show</c>/<c>hide</c> 决定（<c>:49-54</c>）。端口把可见的勾贴到缓冲区上，位置取
    /// <see cref="MovableSprite.X"/>/<see cref="MovableSprite.Y"/>。
    /// </remarks>
    public static void DrawMark(MovableSprite mark, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(mark);
        ArgumentNullException.ThrowIfNull(output);

        if (mark.IsHidden || mark.Sprite is not { } image)
        {
            return;
        }

        ImageOps.BlitSprite(output, new Point(mark.X, mark.Y), image);
    }
}
