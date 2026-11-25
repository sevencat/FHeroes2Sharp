using FHeroes2.Assets.Icn;

namespace FHeroes2.Game.Battle;

/// <summary>One animation of a battle dialog's backdrop, the pairs pushed into the reference's sequence.</summary>
/// <param name="Icn">The animated ICN.</param>
/// <param name="Loop">Whether it starts over instead of stopping on its last frame.</param>
public readonly record struct BattleSummaryAnimationStep(IcnType Icn, bool Loop);

/// <summary>
/// The strings and the backdrop animations of the battle result window, <c>Battle::GetSummaryParams</c>
/// (<c>battle_dialogs.cpp:416-471</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把结果拆成三块文字：标题（谁赢了/怎么输的）、投降赔款一行（只有「敌人投降」时才有）、战功一行（只有
/// 英雄获胜、且指挥官确实是英雄时才有）。三段动画则按「自己这一侧的结局」排：赢是循环的 <c>WINCMBT</c>，
/// 撤退是 <c>CMBTFLE1-3</c> 各播一遍，投降是循环的 <c>CMBTSURR</c>，全军覆没是 <c>CMBTLOS1-2</c> 播完后
/// <c>CMBTLOS3</c> 循环。
/// </para>
/// <para>
/// 差异：参考对「撤退/投降却没有英雄」<c>assert</c>，端口把名字退化成空串（发布版不断言，且这种组合在
/// 正常流程里到不了）。
/// </para>
/// </remarks>
public sealed class BattleSummaryText
{
    private BattleSummaryText(string title, string surrenderText, string outcomeText, IReadOnlyList<BattleSummaryAnimationStep> animation)
    {
        Title = title;
        SurrenderText = surrenderText;
        OutcomeText = outcomeText;
        Animation = animation;
    }

    /// <summary>The headline, <c>title</c>.</summary>
    public string Title { get; }

    /// <summary>The "their cowardice costs them N gold" line, <c>surrenderText</c>; empty unless the enemy surrendered.</summary>
    public string SurrenderText { get; }

    /// <summary>The experience line, <c>outcomeText</c>; empty unless a hero won.</summary>
    public string OutcomeText { get; }

    /// <summary>The backdrop animations, in the order they play.</summary>
    public IReadOnlyList<BattleSummaryAnimationStep> Animation { get; }

    /// <summary>
    /// Builds the strings and animations for one side's result, <c>GetSummaryParams()</c>.
    /// </summary>
    /// <param name="own">This side's result flags, <c>res1</c>.</param>
    /// <param name="opponent">The other side's result flags, <c>res2</c>.</param>
    /// <param name="heroName">
    /// The commander's name, or <see langword="null"/> when there is no commander or it is a captain — the reference
    /// only prints the name for heroes (<c>hero-&gt;isHeroes()</c>), and the retreat/surrender branches need a hero
    /// by construction.
    /// </param>
    /// <param name="experience">Experience this side's hero earned, <c>exp</c>.</param>
    /// <param name="surrenderCost">What the enemy paid to surrender, <c>surrenderCost</c>.</param>
    /// <returns>The window's texts.</returns>
    public static BattleSummaryText Create(BattleOutcome own, BattleOutcome opponent, string? heroName, uint experience, uint surrenderCost)
    {
        List<BattleSummaryAnimationStep> animation = new(3);

        string title;
        string surrenderText = string.Empty;
        string outcomeText = string.Empty;

        if ((own & BattleOutcome.Wins) != 0)
        {
            animation.Add(new BattleSummaryAnimationStep(IcnType.Wincmbt, true));

            if ((opponent & BattleOutcome.Surrender) != 0)
            {
                title = "The enemy has surrendered!";
                surrenderText = $"Their cowardice costs them {surrenderCost} gold.";
            }
            else if ((opponent & BattleOutcome.Retreat) != 0)
            {
                title = "The enemy has fled!";
            }
            else
            {
                title = "A glorious victory!";
            }

            if (!string.IsNullOrEmpty(heroName))
            {
                outcomeText = $"For valor in combat, {heroName} receives {experience} experience.";
            }
        }
        else if ((own & BattleOutcome.Retreat) != 0)
        {
            animation.Add(new BattleSummaryAnimationStep(IcnType.Cmbtfle1, false));
            animation.Add(new BattleSummaryAnimationStep(IcnType.Cmbtfle2, false));
            animation.Add(new BattleSummaryAnimationStep(IcnType.Cmbtfle3, false));

            title = $"The cowardly {heroName ?? string.Empty} flees from battle.";
        }
        else if ((own & BattleOutcome.Surrender) != 0)
        {
            animation.Add(new BattleSummaryAnimationStep(IcnType.Cmbtsurr, true));

            title = $"{heroName ?? string.Empty} surrenders to the enemy, and departs in shame.";
        }
        else
        {
            animation.Add(new BattleSummaryAnimationStep(IcnType.Cmbtlos1, false));
            animation.Add(new BattleSummaryAnimationStep(IcnType.Cmbtlos2, false));
            animation.Add(new BattleSummaryAnimationStep(IcnType.Cmbtlos3, true));

            title = string.IsNullOrEmpty(heroName)
                ? "Your forces suffer a bitter defeat."
                : $"Your forces suffer a bitter defeat, and {heroName} abandons your cause.";
        }

        return new BattleSummaryText(title, surrenderText, outcomeText, animation);
    }
}
