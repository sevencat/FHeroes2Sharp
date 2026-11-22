using FHeroes2.Game;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Spells;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Interface;

/// <summary>
/// What an adventure map event asks the rest of the game to do, the <c>fheroes2::GameMode</c> a
/// <c>Interface::AdventureMap::Event*()</c> returns (<c>game_mode.h:32-53</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference returns a game mode because its caller is the <c>StartGame()</c> loop, which switches on it. The
/// port has scenes instead of that loop (<see cref="AdventureMapScene"/>), so the handful of outcomes an event can
/// produce are spelled out here, the same way <c>FileOptionsResult</c> spells out the five outcomes of
/// <c>Dialog::FileOptions()</c> (ADR-163).
/// </para>
/// <para>
/// <b>Cancel</b> is the reference's "nothing happened, stay on the map", and is what every event that returns
/// <c>fheroes2::GameMode::CANCEL</c> reports.
/// </para>
/// </remarks>
public enum AdventureEventResult
{
    /// <summary>Nothing happened, <c>fheroes2::GameMode::CANCEL</c>.</summary>
    Cancel = 0,

    /// <summary>The player's turn is over, <c>fheroes2::GameMode::END_TURN</c>.</summary>
    EndTurn = 1,

    /// <summary>A new game should start, <c>fheroes2::GameMode::NEW_GAME</c>.</summary>
    NewGame = 2,

    /// <summary>A saved game should be loaded, <c>fheroes2::GameMode::LOAD_GAME</c>.</summary>
    LoadGame = 3,

    /// <summary>The application should quit, <c>fheroes2::GameMode::QUIT_GAME</c>.</summary>
    QuitGame = 4,

    /// <summary>The scenario has been won, <c>fheroes2::GameMode::WIN</c>.</summary>
    Win = 5,

    /// <summary>The scenario has been lost, <c>fheroes2::GameMode::LOSE</c>.</summary>
    Lose = 6,

    /// <summary>A high score should be shown, <c>fheroes2::GameMode::HIGH_SCORE</c>.</summary>
    HighScore = 7,
}

/// <summary>
/// What the adventure options dialog offers, <c>Dialog::AdventureOptions()</c>'s return value
/// (<c>dialog.h:95-104</c>, <c>dialog_adventure.cpp</c>).
/// </summary>
public enum AdventureOption
{
    /// <summary>The dialog was closed, <c>Dialog::CANCEL</c>.</summary>
    Cancel = 0,

    /// <summary>Show the world map, <c>Dialog::WORLD</c>.</summary>
    World = 1,

    /// <summary>Show the puzzle map, <c>Dialog::PUZZLE</c>.</summary>
    Puzzle = 2,

    /// <summary>Show the scenario information, <c>Dialog::INFO</c>.</summary>
    Info = 3,

    /// <summary>Dig for the ultimate artifact, <c>Dialog::DIG</c>.</summary>
    Dig = 4,
}

/// <summary>
/// The dialogs and the rest of the game that the adventure map events call into, i.e. the global functions the
/// reference reaches from <c>gui/interface_events.cpp</c>: <c>Dialog::*</c>, <c>Game::Open*Dialog()</c>,
/// <c>Game::Save()</c>, <c>Kingdom::openOverviewDialog()</c>, <c>PuzzleMaps::ShowMapsDialog()</c>,
/// <c>ViewWorld::ViewWorldWindow()</c> and <c>fheroes2::showStandardTextMessage()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The port has no blocking message boxes and no dialog singletons: a dialog is an object the owner creates and
/// feeds events to (<see cref="Dialogs.MessageBox"/>, <see cref="Dialogs.DialogElement"/>). The owner of the
/// adventure screen is the scene, so the events report "open this" to it instead of opening anything themselves —
/// the same split <see cref="IconsPanelAction"/> uses, lifted to the whole screen.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): the events are ported against this interface, and the scene implements it when
/// G5 wires the two together.
/// </para>
/// </remarks>
public interface IAdventureHost
{
    /// <summary>Opens the hero dialog, <c>Game::OpenHeroesDialog( hero, true, true )</c>.</summary>
    /// <param name="hero">The hero to show.</param>
    void OpenHeroesDialog(Hero hero);

    /// <summary>Opens the castle dialog, <c>Game::OpenCastleDialog( castle )</c>.</summary>
    /// <param name="castle">The castle to show.</param>
    void OpenCastleDialog(CastleTown castle);

    /// <summary>
    /// Shows what stands on a map tile without leaving the map, <c>Dialog::QuickInfo( tile )</c>
    /// (<c>dialog_quickinfo.cpp</c>).
    /// </summary>
    /// <param name="tile">The tile the player right-clicked.</param>
    void ShowQuickInfo(Tile tile);

    /// <summary>Shows a town without leaving the map, <c>Dialog::QuickInfo( castle )</c>.</summary>
    /// <param name="castle">The town the player right-clicked.</param>
    void ShowQuickInfo(CastleTown castle);

    /// <summary>Shows a hero without leaving the map, <c>Dialog::QuickInfo( heroes )</c>.</summary>
    /// <param name="hero">The hero the player right-clicked.</param>
    void ShowQuickInfo(Hero hero);

    /// <summary>Opens the spell book and casts what the player picks, <c>Heroes::OpenSpellBook( ADVN, true, false, {} )</c>.</summary>
    /// <param name="hero">The hero whose book is opened.</param>
    /// <param name="spell">The spell the player picked, or the invalid spell when the book was closed.</param>
    /// <returns>Whether a spell was picked.</returns>
    bool OpenSpellBook(Hero hero, out Spell spell);

    /// <summary>Opens the kingdom overview, <c>Kingdom::openOverviewDialog()</c>.</summary>
    void OpenKingdomOverview();

    /// <summary>Opens the puzzle map, <c>PuzzleMaps::ShowMapsDialog()</c>.</summary>
    void OpenPuzzleMaps();

    /// <summary>Opens the world map, <c>ViewWorld::ViewWorldWindow( color, OnlyVisible, interface )</c>.</summary>
    void OpenViewWorld();

    /// <summary>Opens the file options dialog, <c>Dialog::FileOptions()</c>.</summary>
    /// <returns>What the player asked for.</returns>
    AdventureEventResult ShowFileOptions();

    /// <summary>Opens the system options dialog, <c>fheroes2::showSystemOptionsDialog()</c>.</summary>
    void ShowSystemOptions();

    /// <summary>Saves the game, <c>Game::Save( filename )</c>.</summary>
    /// <param name="fileName">Where to write the save.</param>
    /// <returns>Whether the save was written.</returns>
    bool SaveGame(string fileName);

    /// <summary>
    /// Evaluates the end-of-game conditions after an action that can decide the scenario,
    /// <c>GameOver::Result::Get().checkGameOver()</c>.
    /// </summary>
    /// <returns>The mode the game should switch to; <see cref="AdventureEventResult.Cancel"/> while the game goes on.</returns>
    AdventureEventResult CheckGameOver();
}
