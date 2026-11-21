using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;

using ArmyTroops = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the quick info box draws: the <c>HeroBase</c> of the reference, which is either a <see cref="Hero"/> or
/// the <see cref="Captain"/> of a town (<c>dialog_quickinfo.cpp:693-911</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>showQuickInfo( const HeroBase &amp; )</c> 同时吃英雄与船长，靠 <c>dynamic_cast</c> 区分两者：只有英雄
/// 才有等级后缀与「移动力」那一行，船长这两样都没有（<c>isActiveHero</c> 为假）。端口没有 <c>HeroBase</c>，
/// 所以把两边共有的东西取成一份快照，让 <see cref="HeroQuickInfoView"/> 只认快照。
/// </para>
/// <para>
/// 快照里 <see cref="Level"/> 与 <see cref="MovePoints"/> 为 <see langword="null"/> 就表示「船长」：名字后面不加
/// 等级、六行四维只画五行。其余东西两边完全一致——幸运、士气、双旗、四项基本技能、魔法点与一整行军队。
/// </para>
/// </remarks>
public sealed class HeroQuickInfoSubject
{
    private HeroQuickInfoSubject(
        string name,
        PlayerColor color,
        int luck,
        int morale,
        int attack,
        int defense,
        int power,
        int knowledge,
        uint spellPoints,
        uint maxSpellPoints,
        int? level,
        uint? movePoints,
        uint? maxMovePoints,
        ArmyTroops army,
        Sprite? portrait)
    {
        Name = name;
        Color = color;
        Luck = luck;
        Morale = morale;
        Attack = attack;
        Defense = defense;
        Power = power;
        Knowledge = knowledge;
        SpellPoints = spellPoints;
        MaxSpellPoints = maxSpellPoints;
        Level = level;
        MovePoints = movePoints;
        MaxMovePoints = maxMovePoints;
        Army = army;
        Portrait = portrait;
    }

    /// <summary>The name drawn across the top, <c>hero.GetName()</c>.</summary>
    public string Name { get; }

    /// <summary>The color whose flags are drawn, <c>hero.GetColor()</c>.</summary>
    public PlayerColor Color { get; }

    /// <summary>The luck, <c>hero.GetLuck()</c>.</summary>
    public int Luck { get; }

    /// <summary>The morale, <c>hero.GetMorale()</c>.</summary>
    public int Morale { get; }

    /// <summary>The attack, <c>hero.GetAttack()</c>.</summary>
    public int Attack { get; }

    /// <summary>The defence, <c>hero.GetDefense()</c>.</summary>
    public int Defense { get; }

    /// <summary>The spell power, <c>hero.GetPower()</c>.</summary>
    public int Power { get; }

    /// <summary>The knowledge, <c>hero.GetKnowledge()</c>.</summary>
    public int Knowledge { get; }

    /// <summary>The spell points, <c>hero.GetSpellPoints()</c>.</summary>
    public uint SpellPoints { get; }

    /// <summary>The ceiling of the spell points, <c>hero.GetMaxSpellPoints()</c>.</summary>
    public uint MaxSpellPoints { get; }

    /// <summary>The hero's level, or <see langword="null"/> for a captain, which has none.</summary>
    public int? Level { get; }

    /// <summary>The hero's remaining move points, or <see langword="null"/> for a captain, which has none.</summary>
    public uint? MovePoints { get; }

    /// <summary>The hero's daily move points, or <see langword="null"/> for a captain.</summary>
    public uint? MaxMovePoints { get; }

    /// <summary>The army drawn on the last line, <c>hero.GetArmy()</c>.</summary>
    public ArmyTroops Army { get; }

    /// <summary>The small portrait, or <see langword="null"/> when it could not be loaded.</summary>
    public Sprite? Portrait { get; }

    /// <summary>
    /// Takes the snapshot of an adventure hero.
    /// </summary>
    /// <param name="hero">The hero the box describes.</param>
    /// <param name="portrait">The hero's <c>MINIPORT</c> frame, or <see langword="null"/>.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public static HeroQuickInfoSubject FromHero(Hero hero, Sprite? portrait)
    {
        ArgumentNullException.ThrowIfNull(hero);

        return new HeroQuickInfoSubject(
            hero.Name,
            hero.Color,
            hero.GetLuck(),
            hero.GetMorale(),
            hero.GetAttack(),
            hero.GetDefense(),
            hero.GetPower(),
            hero.GetKnowledge(),
            hero.SpellPoints,
            hero.GetMaxSpellPoints(),
            hero.Level,
            hero.MovePoints,
            hero.GetMaxMovePoints(),
            hero.Army,
            portrait);
    }

    /// <summary>
    /// Takes the snapshot of a town's captain.
    /// </summary>
    /// <param name="captain">The captain the box describes.</param>
    /// <param name="portrait">The captain's <c>MINICAPT</c> frame, or <see langword="null"/>.</param>
    /// <returns>The snapshot, with no level and no move points.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="captain"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 船长的名字是所守城镇的名字（<c>Captain::GetName()</c>），军队就是卫戍部队本身，不是副本；颜色旗也照城镇的
    /// 颜色画。它的魔法点上限是知识 ×10，所以魔法点那一行照常显示，只是「移动力」那一行整行缺席。
    /// </remarks>
    public static HeroQuickInfoSubject FromCaptain(Captain captain, Sprite? portrait)
    {
        ArgumentNullException.ThrowIfNull(captain);

        return new HeroQuickInfoSubject(
            captain.Name,
            captain.Color,
            captain.GetLuck(),
            captain.GetMorale(),
            captain.GetAttack(),
            captain.GetDefense(),
            captain.GetPower(),
            captain.GetKnowledge(),
            captain.SpellPoints,
            (uint)captain.GetMaxSpellPoints(),
            level: null,
            movePoints: null,
            maxMovePoints: null,
            captain.Army,
            portrait);
    }
}
