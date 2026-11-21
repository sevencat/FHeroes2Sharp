using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Tints everything around a window, a port of <c>InvertedShadow()</c>
/// (<c>src/fheroes2/gui/ui_tool.cpp:750-764</c>).
/// </summary>
/// <remarks>
/// 参考把 <c>roi</c> 挖掉 <c>excludedRoi</c> 之后剩下的四块（上、下、左、右）分别按调色板表换色，
/// 换 <c>paletteCount</c> 遍。<c>InvertedFadeWithPalette()</c> 每帧调一次（<c>paletteCount</c> 为 1），
/// 于是窗口周围一点点变暗。<c>bottom</c> 与 <c>right</c> 的高度用「<c>roi</c> 的右下角减去 <c>excludedRoi</c> 的右下角」算，
/// 与参考一致，负数或零时跳过（参考的 <c>ApplyPalette</c> 会自行校验后返回）。
/// </remarks>
public static class InvertedShadow
{
    /// <summary>Applies the palette of <paramref name="paletteId"/> around <paramref name="excludedRoi"/>.</summary>
    /// <param name="image">Frame to tint in place.</param>
    /// <param name="roi">Whole region the shadow covers.</param>
    /// <param name="excludedRoi">Region left untouched, usually the window itself.</param>
    /// <param name="paletteId">Palette transform applied to the surrounding region.</param>
    /// <param name="paletteCount">How many times the transform is applied.</param>
    public static void Apply(
        PixelBuffer image, in Rect roi, in Rect excludedRoi, PaletteId paletteId, int paletteCount)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (paletteCount <= 0)
        {
            return;
        }

        Rect top = new(roi.X, roi.Y, roi.Width, excludedRoi.Y - roi.Y);
        Rect bottom = new(
            roi.X,
            excludedRoi.Y + excludedRoi.Height,
            roi.Width,
            roi.Y + roi.Height - excludedRoi.Y - excludedRoi.Height);
        Rect left = new(roi.X, excludedRoi.Y, excludedRoi.X - roi.X, excludedRoi.Height);
        Rect right = new(
            excludedRoi.X + excludedRoi.Width,
            excludedRoi.Y,
            roi.X + roi.Width - excludedRoi.X - excludedRoi.Width,
            excludedRoi.Height);

        ReadOnlySpan<byte> table = PaletteTable.Get(paletteId);

        for (int i = 0; i < paletteCount; ++i)
        {
            ApplyPart(image, top, table);
            ApplyPart(image, bottom, table);
            ApplyPart(image, left, table);
            ApplyPart(image, right, table);
        }
    }

    private static void ApplyPart(PixelBuffer image, in Rect part, ReadOnlySpan<byte> table)
    {
        if (part.Width <= 0 || part.Height <= 0)
        {
            return;
        }

        ImageOps.ApplyPalette(image, part, table);
    }
}

/// <summary>
/// Shades everything around a window step by step, a port of <c>InvertedFadeWithPalette()</c>
/// (<c>src/fheroes2/gui/ui_tool.cpp:726-748</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考每帧调一次 <see cref="InvertedShadow.Apply"/>（<c>paletteCount</c> 为 1）并重绘 <c>roi</c>，
/// 一共 <c>frameCount</c> 帧，每帧 <c>fadeTimeMs / frameCount</c> 毫秒，
/// 于是窗口之外一层层变暗。
/// </para>
/// <para>
/// 差异：参考在函数内部跑事件循环，端口把逐帧的时机交给宿主，见 <see cref="TryAdvance"/>。
/// </para>
/// </remarks>
public sealed class InvertedFade
{
    private readonly PixelBuffer _image;
    private readonly PaletteId _paletteId;
    private int _frameNumber;

    /// <summary>Prepares the fade, the part of <c>InvertedFadeWithPalette()</c> before its loop.</summary>
    /// <param name="image">Frame that is shaded in place.</param>
    /// <param name="roi">Whole region the shade covers.</param>
    /// <param name="excludedRoi">Region left untouched, usually the window itself.</param>
    /// <param name="paletteId">Palette transform applied around the window every frame.</param>
    /// <param name="fadeTimeMs">Length of the fade in milliseconds.</param>
    /// <param name="frameCount">Number of frames the fade renders.</param>
    /// <remarks>参考：<c>ui_tool.cpp:726-734</c>。</remarks>
    public InvertedFade(
        PixelBuffer image,
        in Rect roi,
        in Rect excludedRoi,
        PaletteId paletteId,
        int fadeTimeMs,
        int frameCount)
    {
        ArgumentNullException.ThrowIfNull(image);

        _image = image;
        Roi = roi;
        ExcludedRoi = excludedRoi;
        _paletteId = paletteId;
        FrameCount = Math.Max(frameCount, 0);
        DelayMs = FrameCount == 0 ? 0 : fadeTimeMs / FrameCount;
    }

    /// <summary>Whole region the shade covers.</summary>
    public Rect Roi { get; }

    /// <summary>Region left untouched.</summary>
    public Rect ExcludedRoi { get; }

    /// <summary>Number of frames the fade renders.</summary>
    public int FrameCount { get; }

    /// <summary>Delay between two frames in milliseconds, <c>fadeTimeMs / frameCount</c>.</summary>
    public int DelayMs { get; }

    /// <summary>Whether every frame has been rendered.</summary>
    public bool IsFinished => _frameNumber >= FrameCount;

    /// <summary>Shades the region one step further.</summary>
    /// <returns><see langword="false"/> once the fade is over.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:736-746</c> 的循环体。</remarks>
    public bool TryAdvance()
    {
        if (IsFinished)
        {
            return false;
        }

        InvertedShadow.Apply(_image, Roi, ExcludedRoi, _paletteId, 1);
        ++_frameNumber;

        return true;
    }
}
