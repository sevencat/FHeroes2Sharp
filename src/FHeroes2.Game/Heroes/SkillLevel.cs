namespace FHeroes2.Game.Heroes;

/// <summary>
/// The level of a skill, the port of <c>Skill::Level</c> (<c>skill.h:29</c>).
/// </summary>
/// <remarks>
/// <para>
/// The values are ordered and the orders are compared: the terrain penalty of the pathfinder asks
/// "is this at least basic" and the artifact bonuses ask "is this expert", so the enum keeps the
/// reference's ascending order instead of a bit mask.
/// </para>
/// <para>
/// The reference stores the level as a <c>uint8_t</c> and passes it straight into
/// <c>Ground::GetPenalty()</c>; the port types the parameter as this enum so that a raw <c>3</c> cannot
/// silently mean "expert" at a call site that meant a skill id.
/// </para>
/// </remarks>
public enum SkillLevel
{
    /// <summary>The hero has not learned the skill, <c>Skill::Level::NONE</c>.</summary>
    None = 0,

    /// <summary>Basic level, <c>Skill::Level::BASIC</c>.</summary>
    Basic = 1,

    /// <summary>Advanced level, <c>Skill::Level::ADVANCED</c>.</summary>
    Advanced = 2,

    /// <summary>Expert level, <c>Skill::Level::EXPERT</c>.</summary>
    Expert = 3,
}
