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

    public Palette Palette { get; }

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
