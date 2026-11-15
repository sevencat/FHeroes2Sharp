namespace FHeroes2.Game.Spells;

/// <summary>
/// The id constants of <c>Spell</c>, <c>src/fheroes2/spell/spell.h:38-120</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the ids in an unnamed <c>enum : int32_t</c> inside the <c>Spell</c> class and wraps them
/// in <c>Spell</c> objects that carry the level, the school and the artwork. Only the ids are here: they are
/// part of the data files and of the monster ability table (<c>SPELL_CASTER, 20, Spell::BLIND</c>), so they
/// have to be exact; the behaviour of a spell waits for the battle engine.
/// </para>
/// <para>
/// Three groups are not ordinary spells and the reference says so: <see cref="Random"/> and
/// <see cref="Random1"/>..<see cref="Random5"/> are placeholders for a random spell of a level and must never
/// reach a spell book, and <see cref="Petrify"/> is only ever cast by a medusa's gaze. They still sit in the
/// same numbering, which is why <see cref="Count"/> is 73 and not 67.
/// </para>
/// </remarks>
public enum SpellType
{
    /// <summary><c>NONE</c>: no spell, the value <c>Spell()</c> starts with.</summary>
    None = 0,
    Fireball = 1,
    FireBlast = 2,
    LightningBolt = 3,
    ChainLightning = 4,
    Teleport = 5,
    Cure = 6,
    MassCure = 7,
    Resurrect = 8,
    ResurrectTrue = 9,
    Haste = 10,
    MassHaste = 11,
    Slow = 12,
    MassSlow = 13,
    Blind = 14,
    Bless = 15,
    MassBless = 16,
    StoneSkin = 17,
    SteelSkin = 18,
    Curse = 19,
    MassCurse = 20,
    HolyWord = 21,
    HolyShout = 22,
    AntiMagic = 23,
    Dispel = 24,
    MassDispel = 25,
    Arrow = 26,
    Berserker = 27,
    Armageddon = 28,
    ElementalStorm = 29,
    MeteorShower = 30,
    Paralyze = 31,
    Hypnotize = 32,
    ColdRay = 33,
    ColdRing = 34,
    DisruptingRay = 35,
    DeathRipple = 36,
    DeathWave = 37,
    DragonSlayer = 38,
    BloodLust = 39,
    AnimateDead = 40,
    MirrorImage = 41,
    Shield = 42,
    MassShield = 43,
    SummonEElement = 44,
    SummonAElement = 45,
    SummonFElement = 46,
    SummonWElement = 47,
    Earthquake = 48,
    ViewMines = 49,
    ViewResources = 50,
    ViewArtifacts = 51,
    ViewTowns = 52,
    ViewHeroes = 53,
    ViewAll = 54,
    IdentifyHero = 55,
    SummonBoat = 56,
    DimensionDoor = 57,
    TownGate = 58,
    TownPortal = 59,
    Visions = 60,
    Haunt = 61,
    SetEGuardian = 62,
    SetAGuardian = 63,
    SetFGuardian = 64,
    SetWGuardian = 65,

    /// <summary><c>RANDOM</c>: placeholder for a random spell, never added to a spell book.</summary>
    Random = 66,
    Random1 = 67,
    Random2 = 68,
    Random3 = 69,
    Random4 = 70,
    Random5 = 71,

    /// <summary><c>PETRIFY</c>: only cast by the medusa's gaze, never added to a spell book.</summary>
    Petrify = 72,

    /// <summary><c>SPELL_COUNT</c>: one past <see cref="Petrify"/>, not a spell.</summary>
    Count = 73,
}

/// <summary>
/// The helpers of the <c>Spell</c> object the port needs before the object itself exists.
/// </summary>
public static class SpellTypes
{
    /// <summary>
    /// Whether the id names a spell, <c>Spell::isValid()</c> (<c>spell.cpp</c>: <c>id != NONE</c>).
    /// </summary>
    /// <param name="spell">The id.</param>
    /// <returns><see langword="false"/> for <see cref="SpellType.None"/> and for anything out of range.</returns>
    public static bool IsValid(SpellType spell) => spell > SpellType.None && spell < SpellType.Count;
}
