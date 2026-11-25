using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Battle;

/// <summary>
/// One thing an attack draws: a unit switching to a sequence, in the order the reference plays them,
/// <c>RedrawActionAttackPart1()</c>/<c>RedrawActionAttackPart2()</c> and
/// <c>_redrawActionWincesKills()</c> (<c>battle_interface.cpp:4088-4256</c>).
/// </summary>
public sealed class BattleAttackStep
{
    /// <summary>Names one step.</summary>
    /// <param name="unit">The unit that plays the sequence.</param>
    /// <param name="animation">The sequence it switches to.</param>
    /// <param name="isShooting">Whether this is the shooting half of a blow, which runs on its own delay.</param>
    public BattleAttackStep(BattleUnit unit, MonsterAnimationType animation, bool isShooting = false)
    {
        ArgumentNullException.ThrowIfNull(unit);

        Unit = unit;
        Animation = animation;
        IsShooting = isShooting;
    }

    /// <summary>The unit that plays the sequence.</summary>
    public BattleUnit Unit { get; }

    /// <summary>The sequence it switches to.</summary>
    public MonsterAnimationType Animation { get; }

    /// <summary>Whether this step is a shot, <c>RedrawActionAttackPart1()</c>'s <c>archer</c> branch.</summary>
    public bool IsShooting { get; }
}

/// <summary>
/// Which sequences an attack plays, the choices of <c>RedrawActionAttackPart1()</c> (<c>:4158-4174</c>),
/// the post-attack half of <c>RedrawActionAttackPart2()</c> (<c>:4185-4195</c>) and the reactions of
/// <c>_redrawActionWincesKills()</c> (<c>:4288-4329</c>).
/// </summary>
/// <remarks>
/// <para>
/// 纯数据：只回答「播哪个序列」，不碰画布也不碰单位状态，因此可以拿真竞技场的结果直接断言。
/// </para>
/// <para>
/// 参考的序列枚举是 <c>MELEE_TOP..RANG_BOT</c> 一顺排（每支攻击占两个号：前一半、后一半），所以
/// 「上面来的减二、下面来的加二」与 <see cref="MonsterAnimationType"/> 的排布一一对应，这里照抄算术。
/// </para>
/// </remarks>
public static class BattleAttackAnimation
{
    /// <summary>
    /// The first half of a blow's sequence, <c>RedrawActionAttackPart1()</c> (<c>:4158-4174</c>).
    /// </summary>
    /// <param name="attacker">The unit that strikes.</param>
    /// <param name="defender">The unit it strikes.</param>
    /// <param name="isShooting">Whether the blow is a shot rather than a melee swing.</param>
    /// <param name="isDoubleCell">Whether the blow sweeps two of the defender's cells.</param>
    /// <returns>The sequence to switch to.</returns>
    /// <remarks>
    /// 参考的近战先按「双击打两格」挑 <c>RANG_FRONT</c>、否则 <c>MELEE_FRONT</c>，再按目标的上下位置 ±2；
    /// 射击按发射角分上/中/下三支，端口用同样的行比较代替那道角度门槛（宽目标时略粗，见 ADR-226）。
    /// </remarks>
    public static MonsterAnimationType SelectSwing(
        BattleUnit attacker, BattleUnit defender, bool isShooting, bool isDoubleCell)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);

        MonsterAnimationType baseSequence = isShooting
            ? MonsterAnimationType.RangFront
            : isDoubleCell ? MonsterAnimationType.RangFront : MonsterAnimationType.MeleeFront;

        int attackerY = attacker.Position.GetRect().Y;
        int defenderY = defender.Position.GetRect().Y;

        if (defenderY < attackerY)
        {
            return (MonsterAnimationType)((int)baseSequence - 2);
        }

        return defenderY > attackerY ? (MonsterAnimationType)((int)baseSequence + 2) : baseSequence;
    }

    /// <summary>
    /// The second half of a swing, <c>++attackStart</c> of <c>RedrawActionAttackPart2()</c> (<c>:4185-4189</c>).
    /// </summary>
    /// <param name="swing">The first half that was just played.</param>
    /// <returns>The sequence to switch to; <paramref name="swing"/> itself when it was not an attack sequence.</returns>
    public static MonsterAnimationType SelectSwingEnd(MonsterAnimationType swing) =>
        swing is >= MonsterAnimationType.MeleeTop and <= MonsterAnimationType.RangBot
            ? (MonsterAnimationType)((int)swing + 1)
            : swing;

    /// <summary>
    /// What one hit unit does, <c>_redrawActionWincesKills()</c>'s kill/wince branch (<c>:4298-4323</c>).
    /// </summary>
    /// <param name="target">The unit the blow hit, with its damage and losses.</param>
    /// <returns>
    /// <see cref="MonsterAnimationType.Kill"/> when the unit fell, <see cref="MonsterAnimationType.Wnce"/> when it
    /// took damage but survived, and <see langword="null"/> when it resisted the blow.
    /// </returns>
    /// <remarks>
    /// 参考先看 <c>!isValid()</c>（整支部队都没了才播死亡），再看 <c>damage</c>（掉血了才算挨打）；
    /// 抗魔/免疫的那一支只响一声、不播动画，端口照此返回空。掉几个兵但部队还在，是抖一下而不是倒下。
    /// </remarks>
    public static MonsterAnimationType? SelectReaction(BattleDamageTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.Defender.IsValid)
        {
            return MonsterAnimationType.Kill;
        }

        return target.Damage > 0 ? MonsterAnimationType.Wnce : null;
    }

    /// <summary>
    /// Everything an already-applied attack still has to draw. The first blow's swing was played before the
    /// command was handed to the arena, so only its reactions are left; later blows (the retaliation and the
    /// second blow of a double attack) play their own swing first.
    /// </summary>
    /// <param name="blows">The blows of the attack, in the order they landed.</param>
    /// <returns>The steps to play, in order.</returns>
    public static IReadOnlyList<BattleAttackStep> CreateReplay(IReadOnlyList<BattleAttackBlow> blows)
    {
        ArgumentNullException.ThrowIfNull(blows);

        List<BattleAttackStep> steps = [];

        for (int index = 0; index < blows.Count; ++index)
        {
            BattleAttackBlow blow = blows[index];

            if (index > 0)
            {
                MonsterAnimationType swing = SelectSwing(
                    blow.Attacker,
                    blow.Defender,
                    blow.IsShooting,
                    blow.Attacker.IsDoubleAttack && blow.Targets.Count == 2);

                steps.Add(new BattleAttackStep(blow.Attacker, swing, blow.IsShooting));
                steps.Add(new BattleAttackStep(blow.Attacker, SelectSwingEnd(swing), blow.IsShooting));
            }

            foreach (BattleDamageTarget target in blow.Targets)
            {
                if (SelectReaction(target) is { } reaction)
                {
                    steps.Add(new BattleAttackStep(target.Defender, reaction));
                }
            }
        }

        return steps;
    }
}
