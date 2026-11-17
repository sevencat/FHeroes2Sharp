using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The combat half of <see cref="BattleArena"/>: the facts a <see cref="BattleUnit"/> asks the arena for while it
/// calculates damage, <see cref="IBattleField"/>.
/// </summary>
public sealed partial class BattleArena
{
    /// <summary>
    /// Whether a shot at the defender goes through a standing castle wall,
    /// <c>Arena::IsShootingPenalty()</c> (<c>battle_arena.cpp:1392-1441</c>).
    /// </summary>
    /// <param name="attacker">The unit that shoots.</param>
    /// <param name="defender">The unit that is shot at.</param>
    /// <returns>True when the shot loses <see cref="GameStatic.CastleWallRangedPenalty"/> percent of its damage.</returns>
    /// <remarks>
    /// <para>参考逐条排除，端口逐条照抄，顺序也不能变——先判「有没有城堡」，再判「是不是塔」，最后才判技能与几何：</para>
    /// <list type="number">
    /// <item>没有城堡（野战）没有墙，不罚。</item>
    /// <item>塔的射击不受墙影响。</item>
    /// <item>射手英雄有黄金弓（<c>NO_SHOOTING_PENALTY</c>）或有箭术技能，不罚。</item>
    /// <item>射手自己在墙内，不罚（墙只挡外来的箭）。</item>
    /// <item>射手与目标在同一侧，不罚。</item>
    /// <item>最后才看几何：沿两支单位的「背点」连线以 14 像素采样，若连线上有**已打穿**的墙格，说明目标从缺口
    /// 暴露出来，不罚。打穿的墙格 <c>Object == 0</c> 且画面矩形包含采样点。</item>
    /// </list>
    /// </remarks>
    public bool IsShootingPenalty(BattleUnit attacker, BattleUnit defender)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);

        // No castle - no castle walls, the penalty does not apply.
        if (_castle == null)
        {
            return false;
        }

        // The penalty does not apply to towers.
        if (attacker.HasModes(BattleMonsterState.CapTower) || defender.HasModes(BattleMonsterState.CapTower))
        {
            return false;
        }

        // The penalty does not apply if the attacker's hero has certain artifacts or skills.
        IArmyCommander? hero = attacker.GetCommander();

        if (hero != null)
        {
            if (hero.BagArtifacts.IsBonusPresent(FHeroes2.Game.Artifacts.ArtifactBonusType.NoShootingPenalty))
            {
                return false;
            }

            if (hero.GetLevelSkill(SecondarySkill.Archery) != SkillLevel.None)
            {
                return false;
            }
        }

        // The penalty does not apply if the shooter is inside the castle walls.
        if (!attacker.IsOutOfCastleWalls())
        {
            return false;
        }

        // The penalty does not apply if both units are on the same side of the walls.
        if (attacker.IsOutOfCastleWalls() == defender.IsOutOfCastleWalls())
        {
            return false;
        }

        // The penalty does not apply if the target is exposed through a broken wall section.
        Point[] points = LinePoints.GetLinePoints(attacker.GetBackPoint(), defender.GetBackPoint(), BattleCell.WidthPx / 3);

        foreach (Point point in points)
        {
            if ((Cell(CastleFirstTopWallPos).Object == 0 && Cell(CastleFirstTopWallPos).Position.Contains(point))
                || (Cell(CastleSecondTopWallPos).Object == 0 && Cell(CastleSecondTopWallPos).Position.Contains(point))
                || (Cell(CastleThirdTopWallPos).Object == 0 && Cell(CastleThirdTopWallPos).Position.Contains(point))
                || (Cell(CastleFourthTopWallPos).Object == 0 && Cell(CastleFourthTopWallPos).Position.Contains(point)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the unit stands in a built moat, <see cref="IBattleField.IsOnMoat"/>.
    /// </summary>
    /// <param name="unit">The unit to test.</param>
    /// <returns>True when its head or tail cell is a moat cell of a castle that actually built one.</returns>
    /// <remarks>
    /// 飞行单位与桥面可通行时不落在护城河里：这两种情况由 <see cref="BattleBoard.IsMoatIndex"/> 回答，护城河是否
    /// 真的建了由城堡回答。两个条件缺一不可，所以不能只问棋盘。
    /// </remarks>
    public bool IsOnMoat(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (_castle == null || !_castle.IsBuild(FHeroes2.Game.Castle.BuildingType.Moat))
        {
            return false;
        }

        bool bridgeIsPassable = _bridge != null && _bridge.IsPassable(unit);

        return BattleBoard.IsMoatIndex(unit.HeadIndex, unit.IsFlying, bridgeIsPassable)
            || BattleBoard.IsMoatIndex(unit.TailIndex, unit.IsFlying, bridgeIsPassable);
    }

    /// <summary>
    /// How much damage a shot loses on its way to the defender,
    /// <c>battle_troop.cpp:1170-1175</c>: <c>Arena::IsShootingPenalty() ? getCastleWallRangedPenalty() : 0</c>.
    /// </summary>
    /// <param name="attacker">The unit that shoots.</param>
    /// <param name="defender">The unit behind the walls.</param>
    /// <returns>The penalty in percent, zero when the shot is not reduced.</returns>
    public int GetShootingPenaltyPercent(BattleUnit attacker, BattleUnit defender) =>
        IsShootingPenalty(attacker, defender) ? GameStatic.CastleWallRangedPenalty : 0;

    /// <summary>
    /// The archery skill of the shooter's hero in percent, <see cref="IBattleField.GetArcheryBonusPercent"/>.
    /// </summary>
    /// <param name="unit">The unit that shoots.</param>
    /// <returns>The bonus in percent; zero when the hero has no archery skill.</returns>
    public int GetArcheryBonusPercent(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return (int)(unit.GetCommander()?.GetSecondarySkillValue(SecondarySkill.Archery) ?? 0);
    }

    /// <summary>
    /// Whether the army on the other side still has a stack of a creature,
    /// <c>Arena::getEnemyForce( color ).HasMonster( monster )</c>.
    /// </summary>
    /// <param name="color">The colour whose enemy is asked about.</param>
    /// <param name="monster">The creature to look for.</param>
    /// <returns>True when the opposing force still has such a stack.</returns>
    public bool EnemyHasMonster(PlayerColor color, MonsterType monster) => GetEnemyForce(color).HasMonster(monster);
}
