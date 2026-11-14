namespace FHeroes2.Assets.Maps;

/// <summary>
/// Victory condition of a map, the port of <c>Maps::FileInfo::VictoryCondition</c>. The values are
/// stored in the MP2 header.
/// </summary>
public enum MapVictoryCondition : byte
{
    /// <summary>Defeat all other players.</summary>
    DefeatEveryone = 0,

    /// <summary>Capture a specific town.</summary>
    CaptureTown = 1,

    /// <summary>Defeat a specific hero.</summary>
    KillHero = 2,

    /// <summary>Find a specific artifact.</summary>
    ObtainArtifact = 3,

    /// <summary>Defeat the other side of an alliance.</summary>
    DefeatOtherSide = 4,

    /// <summary>Accumulate a specific amount of gold.</summary>
    CollectGold = 5,
}
