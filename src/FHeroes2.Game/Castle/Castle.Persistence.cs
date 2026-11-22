using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Army;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Castle;

/// <summary>
/// Reading and writing one town, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Castle</c>
/// (<c>castle.cpp:2411-2460</c>).
/// </summary>
/// <remarks>
/// <para>
/// Position, modes, race, the two building bitmaps, the captain, the colour, the name, the mage guild, the six dwelling
/// counts and the garrison.
/// </para>
/// <para>
/// <b>The captain is a whole hero base</b> (<see cref="Captain"/> carries the primary skills, spell points, spell book
/// and artifact bag of <c>HeroBase</c>, <c>heroes_base.cpp:618-680</c>) — a town therefore writes far more than its own
/// fields and reading it needs a captain to read into, which the town already holds.
/// </para>
/// <para>
/// <b>The port has no <c>modes</c> word</b>: it keeps <see cref="CanBuildToday"/> and <see cref="HasCustomArmy"/> as
/// flags of their own. The word is composed when writing and split again when reading; the other two bits the reference
/// keeps there (<c>BUILT</c> and <c>UNUSED_ALLOW_CASTLE_CONSTRUCTION</c>) are not used by the port.
/// </para>
/// <para>
/// Two gates: before 1.1.01 there is no disabled-buildings word at all and the reference derives it from
/// <c>UNUSED_ALLOW_CASTLE_CONSTRUCTION</c>, and before 1.1.09 the colour is an <c>int32_t</c>
/// (<c>castle.cpp:2431-2444</c>).
/// </para>
/// <para>
/// After reading, the garrison is bound back to the captain and takes the town's colour — what the reference's
/// <c>_postLoad()</c> does (<c>castle.cpp:2455-2458</c>). The captain in the port belongs to the town from construction,
/// so it is read into the captain the town already holds.
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>The <c>UNUSED_ALLOW_CASTLE_CONSTRUCTION</c> bit of the reference's <c>modes</c> word.</summary>
    private const uint ModeAllowCastleConstruction = 1 << 1;

    /// <summary>The <c>CUSTOM_ARMY</c> bit of the reference's <c>modes</c> word.</summary>
    private const uint ModeCustomArmy = 1 << 2;

    /// <summary>The <c>ALLOW_TO_BUILD_TODAY</c> bit of the reference's <c>modes</c> word.</summary>
    private const uint ModeAllowToBuildToday = 1 << 3;

    /// <summary>
    /// Creates a town without a place, for reading a save file's town into.
    /// </summary>
    /// <remarks>
    /// The position is the first field of a town in a save file, and the port's town cannot be moved once it exists
    /// (<see cref="TileIndex"/> is read-only outside this file) — so the reader makes a town first and fills in where it
    /// stands. Nothing is placed on the map by this constructor: <see cref="AllCastles.ReadFrom"/> adds it afterwards,
    /// once the file has said where it goes.
    /// </remarks>
    internal Castle()
        : this(0, MapRace.None)
    {
    }

    /// <summary>Writes the town, <c>castle.cpp:2411</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn a position into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        MapObject.WritePosition(stream, TileIndex, 0, geometry);

        uint modes = (CanBuildToday ? ModeAllowToBuildToday : 0) | (HasCustomArmy ? ModeCustomArmy : 0);

        stream.Write(modes);
        stream.Write((int)Race);
        stream.Write((uint)_constructedBuildings);
        stream.Write((uint)DisabledBuildings);
        _captain.WriteTo(stream, geometry);
        stream.Write((byte)Color);
        stream.Write(Name);
        _mageGuild.WriteTo(stream);

        stream.Write((uint)_dwelling.Length);

        foreach (uint dwelling in _dwelling)
        {
            stream.Write(dwelling);
        }

        _army.WriteTo(stream);
    }

    /// <summary>Reads the town, <c>castle.cpp:2427</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn a point into a position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        TileIndex = MapObject.ReadPosition(stream, version, geometry).TileIndex;

        uint modes = stream.ReadUInt32();

        CanBuildToday = (modes & ModeAllowToBuildToday) != 0;
        HasCustomArmy = (modes & ModeCustomArmy) != 0;

        Race = (MapRace)stream.ReadInt32();
        _constructedBuildings = (BuildingType)stream.ReadUInt32();

        if (version < SaveFileFormat.Release1_1_01)
        {
            // No word of its own before 1.1.01: a town that may still be built into a castle is written without one
            // (castle.cpp:2431-2436).
            DisabledBuildings = (modes & ModeAllowCastleConstruction) != 0 ? (BuildingType)0 : BuildingType.Castle;
        }
        else
        {
            DisabledBuildings = (BuildingType)stream.ReadUInt32();
        }

        _captain.ReadFrom(stream, version, geometry);

        Color = version < SaveFileFormat.Release1_1_09 ? (PlayerColor)stream.ReadInt32() : (PlayerColor)stream.ReadUInt8();
        Name = stream.ReadString();
        _mageGuild.ReadFrom(stream);

        uint dwellingCount = stream.ReadUInt32();

        if (dwellingCount != (uint)_dwelling.Length)
        {
            // A town with another number of dwellings cannot be laid out (castle.cpp:2448-2454).
            stream.SetFail();
            return;
        }

        for (int i = 0; i < _dwelling.Length && !stream.Fail; ++i)
        {
            _dwelling[i] = stream.ReadUInt32();
        }

        _ = _army.ReadFrom(stream, version);

        // _postLoad(): the garrison is the captain's and takes the town's colour (castle.cpp:2455-2458).
        _army.Race = Race;
        _army.Color = Color;
        _army.Commander = _captain;
    }
}
