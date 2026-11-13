using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Geometry and theme resolution for the software cursor, the port of <c>Cursor::SetThemes()</c>,
/// <c>Cursor::SetOffset()</c>, <c>Cursor::DistanceThemes()</c> and
/// <c>Cursor::WithoutDistanceThemes()</c> (<c>src/fheroes2/gui/cursor.cpp</c>).
/// </summary>
/// <remarks>
/// The original engine has no cursor file of its own: a theme number selects an image out of an
/// ICN set (or out of the monochrome <c>*.BMP</c> entries, see
/// <c>FHeroes2.Assets.Cursor.MonoCursorFile</c>) and the drawing offset is derived from the sprite
/// anchor, with hard coded exceptions for the scroll and sword cursors that would otherwise be
/// drawn far away from the hotspot.
/// </remarks>
public static class CursorLayout
{
    /// <summary>Highest distance the adventure map cursors have a variant for.</summary>
    public const int MaximumDistance = 8;

    /// <summary>Selects the image set a theme is drawn from, ignoring the distance variants.</summary>
    /// <param name="monochrome">Whether the monochrome cursor themes are enabled in the settings.</param>
    public static CursorImageSet ImageSet(CursorTheme theme, bool monochrome)
    {
        // The video pointer cannot be rendered properly in black and white, so it always uses the
        // color set - the same exception the reference engine makes.
        bool useMonochrome = monochrome && theme != CursorTheme.PointerVideo;

        return ((int)theme & 0xF000) switch
        {
            0x4000 => useMonochrome
                ? CursorImageSet.AdventureMapDistanceMonochrome
                : CursorImageSet.AdventureMapDistanceColor,
            0x3000 => useMonochrome ? CursorImageSet.SpellMonochrome : CursorImageSet.Spell,
            0x2000 => useMonochrome ? CursorImageSet.CombatMonochrome : CursorImageSet.Combat,
            _ => useMonochrome ? CursorImageSet.AdventureMapMonochrome : CursorImageSet.AdventureMap,
        };
    }

    /// <summary>Index of the sprite inside the set selected by <see cref="ImageSet"/>.</summary>
    public static int ImageIndex(CursorTheme theme) => (int)theme & 0xFF;

    /// <summary>
    /// The drawing offset implied by a sprite anchor, i.e.
    /// <c>{ (width - x) / 2, (height - y) / 2 }</c> in <c>Cursor::SetThemes()</c>.
    /// </summary>
    public static Point AnchorOffset(Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        return new Point(
            (sprite.Width - sprite.OffsetX) / 2,
            (sprite.Height - sprite.OffsetY) / 2);
    }

    /// <summary>
    /// Offset between the mouse position and the top left corner of the cursor image. The scroll
    /// and sword cursors are pinned to their arrow tip, every other theme centers on the anchor.
    /// </summary>
    public static Point Offset(CursorTheme theme, Point anchorOffset)
    {
        if (theme is CursorTheme.Pointer or CursorTheme.PointerVideo or CursorTheme.CombatPointer)
        {
            return new Point(0, 0);
        }

        if (theme >= CursorTheme.HeroFight && theme <= CursorTheme.HeroFight8)
        {
            return new Point(-10, -11);
        }

        return theme switch
        {
            CursorTheme.ScrollTopRight or CursorTheme.ScrollRight => new Point(-15, 0),
            CursorTheme.ScrollBottom or CursorTheme.ScrollBottomLeft => new Point(0, -15),
            CursorTheme.ScrollBottomRight or CursorTheme.SwordBottomRight => new Point(-20, -20),
            CursorTheme.SwordBottomLeft => new Point(-5, -20),
            CursorTheme.SwordTopLeft => new Point(-5, -5),
            CursorTheme.SwordTopRight => new Point(-20, -5),
            CursorTheme.SwordTop => new Point(-anchorOffset.X, -5),
            CursorTheme.SwordBottom => new Point(-anchorOffset.X, -25),
            CursorTheme.SwordLeft => new Point(-5, -7),
            CursorTheme.SwordRight => new Point(-25, -7),
            _ => new Point(-anchorOffset.X, -anchorOffset.Y),
        };
    }

    /// <summary>
    /// Picks the variant of a distance theme for <paramref name="distance"/> tiles, clamped to
    /// <see cref="MaximumDistance"/>; a distance of zero always shows the plain pointer.
    /// </summary>
    public static CursorTheme WithDistance(CursorTheme theme, uint distance)
    {
        if (distance == 0)
        {
            return CursorTheme.Pointer;
        }

        int clamped = (int)Math.Min(distance, MaximumDistance);

        bool hasVariants = theme is CursorTheme.HeroMove or CursorTheme.HeroFight or CursorTheme.HeroBoat
            or CursorTheme.HeroAnchor or CursorTheme.HeroMeet or CursorTheme.HeroAction
            or CursorTheme.HeroBoatAction;

        return hasVariants ? (CursorTheme)((int)theme + clamped - 1) : theme;
    }

    /// <summary>Drops the distance variant of a theme, e.g. <c>HeroBoat4</c> becomes <c>HeroBoat</c>.</summary>
    public static CursorTheme WithoutDistance(CursorTheme theme)
    {
        if (IsVariantOf(theme, CursorTheme.HeroMove, CursorTheme.HeroMove8))
        {
            return CursorTheme.HeroMove;
        }

        if (IsVariantOf(theme, CursorTheme.HeroFight, CursorTheme.HeroFight8))
        {
            return CursorTheme.HeroFight;
        }

        if (IsVariantOf(theme, CursorTheme.HeroBoat, CursorTheme.HeroBoat8))
        {
            return CursorTheme.HeroBoat;
        }

        if (IsVariantOf(theme, CursorTheme.HeroAnchor, CursorTheme.HeroAnchor8))
        {
            return CursorTheme.HeroAnchor;
        }

        if (IsVariantOf(theme, CursorTheme.HeroMeet, CursorTheme.HeroMeet8))
        {
            return CursorTheme.HeroMeet;
        }

        if (IsVariantOf(theme, CursorTheme.HeroAction, CursorTheme.HeroAction8))
        {
            return CursorTheme.HeroAction;
        }

        return IsVariantOf(theme, CursorTheme.HeroBoatAction, CursorTheme.HeroBoatAction8)
            ? CursorTheme.HeroBoatAction
            : theme;
    }

    /// <summary>
    /// Whether <paramref name="theme"/> is one of the distance variants of a theme. The base theme
    /// itself is not a variant, matching the <c>theme &gt; base &amp;&amp; theme &lt;= last</c> tests
    /// of the reference engine.
    /// </summary>
    private static bool IsVariantOf(CursorTheme theme, CursorTheme first, CursorTheme last) =>
        theme > first && theme <= last;
}
