namespace FHeroes2.Assets.Maps;

/// <summary>
/// One of the resources a pile, a mine or a chest can hold, with the bit values of
/// <c>Resource::WOOD</c> … <c>Resource::GOLD</c> so that the masks of the reference can be reused.
/// </summary>
[Flags]
public enum Mp2ResourceType
{
    /// <summary>Nothing, which is also what an unknown sprite decodes to (<c>Resource::UNKNOWN</c>).</summary>
    None = 0,

    /// <summary><c>Resource::WOOD</c>.</summary>
    Wood = 1,

    /// <summary><c>Resource::MERCURY</c>.</summary>
    Mercury = 2,

    /// <summary><c>Resource::ORE</c>.</summary>
    Ore = 4,

    /// <summary><c>Resource::SULFUR</c>.</summary>
    Sulfur = 8,

    /// <summary><c>Resource::CRYSTAL</c>.</summary>
    Crystal = 16,

    /// <summary><c>Resource::GEMS</c>.</summary>
    Gems = 32,

    /// <summary><c>Resource::GOLD</c>.</summary>
    Gold = 64,

    /// <summary>Every resource, <c>Resource::ALL</c>.</summary>
    All = Wood | Mercury | Ore | Sulfur | Crystal | Gems | Gold,
}

/// <summary>
/// What the 13 bit data word of a tile means, and what is left of the meaning when the sprite has to
/// tell instead. The reference reads the word in <c>Maps::setInitialObjectInfo</c> while loading a
/// map, and that function settles the question: <c>OBJ_MONSTER</c> is the only action object whose
/// word it keeps, as the count of the troop stack. For resource piles, mines, chests and graveyards
/// it throws the word away — the sprite names the resource of a pile or a mine, and everything else
/// is rolled when the map starts. The amounts do exist in the reference's own map format, but they
/// live in an extended metadata block (<c>metadata[1]</c>) that an MP2 file does not have, so the
/// editor can describe the data word but never a number of gold pieces it cannot store.
/// </summary>
public static class Mp2ObjectData
{
    /// <summary>
    /// Decodes a sprite index of an <see cref="Mp2ObjectIcnType.ObjnRsrc"/> pile the way
    /// <c>Resource::FromIndexSprite</c> does: every second frame is a resource, the odd ones being
    /// the shadows, so 1 is wood, 3 mercury, 5 ore, 7 sulfur, 9 crystal, 11 gems and 13 gold.
    /// </summary>
    /// <param name="spriteIndex">Frame of the pile inside <c>OBJNRSRC</c>.</param>
    /// <returns>The resource, <see cref="Mp2ResourceType.None"/> when the frame names none.</returns>
    public static Mp2ResourceType ResourceFromSpriteIndex(int spriteIndex) => spriteIndex switch
    {
        1 => Mp2ResourceType.Wood,
        3 => Mp2ResourceType.Mercury,
        5 => Mp2ResourceType.Ore,
        7 => Mp2ResourceType.Sulfur,
        9 => Mp2ResourceType.Crystal,
        11 => Mp2ResourceType.Gems,
        13 => Mp2ResourceType.Gold,
        _ => Mp2ResourceType.None,
    };

    /// <summary>
    /// The resource a pile on a tile holds, read from its sprite: the main object first, then the
    /// ground object, the order the reference tries them in.
    /// </summary>
    /// <param name="tile">Tile to read, an empty tile holds no resource.</param>
    /// <returns>The resource, <see cref="Mp2ResourceType.None"/> when no part is a pile.</returns>
    public static Mp2ResourceType ResourceOf(Mp2TileInfo tile)
    {
        if (tile.HasTopObject && tile.TopIcnType == Mp2ObjectIcnType.ObjnRsrc)
        {
            return ResourceFromSpriteIndex(tile.TopIcnImageIndex);
        }

        if (tile.HasGroundObject && tile.GroundIcnType == Mp2ObjectIcnType.ObjnRsrc)
        {
            return ResourceFromSpriteIndex(tile.BottomIcnImageIndex);
        }

        return Mp2ResourceType.None;
    }

    /// <summary>
    /// The resource a mine produces, read from the <c>EXTRAOVR</c> overlay the reference looks for:
    /// frame 0 is ore, 1 sulfur, 2 crystal, 3 gems and 4 gold.
    /// </summary>
    /// <param name="tile">Tile to read.</param>
    /// <returns>The resource, <see cref="Mp2ResourceType.None"/> when the overlay is missing.</returns>
    public static Mp2ResourceType MineOf(Mp2TileInfo tile)
    {
        if (tile.HasTopObject && tile.TopIcnType == Mp2ObjectIcnType.ExtraOvr)
        {
            return MineFromOverlayIndex(tile.TopIcnImageIndex);
        }

        if (tile.HasGroundObject && tile.GroundIcnType == Mp2ObjectIcnType.ExtraOvr)
        {
            return MineFromOverlayIndex(tile.BottomIcnImageIndex);
        }

        return Mp2ResourceType.None;
    }

    /// <summary>
    /// Whether the data word of an object is the size of the troop stack on it, i.e. whether the
    /// reference keeps the word instead of clearing it.
    /// </summary>
    /// <param name="type">Object type of the tile.</param>
    /// <returns><see langword="true"/> for the monster objects.</returns>
    public static bool CarriesTroopCount(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionMonster => true,
        Mp2ObjectType.ActionRandomMonster => true,
        Mp2ObjectType.ActionRandomMonsterWeak => true,
        Mp2ObjectType.ActionRandomMonsterMedium => true,
        Mp2ObjectType.ActionRandomMonsterStrong => true,
        Mp2ObjectType.ActionRandomMonsterVeryStrong => true,
        _ => false,
    };

    /// <summary>
    /// One sentence about what the data word of a tile means, the line the information box shows
    /// under the word itself.
    /// </summary>
    /// <param name="tile">Tile to describe, <see cref="Mp2TileInfo.Metadata"/> being its word.</param>
    /// <returns>The meaning of the word for this kind of object.</returns>
    public static string Describe(Mp2TileInfo tile)
    {
        Mp2ObjectType type = tile.ObjectType;
        uint word = tile.Metadata;

        if (CarriesTroopCount(type))
        {
            return FormattableString.Invariant($"the word is the troop count: {word} monsters");
        }

        switch (type)
        {
            case Mp2ObjectType.ActionResource:
            case Mp2ObjectType.ActionRandomResource:
                return DescribePile(tile);

            case Mp2ObjectType.ActionMine:
                return DescribeMine(tile);

            case Mp2ObjectType.ActionTreasureChest:
                return "the gold or the experience is rolled when the map starts";

            case Mp2ObjectType.ActionGraveyard:
                return "the artifact it holds is rolled when the map starts";
        }

        if (type == Mp2ObjectType.None)
        {
            return "bare ground, no object to carry a word";
        }

        return Mp2ObjectTypes.ContainsMetadata(type)
            ? "the word is metadata this build does not read yet"
            : "the word means nothing to this object, the game clears it";
    }

    /// <summary>Names a single resource, the way the reference prints it.</summary>
    /// <param name="resource">Resource to name.</param>
    /// <returns>Its lower case English name.</returns>
    public static string Name(Mp2ResourceType resource) => resource switch
    {
        Mp2ResourceType.Wood => "wood",
        Mp2ResourceType.Mercury => "mercury",
        Mp2ResourceType.Ore => "ore",
        Mp2ResourceType.Sulfur => "sulfur",
        Mp2ResourceType.Crystal => "crystal",
        Mp2ResourceType.Gems => "gems",
        Mp2ResourceType.Gold => "gold",
        _ => "nothing",
    };

    /// <summary>Frame of the mine overlay to the resource it produces.</summary>
    private static Mp2ResourceType MineFromOverlayIndex(int overlayIndex) => overlayIndex switch
    {
        0 => Mp2ResourceType.Ore,
        1 => Mp2ResourceType.Sulfur,
        2 => Mp2ResourceType.Crystal,
        3 => Mp2ResourceType.Gems,
        4 => Mp2ResourceType.Gold,
        _ => Mp2ResourceType.None,
    };

    /// <summary>The sentence for a resource pile, whose sprite names the resource but not the count.</summary>
    private static string DescribePile(Mp2TileInfo tile)
    {
        Mp2ResourceType resource = ResourceOf(tile);

        if (tile.ObjectType == Mp2ObjectType.ActionRandomResource)
        {
            return "a random pile: resource and amount are rolled when the map starts";
        }

        return resource == Mp2ResourceType.None
            ? "the sprite names no resource, the game makes it wood and rolls the amount"
            : FormattableString.Invariant(
                $"the sprite shows {Name(resource)}, the amount is rolled when the map starts");
    }

    /// <summary>The sentence for a mine, whose overlay names the resource.</summary>
    private static string DescribeMine(Mp2TileInfo tile)
    {
        Mp2ResourceType resource = MineOf(tile);

        return resource == Mp2ResourceType.None
            ? "no overlay sprite: an unknown mine, the game turns the tile into bare ground"
            : FormattableString.Invariant($"the overlay shows the {Name(resource)} mine");
    }
}
