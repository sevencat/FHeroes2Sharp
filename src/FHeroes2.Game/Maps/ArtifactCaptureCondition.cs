namespace FHeroes2.Game.Maps;

/// <summary>
/// What a guarded artifact asks before it can be taken, <c>Maps::ArtifactCaptureCondition</c>
/// (<c>maps_tiles_helper.h:53</c>).
/// </summary>
/// <remarks>
/// The value is stored in <c>metadata[2]</c> of an artifact tile, so the numbering is part of the map
/// data and must not be renumbered. Value 0 means "no conditions" — the reference notes that every
/// enumeration of this kind starts at an empty value so that <c>resetObjectInfoOnTile()</c> needs no
/// special cases.
/// </remarks>
public enum ArtifactCaptureCondition : uint
{
    /// <summary>Unguarded.</summary>
    NoConditions = 0,

    /// <summary>Ask for 2000 gold.</summary>
    Pay2000Gold = 1,

    /// <summary>Ask for 2500 gold and three resources.</summary>
    Pay2500GoldAnd3Resources = 2,

    /// <summary>Ask for 3000 gold and five resources.</summary>
    Pay3000GoldAnd5Resources = 3,

    /// <summary>Ask for the wisdom skill.</summary>
    HaveWisdomSkill = 4,

    /// <summary>Ask for the leadership skill.</summary>
    HaveLeadershipSkill = 5,

    /// <summary>Fight 50 rogues.</summary>
    Fight50Rogues = 6,

    /// <summary>Fight one genie.</summary>
    Fight1Genie = 7,

    /// <summary>Fight one paladin.</summary>
    Fight1Paladin = 8,

    /// <summary>Fight one cyclops.</summary>
    Fight1Cyclops = 9,

    /// <summary>Fight one phoenix.</summary>
    Fight1Phoenix = 10,

    /// <summary>Fight one green dragon.</summary>
    Fight1GreenDragon = 11,

    /// <summary>Fight one titan.</summary>
    Fight1Titan = 12,

    /// <summary>Fight one bone dragon.</summary>
    Fight1BoneDragon = 13,

    /// <summary>
    /// The artifact is a spell scroll and <c>metadata[1]</c> holds the spell; there is no 14.
    /// </summary>
    ContainsSpell = 15,
}
