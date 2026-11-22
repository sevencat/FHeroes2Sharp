using System.Buffers.Binary;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game;

/// <summary>
/// A timed event of the map — a message with resources that fires on a given day — the reference's
/// <c>EventDate</c> (<c>world.h:171</c>), read from an information block by <c>EventDate::LoadFromMP2()</c>
/// (<c>world.cpp:1517</c>).
/// </summary>
/// <remarks>
/// <para>
/// A daily event has no tile: it is a block that no object of the map points at. That is why the block
/// carries its own marker — byte 0 is <c>0x00</c> and byte 42 is <c>0x01</c>, which is the pair the loader
/// looks for before it treats a block as an event (<c>world_loadmap.cpp:659</c>).
/// </para>
/// <para>
/// The amounts may be negative, so an event can take resources away as well as give them. The reference
/// keeps a <c>title</c> beside the message; only the newer map format fills it, so the port keeps it empty
/// and leaves it out of the loading.
/// </para>
/// </remarks>
public sealed partial class EventDate
{
    /// <summary>Smallest block that can be a daily event, <c>MP2::MP2_EVENT_STRUCTURE_MIN_SIZE</c> (<c>mp2.h:37</c>).</summary>
    public const int MinimumStructureSize = 50;

    /// <summary>The first byte of a block that belongs to no object, <c>data[0] == 0</c>.</summary>
    private const byte WorldBlockMarker = 0x00;

    /// <summary>
    /// The byte that tells a daily event from a rumor, <c>data[42] == 1</c>
    /// (<c>world_loadmap.cpp:659</c>); a rumor has a zero there.
    /// </summary>
    private const int DailyEventMarkerOffset = 42;

    /// <summary>Offset of the wood amount; the seven amounts follow each other.</summary>
    private const int ResourceOffset = 1;

    /// <summary>Offset of the "applies to AI players as well" flag, a 16 bit word.</summary>
    private const int ComputerPlayerOffset = 31;

    /// <summary>Offset of the first day the event fires, a 16 bit word.</summary>
    private const int FirstDayOffset = 33;

    /// <summary>Offset of the repeat period in days, a 16 bit word; 0 means the event never repeats.</summary>
    private const int RepeatPeriodOffset = 35;

    /// <summary>Offset of the six color flags: blue, green, red, yellow, orange, purple.</summary>
    private const int ColorOffset = 43;

    /// <summary>Offset of the message string.</summary>
    private const int MessageOffset = 49;

    private EventDate()
    {
    }

    /// <summary>The resources the event changes, negative when it takes them away.</summary>
    public Funds Resources { get; private init; }

    /// <summary>The first day the event fires, <c>firstOccurrenceDay</c>.</summary>
    public uint FirstOccurrenceDay { get; private init; }

    /// <summary>
    /// The period in days between two occurrences, <c>repeatPeriodInDays</c>; 0 when the event fires once.
    /// </summary>
    public uint RepeatPeriodInDays { get; private init; }

    /// <summary>The colors the event fires for; a bit set of <see cref="PlayerColor"/>.</summary>
    public PlayerColor Colors { get; private init; }

    /// <summary>Whether the event also fires for computer players, <c>isApplicableForAIPlayers</c>.</summary>
    public bool IsApplicableForAIPlayers { get; private init; }

    /// <summary>The message of the event, empty when the block carries none.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The title of the event; only the newer map format fills it.</summary>
    public string Title { get; private init; } = string.Empty;

    /// <summary>
    /// Restores the stolen French characters of the message, <c>World::fixFrenchCharactersInStrings()</c>
    /// (<c>world.cpp:1290-1292</c>).
    /// </summary>
    internal void FixFrenchCharacters() => Message = MapTextCodec.FixFrenchCharacters(Message);

    /// <summary>
    /// Whether a date is past the only occurrence of the event, <c>EventDate::isDeprecated()</c>
    /// (<c>world.h:177</c>).
    /// </summary>
    /// <param name="date">The current day.</param>
    /// <returns><see langword="true"/> when the event can no longer fire.</returns>
    public bool IsDeprecated(uint date) => date > FirstOccurrenceDay && RepeatPeriodInDays == 0;

    /// <summary>
    /// Whether the event fires for a player on a day, <c>EventDate::isAllow()</c> (<c>world.cpp:1647</c>).
    /// </summary>
    /// <param name="color">Color of the player.</param>
    /// <param name="date">The current day.</param>
    /// <returns><see langword="true"/> when the event fires.</returns>
    public bool IsAllowed(PlayerColor color, uint date)
    {
        if ((Colors & color) == 0)
        {
            return false;
        }

        if (FirstOccurrenceDay > date)
        {
            return false;
        }

        if (FirstOccurrenceDay == date)
        {
            return true;
        }

        if (RepeatPeriodInDays == 0)
        {
            return false;
        }

        return (date - FirstOccurrenceDay) % RepeatPeriodInDays == 0;
    }

    /// <summary>
    /// Reads the block of a daily event, <c>EventDate::LoadFromMP2()</c> (<c>world.cpp:1517</c>).
    /// </summary>
    /// <remarks>
    /// The reference asserts the size, the marker and the byte at 42; the port reports the same decision
    /// through the return value, so that the caller can tell an event from a rumor.
    /// </remarks>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="eventDate">The event when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> is a daily event block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, out EventDate? eventDate)
    {
        eventDate = null;

        if (data.Length < MinimumStructureSize || data[0] != WorldBlockMarker || data[DailyEventMarkerOffset] != 0x01)
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

        eventDate = new EventDate
        {
            Resources = resources,
            IsApplicableForAIPlayers = BinaryPrimitives.ReadUInt16LittleEndian(data[ComputerPlayerOffset..]) != 0,
            FirstOccurrenceDay = BinaryPrimitives.ReadUInt16LittleEndian(data[FirstDayOffset..]),
            RepeatPeriodInDays = BinaryPrimitives.ReadUInt16LittleEndian(data[RepeatPeriodOffset..]),
            Colors = colors,
            Message = MapTextCodec.Decode(data[MessageOffset..]),
        };

        return true;
    }
}
