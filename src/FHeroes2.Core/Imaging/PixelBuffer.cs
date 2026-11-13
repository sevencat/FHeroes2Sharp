using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Dense 8 bit per pixel image, the equivalent of <c>fheroes2::Image</c> in its
/// non-transformed form. Row stride equals the width, matching the reference engine.
/// </summary>
public sealed class PixelBuffer
{
    public PixelBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Width = width;
        Height = height;
        Stride = width;
        Pixels = new byte[width * height];
    }

    public PixelBuffer(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        if (pixels.Length < width * height)
        {
            throw new ArgumentException(
                $"Pixel array of {pixels.Length} bytes is too small for {width}x{height}.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Stride = width;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    /// <summary>Raw indexed pixels in row major order.</summary>
    public byte[] Pixels { get; }

    public Size Size => new(Width, Height);

    public Rect Bounds => new(0, 0, Width, Height);

    public bool IsEmpty => Width == 0 || Height == 0;

    public byte this[int x, int y]
    {
        get => Pixels[(y * Stride) + x];
        set => Pixels[(y * Stride) + x] = value;
    }

    public Span<byte> AsSpan() => Pixels.AsSpan(0, Height * Stride);

    public Span<byte> Row(int y) => Pixels.AsSpan(y * Stride, Width);

    public void Fill(byte value) => AsSpan().Fill(value);

    public void Clear() => Fill(0);

    public PixelBuffer Clone()
    {
        byte[] copy = new byte[Pixels.Length];
        Pixels.AsSpan(0, Height * Stride).CopyTo(copy);
        return new PixelBuffer(Width, Height, copy);
    }
}
