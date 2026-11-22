using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The small portrait of a hero, <c>Heroes::PortraitRedraw( x, y, PORT_SMALL, display )</c> — the frame of
/// <c>PORTXTRA</c>, the <c>MINIPORT</c> face with the mobility bar on its left and the mana bar on its right.
/// </summary>
/// <remarks>
/// Two screens need the same 30x30 picture: the adventure map's hero icons and the kingdom overview's hero list. The
/// reference calls one <c>Heroes</c> member from both, so the port keeps one implementation as well; the icon list's
/// own drawing delegates here.
/// </remarks>
public static class HeroPortraitPainter
{
    /// <summary>Width of the bars on both sides of the face, <c>barWidth</c>.</summary>
    public const int BarWidth = 7;

    /// <summary>Draws the small portrait of a hero, <c>Heroes::PortraitRedraw()</c> with <c>PORT_SMALL</c>.</summary>
    /// <param name="hero">The hero to draw.</param>
    /// <param name="sprites">Where <c>PORTXTRA</c>, <c>MINIPORT</c>, <c>MOBILITY</c> and <c>MANA</c> come from.</param>
    /// <param name="world">The world the mobility is read from.</param>
    /// <param name="position">The top left corner of the portrait.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hero"/>, <paramref name="sprites"/>, <paramref name="world"/> or <paramref name="output"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static void DrawSmall(Hero hero, IViewWorldSpriteSource sprites, World world, Point position,
        PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(output);

        if (sprites.GetSprite(IcnType.Portxtra, 0) is { } background)
        {
            ImageOps.BlitSprite(output, position, background);
        }

        if (sprites.GetSprite(IcnType.Miniport, hero.PortraitId - 1) is not { } portrait)
        {
            return;
        }

        uint mobilityIndex = hero.GetMobilityIndexSprite(world);

        if (mobilityIndex > 0 && sprites.GetSprite(IcnType.Mobility, (int)mobilityIndex) is { } mobility)
        {
            ImageOps.Blit(output, new Point(position.X, position.Y + mobility.OffsetY), mobility.Image,
                new Rect(0, 0, mobility.Width, mobility.Height));
        }

        ImageOps.Blit(output, new Point(position.X + BarWidth + 1, position.Y), portrait.Image,
            new Rect(0, 0, portrait.Width, portrait.Height));

        uint manaIndex = hero.GetManaIndexSprite();

        if (manaIndex > 0 && sprites.GetSprite(IcnType.Mana, (int)manaIndex) is { } mana)
        {
            ImageOps.Blit(output, new Point(position.X + BarWidth + portrait.Width + 2, position.Y + mana.OffsetY),
                mana.Image, new Rect(0, 0, mana.Width, mana.Height));
        }

        // The "Z" of a sleeping hero, blitted over a patch that first wipes whatever the portrait left there.
        if (!hero.IsControlHuman || !hero.Modes.HasFlag(HeroModes.Sleeper))
        {
            return;
        }

        if (sprites.GetSprite(IcnType.Misc4, 14) is not { } sleeper)
        {
            return;
        }

        Sprite patch = new(sleeper.Width - 4, sleeper.Height - 4);
        ImageOps.Blit(output, new Point(position.X + 35 + 3, position.Y - 1), patch.Image,
            new Rect(0, 0, patch.Width, patch.Height));
        ImageOps.BlitSprite(output, new Point(position.X + 35 + 1, position.Y - 3), sleeper);
    }
}
