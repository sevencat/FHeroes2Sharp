using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The sprites of one object part, looked up by the ICN tile set and the frame index that an MP2
/// tile or add-on stores. It is the equivalent of <c>Assets::getImage( icnId, icnIndex )</c> for the
/// object tile sets: files are decoded on first use and cached for the rest of the session.
/// </summary>
/// <remarks>
/// The mapping from the tile set to the archive entry is <see cref="Mp2ObjectIcnTypes.GetArchiveName"/>;
/// the decoding and the cache are shared with <see cref="HeroSpriteSet"/> through
/// <see cref="IcnSpriteCache"/>.
/// </remarks>
public sealed class ObjectSpriteSet : IObjectSpriteSource
{
    private readonly IcnSpriteCache _cache;
    private readonly bool _priceOfLoyalty;

    private ObjectSpriteSet(IcnSpriteCache cache, bool priceOfLoyalty)
    {
        _cache = cache;
        _priceOfLoyalty = priceOfLoyalty;
    }

    /// <summary>Number of tile sets that have been decoded so far.</summary>
    public int LoadedCount => _cache.Count;

    /// <summary>Archive entries the map asked for but the archives do not hold.</summary>
    public IReadOnlyCollection<string> Missing => _cache.Missing;

    /// <summary>
    /// Creates the set for an archive bundle; <see langword="null"/> when there is no data, which
    /// is what makes <c>AdventureMapView</c> fall back to the bare terrain layer.
    /// </summary>
    public static ObjectSpriteSet? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        return new ObjectSpriteSet(
            new IcnSpriteCache(assets),
            assets.ExpansionArchive is not null);
    }

    /// <summary>
    /// The sprite of <paramref name="index"/> inside the tile set of <paramref name="type"/>, or
    /// <see langword="null"/> when the file is missing or the frame does not exist.
    /// </summary>
    /// <remarks>
    /// The set does not decide what may be drawn: <c>MINIHERO</c> is a tile set like any other, it
    /// is just not drawn by the generic object path. That filter belongs to the renderer.
    /// </remarks>
    public Sprite? GetSprite(Mp2ObjectIcnType type, int index) =>
        Mp2ObjectIcnTypes.GetArchiveName(type, _priceOfLoyalty) is string name
            ? _cache.GetSprite(name, index)
            : null;
}
