using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

using ArmyStack = FHeroes2.Game.Army.Army;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What happened when the hero walked onto an object a player can own, <c>ActionToCaptureObject()</c>
/// (<c>heroes_action.cpp:2022</c>).
/// </summary>
/// <remarks>
/// 参考只有三个出口：本来就是朋友的不动、有守军先打、没守军直接接管。打完输了还可能只是把守军打光——
/// 那时守军记录被清零，矿上的守卫法术被摘掉，英雄照样被抬走（<c>2163-2177</c>）。
/// </remarks>
public enum CaptureObjectOutcome
{
    /// <summary>No capturable object on the tile; nothing happened (<c>assert( 0 )</c> in the reference).</summary>
    NoObject,

    /// <summary>The object already belongs to this hero's side, <c>heroes_action.cpp:2028</c>.</summary>
    AlreadyFriendly,

    /// <summary>Nobody was guarding it: the colour simply moved over, <c>heroes_action.cpp:2179-2181</c>.</summary>
    Captured,

    /// <summary>The guards were beaten and the object taken, <c>heroes_action.cpp:2157-2161</c>.</summary>
    Won,

    /// <summary>The guards held; the hero was dismissed, <c>heroes_action.cpp:2176</c>.</summary>
    Lost,
}

/// <summary>
/// What happened at the abandoned gold mine, <c>ActionToAbandonedMine()</c> (<c>heroes_action.cpp:2185</c>).
/// </summary>
public enum AbandonedMineOutcome
{
    /// <summary>The hero chose not to go in, <c>heroes_action.cpp:2194-2198</c>.</summary>
    Declined,

    /// <summary>The ghosts were beaten and the mine reopened, <c>heroes_action.cpp:2210-2229</c>.</summary>
    Restored,

    /// <summary>The ghosts won; the hero was dismissed, <c>heroes_action.cpp:2232</c>.</summary>
    Lost,
}

/// <summary>
/// What happened at a graveyard or a wreck, <c>ActionToPoorMoraleObject()</c> (<c>heroes_action.cpp:1354</c>).
/// </summary>
public enum PoorMoraleObjectOutcome
{
    /// <summary>The hero chose not to search, <c>heroes_action.cpp:1391-1393</c>.</summary>
    Declined,

    /// <summary>Nothing was there: a visit record and a morale warning, <c>heroes_action.cpp:1451-1460</c>.</summary>
    NothingFound,

    /// <summary>The guards were beaten and the loot taken, <c>heroes_action.cpp:1406-1441</c>.</summary>
    Found,

    /// <summary>The guards won; the hero was dismissed, <c>heroes_action.cpp:1442-1444</c>.</summary>
    Lost,
}

public sealed partial class HeroActions
{
    /// <summary>
    /// Taking over a mine, a sawmill, an alchemist's lab or a lighthouse, <c>ActionToCaptureObject()</c>
    /// (<c>heroes_action.cpp:2022</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2022-2183</c>。外层只有一判——<c>!hero.isFriends( getColorFromTile( tile ) )</c>，
    /// 也就是「已经是我方（含盟友）的就不动」，连提示都没有。
    /// </para>
    /// <para>
    /// <b>守护与所有权是两层</b>：守军住在 <see cref="CapturedObject.Guardians"/>（<see cref="Tile.GetTroop"/> 对可捕获
    /// 对象就是它），所有权住在同一张表的颜色字段。所以「打掉守军但英雄输了」只清守军、不改颜色
    /// （<c>2167-2174</c>）；<b>矿还有第三层</b>，守卫法术记在瓦片元数据上，靠
    /// <see cref="Tile.RemoveMineSpell"/> 摘掉。
    /// </para>
    /// <para>
    /// <b>不弹窗</b>：参考在接管后按对象挑一句话（「你接管了锯木厂，每天给 2 木材」），资源种类与数量全在瓦片
    /// 元数据里，调用方读 <see cref="Tile.GetDailyIncomeObjectResources"/> 自己拼。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object being taken, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the capture ended.</returns>
    public CaptureObjectOutcome ActionToCaptureObject(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);

        // 站上已经拿下的矿之后瓦片上放的是英雄本身（ActionHero），真正的东西记在英雄身上
        // （heroes_action.cpp:2214 把 OBJ_MINE 记进 hero），所以这里两种都要看。
        Mp2ObjectType tileType = tile.MainObjectType == Mp2ObjectType.ActionHero
            ? tile.GetMainObjectType(world, false)
            : tile.MainObjectType;

        if (!Mp2ObjectTypes.IsCaptureObject(tileType))
        {
            Debug.Assert(false, $"A capture action was dispatched to {objectType} on a tile that cannot be captured.");

            return CaptureObjectOutcome.NoObject;
        }

        if (_hero.IsFriends(tile.GetColor(world)))
        {
            return CaptureObjectOutcome.AlreadyFriendly;
        }

        if (tile.IsCaptureObjectProtected(world))
        {
            ArmyStack defenders = new();
            defenders.SetFromTile(world, tile);

            BattleResult result = Fight(world, _hero, defenders, tileIndex, castle: null);

            if (result.IsAttackerWin)
            {
                _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

                CaptureObject(tileIndex, world);

                return CaptureObjectOutcome.Won;
            }

            uint monstersLeft = defenders.GetCount();
            Troop guardians = world.GetCapturedObject(tileIndex).Guardians;

            Debug.Assert(
                defenders.GetCountMonsters(guardians.Monster) == monstersLeft,
                "The defeated guards must be the original stack of the object.");

            if (monstersLeft > 0)
            {
                guardians.Count = monstersLeft;
            }
            else
            {
                // 守军全灭但英雄还是输了：保护没了，矿照旧留在那儿等下一拨人来抢（2170-2174）。
                RemoveObjectProtection(world, tile);
            }

            BattleLose(world, _hero, result, attacker: true);

            return CaptureObjectOutcome.Lost;
        }

        CaptureObject(tileIndex, world);

        return CaptureObjectOutcome.Captured;
    }

    /// <summary>
    /// The abandoned gold mine, <c>ActionToAbandonedMine()</c> (<c>heroes_action.cpp:2185</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2185-2236</c>。先问「要进去吗」（<c>Dialog::YES|NO</c>），进去就是一场战斗，
    /// 赢了把废墟恢复成金矿并接管，输了走 <c>BattleLose</c>。
    /// </para>
    /// <para>
    /// <b>恢复是三步且顺序有意义</b>：<see cref="Tile.RestoreAbandonedMine"/> 换掉瓦片主对象与图块、
    /// 把英雄脚下的对象类型改成矿（否则「透过英雄看」还会看到废墟）、再改颜色。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the ruins stand on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the visit ended.</returns>
    public AbandonedMineOutcome ActionToAbandonedMine(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_host.Ask(HeroActionQuestion.EnterAbandonedMine))
        {
            return AbandonedMineOutcome.Declined;
        }

        Tile tile = world.GetTile(tileIndex);

        ArmyStack defenders = new();
        defenders.SetFromTile(world, tile);

        BattleResult result = Fight(world, _hero, defenders, tileIndex, castle: null);

        if (!result.IsAttackerWin)
        {
            BattleLose(world, _hero, result, attacker: true);

            return AbandonedMineOutcome.Lost;
        }

        _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

        tile.RestoreAbandonedMine(world, Resource.Gold);
        _hero.SetObjectTypeUnderHero(Mp2ObjectType.Mine);
        world.CaptureObject(tileIndex, _hero.Color);

        return AbandonedMineOutcome.Restored;
    }

    /// <summary>
    /// Searching a graveyard or a wreck, <c>ActionToPoorMoraleObject()</c> (<c>heroes_action.cpp:1354</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1354-1461</c>。这里有两种「宝藏」——瓦片上的金币与瓦片上的神器，
    /// 有金币就说明有守军（骷髅/僵尸/幽灵），要打；<b>没金币就没有守军，也什么都没有</b>，只有一段
    /// 「白忙一场」的提示与一次士气警告（<c>1451-1460</c>）。
    /// </para>
    /// <para>
    /// <b>参考在这里不清元数据</b>：白忙那一支只记访问，弃船与坟场于是可以反复搜（每搜一次看完那段提示），
    /// 直到有金币的那一支把它清掉。端口照抄，见 <see cref="PoorMoraleObjectOutcome.NothingFound"/>。
    /// </para>
    /// <para>
    /// <b>背包满则折金币</b>：神器给了但背包装不下时按 <see cref="ArtifactGold.InsteadOf"/> 折钱
    /// （<c>1418-1424</c>），金币与神器<b>可能同时给</b>——瓦片上的金币是战利品，与神器无关。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object being searched, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the search ended.</returns>
    public PoorMoraleObjectOutcome ActionToPoorMoraleObject(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_host.Ask(HeroActionQuestion.SearchPoorMoraleObject))
        {
            return PoorMoraleObjectOutcome.Declined;
        }

        Tile tile = world.GetTile(tileIndex);
        Funds loot = tile.GetFundsFromTile(world);
        uint gold = (uint)loot.Gold;
        bool complete = false;
        ObjectPickupReward reward = default;

        if (gold > 0)
        {
            ArmyStack defenders = new();
            defenders.SetFromTile(world, tile);

            BattleResult result = Fight(world, _hero, defenders, tileIndex, castle: null);

            if (!result.IsAttackerWin)
            {
                BattleLose(world, _hero, result, attacker: true);

                return PoorMoraleObjectOutcome.Lost;
            }

            _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

            complete = true;

            ArtifactType artifact = Mp2ObjectTypes.IsArtifactObject(tile.MainObjectType)
                ? tile.GetArtifactFromTile(world)
                : ArtifactType.Unknown;

            if (artifact != ArtifactType.Unknown)
            {
                if (_hero.BagArtifacts.IsFull)
                {
                    gold = ArtifactGold.InsteadOf(objectType);
                }
                else if (_hero.BagArtifacts.Push(artifact))
                {
                    reward = new ObjectPickupReward(default, artifact);
                }
            }

            Funds payment = new(Resource.Gold, gold);

            if (payment.Gold > 0)
            {
                Kingdom.AddFundsResource(payment);

                reward = reward with { Funds = payment };
            }
        }

        if (complete)
        {
            tile.ResetObjectMetadata();
            Kingdom.SetVisited(tileIndex, objectType);

            return PoorMoraleObjectOutcome.Found;
        }

        // 白忙一场：本地与全局各记一笔，士气警告归界面（参考只是把图标画进对话框，没改士气值）。
        _hero.SetVisited(tileIndex, objectType);
        Kingdom.SetVisited(tileIndex, objectType);

        return PoorMoraleObjectOutcome.NothingFound;
    }

    /// <summary>
    /// Marks the object as the hero's, <c>setColorOnTile( tile, hero.GetColor() )</c> plus the protection reset
    /// of <c>heroes_action.cpp:2070-2075</c>.
    /// </summary>
    /// <param name="tileIndex">The tile to take over.</param>
    /// <param name="world">The world.</param>
    private void CaptureObject(int tileIndex, World world)
    {
        RemoveObjectProtection(world, world.GetTile(tileIndex));

        world.CaptureObject(tileIndex, _hero.Color);
    }

    /// <summary>
    /// Drops what guards a capturable object besides its troops, the <c>removeObjectProtection</c> lambda of
    /// <c>heroes_action.cpp:2063-2068</c>.
    /// </summary>
    /// <remarks>
    /// 参考这里只有一件事：矿上的守卫法术（<c>SetElementalGuardian</c> 那一批）要在换主人时消失，否则新主人会
    /// 被自己的矿拦住。其余可捕获对象的守护就是 <see cref="CapturedObject.Guardians"/> 本身，由调用方清零。
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="tile">The captured object.</param>
    private static void RemoveObjectProtection(World world, Tile tile)
    {
        if (Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(world, false)) == Mp2ObjectType.Mine)
        {
            tile.RemoveMineSpell(world);
        }
    }
}
