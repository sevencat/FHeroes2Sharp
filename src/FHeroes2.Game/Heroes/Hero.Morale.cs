using System.Text;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The luck and the morale of a hero, <c>Heroes::GetLuck()</c> and <c>Heroes::GetMorale()</c>
/// (<c>heroes.cpp:1033-1103</c>).
/// </summary>
/// <remarks>
/// <para>
/// Both are a sum of four terms in the reference, in this order: the secondary skill (luck or leadership,
/// one point per level), the objects the hero has visited (<c>GameStatic::getObjectLuckEffect</c> and its
/// morale twin, e.g. a fountain for +1), the artifact bonuses, and — last, overriding everything — an
/// artifact that grants the maximum of the scale outright. The sum is clamped at the end, so a hero with
/// expert leadership and a fountain is still only <see cref="Morale.Blood"/>.
/// </para>
/// <para>
/// The port has all four terms now: the army's own share (the race penalty, the undead penalty, the garrison
/// bonus of a castle) comes from <see cref="Army.IArmyCommander"/>'s <c>GetArmy().GetMoraleModificator()</c>, and
/// the artifact share is <see cref="GetArtifactMoraleModifier"/> — the reference's
/// <c>HeroBase::GetMoraleModificator()</c> minus the army half it already folded in.
/// </para>
/// <para>
/// <b>The army's share has to be added here now</b>: <see cref="Heroes.Hero"/> implements
/// <see cref="Army.IArmyCommander"/>, so <c>Army.GetMorale()</c> asks the hero and not the other way round —
/// stop adding it and a hero's army would silently lose its race and undead penalties.
/// </para>
/// <para>
/// <b>The objects are counted by type, not by visit</b>: <c>getAllVisitedObjectTypes()</c> is a set, so three
/// temples are still +2. That is also what makes the effect disappear by itself — the ticks in
/// <c>Hero.Visit.cs</c> drop the records, and the next <see cref="GetMorale"/> recomputes without them
/// (ADR-122).
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// The morale of the hero's army, <c>Heroes::GetMorale()</c> (<c>heroes.cpp:1033</c>).
    /// </summary>
    /// <returns>
    /// <see cref="Morale.Normal"/> plus the leadership skill, the visited-object effect and the army's own
    /// modificator, clamped into the scale.
    /// </returns>
    public int GetMorale() => GetMoraleWithModifiers(null);

    /// <summary>
    /// The morale together with the lines that explain it, <c>Heroes::getMoraleWithModifiers()</c>
    /// (<c>heroes.cpp:1038-1066</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 顺序与参考一致：基础值、领导术、访问过的对象、宝物（含海战加成与诅咒）。<c>MAXIMUM_MORALE</c> 的神器放在
    /// <b>最后</b>并且直接返回满值，所以前面的加总再大也不会被它压过——参考注释里专门写了「必须在最后判」。
    /// </para>
    /// <para>
    /// <paramref name="text"/> 非空时按参考的格式写「技能名 +N\n」与「神器名 gives you maximum morale」；参考还会
    /// 逐件列出宝物，端口的 <see cref="Artifacts.ArtifactBag.GetTotalArtifactEffectValue(Artifacts.ArtifactBonusType)"/>
    /// 只有数值版本，所以宝物那一行暂时缺失。
    /// </para>
    /// </remarks>
    /// <param name="text">Optional explanation to append to; <see langword="null"/> for the number only.</param>
    /// <returns>The morale, clamped into the scale unless a maximum-morale artifact is present.</returns>
    public int GetMoraleWithModifiers(StringBuilder? text)
    {
        int result = Morale.Normal
                     + AppendSkillModifier(text, SecondarySkill.Leadership)
                     + GetVisitedObjectsMorale()
                     + Army.GetMoraleModificator()
                     + GetArtifactMoraleModifier();

        ArtifactType maximumMorale = BagArtifacts.GetFirstArtifactWithBonus(ArtifactBonusType.MaximumMorale);
        if (maximumMorale != ArtifactType.Unknown)
        {
            text?.Append(ArtifactTable.Get(maximumMorale).Name).Append(" gives you maximum morale");
            return Morale.Blood;
        }

        TrimTrailingNewLine(text);

        return Morale.Normalize(result);
    }

    /// <summary>
    /// The luck of the hero's army, <c>Heroes::GetLuck()</c> (<c>heroes.cpp:1069</c>).
    /// </summary>
    /// <returns>
    /// <see cref="Luck.Normal"/> plus the luck skill, the visited-object effect and the army's own
    /// modificator, clamped into the scale.
    /// </returns>
    public int GetLuck() => GetLuckWithModifiers(null);

    /// <summary>
    /// The luck together with the lines that explain it, <c>Heroes::getLuckWithModifiers()</c>
    /// (<c>heroes.cpp:1074-1102</c>).
    /// </summary>
    /// <remarks>
    /// 规则与 <see cref="GetMoraleWithModifiers"/> 完全相同，只是换了几张表：技能是 <see cref="SecondarySkill.Luck"/>，
    /// 对象效果走 <c>GetObjectLuckEffect</c>，满值神器是 <c>MAXIMUM_LUCK</c>，上限是 <see cref="Luck.Irish"/>。
    /// </remarks>
    /// <param name="text">Optional explanation to append to; <see langword="null"/> for the number only.</param>
    /// <returns>The luck, clamped into the scale unless a maximum-luck artifact is present.</returns>
    public int GetLuckWithModifiers(StringBuilder? text)
    {
        int result = Luck.Normal
                     + AppendSkillModifier(text, SecondarySkill.Luck)
                     + GetVisitedObjectsLuck()
                     + Army.GetLuckModificator()
                     + GetArtifactLuckModifier();

        ArtifactType maximumLuck = BagArtifacts.GetFirstArtifactWithBonus(ArtifactBonusType.MaximumLuck);
        if (maximumLuck != ArtifactType.Unknown)
        {
            text?.Append(ArtifactTable.Get(maximumLuck).Name).Append(" gives you maximum luck");
            return Luck.Irish;
        }

        TrimTrailingNewLine(text);

        return Luck.Normalize(result);
    }

    /// <summary>
    /// The artifact share of the morale, the <c>MORALE</c> half of <c>HeroBase::GetMoraleModificator()</c>
    /// (<c>heroes_base.cpp:60-95</c>).
    /// </summary>
    /// <remarks>
    /// 海战加成只在船长位上算（<c>SEA_BATTLE_MORALE_BOOST</c>），这是 <c>SHIPMASTER</c> 位的第一个实际用处；
    /// 诅咒（<c>MORALE</c> 型）从加总里减掉，所以一件「士气 -1」的宝物能把两件「士气 +1」抵回去。
    /// </remarks>
    private int GetArtifactMoraleModifier() =>
        BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.Morale)
        + (IsShipMaster ? BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.SeaBattleMoraleBoost) : 0)
        - BagArtifacts.GetTotalArtifactEffectValue(ArtifactCurseType.Morale);

    /// <summary>
    /// The artifact share of the luck, the mirror of <see cref="GetArtifactMoraleModifier"/>.
    /// </summary>
    private int GetArtifactLuckModifier() =>
        BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.Luck)
        + (IsShipMaster ? BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.SeaBattleLuckBoost) : 0);

    /// <summary>
    /// Writes one secondary skill's contribution the way the reference does: <c>"Luck +1\n"</c>.
    /// </summary>
    /// <param name="text">Where to write; <see langword="null"/> to only compute.</param>
    /// <param name="skill">The skill to report.</param>
    /// <returns>The skill's value, 0 when the hero has no level in it.</returns>
    private int AppendSkillModifier(StringBuilder? text, SecondarySkill skill)
    {
        // `SecondarySkills` is also the name of the hero's own property, so the static tables have to be spelled
        // out in full here.
        SkillLevel level = _secondarySkills.GetLevel(skill);

        return skill switch
        {
            SecondarySkill.Leadership => global::FHeroes2.Game.Heroes.SecondarySkills.GetLeadershipModifiers(level, text),
            SecondarySkill.Luck => global::FHeroes2.Game.Heroes.SecondarySkills.GetLuckModifiers(level, text),
            _ => (int)_secondarySkills.GetValue(skill),
        };
    }

    /// <summary>
    /// Drops the trailing newline the last explanation line left behind, the <c>pop_back()</c> in
    /// <c>heroes.cpp:1096</c>.
    /// </summary>
    /// <param name="text">The explanation, possibly empty.</param>
    private static void TrimTrailingNewLine(StringBuilder? text)
    {
        if (text is { Length: > 0 } && text[^1] == '\n')
        {
            text.Length--;
        }
    }

    /// <summary>
    /// The morale the visited objects are worth, <c>getObjectMoraleModifiers( getAllVisitedObjectTypes() )</c>.
    /// </summary>
    /// <returns>The sum over the distinct visited types; a negative number for graveyards and wrecks.</returns>
    private int GetVisitedObjectsMorale() => GetAllVisitedObjectTypes().Sum(GameStatic.GetObjectMoraleEffect);

    /// <summary>
    /// The luck the visited objects are worth, <c>getObjectLuckModifiers( getAllVisitedObjectTypes() )</c>.
    /// </summary>
    /// <returns>The sum over the distinct visited types; a negative number for the pyramid and the black cat.</returns>
    private int GetVisitedObjectsLuck() => GetAllVisitedObjectTypes().Sum(GameStatic.GetObjectLuckEffect);
}
