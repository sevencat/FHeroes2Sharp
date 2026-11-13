namespace FHeroes2.Core.Imaging;

/// <summary>
/// Straight (non-premultiplied) 8 bit per channel color.
/// The game keeps its own type so that decoding stays independent from the renderer.
/// </summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly RgbaColor Transparent = new(0, 0, 0, 0);

    public static readonly RgbaColor Black = new(0, 0, 0, 255);

    public static readonly RgbaColor White = new(255, 255, 255, 255);

    public bool IsTransparent => A == 0;

    public static RgbaColor FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);

    /// <summary>
    /// Packs the color as <c>0xAABBGGRR</c>, whose in-memory byte order is R, G, B, A — the layout
    /// that <c>SDL_PIXELFORMAT_ABGR8888</c> expects.
    /// </summary>
    public uint ToPackedAbgr() => ((uint)A << 24) | ((uint)B << 16) | ((uint)G << 8) | R;

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
