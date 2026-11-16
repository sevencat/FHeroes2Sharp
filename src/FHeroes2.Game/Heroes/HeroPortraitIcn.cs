using FHeroes2.Assets.Icn;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The ICN that holds the portrait of a hero, the reference's <c>ICN::getHeroPortraitIcnId</c>
/// (<c>src/fheroes2/agg/icn.cpp:726</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考那张 <c>switch</c> 有七十四个 <c>case</c>，但它是纯粹的线性映射：<c>LORDKILBURN</c>（1）到
/// <c>JARKONAS</c>（71）依次是 <c>PORT0000</c> 到 <c>PORT0070</c>。端口用算术表达同一条映射，并在测试里
/// 钉住两端的值；<c>DEBUG_HERO</c> 借用 <c>BRAX</c>（<c>PORT0059</c>）这一处不是线性的，单独一支。
/// </para>
/// <para>
/// 它放在游戏层而不是 <c>FHeroes2.Assets.Icn.IcnCatalog</c>：英雄的 id 属于 <c>Hero</c>，而资源层看不见
/// 游戏层。
/// </para>
/// </remarks>
public static class HeroPortraitIcn
{
    /// <summary>
    /// The portrait ICN of a hero.
    /// </summary>
    /// <param name="heroId">The hero id, from <see cref="Hero.UnknownId"/> to <see cref="Hero.DebugHero"/>.</param>
    /// <returns>
    /// The portrait ICN, or <see cref="IcnType.Unknown"/> for <see cref="Hero.UnknownId"/> and for any id the
    /// reference has no branch for.
    /// </returns>
    /// <remarks>参考：<c>icn.cpp:726-882</c>。</remarks>
    public static IcnType Get(int heroId)
    {
        if (heroId == Hero.UnknownId)
        {
            return IcnType.Unknown;
        }

        // The eleven The Price of Loyalty heroes continue the same run: 61 is PORT0060.
        if (heroId >= Hero.LordKilburn && heroId <= Hero.Jarkonas)
        {
            return (IcnType)((int)IcnType.Port0000 + (heroId - Hero.LordKilburn));
        }

        // The debug hero has no portrait of its own and borrows Brax's.
        if (heroId == Hero.DebugHero)
        {
            return IcnType.Port0059;
        }

        return IcnType.Unknown;
    }
}
