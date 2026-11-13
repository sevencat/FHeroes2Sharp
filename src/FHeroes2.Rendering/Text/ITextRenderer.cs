namespace FHeroes2.Rendering.Text;

/// <summary>
/// Backend independent text measurement/layout service.
///
/// The M0 renderer is FontStashSharp based (TrueType). The original bitmap fonts
/// (<c>BIGFONT.ICN</c>/<c>SMALFONT.ICN</c>) will be provided by a second implementation in a
/// later milestone so the UI layer never depends on a concrete backend.
/// </summary>
public interface ITextRenderer : IDisposable
{
    /// <summary>Human readable backend name, used by the diagnostics overlay.</summary>
    string Name { get; }

    /// <summary>Measures a single line of text.</summary>
    TextMetrics Measure(string text, TextStyle style);
}
