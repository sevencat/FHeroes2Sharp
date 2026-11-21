using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.AssetProbe;

/// <summary>
/// Draws <see cref="StandardWindow"/> with the artwork of the archive so that the port can be
/// compared with the C++ implementation.
/// </summary>
/// <remarks>
/// <para>
/// <c>tools/reference/fh2_reference.exe window</c> runs the coordinate arithmetic of
/// <c>ui_window.cpp</c> on the same sprites and writes files with the same names, so a
/// SHA-256 comparison covers every pixel of the frame: the corners, the repeated borders, the
/// dithered seams, the tiled interior and the shadow.
/// <c>tools/scripts/CompareWindowWithReference.ps1</c> runs both sides.
/// </para>
/// <para>
/// <see cref="StandardWindow"/> reads <c>SURDRBKG.ICN</c>, <c>WINLOSE.ICN</c> and
/// <c>STONEBAK.ICN</c> itself through <c>GameAssets.Current</c>, so the caller must open the game
/// data and set it before rendering.
/// </para>
/// <para>
/// The canvas fill is the formula of <see cref="EffectsPreview"/>, so the pixels a shadow darkens
/// are the same on both sides. Keep the cases in sync with <c>fh2_reference.cpp</c>.
/// </para>
/// </remarks>
internal static class WindowPreview
{
    private const int CanvasWidth = 640;

    private const int CanvasHeight = 480;

    /// <summary>One dialog: the area the caller asks for and whether the interior is tiled.</summary>
    private static readonly (string Name, Rect ActiveArea, bool Background)[] Cases =
    [
        ("01-window.bmp", new Rect(170, 140, 300, 200), true),
        ("02-window-odd.bmp", new Rect(195, 161, 251, 157), true),
        ("03-window-plain.bmp", new Rect(170, 140, 300, 200), false),
        ("04-window-wide.bmp", new Rect(64, 40, 520, 400), true),
    ];

    /// <summary>Renders one canvas per case, in the order the reference writes them.</summary>
    /// <exception cref="InvalidOperationException">The archive has no window artwork.</exception>
    public static IReadOnlyList<(string Name, PixelBuffer Image)> Render()
    {
        List<(string, PixelBuffer)> images = new(Cases.Length);

        foreach ((string name, Rect activeArea, bool background) in Cases)
        {
            PixelBuffer canvas = CreateCanvas();
            StandardWindow.Create(activeArea, background).Draw(canvas);
            images.Add((name, canvas));
        }

        return images;
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
}
