using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="AdventureMapDialogs"/>, the dialogs the adventure map now opens itself instead of asking
/// <see cref="IAdventureHost"/> (<c>gui/interface_events.cpp</c>).
/// </summary>
/// <remarks>
/// The windows read <c>FONT.ICN</c>, <c>SYSTEM.ICN</c> and the frame artwork from the archives, so the tests that
/// really open one need the installed game data and return when the machine has none; the button mapping runs always.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class AdventureMapDialogsTests
{
    [Theory]
    [InlineData(DialogButton.World, AdventureOption.World)]
    [InlineData(DialogButton.Puzzle, AdventureOption.Puzzle)]
    [InlineData(DialogButton.Info, AdventureOption.Info)]
    [InlineData(DialogButton.Dig, AdventureOption.Dig)]
    [InlineData(DialogButton.Cancel, AdventureOption.Cancel)]
    [InlineData(DialogButton.Ok, AdventureOption.Cancel)]
    [InlineData(DialogButton.None, AdventureOption.Cancel)]
    public void ToAdventureOption_MapsTheFourDoorsAndNothingElse(DialogButton button, AdventureOption expected) =>
        Assert.Equal(expected, AdventureMapDialogs.ToAdventureOption(button));

    [Theory]
    [InlineData(GameOverMode.MainMenu, AdventureEventResult.Lose)]
    [InlineData(GameOverMode.HighScoresStandard, AdventureEventResult.HighScore)]
    [InlineData(GameOverMode.CompleteCampaignScenario, AdventureEventResult.Win)]
    [InlineData(GameOverMode.Cancel, AdventureEventResult.Cancel)]
    public void ToAdventureEventResult_MapsTheFourVerdicts(GameOverMode mode, AdventureEventResult expected) =>
        Assert.Equal(expected, AdventureMapDialogs.ToAdventureEventResult(mode));

    [Fact]
    public void ShowYesNo_IsNoWhenTheEngineStops() => Assert.True(TryWithArchives(
        (dialogs, events) => dialogs.ShowYesNo(
            "Beware!",
            "Do you really want to quit the game?\nUnsaved changes will be lost.",
            events),
        answer => Assert.False(answer)));

    [Fact]
    public void ShowMessage_CanBeClosedByTheEngine() => Assert.True(TryWithArchives(
        (dialogs, events) =>
        {
            dialogs.ShowMessage("Beware!", "No hero is selected.", events);

            return 0;
        },
        _ => { }));

    [Fact]
    public void OpenCastleDialog_ClosesWhenTheEngineStops() => Assert.True(TryWithArchives(
        (dialogs, events) =>
        {
            CastleTown castle = new(0, MapRace.Knight);
            castle.ChangeColor(PlayerColor.Red);
            dialogs.OpenCastleDialog(castle, events);

            return true;
        },
        answer => Assert.True(answer)));

    [Fact]
    public void OpenKingdomOverview_ClosesWhenTheEngineStops() => Assert.True(TryWithArchives(
        (dialogs, events) =>
        {
            dialogs.OpenKingdomOverview(events);

            return true;
        },
        answer => Assert.True(answer)));

    [Fact]
    public void OpenHeroesDialog_ClosesWhenTheEngineStops() => Assert.True(TryWithArchives(
        (dialogs, events) =>
        {
            dialogs.OpenHeroesDialog(new Hero(1, MapRace.Knight), events);

            return true;
        },
        answer => Assert.True(answer)));

    [Fact]
    public void ShowFileOptions_IsQuitWhenTheEngineStops() => Assert.True(TryWithArchives(
        (dialogs, events) => dialogs.ShowFileOptions(events),
        answer => Assert.Equal(AdventureEventResult.QuitGame, answer)));

    [Fact]
    public void ShowAdventureOptions_IsCancelledWhenTheEngineStops() => Assert.True(TryWithArchives(
        (dialogs, events) => dialogs.ShowAdventureOptions(false, events),
        answer => Assert.Equal(AdventureOption.Cancel, answer)));

    /// <summary>Runs a test that needs the message, system and font artwork, or returns when the machine has none.</summary>
    /// <param name="open">Opens the dialog with a helper and an engine that stops at once.</param>
    /// <param name="assert">Checks the answer.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool TryWithArchives<T>(Func<AdventureMapDialogs, LocalEvent, T> open, Action<T> assert)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            PixelBuffer target = new(640, 480);
            AdventureMapDialogs dialogs = new(target, assets);
            LocalEvent events = new(() => 0) { Engine = new StoppingEngine() };

            assert(open(dialogs, events));
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>An event engine that has nothing to poll and asks the loop to stop.</summary>
    private sealed class StoppingEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            return false;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }
}
