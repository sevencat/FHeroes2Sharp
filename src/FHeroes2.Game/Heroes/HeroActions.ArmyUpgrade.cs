using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What came of a visit to something that upgrades troops, <c>ActionToUpgradeArmyObject()</c>
/// (<c>heroes_action.cpp:2642</c>).
/// </summary>
public enum ArmyUpgradeOutcome
{
    /// <summary>The object keeps no upgrade table of its own, <c>heroes_action.cpp:2682-2685</c>.</summary>
    NotAnUpgradeObject,

    /// <summary>The hero carries none of the creatures the object upgrades, <c>heroes_action.cpp:2697-2699</c>.</summary>
    NothingToUpgrade,

    /// <summary>At least one stack was upgraded, <c>heroes_action.cpp:2700-2756</c>.</summary>
    Upgraded,
}

/// <summary>
/// What the upgrade came to.
/// </summary>
/// <param name="Outcome">Whether anything was upgraded.</param>
/// <param name="UpgradedFrom">The creatures that were upgraded, in the order the reference lists them.</param>
public readonly record struct ArmyUpgradeResult(ArmyUpgradeOutcome Outcome, IReadOnlyList<MonsterType> UpgradedFrom);

/// <summary>
/// What a visit to the stables came to, <c>ActionToStables()</c> (<c>heroes_action.cpp:3313</c>).
/// </summary>
/// <param name="TookFreshHorse">Whether this was the first visit of this hero, i.e. whether he got the extra movement.</param>
/// <param name="Upgrade">What happened to the cavalry he carries.</param>
public readonly record struct StablesResult(bool TookFreshHorse, ArmyUpgradeResult Upgrade);

public sealed partial class HeroActions
{
    /// <summary>
    /// Turns riders and footmen into their better versions, <c>ActionToUpgradeArmyObject()</c>
    /// (<c>heroes_action.cpp:2642</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2642-2757</c>。三座瓦片共用这一支，只有「升哪几种」的表不同：
    /// 山丘堡垒升食人魔/兽人/矮人，铸造厂升剑士/长矛兵/石像鬼，马厩升骑兵。
    /// 一律<b>免费</b>——参考只查 <c>Army::HasMonster()</c>，不查钱、不问话。
    /// </para>
    /// <para>
    /// 端口与参考的一处差别在 <c>Army::UpgradeMonsters()</c> 的返回值上：参考靠它判断「有没有升成功」，
    /// 并在成功时按 <c>GetUpgrade()</c> 拼一段话；端口让这个方法返回「升了哪些」，界面自己去查升级后的种类。
    /// </para>
    /// <para>
    /// 级别不够的兵（比如没升级的弓箭手）留在原地不动，所以一次拜访可能只升两三种——甚至一种都没升，
    /// 那时参考也只是照常说话，什么都不改，端口用 <see cref="ArmyUpgradeOutcome.NothingToUpgrade"/> 表达同一件事。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <returns>Whether anything was upgraded and what.</returns>
    public ArmyUpgradeResult ActionToUpgradeArmyObject(Mp2ObjectType objectType)
    {
        MonsterType[] targets = objectType switch
        {
            Mp2ObjectType.HillFort => [MonsterType.Ogre, MonsterType.Orc, MonsterType.Dwarf],
            Mp2ObjectType.FreemansFoundry => [MonsterType.Swordsman, MonsterType.Pikeman, MonsterType.IronGolem],
            Mp2ObjectType.Stables => [MonsterType.Cavalry],
            _ => [],
        };

        if (targets.Length == 0)
        {
            Debug.Assert(false, $"An army upgrade was asked of {objectType}.");

            return new ArmyUpgradeResult(ArmyUpgradeOutcome.NotAnUpgradeObject, []);
        }

        List<MonsterType> upgraded = [];

        foreach (MonsterType monster in targets)
        {
            if (!_hero.Army.HasMonster(monster))
            {
                continue;
            }

            _hero.Army.UpgradeMonsters(monster);
            upgraded.Add(monster);
        }

        return upgraded.Count == 0
            ? new ArmyUpgradeResult(ArmyUpgradeOutcome.NothingToUpgrade, [])
            : new ArmyUpgradeResult(ArmyUpgradeOutcome.Upgraded, upgraded);
    }
}
