using FHeroes2.Assets.Maps;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The translated strings the scenario selection dialog draws: the label of the difficulty box and
/// the names of the five difficulty levels, of the six victory conditions and of the four loss
/// conditions.
/// </summary>
/// <remarks>
/// The UI layer never reads a translation catalogue: the caller passes the msgids resolved, exactly
/// like <see cref="SettingsOption"/> takes its labels as plain strings. The English values of
/// <see cref="Default"/> are the msgids of <c>dialog_selectscenario.cpp</c> and
/// <c>difficulty.cpp</c>, so a catalogue that does not know them still paints readable text.
/// </remarks>
public sealed record ScenarioDialogText
{
    /// <summary>Label in front of the difficulty box, <c>_( "Map difficulty:" )</c>.</summary>
    public required string DifficultyLabel { get; init; }

    /// <summary>Names of the five difficulty levels, indexed by <see cref="GameDifficulty"/>.</summary>
    public required IReadOnlyList<string> Difficulties { get; init; }

    /// <summary>Names of the six victory conditions, indexed by <see cref="MapVictoryCondition"/>.</summary>
    public required IReadOnlyList<string> VictoryConditions { get; init; }

    /// <summary>Names of the four loss conditions, indexed by <see cref="MapLossCondition"/>.</summary>
    public required IReadOnlyList<string> LossConditions { get; init; }

    /// <summary>The untranslated strings of the reference implementation.</summary>
    public static ScenarioDialogText Default { get; } = new()
    {
        DifficultyLabel = "Map difficulty:",
        Difficulties =
        [
            "difficulty|Easy",
            "difficulty|Normal",
            "difficulty|Hard",
            "difficulty|Expert",
            "difficulty|Impossible",
        ],
        VictoryConditions =
        [
            "Defeat all enemy heroes and towns.",
            "Capture a specific town.",
            "Defeat a specific hero.",
            "Find a specific artifact.",
            "Your side must defeat the opposing side.",
            "Accumulate a large amount of gold.",
        ],
        LossConditions =
        [
            "Lose all your heroes and towns.",
            "Lose a specific town.",
            "Lose a specific hero.",
            "Run out of time. Fail to win by a certain point.",
        ],
    };

    /// <summary>Name of <paramref name="difficulty"/>; empty when the list is shorter than the enum.</summary>
    public string Difficulty(GameDifficulty difficulty) => At(Difficulties, (int)difficulty);

    /// <summary>Name of <paramref name="condition"/>; empty when the list is shorter than the enum.</summary>
    public string Victory(MapVictoryCondition condition) => At(VictoryConditions, (int)condition);

    /// <summary>Name of <paramref name="condition"/>; empty when the list is shorter than the enum.</summary>
    public string Loss(MapLossCondition condition) => At(LossConditions, (int)condition);

    private static string At(IReadOnlyList<string> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : string.Empty;
}
