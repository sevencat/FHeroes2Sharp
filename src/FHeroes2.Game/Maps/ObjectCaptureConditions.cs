namespace FHeroes2.Game.Maps;

/// <summary>
/// What a visited Daemon Cave hands out, <c>DaemonCaveCaptureBonus</c>
/// (<c>maps_tiles_helper.h:73-80</c>). Stored in the third metadata word of the tile.
/// </summary>
public enum DaemonCaveCaptureBonus : uint
{
    /// <summary>The cave has not been visited yet, and nothing was ever assigned to it.</summary>
    Empty = 0,

    /// <summary><c>GET_1000_EXPERIENCE</c>.</summary>
    Get1000Experience = 1,

    /// <summary><c>GET_1000_EXPERIENCE_AND_2500_GOLD</c>.</summary>
    Get1000ExperienceAnd2500Gold = 2,

    /// <summary><c>GET_1000_EXPERIENCE_AND_ARTIFACT</c>.</summary>
    Get1000ExperienceAndArtifact = 3,

    /// <summary><c>PAY_2500_GOLD</c> — the gold amount itself lives in the second metadata word.</summary>
    Pay2500Gold = 4,
}

/// <summary>
/// What a visited Shipwreck hands out, <c>ShipwreckCaptureCondition</c>
/// (<c>maps_tiles_helper.h:82-89</c>). Stored in the third metadata word of the tile.
/// </summary>
public enum ShipwreckCaptureCondition : uint
{
    /// <summary>The wreck has not been visited yet, and nothing was ever assigned to it.</summary>
    Empty = 0,

    /// <summary><c>FIGHT_10_GHOSTS_AND_GET_1000_GOLD</c>.</summary>
    Fight10GhostsAndGet1000Gold = 1,

    /// <summary><c>FIGHT_15_GHOSTS_AND_GET_2000_GOLD</c>.</summary>
    Fight15GhostsAndGet2000Gold = 2,

    /// <summary><c>FIGHT_25_GHOSTS_AND_GET_5000_GOLD</c>.</summary>
    Fight25GhostsAndGet5000Gold = 3,

    /// <summary><c>FIGHT_50_GHOSTS_AND_GET_2000_GOLD_WITH_ARTIFACT</c>.</summary>
    Fight50GhostsAndGet2000GoldWithArtifact = 4,
}
