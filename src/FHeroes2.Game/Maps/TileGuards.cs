using System;

using FHeroes2.Assets.Maps;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Maps;

public sealed partial class Tile
{
    /// <summary>
    /// Whether a capturable object still has to be fought for, <c>isCaptureObjectProtected()</c>
    /// (<c>maps_tiles_helper.cpp:1836-1856</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference only knows one kind of protection, a garrison: a castle is protected while its army is
    /// not empty, every other capturable object while the tile holds a valid troop.
    /// </para>
    /// <para>
    /// <c>ai_planner_hero.cpp</c> keeps a private copy of this rule and answers the castle case with "a hero
    /// stands on the entrance" instead of "the garrison is empty"; that copy is left alone until the AI is
    /// verified, so the two answers can differ for a castle tile. The quick info uses this one.
    /// </para>
    /// </remarks>
    /// <param name="world">World the tile belongs to.</param>
    /// <returns>True when the object cannot be taken without a fight.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public bool IsCaptureObjectProtected(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(GetMainObjectType(world, false));

        if (!Mp2ObjectTypes.IsCaptureObject(objectType))
        {
            return false;
        }

        if (objectType == Mp2ObjectType.Castle)
        {
            CastleTown? castle = world.GetCastleEntrance(Index);

            return castle is not null && castle.Army.IsValid;
        }

        return GetTroop(world).IsValid;
    }
}
