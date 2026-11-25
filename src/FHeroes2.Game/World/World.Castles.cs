using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game;

/// <summary>
/// The towns and castles of the world: the table, the lookups that answer for their footprint, and the map file
/// side that builds them.
/// </summary>
/// <remarks>
/// <para>
/// The reference splits this over three places: the table itself (<c>AllCastles</c>), two lookups on the world
/// (<c>World::getCastle()</c>, <c>World::getCastleEntrance()</c>, <c>world_loadmap.cpp:302-440</c>) and two sprite
/// repaints in <c>maps.cpp</c> that the map loader calls when it meets a random town. All of them need the world,
/// so they all live here rather than in the castle class.
/// </para>
/// <para>
/// <b>What the loader does, in order</b>: the town table of the file is turned into <see cref="CastleTown"/> objects
/// before the information blocks are read, because a block finds its town by the tile it stands on; then every block
/// fills one town; then the unnamed towns get a name. The order matters — a block that is read before its town
/// exists has nowhere to go, which is what used to happen when the port skipped the table.
/// </para>
/// </remarks>
public sealed partial class World
{
    private AllCastles _castles = new(0);

    /// <summary>
    /// Every town of the world, <c>vec_castles</c> (<c>world.h:311</c>).
    /// </summary>
    /// <remarks>
    /// Replaced, not cleared, when a map is loaded: the table remembers the map width it was built for in order to
    /// map coordinates to tiles.
    /// </remarks>
    public AllCastles Castles => _castles;

    /// <summary>
    /// The town that owns a tile, <c>World::getCastle()</c> (<c>world.cpp:390</c>).
    /// </summary>
    /// <remarks>
    /// Answers for every tile of the town's footprint, not only for its entrance — the reference's table registers
    /// the seventeen tiles the sprite covers.
    /// </remarks>
    /// <param name="tileIndex">Tile index, row major.</param>
    /// <returns>The town, or <see langword="null"/> when no town covers the tile.</returns>
    public CastleTown? GetCastle(int tileIndex) => _castles.Get(tileIndex);

    /// <summary>
    /// The town that owns a tile, <c>World::getCastle()</c> (<c>world.cpp:390</c>).
    /// </summary>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <returns>The town, or <see langword="null"/> when no town covers the tile.</returns>
    public CastleTown? GetCastle(int x, int y) => _castles.Get(x, y);

    /// <summary>
    /// The town whose gate stands on a tile, <c>World::getCastleEntrance()</c> (<c>world.cpp:399</c>).
    /// </summary>
    /// <remarks>
    /// A town's block belongs to its entrance, so this is the lookup the map loader uses; unlike
    /// <see cref="GetCastle(int)"/> it rejects a tile of the footprint that is not the gate.
    /// </remarks>
    /// <param name="tileIndex">Tile index of the entrance.</param>
    /// <returns>The town, or <see langword="null"/> when the tile is not a castle entrance.</returns>
    public CastleTown? GetCastleEntrance(int tileIndex) =>
        IsValidCastleEntrance(tileIndex) ? GetCastle(tileIndex) : null;

    /// <summary>
    /// The town whose gate stands on a tile, given as an absolute position,
    /// <c>World::getCastleEntrance( const fheroes2::Point &amp; )</c> (<c>world.cpp:399</c>).
    /// </summary>
    /// <remarks>
    /// 参考只有一个以 <c>Point</c> 为参数的版本，而 <c>getTile()</c> 对图外的点会断言；地图条件里存的正是
    /// <c>Point</c>，而且它可能是 (-1, -1)（条件没有坐标时），所以这个重载先查图内再换算成下标。
    /// </remarks>
    /// <param name="tile">Absolute map position of the entrance.</param>
    /// <returns>The town, or <see langword="null"/> when the tile is outside the map or not a castle entrance.</returns>
    public CastleTown? GetCastleEntrance(Point tile) =>
        IsInsideMap(tile.X, tile.Y) ? GetCastleEntrance(tile.Y * Width + tile.X) : null;

    /// <summary>
    /// The hero standing on a town, <c>World::GetHero( const Castle &amp; )</c> (<c>world.h:330</c>).
    /// </summary>
    /// <remarks>
    /// The reference keeps a back pointer from the town to its guest; the port's <see cref="CastleTown"/> does not,
    /// so the guest is found by the tile it stands on. A town's gate tile is where the visiting hero stands, which is
    /// the same rule <c>Castle::BuyBuilding</c> applies.
    /// </remarks>
    /// <param name="castle">The town.</param>
    /// <returns>The visiting hero, or <see langword="null"/> when nobody is home.</returns>
    public Hero? GetHero(CastleTown castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        foreach (Hero hero in Heroes)
        {
            if (hero.MapIndex == castle.TileIndex)
            {
                return hero;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a tile carries the entrance of a town, <c>World::isValidCastleEntrance()</c>
    /// (<c>world.cpp:405-408</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the object type is checked, not the town table: the reference asks the tile what it is. A castle tile
    /// with no town behind it is a broken map, and the loaders warn about it where they meet it.
    /// </para>
    /// <para>
    /// <c>MP2::OBJ_CASTLE</c> 带动作位（<c>mp2.h:307</c>，35 + 128），城门那一格存的也是带位的那一个，
    /// 而城的其余格子（塔、背后山体）存的是不带位的 35。所以这里比的是「动作位去掉之后是城堡」，
    /// 拿裸的 <see cref="Mp2ObjectType.Castle"/> 比对会让每一座城的入口都判成「这里没有城」。
    /// </para>
    /// <para>
    /// The type is read with <c>ignoreObjectUnderHero: false</c> like the reference: a hero standing on the
    /// gate has hidden it behind <see cref="Mp2ObjectType.ActionHero"/>, and the entrance must still be found.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Tile index.</param>
    /// <returns><see langword="true"/> when the tile is the entrance of a town.</returns>
    public bool IsValidCastleEntrance(int tileIndex) =>
        (uint)tileIndex < (uint)_tiles.Count &&
        Mp2ObjectTypes.NonActionType(GetTile(tileIndex).GetMainObjectType(this, false)) == Mp2ObjectType.Castle;

    /// <summary>
    /// Whether the map has water next to a town, <c>Castle::HasSeaAccess()</c> (<c>castle.cpp:1836-1875</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three tiles are tried, in this order: the one below the town's bottom row and then its left and right
    /// neighbours. A tile counts when it is water <b>and</b> a boat could be moored there — nothing on it, or only
    /// something a hero could pick up, or a boat, or something drawn below the object layer like a shadow. That is
    /// exactly the rule the reference spells out with <c>isPickupObject()</c> and
    /// <c>isPassabilityTransparent()</c>; it exists so a town on a lake gets a shipyard only when a boat can
    /// actually leave it.
    /// </para>
    /// <para>
    /// The reference is a method of the castle and reaches the world through the global; the port keeps the scan on
    /// the world, and the castle holds the answer in <see cref="CastleTown.HasSeaAccess"/>.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Tile index of the town's entrance.</param>
    /// <returns><see langword="true"/> when the town can build and use a shipyard.</returns>
    public bool DoesCastleHaveSeaAccess(int tileIndex)
    {
        int x = tileIndex % Width;
        int y = tileIndex / Width + 2;

        if (!IsInsideMap(x, y))
        {
            // No tile below the town means no water can be reached: skip the other two probes, as the reference does.
            return false;
        }

        int index = y * Width + x;

        if (AllowsBoat(index))
        {
            return true;
        }

        return (IsInsideMap(x - 1, y) && AllowsBoat(index - 1))
            || (IsInsideMap(x + 1, y) && AllowsBoat(index + 1));

        bool AllowsBoat(int probe)
        {
            Tile tile = GetTile(probe);

            if (!tile.IsWater)
            {
                return false;
            }

            if (tile.MainObjectPart.IcnType == Mp2ObjectIcnType.Unknown)
            {
                // Nothing is drawn as this tile's main object: only shadows or pieces of other objects are here.
                return true;
            }

            Mp2ObjectType type = tile.MainObjectType;

            return Mp2ObjectTypes.IsPickupObject(type) || type == Mp2ObjectType.Boat || tile.IsPassabilityTransparent;
        }
    }

    /// <summary>
    /// Turns the town table of a map file into <see cref="CastleTown"/> objects, the <c>MP2::OBJ_CASTLE</c> loop of
    /// <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:304-355</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file's table has a fixed 72 entries and empty ones are not returned by the parser, so every position here
    /// is a town. The race of the record is only a provisional answer: a random town's real race comes from its
    /// information block, and a town without a block keeps this one.
    /// </para>
    /// <para>
    /// The reference does not validate the coordinates — a bad table entry would build a town outside the map. The
    /// port warns and skips instead of leaving a town whose footprint indexes past the tile vector.
    /// </para>
    /// <para>
    /// <b>The capture table is filled here</b>, not with the blocks: a town is capturable from the moment it exists,
    /// and its colour is the neutral one until a block says otherwise.
    /// </para>
    /// </remarks>
    /// <param name="map">The map being loaded.</param>
    private void CreateMapCastles(Mp2Map map)
    {
        foreach (Mp2CastlePosition position in map.Castles)
        {
            if (!IsInsideMap(position.X, position.Y))
            {
                GameLog.Warn($"Map castle at [{position.X}, {position.Y}] is outside the {Width}x{Height} map, skipped.");

                continue;
            }

            int tileIndex = position.Y * Width + position.X;

            var castle = new CastleTown(tileIndex, position.Race, map.Info.Version)
            {
                HasSeaAccess = DoesCastleHaveSeaAccess(tileIndex),
            };

            _castles.Add(castle);

            _capturedObjects.Set(tileIndex, Mp2ObjectType.Castle, PlayerColor.None);
        }
    }

    /// <summary>
    /// Registers the capturable objects of a map file, the mine loop of <c>World::LoadMapMP2()</c>
    /// (<c>world_loadmap.cpp:382-430</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table is the list of mines and similar objects the map marks as owned: the sawmill (<c>0x00</c>), the
    /// alchemist lab (<c>0x01</c>), the five mines (<c>0x02…0x06</c>), the lighthouse (<c>0x64</c>), the dragon city
    /// (<c>0x65</c>) and the abandoned mine (<c>0x67</c>). The parser has already turned the byte into an object
    /// type (<see cref="Mp2CaptureObject.ObjectType"/>), so only the entry itself is left; an unknown byte becomes
    /// <see cref="Mp2ObjectType.None"/> and is skipped with a warning, like the reference.
    /// </para>
    /// <para>
    /// The reference registers the town table right before this loop with the same neutral owner, and the answer to
    /// "who owns the mine" is completed later by the information blocks, which may name a colour
    /// (<c>world_loadmap.cpp:498</c>). The port keeps the same split: a mine is owned from the moment it exists, and
    /// its colour follows when a block says so.
    /// </para>
    /// </remarks>
    /// <param name="map">The map being loaded.</param>
    private void ReadCaptureObjects(Mp2Map map)
    {
        foreach (Mp2CaptureObject captureObject in map.CaptureObjects)
        {
            if (!IsInsideMap(captureObject.X, captureObject.Y))
            {
                GameLog.Warn(
                    $"Map capture object at [{captureObject.X}, {captureObject.Y}] is outside the {Width}x{Height} map, skipped.");

                continue;
            }

            if (captureObject.ObjectType == Mp2ObjectType.None)
            {
                GameLog.Warn(
                    $"Map capture object of type 0x{captureObject.RawType:X2} at [{captureObject.X}, {captureObject.Y}] is not known, skipped.");

                continue;
            }

            _capturedObjects.Set(captureObject.Y * Width + captureObject.X, captureObject.ObjectType, PlayerColor.None);
        }
    }

    /// <summary>
    /// Gives every town without a name one of the default names, <c>updateCastleNames()</c>
    /// (<c>world_loadmap.cpp:92-111</c>).
    /// </summary>
    /// <remarks>
    /// The set of taken names starts with the names the map itself gave its towns, so a hand named
    /// "Blackridge" keeps it and a generated name can never collide with it.
    /// </remarks>
    private void UpdateCastleNames()
    {
        List<CastleTown> unnamed = new();
        HashSet<string> taken = new(StringComparer.Ordinal);

        foreach (CastleTown castle in _castles)
        {
            if (string.IsNullOrEmpty(castle.Name))
            {
                unnamed.Add(castle);
            }
            else
            {
                taken.Add(castle.Name);
            }
        }

        foreach (CastleTown castle in unnamed)
        {
            castle.SetName(taken);
            taken.Add(castle.Name);
        }
    }

    /// <summary>
    /// Reads a town block into the town it belongs to, the two castle branches of the block loop
    /// (<c>world_loadmap.cpp:596-641</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two branches differ in one thing only: a built castle is found by its entrance tile, a random one by any
    /// tile of its footprint, because the file marks a random town's tiles with a random object type instead of a
    /// castle one. After the block is read the random town is turned into the town it rolled: its sprite is repainted
    /// for the race and the castle flag, and its tiles stop claiming to be random.
    /// </para>
    /// <para>
    /// A missing town, a wrong block size and a random tile that is not a random town are all map defects; each is
    /// warned about and skipped, where the reference asserts.
    /// </para>
    /// <para>
    /// The difficulty reaches the block because a town without custom buildings rolls its defaults, and the roll
    /// depends on it (<c>castle.cpp:542</c> asks <c>Game::getDifficulty()</c>, which the port records in
    /// <c>World._difficulty</c>). The race of an owned random town would be asked of the owner's kingdom — the port
    /// has no kingdom at load time, so the block rolls a random race for such a town exactly as it does for a
    /// neutral one.
    /// </para>
    /// </remarks>
    /// <param name="block">The information block of the town.</param>
    /// <param name="tileIndex">Tile the block belongs to.</param>
    /// <param name="isRandom">Whether the tile is a random town or castle.</param>
    private void ReadCastleBlock(Mp2InfoBlock block, int tileIndex, bool isRandom)
    {
        if (block.Data.Length != CastleTown.Mp2BlockSize)
        {
            GameLog.Warn($"Map castle block {block.ObjectId} at tile {tileIndex}: {block.Data.Length} bytes instead of {CastleTown.Mp2BlockSize}, skipped.");

            return;
        }

        CastleTown? castle = isRandom ? GetCastle(tileIndex) : GetCastleEntrance(tileIndex);

        if (castle is null)
        {
            GameLog.Warn($"Map castle block {block.ObjectId} at tile {tileIndex}: no town stands there, skipped.");

            return;
        }

        castle.LoadFromMP2(block.Data, _difficulty, ownerRace: null, generator: null, _day);

        if (isRandom)
        {
            UpdateCastleSprite(castle.TileIndex, castle.Race, castle.IsCastle, true);
            ReplaceRandomCastleObjectId(castle.TileIndex);
        }

        _capturedObjects.SetColor(tileIndex, castle.Color);
    }

    /// <summary>
    /// Stops the tiles of a random town from claiming to be random, <c>Maps::ReplaceRandomCastleObjectId()</c>
    /// (<c>maps.cpp:717-732</c>).
    /// </summary>
    /// <remarks>
    /// Only the object type changes; the sprite was already repainted by <see cref="UpdateCastleSprite"/>. The
    /// entrance is set last and unconditionally, so a town whose gate was not marked random still ends up with a
    /// castle type.
    /// </remarks>
    /// <param name="centerTileIndex">Tile index of the town's entrance.</param>
    public void ReplaceRandomCastleObjectId(int centerTileIndex)
    {
        int centerX = centerTileIndex % Width;
        int centerY = centerTileIndex / Width;

        for (int y = -3; y < 2; ++y)
        {
            for (int x = -2; x < 3; ++x)
            {
                if (!IsInsideMap(centerX + x, centerY + y))
                {
                    // The reference calls getTile() with the absolute point, which asserts; a town at the map edge
                    // would leave the map here, so the port skips the tile instead.
                    continue;
                }

                Tile tile = GetTile((centerY + y) * Width + centerX + x);

                if (tile.MainObjectType is Mp2ObjectType.RandomCastle or Mp2ObjectType.RandomTown)
                {
                    tile.SetMainObjectType(Mp2ObjectType.Castle);
                }
            }
        }

        GetTile(centerTileIndex).SetMainObjectType(Mp2ObjectType.Castle);
    }

    /// <summary>
    /// Repaints a town's forty-two sprites for its race and its castle flag,
    /// <c>Maps::UpdateCastleSprite()</c> (<c>maps.cpp:734-819</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A town is drawn from three ICN sets: sixteen <c>OBJNTOWN</c> tiles for the buildings, sixteen
    /// <c>OBJNTWSH</c> shadows to their left and ten <c>OBJNTWBA</c> base tiles. A random town is drawn from
    /// <c>OBJNTWRD</c> instead, and the repaint is a sprite swap: <see cref="Tile.ReplaceObject"/> hands the parts
    /// the new ICN and image indices, while a town that was only placed (not randomized) keeps its ICN and just moves
    /// sixteen images up or down. The layout, from the reference:
    /// </para>
    /// <code>
    /// Shadows (OBJNTWSH)  Castle (OBJNTOWN)
    ///                           0
    ///    32 33 34 35       1  2  3  4  5
    /// 36 37 38 39 40       6  7  8  9 10
    ///    41 42 43 44      11 12 13 14 15
    ///       45 46 47
    /// </code>
    /// <para>
    /// <b>The top tile needs a second fix</b>: <c>OBJNTWRD</c> index 0 is a different picture from
    /// <c>OBJNTOWN</c> index 0, so the top level part of the tile above the gate is repointed by hand to
    /// <c>fullTownIndex - 16</c>. Without it the random town keeps a piece of its old sprite.
    /// </para>
    /// <para>
    /// <paramref name="isRandom"/> is also checked against the entrance tile, as the reference does: a repaint of a
    /// tile that is not a random town means the caller is confused, so it warns and stops.
    /// </para>
    /// </remarks>
    /// <param name="centerTileIndex">Tile index of the town's entrance.</param>
    /// <param name="race">The race whose sprites to use; anything unknown falls back to the knight graphics.</param>
    /// <param name="isCastle">Whether the town has a fort, which picks the first or second half of the sixteen tiles.</param>
    /// <param name="isRandom">Whether the town was a random town, i.e. whether its sprites are <c>OBJNTWRD</c>.</param>
    public void UpdateCastleSprite(int centerTileIndex, MapRace race, bool isCastle, bool isRandom = false)
    {
        Tile entrance = GetTile(centerTileIndex);
        uint castleUid = entrance.MainObjectPart.Uid;

        // A map file marks the gate of a random town with the action type — the reference compares against
        // MP2::OBJ_RANDOM_CASTLE / MP2::OBJ_RANDOM_TOWN, both of which carry the action bit
        // (maps.cpp:753) — while a town placed by the editor or built by a test may carry the non action
        // twin, so both spellings are accepted.
        if (isRandom && entrance.MainObjectType is not (Mp2ObjectType.RandomCastle or Mp2ObjectType.RandomTown
            or Mp2ObjectType.ActionRandomCastle or Mp2ObjectType.ActionRandomTown))
        {
            GameLog.Warn($"Tile {centerTileIndex} is not a random town, its sprite was not updated.");

            return;
        }

        byte raceIndex = race switch
        {
            MapRace.Barbarian => 1,
            MapRace.Sorceress => 2,
            MapRace.Warlock => 3,
            MapRace.Wizard => 4,
            MapRace.Necromancer => 5,
            _ => 0,
        };

        (int X, int Y)[] castleTiles =
        [
            (0, -3), (-2, -2), (-1, -2), (0, -2), (1, -2), (2, -2), (-2, -1), (-1, -1),
            (0, -1), (1, -1), (2, -1), (-2, 0), (-1, 0), (0, 0), (1, 0), (2, 0),
        ];

        (int X, int Y)[] shadowTiles =
        [
            (-4, -2), (-3, -2), (-2, -2), (-1, -2), (-5, -1), (-4, -1), (-3, -1), (-2, -1),
            (-1, -1), (-4, 0), (-3, 0), (-2, 0), (-1, 0), (-3, 1), (-2, 1), (-1, 1),
        ];

        byte indexOffset = (byte)(isCastle ? 0 : 16);
        int centerX = centerTileIndex % Width;
        int centerY = centerTileIndex / Width;

        for (byte index = 0; index < 16; ++index)
        {
            byte fullTownIndex = (byte)(index + indexOffset + (raceIndex * 32));
            byte lookupId = isRandom ? (byte)(index + indexOffset) : fullTownIndex;

            int castleTile = (centerY + castleTiles[index].Y) * Width + centerX + castleTiles[index].X;

            if (IsInsideMap(centerX + castleTiles[index].X, centerY + castleTiles[index].Y))
            {
                Tile tile = GetTile(castleTile);

                if (isRandom)
                {
                    tile.ReplaceObject(castleUid, Mp2ObjectIcnType.ObjnTwrd, Mp2ObjectIcnType.ObjnTown, lookupId, fullTownIndex);
                }
                else
                {
                    tile.UpdateObjectImageIndex(castleUid, Mp2ObjectIcnType.ObjnTown, -16);
                }

                if (index == 0)
                {
                    RepointTopTile(tile, castleUid, fullTownIndex);
                }
            }

            int shadowTile = (centerY + shadowTiles[index].Y) * Width + centerX + shadowTiles[index].X;

            if (IsInsideMap(centerX + shadowTiles[index].X, centerY + shadowTiles[index].Y))
            {
                Tile tile = GetTile(shadowTile);

                if (isRandom)
                {
                    tile.ReplaceObject(castleUid, Mp2ObjectIcnType.ObjnTwrd, Mp2ObjectIcnType.ObjnTwsh, (byte)(lookupId + 32), fullTownIndex);
                }
                else
                {
                    tile.UpdateObjectImageIndex(castleUid, Mp2ObjectIcnType.ObjnTwsh, -16);
                }
            }
        }

        // The top level part of the tile above the gate keeps a copy of the tile's main sprite, and the reference
        // repoints it by hand because replaceObject() only walks the ground level parts.
        void RepointTopTile(Tile tile, uint uid, byte fullTown)
        {
            List<ObjectPart> parts = tile.TopObjectParts;
            int position = parts.FindIndex(part => part.Uid == uid);

            if (position < 0 || parts[position].IcnType != Mp2ObjectIcnType.ObjnTwrd)
            {
                return;
            }

            ObjectPart part = parts[position];

            part.IcnType = Mp2ObjectIcnType.ObjnTown;
            part.IcnIndex = (byte)(fullTown - 16);
            parts[position] = part;
        }
    }

    /// <summary>Whether a point is inside the map, <c>Maps::isValidAbsPoint()</c>.</summary>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <returns><see langword="true"/> when both coordinates are inside the map.</returns>
    private bool IsInsideMap(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;
}
