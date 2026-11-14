namespace FHeroes2.Assets.Maps;

/// <summary>
/// A ready made map object the editor can place: the ground and top part of a tile plus the add-ons
/// that make up the rest of a multi piece object.
/// </summary>
///
/// <remarks>
/// <para>
/// The map format knows no object catalogue: a tile stores an <see cref="Mp2ObjectIcnType"/> and a
/// sprite index, so "a tree" is only a combination of bytes that the renderer happens to draw as a
/// tree. The presets in <see cref="Mp2ObjectPresets"/> were taken from a shipped map
/// (<c>Alteris.mp2</c>), which is why the fields below are copied verbatim instead of computed: the
/// editor has to place what the original editor places.
/// </para>
/// <para>
/// Three fields of <see cref="Template"/> belong to the tile the object lands on, not to the object:
/// the terrain image, the terrain flags (the tile keeps the shape it had) and the add-on link, which
/// the placement builds from <see cref="Addons"/>. The object UIDs are replaced as well, because an
/// object without a fresh identifier is not a new object to the game. A preset is therefore a
/// template, not a tile.
/// </para>
/// </remarks>
public sealed record Mp2ObjectPreset
{
    /// <summary>Name shown in the toolbar and accepted by <c>--editor-object</c>.</summary>
    public string Name { get; }

    /// <summary>Object part of the tile: the ICN types, sprite indexes, layer and metadata.</summary>
    public Mp2TileInfo Template { get; }

    /// <summary>
    /// Add-ons of the object, in drawing order. Their <see cref="Mp2AddonInfo.NextAddonIndex"/> and
    /// UIDs are ignored: placement chains them and stamps a fresh identifier.
    /// </summary>
    public IReadOnlyList<Mp2AddonInfo> Addons { get; }

    /// <summary>Builds a preset; the template has to carry at least one object part.</summary>
    /// <param name="name">Name of the object.</param>
    /// <param name="template">Tile fields that describe the object.</param>
    /// <param name="addons">Extra pieces, none by default.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> or <paramref name="addons"/> is <see langword="null"/>.</exception>
    public Mp2ObjectPreset(string name, Mp2TileInfo template, IReadOnlyList<Mp2AddonInfo>? addons = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Template = template;
        Addons = addons ?? [];
    }

    /// <summary>Object type the game reports for the placed object.</summary>
    public Mp2ObjectType Type => Template.ObjectType;

    /// <summary>Whether the object has a ground part, i.e. is drawn in one of the four ground passes.</summary>
    public bool HasGroundPart => Template.HasGroundObject;

    /// <summary>Whether the object has a top part, i.e. is drawn over everything else on the tile.</summary>
    public bool HasTopPart => Template.HasTopObject;

    /// <summary>Number of add-ons the object needs on top of the tile itself.</summary>
    public int AddonCount => Addons.Count;

    /// <summary>Short diagnostic line, used by the toolbar and the tests.</summary>
    public string GetSummary()
    {
        string ground = HasGroundPart
            ? $"{Template.GroundIcnType}:{Template.BottomIcnImageIndex}"
            : "-";
        string top = HasTopPart ? $"{Template.TopIcnType}:{Template.TopIcnImageIndex}" : "-";

        return $"{Name} ({Type}) ground {ground} top {top} addons {Addons.Count}";
    }
}

/// <summary>
/// The objects the editor offers, one per entry, in the order the <c>O</c> key cycles them.
/// </summary>
///
/// <remarks>
/// Every definition is a verbatim copy of a tile of <c>Alteris.mp2</c> (a 144x144 Succession Wars
/// map): the ICN type and sprite index are what the renderer needs, the object type is what the
/// status bar and the game logic report, and the layer tells which of the four ground passes draws
/// the piece. Nothing here is invented, so every preset draws the same sprite the shipped map draws.
/// </remarks>
public static class Mp2ObjectPresets
{
    private static readonly Mp2ObjectPreset[] Presets =
    [
        // A deciduous tree of the temperate ground, drawn in the object pass (layer 0).
        new(
            "Tree",
            Ground(Mp2ObjectType.Trees, Mp2ObjectIcnType.TreVil, 19)),

        // The jungle tree is drawn one layer up (quantity1 bit 2 set in the shipped map).
        new(
            "Jungle Tree",
            Ground(Mp2ObjectType.Trees, Mp2ObjectIcnType.TreJngl, 5, quantity1: 4)),

        // Some trees are stored as a top part only, which is drawn over the ground object layer.
        new(
            "Tree (top)",
            Top(Mp2ObjectType.Trees, Mp2ObjectIcnType.TreVil, 14)),

        // A grass mountain, one tile wide.
        new(
            "Mountain",
            Ground(Mp2ObjectType.Mountains, Mp2ObjectIcnType.MtnGras, 60)),

        // A mountain whose peak is an add-on, the smallest multi piece object there is.
        new(
            "Mountain (peak)",
            Ground(Mp2ObjectType.Mountains, Mp2ObjectIcnType.MtnGras, 56, quantity1: 4),
            [Addon(Mp2ObjectIcnType.MtnGras, 75, layer: 2)]),

        new(
            "Graveyard",
            Ground(Mp2ObjectType.Graveyard, Mp2ObjectIcnType.ObjnMul2, 207)),

        new(
            "Treasure Chest",
            Ground(Mp2ObjectType.ActionTreasureChest, Mp2ObjectIcnType.ObjnRsrc, 19)),

        new(
            "Mine",
            Ground(Mp2ObjectType.Mine, Mp2ObjectIcnType.MtnMult, 81)),

        new(
            "Resources",
            Ground(Mp2ObjectType.ActionRandomResource, Mp2ObjectIcnType.ObjnRsrc, 17)),
    ];

    /// <summary>The objects the editor offers, in cycling order.</summary>
    public static IReadOnlyList<Mp2ObjectPreset> All => Presets;

    /// <summary>Number of presets, i.e. the largest number <c>--editor-object</c> accepts.</summary>
    public static int Count => Presets.Length;

    /// <summary>
    /// Finds a preset by name (case insensitive) or by its one based position, the two forms
    /// <c>--editor-object</c> accepts.
    /// </summary>
    /// <param name="value">Name such as <c>Tree</c>, or a number from 1 to <see cref="Count"/>.</param>
    /// <returns>The preset, <see langword="null"/> when nothing matches.</returns>
    public static Mp2ObjectPreset? Find(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();

        if (int.TryParse(trimmed, out int number))
        {
            return number >= 1 && number <= Presets.Length ? Presets[number - 1] : null;
        }

        return Presets.FirstOrDefault(preset =>
            string.Equals(preset.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Position of <paramref name="preset"/> in <see cref="All"/>, or -1.</summary>
    public static int IndexOf(Mp2ObjectPreset? preset) =>
        preset is null ? -1 : Array.IndexOf(Presets, preset);

    /// <summary>The ground part of an object, encoded the way a tile stores it.</summary>
    private static Mp2TileInfo Ground(
        Mp2ObjectType type, Mp2ObjectIcnType icn, int image, int quantity1 = 0) =>
        Empty(type) with
        {
            ObjectName1 = (byte)((int)icn << 2),
            BottomIcnImageIndex = (byte)image,
            Quantity1 = (byte)quantity1,
        };

    /// <summary>The top part of an object, encoded the way a tile stores it.</summary>
    private static Mp2TileInfo Top(Mp2ObjectType type, Mp2ObjectIcnType icn, int image) =>
        Empty(type) with
        {
            ObjectName2 = (byte)((int)icn << 2),
            TopIcnImageIndex = (byte)image,
        };

    /// <summary>
    /// One add-on. <paramref name="icn"/> and <paramref name="topIcn"/> are given as the ICN type of
    /// the piece; <see cref="Mp2AddonInfo"/> stores the ground one multiplied by two, the quirk
    /// <see cref="Mp2AddonInfo.Read"/> documents, so the shift below is the read side of it.
    /// </summary>
    private static Mp2AddonInfo Addon(
        Mp2ObjectIcnType icn,
        int image,
        int layer,
        Mp2ObjectIcnType topIcn = Mp2ObjectIcnType.Unknown,
        int topImage = Mp2TileInfo.EmptyImageIndex) =>
        new()
        {
            NextAddonIndex = 0,
            ObjectNameN1 = (byte)((int)icn << 2),
            BottomIcnImageIndex = (byte)image,
            QuantityN = (byte)layer,
            ObjectNameN2 = (byte)((int)topIcn << 2),
            TopIcnImageIndex = (byte)topImage,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

    /// <summary>
    /// A tile with nothing on it, the base every definition starts from: the terrain fields and the
    /// UIDs are overwritten when the object is placed.
    /// </summary>
    private static Mp2TileInfo Empty(Mp2ObjectType type) => new()
    {
        TerrainImageIndex = 0,
        ObjectName1 = 0,
        BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        Quantity1 = 0,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = type,
        NextAddonIndex = 0,
        Level1ObjectUid = 0,
        Level2ObjectUid = 0,
    };
}
