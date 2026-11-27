using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;

using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the daily events model the editor's spec window normalizes,
/// <c>editDailyEvent()</c> of <c>editor_daily_event_spec_window.cpp:70-77</c>.
/// </summary>
public sealed class MapDailyEventTests
{
    [Fact]
    public void DefaultsMatchTheMapFormat()
    {
        MapDailyEvent dailyEvent = new();

        Assert.Equal(1u, dailyEvent.FirstOccurrenceDay);
        Assert.Equal(0u, dailyEvent.RepeatPeriodInDays);
        Assert.Equal(PlayerColor.None, dailyEvent.HumanPlayerColors);
        Assert.Equal(PlayerColor.None, dailyEvent.ComputerPlayerColors);
        Assert.Equal(string.Empty, dailyEvent.Message);
        Assert.Equal(0u, dailyEvent.Resources.GetValidItemsCount());
    }

    [Fact]
    public void ConstantsMatchTheSpecWindow()
    {
        Assert.Equal(7u * 4 * 12, MapDailyEvent.DaysInYear);
        Assert.Equal(33600u, MapDailyEvent.LastDayForEvents);
    }

    [Fact]
    public void ClampDropsColorsTheMapNoLongerAllows()
    {
        MapDailyEvent dailyEvent = new()
        {
            HumanPlayerColors = PlayerColor.Blue | PlayerColor.Red | PlayerColor.Green,
            ComputerPlayerColors = PlayerColor.Yellow | PlayerColor.Orange
        };

        dailyEvent.Clamp(PlayerColor.Blue | PlayerColor.Green, PlayerColor.Yellow);

        Assert.Equal(PlayerColor.Blue | PlayerColor.Green, dailyEvent.HumanPlayerColors);
        Assert.Equal(PlayerColor.Yellow, dailyEvent.ComputerPlayerColors);
    }

    [Fact]
    public void ClampKeepsADayInsideTheSupportedRange()
    {
        MapDailyEvent tooEarly = new() { FirstOccurrenceDay = 0 };
        tooEarly.Clamp(PlayerColor.None, PlayerColor.None);
        Assert.Equal(1u, tooEarly.FirstOccurrenceDay);

        MapDailyEvent tooLate = new() { FirstOccurrenceDay = 1_000_000 };
        tooLate.Clamp(PlayerColor.None, PlayerColor.None);
        Assert.Equal(MapDailyEvent.LastDayForEvents, tooLate.FirstOccurrenceDay);

        MapDailyEvent keep = new() { FirstOccurrenceDay = 42 };
        keep.Clamp(PlayerColor.None, PlayerColor.None);
        Assert.Equal(42u, keep.FirstOccurrenceDay);
    }

    [Fact]
    public void ClampCapsTheRepeatPeriodAtOneYear()
    {
        MapDailyEvent dailyEvent = new() { RepeatPeriodInDays = 100_000 };

        dailyEvent.Clamp(PlayerColor.None, PlayerColor.None);

        Assert.Equal(MapDailyEvent.DaysInYear, dailyEvent.RepeatPeriodInDays);
    }

    [Fact]
    public void CloneCopiesEveryField()
    {
        MapDailyEvent source = new()
        {
            Message = "hello",
            HumanPlayerColors = PlayerColor.Blue,
            ComputerPlayerColors = PlayerColor.Red,
            FirstOccurrenceDay = 7,
            RepeatPeriodInDays = 3,
            Resources = new Funds(Resource.Gold, 500)
        };

        MapDailyEvent copy = source.Clone();

        Assert.NotSame(source, copy);
        Assert.Equal(source.Message, copy.Message);
        Assert.Equal(source.HumanPlayerColors, copy.HumanPlayerColors);
        Assert.Equal(source.ComputerPlayerColors, copy.ComputerPlayerColors);
        Assert.Equal(source.FirstOccurrenceDay, copy.FirstOccurrenceDay);
        Assert.Equal(source.RepeatPeriodInDays, copy.RepeatPeriodInDays);
        Assert.Equal(source.Resources.Gold, copy.Resources.Gold);
    }
}
