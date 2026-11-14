using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// Looks up the sprite of one object part by the ICN tile set and the frame index an MP2 tile or
/// add-on stores. <see cref="ObjectSpriteSet"/> is the implementation backed by the archives; tests
/// substitute their own, which is what keeps the drawing logic verifiable without game data.
/// </summary>
public interface IObjectSpriteSource
{
    /// <summary>
    /// The sprite of <paramref name="index"/> inside the tile set of <paramref name="type"/>, or
    /// <see langword="null"/> when the part draws nothing or the frame does not exist.
    /// </summary>
    Sprite? GetSprite(Mp2ObjectIcnType type, int index);
}
