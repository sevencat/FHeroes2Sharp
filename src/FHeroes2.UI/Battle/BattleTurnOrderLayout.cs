using FHeroes2.Core.Geometry;
using FHeroes2.Game.Battle;

namespace FHeroes2.UI.Battle;

/// <summary>
/// Which stacks the turn order bar shows and where their icons go, <c>Battle::TurnOrder::redraw()</c>
/// (<c>battle_interface.cpp:1268-1340</c>).
/// </summary>
/// <remarks>
/// <para>
/// 图标是 43 像素见方（<c>turnOrderMonsterIconSize</c>），一条最多放「战场宽度 / 43」个，整条居中。条子放不下
/// 全队时，已经行动过的单位先让位：只有「剩下的位置比还没画的单位少」时才丢（<c>:1315-1320</c>），所以回合初
/// 一条能排下所有人，到了轮末只剩下一两个还没行动的。
/// </para>
/// <para>
/// 这段只算索引与矩形，不碰精灵，因此顺序条能脱离资产单测。
/// </para>
/// </remarks>
public static class BattleTurnOrderLayout
{
    /// <summary>Edge length of one icon, <c>turnOrderMonsterIconSize</c>.</summary>
    public const int IconSize = 43;

    /// <summary>
    /// The bar rectangle, centered over the battlefield.
    /// </summary>
    /// <param name="battlefield">The battlefield surface, <c>battleRoi</c>.</param>
    /// <param name="validUnitCount">How many stacks in the order can still act.</param>
    /// <returns>
    /// An empty rectangle when there is nothing to draw, otherwise <see cref="IconSize"/> tall and
    /// <see cref="IconSize"/> wide per slot.
    /// </returns>
    public static Rect GetArea(Rect battlefield, int validUnitCount)
    {
        int slots = GetSlotCount(battlefield.Width, validUnitCount);
        int width = IconSize * slots;

        return new Rect(battlefield.X + ((battlefield.Width - width) / 2), battlefield.Y, width, IconSize);
    }

    /// <summary>The number of icons that fit, <c>unitsToDraw</c>.</summary>
    /// <param name="battlefieldWidth">Width of the battlefield surface, <c>battleRoi.width</c>.</param>
    /// <param name="validUnitCount">How many stacks in the order can still act.</param>
    /// <returns><c>min( battlefieldWidth / IconSize, validUnitCount )</c>, never negative.</returns>
    public static int GetSlotCount(int battlefieldWidth, int validUnitCount) =>
        Math.Max(0, Math.Min(battlefieldWidth / IconSize, validUnitCount));

    /// <summary>
    /// The stacks that get an icon, in drawing order, as indices into the order list.
    /// </summary>
    /// <param name="order">The predicted order, <c>_orderOfUnits</c>.</param>
    /// <param name="slots">How many icons fit, <see cref="GetSlotCount"/>.</param>
    /// <returns>At most <paramref name="slots"/> indices.</returns>
    /// <remarks>
    /// 参考的循环（<c>:1305-1330</c>）：跳过不能行动的单位；已经行动过的（<c>TR_MOVED</c>）只在「还有富余位置」时
    /// 保留，一旦位置不够就先牺牲它们，好让玩家看到接下来是谁动。
    /// </remarks>
    public static IReadOnlyList<int> SelectSlots(IReadOnlyList<BattleUnit> order, int slots)
    {
        ArgumentNullException.ThrowIfNull(order);

        List<int> indices = new();

        if (slots <= 0)
        {
            return indices;
        }

        int validUnitCount = 0;

        foreach (BattleUnit unit in order)
        {
            if (unit.IsValid)
            {
                validUnitCount++;
            }
        }

        // 参考的 `unitsProcessed`：跳过已行动单位时也会加一，所以「还剩多少位置」随遍历一起收紧。
        int processed = 0;

        for (int i = 0; i < order.Count && indices.Count < slots; i++)
        {
            BattleUnit unit = order[i];

            if (!unit.IsValid)
            {
                continue;
            }

            if (unit.HasModes(BattleMonsterState.Moved) && (validUnitCount - processed) > slots)
            {
                processed++;
                continue;
            }

            indices.Add(i);
            processed++;
        }

        return indices;
    }
}
