using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Reading and writing one hero, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Heroes</c>
/// (<c>heroes.cpp:2560-2646</c>).
/// </summary>
/// <remarks>
/// <para>
/// Everything a <c>HeroBase</c> holds comes first — primary skills, position, modes, spell and move points, the spell
/// book and the artifact bag (<c>heroes_base.cpp:618-680</c>) — then the hero alone: name, colour, experience, secondary
/// skills, army, id, portrait, race, the object under him, the route, facing, sprite, patrol centre and distance, the
/// visit list and the ground region.
/// </para>
/// <para>
/// <b>The primary skills are written attack, defence, knowledge, power.</b> Knowledge before power is the reference's
/// order, not the constructor's and not the panel's; reading them in the obvious order swaps two stats of every hero in
/// the game and a round trip never notices.
/// </para>
/// <para>
/// Four gates decide the older layouts: the object under the hero was an <c>int32_t</c> before 1.0.09-pre1 and a
/// <c>uint8_t</c> before 1.1.00-pre3; the patrol centre was two <c>int16_t</c> before 1.0.10; and before 1.1.00-pre1 an
/// empty name was not a name at all, so the reference filled in the hero's default one
/// (<c>heroes.cpp:2584-2591</c>).
/// </para>
/// <para>
/// <b>1.0.10 moved <c>UNKNOWN</c></b>: it was 72 and became 0, so every older id — the hero's and the portrait's — is one
/// higher than the file says, and 72 itself becomes <c>UNKNOWN</c> (<c>heroes.cpp:2593-2604</c>).
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// Creates a hero that holds nothing, for a save file's placeholder slot.
    /// </summary>
    /// <remarks>
    /// The reference's <c>AllHeroes</c> holds a real object in every one of its slots, including the unknown hero and the
    /// ones the map never placed, and a save file writes each of them. The port's slots are <see langword="null"/>
    /// instead, so reading such a slot needs an object to read into and writing one needs an object to write from.
    /// </remarks>
    internal Hero()
    {
        _army.Commander = this;
    }

    /// <summary>Writes the hero, <c>heroes.cpp:2560</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn a position into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // HeroBase (heroes_base.cpp:618-627). Knowledge goes before power.
        stream.Write(_attack);
        stream.Write(_defense);
        stream.Write(_knowledge);
        stream.Write(_power);

        // The hero's UID is not used by the port: heroes are found by id.
        MapObject.WritePosition(stream, MapIndex, 0, geometry);

        stream.Write((uint)_modes);
        stream.Write(_spellPoints);
        stream.Write(_movePoints);
        _spellBook.WriteTo(stream);
        _bagArtifacts.WriteTo(stream);

        // Heroes (heroes.cpp:2569-2570).
        stream.Write(_customName);
        stream.Write((byte)Color);
        stream.Write(_experience);
        _secondarySkills.WriteTo(stream);
        _army.WriteTo(stream);
        stream.Write(Id);
        stream.Write(PortraitId);
        stream.Write((int)Race);
        stream.Write((ushort)_objectTypeUnderHero);
        _path.WriteTo(stream);
        stream.Write((int)_direction);
        stream.Write(_spriteIndex);
        WritePatrolCenter(stream, geometry);
        stream.Write(PatrolDistance);
        WriteVisitedObjects(stream);

        // The ground region is not tracked by the port.
        stream.Write(0u);
    }

    /// <summary>Reads the hero, <c>heroes.cpp:2573</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn a point into a position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // HeroBase (heroes_base.cpp:630-645). Knowledge goes before power.
        _attack = stream.ReadInt32();
        _defense = stream.ReadInt32();
        _knowledge = stream.ReadInt32();
        _power = stream.ReadInt32();

        MapIndex = MapObject.ReadPosition(stream, version, geometry).TileIndex;

        _modes = (HeroModes)stream.ReadUInt32();
        _spellPoints = stream.ReadUInt32();
        _movePoints = stream.ReadUInt32();
        _spellBook.ReadFrom(stream);
        _bagArtifacts.ReadFrom(stream);

        // Heroes (heroes.cpp:2582).
        _customName = stream.ReadString();
        Color = (PlayerColor)stream.ReadUInt8();
        _experience = stream.ReadUInt32();
        _secondarySkills.ReadFrom(stream);
        _ = _army.ReadFrom(stream, version);
        Id = stream.ReadInt32();
        PortraitId = stream.ReadInt32();
        Race = (MapRace)stream.ReadInt32();

        if (version < SaveFileFormat.PreRelease1_1_00 && _customName.Length == 0)
        {
            // Before 1.1.00-pre1 an empty name was not checked, and a hero without a name cannot be drawn
            // (heroes.cpp:2584-2591).
            _customName = HeroNames.Get(Id);
        }

        if (version < SaveFileFormat.Release1_0_10)
        {
            // UNKNOWN was 72 before 1.0.10 (heroes.cpp:2593-2604).
            if (Id == 72)
            {
                Id = UnknownId;
                PortraitId = UnknownId;
            }
            else
            {
                ++Id;
                ++PortraitId;
            }
        }

        if (version < SaveFileFormat.PreRelease1_1_0_09)
        {
            _objectTypeUnderHero = (Mp2ObjectType)stream.ReadInt32();
        }
        else if (version < SaveFileFormat.PreRelease3_1_1_00)
        {
            _objectTypeUnderHero = (Mp2ObjectType)stream.ReadUInt8();
        }
        else
        {
            _objectTypeUnderHero = (Mp2ObjectType)stream.ReadUInt16();
        }

        _path.ReadFrom(stream, version, geometry);
        _direction = (Direction)stream.ReadInt32();
        _spriteIndex = stream.ReadInt32();
        ReadPatrolCenter(stream, version, geometry);
        PatrolDistance = stream.ReadInt32();
        ReadVisitedObjects(stream, version);

        // The ground region: not tracked by the port.
        stream.ReadUInt32();

        // The army points back at its commander after the color has been read (heroes.cpp:2643).
        _army.Commander = this;
    }

    /// <summary>Writes the patrol centre, a point since 1.0.10 and two words before it.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn a position into a point.</param>
    private void WritePatrolCenter(StreamBase stream, MapGeometry geometry)
    {
        if (PatrolCenterIndex < 0)
        {
            stream.Write(-1);
            stream.Write(-1);
            return;
        }

        var point = geometry.GetPoint(PatrolCenterIndex);

        stream.Write(point.X);
        stream.Write(point.Y);
    }

    /// <summary>Reads the patrol centre, a point since 1.0.10 and two words before it.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn a point into a position.</param>
    private void ReadPatrolCenter(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        short x;
        short y;

        if (version < SaveFileFormat.Release1_0_10)
        {
            x = stream.ReadInt16();
            y = stream.ReadInt16();
        }
        else
        {
            x = (short)stream.ReadInt32();
            y = (short)stream.ReadInt32();
        }

        PatrolCenterIndex = x < 0 || y < 0 ? -1 : geometry.GetIndexFromAbsPoint(x, y);
    }

    /// <summary>Writes the objects the hero has visited, in index order.</summary>
    /// <remarks>
    /// The reference keeps them in a <c>std::set&lt;IndexObject&gt;</c>, which writes ascending by index. The port's list
    /// is in insertion order, so it is sorted here: the same visits must produce the same bytes, otherwise a save file
    /// would depend on the order the player happened to walk the map.
    /// </remarks>
    /// <param name="stream">The stream to write to.</param>
    private void WriteVisitedObjects(StreamBase stream)
    {
        stream.Write((uint)_visitedObjects.Count);

        foreach (IndexObject visited in _visitedObjects.OrderBy(static entry => entry.Index))
        {
            stream.Write(visited.Index);
            stream.Write((ushort)visited.Object);
        }
    }

    /// <summary>Reads the objects the hero has visited.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    private void ReadVisitedObjects(StreamBase stream, SaveFileFormat version)
    {
        uint count = stream.ReadUInt32();

        _visitedObjects.Clear();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            int index = stream.ReadInt32();

            // IndexObject's type is a word, but it was a full int32 before 1.1.03-pre1 (pairs.h:72-90).
            Mp2ObjectType objectType = version < SaveFileFormat.PreRelease1_1_1_03
                ? (Mp2ObjectType)stream.ReadInt32()
                : (Mp2ObjectType)stream.ReadUInt16();

            _visitedObjects.Add(new IndexObject(index, objectType));
        }
    }
}
