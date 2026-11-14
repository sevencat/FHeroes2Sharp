namespace FHeroes2.Assets.Audio;

/// <summary>
/// Background music tracks of the original game, mirroring <c>MUS::MusicTrack</c>
/// (<c>agg/mus.h</c>). The numeric values are the music track ids used by the external music
/// file names, so they must not be renumbered.
/// </summary>
public enum MusicTrack
{
    /// <summary>No track; it has no external file.</summary>
    Unused = 0,

    /// <summary>Data track of the CD; not in use, it has no external file.</summary>
    DataTrack = 1,

    Battle1 = 2,
    Battle2 = 3,
    Battle3 = 4,
    SorceressCastle = 5,
    WarlockCastle = 6,
    NecromancerCastle = 7,
    KnightCastle = 8,
    BarbarianCastle = 9,
    WizardCastle = 10,
    Lava = 11,
    Wasteland = 12,
    Desert = 13,
    Snow = 14,
    Swamp = 15,
    Ocean = 16,
    Dirt = 17,
    Grass = 18,
    LostGame = 19,
    NewWeek = 20,
    NewMonth = 21,
    ArchibaldCampaignScreen = 22,
    Puzzle = 23,
    RolandCampaignScreen = 24,

    /// <summary>Reserved by the original game; it has no track of its own.</summary>
    Caravans = 25,

    /// <summary>Reserved by the original game; it has no track of its own.</summary>
    Caravans2 = 26,

    /// <summary>Reserved by the original game; it has no track of its own.</summary>
    Caravans3 = 27,

    ComputerTurn = 28,
    BattleWin = 29,
    BattleLose = 30,
    Dungeon = 31,
    Waterspring = 32,
    Arabian = 33,
    Hillfort = 34,
    Treehouse = 35,
    Demoncave = 36,
    Experience = 37,
    Skill = 38,
    Watchtower = 39,
    Xanadu = 40,
    UltimateArtifact = 41,
    MainMenu = 42,
    Victory = 43,

    /// <summary>Sentinel of the reference implementation; it has no external file.</summary>
    Unknown = 44,
}
