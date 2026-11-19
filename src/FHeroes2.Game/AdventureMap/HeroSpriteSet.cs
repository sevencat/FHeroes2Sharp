using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// Looks up the three sprites that make up a hero standing on the adventure map: the body, the flag
/// of the kingdom and the shadow on the ground. <see cref="HeroSpriteSet"/> is the implementation
/// backed by the archives; tests substitute their own.
/// </summary>
public interface IHeroSpriteSource
{
    /// <summary>The body of a hero of <paramref name="race"/>, or <see langword="null"/> when unknown.</summary>
    Sprite? GetBody(MapRace race);

    /// <summary>
    /// The body of a hero of <paramref name="race"/> in <paramref name="frame"/> of its tile set,
    /// <c>Heroes::GetHeroSpriteIndex()</c>'s result.
    /// </summary>
    /// <remarks>
    /// Every set holds nine frames per direction and the directions run from <c>TOP</c> clockwise, so a hero
    /// that faces <c>RIGHT</c> and does not walk is frame <see cref="HeroSpriteSet.StandingFrame"/>. The
    /// default implementation ignores the frame and falls back to <see cref="GetBody(MapRace)"/>: a source
    /// that only knows the standing frame — the one the map reader needs — stays valid.
    /// </remarks>
    /// <param name="race">Race of the hero.</param>
    /// <param name="frame">Index of the frame inside the tile set.</param>
    Sprite? GetBody(MapRace race, int frame) => GetBody(race);

    /// <summary>The flag of <paramref name="color"/>, or <see langword="null"/> for no kingdom.</summary>
    Sprite? GetFlag(PlayerColor color);

    /// <summary>The shadow every hero casts on the ground.</summary>
    Sprite? GetShadow();
}

/// <summary>
/// The hero sprites out of the archives, the port of <c>getHeroSpriteInfo</c>,
/// <c>getFlagSpriteInfo</c> and <c>getShadowSpriteInfo</c> of the reference.
/// </summary>
/// <remarks>
/// <para>
/// The body is not <c>MINIHERO.ICN</c> — that set belongs to the original map editor. A hero of the
/// game is drawn from the <c>KNGT32</c>, <c>BARB32</c>, <c>SORC32</c>, <c>WRLK32</c>, <c>WZRD32</c>
/// or <c>NECR32</c> set of its race, coloured by its own flag out of <c>B_FLAG32</c> … <c>P_FLAG32</c>.
/// </para>
/// <para>
/// Every one of those sets holds nine frames per direction, in the order <c>TOP</c>,
/// <c>TOP_RIGHT</c>, <c>RIGHT</c>, <c>BOTTOM_RIGHT</c>, <c>BOTTOM</c> and the three mirrored
/// directions, and the last nine frames are the "changing direction" ones. A hero that is not moving —
/// every hero right after the map is loaded — faces <c>RIGHT</c> with the first frame of the group,
/// which is why the whole set is asked for frame 18. The idle animation only moves the flag, so the
/// body and the shadow never leave that frame.
/// </para>
/// </remarks>
public sealed class HeroSpriteSet : IHeroSpriteSource
{
    /// <summary>Number of frames per direction, <c>Heroes::heroFrameCountPerTile</c>.</summary>
    public const int FramesPerDirection = 9;

    /// <summary>
    /// Offset of the <c>RIGHT</c> group, the direction a hero starts with
    /// (<c>Heroes::_direction</c> defaults to <c>Direction::RIGHT</c>). The frame inside the group is
    /// zero because the body of a standing hero is not animated.
    /// </summary>
    public const int StandingFrame = 18;

    private readonly IcnSpriteCache _cache;

    private HeroSpriteSet(IcnSpriteCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Creates the set for an archive bundle; <see langword="null"/> when there is no data, which is
    /// what makes <c>AdventureMapView</c> fall back to drawing bare objects.
    /// </summary>
    public static HeroSpriteSet? TryCreate(AssetBundle? assets) =>
        assets is null ? null : new HeroSpriteSet(new IcnSpriteCache(assets));

    /// <summary>Number of tile sets that have been decoded so far.</summary>
    public int LoadedCount => _cache.Count;

    /// <summary>The archive entries this set asked for but could not read.</summary>
    public IReadOnlyCollection<string> Missing => _cache.Missing;

    /// <summary>The body of a hero of <paramref name="race"/>.</summary>
    public Sprite? GetBody(MapRace race) =>
        GetArchiveName(race) is string name ? _cache.GetSprite(name, StandingFrame) : null;

    /// <summary>The body of a hero of <paramref name="race"/> in <paramref name="frame"/>.</summary>
    public Sprite? GetBody(MapRace race, int frame) =>
        GetArchiveName(race) is string name ? _cache.GetSprite(name, frame) : null;

    /// <summary>The flag of <paramref name="color"/>.</summary>
    public Sprite? GetFlag(PlayerColor color) =>
        GetArchiveName(color) is string name ? _cache.GetSprite(name, StandingFrame) : null;

    /// <summary>The shadow of a hero, <c>SHADOW32.ICN</c> in the frame of the shadow layer.</summary>
    public Sprite? GetShadow() => _cache.GetSprite(ShadowFileName, StandingFrame);

    /// <summary>The archive entry that holds the sprites of <paramref name="race"/>.</summary>
    /// <returns>
    /// <see langword="null"/> for <see cref="MapRace.None"/> and <see cref="MapRace.Random"/>: a random
    /// hero stands for "the race of its kingdom", which is not in the tile and is chosen when the game
    /// starts, so a map reader cannot draw it yet.
    /// </returns>
    public static string? GetArchiveName(MapRace race) =>
        race switch
        {
            MapRace.Knight => "KNGT32.ICN",
            MapRace.Barbarian => "BARB32.ICN",
            MapRace.Sorceress => "SORC32.ICN",
            MapRace.Warlock => "WRLK32.ICN",
            MapRace.Wizard => "WZRD32.ICN",
            MapRace.Necromancer => "NECR32.ICN",
            _ => null,
        };

    /// <summary>The archive entry that holds the flags of <paramref name="color"/>.</summary>
    /// <returns><see langword="null"/> for <see cref="PlayerColor.None"/>.</returns>
    public static string? GetArchiveName(PlayerColor color) =>
        color switch
        {
            PlayerColor.Blue => "B-FLAG32.ICN",
            PlayerColor.Green => "G-FLAG32.ICN",
            PlayerColor.Red => "R-FLAG32.ICN",
            PlayerColor.Yellow => "Y-FLAG32.ICN",
            PlayerColor.Orange => "O-FLAG32.ICN",
            PlayerColor.Purple => "P-FLAG32.ICN",
            _ => null,
        };

    /// <summary>The tile set of hero shadows, shared by heroes and by every other object that casts one.</summary>
    public const string ShadowFileName = "SHADOW32.ICN";
}
