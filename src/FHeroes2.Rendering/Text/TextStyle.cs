using FHeroes2.Core.Imaging;

namespace FHeroes2.Rendering.Text;

/// <summary>Requested appearance of a text run.</summary>
/// <param name="PixelSize">Em size in logical (game) pixels.</param>
/// <param name="Color">Foreground color; <see langword="null"/> means white.</param>
/// <param name="Shadow">Whether a one pixel drop shadow is drawn (the classic HoMM II look).</param>
public readonly record struct TextStyle(int PixelSize, RgbaColor? Color = null, bool Shadow = true)
{
    public RgbaColor EffectiveColor => Color ?? RgbaColor.White;
}
