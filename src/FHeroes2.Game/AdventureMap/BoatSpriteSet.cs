using FHeroes2.Assets;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// Looks up the two sprites an empty boat on the adventure map is made of: the hull and the shadow it
/// casts on the water. <see cref="BoatSpriteSet"/> is the implementation backed by the archives;
/// tests substitute their own.
/// </summary>
public interface IBoatSpriteSource
{
    /// <summary>The hull of an empty boat, or <see langword="null"/> when the set cannot draw it.</summary>
    Sprite? GetBoat();

    /// <summary>The shadow an empty boat casts, drawn under the objects of the water it covers.</summary>
    Sprite? GetShadow();
}

/// <summary>
/// The boat sprites out of <c>BOAT32.ICN</c> and <c>BOATSHAD.ICN</c>, the port of
/// <c>getBoatSpritesPerTile</c> and <c>getBoatShadowSpritesPerTile</c> of the reference.
/// </summary>
/// <remarks>
/// <para>
/// A boat in an MP2 file is stored the way the original editor shows it: the boat object with the
/// <c>OBJNWAT2</c> frame of the water set in its ground part. The reference throws that sprite away
/// while loading — <c>Maps::setInitialObjectInfo</c> rewrites the main object part to
/// <c>BOAT32</c> frame 18 — and draws the hull out of <c>BOAT32</c> instead, with the matching frame
/// of <c>BOATSHAD</c> as its shadow.
/// </para>
/// <para>
/// <c>BOAT32</c> holds eight directions of nine frames each, and the left facing ones are stored
/// mirrored: an index above 128 names the same frame flipped horizontally. A map only ever holds an
/// empty boat facing <c>RIGHT</c>, which is frame 18; the other directions and the mirrored half are
/// set by <c>Maps::Tile::setBoat</c> when a hero boards or leaves a boat, which needs a world to exist.
/// </para>
/// </remarks>
public sealed class BoatSpriteSet : IBoatSpriteSource
{
    /// <summary>
    /// Frame of an empty boat on a map, the <c>RIGHT</c> facing hull the reference forces on every
    /// boat it reads out of an MP2 file.
    /// </summary>
    public const int DefaultFrame = 18;

    /// <summary>The archive entry that holds the hulls.</summary>
    public const string FileName = "BOAT32.ICN";

    /// <summary>The archive entry that holds the shadows.</summary>
    public const string ShadowFileName = "BOATSHAD.ICN";

    private readonly IcnSpriteCache _cache;

    private BoatSpriteSet(IcnSpriteCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Creates the set for an archive bundle; <see langword="null"/> when there is no data, which is
    /// what makes <c>AdventureMapView</c> leave the boats out.
    /// </summary>
    public static BoatSpriteSet? TryCreate(AssetBundle? assets) =>
        assets is null ? null : new BoatSpriteSet(new IcnSpriteCache(assets));

    /// <summary>Number of tile sets that have been decoded so far.</summary>
    public int LoadedCount => _cache.Count;

    /// <summary>The archive entries this set asked for but could not read.</summary>
    public IReadOnlyCollection<string> Missing => _cache.Missing;

    /// <summary>The hull of an empty boat.</summary>
    public Sprite? GetBoat() => _cache.GetSprite(FileName, DefaultFrame);

    /// <summary>The shadow of an empty boat, <c>BOATSHAD</c> in the frame of the hull.</summary>
    public Sprite? GetShadow() => _cache.GetSprite(ShadowFileName, DefaultFrame);
}
