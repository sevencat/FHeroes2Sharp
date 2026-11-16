using System.Buffers.Binary;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using KingdomState = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the day cycle of a kingdom: <c>Kingdom::ActionNewDayResourceUpdate()</c>
/// (<c>kingdom.cpp:225-250</c>), which books the income and then the events of the day.
/// </summary>
public sealed class KingdomDailyUpdateTests
{
    [Fact]
    public void TheFirstDayPaysNothing()
    {
        KingdomState kingdom = KingdomWithGold(0);

        // kingdom.cpp:227: the player starts with the funds of the first day already granted.
        kingdom.ActionNewDayResourceUpdate(dayCount: 1, _ => 3, [], isAiControlled: false);

        Assert.Equal(0, kingdom.Treasury.Gold);
        Assert.Equal(0, kingdom.Treasury.Wood);
    }

    [Fact]
    public void FromTheSecondDayTheMinesPay()
    {
        KingdomState kingdom = KingdomWithGold(0);

        // Three gold mines at 1000 a day and a sawmill at 2 wood a day (profit.cpp:98-122).
        kingdom.ActionNewDayResourceUpdate(
            dayCount: 2,
            resource => resource switch
            {
                Resource.Gold => 3u,
                Resource.Wood => 1u,
                _ => 0u,
            },
            [],
            isAiControlled: false);

        Assert.Equal(3000, kingdom.Treasury.Gold);
        Assert.Equal(2, kingdom.Treasury.Wood);
    }

    [Fact]
    public void AnEventGrantsItsResourcesAndTellsTheDialog()
    {
        KingdomState kingdom = KingdomWithGold(0);
        EventDate eventDate = CreateEvent(gold: 1500);

        var shown = new List<(EventDate Event, Funds Funds)>();

        kingdom.ActionNewDayResourceUpdate(
            dayCount: 2,
            _ => 0,
            [eventDate],
            isAiControlled: false,
            (fired, funds) => shown.Add((fired, funds)));

        Assert.Equal(1500, kingdom.Treasury.Gold);

        (EventDate reported, Funds granted) = Assert.Single(shown);

        Assert.Same(eventDate, reported);
        Assert.Equal(new Funds { Gold = 1500 }, granted);
    }

    [Fact]
    public void AnEventCannotTakeMoreThanTheKingdomHas()
    {
        KingdomState kingdom = KingdomWithGold(50);

        kingdom.ActionNewDayResourceUpdate(dayCount: 2, _ => 0, [CreateEvent(gold: -1000)], isAiControlled: false);

        // resource.cpp:469 clamps the delta to what is there, so gold lands on zero rather than below it.
        Assert.Equal(0, kingdom.Treasury.Gold);
    }

    [Fact]
    public void TheDialogSeesTheClampedAmountNotTheRequestedOne()
    {
        KingdomState kingdom = KingdomWithGold(50);

        var shown = new List<Funds>();

        kingdom.ActionNewDayResourceUpdate(
            dayCount: 2,
            _ => 0,
            [CreateEvent(gold: -1000)],
            isAiControlled: false,
            (_, funds) => shown.Add(funds));

        // The message on screen is about what happened: fifty gold, not a thousand.
        Assert.Equal(new Funds { Gold = -50 }, Assert.Single(shown));
    }

    [Fact]
    public void TheComputerSkipsEventsItsMapMarkedForHumansOnly()
    {
        KingdomState kingdom = KingdomWithGold(0);
        EventDate humanOnly = CreateEvent(gold: 1000, forAiPlayers: false);

        kingdom.ActionNewDayResourceUpdate(dayCount: 2, _ => 0, [humanOnly], isAiControlled: true);
        Assert.Equal(0, kingdom.Treasury.Gold);

        kingdom.ActionNewDayResourceUpdate(dayCount: 2, _ => 0, [humanOnly], isAiControlled: false);
        Assert.Equal(1000, kingdom.Treasury.Gold);
    }

    [Fact]
    public void ASilentUpdateAppliesTheEventWithoutADialog()
    {
        KingdomState kingdom = KingdomWithGold(0);
        Action<EventDate, Funds>? noDialog = null;

        kingdom.ActionNewDayResourceUpdate(dayCount: 2, _ => 0, [CreateEvent(gold: 250)], isAiControlled: true, noDialog);

        Assert.Equal(250, kingdom.Treasury.Gold);
    }

    [Fact]
    public void EveryEventOfTheDayIsBooked()
    {
        KingdomState kingdom = KingdomWithGold(0);

        kingdom.ActionNewDayResourceUpdate(
            dayCount: 2,
            _ => 0,
            [CreateEvent(gold: 100), CreateEvent(gold: 20, wood: 3), CreateEvent(gold: 1)],
            isAiControlled: false);

        Assert.Equal(121, kingdom.Treasury.Gold);
        Assert.Equal(3, kingdom.Treasury.Wood);
    }

    /// <summary>A kingdom of the blue player whose purse holds one amount of gold and nothing else.</summary>
    /// <param name="gold">Gold in the purse.</param>
    private static KingdomState KingdomWithGold(int gold)
    {
        KingdomState kingdom = new();

        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        // The starting resources are not the subject here, so the tests start from a known purse.
        kingdom.Treasury = new Funds { Gold = gold };

        return kingdom;
    }

    /// <summary>
    /// A daily event for the blue player, built the way the loader does it, through
    /// <see cref="EventDate.TryRead"/>: the constructor is private and an event only ever comes from a map block.
    /// </summary>
    /// <param name="gold">Gold the event grants; negative takes it away.</param>
    /// <param name="wood">Wood the event grants.</param>
    /// <param name="forAiPlayers">Whether the map marked the event applicable to computer players.</param>
    /// <param name="firstDay">The day the event fires.</param>
    /// <param name="repeat">Period in days; 0 fires once.</param>
    private static EventDate CreateEvent(
        int gold,
        int wood = 0,
        bool forAiPlayers = true,
        ushort firstDay = 1,
        ushort repeat = 0)
    {
        // MP2_EVENT_STRUCTURE_MIN_SIZE is 50, and the byte at 42 tells an event from a rumor (world.cpp:1517).
        byte[] block = new byte[50];

        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(1), wood);
        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(25), gold);

        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(31), forAiPlayers ? (ushort)1 : (ushort)0);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(33), firstDay);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(35), repeat);

        block[42] = 0x01;
        block[43] = 1; // blue

        Assert.True(EventDate.TryRead(block, out EventDate? eventDate));

        return eventDate!;
    }
}
