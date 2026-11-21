using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The logical 640x480 indexed frame buffer. Only the dirty region is re-uploaded to the GPU
/// by the presenter, which keeps the CPU side palette conversion cheap.
/// </summary>
public sealed class ScreenBuffer
{
    /// <summary>Logical width of the original game, in game pixels.</summary>
    public const int DefaultWidth = 640;

    /// <summary>Logical height of the original game, in game pixels.</summary>
    public const int DefaultHeight = 480;

    public static readonly Size DefaultSize = new(DefaultWidth, DefaultHeight);

    public ScreenBuffer(Size size, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height, nameof(size));

        Size = size;
        Palette = palette;
        Pixels = new PixelBuffer(size.Width, size.Height);
        DirtyRegion = Pixels.Bounds;
    }

    public Size Size { get; }

    public PixelBuffer Pixels { get; }

    /// <summary>Palette the indices of <see cref="Pixels"/> are drawn with.</summary>
    /// <remarks>Changed through <see cref="ChangePalette"/>, never by the drawing code.</remarks>
    public Palette Palette { get; private set; }

    /// <summary>
    /// Swaps the palette the frame is shown with, a port of <c>Display::changePalette()</c>
    /// (<c>src/engine/screen.cpp:1622-1630</c>).
    /// </summary>
    /// <param name="palette">Palette the same indices are drawn with from now on.</param>
    /// <remarks>
    /// <para>
    /// 参考换掉调色板后让渲染引擎刷新 GPU 的调色板，整屏颜色立刻变；端口在上传时把索引落成 RGBA，
    /// 已经传上去的像素不会自己变色，所以换完调色板必须整屏重传——<see cref="InvalidateAll"/> 就是这件事。
    /// </para>
    /// <para>
    /// 参考在 <c>currentRGBPalette == palette</c> 时直接返回（指针比较），端口按引用比较，语义相同。
    /// 参考还有一条「传 <c>nullptr</c> 且没要求强制刷新、当前已是游戏调色板」的早退，端口由调用方
    /// 显式传入游戏调色板来表达，不再有 <c>nullptr</c> 这条路径。
    /// </para>
    /// </remarks>
    public void ChangePalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        if (ReferenceEquals(Palette, palette))
        {
            return;
        }

        Palette = palette;
        InvalidateAll();
    }

    public Rect Bounds => Pixels.Bounds;

    /// <summary>Union of every rectangle touched since the last <see cref="TakeDirtyRegion"/> call.</summary>
    public Rect DirtyRegion { get; private set; }

    public bool HasDirtyRegion => !DirtyRegion.IsEmpty;

    public void MarkDirty(in Rect rect)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        Rect clipped = rect.Intersect(Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        DirtyRegion = Rect.Union(DirtyRegion, clipped);
    }

    public void InvalidateAll() => DirtyRegion = Bounds;

    /// <summary>Returns the dirty region accumulated so far and resets it.</summary>
    public Rect TakeDirtyRegion()
    {
        Rect region = DirtyRegion;
        DirtyRegion = Rect.Empty;
        return region;
    }
}
