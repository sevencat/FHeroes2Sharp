using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The settings door of the main menu street scene: hovering it lights it up, which is how the
/// original engine points at the settings entrance of <c>Game::MainMenu()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The door itself is painted into <c>HEROES.ICN</c>; the light comes from sprite 18 of
/// <c>SHNGANIM.ICN</c>, the table that also animates the lantern glow. The reference blits only the
/// rows below the lantern anchor (<c>doorOffsetY = 55 * scale + offsetY</c>), so
/// <see cref="SourceArea"/> starts <see cref="SourceOffsetY"/> rows down and the light covers the
/// lower part of the door. The sprite is recoloured with <see cref="PaletteId.NoCycle"/>
/// (<c>fheroes2::ApplyPalette( highlightDoor, 8 )</c>) because the glow frames are drawn with
/// cycling palette entries while the door light is meant to stand still.
/// </para>
/// <para>
/// The hover area is <c>settingsArea</c> of the C++ code (<c>63,202</c> at the logical resolution)
/// rather than the lit pixels: the cursor lights the door as soon as it is over the recess, not only
/// over the bright part. The reference redraws the highlight once per lantern tick (250 ms); this
/// port draws it on every frame, because the scene is recomposed from the background anyway, and
/// keeping it out of the animation clock is what makes <c>--freeze-animation</c> screenshots of the
/// lit door deterministic.
/// </para>
/// </remarks>
public sealed class DoorHighlight
{
    /// <summary>Sprite of <c>SHNGANIM.ICN</c> that lights the door (<c>Assets::getImage( ICN::SHNGANIM, 18 )</c>).</summary>
    public const int HighlightFrame = 18;

    /// <summary>Rows of the sprite above the door, <c>55 * scale</c> in <c>Game::MainMenu()</c>.</summary>
    public const int SourceOffsetY = 55;

    /// <summary>Hover area of the door, the <c>settingsArea</c> of <c>Game::MainMenu()</c>.</summary>
    public static readonly Rect HoverArea = new(63, 202, 108, 160);

    private readonly Sprite _highlight;
    private readonly Rect _source;

    /// <summary>
    /// Wraps the sprite that lights the door.
    /// </summary>
    /// <param name="highlight">
    /// Sprite 18 of <c>SHNGANIM.ICN</c> with its palette already applied, see
    /// <see cref="CreateHighlight"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="highlight"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="highlight"/> is not taller than <see cref="SourceOffsetY"/>, so there would be
    /// no rows left below the lantern to light the door with.
    /// </exception>
    public DoorHighlight(Sprite highlight)
    {
        ArgumentNullException.ThrowIfNull(highlight);

        if (highlight.Height <= SourceOffsetY)
        {
            throw new ArgumentException(
                $"The door highlight must be taller than {SourceOffsetY} rows but is " +
                $"{highlight.Width}x{highlight.Height}.",
                nameof(highlight));
        }

        _highlight = highlight;
        _source = new Rect(0, SourceOffsetY, highlight.Width, highlight.Height - SourceOffsetY);
    }

    /// <summary>The sprite that is drawn while the cursor is over the door.</summary>
    public Sprite Highlight => _highlight;

    /// <summary>
    /// The part of <see cref="Highlight"/> that is blitted, in sprite local coordinates: everything
    /// below <see cref="SourceOffsetY"/>, because the rows above it belong to the lantern glow.
    /// </summary>
    public Rect SourceArea => _source;

    /// <summary>The screen area the light covers, i.e. <see cref="SourceArea"/> placed at the sprite anchor.</summary>
    public Rect Area => new(
        _highlight.OffsetX, _highlight.OffsetY + _source.Y, _source.Width, _source.Height);

    /// <summary>Checks whether <paramref name="cursor"/> is inside <see cref="HoverArea"/>.</summary>
    public bool IsHovered(Point? cursor) => cursor is { } position && HoverArea.Contains(position);

    /// <summary>
    /// Lights the door when <paramref name="cursor"/> is over it.
    /// </summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <param name="cursor">Cursor in logical pixels, or <see langword="null"/> when it left the window.</param>
    /// <returns><see langword="true"/> when the door was lit, so a caller can tell the two cases apart.</returns>
    public bool Draw(PixelBuffer target, Point? cursor)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!IsHovered(cursor))
        {
            return false;
        }

        // Transform layer aware, like every other sprite blit: the glow is dithered rather than
        // opaque, so a plain copy would paint its "holes" black.
        ImageOps.BlitSprite(
            target, new Point(_highlight.OffsetX, _highlight.OffsetY), _highlight, _source);

        return true;
    }

    /// <summary>
    /// Prepares a freshly read sprite for the door: the equivalent of
    /// <c>fheroes2::ApplyPalette( highlightDoor, 8 )</c>, applied to a copy so the caller's sprite
    /// keeps its own colours (the animation still needs the original, cycling one).
    /// </summary>
    /// <param name="sprite">Sprite 18 of <c>SHNGANIM.ICN</c> as read from the ICN file.</param>
    /// <returns>A copy without cycling colours.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sprite"/> is <see langword="null"/>.</exception>
    public static Sprite CreateHighlight(Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        Sprite highlight = sprite.Clone();
        ImageOps.ApplyPalette(highlight.Image, PaletteId.NoCycle);

        return highlight;
    }
}
