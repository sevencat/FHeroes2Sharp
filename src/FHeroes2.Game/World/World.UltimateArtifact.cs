using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game;

/// <summary>
/// Choosing where the ultimate artifact is buried, <c>World::setUltimateArtifact()</c>
/// (<c>world_loadmap.cpp:1627</c>).
/// </summary>
/// <remarks>
/// <para>
/// The map may name a spot: the object <c>OBJ_RANDOM_ULTIMATE_ARTIFACT</c> carries a radius in its first metadata lane
/// and an artifact id in the second. The object itself is removed — it is an editor placeholder, not something the
/// player ever sees — and the artifact is buried somewhere within that radius, or anywhere on the map when the radius
/// is 0.
/// </para>
/// <para>
/// <b>A map without the placeholder still gets an artifact</b>, anywhere on the map: that is what the reference does,
/// and it is what makes the <c>WINS_ARTIFACT</c> condition meaningful on an original map. The alternative — no artifact
/// on a map that never mentions one — would leave the Eye of the Magi and every dig pointless, so the port keeps the
/// reference's behaviour.
/// </para>
/// <para>
/// The port names the method <c>PlaceUltimateArtifact</c> rather than <c>SetUltimateArtifact</c>: the artifact itself
/// is the <see cref="UltimateArtifact"/> property, and C# does not allow a property and a method of the same name.
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>
    /// How many tiles along every edge are never used, <c>ultimateArtifactOffset</c> (<c>world_loadmap.cpp:1629</c>).
    /// </summary>
    /// <remarks>
    /// A ninth of the map's width, in the reference's arithmetic. The border is excluded because the map's edge is where
    /// the fog never clears evenly and where a hero arriving by boat would stumble on the artifact; the reference has
    /// used a ninth since the original game.
    /// </remarks>
    private int UltimateArtifactOffset => Width / 9;

    /// <summary>
    /// Buries the ultimate artifact, <c>World::setUltimateArtifact()</c> (<c>world_loadmap.cpp:1627</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole map is searched, not a random sample</b>: the pool is every tile that may hold the artifact, and one
    /// of them is drawn uniformly. That is O(tiles) per map load and simpler than any sampling scheme, which is why the
    /// reference does it too.
    /// </para>
    /// <para>
    /// <b>A radius of 0 means "anywhere".</b> The editor writes 0 for the default placement, which is not a circle of
    /// radius 0 (the placeholder tile itself) but the whole map — the one place where a metadata value of 0 is not a
    /// literal.
    /// </para>
    /// <para>
    /// When the radius is positive but no tile within it qualifies, the port falls back to the placeholder tile itself
    /// rather than to the whole map: the editor guarantees at least the centre qualifies, so a map where it does not is
    /// corrupted, and burying the artifact under the spot the map named is the answer that keeps the map playable.
    /// </para>
    /// <para>
    /// The artifact id may be 0 — the placeholder's metadata lane is empty on a map that did not choose one — and
    /// <see cref="UltimateArtifact.Set"/> draws a random ultimate for exactly that case, so this method never has to
    /// know the eight ultimates.
    /// </para>
    /// </remarks>
    public void PlaceUltimateArtifact()
    {
        int tileId = -1;
        int radius = 0;
        ArtifactType artifact = ArtifactType.Unknown;

        for (int index = 0; index < _tiles.Count; ++index)
        {
            if (_tiles[index].GetMainObjectType(this) != Mp2ObjectType.ActionRandomUltimateArtifact)
            {
                continue;
            }

            // The reference asserts that there is only one; the port takes the first and ignores the rest, because a
            // map with two placeholders is an editor bug rather than a reason to refuse to load.
            tileId = index;
            radius = (int)_tiles[index].Metadata[0];
            artifact = (ArtifactType)_tiles[index].Metadata[1];

            // Remove the placeholder and its shadow, which is a part of the same UID on the tile to the left.
            uint uid = _tiles[index].MainObjectPart.Uid;
            _tiles[index].RemoveObjectPartsByUID(this, uid);

            int shadowIndex = Geometry!.Value.GetDirectionIndex(index, Direction.Left);

            if (shadowIndex >= 0)
            {
                _tiles[shadowIndex].RemoveObjectPartsByUID(this, uid);
            }

            break;
        }

        MapVersion version = MapInfo?.Version ?? MapVersion.SuccessionWars;

        if (tileId < 0)
        {
            // Nothing named a spot: the whole map is the pool.
            var pool = new List<int>();

            for (int index = 0; index < _tiles.Count; ++index)
            {
                if (IsSuitableForUltimateArtifact(index))
                {
                    pool.Add(index);
                }
            }

            if (pool.Count > 0)
            {
                UltimateArtifact.Set(Rand.Get(pool), artifact, version);
            }

            return;
        }

        int position = tileId;

        if (radius > 0)
        {
            List<int> pool = MapGeometry.GetAroundIndexes(tileId, Width, Height, radius);

            // The neighbourhood query excludes the centre, and the centre is a candidate too.
            pool.Add(tileId);
            pool.RemoveAll(index => !IsSuitableForUltimateArtifact(index));

            if (pool.Count > 0)
            {
                position = Rand.Get(pool);
            }
        }

        UltimateArtifact.Set(position, artifact, version);
    }

    /// <summary>
    /// Whether the artifact may be buried on a tile, <c>setUltimateArtifact()</c>'s lambda
    /// (<c>world_loadmap.cpp:1662-1674</c>).
    /// </summary>
    /// <param name="index">Tile index.</param>
    /// <returns>Whether the tile is far enough from the border and suitable.</returns>
    private bool IsSuitableForUltimateArtifact(int index)
    {
        int x = index % Width;

        if (x < UltimateArtifactOffset || x >= Width - UltimateArtifactOffset)
        {
            return false;
        }

        int y = index / Width;

        if (y < UltimateArtifactOffset || y >= Height - UltimateArtifactOffset)
        {
            return false;
        }

        return _tiles[index].IsSuitableForUltimateArtifact(this);
    }
}
