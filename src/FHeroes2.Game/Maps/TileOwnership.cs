using System.Diagnostics;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The ownership flag of an object and the boat on a tile, the <c>Maps::Tile</c> half of
/// <c>maps_tiles.cpp:484-581</c> and <c>:1185-1303</c>.
/// </summary>
/// <remarks>
/// <para>
/// A captured object is marked twice in the reference: the <see cref="CapturedObject"/> of the world
/// remembers the color for the game logic, and a <c>FLAG32</c> object part is hung on the tile so the
/// player sees who owns it. This file is the second half — before it existed,
/// <see cref="World.CaptureObject"/> changed the color and left the sprite alone.
/// </para>
/// <para>
/// <b>The flag is not on one tile only.</b> A mine, a lighthouse, an alchemist's lab, a sawmill and a
/// castle draw their flag on the tile the sprite of the object actually covers, which for four of them is
/// a neighbour: the reference takes <c>_mainObjectPart._uid</c> as the flag's UID, so the flag is painted
/// with the sprite of the object no matter which tile carries it.
/// </para>
/// <para>
/// Every sprite index below is an offset into <c>FLAG32.ICN</c>: the six player colors occupy
/// <c>0..5</c>, <c>6</c> is the neutral flag, and each object adds its own base to that.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    // PlayerColor of the boat standing here, _boatOwnerColor (maps_tiles.h:424).
    private PlayerColor _boatOwnerColor = PlayerColor.None;

    /// <summary>
    /// The player the boat on this tile belongs to, <c>getBoatOwnerColor()</c>
    /// (<c>maps_tiles.h:220</c>).
    /// </summary>
    public PlayerColor BoatOwnerColor => _boatOwnerColor;

    /// <summary>
    /// Forgets the owner of the boat, <c>resetBoatOwnerColor()</c> (<c>maps_tiles.h:215</c>).
    /// </summary>
    /// <remarks>
    /// Called when the boat is taken out of the water — the hero boards it and it turns into a hero, so
    /// the tile must not remember a color for a boat that is no longer there.
    /// </remarks>
    public void ResetBoatOwnerColor() => _boatOwnerColor = PlayerColor.None;

    /// <summary>
    /// Puts a boat on the tile, <c>setBoat()</c> (<c>maps_tiles.cpp:484</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The object that was on the tile survives as a ground level part at the front of the list.</b>
    /// Boats are placed on a coast tile that may already carry a shadow or a piece of terrain, and the
    /// reference is explicit that the order matters for rendering — the main part is pushed to the
    /// front, not appended.
    /// </para>
    /// <para>
    /// <b>The direction selects the sprite, and the four left-hand ones are mirrored sprites</b>: their
    /// index has <c>128</c> added, which is how <c>BOAT32.ICN</c> marks the flipped frames. The same
    /// table is read back by <see cref="GetBoatDirection"/>.
    /// </para>
    /// <para>
    /// The reference asserts that the tile is water (a boat on land is a loader bug) and that the part
    /// is not passability transparent (a boat can never be walked over). Both are kept.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to, for the object UID.</param>
    /// <param name="direction">Direction the boat faces.</param>
    /// <param name="color">Owner of the boat.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public void SetBoat(World world, Direction direction, PlayerColor color)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown)
        {
            // Rendering order matters: the object that was here goes to the front of the ground parts.
            _groundObjectParts.Insert(0, _mainObjectPart);
        }

        Debug.Assert(IsWater, "A boat can only be placed on water.");

        SetMainObjectType(Mp2ObjectType.ActionBoat);
        world.ResetPathfinder();

        // Left-hand sprites are the flipped ones: their index carries the flip bit.
        var icnIndex = (byte)(direction switch
        {
            Direction.Top => 0,
            Direction.TopRight => 9,
            Direction.Right => 18,
            Direction.BottomRight => 27,
            Direction.Bottom => 36,
            Direction.BottomLeft => 27 + 128,
            Direction.Left => 18 + 128,
            Direction.TopLeft => 9 + 128,
            _ => 18,
        });

        _mainObjectPart = new ObjectPart(ObjectLayerType.Object, world.GetNewObjectUid(), Mp2ObjectIcnType.Boat32, icnIndex);

        Debug.Assert(!_mainObjectPart.IsPassabilityTransparent, "A boat must not be passability transparent.");

        _boatOwnerColor = color;
    }

    /// <summary>
    /// The direction the boat on this tile faces, <c>getBoatDirection()</c>
    /// (<c>maps_tiles.cpp:552</c>).
    /// </summary>
    /// <remarks>
    /// The exact inverse of the table in <see cref="SetBoat"/>: <c>0, 9, 18, 27, 36</c> and the same
    /// four values with the flip bit. A tile whose main part is not a boat answers
    /// <see cref="Direction.Unknown"/>, and so does a boat with an index outside the table — the
    /// reference only checks the ICN type before the switch.
    /// </remarks>
    /// <returns>The facing of the boat, or <see cref="Direction.Unknown"/>.</returns>
    public Direction GetBoatDirection()
    {
        if (_mainObjectPart.IcnType != Mp2ObjectIcnType.Boat32)
        {
            return Direction.Unknown;
        }

        return _mainObjectPart.IcnIndex switch
        {
            0 => Direction.Top,
            9 => Direction.TopRight,
            18 => Direction.Right,
            27 => Direction.BottomRight,
            36 => Direction.Bottom,
            27 + 128 => Direction.BottomLeft,
            18 + 128 => Direction.Left,
            9 + 128 => Direction.TopLeft,
            _ => Direction.Unknown,
        };
    }

    /// <summary>
    /// Paints the flag of the owner of the object, <c>setOwnershipFlag()</c>
    /// (<c>maps_tiles.cpp:1185</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The object type selects where the flag lands</b>, and the caller passes the type with the
    /// action bit, because that is what a tile stores (<c>MP2::OBJ_MINE</c> is
    /// <see cref="Mp2ObjectType.ActionMine"/>):
    /// </para>
    /// <list type="bullet">
    /// <item><description>a mine paints its flag on itself, on the upper layer;</description></item>
    /// <item><description>a lighthouse paints on the left neighbour and on itself;</description></item>
    /// <item><description>an alchemist's lab paints on the top neighbour;</description></item>
    /// <item><description>a sawmill paints on the top right neighbour;</description></item>
    /// <item><description>a castle paints on both the left and the right neighbour, two sprites per color.</description></item>
    /// </list>
    /// <para>
    /// <b>A neutral castle keeps its flags.</b> The reference replaces <see cref="PlayerColor.None"/> with
    /// its <c>UNUSED</c> color before painting, so both sides of the gate keep the neutral sprite
    /// (<c>12</c> and <c>13</c>) instead of losing the flag; that is why the castle branch below never
    /// deletes a flag.
    /// </para>
    /// <para>
    /// Every other object type is left alone, which is what makes this call safe for dwellings: they
    /// carry a color without drawing a flag.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the tile belongs to, for the neighbouring tiles.</param>
    /// <param name="objectType">Type of the object that was captured, action bit included.</param>
    /// <param name="color">
    /// New owner, or <see cref="PlayerColor.None"/> to take the flag down (a neutral castle keeps its
    /// flag, see the remarks).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The world has no map.</exception>
    public void SetOwnershipFlag(World world, Mp2ObjectType objectType, PlayerColor color)
    {
        ArgumentNullException.ThrowIfNull(world);

        MapGeometry geometry = world.Geometry ?? throw new InvalidOperationException("The world has no map yet.");

        byte spriteIndex = FlagSpriteIndexForColor(color);

        // The flag belongs to the object, so it carries the UID of the main part whichever tile it
        // is painted on.
        uint uid = _mainObjectPart.Uid;

        switch (objectType)
        {
        case Mp2ObjectType.ActionMine:
            UpdateFlag(color, (byte)(spriteIndex + 14), uid, true);
            break;

        case Mp2ObjectType.ActionLighthouse:
            if (geometry.IsValidDirection(Index, Direction.Left))
            {
                world.GetTile(geometry.GetDirectionIndex(Index, Direction.Left)).UpdateFlag(color, (byte)(spriteIndex + 49), uid, false);
            }

            UpdateFlag(color, (byte)(spriteIndex + 56), uid, false);
            break;

        case Mp2ObjectType.ActionAlchemistLab:
            if (geometry.IsValidDirection(Index, Direction.Top))
            {
                world.GetTile(geometry.GetDirectionIndex(Index, Direction.Top)).UpdateFlag(color, (byte)(spriteIndex + 21), uid, true);
            }

            break;

        case Mp2ObjectType.ActionSawmill:
            if (geometry.IsValidDirection(Index, Direction.TopRight))
            {
                world.GetTile(geometry.GetDirectionIndex(Index, Direction.TopRight)).UpdateFlag(color, (byte)(spriteIndex + 28), uid, true);
            }

            break;

        case Mp2ObjectType.ActionCastle:
            // A neutral castle keeps a flag on both sides of the gate: the reference paints them with
            // its UNUSED color, which reaches updateFlag() as "not none" and only selects the neutral
            // sprite. The port expresses that by skipping the deletion, which is the same thing.
            byte side = (byte)(spriteIndex * 2);

            if (geometry.IsValidDirection(Index, Direction.Left))
            {
                world.GetTile(geometry.GetDirectionIndex(Index, Direction.Left)).UpdateFlagSprite(side, uid, true);
            }

            if (geometry.IsValidDirection(Index, Direction.Right))
            {
                world.GetTile(geometry.GetDirectionIndex(Index, Direction.Right)).UpdateFlagSprite((byte)(side + 1), uid, true);
            }

            break;

        default:
            break;
        }
    }

    /// <summary>
    /// Puts, repaints or takes down one flag, <c>updateFlag()</c> (<c>maps_tiles.cpp:1282</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flag is found by the pair <paramref name="uid"/> + <c>FLAG32</c> and never by the sprite: one
    /// object has exactly one flag, and repainting it must not leave the old sprite behind. Only the
    /// ground level and the top level are searched — the main part is an object and never a flag.
    /// </para>
    /// <para>
    /// <see cref="PlayerColor.None"/> means "take the flag down", which is the only reason the color is
    /// needed at all.
    /// </para>
    /// </remarks>
    /// <param name="color">Owner to paint, or <see cref="PlayerColor.None"/> to remove the flag.</param>
    /// <param name="objectSpriteIndex">Sprite index inside <c>FLAG32.ICN</c>.</param>
    /// <param name="uid">UID of the object the flag belongs to.</param>
    /// <param name="setOnUpperLayer">
    /// <see langword="true"/> to append a new flag to the top level, <see langword="false"/> to append it
    /// to the ground level. An existing flag keeps the level it is on.
    /// </param>
    public void UpdateFlag(PlayerColor color, byte objectSpriteIndex, uint uid, bool setOnUpperLayer)
    {
        if (color == PlayerColor.None)
        {
            _groundObjectParts.RemoveAll(part => part.Uid == uid && part.IcnType == Mp2ObjectIcnType.Flag32);
            _topObjectParts.RemoveAll(part => part.Uid == uid && part.IcnType == Mp2ObjectIcnType.Flag32);

            return;
        }

        UpdateFlagSprite(objectSpriteIndex, uid, setOnUpperLayer);
    }

    /// <summary>
    /// Paints a flag without the "none removes it" rule of <see cref="UpdateFlag"/>.
    /// </summary>
    /// <remarks>
    /// The reference reaches this through its <c>UNUSED</c> color, which exists only to say "paint the
    /// neutral sprite". The port has no such color and passes the intent directly — it is what a neutral
    /// castle needs.
    /// </remarks>
    /// <param name="objectSpriteIndex">Sprite index inside <c>FLAG32.ICN</c>.</param>
    /// <param name="uid">UID of the object the flag belongs to.</param>
    /// <param name="setOnUpperLayer">Layer to append a new flag to.</param>
    private void UpdateFlagSprite(byte objectSpriteIndex, uint uid, bool setOnUpperLayer)
    {
        int groundIndex = _groundObjectParts.FindIndex(part => part.Uid == uid && part.IcnType == Mp2ObjectIcnType.Flag32);

        if (groundIndex >= 0)
        {
            ObjectPart part = _groundObjectParts[groundIndex];
            part.IcnIndex = objectSpriteIndex;
            _groundObjectParts[groundIndex] = part;

            return;
        }

        int topIndex = _topObjectParts.FindIndex(part => part.Uid == uid && part.IcnType == Mp2ObjectIcnType.Flag32);

        if (topIndex >= 0)
        {
            ObjectPart part = _topObjectParts[topIndex];
            part.IcnIndex = objectSpriteIndex;
            _topObjectParts[topIndex] = part;

            return;
        }

        var flag = new ObjectPart(ObjectLayerType.Object, uid, Mp2ObjectIcnType.Flag32, objectSpriteIndex);

        if (setOnUpperLayer)
        {
            _topObjectParts.Add(flag);
        }
        else
        {
            _groundObjectParts.Add(flag);
        }
    }

    /// <summary>
    /// The <c>FLAG32.ICN</c> sprite of a color, the first switch of <c>setOwnershipFlag()</c>
    /// (<c>maps_tiles.cpp:1191</c>).
    /// </summary>
    /// <remarks>
    /// The reference asserts on its <c>UNUSED</c> color and on anything it does not know. The port falls
    /// back to the neutral sprite after a debug assertion, because a color outside the six players cannot
    /// reach this call from the game logic.
    /// </remarks>
    /// <param name="color">Owner color.</param>
    /// <returns>Sprite index of the flag inside <c>FLAG32.ICN</c>, <c>0..6</c>.</returns>
    private static byte FlagSpriteIndexForColor(PlayerColor color) =>
        color switch
        {
            PlayerColor.None => 6,
            PlayerColor.Blue => 0,
            PlayerColor.Green => 1,
            PlayerColor.Red => 2,
            PlayerColor.Yellow => 3,
            PlayerColor.Orange => 4,
            PlayerColor.Purple => 5,
            _ => NeutralFlagSprite(color),
        };

    private static byte NeutralFlagSprite(PlayerColor color)
    {
        Debug.Assert(false, $"A flag cannot be painted for the color {color}.");

        return 6;
    }
}
