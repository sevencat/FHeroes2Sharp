using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Reading and writing one tile, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Maps::Tile</c>
/// (<c>maps_tiles.cpp:1910-1964</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>What is stored and what is not.</b> The index, the terrain image, the terrain flags, the passability
/// directions, the main object part and type, the fog colours, the three metadata words, the hero standing on the
/// tile, the road mark, both object part lists and the boat owner. The fog <i>direction</i> and the region are not
/// stored: they are derived again by <c>World::PostLoad</c> from the fog colours and the map geometry
/// (<c>maps_tiles.h:424-427</c>).
/// </para>
/// <para>
/// <b>Three width traps.</b> <c>_tilePassabilityDirections</c> is <c>uint16_t</c> in the file while the port keeps it
/// as a 32 bit <see cref="Direction"/>; <c>_mainObjectType</c> was a <c>uint8_t</c> before 1.1.00 and is a
/// <c>uint16_t</c> after; the fog colours and the boat owner are single bytes. All four are cast explicitly so that the
/// port's enum widths cannot leak into the file.
/// </para>
/// <para>
/// <b>The metadata block.</b> The reference serialises <c>std::array&lt;uint32_t, 3&gt;</c>, and that operator writes
/// the element count before the elements (<c>serialize.h:409-415</c>), so the block is sixteen bytes, not twelve. A
/// reader that finds a count other than three must fail the stream rather than resize anything.
/// </para>
/// <para>
/// <b>The 1.1.08 mine repair lives in <see cref="World"/>.</b> Before 1.1.08 a mine whose ICN type is not
/// <c>EXTRAOVR</c> is passed through <c>setInitialObjectInfo()</c>, which invents the resource of a "hacked" mine
/// (<c>maps_tiles.cpp:1956-1961</c>). The port's method needs the world (its random branches do), so the gate is
/// applied right after the tile loop of <see cref="World.ReadFrom(StreamBase, SaveFileFormat)"/> instead of here.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>How many metadata words a tile carries (<c>maps_tiles.h:409</c>).</summary>
    public const int MetadataCount = 3;

    /// <summary>
    /// Writes the tile, <c>operator&lt;&lt;</c> (<c>maps_tiles.cpp:1910</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(_index);
        stream.Write(_terrainImageIndex);
        stream.Write(_terrainFlags);
        stream.Write((ushort)_tilePassabilityDirections);
        _mainObjectPart.WriteTo(stream);
        stream.Write((ushort)_mainObjectType);
        stream.Write((byte)_fogColors);

        stream.Write((uint)MetadataCount);

        foreach (uint word in _metadata)
        {
            stream.Write(word);
        }

        stream.Write(_occupantHeroId);
        stream.Write(_isTileMarkedAsRoad);

        stream.WriteList(_groundObjectParts, static (target, part) => part.WriteTo(target));
        stream.WriteList(_topObjectParts, static (target, part) => part.WriteTo(target));

        stream.Write((byte)_boatOwnerColor);
    }

    /// <summary>
    /// Reads a tile, <c>operator&gt;&gt;</c> (<c>maps_tiles.cpp:1917</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <returns>
    /// The tile; when the stream fails on the way the result holds whatever was read before the failure and the
    /// stream's <c>Fail</c> flag stays set, so the caller decides whether that is fatal.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static Tile ReadFrom(StreamBase stream, SaveFileFormat version)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Tile tile = new();

        tile._index = stream.ReadInt32();
        tile._terrainImageIndex = stream.ReadUInt16();
        tile._terrainFlags = stream.ReadUInt8();
        tile._tilePassabilityDirections = (Direction)stream.ReadUInt16();

        // Before 1.1.04 the part was written field by field: the layer type went last and the image index sat right
        // after the two dropped bools, so the whole struct cannot be read at once.
        uint mainUid = 0;
        byte mainIcnType = 0;
        byte mainIcnIndex = 0;
        byte mainLayerType = 0;

        if (version < SaveFileFormat.Release1_1_04)
        {
            mainUid = stream.ReadUInt32();
            mainIcnType = stream.ReadUInt8();
        }
        else
        {
            tile._mainObjectPart = ObjectPart.ReadFrom(stream, version);
        }

        if (version < SaveFileFormat.PreRelease2_1_0_09)
        {
            stream.ReadBool();
            stream.ReadBool();
        }

        if (version < SaveFileFormat.Release1_1_04)
        {
            mainIcnIndex = stream.ReadUInt8();
        }

        tile._mainObjectType = version < SaveFileFormat.PreRelease3_1_1_00
            ? (Mp2ObjectType)stream.ReadUInt8()
            : (Mp2ObjectType)stream.ReadUInt16();

        tile._fogColors = (PlayerColor)stream.ReadUInt8();

        uint metadataCount = stream.ReadUInt32();

        if (metadataCount != MetadataCount)
        {
            // The reference zeroes the array and fails the stream (serialize.h:409-415); it never trusts the count.
            stream.SetFail();
        }

        for (int i = 0; i < MetadataCount; ++i)
        {
            tile._metadata[i] = stream.ReadUInt32();
        }

        tile._occupantHeroId = stream.ReadUInt8();
        tile._isTileMarkedAsRoad = stream.ReadBool();

        tile._groundObjectParts.Clear();
        tile._groundObjectParts.AddRange(stream.ReadList(source => ObjectPart.ReadFrom(source, version)));

        tile._topObjectParts.Clear();
        tile._topObjectParts.AddRange(stream.ReadList(source => ObjectPart.ReadFrom(source, version)));

        if (version < SaveFileFormat.Release1_1_04)
        {
            mainLayerType = stream.ReadUInt8();
            tile._mainObjectPart = new ObjectPart((ObjectLayerType)mainLayerType, mainUid, (Mp2ObjectIcnType)mainIcnType, mainIcnIndex);
        }

        tile._boatOwnerColor = (PlayerColor)stream.ReadUInt8();

        return tile;
    }
}
