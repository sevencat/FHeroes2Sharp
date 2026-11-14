using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The slot frames of the icon bar, looked up by what a slot shows. <see cref="IconSpriteSet"/> is the
/// implementation backed by the archives; tests substitute their own.
/// </summary>
public interface IIconSpriteSource
{
    /// <summary>The icon of a castle, or <see langword="null"/> when there is none for its race.</summary>
    Sprite? GetCastle(MapRace race, bool hasFort);

    /// <summary>The empty slot of row <paramref name="row"/>, one frame per player color.</summary>
    Sprite? GetEmptySlot(int row);
}

/// <summary>
/// The icons of <see cref="IconBar"/> out of <c>LOCATORS.ICN</c>, the port of
/// <c>fheroes2::drawCastleIcon</c> and of the empty slot loop of
/// <c>Interface::IconsBar::redrawBackground</c>.
/// </summary>
/// <remarks>
/// The frames are decoded on first use and cached through <see cref="IcnSpriteCache"/>, like the
/// object and hero tile sets. The building markers of a castle (<c>CSLMARKER</c>) are not drawn: they
/// report whether a town can build today, which needs a kingdom and its buildings.
/// </remarks>
public sealed class IconSpriteSet : IIconSpriteSource
{
    private readonly IcnSpriteCache _cache;

    private IconSpriteSet(IcnSpriteCache cache)
    {
        _cache = cache;
    }

    /// <summary>Number of tile sets that have been decoded so far.</summary>
    public int LoadedCount => _cache.Count;

    /// <summary>Archive entries the bar asked for but the archives do not hold.</summary>
    public IReadOnlyCollection<string> Missing => _cache.Missing;

    /// <summary>
    /// Creates the set for an archive bundle; <see langword="null"/> when there is no data, which
    /// keeps <see cref="IconBar"/> from painting anything.
    /// </summary>
    public static IconSpriteSet? TryCreate(AssetBundle? assets) =>
        assets is null ? null : new IconSpriteSet(new IcnSpriteCache(assets));

    /// <summary>The frame of <paramref name="race"/>, or <see langword="null"/> when it has none.</summary>
    public Sprite? GetCastle(MapRace race, bool hasFort) =>
        IconBar.CastleFrame(race, hasFort) is int frame
            ? _cache.GetSprite(IconBar.AssetName, frame)
            : null;

    /// <summary>The empty slot frame of <paramref name="row"/>.</summary>
    public Sprite? GetEmptySlot(int row) =>
        _cache.GetSprite(IconBar.AssetName, IconBar.EmptySlotFrame(row));
}
