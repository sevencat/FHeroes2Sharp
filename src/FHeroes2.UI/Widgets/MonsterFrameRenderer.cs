using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The artwork <c>renderMonsterFrame()</c> needs, the two <c>Assets::getImage()</c> lookups of
/// <c>ui_monster.cpp:31-59</c>.
/// </summary>
/// <remarks>
/// The race plates are <c>ICN::STRIP</c> frames 4…10 (Knight, Barbarian, Sorceress, Warlock, Wizard, Necromancer and
/// the neutral/default one); the portrait is frame 0 of the creature's own ICN, <c>Monster::ICNMonh()</c>. Both come
/// from the owner, because this layer never reaches for the game data itself.
/// </remarks>
public interface IMonsterFrameSprites
{
    /// <summary>The plate behind a creature portrait, <c>STRIP</c> frames 4…10.</summary>
    /// <param name="race">The creature's race.</param>
    /// <returns>The sprite, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetRaceFrame(MapRace race);

    /// <summary>The portrait of a creature, frame 0 of its own ICN.</summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The sprite, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetMonsterFrame(MonsterType monster);
}

/// <summary>
/// The creature portrait of an army slot, <c>fheroes2::renderMonsterFrame()</c> (<c>ui_monster.cpp:31-59</c>).
/// </summary>
/// <remarks>
/// The plate is painted at the offset, the portrait at the offset plus its own <c>x()</c>/<c>y()</c> — the reference
/// blits the frame at <c>offset.x + monsterImage.x()</c>, so a portrait whose source rectangle starts at a negative
/// coordinate hangs over the plate to the left.
/// </remarks>
public static class MonsterFrameRenderer
{
    /// <summary>Paints one creature into an army slot.</summary>
    /// <param name="monster">The creature.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="offset">The top left corner of the slot.</param>
    /// <param name="sprites">The artwork.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> or <paramref name="sprites"/> is <see langword="null"/>.</exception>
    public static void Render(MonsterType monster, PixelBuffer output, Point offset, IMonsterFrameSprites sprites)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(sprites);

        Sprite? plate = sprites.GetRaceFrame(MonsterStatistics.GetGeneralStats(monster).Race);

        if (plate is not null)
        {
            ImageOps.BlitSprite(output, offset, plate);
        }

        Sprite? frame = sprites.GetMonsterFrame(monster);

        if (frame is not null)
        {
            ImageOps.BlitSprite(output, new Point(offset.X + frame.OffsetX, offset.Y + frame.OffsetY), frame);
        }
    }
}
