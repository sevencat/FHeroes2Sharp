using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="CastleDialog"/>: the town screen's date line, status line and return values.
/// </summary>
public sealed class CastleDialogTests
{
    [Fact]
    public void TheDateLineIsSpelledOut()
    {
        Assert.Equal("Month: 1, Week: 3, Day: 5", CastleDialog.GetDateString(1, 3, 5));
        Assert.Equal("Month: 12, Week: 4, Day: 7", CastleDialog.GetDateString(12, 4, 7));
    }

    [Fact]
    public void TheStatusLineOfADwellingOffersItsCreature()
    {
        Assert.Equal("Recruit peasants", CastleDialog.GetStatusLine(MapRace.Knight, BuildingType.DwellingMonster1));
    }

    [Fact]
    public void TheStatusLineOfABuildingIsItsName()
    {
        Assert.Equal(
            CastleBuildingInfo.GetName(MapRace.Necromancer, BuildingType.Shrine),
            CastleDialog.GetStatusLine(MapRace.Necromancer, BuildingType.Shrine));
    }

    [Fact]
    public void TheReturnValuesAreTheReferences()
    {
        // Eight answers, and "stay open" is one of them rather than the absence of an answer.
        Assert.Equal(8, Enum.GetValues<CastleDialog.Result>().Length);
        Assert.Equal(CastleDialog.Result.DoNothing, Enum.GetValues<CastleDialog.Result>()[0]);
        Assert.Equal(CastleDialog.Result.PreviousMageGuildWindow, Enum.GetValues<CastleDialog.Result>()[7]);
    }
}
