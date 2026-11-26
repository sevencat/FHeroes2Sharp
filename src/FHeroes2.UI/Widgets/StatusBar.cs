using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A one line message centred inside a strip of the screen, <c>StatusBar</c> (<c>gui/statusbar.h:31-52</c>) and
/// <c>GUI/StatusBar.cs</c> of the reference layout.
/// </summary>
/// <remarks>
/// <para>
/// The reference derives from <c>fheroes2::MovableText</c>; the ported <see cref="Widgets.MovableText"/> is sealed, so
/// this class holds one instead. Everything else follows the reference: the message is cut to the width of the strip
/// with <c>fitToOneRow()</c>, centred, drawn two rows below the top of the strip and remembered so that an unchanged
/// message costs nothing.
/// </para>
/// <para>
/// <c>ShowMessage()</c> ends with <c>Display::instance().render( area )</c> in the reference; the port has no display to
/// submit to, so it returns the area the caller has to mark dirty (an empty area means nothing changed).
/// </para>
/// </remarks>
public sealed class StatusBar
{
    private readonly PixelBuffer _output;
    private readonly MovableText _movable = new();

    private Rect _roi = Rect.Empty;
    private string _previousMessage = string.Empty;
    private Rect _previousMessageRoi = Rect.Empty;

    /// <summary>Creates an empty bar, <c>StatusBar::StatusBar()</c> (<c>statusbar.cpp:34-38</c>).</summary>
    /// <param name="output">The frame the bar paints into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public StatusBar(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    /// <summary>The strip the message is centred in, <c>setRoi()</c> (<c>statusbar.h:36-39</c>).</summary>
    public Rect Roi => _roi;

    /// <summary>Sets the strip the message is centred in.</summary>
    /// <param name="roi">The strip.</param>
    public void SetRoi(Rect roi) => _roi = roi;

    /// <summary>
    /// Forgets what was painted last so that the next <see cref="ShowMessage"/> repaints even an unchanged
    /// message.
    /// </summary>
    /// <remarks>
    /// 参考的状态栏是贴在画面上的活动物件，只有 <c>updateMessage()</c> 自己会改那块像素，所以记住上一条消息就够了。
    /// 端口没有这一层：调用方每帧整块重绘窗口，状态栏底下的 <c>CASLBAR</c> 也被重画一遍，此时缓存必须作废，
    /// 否则同一条消息第二次就画不出来。
    /// </remarks>
    public void Reset()
    {
        _previousMessage = string.Empty;
        _previousMessageRoi = Rect.Empty;
    }

    /// <summary>
    /// Paints a message and reports what has to be redrawn, <c>StatusBar::ShowMessage()</c> (<c>statusbar.cpp:70-78</c>).
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The dirty area, <see cref="Rect.Empty"/> when the message did not change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public Rect ShowMessage(string message) => UpdateMessage(message);

    /// <summary>
    /// Paints a message without rendering, <c>StatusBar::updateMessage()</c> (<c>statusbar.cpp:40-68</c>).
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The dirty area, <see cref="Rect.Empty"/> when the message did not change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public Rect UpdateMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message == _previousMessage)
        {
            // No updates.
            return Rect.Empty;
        }

        _previousMessage = message;

        // Normal white font, with a shadow.
        Text text = new(message, FontType.NormalWhite());

        // Cut off the end of the text if it exceeds the given width.
        text.FitToOneRow(_roi.Width);

        int textWidth = text.Width();
        Rect messageRoi = new(_roi.X + ((_roi.Width - textWidth) / 2), _roi.Y + 1, textWidth, text.Height());

        _movable.Update(text);

        // Draw the text aligned and cut with the ROI, accounting for the +3 ROI offset: the reference draws at y + 2
        // inside a strip whose first three rows are the frame.
        _movable.DrawInRoi(messageRoi.X, messageRoi.Y + 2, _output, messageRoi);

        Rect area = BoundaryRect(_previousMessageRoi, messageRoi);
        _previousMessageRoi = messageRoi;

        return area;
    }

    /// <summary>
    /// The bounding rectangle of two areas, <c>fheroes2::getBoundaryRect()</c>.
    /// </summary>
    /// <remarks>
    /// A plain <c>min</c>/<c>max</c> over both rectangles, like the reference: it is deliberately <b>not</b>
    /// <see cref="Rect.Union"/>, which treats an empty rectangle as "nothing" and would swallow the first message's
    /// area (the first call has an empty <c>_prevMessageRoi</c>).
    /// </remarks>
    private static Rect BoundaryRect(Rect first, Rect second) =>
        new(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Max(first.Right, second.Right) - Math.Min(first.X, second.X),
            Math.Max(first.Bottom, second.Bottom) - Math.Min(first.Y, second.Y));
}
