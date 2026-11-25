using FHeroes2.Assets.Maps;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The strings of the scenario info screen, <c>Game::SelectScenario()</c> of <c>game_scenarioinfo.cpp</c>.
/// </summary>
/// <remarks>
/// 参考把 <c>_()</c> 的原文写在函数里；端口按 <see cref="ScenarioDialogText"/> 的做法收成一个可替换的记录，方便测试
/// 与翻译。难度名与剧本列表共用 <see cref="ScenarioDialogText.Difficulties"/> 的 msgid，由调用方翻译好再传进来。
/// </remarks>
public sealed record ScenarioInfoText
{
    /// <summary>The label of the map name box, <c>_( "Scenario:" )</c>.</summary>
    public required string ScenarioLabel { get; init; }

    /// <summary>The label above the difficulty icons, <c>_( "Game Difficulty:" )</c>.</summary>
    public required string DifficultyLabel { get; init; }

    /// <summary>The label above the opponents panel, <c>_( "Opponents:" )</c>.</summary>
    public required string OpponentsLabel { get; init; }

    /// <summary>The label above the class panel, <c>_( "Class:" )</c>.</summary>
    public required string ClassLabel { get; init; }

    /// <summary>The rating line, <c>_( "Rating %{rating}%" )</c> before the number is substituted.</summary>
    public required string RatingTemplate { get; init; }

    /// <summary>Name of the "Select Maps" button; the artwork carries no text, so it is only used by help and tests.</summary>
    public required string SelectMapsButton { get; init; }

    /// <summary>Title of the map name box help, <c>_( "Scenario" )</c>.</summary>
    public required string ScenarioHelpTitle { get; init; }

    /// <summary>Help of the map name box, <c>_( "Click here to select which scenario to play." )</c>.</summary>
    public required string ScenarioHelp { get; init; }

    /// <summary>Title of the difficulty help, <c>_( "Game Difficulty" )</c>.</summary>
    public required string DifficultyHelpTitle { get; init; }

    /// <summary>Body of the difficulty help.</summary>
    public required string DifficultyHelp { get; init; }

    /// <summary>Title of the rating help, <c>_( "Difficulty Rating" )</c>.</summary>
    public required string RatingHelpTitle { get; init; }

    /// <summary>Body of the rating help.</summary>
    public required string RatingHelp { get; init; }

    /// <summary>Title of the okay help, <c>_( "Okay" )</c>.</summary>
    public required string OkayHelpTitle { get; init; }

    /// <summary>Body of the okay help, <c>_( "Click to accept these settings and start a new game." )</c>.</summary>
    public required string OkayHelp { get; init; }

    /// <summary>Title of the cancel help, <c>_( "Cancel" )</c>.</summary>
    public required string CancelHelpTitle { get; init; }

    /// <summary>Body of the cancel help, <c>_( "Click to return to the main menu." )</c>.</summary>
    public required string CancelHelp { get; init; }

    /// <summary>The five difficulty names, easiest first, <c>Difficulty::String()</c>.</summary>
    public required IReadOnlyList<string> Difficulties { get; init; }

    /// <summary>The text of the rating line with the rating substituted.</summary>
    /// <param name="rating">The rating, as <c>Game::GetRating()</c> prints it.</param>
    public string Rating(string rating) => RatingTemplate.Replace("%{rating}", rating, StringComparison.Ordinal);

    /// <summary>The name of a difficulty, <c>Difficulty::String( int difficulty )</c> (<c>difficulty.cpp:35-64</c>).</summary>
    /// <param name="difficulty">The difficulty to name.</param>
    public string Difficulty(GameDifficulty difficulty)
    {
        int index = (int)difficulty;

        return (uint)index < (uint)Difficulties.Count ? Difficulties[index] : string.Empty;
    }

    /// <summary>The English strings of the reference.</summary>
    public static ScenarioInfoText Default { get; } = new()
    {
        ScenarioLabel = "Scenario:",
        DifficultyLabel = "Game Difficulty:",
        OpponentsLabel = "Opponents:",
        ClassLabel = "Class:",
        Difficulties = [.. ScenarioDialogText.Default.Difficulties],
        RatingTemplate = "Rating %{rating}%",
        SelectMapsButton = "Select Maps",
        ScenarioHelpTitle = "Scenario",
        ScenarioHelp = "Click here to select which scenario to play.",
        DifficultyHelpTitle = "Game Difficulty",
        DifficultyHelp =
            "This lets you change the starting difficulty at which you will play. Higher difficulty levels start you off with fewer resources, and at the higher settings, give extra resources to the computer.",
        RatingHelpTitle = "Difficulty Rating",
        RatingHelp =
            "The difficulty rating reflects a combination of various settings for your game. This number will be applied to your final score.",
        OkayHelpTitle = "Okay",
        OkayHelp = "Click to accept these settings and start a new game.",
        CancelHelpTitle = "Cancel",
        CancelHelp = "Click to return to the main menu.",
    };
}
