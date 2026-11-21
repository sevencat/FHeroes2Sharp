using System.Diagnostics;
using System.Text;

using FHeroes2.Game.Artifacts;

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

    /// <summary>
    /// A skill with its level in front, <c>Skill::Secondary::GetName()</c> (<c>skill.cpp:457-471</c>).
    /// </summary>
    /// <remarks>
    /// 参考用一张四十二格的扁平表，下标是 <c>(等级 - 1) + (技能 - 1) * 3</c>；端口用两个查表拼字符串，
    /// 结果一致而不用维护那张表。等级或技能非法时参考返回 "unknown"，端口照抄小写。
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <param name="level">The hero's level in it.</param>
    /// <returns>The name, e.g. <c>"Basic Luck"</c>.</returns>
    public static string GetFullName(SecondarySkill skill, SkillLevel level)
    {
        if (skill <= SecondarySkill.Unknown || skill > SecondarySkill.Estates || level is < SkillLevel.Basic or > SkillLevel.Expert)
        {
            return "unknown";
        }

        string prefix = level switch
        {
            SkillLevel.Basic => "Basic ",
            SkillLevel.Advanced => "Advanced ",
            _ => "Expert ",
        };

        return prefix + GetName(skill);
    }

    /// <summary>
    /// What a level of leadership adds to morale, and the line that explains it,
    /// <c>Skill::getLeadershipModifiers()</c> (<c>skill.cpp:852-865</c>).
    /// </summary>
    /// <remarks>
    /// 数值就是技能行里的值（1/2/3），所以这个函数的重点是那句 <c>"Leadership +1\n"</c>：界面把全部加成
    /// 一条行拼起来给玩家看，拼接格式在参考里是硬编码的。
    /// </remarks>
    /// <param name="level">The hero's level in leadership.</param>
    /// <param name="text">Where to append the line; <see langword="null"/> to only compute.</param>
    /// <returns>The modifier, 0 without the skill.</returns>
    public static int GetLeadershipModifiers(SkillLevel level, StringBuilder? text) =>
        AppendModifierLine(SecondarySkill.Leadership, level, text);

    /// <summary>
    /// The same for luck, <c>Skill::getLuckModifiers()</c> (<c>skill.cpp:867-881</c>).
    /// </summary>
    /// <param name="level">The hero's level in luck.</param>
    /// <param name="text">Where to append the line; <see langword="null"/> to only compute.</param>
    /// <returns>The modifier, 0 without the skill.</returns>
    public static int GetLuckModifiers(SkillLevel level, StringBuilder? text) =>
        AppendModifierLine(SecondarySkill.Luck, level, text);

    /// <summary>
    /// The name of a level, <c>Skill::Level::String()</c> (<c>skill.cpp:325-341</c>).
    /// </summary>
    /// <remarks>
    /// 参考对不在 <c>BASIC..EXPERT</c> 里的数 <c>assert( 0 )</c> 之后返回 "None"；端口照抄——等级只有四个值，
    /// 越界说明调用方把技能编号当成了等级。
    /// </remarks>
    /// <param name="level">The level.</param>
    /// <returns>"Basic", "Advanced", "Expert", or "None".</returns>
    public static string GetLevelName(SkillLevel level) => level switch
    {
        SkillLevel.Basic => "Basic",
        SkillLevel.Advanced => "Advanced",
        SkillLevel.Expert => "Expert",
        _ => UnsupportedLevel(level),
    };

    /// <summary>
    /// The level name with the necromancy bonus appended, <c>Skill::Level::StringWithBonus()</c>
    /// (<c>skill.cpp:343-350</c>).
    /// </summary>
    /// <remarks>
    /// 只有招魂、且神龛/宝物加成大于零时才补 <c>"+N"</c>：英雄信息页把「Expert+2」当作那一格的值，
    /// 因为招魂的实际效果是技能行加上加成。
    /// </remarks>
    /// <param name="skill">The skill and the hero's level in it.</param>
    /// <param name="necromancyBonus">The bonus from shrines and artifacts.</param>
    /// <returns>The level name, possibly with a bonus.</returns>
    public static string GetLevelNameWithBonus(SecondarySkillLevel skill, uint necromancyBonus) =>
        skill.Skill == SecondarySkill.Necromancy && necromancyBonus > 0
            ? GetLevelName(skill.Level) + "+" + necromancyBonus
            : GetLevelName(skill.Level);

    /// <summary>
    /// A skill's name with the level in front and the necromancy bonus appended,
    /// <c>Skill::Secondary::GetNameWithBonus()</c> (<c>skill.cpp:473-479</c>).
    /// </summary>
    /// <param name="skill">The skill and the hero's level in it.</param>
    /// <param name="necromancyBonus">The bonus from shrines and artifacts.</param>
    /// <returns>The name, possibly followed by <c>" (+2)"</c>.</returns>
    public static string GetNameWithBonus(SecondarySkillLevel skill, uint necromancyBonus) =>
        skill.Skill == SecondarySkill.Necromancy && necromancyBonus > 0
            ? GetFullName(skill.Skill, skill.Level) + " (+" + necromancyBonus + ")"
            : GetFullName(skill.Skill, skill.Level);

    /// <summary>
    /// How many extra necromancy points a hero's realm has, <c>Skill::GetNecromancyBonus()</c>
    /// (<c>skill.cpp:882-888</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两点来源：每座招魂神龛一点，宝物栏里任意一件招魂系宝物一点，合计封顶七点。七点上限是参考写死的，
    /// 对应「满编七座神龛」，再堆也不涨。
    /// </para>
    /// <para>
    /// 参考读全局 <c>world</c> 取王国；端口把 <see cref="World"/> 传进来，<paramref name="hero"/> 的阵营决定看谁的
    /// 王国——被击败的英雄没有王国时会拿到零座神龛，神龛数因此夹到非负。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero whose realm and bag are counted.</param>
    /// <param name="world">The world holding the kingdoms.</param>
    /// <returns>The bonus, 0 to 7.</returns>
    public static uint GetNecromancyBonus(Hero hero, World world)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(world);

        uint shrineCount = (uint)Math.Max(0, world.GetKingdom(hero.Color).CountNecromancyShrines());
        uint artifactEffect = hero.BagArtifacts.IsBonusPresent(ArtifactBonusType.NecromancySkill) ? 1U : 0U;

        return Math.Min(7U, shrineCount + artifactEffect);
    }

    /// <summary>
    /// What share of the fallen a hero raises, <c>Skill::GetNecromancyPercent()</c> (<c>skill.cpp:890-896</c>).
    /// </summary>
    /// <remarks>
    /// 技能本身给 10/20/30%，每点 <see cref="GetNecromancyBonus"/> 再加 10%，封顶 100%——满值就是「敌军全灭都
    /// 变成骷髅」。
    /// </remarks>
    /// <param name="hero">The hero.</param>
    /// <param name="world">The world holding the hero's realm.</param>
    /// <returns>The percentage, 0 to 100.</returns>
    public static uint GetNecromancyPercent(Hero hero, World world)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(world);

        uint percent = GetValue(SecondarySkill.Necromancy, hero.GetLevelSkill(SecondarySkill.Necromancy));

        return Math.Min(percent + (10U * GetNecromancyBonus(hero, world)), 100U);
    }

    /// <summary>Logs the reference's <c>assert( 0 )</c> for a value that is not a level.</summary>
    /// <param name="level">The offending value.</param>
    /// <returns>The word the reference falls through to, <c>"None"</c>.</returns>
    private static string UnsupportedLevel(SkillLevel level)
    {
        Debug.Assert(false, $"Not a skill level: {level}.");

        return "None";
    }

    /// <summary>
    /// Writes <c>"Leadership +1\n"</c> for one skill of the two that only shift a scale.
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <param name="level">The hero's level in it.</param>
    /// <param name="text">Where to write, possibly <see langword="null"/>.</param>
    /// <returns>The value of that level.</returns>
    private static int AppendModifierLine(SecondarySkill skill, SkillLevel level, StringBuilder? text)
    {
        int value = GetValue(skill, level);

        if (value != 0 && text != null)
        {
            text.Append(GetName(skill));
            text.Append(value < 0 ? " " : " +").Append(value);
            text.Append('\n');
        }

        return value;
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
