using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Ai;

/// <summary>
/// Forgets the hero's meetings with his own kingdom's heroes once his army has changed,
/// <c>Heroes::AIHeroMeetingUpdater</c> (<c>heroes.h:245-290</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用 RAII：进 <c>AI::HeroesAction</c> 时记下兵力，出来时如果兵力变了（换兵、被抢），就把「见过谁」的记录全清掉——
/// 因为兵力变了说明那一次会面已经办完事，下次再撞上应该重新谈。C# 里用 <see cref="IDisposable"/> 与 <c>using</c> 表达同一件事。
/// </para>
/// <para>
/// 清记录要遍历王国的全部英雄（<c>heroes.cpp:1380-1396</c>），而英雄不该认识王国，所以那一圈放在这里。
/// 只有本次英雄与对方英雄两边都清，才不会出现「单方面记得见过」。
/// </para>
/// </remarks>
public sealed class AiHeroMeetingUpdater : IDisposable
{
    private readonly Hero _hero;
    private readonly double _initialArmyStrength;

    /// <summary>Records the current army strength (<c>heroes.h:251-256</c>).</summary>
    /// <param name="hero">The hero about to act.</param>
    public AiHeroMeetingUpdater(Hero hero)
    {
        _hero = hero;
        _initialArmyStrength = hero.Army.GetStrength();
    }

    /// <summary>
    /// Forgets the meetings if the army changed, <c>~AIHeroMeetingUpdater()</c> (<c>heroes.h:260-285</c>).
    /// </summary>
    public void Dispose()
    {
        if (Math.Abs(_initialArmyStrength - _hero.Army.GetStrength()) > 0.001)
        {
            UnmarkMeetings(_hero);
        }
    }

    /// <summary>
    /// <c>Heroes::unmarkHeroMeeting()</c> (<c>heroes.cpp:1380-1396</c>): both sides of every pair forget each other.
    /// </summary>
    private static void UnmarkMeetings(Hero hero)
    {
        GameKingdom kingdom = World.Current.GetKingdom(hero.Color);

        foreach (Hero other in kingdom.Heroes)
        {
            if (other is null || ReferenceEquals(other, hero))
            {
                continue;
            }

            other.RemoveVisited(hero.Id, Mp2ObjectType.Hero);
            hero.RemoveVisited(other.Id, Mp2ObjectType.Hero);
        }
    }
}
