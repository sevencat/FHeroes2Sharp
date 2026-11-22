using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Reading and writing a ground event, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>MapEvent</c>
/// (<c>maps_objects.cpp:344-376</c>).
/// </summary>
/// <remarks>
/// <para>
/// The order is the position and the UID, then the resources, the artifact, the two flags, the player colours, the
/// message, the secondary skill and the experience. The colours are a <c>PlayerColorsSet</c>, which is the
/// <c>uint8_t</c> behind <c>PlayerColor</c> (<c>color.h:44</c>) — but before 1.1.09 they were written as an
/// <c>int32_t</c>, so an older save has four bytes to swallow for the low one.
/// </para>
/// <para>
/// The skill and the experience were added to the format in 1.1.06 (<c>maps_objects.cpp:366-373</c>): a reader of an
/// older save must set them to the empty defaults instead of reading them, so the gate is a gate on reading only.
/// The reference writes them unconditionally.
/// </para>
/// <para>
/// The artifact is written the way <c>Artifact</c> writes itself, <c>id</c> then <c>ext</c>
/// (<c>artifact.cpp:510-513</c>). The port has no <c>ext</c> — it is the spell of a spell scroll — so it writes a zero
/// and skips the field on the way back.
/// </para>
/// </remarks>
public sealed partial class MapEvent
{
    /// <summary>
    /// Writes the event, <c>operator&lt;&lt;</c> (<c>maps_objects.cpp:344</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn the tile index into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        WritePosition(stream, geometry);
        Resources.WriteTo(stream);
        stream.Write((int)Artifact);
        stream.Write(0);
        stream.Write(IsComputerPlayerAllowed);
        stream.Write(IsSingleTimeEvent);
        stream.Write((byte)Colors);
        stream.Write(Message);
        stream.Write((int)SecondarySkill.Skill);
        stream.Write((int)SecondarySkill.Level);
        stream.Write(Experience);
    }

    /// <summary>
    /// Reads an event, <c>operator&gt;&gt;</c> (<c>maps_objects.cpp:350</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn the point into a tile index.</param>
    /// <returns>The event; a stream that failed on the way leaves the defaults and its <c>Fail</c> flag set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal static MapEvent ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        (int tileIndex, uint uid) = ReadPosition(stream, version, geometry);

        Funds resources = Funds.ReadFrom(stream);
        ArtifactType artifact = (ArtifactType)stream.ReadInt32();

        // ext, the spell of a spell scroll; dumped, like the ultimate artifact does.
        stream.ReadInt32();

        bool isComputerPlayerAllowed = stream.ReadBool();
        bool isSingleTimeEvent = stream.ReadBool();
        PlayerColor colors = version < SaveFileFormat.Release1_1_09
            ? (PlayerColor)stream.ReadInt32()
            : (PlayerColor)stream.ReadUInt8();
        string message = stream.ReadString();

        SecondarySkillLevel secondarySkill = default;
        int experience = 0;

        if (version >= SaveFileFormat.Release1_1_06)
        {
            SecondarySkill skill = (SecondarySkill)stream.ReadInt32();
            SkillLevel level = (SkillLevel)stream.ReadInt32();

            secondarySkill = new SecondarySkillLevel(skill, level);
            experience = stream.ReadInt32();
        }

        MapEvent mapEvent = new()
        {
            Resources = resources,
            Artifact = artifact,
            IsComputerPlayerAllowed = isComputerPlayerAllowed,
            IsSingleTimeEvent = isSingleTimeEvent,
            Colors = colors,
            Message = message,
            SecondarySkill = secondarySkill,
            Experience = experience,
        };

        mapEvent.PlaceOn(tileIndex, uid);

        return mapEvent;
    }
}
