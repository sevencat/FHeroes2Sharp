namespace FHeroes2.Game.Heroes;

/// <summary>
/// The fourteen secondary skills a hero can learn — <c>Skill::Secondary</c> (<c>skill.h:70-87</c>).
/// </summary>
/// <remarks>
/// <para>
/// The values are the reference's, and the order is the reference's <c>Secondary::</c> block: the id of a
/// skill is also its position in every table of this port — <see cref="SecondarySkills.GetValue"/>,
/// <see cref="SecondarySkillTable"/> and <see cref="SecSkills"/> all index by it.
/// </para>
/// <para>
/// The struct <c>Skill::SecondarySkillValues</c> (<c>skill_static.h:45-61</c>) names the same fourteen
/// skills in <b>alphabetical</b> field order, so every literal copied from <c>game_static.cpp</c> has to be
/// reshuffled once, on the way in. That reshuffle is the only place where a transcription slip is silent —
/// the rows are all fourteen numbers long and every one of them is a plausible value — so the tests pin
/// single fields by name (<c>pathfinding</c>, <c>ballistics</c>, <c>necromancy</c>) rather than whole rows.
/// </para>
/// </remarks>
public enum SecondarySkill
{
    /// <summary>No skill — <c>Secondary::UNKNOWN</c>.</summary>
    Unknown = 0,

    /// <summary><c>PATHFINDING</c> — reduces the movement penalty of rough terrain.</summary>
    Pathfinding = 1,

    /// <summary><c>ARCHERY</c> — increases the damage of ranged attacks.</summary>
    Archery = 2,

    /// <summary><c>LOGISTICS</c> — increases the movement points of the hero.</summary>
    Logistics = 3,

    /// <summary><c>SCOUTING</c> — increases the sight radius.</summary>
    Scouting = 4,

    /// <summary><c>DIPLOMACY</c> — makes wandering monsters join and lowers the surrender cost.</summary>
    Diplomacy = 5,

    /// <summary><c>NAVIGATION</c> — increases the movement points at sea.</summary>
    Navigation = 6,

    /// <summary><c>LEADERSHIP</c> — raises the morale of the army.</summary>
    Leadership = 7,

    /// <summary><c>WISDOM</c> — allows spells of a higher level to be learned.</summary>
    Wisdom = 8,

    /// <summary><c>MYSTICISM</c> — regenerates spell points every day.</summary>
    Mysticism = 9,

    /// <summary><c>LUCK</c> — raises the luck of the army.</summary>
    Luck = 10,

    /// <summary><c>BALLISTICS</c> — makes the catapult shoot more accurately.</summary>
    Ballistics = 11,

    /// <summary><c>EAGLE_EYE</c> — lets the hero learn spells from the enemy's mage guild.</summary>
    EagleEye = 12,

    /// <summary><c>NECROMANCY</c> — raises skeletons from the fallen.</summary>
    Necromancy = 13,

    /// <summary><c>ESTATES</c> — adds gold to the kingdom every day.</summary>
    Estates = 14,
}
