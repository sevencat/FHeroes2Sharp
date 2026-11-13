using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Converts the indexed <see cref="ScreenBuffer"/> into the packed 32 bit image the platform back
/// end streams into a texture.
/// </summary>
/// <remarks>
/// Pixels are packed with <see cref="RgbaColor.ToPackedAbgr"/>, whose in-memory byte order is
/// R, G, B, A — exactly what <c>SDL_PIXELFORMAT_ABGR8888</c> expects. A 256 entry lookup table is
/// built once per palette so that each frame only has to walk the dirty rectangle.
/// </remarks>
public static class IndexedFrameConverter
{
    /// <summary>Size of one converted pixel in bytes.</summary>
    public const int BytesPerPixel = 4;

    /// <summary>Builds the 256 entry color lookup table of <paramref name="palette"/>.</summary>
    public static uint[] BuildLookup(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        uint[] lookup = new uint[Palette.ColorCount];
        for (int index = 0; index < Palette.ColorCount; ++index)
        {
            lookup[index] = palette[index].ToPackedAbgr();
        }

        return lookup;
    }

    /// <summary>
    /// Converts <paramref name="region"/> of <paramref name="frame"/> into
    /// <paramref name="destination"/>: row major, no padding, one 32 bit pixel per source pixel.
    /// Parts of the region outside the frame are skipped.
    /// </summary>
    /// <returns>The part of <paramref name="region"/> that was converted.</returns>
    /// <exception cref="ArgumentException">
    /// The lookup table has fewer than <see cref="Palette.ColorCount"/> entries or the destination
    /// is smaller than the clipped region.
    /// </exception>
    public static Rect ConvertRegion(ScreenBuffer frame, in Rect region, ReadOnlySpan<uint> lookup, Span<uint> destination)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (lookup.Length < Palette.ColorCount)
        {
            throw new ArgumentException(
                $"The color lookup needs {Palette.ColorCount} entries but only {lookup.Length} were provided.",
                nameof(lookup));
        }

        Rect clipped = region.Intersect(frame.Bounds);
        if (clipped.IsEmpty)
        {
            return Rect.Empty;
        }

        if (destination.Length < clipped.Area)
        {
            throw new ArgumentException(
                $"Converting {clipped.Width}x{clipped.Height} needs {clipped.Area} pixels but the destination holds {destination.Length}.",
                nameof(destination));
        }

        PixelBuffer pixels = frame.Pixels;
        int target = 0;

        for (int y = clipped.Y; y < clipped.Bottom; ++y)
        {
            ReadOnlySpan<byte> row = pixels.Row(y);
            for (int x = clipped.X; x < clipped.Right; ++x)
            {
                destination[target++] = lookup[row[x]];
            }
        }

        return clipped;
    }
}
