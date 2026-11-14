namespace FHeroes2.Assets.Maps;

/// <summary>
/// Difficulty level of a map, the port of <c>Difficulty::DifficultyLevel</c>. The order is part of the
/// map format (see <c>difficulty.h</c>) and must not change.
/// </summary>
public enum GameDifficulty
{
    /// <summary>Easy.</summary>
    Easy = 0,

    /// <summary>Normal (the default of <c>Maps::FileInfo::Reset</c>).</summary>
    Normal = 1,

    /// <summary>Hard.</summary>
    Hard = 2,

    /// <summary>Expert.</summary>
    Expert = 3,

    /// <summary>Impossible; never stored in an MP2 file, only chosen by the player.</summary>
    Impossible = 4,
}
