using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

using CastleType = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Army;

/// <summary>
/// What an army asks its commander: the four calls <c>Army::GetStrength()</c> makes on
/// <c>Army::commander</c> (<c>army.cpp:1291-1333</c>) and the one identity question the transfer methods ask
/// (<c>army.cpp:1428</c>, <c>army.cpp:1440</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference's back-pointer is <c>HeroBase *</c>, a class that both <c>Heroes</c> and the battle-only
/// <c>Captain</c> derive from. The port has no such base class — <see cref="Heroes.Hero"/> is a sealed class of
/// its own — so the five members the army actually calls are named here instead of pulling a base class in for
/// them. A captain, when it lands, implements this interface and needs no change in <see cref="Army"/>.
/// </para>
/// <para>
/// The army only reads; nothing here lets a commander push back on the army. That is deliberate: the reference
/// back-pointer is used in exactly these places — four of them inside the strength calculation, one where an
/// army is about to be emptied and a hero must not be.
/// </para>
/// </remarks>
public interface IArmyCommander
{
    /// <summary>
    /// The hero's attack, <c>commander-&gt;GetAttack()</c> (<c>heroes.cpp:903</c>).
    /// </summary>
    /// <remarks>
    /// This is the effective attack, artifacts included — the same number the hero screen shows.
    /// </remarks>
    /// <returns>The attack skill, at least zero.</returns>
    int GetAttack();

    /// <summary>
    /// The hero's defense, <c>commander-&gt;GetDefense()</c> (<c>heroes.cpp:914</c>).
    /// </summary>
    /// <returns>The defense skill, at least zero.</returns>
    int GetDefense();

    /// <summary>
    /// A secondary skill's value, <c>commander-&gt;GetSecondarySkillValue( skill )</c>
    /// (<c>heroes.h:262</c>).
    /// </summary>
    /// <remarks>
    /// The army asks for one skill only — <see cref="SecondarySkill.Archery"/> — but the method is the
    /// general one so that the next caller does not have to widen the interface.
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <returns>Its value: 10, 25 or 50 per level for archery, zero when the hero does not have it.</returns>
    uint GetSecondarySkillValue(SecondarySkill skill);

    /// <summary>
    /// What the hero's spells add to the army, <c>commander-&gt;GetMagicStrategicValue( strength )</c>
    /// (<c>heroes.cpp:2124</c>).
    /// </summary>
    /// <remarks>
    /// Called once, with the strength of the stacks as the argument. The value grows with the spell book and the
    /// spell points: it is the best <c>Spell::GetStrategicValue</c> over the combat spells the hero knows, and
    /// zero for a hero with no book or no mana.
    /// </remarks>
    /// <param name="armyStrength">The strength of the stacks.</param>
    /// <returns>The extra strength the spells are worth.</returns>
    double GetMagicStrategicValue(double armyStrength);

    /// <summary>
    /// Whether the commander is a hero and not a captain, <c>HeroBase::isHeroes()</c>
    /// (<c>heroes_base.cpp:91</c>).
    /// </summary>
    /// <remarks>
    /// The reference answers <c>GetType() == HEROES</c>: true for a map hero, false for the battle-only
    /// captain. The army asks it to decide whether the army may be emptied — a hero who loses his last stack
    /// has lost the game, a garrison that empties is just an empty garrison — and the reference guards that in
    /// <c>JoinStrongestFromArmy</c> and <c>MoveTroops</c>. <see cref="Heroes.Hero"/> answers true.
    /// </remarks>
    bool IsHeroes { get; }

    /// <summary>
    /// The commander's race, <c>HeroBase::GetRace()</c> (<c>heroes.h:238</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The army asks it in one place outside combat: the editor's army bar picks the level-one creature of the
    /// commander's race as the default for an empty slot (<c>army_bar.cpp:452</c>). <see cref="Heroes.Hero"/>
    /// answers with the race the map gave it; the battle-only captain answers with the race of the town it
    /// defends.
    /// </para>
    /// <para>
    /// The default implementation returns <see cref="MapRace.None"/> so that a commander without a race needs no
    /// code, which is also what the reference's "no commander" branch does.
    /// </para>
    /// </remarks>
    MapRace Race => MapRace.None;

    /// <summary>
    /// The town the commander stands in, <c>HeroBase::inCastle()</c> (<c>heroes_base.h:243</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The army asks it for one thing: the castle's morale and luck modifiers
    /// (<c>army.cpp:1206-1270</c>). A captain answers with the town it defends; a hero answers <see langword="null"/>
    /// for now — the port's <see cref="Heroes.Hero"/> does not track the town it stands in, and the reference's
    /// answer is a map lookup the hero cannot make on its own. The morale of a hero's army therefore still misses
    /// its castle term, exactly as before the interface had the member.
    /// </para>
    /// <para>
    /// The default implementation returns <see langword="null"/> so that a commander without a town needs no code.
    /// </para>
    /// </remarks>
    CastleType? InCastle => null;

    /// <summary>
    /// The commander's artifacts, <c>HeroBase::GetBagArtifacts()</c> (<c>heroes_base.h:250</c>).
    /// </summary>
    /// <remarks>
    /// The army reads it once: the "Arm of the Martyr" curse adds the undead morale penalty to a living army
    /// (<c>army.cpp:1236</c>). A hero carries a real bag; a captain's is permanently empty, which is what the
    /// reference's captain has too.
    /// </remarks>
    ArtifactBag BagArtifacts { get; }

    /// <summary>
    /// The level of a secondary skill, <c>HeroBase::GetLevelSkill( skill )</c> (<c>heroes.cpp:1737</c>).
    /// </summary>
    /// <remarks>
    /// 战斗要的是**等级**而不是 <see cref="GetSecondarySkillValue"/> 的数值：投降折扣按等级查表
    /// （<c>Skill::GetDiplomacySurrenderCostDiscount</c>，<c>battle_army.cpp:195-201</c>）。队长没有技能，
    /// 默认回答 <see cref="SkillLevel.None"/>——与参考 <c>Captain::GetLevelSkill()</c> 一致。
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <returns>The level, or <see cref="SkillLevel.None"/> when the skill is not known.</returns>
    SkillLevel GetLevelSkill(SecondarySkill skill) => SkillLevel.None;

    /// <summary>
    /// Clears mode bits, <c>HeroBase::ResetModes()</c>.
    /// </summary>
    /// <remarks>
    /// 参考在每回合开头清掉英雄的 <c>SPELLCASTED</c>，让他重新能施法（<c>battle_army.cpp:212-214</c>）。
    /// 这是这条本来只读的接口上唯一的写操作，收窄成「清掉某些标记」；没有模式字的队长默认什么都不做。
    /// </remarks>
    /// <param name="modes">The bits to clear.</param>
    void ResetModes(HeroModes modes)
    {
    }

    /// <summary>
    /// Sets mode bits, <c>HeroBase::SetModes()</c>.
    /// </summary>
    /// <remarks>
    /// 战斗用到的只有一位：<see cref="HeroModes.Spellcasted"/>——施法之后由竞技场打上，本回合不能再施
    /// （<c>battle_action.cpp:474</c>），每回合开头由 <see cref="BattleForce"/> 清掉。没有模式字的队长默认什么都不做。
    /// </remarks>
    /// <param name="modes">The bits to set.</param>
    void SetModes(HeroModes modes)
    {
    }

    /// <summary>
    /// Called just before a battle starts, <c>HeroBase::ActionPreBattle()</c>.
    /// </summary>
    /// <remarks>
    /// 参考的英雄实现只重置法术书翻到第几页、选中了哪条法术（<c>heroes.cpp</c> 的
    /// <c>Heroes::ActionPreBattle()</c> → <c>SpellBook::resetState()</c>），纯粹是界面状态；端口没有法术书界面，
    /// 于是这里是空实现，接口留着一个位置是为了让 <c>BattleLoader.Load</c> 与参考的调用顺序一一对应。
    /// </remarks>
    void ActionPreBattle()
    {
    }

    /// <summary>
    /// Called once the battle is over, <c>HeroBase::ActionAfterBattle()</c>.
    /// </summary>
    /// <remarks>英雄的实现见 <c>Hero.ActionAfterBattle</c>（恢复移动力、清理残余状态）；队长没有额外动作。</remarks>
    void ActionAfterBattle()
    {
    }

    /// <summary>
    /// Writes back the spell points the battle started with, the rollback of <c>ActionPreBattle()</c>'s snapshot.
    /// </summary>
    /// <remarks>
    /// 参考的「重打成手动」是把手上的 <c>spell_points</c> 抄回去再重开一个竞技场（<c>battle_main.cpp:420-430</c>），
    /// 端口因此需要一个写入口；队长没有法力池，默认什么都不做。
    /// </remarks>
    /// <param name="spellPoints">The spell points to restore.</param>
    void SetSpellPoints(uint spellPoints)
    {
    }

    /// <summary>
    /// Pays for a spell, <c>HeroBase::SpellCasted()</c>.
    /// </summary>
    /// <remarks>
    /// 参考在战斗施法之后调它扣法力与移动力（<c>battle_action.cpp:475</c>）。英雄的实现见
    /// <c>Hero.SpellCasted</c>；队长没有法力池，默认什么都不做。
    /// </remarks>
    /// <param name="spell">The spell that was cast.</param>
    void SpellCasted(Spell spell)
    {
    }

    /// <summary>
    /// The commander's mode bits, <c>HeroBase::Modes()</c> (<c>heroes_base.h:190</c>).
    /// </summary>
    /// <remarks>
    /// 战斗只读一位：<see cref="HeroModes.Spellcasted"/>。竞技场用它回答「这个英雄本回合还能不能施法」
    /// （<c>battle_arena.cpp</c> 的 <c>isDisableCastSpell</c>）与「敌方英雄能不能投降」。没有模式字的队长默认
    /// 回答 <see cref="HeroModes.None"/>。
    /// </remarks>
    /// <returns>The mode bits that are set.</returns>
    HeroModes Modes => HeroModes.None;

    /// <summary>
    /// Who gives this commander orders, <c>HeroBase::GetControl()</c>.
    /// </summary>
    /// <remarks>
    /// 英雄答王国、队长答城堡，两者最终都落到 <c>Players::GetPlayerControl( color )</c>——所以战斗只问这个数，
    /// 不关心是谁在指挥。
    /// </remarks>
    /// <returns>The control of the commander's side.</returns>
    PlayerControl GetControl();

    /// <summary>
    /// The commander's side, <c>HeroBase::GetColor()</c> (<c>army.cpp:1194-1198</c> asks it first).
    /// </summary>
    /// <remarks>
    /// 参考里 <c>Army::GetColor()</c> 先问指挥官，问不到才用军队自己的颜色。战斗算法术伤害时要看施法者的颜色
    /// （<c>CalculateSpellDamage</c> 用它挑「敌方是否站在自己人旁边」这类判断），所以这里也得能问出来。
    /// </remarks>
    /// <returns>The colour, <see cref="PlayerColor.None"/> when the commander belongs to nobody yet.</returns>
    PlayerColor GetColor();

    /// <summary>
    /// The commander's spell power, <c>HeroBase::GetPower()</c> (<c>heroes.cpp:944</c>).
    /// </summary>
    /// <remarks>
    /// 战斗里只有一处用到它：把法术强度换成治疗量/复活量/伤害值（<c>SpellInfo</c> 的四个公式）。
    /// 队长的强度由宝物给（<c>captain.cpp:GetPower</c>），所以不能写死成 0。
    /// </remarks>
    /// <returns>The spell power, at least one.</returns>
    int GetPower();

    /// <summary>
    /// The mana left to the commander, <c>HeroBase::GetSpellPoints()</c>.
    /// </summary>
    /// <remarks>
    /// 战斗 AI 用它判断「这个法术还付得起吗」（<c>ai_battle_spell.cpp</c>），施法完成后由竞技场扣。
    /// 队长没有法力池，默认回答零。
    /// </remarks>
    uint SpellPoints => 0;

    /// <summary>
    /// The size of the mana pool, <c>HeroBase::GetMaxSpellPoints()</c> (<c>heroes.cpp:967-970</c>,
    /// <c>captain.cpp:GetMaxSpellPoints</c>).
    /// </summary>
    /// <remarks>Ten per point of knowledge. 战斗 AI 用它判断「这是不是最后一个能施法的回合」。</remarks>
    /// <returns>The maximum spell points; zero for a commander with no mana pool at all.</returns>
    uint GetMaxSpellPoints() => 0;

    /// <summary>
    /// Whether the commander could cast this spell now, <c>HeroBase::CanCastSpell()</c> (<c>heroes_base.cpp:CanCastSpell</c>).
    /// </summary>
    /// <remarks>书、法力两关由指挥官自己回答；「这个格子能不能吃这个法术」由竞技场回答。</remarks>
    /// <param name="spell">The spell about to be cast.</param>
    /// <returns>Whether the commander knows and can afford it.</returns>
    bool CanCastSpell(Spell spell) => false;

    /// <summary>
    /// Whether the commander carries a spell book, <c>HeroBase::HaveSpellBook()</c> (<c>heroes_base.h:145-148</c>).
    /// </summary>
    /// <remarks>没有书就没有 <see cref="GetAllSpells"/> 里的东西，也就不能施法；队长默认回答否。</remarks>
    bool HasSpellBook => false;

    /// <summary>
    /// Every combat spell the commander can cast, <c>HeroBase::getAllSpells()</c>.
    /// </summary>
    /// <remarks>
    /// 参考返回「魔法书里的法术」；端口把它抽成接口成员，好让战斗 AI 不必知道指挥者是英雄还是队长。
    /// 没有书的指挥官默认给一份空表。
    /// </remarks>
    /// <returns>The spells, empty for a commander without a book.</returns>
    SpellStorage GetAllSpells() => new();

    /// <summary>
    /// The commander's morale, <c>HeroBase::GetMorale()</c> (<c>heroes.cpp:1033</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Army::GetMorale()</c> 有指挥官时直接回答指挥官的士气，没有时才用军队自己的构成修正
    /// （<c>army.cpp:1220-1224</c>）。也就是说英雄的军队在战斗里**不**吃多种族惩罚——这是参考的行为，
    /// 不是端口漏了。
    /// </para>
    /// <para>
    /// 队长的士气只由宝物给（<c>captain.cpp:GetMorale</c>）。
    /// </para>
    /// </remarks>
    /// <returns>The morale, already clamped into its scale.</returns>
    int GetMorale();

    /// <summary>
    /// The commander's luck, <c>HeroBase::GetLuck()</c> (<c>heroes.cpp:1069</c>).
    /// </summary>
    /// <returns>The luck, already clamped into its scale; zero for a captain with no luck artifact.</returns>
    int GetLuck();
}
