using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// A drawable text, the reference's <c>fheroes2::TextBase</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_text.h:123</c> class TextBase（<c>width</c> 130、<c>height</c> 133、
/// <c>width( maxWidth )</c> 136、<c>height( maxWidth )</c> 139、<c>rows</c> 142、<c>area</c> 145、
/// <c>drawInRoi</c> 148/151、<c>empty</c> 154、<c>fitToOneRow</c> 157、<c>text</c> 160、
/// <c>_isUniformedVerticalAlignment</c> 187）。
/// </para>
/// <para>
/// 未搬：<c>getLanguage()</c> 与 <c>_language</c>（本地化子系统 <c>ui_language.*</c> 还没移植），
/// 派生类 <c>TextInput</c>（278）与 <c>MultiFontText</c>（347）。
/// </para>
/// <para>
/// 差异：参考的 <c>text()</c> 与类同名，C# 不允许成员名等于外层类型名，故叫 <see cref="Content"/>。
/// </para>
/// </remarks>
public abstract class TextBase
{
    /// <summary>Width of the text on a single line.</summary>
    /// <returns>Width in pixels.</returns>
    public abstract int Width();

    /// <summary>Height of the text on a single line.</summary>
    /// <returns>Height in pixels.</returns>
    public abstract int Height();

    /// <summary>Width of the text wrapped to <paramref name="maxWidth"/>.</summary>
    /// <param name="maxWidth">Maximum line width; <c>&lt; 1</c> keeps the text on one line.</param>
    /// <returns>Width in pixels.</returns>
    public abstract int Width(int maxWidth);

    /// <summary>Height of the text wrapped to <paramref name="maxWidth"/>.</summary>
    /// <param name="maxWidth">Maximum line width; <c>&lt; 1</c> keeps the text on one line.</param>
    /// <returns>Height in pixels.</returns>
    public abstract int Height(int maxWidth);

    /// <summary>Number of lines the text occupies.</summary>
    /// <param name="maxWidth">Maximum line width; <c>&lt; 1</c> keeps the text on one line.</param>
    /// <returns>The line count.</returns>
    public abstract int Rows(int maxWidth);

    /// <summary>Area covered by the glyphs of a single line text.</summary>
    /// <returns>The area, relative to the drawing origin.</returns>
    public abstract Rect Area();

    /// <summary>Draws the text on one line, clipped to <paramref name="imageRoi"/>.</summary>
    /// <param name="x">Left edge of the text origin.</param>
    /// <param name="y">Top edge of the text origin.</param>
    /// <param name="output">Frame buffer.</param>
    /// <param name="imageRoi">Clip rectangle.</param>
    public abstract void DrawInRoi(int x, int y, PixelBuffer output, in Rect imageRoi);

    /// <summary>Draws the text wrapped to <paramref name="maxWidth"/>, clipped to <paramref name="imageRoi"/>.</summary>
    /// <param name="x">Left edge of the text origin.</param>
    /// <param name="y">Top edge of the text origin.</param>
    /// <param name="maxWidth">Maximum line width; <c>&lt; 1</c> keeps the text on one line.</param>
    /// <param name="output">Frame buffer.</param>
    /// <param name="imageRoi">Clip rectangle.</param>
    public abstract void DrawInRoi(int x, int y, int maxWidth, PixelBuffer output, in Rect imageRoi);

    /// <summary>Draws the text on one line over the whole frame buffer.</summary>
    /// <param name="x">Left edge of the text origin.</param>
    /// <param name="y">Top edge of the text origin.</param>
    /// <param name="output">Frame buffer.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.h:148</c> <c>draw( x, y, output )</c>。</remarks>
    public void Draw(int x, int y, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        DrawInRoi(x, y, output, new Rect(0, 0, output.Width, output.Height));
    }

    /// <summary>Draws the text wrapped to <paramref name="maxWidth"/> over the whole frame buffer.</summary>
    /// <param name="x">Left edge of the text origin.</param>
    /// <param name="y">Top edge of the text origin.</param>
    /// <param name="maxWidth">Maximum line width.</param>
    /// <param name="output">Frame buffer.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.h:154</c> <c>draw( x, y, maxWidth, output )</c>。</remarks>
    public void Draw(int x, int y, int maxWidth, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        DrawInRoi(x, y, maxWidth, output, new Rect(0, 0, output.Width, output.Height));
    }

    /// <summary>Whether there is nothing to draw.</summary>
    public abstract bool IsEmpty { get; }

    /// <summary>Cuts the text so that it fits into <paramref name="maxWidth"/> on a single line.</summary>
    /// <param name="maxWidth">Maximum width in pixels.</param>
    public abstract void FitToOneRow(int maxWidth);

    /// <summary>The text itself.</summary>
    public abstract string Content { get; }

    /// <summary>
    /// <c>_isUniformedVerticalAlignment</c>: when set, a wrapped text is rebalanced so that it spreads
    /// over its line count evenly instead of using the full width of the last line.
    /// </summary>
    protected bool IsUniformedVerticalAlignment { get; set; } = true;
}
