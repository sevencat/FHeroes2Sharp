namespace FHeroes2.Assets.Maps;

/// <summary>
/// A hero standing on the map: where it is and which sprite of <c>MINIHERO.ICN</c> draws it. The
/// reference gets the same three things out of the tile itself
/// (<c>Maps::getColorRaceFromHeroSprite</c>), so no information block has to be read to place a hero.
/// </summary>
/// <param name="X">Column of the tile the hero stands on.</param>
/// <param name="Y">Row of the tile the hero stands on.</param>
/// <param name="SpriteIndex">
/// Frame of <c>MINIHERO.ICN</c>, i.e. the tile's ground object image index. Six colors of seven
/// sprites each are laid out in the order of <see cref="PlayerColors.Ordered"/>.
/// </param>
/// <param name="Color">Kingdom the hero belongs to, derived from <paramref name="SpriteIndex"/>.</param>
/// <param name="Race">Class of the hero, derived from <paramref name="SpriteIndex"/>.</param>
public readonly record struct Mp2MapHero(int X, int Y, int SpriteIndex, PlayerColor Color, MapRace Race);

/// <summary>Helpers that turn a <c>MINIHERO.ICN</c> frame into a color and a race.</summary>
public static class Mp2MapHeroes
{
    /// <summary>Number of sprites per color in <c>MINIHERO.ICN</c>.</summary>
    public const int SpritesPerColor = 7;

    /// <summary>
    /// Reads a hero off a tile. The tile is a hero tile when its object type is
    /// <see cref="Mp2ObjectType.Hero"/> and its ground object uses
    /// <see cref="Mp2ObjectIcnType.MiniHero"/>; both are checked because an editor can leave either
    /// half behind.
    /// </summary>
    public static bool TryRead(in Mp2TileInfo tile, int x, int y, out Mp2MapHero hero)
    {
        hero = default;

        if (tile.ObjectType is not (Mp2ObjectType.Hero or Mp2ObjectType.ActionHero)
            || tile.GroundIcnType != Mp2ObjectIcnType.MiniHero
            || tile.BottomIcnImageIndex == Mp2TileInfo.EmptyImageIndex)
        {
            return false;
        }

        hero = FromSpriteIndex(tile.BottomIcnImageIndex, x, y);
        return true;
    }

    /// <summary>
    /// Builds a hero from its sprite index, the port of <c>Maps::getColorRaceFromHeroSprite</c>. Index
    /// 0…6 belong to blue, 7…13 to green, and so on; inside a group the order is knight, barbarian,
    /// sorceress, warlock, wizard, necromancer, random.
    /// </summary>
    /// <param name="spriteIndex">Frame of <c>MINIHERO.ICN</c>.</param>
    /// <param name="x">Column of the tile.</param>
    /// <param name="y">Row of the tile.</param>
    public static Mp2MapHero FromSpriteIndex(int spriteIndex, int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spriteIndex);

        int colorIndex = Math.Min(spriteIndex / SpritesPerColor, PlayerColors.Ordered.Count - 1);
        PlayerColor color = PlayerColors.FromIndex(colorIndex);

        // A sprite index past the blue group of the last color is still drawn red-ish by the engine;
        // clamping keeps the derived color inside the playable set instead of throwing.
        MapRace race = (spriteIndex % SpritesPerColor) switch
        {
            0 => MapRace.Knight,
            1 => MapRace.Barbarian,
            2 => MapRace.Sorceress,
            3 => MapRace.Warlock,
            4 => MapRace.Wizard,
            5 => MapRace.Necromancer,
            _ => MapRace.Random,
        };

        return new Mp2MapHero(x, y, spriteIndex, color, race);
    }
}
