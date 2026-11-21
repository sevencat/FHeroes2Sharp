using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Flashes an image at a position with a palette tint, a port of <c>FadeDisplayWithPalette()</c>
/// (<c>src/fheroes2/gui/ui_tool.cpp:698-724</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把 <paramref name="top"/> 拷一份留底，然后每帧给留底按 <paramref name="paletteId"/> 换色并贴到
/// <paramref name="position"/>，一共 <c>frameCount</c> 帧，每帧 <c>fadeTimeMs / frameCount</c> 毫秒。
/// 换色每帧重做一次（表是幂等的，效果与参考相同）。
/// </para>
/// <para>
/// 差异：参考在函数内部跑事件循环，端口把逐帧的时机交给宿主，见 <see cref="TryAdvance"/>。
/// </para>
/// </remarks>
public sealed class DisplayFadeWithPalette
{
    private readonly PixelBuffer _display;
    private readonly PixelBuffer _shadow;
    private readonly PaletteId _paletteId;
    private uint _frameNumber;

    /// <summary>Prepares the flash, the part of <c>FadeDisplayWithPalette()</c> before its loop.</summary>
    /// <param name="display">Frame the image is drawn into.</param>
    /// <param name="top">Image to flash.</param>
    /// <param name="position">Top left corner of the image in <paramref name="display"/>.</param>
    /// <param name="paletteId">Palette transform applied to the image every frame.</param>
    /// <param name="fadeTimeMs">Length of the flash in milliseconds.</param>
    /// <param name="frameCount">Number of frames the flash renders.</param>
    /// <remarks>参考：<c>ui_tool.cpp:698-709</c>。</remarks>
    public DisplayFadeWithPalette(
        PixelBuffer display,
        PixelBuffer top,
        in Point position,
        PaletteId paletteId,
        int fadeTimeMs,
        int frameCount)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(top);

        _display = display;
        _shadow = new PixelBuffer(top.Width, top.Height);
        ImageOps.Blit(_shadow, new Point(0, 0), top, top.Bounds);

        Roi = new Rect(position.X, position.Y, top.Width, top.Height);
        Position = position;
        _paletteId = paletteId;
        FrameCount = Math.Max(frameCount, 0);
        DelayMs = FrameCount == 0 ? 0 : fadeTimeMs / FrameCount;
    }

    /// <summary>Region the flash covers, <c>roi</c> of the reference.</summary>
    public Rect Roi { get; }

    /// <summary>Top left corner the image is drawn at.</summary>
    public Point Position { get; }

    /// <summary>Number of frames the flash renders.</summary>
    public int FrameCount { get; }

    /// <summary>Delay between two frames in milliseconds, <c>fadeTimeMs / frameCount</c>.</summary>
    public int DelayMs { get; }

    /// <summary>Whether every frame has been rendered.</summary>
    public bool IsFinished => _frameNumber >= (uint)FrameCount;

    /// <summary>Draws one more frame of the flash.</summary>
    /// <returns><see langword="false"/> once the flash is over.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:711-721</c> 的循环体。</remarks>
    public bool TryAdvance()
    {
        if (IsFinished)
        {
            return false;
        }

        ImageOps.ApplyPalette(_shadow, _paletteId);
        ImageOps.Blit(_display, Position, _shadow, _shadow.Bounds);

        ++_frameNumber;

        return true;
    }
}
