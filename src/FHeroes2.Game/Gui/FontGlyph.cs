using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The two sprite tricks the font generators use over and over: <c>resize()</c> plus <c>reset()</c>, and
/// <c>setPosition()</c>.
/// </summary>
/// <remarks>
/// C# 的 <see cref="Sprite"/> 尺寸不可变，所以参考里「就地放大再重置」的两步在这里合成一个新建。
/// 两处都要留意：构造函数**不会**把变换层置 1，必须 <see cref="Sprite.Reset"/>，否则整张图都是可见的黑色；
/// 而 <c>resize</c> 不碰锚点，新图要沿用被替换那张的锚点，参考的 <c>setPosition</c> 才是真正的移动。
/// </remarks>
internal static class FontGlyph
{
    /// <summary>Semi transparent (shadow) transform id the reference passes around as <c>2</c>.</summary>
    public const byte ShadowTransformId = 2;

    /// <summary><c>updateNormalFontLetterShadow</c>: <c>updateShadow( letter, { -1, 2 }, 2, false )</c>.</summary>
    /// <param name="glyph">Letter to shade; <see langword="null"/> is ignored.</param>
    public static void NormalShadow(Sprite? glyph)
    {
        if (glyph is not null)
        {
            ImageOps.UpdateShadow(glyph, new Point(-1, 2), ShadowTransformId, false);
        }
    }

    /// <summary><c>updateSmallFontLetterShadow</c>: <c>updateShadow( letter, { -1, 1 }, 2, true )</c>.</summary>
    /// <param name="glyph">Letter to shade; <see langword="null"/> is ignored.</param>
    public static void SmallShadow(Sprite? glyph)
    {
        if (glyph is not null)
        {
            ImageOps.UpdateShadow(glyph, new Point(-1, 1), ShadowTransformId, true);
        }
    }

    /// <summary>A fresh transparent sprite sized <paramref name="width"/> × <paramref name="height"/>.</summary>
    /// <param name="previous">The sprite being replaced; its anchor is kept, like <c>resize()</c> does.</param>
    /// <param name="width">New width.</param>
    /// <param name="height">New height.</param>
    /// <returns>The new sprite.</returns>
    public static Sprite Create(Sprite? previous, int width, int height)
    {
        Sprite glyph = previous is { } anchor
            ? new Sprite(width, height, anchor.OffsetX, anchor.OffsetY)
            : new Sprite(width, height);

        glyph.Reset();

        return glyph;
    }

    /// <summary>Same content, different anchor, the reference's <c>setPosition</c>.</summary>
    /// <param name="glyph">Sprite to move.</param>
    /// <param name="offsetX">New horizontal anchor.</param>
    /// <param name="offsetY">New vertical anchor.</param>
    /// <returns>A new sprite sharing the image and the transform layer.</returns>
    public static Sprite Moved(Sprite glyph, int offsetX, int offsetY) =>
        new(glyph.Image, offsetX, offsetY, glyph.Transform);
}
