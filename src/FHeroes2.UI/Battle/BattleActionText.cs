using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The sentences the battle interface writes to the upper status line when a unit acts, the
/// <c>RedrawAction*Status()</c> family of <c>battle_interface.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 只搬了「跳过」这一句：参考的其余动作反馈是动画加状态行，端口还没有动作动画（见 <c>bd list</c> 的
/// <c>ukw.19</c>），这时先补这一句是唯一不依赖动画的。
/// </para>
/// <para>
/// <b>为什么在 UI 层而不是场景里</b>：句子要用生物的复数名与单复数动词，属于界面的措辞而不是战斗规则，
/// 和 <see cref="BattleSpellCursor"/> 一样做成纯函数，测试不必起窗口。
/// </para>
/// </remarks>
public static class BattleActionText
{
    /// <summary>
    /// The message of a skipped turn, <c>Interface::RedrawActionSkipStatus()</c>
    /// (<c>battle_interface.cpp:4005-4011</c>).
    /// </summary>
    /// <param name="unit">The unit that skipped its turn.</param>
    /// <returns>For example <c>"The peasants skip their turn."</c>.</returns>
    /// <remarks>
    /// 参考用 <c>_n()</c> 给两句模板、按数量挑一句，再把生物的复数名（数量为 1 时是单数名）填进
    /// <c>%{name}</c>；名字在句中被
    /// <c>StringReplaceWithLowercase()</c> 改成小写，所以句首是「The」、生物名不大写。
    /// </remarks>
    public static string Skip(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        // _n( singular, plural, count ): the plural form is the one for everything but one creature.
        string template = unit.Count == 1
            ? "The %{name} skips their turn."
            : "The %{name} skip their turn.";

        return Army.StringReplaceWithLowercase(template, "%{name}", MonsterNames.GetPluralName(unit.Monster, unit.Count));
    }
}
