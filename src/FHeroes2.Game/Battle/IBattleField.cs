using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The six facts about the battlefield that a unit cannot know by itself, <c>Battle::Arena</c>'s side of
/// <c>battle_troop.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考里 <c>Battle::Unit</c> 随时能问 <c>Arena::GetCastle()</c>、<c>Arena::GetOppositeColor()</c>、
/// <c>Arena::IsShootingPenalty()</c> 与 <c>Arena::getAttackingArmyColor()</c>，因为竞技场是单例。端口不做单例：
/// 射击惩罚要城堡与城墙的判据、护城河要城堡是否建了护城河、箭术加成要英雄、反向颜色与攻方颜色要两支军队、
/// 敌方兵种要两支军队的部队表，这些只有竞技场（<c>BattleArena</c>）知道，于是收进这一个接口，单位持有一个可空的引用。
/// </para>
/// <para>
/// 引用为空时一律按「没有任何加成、也不在任何城堡地形上、朝哪边都不动」处理——单测里造一个单位不必先造一座城堡。
/// </para>
/// </remarks>
public interface IBattleField
{
    /// <summary>
    /// Whether the unit stands in a built moat, <c>Arena::GetCastle()-&gt;isBuild( BUILD_MOAT ) &amp;&amp;
    /// Board::isMoatIndex()</c>.
    /// </summary>
    /// <param name="unit">The unit to test.</param>
    /// <returns>True when its head or tail cell is a moat cell of a castle that actually built one.</returns>
    bool IsOnMoat(BattleUnit unit);

    /// <summary>
    /// How much damage a shot loses on its way to the defender, <c>Arena::IsShootingPenalty()</c> plus
    /// <c>GameStatic::getCastleWallRangedPenalty()</c>.
    /// </summary>
    /// <param name="attacker">The unit that shoots.</param>
    /// <param name="defender">The unit behind the walls.</param>
    /// <returns>The penalty in percent, zero when the shot is not reduced.</returns>
    int GetShootingPenaltyPercent(BattleUnit attacker, BattleUnit defender);

    /// <summary>
    /// The archery skill of the shooter's hero in percent,
    /// <c>GetCommander()-&gt;GetSecondarySkillValue( Skill::Secondary::ARCHERY )</c>.
    /// </summary>
    /// <param name="unit">The unit that shoots.</param>
    /// <returns>The bonus in percent; zero when the hero has no archery skill.</returns>
    int GetArcheryBonusPercent(BattleUnit unit);

    /// <summary>The colour of the other army, <c>Arena::GetOppositeColor()</c>.</summary>
    /// <param name="color">One army's colour.</param>
    /// <returns>The colour of the other army, or <see cref="PlayerColor.None"/> when it is unknown.</returns>
    PlayerColor GetOppositeColor(PlayerColor color);

    /// <summary>
    /// Whether the army on the other side still has a stack of a creature,
    /// <c>Arena::getEnemyForce( color ).HasMonster( monster )</c> (<c>battle_troop.cpp:284</c>).
    /// </summary>
    /// <remarks>
    /// 士气要减一格时问的是「对面还有骨龙吗」——只有竞技场知道两边各有哪些兵。接口不给默认实现：回答不了这个
    /// 问题的实现（比如单测里的假体）应该显式回答 <see langword="false"/>，而不是让一个静默的默认值把惩罚藏起来。
    /// </remarks>
    /// <param name="color">The colour whose enemy is asked about.</param>
    /// <param name="monster">The creature to look for.</param>
    /// <returns>True when the opposing force still has such a stack.</returns>
    bool EnemyHasMonster(PlayerColor color, MonsterType monster);

    /// <summary>
    /// The colour of the side that walked into the fight, <c>Arena::getAttackingArmyColor()</c>.
    /// </summary>
    /// <remarks>
    /// 参考只有一个调用者：<c>Unit::UpdateDirection()</c> 用它决定「没被打过、也没走过路的单位该朝哪边」——
    /// 攻方朝右、守方朝左（<c>battle_troop.cpp:196-202</c>）。这是竞技场被单位反查的第六个事实。
    /// </remarks>
    PlayerColor AttackingArmyColor { get; }

    /// <summary>
    /// The commander of one side, <c>Arena::getCommander( color )</c>.
    /// </summary>
    /// <remarks>
    /// <c>Unit::GetCurrentOrArmyCommander()</c> 靠它回答「这个单位此刻替谁打」：颜色已经算上催眠，所以不能直接问
    /// 自己所属的军队。副作用类法术（吸血、鹰眼之外的加成）与战后的宝物转移都要按当前这一方去问。
    /// 默认实现回答 <see langword="null"/>，让只关心其它几件事的假体不必多写一个方法。
    /// </remarks>
    /// <param name="color">The side to ask about.</param>
    /// <returns>The hero or captain of that side, or <see langword="null"/> when it has none.</returns>
    IArmyCommander? GetCommander(PlayerColor color) => null;
}
