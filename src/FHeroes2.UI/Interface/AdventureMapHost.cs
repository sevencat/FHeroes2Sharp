using FHeroes2.Game;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
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
/// reference reaches from <c>gui/interface_events.cpp</c>: <c>ViewWorld::ViewWorldWindow()</c>, the system and
/// file dialogs.
/// The message boxes, the save file picker, the quick info panels, the puzzle map, the spell book, the file
/// menu, the hero dialog, the town screen, the kingdom overview and <c>checkGameOver()</c> have already moved to
/// <see cref="AdventureMapDialogs"/>.
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
    /// <summary>Opens the world map, <c>ViewWorld::ViewWorldWindow( color, OnlyVisible, interface )</c>.</summary>
    void OpenViewWorld();

    /// <summary>Opens the system options dialog, <c>fheroes2::showSystemOptionsDialog()</c>.</summary>
    void ShowSystemOptions();
}
