using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Screen fade in and fade out, a port of <c>fadeOutDisplay()</c>, <c>fadeInDisplay()</c> and their helper
/// <c>fadeDisplay()</c> (<c>src/fheroes2/gui/ui_tool.cpp:62-118</c>, <c>:653-696</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的做法：先把这个区域的原样拷一份，然后每帧用 <see cref="ImageOps.ApplyAlpha"/> 把拷贝按逐渐变小的
/// alpha 压暗写回画面（<c>255</c> 直接拷回、<c>0</c> 直接填黑），一帧一次延时，
/// <c>frameCount</c> 帧走完。alpha 从 <c>startAlpha</c> 到 <c>endAlpha</c> 等分 <c>frameCount - 1</c> 段，
/// 每帧取整（<c>std::round</c>，端口用 <see cref="MidpointRounding.AwayFromZero"/>）。
/// </para>
/// <para>
/// 差异：参考在函数内部跑事件循环并调用 <c>Display::render</c>，端口把循环交给宿主——
/// 每帧调一次 <see cref="TryAdvance"/>，画面呈现由宿主负责。<c>DelayMs</c> 与每帧的 alpha 序列一致。
/// </para>
/// <para>
/// 差异：参考的 <c>fadeDisplay()</c> 会把光标切成 <c>Cursor::POINTER</c>，端口的光标还没有移植（计划 B 步），
/// 这里不做。
/// </para>
/// </remarks>
public sealed class DisplayFade
{
    /// <summary>Alpha of a fully dark frame, <c>fullDarkAlpha</c>.</summary>
    public const byte FullDarkAlpha = 0;

    /// <summary>Alpha of a fully bright frame, <c>fullBrightAlpha</c>.</summary>
    public const byte FullBrightAlpha = 255;

    /// <summary>Duration of one fade direction in milliseconds, <c>screenFadeTimeMs</c>.</summary>
    public const uint ScreenFadeTimeMs = 100;

    /// <summary>Frames of one fade direction, <c>screenFadeFrameCount</c> (6 frames over 100 ms is 60 FPS).</summary>
    public const uint ScreenFadeFrameCount = 6;

    /// <summary>Alpha covered by one frame, <c>screenFadeStep</c>.</summary>
    public const byte ScreenFadeStep = (FullBrightAlpha - FullDarkAlpha) / (byte)ScreenFadeFrameCount;

    private readonly PixelBuffer _display;
    private readonly PixelBuffer _saved;
    private readonly double _alphaStep;
    private double _alpha;
    private uint _frameNumber;

    private DisplayFade(
        PixelBuffer display, byte startAlpha, byte endAlpha, in Rect roi, uint fadeTimeMs, uint frameCount)
    {
        _display = display;
        StartAlpha = startAlpha;
        EndAlpha = endAlpha;
        FrameCount = frameCount;
        DelayMs = (int)(fadeTimeMs / frameCount);

        Roi = roi.Intersect(display.Bounds);
        _saved = new PixelBuffer(Roi.Width, Roi.Height);
        ImageOps.Blit(_saved, new Point(0, 0), display, Roi);

        _alpha = startAlpha;
        _alphaStep = (startAlpha - endAlpha) / (double)(frameCount - 1);
    }

    /// <summary>Alpha of the first frame.</summary>
    public byte StartAlpha { get; }

    /// <summary>Alpha the fade heads to.</summary>
    public byte EndAlpha { get; }

    /// <summary>Number of frames the fade renders.</summary>
    public uint FrameCount { get; }

    /// <summary>Delay between two frames in milliseconds, <c>fadeTimeMs / frameCount</c>.</summary>
    public int DelayMs { get; }

    /// <summary>Clipped region the fade covers.</summary>
    public Rect Roi { get; }

    /// <summary>Number of frames rendered so far.</summary>
    public uint FrameNumber => _frameNumber;

    /// <summary>Alpha of the next frame, the <c>alpha</c> local of <c>fadeDisplay()</c>.</summary>
    public double CurrentAlpha => _alpha;

    /// <summary>Whether every frame has been rendered.</summary>
    public bool IsFinished => _frameNumber >= FrameCount;

    /// <summary>Fades the whole screen out, <c>fadeOutDisplay()</c>.</summary>
    /// <param name="display">Frame to fade.</param>
    /// <returns>The fade, or <see langword="null"/> when there is nothing to do.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:653-658</c>。</remarks>
    public static DisplayFade? Out(PixelBuffer display)
    {
        ArgumentNullException.ThrowIfNull(display);

        return Out(display, display.Bounds, false);
    }

    /// <summary>Fades a region out to black, <c>fadeOutDisplay( roi, halfFade )</c>.</summary>
    /// <param name="display">Frame to fade.</param>
    /// <param name="roi">Region that fades.</param>
    /// <param name="halfFade">
    /// When <see langword="true"/>, only the first half of the fade: half the time, half the frames,
    /// down to half darkened.
    /// </param>
    /// <returns>The fade, or <see langword="null"/> when there is nothing to do.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:660-673</c>。</remarks>
    public static DisplayFade? Out(PixelBuffer display, in Rect roi, bool halfFade)
    {
        ArgumentNullException.ThrowIfNull(display);

        // As the screen is fully bright already, the fade starts one step in and skips the bright frame.
        byte startAlpha = (byte)(FullBrightAlpha - ScreenFadeStep);

        return halfFade
            ? Create(display, startAlpha, (byte)((FullBrightAlpha - FullDarkAlpha) / 2), roi,
                ScreenFadeTimeMs / 2, ScreenFadeFrameCount / 2)
            : Create(display, startAlpha, FullDarkAlpha, roi, ScreenFadeTimeMs, ScreenFadeFrameCount);
    }

    /// <summary>Fades the whole screen in, <c>fadeInDisplay()</c>.</summary>
    /// <param name="display">Frame holding the dark picture to brighten.</param>
    /// <returns>The fade, or <see langword="null"/> when there is nothing to do.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:675-680</c>。</remarks>
    public static DisplayFade? In(PixelBuffer display)
    {
        ArgumentNullException.ThrowIfNull(display);

        return In(display, display.Bounds, false);
    }

    /// <summary>Brightens a region of the dark picture, <c>fadeInDisplay( roi, halfFade )</c>.</summary>
    /// <param name="display">Frame holding the dark picture to brighten.</param>
    /// <param name="roi">Region that brightens.</param>
    /// <param name="halfFade">
    /// When <see langword="true"/>, only the second half of the fade. The reference adds an extra frame so
    /// the last one is fully bright.
    /// </param>
    /// <returns>The fade, or <see langword="null"/> when there is nothing to do.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:682-696</c>。</remarks>
    public static DisplayFade? In(PixelBuffer display, in Rect roi, bool halfFade)
    {
        ArgumentNullException.ThrowIfNull(display);

        // The screen is the dark picture of the previous fade-out, so the fade starts one step in.
        byte startAlpha = (byte)(FullDarkAlpha + ScreenFadeStep);

        return halfFade
            ? Create(display, (byte)((FullBrightAlpha - startAlpha) / 2), FullBrightAlpha, roi,
                ScreenFadeTimeMs * 3 / 4, (ScreenFadeFrameCount / 2) + 1)
            : Create(display, startAlpha, FullBrightAlpha, roi, ScreenFadeTimeMs, ScreenFadeFrameCount);
    }

    /// <summary>Renders the next frame of the fade.</summary>
    /// <param name="gamePalette">The game palette in video values, used to darken the colours.</param>
    /// <returns><see langword="false"/> once the fade is over.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:89-117</c> 的循环体。</remarks>
    public bool TryAdvance(Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (IsFinished)
        {
            return false;
        }

        byte fadeAlpha = (byte)Math.Round(_alpha, MidpointRounding.AwayFromZero);

        if (fadeAlpha == FullBrightAlpha)
        {
            // A fully bright frame needs no blending at all.
            ImageOps.Blit(_display, new Point(Roi.X, Roi.Y), _saved, _saved.Bounds);
        }
        else if (fadeAlpha == FullDarkAlpha)
        {
            // Index 0 is black in every game palette, including the video ones.
            ImageOps.FillRect(_display, Roi, 0);
        }
        else
        {
            ImageOps.ApplyAlpha(_saved, _saved.Bounds, _display, new Point(Roi.X, Roi.Y), fadeAlpha, gamePalette);
        }

        _alpha -= _alphaStep;
        ++_frameNumber;

        return true;
    }

    /// <summary>Reference <c>fadeDisplay()</c> bails out on degenerate input instead of fading.</summary>
    private static DisplayFade? Create(
        PixelBuffer display, byte startAlpha, byte endAlpha, in Rect roi, uint fadeTimeMs, uint frameCount)
    {
        if (frameCount < 2 || roi.Height <= 0 || roi.Width <= 0)
        {
            return null;
        }

        Rect clipped = roi.Intersect(display.Bounds);
        if (clipped.IsEmpty)
        {
            return null;
        }

        return new DisplayFade(display, startAlpha, endAlpha, clipped, fadeTimeMs, frameCount);
    }
}
