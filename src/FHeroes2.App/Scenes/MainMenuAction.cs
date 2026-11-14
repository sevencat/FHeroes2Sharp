namespace FHeroes2.App.Scenes;

/// <summary>What a main menu button asks the host to do.</summary>
/// <remarks>
/// The signs of the street scene and the editor button in the bottom right corner are the entries the
/// reference engine has (<c>Game::MainMenu()</c>); <see cref="Quit"/>, <see cref="NewGame"/> and
/// <see cref="Editor"/> are handled by the host today, the others are the sign posts the later
/// milestones hang their screens on (M8 load game, M9 high scores / credits).
/// </remarks>
public enum MainMenuAction
{
    NewGame = 0,
    LoadGame = 1,
    HighScores = 2,
    Credits = 3,
    Quit = 4,
    Editor = 5,
}
