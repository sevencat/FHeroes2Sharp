using System.Diagnostics.CodeAnalysis;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The dispatcher: one tile in, one <c>ActionTo*</c> out, <c>Heroes::Action()</c> (<c>heroes_action.cpp:3747</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>Heroes::Action()</c> 是一张 300 行的 <c>switch</c>：先取瓦片上的对象类型，判「这是不是英雄能触发的
/// 动作对象」，是就置 <c>ACTION</c> 模式，然后按类型调一支 <c>ActionTo*</c>。端口把这 30 支都搬完了
/// （<see cref="HeroActions"/>），却一直没有分发者；这一文件补的就是它。
/// </para>
/// <para>
/// <b>类型在进门处折一次</b>：瓦片存的是动作值（<see cref="Mp2ObjectType.ActionWindmill"/>），而端口的名册、
/// 分类谓词与 30 支动作的入参讲的都是非动作值（<see cref="Mp2ObjectTypes.NonActionType"/>，ADR-133）。分发器是
/// 这两个世界唯一的交点，所以它先把类型折成非动作值再 <c>switch</c>——这也让每一支的 <c>case</c> 与参考的
/// <c>OBJ_*</c> 名字能直接对上（参考的 <c>OBJ_WINDMILL</c> 是带位的那一个，端口比不带位的）。
/// </para>
/// <para>
/// <b>结果给谁</b>：参考没有返回值，它靠对话框把结果说给玩家听，端口没有对话框层。所以结果折成
/// <see cref="HeroActionResult"/>：<see cref="HeroActionResult.Kind"/> 说是哪一支干的活，
/// <see cref="HeroActionResult.Changed"/> 说世界有没有被改动（要不要重绘、要不要刷面板），
/// <see cref="HeroActionResult.Payload"/> 装着那一支自己的结果（<see cref="Funds"/>、
/// <see cref="TreasureChestReward"/>、<see cref="TeleportOutcome"/> ……），用
/// <see cref="HeroActionResult.TryGetPayload{T}"/> 取。等消息层落地，它就是这一格的消费者。
/// </para>
/// <para>
/// <b>还没搬的对象</b>：怪物、城堡、另一个英雄、船、金字塔、沉船、龙穴、矿井、招募所、事件、神谕、狮身人面像
/// ——参考在这半数 <c>case</c> 上都要战斗、要城堡界面或要文本系统，端口的落点还不存在。它们不会静默通过：
/// <see cref="HeroActionKind.Unsupported"/> 明说「参考在这里会做事，端口还不会」，调用方可以据此提示玩家或者
/// 干脆不放他走过去。踩在空地上、或者那一格根本没有对象时才是 <see cref="HeroActionKind.None"/>。
/// </para>
/// <para>
/// <b>两步走的对象</b>：竞技场要先问玩家加哪一项主技能（参考在函数里弹 <c>Dialog::SelectSkillFromArena</c>），
/// 所以分发器只报到 <see cref="HeroActionKind.Arena"/>、什么都不改，由调用方问完再调
/// <see cref="ActionToArena"/>。马格勒的航海图与有主的神器同理，但它们的「还没问」本来就是自己返回值里的一员
/// （<see cref="MagellansMapsOutcome.NeedsConfirmation"/>、<see cref="ArtifactPickupOutcome.NeedsPayment"/>），
/// 所以照样分发，结果里自会说明。宝箱那条岔路（钱还是经验）用动作的默认值：拿钱。
/// </para>
/// <para>
/// <b>两处缺口</b>：参考在船长的位置上还会判 <c>isSuitableForDisembarkation</c> 走 <c>ActionToCoast</c>，端口的
/// <see cref="HeroModes"/> 里没有 SHIPMASTER、船也还没搬，故略过；参考对 AI 控制的王国直接转给
/// <c>AI::HeroesAction</c>，端口没有 AI，一律走人这一支。
/// </para>
/// </remarks>
public sealed partial class HeroActions
{
    /// <summary>
    /// Runs the action of the object on <paramref name="tileIndex"/>, <c>Heroes::Action( tileIndex )</c>
    /// (<c>heroes_action.cpp:3747</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 调用方是移动系统的「走到这一格了」那一刻：参考在 <c>heroesMove</c> 结束处调它，端口还没有真正的移动循环
    /// （<see cref="Hero.Movement"/> 只算路），所以这里先只有入口。
    /// </para>
    /// <para>
    /// 对象类型的读法与参考一致：<c>tile.getMainObjectType( tileIndex != heroPosIndex )</c>——目标格不是英雄脚下那
    /// 一格时看瓦片缓存的那一层（对象在英雄面前），是脚下那一格时透过英雄看（英雄踩在井上、风车上、立石上）。
    /// </para>
    /// <para>
    /// 英雄能触发动作对象时会置上 <see cref="HeroModes.Action"/>（参考在 <c>switch</c> 之前就置）；清掉它的是
    /// 动作动画的收尾方，参考里是界面层，端口是 <see cref="Hero.Movement"/>。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Index of the tile whose object the hero is acting on.</param>
    /// <param name="world">The world the tile belongs to.</param>
    /// <returns>Which action ran, whether it changed anything, and what it returned.</returns>
    public HeroActionResult Action(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Mp2ObjectType tileObjectType = tile.GetMainObjectType(world, tileIndex != _hero.MapIndex);

        if (!Mp2ObjectTypes.IsInGameActionObject(tileObjectType, false))
        {
            return new HeroActionResult(Mp2ObjectTypes.NonActionType(tileObjectType), HeroActionKind.None, false, null);
        }

        _hero.SetModes(HeroModes.Action);

        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tileObjectType);

        HeroActionResult Report(HeroActionKind kind, bool changed, object? payload = null) =>
            new(objectType, kind, changed, payload);

        // 「改了没有」本身就是那几支的全部结果（布尔），两个格子装同一个值。
        HeroActionResult ReportFlag(HeroActionKind kind, bool outcome) => new(objectType, kind, outcome, outcome);

        switch (objectType)
        {
            case Mp2ObjectType.MagicWell:
                return ReportFlag(HeroActionKind.MagicWell, ActionToMagicWell(tileIndex));

            case Mp2ObjectType.ArtesianSpring:
                return ReportFlag(HeroActionKind.ArtesianSpring, ActionToArtesianSpring(tileIndex));

            case Mp2ObjectType.Buoy:
            case Mp2ObjectType.Oasis:
            case Mp2ObjectType.WateringHole:
            case Mp2ObjectType.Temple:
                return ReportFlag(HeroActionKind.GoodMoraleObject, ActionToGoodMoraleObject(objectType, tileIndex));

            case Mp2ObjectType.Fountain:
            case Mp2ObjectType.FaerieRing:
            case Mp2ObjectType.Idol:
            case Mp2ObjectType.Mermaid:
                return ReportFlag(HeroActionKind.GoodLuckObject, ActionToGoodLuckObject(objectType, tileIndex));

            case Mp2ObjectType.Artifact:
            {
                ArtifactPickupOutcome outcome = ActionToArtifact(tileIndex, world);

                return Report(HeroActionKind.Artifact, outcome == ArtifactPickupOutcome.Picked, outcome);
            }

            case Mp2ObjectType.TreasureChest:
            case Mp2ObjectType.SeaChest:
            {
                TreasureChestReward reward = ActionToTreasureChest(tileIndex, world);

                return Report(HeroActionKind.TreasureChest, !reward.IsEmpty, reward);
            }

            case Mp2ObjectType.Resource:
            case Mp2ObjectType.Campfire:
            case Mp2ObjectType.Barrel:
            {
                Funds funds = ActionToPickupResource(tileIndex, world);

                return Report(HeroActionKind.PickupResource, funds.GetValidItemsCount() > 0, funds);
            }

            case Mp2ObjectType.Skeleton:
            {
                ObjectPickupReward reward = ActionToSkeleton(tileIndex, world);

                return Report(HeroActionKind.Skeleton, !reward.IsEmpty, reward);
            }

            case Mp2ObjectType.Wagon:
            {
                ObjectPickupReward reward = ActionToWagon(tileIndex, world);

                return Report(HeroActionKind.Wagon, !reward.IsEmpty, reward);
            }

            case Mp2ObjectType.Flotsam:
            {
                Funds funds = ActionToFlotsam(tileIndex, world);

                return Report(HeroActionKind.Flotsam, funds.GetValidItemsCount() > 0, funds);
            }

            case Mp2ObjectType.ShipwreckSurvivor:
            {
                ObjectPickupReward reward = ActionToShipwreckSurvivor(tileIndex, world);

                return Report(HeroActionKind.ShipwreckSurvivor, !reward.IsEmpty, reward);
            }

            case Mp2ObjectType.Jail:
            {
                JailOutcome outcome = ActionToJail(tileIndex, world);

                return Report(HeroActionKind.Jail, outcome == JailOutcome.Released, outcome);
            }

            case Mp2ObjectType.Gazebo:
                return ReportFlag(HeroActionKind.ExperienceObject, ActionToExperienceObject(tileIndex, world));

            case Mp2ObjectType.TreeOfKnowledge:
            {
                TreeKnowledgeOutcome outcome = ActionToTreeKnowledge(tileIndex, world);

                return Report(HeroActionKind.TreeKnowledge, outcome == TreeKnowledgeOutcome.Learned, outcome);
            }

            case Mp2ObjectType.ShrineFirstCircle:
            case Mp2ObjectType.ShrineSecondCircle:
            case Mp2ObjectType.ShrineThirdCircle:
            {
                ShrineOutcome outcome = ActionToShrine(tileIndex, world);

                return Report(HeroActionKind.Shrine, outcome == ShrineOutcome.Learned, outcome);
            }

            case Mp2ObjectType.WitchsHut:
            {
                WitchsHutOutcome outcome = ActionToWitchsHut(tileIndex, world);

                return Report(HeroActionKind.WitchsHut, outcome == WitchsHutOutcome.Learned, outcome);
            }

            case Mp2ObjectType.Barrier:
                // 什么也不改：只是回答「同色的帐篷进过没有」。
                return Report(HeroActionKind.Barrier, false, ActionToBarrier(tileIndex, world));

            case Mp2ObjectType.Arena:
                // 参考在这里弹四选一；端口把选择留给调用方，问完再调 ActionToArena。
                return Report(HeroActionKind.Arena, false);

            case Mp2ObjectType.BlackCat:
                return ReportFlag(HeroActionKind.BlackCat, ActionToBlackCat(tileIndex));

            case Mp2ObjectType.Xanadu:
            {
                XanaduOutcome outcome = ActionToXanadu(tileIndex);

                return Report(HeroActionKind.Xanadu, outcome == XanaduOutcome.Trained, outcome);
            }

            case Mp2ObjectType.Windmill:
            case Mp2ObjectType.WaterWheel:
            case Mp2ObjectType.LeanTo:
            case Mp2ObjectType.MagicGarden:
            {
                Funds funds = ActionToObjectResource(objectType, tileIndex, world);

                return Report(HeroActionKind.ObjectResource, funds.GetValidItemsCount() > 0, funds);
            }

            case Mp2ObjectType.TravellerTent:
                // 记号是幂等的：第二次来也「改」一次同一个位，所以这里恒为 true。
                return Report(HeroActionKind.TravellersTent, true, ActionToTravellersTent(tileIndex, world));

            case Mp2ObjectType.Stables:
                return ReportFlag(HeroActionKind.Stables, ActionToStables(tileIndex));

            case Mp2ObjectType.Obelisk:
                return ReportFlag(HeroActionKind.Obelisk, ActionToObelisk(tileIndex, world));

            case Mp2ObjectType.Sirens:
            {
                SirensReward reward = ActionToSirens(tileIndex, world);

                return Report(HeroActionKind.Sirens, !reward.AlreadyVisited, reward);
            }

            case Mp2ObjectType.Sign:
                return Report(HeroActionKind.Sign, false, ActionToSign(tileIndex, world));

            case Mp2ObjectType.ObservationTower:
            {
                int revealed = ActionToObservationTower(tileIndex, world);

                return Report(HeroActionKind.ObservationTower, revealed > 0, revealed);
            }

            case Mp2ObjectType.MagellansMaps:
            {
                MagellansMapsOutcome outcome = ActionToMagellanMaps(tileIndex, world);

                return Report(HeroActionKind.MagellansMaps, outcome == MagellansMapsOutcome.Bought, outcome);
            }

            case Mp2ObjectType.StoneLiths:
            {
                TeleportOutcome outcome = ActionToTeleports(tileIndex, world);

                return Report(HeroActionKind.Teleports, outcome == TeleportOutcome.Moved, outcome);
            }

            case Mp2ObjectType.Whirlpool:
            {
                TeleportOutcome outcome = ActionToWhirlpools(tileIndex, world);

                return Report(HeroActionKind.Whirlpools, outcome == TeleportOutcome.Moved, outcome);
            }

            default:
                return Report(HeroActionKind.Unsupported, false);
        }
    }

}

/// <summary>
/// Which <c>ActionTo*</c> of <see cref="HeroActions"/> ran for a tile, <see cref="HeroActions.Action"/>.
/// </summary>
/// <remarks>
/// 名字照着参考的函数取：<see cref="MagicWell"/> 就是 <c>ActionToMagicWell()</c>。这样调用方
/// （消息层、AI、测试）说的是「哪一支」，而不是「哪个对象」——好几个对象共用一支
/// （<see cref="GoodMoraleObject"/> 有四个、<see cref="ObjectResource"/> 也有四个）。
/// </remarks>
public enum HeroActionKind
{
    /// <summary>Nothing to do: the tile holds no object, or one that no hero triggers.</summary>
    None = 0,

    /// <summary>The reference would act on this object; the port has no handler for it yet.</summary>
    Unsupported,

    /// <summary><see cref="HeroActions.ActionToMagicWell"/> ran.</summary>
    MagicWell,

    /// <summary><see cref="HeroActions.ActionToArtesianSpring"/> ran.</summary>
    ArtesianSpring,

    /// <summary><see cref="HeroActions.ActionToGoodMoraleObject"/> ran.</summary>
    GoodMoraleObject,

    /// <summary><see cref="HeroActions.ActionToGoodLuckObject"/> ran.</summary>
    GoodLuckObject,

    /// <summary><see cref="HeroActions.ActionToArtifact"/> ran.</summary>
    Artifact,

    /// <summary><see cref="HeroActions.ActionToTreasureChest"/> ran.</summary>
    TreasureChest,

    /// <summary><see cref="HeroActions.ActionToPickupResource"/> ran.</summary>
    PickupResource,

    /// <summary><see cref="HeroActions.ActionToSkeleton"/> ran.</summary>
    Skeleton,

    /// <summary><see cref="HeroActions.ActionToWagon"/> ran.</summary>
    Wagon,

    /// <summary><see cref="HeroActions.ActionToFlotsam"/> ran.</summary>
    Flotsam,

    /// <summary><see cref="HeroActions.ActionToShipwreckSurvivor"/> ran.</summary>
    ShipwreckSurvivor,

    /// <summary><see cref="HeroActions.ActionToJail"/> ran.</summary>
    Jail,

    /// <summary><see cref="HeroActions.ActionToExperienceObject"/> ran.</summary>
    ExperienceObject,

    /// <summary><see cref="HeroActions.ActionToTreeKnowledge"/> ran.</summary>
    TreeKnowledge,

    /// <summary><see cref="HeroActions.ActionToShrine"/> ran.</summary>
    Shrine,

    /// <summary><see cref="HeroActions.ActionToWitchsHut"/> ran.</summary>
    WitchsHut,

    /// <summary><see cref="HeroActions.ActionToBarrier"/> ran.</summary>
    Barrier,

    /// <summary>An arena: recognised, but the player still has to pick the skill for <see cref="HeroActions.ActionToArena"/>.</summary>
    Arena,

    /// <summary><see cref="HeroActions.ActionToBlackCat"/> ran.</summary>
    BlackCat,

    /// <summary><see cref="HeroActions.ActionToXanadu"/> ran.</summary>
    Xanadu,

    /// <summary><see cref="HeroActions.ActionToObjectResource"/> ran.</summary>
    ObjectResource,

    /// <summary><see cref="HeroActions.ActionToTravellersTent"/> ran.</summary>
    TravellersTent,

    /// <summary><see cref="HeroActions.ActionToStables"/> ran.</summary>
    Stables,

    /// <summary><see cref="HeroActions.ActionToObelisk"/> ran.</summary>
    Obelisk,

    /// <summary><see cref="HeroActions.ActionToSirens"/> ran.</summary>
    Sirens,

    /// <summary><see cref="HeroActions.ActionToSign"/> ran.</summary>
    Sign,

    /// <summary><see cref="HeroActions.ActionToObservationTower"/> ran.</summary>
    ObservationTower,

    /// <summary><see cref="HeroActions.ActionToMagellanMaps"/> ran.</summary>
    MagellansMaps,

    /// <summary><see cref="HeroActions.ActionToTeleports"/> ran.</summary>
    Teleports,

    /// <summary><see cref="HeroActions.ActionToWhirlpools"/> ran.</summary>
    Whirlpools,
}

/// <summary>
/// What <see cref="HeroActions.Action"/> did: which handler ran, whether the world moved, and what it returned.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>Heroes::Action()</c> 没有返回值——它靠对话框直接说给玩家听。端口没有对话框层（ADR-122），
/// 结果必须交回给调用方，于是有了这个结构。
/// </para>
/// <para>
/// <see cref="Changed"/> 说的是<b>世界状态</b>有没有被改动（要不要重绘地图、要不要刷英雄面板），不是「玩家要不要
/// 看到消息」：告示牌什么都不改，但它的文字在 <see cref="Payload"/> 里；同样，第二次踩上法力泉
/// <see cref="Changed"/> 是 <see langword="false"/>，可调用方照样该说一句「晚点再来」。
/// </para>
/// <para>
/// <see cref="Payload"/> 装的是那一支自己的返回值（<see cref="Funds"/>、<see cref="TreasureChestReward"/>、
/// <see cref="TeleportOutcome"/> ……）。30 支动作各有各的结果类型，硬挤进一个结构只能是 <see cref="object"/>——
/// 每一次冒险地图动作都是玩家的一个点击，装箱的代价无关紧要。等消息层落地后，这一格会换成按
/// <see cref="Kind"/> 分派的消息记录，那时它就退场。
/// </para>
/// </remarks>
/// <param name="ObjectType">The object the action ran on, in the non-action vocabulary.</param>
/// <param name="Kind">Which handler ran.</param>
/// <param name="Changed">Whether the action changed game state.</param>
/// <param name="Payload">The handler's own result, <see langword="null"/> when there is none.</param>
public readonly record struct HeroActionResult(
    Mp2ObjectType ObjectType,
    HeroActionKind Kind,
    bool Changed,
    object? Payload)
{
    /// <summary>
    /// Whether <see cref="Payload"/> is a <typeparamref name="T"/>, and if so, it.
    /// </summary>
    /// <typeparam name="T">The result type the caller expects.</typeparam>
    /// <param name="payload">The strongly typed payload when it matched.</param>
    /// <returns><see langword="true"/> when the payload had that type.</returns>
    public bool TryGetPayload<T>([MaybeNullWhen(false)] out T payload)
    {
        if (Payload is T value)
        {
            payload = value;

            return true;
        }

        payload = default;

        return false;
    }
}
