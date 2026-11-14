using FHeroes2.Assets;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// Looks up the sprite of a monster wandering the adventure map. <see cref="MonsterSpriteSet"/> is the
/// implementation backed by the archives; tests substitute their own.
/// </summary>
public interface IMonsterSpriteSource
{
    /// <summary>
    /// The frame of a monster, or <see langword="null"/> when the set cannot draw
    /// <paramref name="monsterId"/>.
    /// </summary>
    Sprite? GetMonster(int monsterId);
}

/// <summary>
/// The map monster sprites out of <c>MINIMON.ICN</c>, the port of the <c>ICN::MINI_MONSTER_IMAGE</c>
/// lookup of <c>getMonsterSpritesPerTile</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>MINIMON</c> holds nine frames per monster: the standing one first, then the eight frames of the
/// attacking animation, one per direction. The reference asks for <c>GetSpriteIndex() * 9</c>, which is
/// <c>( monsterId - 1 ) * 9</c> — <c>Monster::UNKNOWN</c> is zero and has no frames of its own.
/// </para>
/// <para>
/// A monster that is being attacked nearby shows its attack animation instead. That needs the position
/// of every hero, so a map without a game always draws the standing frame.
/// </para>
/// </remarks>
public sealed class MonsterSpriteSet : IMonsterSpriteSource
{
    /// <summary>Number of frames per monster, <c>monsterAnimationCount</c> of the reference.</summary>
    public const int FramesPerMonster = 9;

    /// <summary>The archive entry that holds the monsters of the adventure map.</summary>
    public const string FileName = "MINIMON.ICN";

    private readonly IcnSpriteCache _cache;

    private MonsterSpriteSet(IcnSpriteCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Creates the set for an archive bundle; <see langword="null"/> when there is no data, which is
    /// what makes <c>AdventureMapView</c> leave the monsters out.
    /// </summary>
    public static MonsterSpriteSet? TryCreate(AssetBundle? assets) =>
        assets is null ? null : new MonsterSpriteSet(new IcnSpriteCache(assets));

    /// <summary>Number of sets that have been decoded so far.</summary>
    public int LoadedCount => _cache.Count;

    /// <summary>The archive entries this set asked for but could not read.</summary>
    public IReadOnlyCollection<string> Missing => _cache.Missing;

    /// <summary>The standing frame of <paramref name="monsterId"/>.</summary>
    public Sprite? GetMonster(int monsterId) =>
        monsterId > 0 ? _cache.GetSprite(FileName, FrameOf(monsterId)) : null;

    /// <summary>
    /// The frame of the standing animation of <paramref name="monsterId"/>, the port of
    /// <c>Monster::GetSpriteIndex() * 9</c>.
    /// </summary>
    public static int FrameOf(int monsterId) => (monsterId - 1) * FramesPerMonster;
}
