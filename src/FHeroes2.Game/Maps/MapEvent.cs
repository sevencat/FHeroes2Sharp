using System.Buffers.Binary;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// A ground event: a spot a hero may step on that gives resources or an artifact, the reference's
/// <c>MapEvent</c> (<c>maps_objects.h:70</c>), read from its information block by
/// <c>MapEvent::LoadFromMP2()</c> (<c>maps_objects.cpp:44</c>).
/// </summary>
/// <remarks>
/// <para>
/// The block is: marker <c>0x01</c>, seven 32 bit resource amounts that may be negative, a 16 bit artifact
/// id, an "AI players too" flag, a "only once" flag, ten unused bytes, six color flags and a NUL terminated
/// message. The artifact counts from 0 in the file and from 1 in the engine, so the port adds one here,
/// like the reference does (<c>maps_objects.cpp:123</c>).
/// </para>
/// <para>
/// The reference keeps a <c>secondarySkill</c> and an <c>experience</c> field as well. The MP2 loader never
/// writes them — only the newer map format does, and a save carries them from 1.1.06 on — so reading an MP2
/// block leaves them at their empty defaults. The <c>isAllow()</c> and <c>SetVisited()</c> helpers are left for
/// the code that visits an event: nothing reads this object yet.
/// </para>
/// </remarks>
public sealed partial class MapEvent : MapObject
{
    /// <summary>Smallest block that can be an event, <c>MP2::MP2_EVENT_STRUCTURE_MIN_SIZE</c> (<c>mp2.h:37</c>).</summary>
    public const int MinimumStructureSize = 50;

    /// <summary>The first byte of a ground event block, <c>data[0] == 1</c> (<c>maps_objects.cpp:48</c>).</summary>
    private const byte EventMarker = 0x01;

    /// <summary>Offset of the wood amount; the seven amounts follow each other.</summary>
    private const int ResourceOffset = 1;

    /// <summary>Offset of the artifact id.</summary>
    private const int ArtifactOffset = 29;

    /// <summary>Offset of the "applies to AI players as well" flag.</summary>
    private const int ComputerPlayerOffset = 31;

    /// <summary>Offset of the "only once" flag.</summary>
    private const int SingleTimeOffset = 32;

    /// <summary>Offset of the six color flags: blue, green, red, yellow, orange, purple.</summary>
    private const int ColorOffset = 43;

    /// <summary>Offset of the message string.</summary>
    private const int MessageOffset = 49;

    private MapEvent()
    {
    }

    /// <summary>The resources the event gives, negative when it takes them away.</summary>
    public Funds Resources { get; private init; }

    /// <summary>The artifact the event gives; <see cref="ArtifactType.Unknown"/> for none.</summary>
    public ArtifactType Artifact { get; private init; }

    /// <summary>Whether the event also fires for computer players, <c>isComputerPlayerAllowed</c>.</summary>
    public bool IsComputerPlayerAllowed { get; private init; }

    /// <summary>Whether the event fires only once, <c>isSingleTimeEvent</c>.</summary>
    public bool IsSingleTimeEvent { get; private init; }

    /// <summary>The colors the event fires for; a bit set of <see cref="PlayerColor"/>.</summary>
    public PlayerColor Colors { get; private init; }

    /// <summary>The message of the event, empty when the block carries none.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// The secondary skill the event teaches, <c>secondarySkill</c> (<c>maps_objects.h:95</c>).
    /// </summary>
    /// <remarks>
    /// Only the newer map format and the save format fill this; an MP2 ground event leaves it at the empty
    /// <see cref="SecondarySkillLevel"/> whose <see cref="SecondarySkillLevel.IsValid"/> is <see langword="false"/>.
    /// </remarks>
    public SecondarySkillLevel SecondarySkill { get; private init; }

    /// <summary>The experience the event gives, <c>experience</c> (<c>maps_objects.h:96</c>).</summary>
    public int Experience { get; private init; }

    /// <summary>
    /// Restores the stolen French characters of the message, <c>World::fixFrenchCharactersInStrings()</c>
    /// (<c>world.cpp:1304-1309</c>).
    /// </summary>
    internal void FixFrenchCharacters() => Message = MapTextCodec.FixFrenchCharacters(Message);

    /// <summary>
    /// Reads the block of a ground event, <c>MapEvent::LoadFromMP2()</c> (<c>maps_objects.cpp:44</c>).
    /// </summary>
    /// <remarks>
    /// The reference asserts the size and the marker and logs a warning before the caller skips the block
    /// (<c>world_loadmap.cpp:634</c>); the port reports the same decision through the return value.
    /// </remarks>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="tileIndex">Index of the tile the block belongs to.</param>
    /// <param name="mapEvent">The event when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> is a ground event block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, int tileIndex, out MapEvent? mapEvent)
    {
        mapEvent = null;

        if (data.Length < MinimumStructureSize || data[0] != EventMarker)
        {
            return false;
        }

        // The file stores the amounts as wood, mercury, ore, sulfur, crystal, gems, gold; the constructor of
        // Funds takes ore first, so the names are spelled out.
        Funds resources = new(
            ore: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 8)..]),
            wood: BinaryPrimitives.ReadInt32LittleEndian(data[ResourceOffset..]),
            mercury: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 4)..]),
            sulfur: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 12)..]),
            crystal: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 16)..]),
            gems: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 20)..]),
            gold: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 24)..]));

        // The artifact counts from 0 in the file and from 1 in the engine (maps_objects.cpp:123).
        ArtifactType artifact = (ArtifactType)(BinaryPrimitives.ReadUInt16LittleEndian(data[ArtifactOffset..]) + 1);

        PlayerColor colors = PlayerColor.None;

        if (data[ColorOffset] != 0)
        {
            colors |= PlayerColor.Blue;
        }

        if (data[ColorOffset + 1] != 0)
        {
            colors |= PlayerColor.Green;
        }

        if (data[ColorOffset + 2] != 0)
        {
            colors |= PlayerColor.Red;
        }

        if (data[ColorOffset + 3] != 0)
        {
            colors |= PlayerColor.Yellow;
        }

        if (data[ColorOffset + 4] != 0)
        {
            colors |= PlayerColor.Orange;
        }

        if (data[ColorOffset + 5] != 0)
        {
            colors |= PlayerColor.Purple;
        }

        mapEvent = new MapEvent
        {
            Resources = resources,
            Artifact = artifact,
            IsComputerPlayerAllowed = data[ComputerPlayerOffset] != 0,
            IsSingleTimeEvent = data[SingleTimeOffset] != 0,
            Colors = colors,
            Message = MapTextCodec.Decode(data[MessageOffset..]),
        };

        mapEvent.PlaceOn(tileIndex);

        return true;
    }
}
