namespace FHeroes2.Assets.Maps;

/// <summary>
/// Product a map file belongs to, the port of <c>GameVersion</c>. For an MP2 header it is derived from
/// the file extension only (<c>.mx2</c> / <c>.hxc</c> are Price of Loyalty maps).
/// </summary>
public enum MapVersion
{
    /// <summary>The original <c>Heroes of Might and Magic II</c> map (<c>.mp2</c>).</summary>
    SuccessionWars = 0,

    /// <summary>The <c>Price of Loyalty</c> expansion map (<c>.mx2</c>, <c>.hxc</c>).</summary>
    PriceOfLoyalty = 1,

    /// <summary>A map of the fheroes2 map format (<c>.fh2m</c>), not supported here.</summary>
    Resurrection = 2,
}
