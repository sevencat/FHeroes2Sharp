namespace FHeroes2.Game.Heroes;

/// <summary>
/// The four primary skills, <c>Skill::Primary</c>'s anonymous enum (<c>skill.h:160-165</c>).
/// </summary>
/// <remarks>
/// The numbers are the reference's and start at one, not zero: <c>UNKNOWN</c> is zero so that an uninitialised
/// value is not a skill. They are used as indices by the MP2 loader and as the argument of
/// <c>Heroes::IncreasePrimarySkill()</c>.
/// </remarks>
public enum PrimarySkill
{
    /// <summary>Not a skill, <c>Skill::Primary::UNKNOWN</c>.</summary>
    Unknown = 0,

    /// <summary>Attack, <c>Skill::Primary::ATTACK</c>.</summary>
    Attack = 1,

    /// <summary>Defense, <c>Skill::Primary::DEFENSE</c>.</summary>
    Defense = 2,

    /// <summary>Spell power, <c>Skill::Primary::POWER</c>.</summary>
    Power = 3,

    /// <summary>Knowledge, <c>Skill::Primary::KNOWLEDGE</c>.</summary>
    Knowledge = 4,
}
