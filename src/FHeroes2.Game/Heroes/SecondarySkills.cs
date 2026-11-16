namespace FHeroes2.Game.Heroes;

/// <summary>
/// The tables and lookups of <see cref="SecondarySkill"/>: how much a level of a skill is worth, which
/// skills a witch's hut may offer, and what a level of diplomacy does to a surrender — the value rows of
/// <c>secondarySkillValuesPerLevel</c> (<c>game_static.cpp:91-104</c>), the accessor
/// <c>GameStatic::GetSecondarySkillValuesPerLevel</c> (<c>game_static.cpp:227</c>),
/// <c>GameStatic::getSecondarySkillsForWitchsHut</c> (<c>game_static.cpp:265</c>) and
/// <c>Skill::GetDiplomacySurrenderCostDiscount</c> (<c>skill.cpp:898</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The rows are not the game's own units.</b> Every number below means something different in its own
/// row — a percentage of movement points for pathfinding, a percentage of damage for archery, a flat gold
/// amount for estates, a flat level for wisdom — which is why <see cref="GetValue"/> returns a bare number
/// and every caller has to say what it means. The reference's doc comment does the same and warns that
/// only the caller knows the unit.
/// </para>
/// <para>
/// The one row where the number is <b>not</b> a magnitude is estate's: 100 / 250 / 500 gold per day. Leaving
/// the units out of the table is what makes <see cref="SecSkills.GetValue"/> usable both by the
/// "gold per day" and by the "percent of something" callers.
/// </para>
/// </remarks>
public static class SecondarySkills
{
    /// <summary>
    /// The number of skills — <c>Skill::numOfSecondarySkills</c>. Every table indexed by
    /// <see cref="SecondarySkill"/> is this long.
    /// </summary>
    public const int Count = 14;

    /// <summary>
    /// The English name of a skill, <c>Skill::Secondary::String()</c> (<c>skill.cpp:417-455</c>).
    /// </summary>
    /// <remarks>
    /// The reference returns <c>_( "..." )</c>, the translation-table lookup. The port has no translation
    /// layer, so the original English text is returned — which is what the reference does without a
    /// translation loaded, and what a screen shows. An unknown skill asserts in the reference and answers an
    /// empty string here, because a name is drawn and an assertion would take the whole screen down.
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <returns>The name, or an empty string for <see cref="SecondarySkill.Unknown"/>.</returns>
    public static string GetName(SecondarySkill skill) => skill switch
    {
        SecondarySkill.Pathfinding => "Pathfinding",
        SecondarySkill.Archery => "Archery",
        SecondarySkill.Logistics => "Logistics",
        SecondarySkill.Scouting => "Scouting",
        SecondarySkill.Diplomacy => "Diplomacy",
        SecondarySkill.Navigation => "Navigation",
        SecondarySkill.Leadership => "Leadership",
        SecondarySkill.Wisdom => "Wisdom",
        SecondarySkill.Mysticism => "Mysticism",
        SecondarySkill.Luck => "Luck",
        SecondarySkill.Ballistics => "Ballistics",
        SecondarySkill.EagleEye => "Eagle Eye",
        SecondarySkill.Necromancy => "Necromancy",
        SecondarySkill.Estates => "Estates",
        _ => string.Empty
    };

    /// <summary>
    /// The sprite of a skill in the skill list, <c>Skill::Secondary::GetIndexSprite1()</c>
    /// (<c>skill.cpp:407-410</c>).
    /// </summary>
    /// <remarks>
    /// The id <b>is</b> the sprite index inside <c>ICN::SECSKILL</c>, so a valid skill answers itself and
    /// anything else answers zero.
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <returns>The sprite index, zero for an invalid skill.</returns>
    public static int GetIndexSprite1(SecondarySkill skill) =>
        skill > SecondarySkill.Unknown && skill <= SecondarySkill.Estates ? (int)skill : 0;

    /// <summary>
    /// The same sprite for <c>ICN::EDITOR</c>, <c>Skill::Secondary::GetIndexSprite2()</c>
    /// (<c>skill.cpp:412-415</c>).
    /// </summary>
    /// <remarks>
    /// The editor's list has no placeholder for "no skill", so the index is one lower and an invalid skill is
    /// <c>0xFF</c> — the reference's way of saying "draw nothing", kept as a number because the editor reads
    /// it as one.
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <returns>The sprite index, <c>0xFF</c> for an invalid skill.</returns>
    public static int GetIndexSprite2(SecondarySkill skill) =>
        skill > SecondarySkill.Unknown && skill <= SecondarySkill.Estates ? (int)skill - 1 : 0xFF;

    /// <summary>
    /// The fourteen skills in id order, i.e. <c>Skill::allSecondarySkills</c>. Used wherever the reference
    /// walks "every skill" — the level-up blacklist, the weight tables, the debug dump.
    /// </summary>
    public static IReadOnlyList<SecondarySkill> All { get; } =
    [
        SecondarySkill.Pathfinding,
        SecondarySkill.Archery,
        SecondarySkill.Logistics,
        SecondarySkill.Scouting,
        SecondarySkill.Diplomacy,
        SecondarySkill.Navigation,
        SecondarySkill.Leadership,
        SecondarySkill.Wisdom,
        SecondarySkill.Mysticism,
        SecondarySkill.Luck,
        SecondarySkill.Ballistics,
        SecondarySkill.EagleEye,
        SecondarySkill.Necromancy,
        SecondarySkill.Estates,
    ];

    /// <summary>
    /// The order in which the constructor of <see cref="SecSkills"/> adds the initial skills of a race —
    /// <c>Skill::SecSkills::SecSkills( race )</c> (<c>skill.cpp:626-681</c>).
    /// </summary>
    /// <remarks>
    /// This is neither the id order nor the alphabetical order of the reference's struct: it is the order the
    /// fourteen <c>if</c> blocks of the constructor are written in. It matters because it is the order a
    /// knight's «leadership, ballistics» is stored and therefore the order the hero screen lists them in.
    /// </remarks>
    public static IReadOnlyList<SecondarySkill> InitialFillOrder { get; } =
    [
        SecondarySkill.Archery,
        SecondarySkill.Diplomacy,
        SecondarySkill.EagleEye,
        SecondarySkill.Estates,
        SecondarySkill.Logistics,
        SecondarySkill.Luck,
        SecondarySkill.Mysticism,
        SecondarySkill.Pathfinding,
        SecondarySkill.Leadership,
        SecondarySkill.Ballistics,
        SecondarySkill.Navigation,
        SecondarySkill.Scouting,
        SecondarySkill.Necromancy,
        SecondarySkill.Wisdom,
    ];

    /// <summary>
    /// The skills a witch's hut may teach — <c>GameStatic::getSecondarySkillsForWitchsHut</c>.
    /// </summary>
    /// <remarks>
    /// Every skill except leadership and necromancy, in the reference's listing order (wisdom before
    /// mysticism there, unlike the id order). The list is a hint for the object's text and generator; the
    /// hut itself picks one at random.
    /// </remarks>
    public static IReadOnlyList<SecondarySkill> WitchsHutSkills { get; } =
    [
        SecondarySkill.Pathfinding,
        SecondarySkill.Archery,
        SecondarySkill.Logistics,
        SecondarySkill.Scouting,
        SecondarySkill.Diplomacy,
        SecondarySkill.Navigation,
        SecondarySkill.Wisdom,
        SecondarySkill.Mysticism,
        SecondarySkill.Luck,
        SecondarySkill.Ballistics,
        SecondarySkill.EagleEye,
        SecondarySkill.Estates,
    ];

    /// <summary>
    /// The value of one level of a skill — <c>Skill::Secondary::GetValue()</c> (<c>skill.cpp:119</c>),
    /// which reads the row of <c>secondarySkillValuesPerLevel</c> selected by the skill.
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <param name="level">The level of the hero in that skill.</param>
    /// <returns>
    /// The value of the row for that level, or zero for <see cref="SkillLevel.None"/> — the reference returns
    /// zero for every level it does not know, including <c>NONE</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="skill"/> is not one of the fourteen skills. The reference returns <c>nullptr</c> for
    /// an unknown skill and <c>GetValue</c> turns that into zero; a caller with
    /// <see cref="SecondarySkill.Unknown"/> in hand has a bug, so the port throws instead of returning a
    /// number that looks like a real value.
    /// </exception>
    public static ushort GetValue(SecondarySkill skill, SkillLevel level)
    {
        return level switch
        {
            SkillLevel.Basic => GetRow(skill).Basic,
            SkillLevel.Advanced => GetRow(skill).Advanced,
            SkillLevel.Expert => GetRow(skill).Expert,
            _ => 0,
        };
    }

    /// <summary>
    /// The discount a level of diplomacy gives on the surrender cost — <c>Skill::GetDiplomacySurrenderCostDiscount</c>.
    /// </summary>
    /// <param name="level">The level of the hero in diplomacy.</param>
    /// <returns>40, 30 or 20 percent, and zero without the skill.</returns>
    public static int GetDiplomacySurrenderCostDiscount(SkillLevel level)
    {
        return level switch
        {
            SkillLevel.Basic => 40,
            SkillLevel.Advanced => 30,
            SkillLevel.Expert => 20,
            _ => 0,
        };
    }

    private static (ushort Basic, ushort Advanced, ushort Expert) GetRow(SecondarySkill skill)
    {
        int index = (int)skill - 1;
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(skill), skill, "Not one of the fourteen secondary skills.");
        }

        return Rows[index];
    }

    /// <summary>
    /// <c>secondarySkillValuesPerLevel</c> (<c>game_static.cpp:91-104</c>), in id order — already reshuffled
    /// out of the alphabetical field order of <c>Skill::SecondarySkillValues</c>.
    /// </summary>
    private static readonly (ushort Basic, ushort Advanced, ushort Expert)[] Rows =
    [
        (25, 50, 100), // pathfinding
        (10, 25, 50), // archery
        (10, 20, 30), // logistics
        (1, 2, 3), // scouting
        (25, 50, 100), // diplomacy
        (33, 66, 100), // navigation
        (1, 2, 3), // leadership
        (3, 4, 5), // wisdom
        (1, 2, 3), // mysticism
        (1, 2, 3), // luck
        (0, 0, 0), // ballistics
        (20, 30, 40), // eagle eye
        (10, 20, 30), // necromancy
        (100, 250, 500), // estates
    ];
}
