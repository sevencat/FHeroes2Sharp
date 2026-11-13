namespace FHeroes2.Core.Imaging;

/// <summary>
/// A decoded, straight alpha 8 bit RGBA image. Produced by <see cref="PngReader"/> and consumed by
/// the golden image tests and the asset probe's <c>compare</c> command.
/// </summary>
public sealed class PngImage
{
    public PngImage(int width, int height, byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException(
                $"An RGBA buffer for {width}x{height} needs {width * height * 4} bytes but {rgba.Length} were provided.",
                nameof(rgba));
        }

        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Bytes per row, always <c>Width * 4</c>.</summary>
    public int Stride => Width * 4;

    /// <summary>Raw RGBA bytes in row major order.</summary>
    public byte[] Rgba { get; }

    public RgbaColor this[int x, int y]
    {
        get
        {
            int offset = (y * Stride) + (x * 4);
            return new RgbaColor(Rgba[offset], Rgba[offset + 1], Rgba[offset + 2], Rgba[offset + 3]);
        }
    }

    public Span<byte> Row(int y) => Rgba.AsSpan(y * Stride, Stride);

    /// <summary>Copies the pixels into an RGBA buffer with the same layout as <see cref="Rgba"/>.</summary>
    public byte[] ToRgba() => (byte[])Rgba.Clone();

    /// <summary>
    /// Converts the image back to a palette index buffer, which is only meaningful for images that
    /// were exported from indexed data. Returns the number of pixels that could not be mapped.
    /// </summary>
    public int ToIndices(Palette palette, PixelBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(destination);

        if (destination.Width != Width || destination.Height != Height)
        {
            throw new ArgumentException(
                $"The destination is {destination.Width}x{destination.Height} but the image is {Width}x{Height}.",
                nameof(destination));
        }

        Dictionary<RgbaColor, byte> lookup = new(Palette.ColorCount);
        for (int i = 0; i < Palette.ColorCount; ++i)
        {
            lookup[palette[i]] = (byte)i;
        }

        int unmapped = 0;
        for (int y = 0; y < Height; ++y)
        {
            for (int x = 0; x < Width; ++x)
            {
                RgbaColor color = this[x, y];
                if (color.A == 0)
                {
                    destination[x, y] = 0;
                    continue;
                }

                if (!lookup.TryGetValue(new RgbaColor(color.R, color.G, color.B), out byte index))
                {
                    index = 0;
                    ++unmapped;
                }

                destination[x, y] = index;
            }
        }

        return unmapped;
    }
}
