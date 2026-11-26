namespace FHeroes2.UI.Interface;

/// <summary>
/// What an adventure map event asks the rest of the game to do, the <c>fheroes2::GameMode</c> a
/// <c>Interface::AdventureMap::Event*()</c> returns (<c>game_mode.h:32-53</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference returns a game mode because its caller is the <c>StartGame()</c> loop, which switches on it. The
/// port has scenes instead of that loop (<c>AdventureMapScene</c>), so the handful of outcomes an event can
/// produce are spelled out here, the same way <c>FileOptionsResult</c> spells out the five outcomes of
/// <c>Dialog::FileOptions()</c> (ADR-163).
/// </para>
/// <para>
/// <b>Cancel</b> is the reference's "nothing happened, stay on the map", and is what every event that returns
/// <c>fheroes2::GameMode::CANCEL</c> reports.
/// </para>
/// <para>
/// The dialogs themselves are no longer reported through a host interface: every one of them is an
/// <see cref="AdventureMapDialogs"/> method, so the events open their windows the way the reference does
/// (<c>Interface::AdventureMap::Event*()</c> calling <c>Dialog::*</c>) and only the game mode travels back.
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

    /// <summary>
    /// The campaign selection has to be opened and its answer acted on,
    /// <c>Game::SelectCampaignScenario( fheroes2::GameMode::CANCEL, true )</c> of <c>interface_events.cpp:378</c>.
    /// </summary>
    /// <remarks>
    /// 这不是 <c>GameMode</c> 里的值，而是端口的一处「请求」：参考的 <c>EventScenarioInformation()</c> 自己调
    /// <c>Game::SelectCampaignScenario()</c> 并把它返回的 <c>START_GAME</c>/<c>CANCEL</c> 直接交回主循环，
    /// 端口的分层里战役选关屏是 App 层的场景，UI 层够不着，所以只报「该开选关屏了」，由宿主跑完再决定
    /// 重开这一关还是留在图上。
    /// </remarks>
    CampaignSelection = 8,
}

/// <summary>
/// What one frame of panel input asks the loop to do: a game mode plus, when the player grabbed a floating panel, the
/// drag the caller has to own, the answer of <see cref="AdventureMap.ProcessPanels"/>.
/// </summary>
/// <param name="Mode">What the loop should do next.</param>
/// <param name="Drag">
/// The drag that started this frame, or <see langword="null"/>. The reference runs its loop inside
/// <c>BorderWindow::QueueEventProcessing()</c>; the port hands the state object back so the scene can do the pumping.
/// </param>
/// <remarks>
/// Only the panels of the hidden-interface mode have a border to grab, so <paramref name="Drag"/> is
/// <see langword="null"/> in the normal layout.
/// </remarks>
public readonly record struct AdventurePanelResult(AdventureEventResult Mode, BorderDrag? Drag)
{
    /// <summary>Nothing happened and nothing is being dragged.</summary>
    public static AdventurePanelResult Cancel => default;
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
