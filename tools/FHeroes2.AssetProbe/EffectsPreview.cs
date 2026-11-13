using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.AssetProbe;

/// <summary>
/// Renders the image primitives the dialogs are built from on synthetic input so that the port can
/// be compared with the C++ implementation.
/// </summary>
/// <remarks>
/// <para>
/// <c>tools/reference/fh2_reference.exe effects</c> draws exactly the same cases with the fheroes2
/// engine and writes files with the same names, so a SHA-256 comparison covers every pixel of
/// <see cref="ImageOps.CreateDitheringTransition"/>, <see cref="ImageOps.AddGradientShadow"/>,
/// <see cref="ImageOps.AddGradientShadowForArea"/> and <see cref="ImageOps.ApplyTransform"/>.
/// <c>tools/scripts/CompareEffectsWithReference.ps1</c> runs both sides.
/// </para>
/// <para>
/// All input values come from formulas, never from the archives: the two implementations share no
/// data, only the arithmetic. Keep the formulas in sync with <c>fh2_reference.cpp</c>.
/// </para>
/// </remarks>
internal static class EffectsPreview
{
    private const int CanvasWidth = 96;

    private const int CanvasHeight = 64;

    private const int SourceWidth = 37;

    private const int SourceHeight = 21;

    /// <summary>Renders one canvas per case, in the order the reference writes them.</summary>
    public static IReadOnlyList<(string Name, PixelBuffer Image)> Render()
    {
        Sprite source = CreateSource();

        return
        [
            ("01-transform.bmp", TransformCase()),
            ("02-shadow-sprite.bmp", SpriteShadowCase(source)),
            ("03-shadow-area.bmp", AreaShadowCase()),
            ("04-transition-vertical.bmp", TransitionCase(source, new Point(0, 0), new Point(20, 20), new Size(20, 16), true, false)),
            ("05-transition-vertical-odd-reverse.bmp", TransitionCase(source, new Point(2, 3), new Point(24, 22), new Size(21, 17), true, true)),
            ("06-transition-horizontal.bmp", TransitionCase(source, new Point(4, 5), new Point(18, 14), new Size(24, 18), false, false)),
            ("07-transition-horizontal-odd-reverse.bmp", TransitionCase(source, new Point(6, 7), new Point(22, 16), new Size(25, 19), false, true)),
            ("08-transition-clipped.bmp", TransitionCase(source, new Point(0, 0), new Point(80, 50), new Size(40, 20), true, false)),
        ];
    }

    /// <summary>An indexed surface without a transform layer, i.e. what the engine renders into.</summary>
    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(CanvasWidth, CanvasHeight);

        for (int y = 0; y < CanvasHeight; ++y)
        {
            for (int x = 0; x < CanvasWidth; ++x)
            {
                canvas[x, y] = (byte)((x * 11) + (y * 17) + 40);
            }
        }

        return canvas;
    }

    /// <summary>A sprite with a transform layer, every fifth diagonal pixel being transparent.</summary>
    private static Sprite CreateSource()
    {
        PixelBuffer image = new(SourceWidth, SourceHeight);
        byte[] transform = new byte[SourceWidth * SourceHeight];

        for (int y = 0; y < SourceHeight; ++y)
        {
            for (int x = 0; x < SourceWidth; ++x)
            {
                int offset = (y * SourceWidth) + x;
                image.Pixels[offset] = (byte)((x * 29) + (y * 7) + 3);
                transform[offset] = (byte)(((x + y) % 5 == 0) ? 1 : 0);
            }
        }

        return new Sprite(image, 0, 0, transform);
    }

    private static PixelBuffer TransformCase()
    {
        PixelBuffer canvas = CreateCanvas();
        ImageOps.ApplyTransform(canvas, 8, 6, 40, 30, 3);

        return canvas;
    }

    private static PixelBuffer SpriteShadowCase(Sprite source)
    {
        PixelBuffer canvas = CreateCanvas();
        ImageOps.AddGradientShadow(source, canvas, new Point(20, 18), new Point(-5, 5));

        return canvas;
    }

    private static PixelBuffer AreaShadowCase()
    {
        PixelBuffer canvas = CreateCanvas();
        ImageOps.AddGradientShadowForArea(canvas, new Point(30, 10), 50, 36, 16);

        return canvas;
    }

    private static PixelBuffer TransitionCase(Sprite source, Point sourcePosition, Point targetPosition, Size size,
        bool vertical, bool reverse)
    {
        PixelBuffer canvas = CreateCanvas();
        ImageOps.CreateDitheringTransition(source, sourcePosition, canvas, targetPosition, size, vertical, reverse);

        return canvas;
    }
}
