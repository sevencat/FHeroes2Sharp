using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The 46x46 icon of a castle on the mini map row of icons, <c>ICN::LOCATORS</c> at the frame the race and the built
/// castle gate pick.
/// </summary>
/// <remarks>
/// The adventure map's icon panel and the kingdom overview's town list show the same picture, so the drawing lives here
/// and the panel delegates. The reference has the same split: <c>Interface::redrawCastleIcon()</c> for the overview and
/// the panel's own code calling <c>fheroes2::AGG::GetICN</c> for the icons row.
/// </remarks>
public static class CastleIconPainter
{
    /// <summary>Draws the icon of a castle, <c>Interface::redrawCastleIcon()</c>.</summary>
    /// <param name="castle">The castle to draw.</param>
    /// <param name="sprites">Where <c>LOCATORS</c> (or <c>LOCATORE</c>) comes from.</param>
    /// <param name="position">Top left corner, the icon's own offset is added to it.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="evilInterface">Whether the black letterbox of <c>LOCATORE</c> is wanted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="castle"/>, <paramref name="sprites"/> or <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    public static void Draw(Castle castle, IViewWorldSpriteSource sprites, Point position, PixelBuffer output,
        bool evilInterface = false)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(output);

        if (IconBar.CastleFrame(castle.Race, castle.IsCastle) is not { } frame)
        {
            return;
        }

        if (sprites.GetSprite(evilInterface ? IcnType.Locatore : IcnType.Locators, frame) is not { } icon)
        {
            return;
        }

        ImageOps.BlitSprite(output, new Point(position.X + icon.OffsetX, position.Y + icon.OffsetY), icon);
    }
}
