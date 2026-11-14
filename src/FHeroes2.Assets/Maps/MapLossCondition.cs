namespace FHeroes2.Assets.Maps;

/// <summary>
/// Loss condition of a map, the port of <c>Maps::FileInfo::LossCondition</c>. The values are stored in
/// the MP2 header.
/// </summary>
public enum MapLossCondition : byte
{
    /// <summary>Lose all towns and heroes.</summary>
    Everything = 0,

    /// <summary>Lose a specific town.</summary>
    LoseTown = 1,

    /// <summary>Lose a specific hero.</summary>
    LoseHero = 2,

    /// <summary>Lose after a specific number of days.</summary>
    OutOfTime = 3,
}
