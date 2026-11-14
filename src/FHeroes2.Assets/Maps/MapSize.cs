namespace FHeroes2.Assets.Maps;

/// <summary>
/// Size class of a map, the port of <c>Maps::MapSize</c> (<c>maps.h</c>). A map is square, so the
/// value of the enumeration is the edge length in tiles, which is exactly how the scenario list of
/// <c>dialog_selectscenario.cpp</c> filters maps.
/// </summary>
public enum MapSize : byte
{
    /// <summary>No size: the "all maps" filter of the scenario list, <c>Maps::ZERO</c>.</summary>
    None = 0,

    /// <summary>36 x 36 tiles, <c>Maps::SMALL</c>.</summary>
    Small = 36,

    /// <summary>72 x 72 tiles, <c>Maps::MEDIUM</c>.</summary>
    Medium = 72,

    /// <summary>108 x 108 tiles, <c>Maps::LARGE</c>.</summary>
    Large = 108,

    /// <summary>144 x 144 tiles, <c>Maps::XLARGE</c>.</summary>
    ExtraLarge = 144,
}

/// <summary>Helpers of the four map sizes the game uses, mirroring the comparisons of the reference.</summary>
public static class MapSizes
{
    /// <summary>The four real sizes, smallest first; <see cref="MapSize.None"/> is not a map size.</summary>
    public static IReadOnlyList<MapSize> All { get; } =
    [
        MapSize.Small,
        MapSize.Medium,
        MapSize.Large,
        MapSize.ExtraLarge,
    ];

    /// <summary>
    /// Size class of a map that is <paramref name="width"/> tiles wide; anything else is
    /// <see cref="MapSize.None"/>, which is what the reference treats as "unknown".
    /// </summary>
    public static MapSize FromWidth(int width) => width switch
    {
        36 => MapSize.Small,
        72 => MapSize.Medium,
        108 => MapSize.Large,
        144 => MapSize.ExtraLarge,
        _ => MapSize.None,
    };

    /// <summary>Edge length of <paramref name="size"/> in tiles, e.g. <c>108</c> for a large map.</summary>
    public static int EdgeLength(MapSize size) => (int)size;

    /// <summary>
    /// Human readable size, e.g. <c>108 x 108</c>; empty for <see cref="MapSize.None"/>. Maps are
    /// always square, so the height is the edge length as well.
    /// </summary>
    public static string Describe(MapSize size) =>
        size == MapSize.None
            ? string.Empty
            : $"{EdgeLength(size)} x {EdgeLength(size)}";
}
