using FHeroes2.Core.Geometry;

namespace FHeroes2.Rendering.Text;

/// <summary>Result of measuring a text run.</summary>
/// <param name="Bounds">Width and height of the laid out text, in logical pixels.</param>
/// <param name="LineHeight">Distance between two consecutive baselines, in logical pixels.</param>
/// <param name="GlyphCount">Number of glyphs that were measured.</param>
public readonly record struct TextMetrics(Size Bounds, int LineHeight, int GlyphCount)
{
    public static readonly TextMetrics Empty = new(Size.Empty, 0, 0);
}
