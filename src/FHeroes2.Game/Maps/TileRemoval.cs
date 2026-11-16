using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Taking objects off the map and putting an abandoned mine back into service: the removal and mine half
/// of <c>maps_tiles_helper.cpp</c> — <c>restoreAbandonedMine()</c> (<c>:1858</c>),
/// <c>removeMainObjectFromTile()</c> (<c>:1984</c>), <c>removeObjectFromTileByType()</c> (<c>:1989</c>) and
/// <c>removeObjectFromMapByUID()</c> (<c>:2025</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>An object can span tiles</b>, and it is the UID that ties the pieces together. Removing by type
/// therefore only <em>finds</em> the object on the tile it is called with — the reverse scan of the top
/// level and then of the ground level, which is the order the reference uses because the part found last
/// is the one on top — and the actual deletion walks the neighbourhood looking for the same UID.
/// </para>
/// <para>
/// <b>A mine draws two tiles</b>: the mine itself and the mountain above it, and both carry the same UID.
/// Restoring an abandoned mine has to fix the sprites of both, because the abandoned variant uses a
/// different ICN set for the mountain (<c>OBJNGRAS</c> instead of <c>MTNGRAS</c>) and for the miner's
/// overlay (<c>EXTRAOVR</c>).
/// </para>
/// <para>
/// The reference returns the owner of the tile in <c>restoreAbandonedMine()</c>; the port keeps the same
/// void signature, since the flag sprite and the color are set by the caller that captured the mine.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>
    /// Turns an abandoned mine back into a working one, <c>restoreAbandonedMine()</c>
    /// (<c>maps_tiles_helper.cpp:1858</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three things change.</b> The resource the mine produces is written into the metadata of the
    /// tile, the main part and every part of the same object on this tile are switched from the abandoned
    /// sprite to the working one, and the neighbour to the right — which carries the second half of the
    /// mine — is switched along with it. Finally the mine tiles around it stop being
    /// <see cref="Mp2ObjectType.AbandonedMine"/>.
    /// </para>
    /// <para>
    /// The reference asserts that the tile is an abandoned mine and that the object has a UID; both are
    /// kept, because a UID of <c>0</c> makes the neighbour lookup meaningless.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to, for the right neighbour and the UIDs.</param>
    /// <param name="resource">A <see cref="Resource"/> constant of the mine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public void RestoreAbandonedMine(World world, int resource)
    {
        ArgumentNullException.ThrowIfNull(world);

        Debug.Assert(GetMainObjectType(world, false) == Mp2ObjectType.ActionAbandonedMine, "The tile is not an abandoned mine.");
        Debug.Assert(_mainObjectPart.Uid != 0, "An abandoned mine must have a UID.");

        Funds income = ProfitConditions.FromMine(resource);

        uint resourceCount = resource switch
        {
            Resource.Ore => (uint)income.Ore,
            Resource.Sulfur => (uint)income.Sulfur,
            Resource.Crystal => (uint)income.Crystal,
            Resource.Gems => (uint)income.Gems,
            Resource.Gold => (uint)income.Gold,
            _ => 0u,
        };

        Debug.Assert(resourceCount > 0, "A mine must produce something.");

        SetResourceOnTile(resource, resourceCount);

        // The main part of the mine and the pieces of the same object on this tile share the sprite swap.
        ObjectPart mainPart = _mainObjectPart;
        RestoreLeftSprite(resource, ref mainPart);
        _mainObjectPart = mainPart;

        for (int i = 0; i < _groundObjectParts.Count; ++i)
        {
            if (_groundObjectParts[i].Uid != mainPart.Uid)
            {
                continue;
            }

            ObjectPart part = _groundObjectParts[i];
            RestoreLeftSprite(resource, ref part);
            _groundObjectParts[i] = part;
        }

        MapGeometry geometry = world.Geometry ?? throw new InvalidOperationException("The world has no map yet.");

        // The right half of the mine is on the next tile, and it is part of the same object.
        if (geometry.IsValidDirection(Index, Direction.Right))
        {
            Tile rightTile = world.GetTile(geometry.GetDirectionIndex(Index, Direction.Right));

            if (rightTile.MainObjectPart.Uid == mainPart.Uid)
            {
                ObjectPart rightPart = rightTile.MainObjectPart;
                RestoreRightSprite(ref rightPart);
                rightTile.SetMainObjectPart(rightPart);
            }

            int partIndex = rightTile.GetGroundObjectPartIndex(mainPart.Uid);

            if (partIndex >= 0)
            {
                ObjectPart part = rightTile.GroundObjectParts[partIndex];
                RestoreRightSprite(ref part);
                rightTile.GroundObjectParts[partIndex] = part;
            }
        }

        RestoreMineObjectType(world, geometry, Direction.Left);
        RestoreMineObjectType(world, geometry, Direction.Right);
        RestoreMineObjectType(world, geometry, Direction.Top);
        RestoreMineObjectType(world, geometry, Direction.TopLeft);
        RestoreMineObjectType(world, geometry, Direction.TopRight);
    }

    /// <summary>
    /// Removes the object that the tile shows, <c>removeMainObjectFromTile()</c>
    /// (<c>maps_tiles_helper.cpp:1984</c>).
    /// </summary>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns><see langword="true"/> when an object was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public bool RemoveMainObject(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return RemoveObjectByType(world, GetMainObjectType(world, false));
    }

    /// <summary>
    /// Removes the object of a type that the tile shows, <c>removeObjectFromTileByType()</c>
    /// (<c>maps_tiles_helper.cpp:1989</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The type is matched against the sprite, not against the tile's object type.</b> The reference
    /// asks <c>getObjectTypeByIcn()</c> for the main part first, then walks the top level backwards and the
    /// ground level backwards, and the reverse order is deliberate: the part that is drawn last is the one
    /// that has to give way first.
    /// </para>
    /// <para>
    /// The first match decides the UID, and from there <see cref="RemoveObjectFromMapByUID"/> takes the
    /// whole object — including the pieces on the neighbouring tiles, which this method never looks at.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="objectType">Type of the object to remove, action bit included.</param>
    /// <returns><see langword="true"/> when the object was found and removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public bool RemoveObjectByType(World world, Mp2ObjectType objectType)
    {
        ArgumentNullException.ThrowIfNull(world);

        Debug.Assert(objectType != Mp2ObjectType.None, "There is no object to remove.");

        uint objectUid = 0;

        if (MapObjectCatalog.GetObjectTypeByIcn(_mainObjectPart.IcnType, _mainObjectPart.IcnIndex) == objectType)
        {
            objectUid = _mainObjectPart.Uid;
        }

        if (objectUid == 0)
        {
            for (int i = _topObjectParts.Count - 1; i >= 0; --i)
            {
                ObjectPart part = _topObjectParts[i];

                if (MapObjectCatalog.GetObjectTypeByIcn(part.IcnType, part.IcnIndex) == objectType)
                {
                    objectUid = part.Uid;
                    break;
                }
            }
        }

        if (objectUid == 0)
        {
            for (int i = _groundObjectParts.Count - 1; i >= 0; --i)
            {
                ObjectPart part = _groundObjectParts[i];

                if (MapObjectCatalog.GetObjectTypeByIcn(part.IcnType, part.IcnIndex) == objectType)
                {
                    objectUid = part.Uid;
                    break;
                }
            }
        }

        if (objectUid == 0)
        {
            return false;
        }

        return RemoveObjectFromMapByUID(world, Index, objectUid);
    }

    /// <summary>
    /// Removes every piece of one object, walking the map outwards from a tile,
    /// <c>removeObjectFromMapByUID()</c> (<c>maps_tiles_helper.cpp:2025</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk is a flood fill over the tiles that actually held a piece.</b> Only a tile that had the
    /// object spreads the search to its eight neighbours, so the cost follows the size of the object and
    /// not the size of the map, and a tile is never visited twice.
    /// </para>
    /// <para>
    /// The reference answers whether <em>any</em> tile was processed, which is always <see langword="true"/>
    /// because the starting tile is pushed unconditionally — the meaningful answer is the one of
    /// <see cref="RemoveObjectByType"/>, and the port keeps the reference's return value as it is.
    /// </para>
    /// <para>
    /// This is a static on <see cref="Tile"/> rather than a method of <see cref="World"/> because that is
    /// where the reference keeps it: it is the object's removal, not the world's.
    /// </para>
    /// </remarks>
    /// <param name="world">The world with the object.</param>
    /// <param name="startTileIndex">Tile the search starts from.</param>
    /// <param name="objectUid">UID of the object to remove, never <c>0</c>.</param>
    /// <returns><see langword="true"/> when at least one tile was examined.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public static bool RemoveObjectFromMapByUID(World world, int startTileIndex, uint objectUid)
    {
        ArgumentNullException.ThrowIfNull(world);

        Debug.Assert(startTileIndex >= 0 && startTileIndex < world.Width * world.Height, "The starting tile is outside the map.");
        Debug.Assert(objectUid > 0, "An object with a zero UID cannot be removed.");

        List<int> tiles = [startTileIndex];
        var processedTiles = new HashSet<int>();

        for (int currentId = 0; currentId < tiles.Count; ++currentId)
        {
            int tileIndex = tiles[currentId];

            if (!processedTiles.Add(tileIndex))
            {
                // Already handled through another piece of the same object.
                continue;
            }

            if (!world.GetTile(tileIndex).RemoveObjectPartsByUID(world, objectUid))
            {
                continue;
            }

            // This tile had the object: the neighbours may hold the other pieces.
            foreach (int neighbourIndex in MapGeometry.GetAroundIndexes(tileIndex, world.Width, world.Height, 1))
            {
                if (!processedTiles.Contains(neighbourIndex))
                {
                    tiles.Add(neighbourIndex);
                }
            }
        }

        return processedTiles.Count > 0;
    }

    /// <summary>
    /// Index of the ground level part of an object, <c>getGroundObjectPart()</c>
    /// (<c>maps_tiles.h:341</c>).
    /// </summary>
    /// <param name="uid">UID of the object.</param>
    /// <returns>The index inside <see cref="GroundObjectParts"/>, or <c>-1</c>.</returns>
    public int GetGroundObjectPartIndex(uint uid) => _groundObjectParts.FindIndex(part => part.Uid == uid);

    /// <summary>
    /// Index of the top level part of an object, <c>getTopObjectPart()</c> (<c>maps_tiles.h:346</c>).
    /// </summary>
    /// <param name="uid">UID of the object.</param>
    /// <returns>The index inside <see cref="TopObjectParts"/>, or <c>-1</c>.</returns>
    public int GetTopObjectPartIndex(uint uid) => _topObjectParts.FindIndex(part => part.Uid == uid);

    /// <summary>
    /// Swaps the abandoned sprite of the left half of a mine for the working one,
    /// the <c>restoreLeftSprite</c> lambda of <c>restoreAbandonedMine()</c> (<c>maps_tiles_helper.cpp:1891</c>).
    /// </summary>
    /// <param name="resource">A <see cref="Resource"/> constant of the mine.</param>
    /// <param name="part">The part to change.</param>
    private static void RestoreLeftSprite(int resource, ref ObjectPart part)
    {
        if (part.IcnType == Mp2ObjectIcnType.ObjnGras && part.IcnIndex == 6)
        {
            part.IcnType = Mp2ObjectIcnType.MtnGras;
            part.IcnIndex = 82;
        }
        else if (part.IcnType == Mp2ObjectIcnType.ObjnDirt && part.IcnIndex == 8)
        {
            part.IcnType = Mp2ObjectIcnType.MtnDirt;
            part.IcnIndex = 112;
        }
        else if (part.IcnType == Mp2ObjectIcnType.ExtraOvr && part.IcnIndex == 5)
        {
            // The miner's overlay is one sprite per resource.
            part.IcnIndex = (byte)(resource switch
            {
                Resource.Ore => 0,
                Resource.Sulfur => 1,
                Resource.Crystal => 2,
                Resource.Gems => 3,
                Resource.Gold => 4,
                _ => 5,
            });
        }
    }

    /// <summary>
    /// Swaps the abandoned sprite of the right half of a mine for the working one,
    /// the <c>restoreRightSprite</c> lambda of <c>restoreAbandonedMine()</c> (<c>maps_tiles_helper.cpp:1923</c>).
    /// </summary>
    /// <param name="part">The part to change.</param>
    private static void RestoreRightSprite(ref ObjectPart part)
    {
        if (part.IcnType == Mp2ObjectIcnType.ObjnDirt && part.IcnIndex == 9)
        {
            part.IcnType = Mp2ObjectIcnType.MtnDirt;
            part.IcnIndex = 113;
        }
        else if (part.IcnType == Mp2ObjectIcnType.ObjnGras && part.IcnIndex == 7)
        {
            part.IcnType = Mp2ObjectIcnType.MtnGras;
            part.IcnIndex = 83;
        }
    }

    /// <summary>
    /// Turns a neighbouring abandoned mine into a working one, the <c>restoreMineObjectType</c> lambda of
    /// <c>restoreAbandonedMine()</c> (<c>maps_tiles_helper.cpp:1934</c>).
    /// </summary>
    /// <remarks>
    /// The neighbour must be an abandoned mine <em>of the same object</em>: either the same main part UID or
    /// a part of it on the ground or the top level. Only then does its type become
    /// <see cref="Mp2ObjectType.Mine"/> — the non-action value, which is what the reference writes.
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="geometry">Geometry of the world.</param>
    /// <param name="direction">Direction of the neighbour to check.</param>
    private void RestoreMineObjectType(World world, MapGeometry geometry, Direction direction)
    {
        if (!geometry.IsValidDirection(Index, direction))
        {
            return;
        }

        Tile mineTile = world.GetTile(geometry.GetDirectionIndex(Index, direction));

        bool isSameObject = mineTile.MainObjectPart.Uid == _mainObjectPart.Uid
                            || mineTile.GetGroundObjectPartIndex(_mainObjectPart.Uid) >= 0
                            || mineTile.GetTopObjectPartIndex(_mainObjectPart.Uid) >= 0;

        if (mineTile.GetMainObjectType(world, false) == Mp2ObjectType.AbandonedMine && isSameObject)
        {
            mineTile.SetMainObjectType(Mp2ObjectType.Mine);
        }
    }
}
