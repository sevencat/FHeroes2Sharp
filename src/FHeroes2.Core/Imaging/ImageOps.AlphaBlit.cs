using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The semi transparent blit of the engine, <c>fheroes2::AlphaBlit()</c> (<c>src/engine/image.cpp:961-1085</c>).
/// </summary>
/// <remarks>
/// <para>
/// Unlike a normal blit this does not copy a palette index: it blends the two colours the indices stand for and then
/// picks the closest entry of the game palette, which is why a 50% blit of a 36x36 icon costs a palette search per
/// pixel. That is exactly what the faded buttons of the hidden interface want
/// (<c>interface_cpanel.cpp:75-86</c>).
/// </para>
/// <para>
/// Reference math, per channel: <c>( source * alpha + destination * ( 255 - alpha ) ) / 255</c>, in the six bit range
/// of <c>KB.PAL</c>, followed by <see cref="PALColorId"/>. A transform value of <c>1</c> skips the pixel, a value
/// above <c>1</c> recolours the <i>destination</i> through <see cref="PaletteTransformTable"/> before blending, and
/// <see cref="byte.MaxValue"/> degrades to a plain <see cref="ImageOps.BlitSprite(PixelBuffer, Point, Sprite)"/>.
/// </para>
/// </remarks>
public static partial class ImageOps
{
    /// <summary>
    /// Blends <paramref name="source"/> onto <paramref name="target"/> at the sprite's own offset,
    /// <c>AlphaBlit( in, out, outX = in.x(), outY = in.y(), alphaValue )</c>.
    /// </summary>
    /// <param name="source">Sprite to blend.</param>
    /// <param name="target">Frame buffer to blend into.</param>
    /// <param name="alpha">Opacity, <c>255</c> copies, <c>0</c> draws nothing.</param>
    /// <param name="gamePalette">The game palette in video values, <c>getRGBGamePalette()</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void AlphaBlit(Sprite source, PixelBuffer target, byte alpha, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(source);

        AlphaBlit(source, target, new Point(source.OffsetX, source.OffsetY), alpha, gamePalette);
    }

    /// <summary>
    /// Blends <paramref name="source"/> onto <paramref name="target"/> with the image's top left corner at
    /// <paramref name="targetPosition"/>, <c>AlphaBlit( in, out, outX, outY, alphaValue )</c>.
    /// </summary>
    /// <param name="source">Sprite to blend.</param>
    /// <param name="target">Frame buffer to blend into.</param>
    /// <param name="targetPosition">Where the image's top left corner goes.</param>
    /// <param name="alpha">Opacity, <c>255</c> copies, <c>0</c> draws nothing.</param>
    /// <param name="gamePalette">The game palette in video values, <c>getRGBGamePalette()</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void AlphaBlit(Sprite source, PixelBuffer target, Point targetPosition, byte alpha, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (alpha == 0)
        {
            // Nothing to blend, the reference returns before even looking at the rectangles.
            return;
        }

        if (alpha == byte.MaxValue)
        {
            BlitSprite(target, targetPosition, source);

            return;
        }

        Rect sourceRect = source.Image.Bounds;
        Rect destinationRect = new(targetPosition, sourceRect.Size);
        Rect clipped = destinationRect.Intersect(target.Bounds);

        if (clipped.IsEmpty)
        {
            return;
        }

        int skipX = clipped.X - destinationRect.X;
        int skipY = clipped.Y - destinationRect.Y;

        ReadOnlySpan<byte> palette = gamePalette.ToRaw();
        ReadOnlySpan<byte> image = source.Image.AsSpan();
        byte[]? transform = source.Transform;
        uint behind = (uint)(byte.MaxValue - alpha);

        for (int y = 0; y < clipped.Height; ++y)
        {
            int sourceRow = ((skipY + y) * source.Image.Stride) + skipX;
            Span<byte> destinationRow = target.Row(clipped.Y + y);

            for (int x = 0; x < clipped.Width; ++x)
            {
                int sourceIndex = sourceRow + x;
                int destinationIndex = clipped.X + x;
                byte transformId = transform is null ? (byte)0 : transform[sourceIndex];

                if (transformId == 1)
                {
                    // The transform layer marks the pixel as see through.
                    continue;
                }

                byte destinationColor = destinationRow[destinationIndex];
                byte sourceColor = image[sourceIndex];

                if (transformId > 1 && transformId < PaletteTransformTable.TransformCount)
                {
                    // The transform recolours what is already there and the result gets blended in.
                    sourceColor = PaletteTransformTable.Apply(transformId, destinationColor);
                }

                uint red = ((uint)(palette[sourceColor * 3] >> 2) * alpha) + ((uint)(palette[destinationColor * 3] >> 2) * behind);
                uint green = ((uint)(palette[(sourceColor * 3) + 1] >> 2) * alpha) + ((uint)(palette[(destinationColor * 3) + 1] >> 2) * behind);
                uint blue = ((uint)(palette[(sourceColor * 3) + 2] >> 2) * alpha) + ((uint)(palette[(destinationColor * 3) + 2] >> 2) * behind);

                destinationRow[destinationIndex] = PALColorId.Get(
                    (byte)(red / byte.MaxValue),
                    (byte)(green / byte.MaxValue),
                    (byte)(blue / byte.MaxValue),
                    palette);
            }
        }
    }
}
