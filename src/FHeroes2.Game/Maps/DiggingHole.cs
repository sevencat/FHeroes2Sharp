using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The sprite of the hole a hero leaves behind when digging, <c>MP2::getDiggingHoleSprite()</c>
/// (<c>mp2.cpp:937-985</c>) and <c>MP2::isDiggingHoleSprite()</c> (<c>mp2.cpp:987-997</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every ground has its own small object ICN file with one hole image in it, and the indexes are not
/// related to each other — they are whatever the artists left in the file. Grass and beach share the
/// grass hole, because beach has none of its own. Water has none and can never be dug, which is the only
/// case the reference reports as <see langword="false"/> for a known ground.
/// </para>
/// <para>
/// The reference keeps this table in <c>mp2.cpp</c>, next to the object types it works with. The port
/// needs <see cref="MapGround"/>, which lives in the game layer and not in <c>FHeroes2.Assets</c>, so the
/// table lives here instead; the object ICN type next to it is the same one the loader reads.
/// </para>
/// </remarks>
public static class DiggingHole
{
    /// <summary>
    /// The sprite of the hole for a ground, <c>MP2::getDiggingHoleSprite()</c>.
    /// </summary>
    /// <remarks>
    /// The reference fills in the two outputs in every branch and returns whether the ground can be dug
    /// at all, so the out parameters are always written. <see cref="MapGround.Unknown"/> is the branch
    /// the reference covers with an <c>assert</c>: a tile of a well formed map never has that ground, and
    /// the port answers "cannot dig" instead of aborting.
    /// </remarks>
    /// <param name="ground">Ground of the tile.</param>
    /// <param name="icnType">The object ICN file that holds the hole image.</param>
    /// <param name="imageIndex">The image inside that file.</param>
    /// <returns>Whether the ground has a hole, i.e. whether it can be dug.</returns>
    public static bool TryGetSprite(
        MapGround ground, out Mp2ObjectIcnType icnType, out byte imageIndex)
    {
        switch (ground)
        {
            case MapGround.Desert:
                icnType = Mp2ObjectIcnType.ObjnDsrt;
                imageIndex = 68;
                return true;

            case MapGround.Snow:
                icnType = Mp2ObjectIcnType.ObjnSnow;
                imageIndex = 11;
                return true;

            case MapGround.Swamp:
                icnType = Mp2ObjectIcnType.ObjnSwmp;
                imageIndex = 86;
                return true;

            case MapGround.Wasteland:
                icnType = Mp2ObjectIcnType.ObjnCrck;
                imageIndex = 70;
                return true;

            case MapGround.Lava:
                icnType = Mp2ObjectIcnType.ObjnLava;
                imageIndex = 26;
                return true;

            case MapGround.Dirt:
                icnType = Mp2ObjectIcnType.ObjnDirt;
                imageIndex = 140;
                return true;

            case MapGround.Beach:
            case MapGround.Grass:
                // 沙滩没有自己的坑，借草地那一张（参考的注释就是这么写的）。
                icnType = Mp2ObjectIcnType.ObjnGra2;
                imageIndex = 9;
                return true;

            case MapGround.Water:
                // 水上挖不了，参考在这里写得明明白白，还留了感叹号。
                icnType = Mp2ObjectIcnType.Unknown;
                imageIndex = 255;
                return false;

            default:
                icnType = Mp2ObjectIcnType.Unknown;
                imageIndex = 255;
                return false;
        }
    }

    /// <summary>
    /// Whether an object part is the hole of <paramref name="ground"/>,
    /// <c>MP2::isDiggingHoleSprite()</c>.
    /// </summary>
    /// <remarks>
    /// The reference has exactly one caller, <c>MP2::isHiddenForPuzzle()</c> (<c>mp2.cpp:186</c>): a dug
    /// hole does not count as an obstacle of the obelisk puzzle. That function belongs to the puzzle model,
    /// which the port does not have yet, so nothing calls this one today; it is here because the two halves
    /// of the table belong together and a test can tell them apart.
    /// </remarks>
    /// <param name="ground">Ground of the tile.</param>
    /// <param name="icnType">Object ICN file of the part.</param>
    /// <param name="imageIndex">Image of the part.</param>
    /// <returns>Whether the part is a hole dug on that ground.</returns>
    public static bool IsSprite(MapGround ground, Mp2ObjectIcnType icnType, byte imageIndex) =>
        TryGetSprite(ground, out Mp2ObjectIcnType holeIcnType, out byte holeIndex)
        && icnType == holeIcnType
        && imageIndex == holeIndex;
}
