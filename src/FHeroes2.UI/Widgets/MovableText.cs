using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Text that is painted on top of an already drawn screen and can be taken back off, a port of
/// <c>fheroes2::MovableText</c> (<c>src/fheroes2/gui/ui_tool.h:103-145</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_tool.cpp:186</c> <c>MovableText::drawInRoi()</c>。参考持有一个
/// <c>ImageRestorer</c>，<c>hide()</c> 时把画之前的那块背景贴回去；本项目每帧从干净缓冲重画整个画面，
/// 没有「恢复」这一回事，所以 <see cref="Hide"/> 只清标志位，<see cref="Draw"/> 直接写进
/// <see cref="PixelBuffer"/>。
/// </para>
/// <para>
/// 参考在 ROI 之外的文字不画、并且裁片的上沿要与 ROI 对齐（给变音符号留空间）：
/// <c>extraShiftY</c> 把文字整体下移、<c>textArea ^ roi</c> 求交集，这两步照搬。
/// </para>
/// <para>
/// 用它的是 <c>SystemInfoRenderer</c>（时间与 FPS 覆盖层，见 <see cref="SystemInfoRenderer"/>）
/// 和需要临时盖字的窗口。
/// </para>
/// </remarks>
public sealed class MovableText
{
    /// <summary>The text to paint, the reference's <c>_text</c>; <see langword="null"/> until set.</summary>
    public TextBase? Text { get; private set; }

    /// <summary>Whether the text is currently on screen.</summary>
    /// <remarks>参考：<c>ui_tool.h:144</c> <c>_isHidden{ true }</c>（构造之后先是隐藏的）。</remarks>
    public bool IsHidden { get; private set; } = true;

    /// <summary>
    /// The rectangle that was painted by the last <see cref="Draw"/> or <see cref="DrawInRoi"/>,
    /// i.e. the region the caller has to mark as dirty; <see cref="Rect.Empty"/> while hidden.
    /// </summary>
    /// <remarks>参考用它来调用 <c>Display::updateNextRenderRoi()</c>（<c>ui_tool.cpp:337</c> 的同类做法）。</remarks>
    public Rect DrawnArea { get; private set; } = Rect.Empty;

    /// <summary>Replaces the text, <c>update( std::unique_ptr&lt;TextBase&gt; )</c> of the reference.</summary>
    /// <param name="text">The new text.</param>
    /// <remarks>参考：<c>ui_tool.h:119</c>。</remarks>
    public void Update(TextBase text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
    }

    /// <summary>Paints the text at the given position over the whole output.</summary>
    /// <param name="x">Left edge of the text.</param>
    /// <param name="y">Top edge of the text.</param>
    /// <param name="output">Buffer to paint into.</param>
    /// <remarks>参考：<c>ui_tool.h:124</c> <c>draw( x, y )</c>，ROI 就是整张输出。</remarks>
    public void Draw(int x, int y, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        DrawInRoi(x, y, output, output.Bounds);
    }

    /// <summary>Paints the text inside <paramref name="roi"/>, the only region the reference touches.</summary>
    /// <param name="x">Left edge of the text.</param>
    /// <param name="y">Top edge of the text.</param>
    /// <param name="output">Buffer to paint into.</param>
    /// <param name="roi">Region of interest, usually the output clipped by what has to be redrawn.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.cpp:186</c>。</remarks>
    public void DrawInRoi(int x, int y, PixelBuffer output, in Rect roi)
    {
        ArgumentNullException.ThrowIfNull(output);

        Hide();

        if (Text is not { IsEmpty: false } text)
        {
            return;
        }

        Rect textArea = text.Area();
        textArea = new Rect(textArea.X + x, textArea.Y + y, textArea.Width, textArea.Height);

        // Not to cut off the top of diacritic signs in capital letters we shift the text down.
        int extraShiftY = textArea.Y < roi.Y ? roi.Y - textArea.Y : 0;
        textArea = new Rect(textArea.X, textArea.Y, textArea.Width, textArea.Height + extraShiftY);

        Rect overlappedRoi = textArea.Intersect(roi);
        if (overlappedRoi.IsEmpty)
        {
            return;
        }

        text.DrawInRoi(x, y + extraShiftY, output, overlappedRoi);

        DrawnArea = overlappedRoi;
        IsHidden = false;
    }

    /// <summary>Takes the text off screen, <c>hide()</c> of the reference.</summary>
    /// <remarks>参考：<c>ui_tool.h:65</c>（恢复背景并置隐藏）。差异：只置标志位，见类注释。</remarks>
    public void Hide()
    {
        IsHidden = true;
        DrawnArea = Rect.Empty;
    }
}
