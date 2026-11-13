namespace FHeroes2.App.Scenes;

/// <summary>What a main menu button asks the host to do.</summary>
/// <remarks>
/// Only <see cref="Quit"/> is handled by the host today; the other entries are the sign posts the
/// later milestones hang their screens on (M3 new game, M8 load game, M9 high scores / credits).
/// </remarks>
public enum MainMenuAction
{
    NewGame = 0,
    LoadGame = 1,
    HighScores = 2,
    Credits = 3,
    Quit = 4,
}
