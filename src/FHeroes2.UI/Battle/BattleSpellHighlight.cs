using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The cells the mouse shadow covers while a spell is aimed, the highlighted set of
/// <c>Interface::RedrawCover()</c> (<c>battle_interface.cpp:2288-2358</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么单独一层</b>：参考在一个 <c>std::set&lt;const Cell*&gt;</c> 里攒格子，攒完再按「格子能过、格子上的兵
/// 吃这个法术」过滤后画阴影。攒格子这一步只看法术、鼠标格和道具状态，不需要任何图片，于是搬成纯函数；
/// 过滤和贴图留在 <see cref="BattleFieldPainter"/>。
/// </para>
/// <para>
/// <b>与参考的差异</b>：只做施法分支。攻击方向、宽单位两格、范围射击（<c>AREA_SHOT</c>）这些分支要行动交互
/// 的整套光标，随那一步补。
/// </para>
/// </remarks>
public static class BattleSpellHighlight
{
    /// <summary>The value meaning "no cell", the reference's <c>-1</c>.</summary>
    public const int NoCell = -1;

    /// <summary>
    /// The cells of one aim, the <c>humanturn_spell</c> branch of <c>Interface::RedrawCover()</c>
    /// (<c>battle_interface.cpp:2291-2358</c>).
    /// </summary>
    /// <param name="spell">The spell being aimed.</param>
    /// <param name="hoveredCell">The cell under the pointer.</param>
    /// <param name="cursor">The cursor shown right now, which tells the teleport branch where it is.</param>
    /// <param name="teleportingUnit">
    /// The unit a teleport would move, <see langword="null"/> when no source cell has been picked or the cell is empty.
    /// </param>
    /// <returns>
    /// The cells to consider for the shadow; empty only for a teleport whose cursor state the reference asserts against.
    /// </returns>
    /// <remarks>
    /// 冷环只圈四周、不含中心，火球与流星雨是中心加一圈，火焰爆裂是中心加两圈——都是参考的原始范围，
    /// 不是法术的真实伤害范围（那在施法时才按 <c>TargetsForSpell</c> 算）。
    /// </remarks>
    public static IReadOnlyList<int> GetCells(
        SpellType spell,
        int hoveredCell,
        CursorTheme cursor,
        BattleUnit? teleportingUnit)
    {
        List<int> cells = [];

        switch (spell)
        {
            case SpellType.ColdRing:
                AddAround(cells, hoveredCell);
                break;

            case SpellType.Fireball:
            case SpellType.MeteorShower:
                AddHovered(cells, hoveredCell);
                AddAround(cells, hoveredCell);
                break;

            case SpellType.FireBlast:
                AddHovered(cells, hoveredCell);
                foreach (int index in BattleBoard.GetDistanceIndexes(hoveredCell, 2))
                {
                    AddDistinct(cells, index);
                }

                break;

            case SpellType.Teleport:
                if (cursor == CursorTheme.CombatNone)
                {
                    AddHovered(cells, hoveredCell);
                }
                else if (cursor == CursorTheme.SpellTeleport && teleportingUnit is not null && BattleBoard.IsValidIndex(hoveredCell))
                {
                    BattlePosition position = BattlePosition.GetPosition(teleportingUnit, hoveredCell);

                    AddDistinct(cells, position.HeadIndex);

                    if (teleportingUnit.IsWide)
                    {
                        AddDistinct(cells, position.TailIndex);
                    }
                }
                else
                {
                    // 参考在「还没选起点」时也只圈鼠标格；这里连同光标异常的兜底一起落到同一分支。
                    AddHovered(cells, hoveredCell);
                }

                break;

            default:
                AddHovered(cells, hoveredCell);
                break;
        }

        return cells;
    }

    /// <summary>Adds the hovered cell itself, when it is on the board.</summary>
    private static void AddHovered(List<int> cells, int hoveredCell)
    {
        if (BattleBoard.IsValidIndex(hoveredCell))
        {
            AddDistinct(cells, hoveredCell);
        }
    }

    /// <summary>Adds the six neighbours of a cell, <c>Board::GetAroundIndexes()</c>.</summary>
    private static void AddAround(List<int> cells, int center)
    {
        if (!BattleBoard.IsValidIndex(center))
        {
            return;
        }

        foreach (int index in BattleBoard.GetAroundIndexes(center))
        {
            AddDistinct(cells, index);
        }
    }

    /// <summary>Adds a cell once; the reference's <c>std::set</c> does the same.</summary>
    private static void AddDistinct(List<int> cells, int index)
    {
        if (BattleBoard.IsValidIndex(index) && !cells.Contains(index))
        {
            cells.Add(index);
        }
    }
}
