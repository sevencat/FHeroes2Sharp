using FHeroes2.Assets.Maps;
using FHeroes2.Game;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="TavernDialog"/>: the two paragraphs of the town's tavern and its single button.
/// </summary>
public sealed class TavernDialogTests
{
    private static readonly string[] Races =
    [
        nameof(MapRace.Knight),
        nameof(MapRace.Barbarian),
        nameof(MapRace.Sorceress),
        nameof(MapRace.Warlock),
        nameof(MapRace.Wizard),
        nameof(MapRace.Necromancer),
    ];

    [Fact]
    public void TheHeadingIsTheNameOfTheTavern()
    {
        // GetStringBuilding( BUILD_TAVERN ) of the reference: the same word in all six towns.
        Assert.Equal("Tavern", TavernDialog.Title);

        foreach (string name in Races)
        {
            MapRace race = Enum.Parse<MapRace>(name);

            Assert.Equal(TavernDialog.Title, CastleBuildingInfo.GetName(race, BuildingType.Tavern));
        }
    }

    [Fact]
    public void TheLeadIsTheLineAboutTheTip()
    {
        Assert.Equal("A generous tip for the barkeep yields the following rumor:", TavernDialog.Lead);
    }

    [Fact]
    public void TheBodyIsTheLeadABlankLineAndTheRumor()
    {
        TavernDialog dialog = TavernDialog.Create(new LocalizedString(World.TruthRumor));

        Assert.Equal(TavernDialog.Lead + "\n\n" + World.TruthRumor, dialog.Body);
    }

    [Fact]
    public void TheRumorKeepsTheLanguageOfItsMap()
    {
        // A rumor typed into a Russian map must be drawn with the Cyrillic face, so the tag travels with the text.
        LocalizedString rumor = new("Слух", SupportedLanguage.Russian);

        TavernDialog dialog = TavernDialog.Create(rumor);

        Assert.Equal("Слух", dialog.Rumor.Text);
        Assert.Equal(SupportedLanguage.Russian, dialog.Rumor.Language);
    }

    [Fact]
    public void TheOnlyButtonIsOkay()
    {
        TavernDialog dialog = TavernDialog.Create(new LocalizedString(World.TruthRumor));

        Assert.Equal(DialogButton.Ok, dialog.Buttons);
    }

    [Fact]
    public void TheWorldOverloadNeedsAWorld()
    {
        // The overload only forwards World.GetCurrentRumor(), which RumorPoolTests covers; the world itself is
        // internal to the game assembly, so there is nothing else to check from here.
        Assert.Throws<ArgumentNullException>(() => TavernDialog.Create((World)null!));
    }

    [Fact]
    public void AnEmptyRumorIsRefused()
    {
        // The loader drops empty rumors, so an empty one here is a bug rather than a state the window can draw.
        Assert.Throws<ArgumentException>(() => TavernDialog.Create(new LocalizedString(string.Empty)));
        Assert.Throws<ArgumentException>(() => TavernDialog.Create(new LocalizedString("   ")));
    }
}
