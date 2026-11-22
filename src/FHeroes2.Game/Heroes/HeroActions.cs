using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The adventure-map actions of a hero that touch nothing but the hero and his kingdom, the first slice of
/// <c>heroes_action.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>heroes_action.cpp:2924</c> 的 <c>Heroes::Action()</c>——一个 3000 行的 <c>switch</c>，每一支都要
/// <c>hero.GetKingdom()</c>、都要弹对话框、都要放音效。端口没有那条「英雄 → 王国」的链接（英雄只有
/// <see cref="Hero.Color"/>，王国在 <see cref="Kingdoms"/> 里），也没有对话框层，所以这里把<b>纯状态</b>的那几支
/// 摘出来：写访问记录、加法力、加移动点。对话框与音效由调用方按返回值决定（ADR-122）。
/// </para>
/// <para>
/// 为什么这些方法不收 <c>objectType</c> 能自己判断的就不收：参考的 <c>ActionToGoodMoraleObject</c> 与
/// <c>ActionToGoodLuckObject</c> 是「一族对象共用一个函数」，好几种对象走同一条路并且<b>消息不同</b>。端口不需要
/// 消息，所以只需要类型来判断「该不该给移动点」；给不出的类型直接什么都不做。
/// </para>
/// <para>
/// 一个 <see cref="HeroActions"/> 是「这位英雄今天」的一组动作，用完即弃；它不持有状态。
/// </para>
/// <para>
/// 这些 <c>ActionTo*</c> 的分发者——参考那张 <c>switch</c>——在 <c>HeroActions.Action.cs</c> 里，
/// 入口是 <see cref="Action"/>，结果模型是 <see cref="HeroActionResult"/>（ADR-133）。
/// </para>
/// </remarks>
public sealed partial class HeroActions
{
    private readonly Hero _hero;
    private readonly Kingdoms _kingdoms;
    private readonly IHeroActionHost _host;

    /// <summary>
    /// Binds the actions to a hero and the container his kingdom lives in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 收 <see cref="Kingdoms"/> 而不是 <see cref="Kingdom"/>：英雄的颜色是固定的，王国却是可查的
    /// （<see cref="Kingdoms.GetKingdom"/> 对非法颜色返回第七个槽），于是调用方不必先自己解析一遍。
    /// </para>
    /// <para>
    /// <paramref name="host"/> 是参考里「停下来问玩家/开界面」的那些点，见 <see cref="IHeroActionHost"/>。
    /// 缺省用 <see cref="NullHeroActionHost"/>：每一步都答「不」、不开战斗界面、不开城堡屏——AI 与测试要的
    /// 正是这个，而人类那一支由界面层传自己的实现。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero performing the action.</param>
    /// <param name="kingdoms">The kingdoms of the world.</param>
    /// <param name="host">The layer above, or <see langword="null"/> for a host that is not there.</param>
    public HeroActions(Hero hero, Kingdoms kingdoms, IHeroActionHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(kingdoms);

        _hero = hero;
        _kingdoms = kingdoms;
        _host = host ?? NullHeroActionHost.Instance;
    }

    /// <summary>The hero acting, <c>*this</c> of the reference's free functions.</summary>
    public Hero Hero => _hero;

    /// <summary>The layer above: the player's answers, the battle screen and the castle screen.</summary>
    public IHeroActionHost Host => _host;

    /// <summary>The hero's kingdom, <c>hero.GetKingdom()</c>.</summary>
    private PlayerKingdom Kingdom => _kingdoms.GetKingdom(_hero.Color);

    /// <summary>
    /// A drink at the magic well, <c>ActionToMagicWell()</c> (<c>heroes_action.cpp:3906</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3906-3938</c>。三条分支：已经满法力（提示、不登记）、今天已经喝过（
    /// <c>isObjectTypeVisited</c>，提示）、否则补满并登记<b>本地</b>访问。
    /// </para>
    /// <para>
    /// 「今天」这个词是字面意思：魔法井是一天寿命的对象，英雄的 <see cref="Hero.ActionNewDay"/> 每天清掉这一批
    /// 记录。王国侧的同名记录也在清（<see cref="Kingdom.ActionNewDay"/>），两条路都能让井第二天重新可用。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the well stands on.</param>
    /// <returns><see langword="true"/> when the spell points were restored.</returns>
    public bool ActionToMagicWell(int tileIndex)
    {
        uint max = _hero.GetMaxSpellPoints();

        if (_hero.SpellPoints >= max)
        {
            return false;
        }

        if (_hero.IsObjectTypeVisited(Mp2ObjectType.MagicWell))
        {
            return false;
        }

        _hero.SetSpellPoints(max);
        _hero.SetVisited(tileIndex, Mp2ObjectType.MagicWell);

        return true;
    }

    /// <summary>
    /// A drink at the artesian spring, <c>ActionToArtesianSpring()</c> (<c>heroes_action.cpp:3993</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3993-4032</c>。「本周已经有人来过」（<c>world.isAnyKingdomVisited</c>，查的是
    /// <b>所有</b>王国）或者「法力已经到两倍」都只提示；否则补到两倍。<b>无论走哪条，最后都登记全局访问</b>——
    /// 这一条在参考里写在 if 外面，是最容易看漏的地方。
    /// </para>
    /// <para>
    /// 登记进王国名册（<c>Visit::GLOBAL</c>）：泉眼是「一方势力一周一次」，不是「一位英雄一周一次」。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the spring stands on.</param>
    /// <returns><see langword="true"/> when the spell points were raised to twice the maximum.</returns>
    public bool ActionToArtesianSpring(int tileIndex)
    {
        bool refilled = false;

        if (!_kingdoms.IsAnyVisited(tileIndex, Mp2ObjectType.ArtesianSpring))
        {
            uint twice = _hero.GetMaxSpellPoints() * 2;

            if (_hero.SpellPoints < twice)
            {
                _hero.SetSpellPoints(twice);
                refilled = true;
            }
        }

        Kingdom.SetVisited(tileIndex, Mp2ObjectType.ArtesianSpring);

        return refilled;
    }

    /// <summary>
    /// A visit to an object that raises morale, <c>ActionToGoodMoraleObject()</c> (<c>heroes_action.cpp:1478</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1478-1533</c>。处理的是一族对象：<c>BUOY</c>（+1 士气）、<c>OASIS</c>
    /// （+1 士气、+800 移动点）、<c>WATERING_HOLE</c>（+1 士气、+400 移动点）、<c>TEMPLE</c>（+2 士气）。
    /// </para>
    /// <para>
    /// <b>士气本身不在这里加</b>：这里只登记访问，士气由 <see cref="Hero.GetMorale"/> 每次重算时按「访问过的
    /// 对象类型」查表得出（<see cref="GameStatic.GetObjectMoraleEffect"/>）。这样战后的清理
    /// （<see cref="Hero.ActionAfterBattle"/>）一删记录，加成自己就没了，不需要配一个「减回去」的对称操作。
    /// </para>
    /// <para>
    /// 已经访问过就只提示（参考会提示「打完一仗再来」），移动点也不再给。给了哪一种对象就加多少移动点由
    /// <see cref="GameStatic.GetMovementPointBonus"/> 决定——浮标与神殿是 0，参考的 <c>IncreaseMovePoints( 0 )</c>
    /// 也就是什么都不做。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object visited; only the four morale objects do anything.</param>
    /// <param name="tileIndex">The tile the object stands on.</param>
    /// <returns><see langword="true"/> when the visit counted.</returns>
    public bool ActionToGoodMoraleObject(Mp2ObjectType objectType, int tileIndex)
    {
        if (!IsGoodMoraleObject(objectType) || _hero.IsObjectTypeVisited(objectType))
        {
            return false;
        }

        _hero.SetVisited(tileIndex, objectType);
        _hero.IncreaseMovePoints(GameStatic.GetMovementPointBonus(objectType));

        return true;
    }

    /// <summary>
    /// A visit to an object that raises luck, <c>ActionToGoodLuckObject()</c> (<c>heroes_action.cpp:1109</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1109-1160</c>。<c>FOUNTAIN</c>、<c>FAERIE_RING</c>、<c>IDOL</c>、<c>MERMAID</c>
    /// 各 +1 运气，同样只登记访问，运气由 <see cref="Hero.GetLuck"/> 重算。
    /// </para>
    /// <para>
    /// 「下一场战斗」的含义由清理时机表达：这批对象属于 <c>isBattleLife</c>，打完一仗
    /// （<see cref="Hero.ActionAfterBattle"/>）记录就被删掉，运气随之回到基准值。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object visited; only the four luck objects do anything.</param>
    /// <param name="tileIndex">The tile the object stands on.</param>
    /// <returns><see langword="true"/> when the visit counted.</returns>
    public bool ActionToGoodLuckObject(Mp2ObjectType objectType, int tileIndex)
    {
        if (!IsGoodLuckObject(objectType) || _hero.IsObjectTypeVisited(objectType))
        {
            return false;
        }

        _hero.SetVisited(tileIndex, objectType);

        return true;
    }

    /// <summary>
    /// Picking up a loose artifact, <c>ActionToArtifact()</c> (<c>heroes_action.cpp:1611</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1611-1700</c>。开头的三道门按顺序挡人：<b>已有法术书</b>不能再拿第二本
    /// （魔法书不算普通神器）、<b>背包满了</b>、然后才看守卫条件（<see cref="Tile.GetArtifactCaptureCondition"/>）。
    /// 前两道<b>不动瓦片</b>——宝物留在原地；这与「付费神器」不同，后者只是等人带钱回来。
    /// </para>
    /// <para>
    /// 端口只闭合「无条件 / 卷轴」这一条：<see cref="ArtifactCaptureCondition.NoConditions"/> 与
    /// <see cref="ArtifactCaptureCondition.ContainsSpell"/> 直接取走。三种付费条件返回
    /// <see cref="ArtifactPickupOutcome.NeedsPayment"/>，把「要不要付、付完再来」交给调用方——参考在那里还要
    /// 拼一句金币/资源文案并等玩家点「是」。技能门槛（智慧/领导）、战斗守卫（50 强盗、精灵、泰坦……）与
    /// 阵营限制返回 <see cref="ArtifactPickupOutcome.NotSupported"/>，都是还没搬的前置。
    /// </para>
    /// <para>
    /// 与资源对象一样，取走靠的是「<see cref="Tile.ResetObjectMetadata"/> 把瓦片清空」而不是查名册，所以第二次
    /// 走过来读到 <see cref="ArtifactType.Unknown"/> 就什么也不做。登记王国名册（参考的
    /// <c>setVisitedForAllies</c>）照旧只为界面与 AI。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the artifact lies on.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What happened to the artifact; see <see cref="ArtifactPickupOutcome"/>.</returns>
    public ArtifactPickupOutcome ActionToArtifact(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        ArtifactType artifact = tile.GetArtifactFromTile(world);

        if (!ArtifactTypes.IsValid(artifact))
        {
            return ArtifactPickupOutcome.None;
        }

        if (artifact == ArtifactType.MagicBook && _hero.HasSpellBook)
        {
            return ArtifactPickupOutcome.AlreadyHasSpellBook;
        }

        if (_hero.BagArtifacts.IsFull)
        {
            return ArtifactPickupOutcome.BagFull;
        }

        switch (tile.GetArtifactCaptureCondition(world))
        {
            case ArtifactCaptureCondition.NoConditions:
            case ArtifactCaptureCondition.ContainsSpell:
                break;

            case ArtifactCaptureCondition.Pay2000Gold:
            case ArtifactCaptureCondition.Pay2500GoldAnd3Resources:
            case ArtifactCaptureCondition.Pay3000GoldAnd5Resources:
                return ArtifactPickupOutcome.NeedsPayment;

            default:
                return ArtifactPickupOutcome.NotSupported;
        }

        if (!_hero.BagArtifacts.Push(artifact))
        {
            return ArtifactPickupOutcome.BagFull;
        }

        tile.ResetObjectMetadata();
        Kingdom.SetVisited(tileIndex, tile.GetNonActionObjectType(world));

        return ArtifactPickupOutcome.Picked;
    }

    /// <summary>
    /// Opening a treasure chest, <c>ActionToTreasureChest()</c> (<c>heroes_action.cpp:1795</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1795-1910</c>。一个箱子里可能有钱、有神器、也可能两样都有，于是分成三块：
    /// <b>海上</b>（<see cref="Tile.IsWater"/>）只能拿钱或「钱+神器」；<b>陆地有钱</b>时在金币与经验之间二选一；
    /// <b>陆地没钱</b>时只剩神器或空箱。空箱也要清元数据——参考的 <c>resetObjectMetadata</c> 写在函数最末尾。
    /// </para>
    /// <para>
    /// 经验是 <c>gold &gt; 500 ? gold - 500 : 500</c>：拿得越多，换成经验越亏，最低保底 500。这条式子不是在算
    /// 「经验价值」，而是原版用来压住高额宝箱的收益，照抄。
    /// </para>
    /// <para>
    /// <b>背包满</b>时参考把宝物折成金币（<see cref="ArtifactGold.InsteadOf"/>），并且是<b>覆盖</b>而不是叠加——
    /// 原箱里的金币一并被这 1000 换掉，看着像 bug，其实是原版行为。
    /// </para>
    /// <para>
    /// 「拿钱还是拿经验」在参考里是玩家对话框（<c>Dialog::SelectGoldOrExp</c>），AI 侧则是一套按角色与国库算的
    /// 概率。端口把决定交给 <paramref name="keepGold"/>：参数为金币与经验，返回是否拿金币；<see langword="null"/>
    /// 表示拿金币。AI 的概率模型等 AI 落地再接。
    /// </para>
    /// <para>
    /// 与风车一类不同，宝箱<b>不进访问名册</b>：参考的 <c>ActionToTreasureChest</c> 只清元数据。箱子开完就没了，
    /// 「开过没有」靠瓦片空不空来答。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the chest lies on.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <param name="keepGold">Decides between the gold and the experience; <see langword="null"/> takes the gold.</param>
    /// <returns>What the chest paid; see <see cref="TreasureChestReward"/>.</returns>
    public TreasureChestReward ActionToTreasureChest(int tileIndex, World world, Func<uint, uint, bool>? keepGold = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Mp2ObjectType objectType = tile.GetNonActionObjectType(world);
        ArtifactType artifact = tile.GetArtifactFromTile(world);
        uint gold = (uint)tile.GetFundsFromTile(world).Gold;
        bool artifactIsValid = ArtifactTypes.IsValid(artifact);
        bool bagIsFull = _hero.BagArtifacts.IsFull;

        TreasureChestReward reward;

        if (tile.IsWater)
        {
            if (gold == 0)
            {
                reward = default;
            }
            else if (artifactIsValid && !bagIsFull)
            {
                reward = new TreasureChestReward(gold, 0, artifact);
            }
            else
            {
                // A full bag on water: the artifact is replaced by gold, and the chest's own gold goes with it.
                reward = new TreasureChestReward(
                    artifactIsValid ? ArtifactGold.InsteadOf(objectType) : gold,
                    0,
                    ArtifactType.Unknown);
            }
        }
        else if (gold > 0 || (artifactIsValid && bagIsFull))
        {
            if (artifactIsValid && bagIsFull)
            {
                gold = ArtifactGold.InsteadOf(objectType);
            }

            uint experience = gold > 500 ? gold - 500 : 500;
            bool takeGold = keepGold?.Invoke(gold, experience) ?? true;

            reward = takeGold
                ? new TreasureChestReward(gold, 0, ArtifactType.Unknown)
                : new TreasureChestReward(0, experience, ArtifactType.Unknown);
        }
        else if (artifactIsValid)
        {
            reward = new TreasureChestReward(0, 0, artifact);
        }
        else
        {
            reward = default;
        }

        if (reward.Gold > 0)
        {
            Kingdom.AddFundsResource(new Funds(Resource.Gold, reward.Gold));
        }

        if (reward.Experience > 0)
        {
            _hero.IncreaseExperience(reward.Experience, world.MapSeed);
        }

        if (reward.Artifact != ArtifactType.Unknown)
        {
            if (!_hero.BagArtifacts.Push(reward.Artifact))
            {
                Debug.Assert(false, "The bag was checked as having room for the artifact.");
            }
        }

        // The reference clears the metadata at the very end, whether the chest paid or not.
        tile.ResetObjectMetadata();

        return reward;
    }

    /// <summary>
    /// Picking up a loose resource, a campfire or a barrel, <c>ActionToPickupResource()</c>
    /// (<c>heroes_action.cpp:752</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:752-793</c>。三个对象共用一条路，差别只在文案：钱与资源都在瓦片的
    /// <c>metadata</c> 里（<see cref="Tile.GetFundsFromTile"/>），取完就 <see cref="Tile.ResetObjectMetadata"/>。
    /// 营火与桶的值不值得拿，由车道本身决定——营火/桶是「N 个某种资源 + N×100 金币」，散落资源就是
    /// 「某种资源 N 个」，这套换算写在车道读取里，动作层不再算一遍。
    /// </para>
    /// <para>
    /// <b>不进访问名册</b>：参考在这个函数里没有 <c>SetVisited</c>，对象靠元数据清零消失。所以「来过没有」的
    /// 答案永远在瓦片上，不在名册里。
    /// </para>
    /// <para>
    /// 参考的第四个分支是<b>漂流瓶</b>（<c>OBJ_BOTTLE</c>）：它不付钱，只弹一段留言。端口没有留言文本系统，
    /// 所以 <see cref="IsPickupResource"/> 不收它，调用方该走「读留言」那条路（尚未实现）。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile holding the resource.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What the tile paid; an empty bag when the tile holds nothing.</returns>
    public Funds ActionToPickupResource(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);

        if (!IsPickupResource(tile.GetNonActionObjectType(world)))
        {
            Debug.Assert(false, "The tile does not hold a resource, a campfire or a barrel.");

            return default;
        }

        Funds funds = tile.GetFundsFromTile(world);

        if (funds.GetValidItemsCount() > 0)
        {
            Kingdom.AddFundsResource(funds);
        }

        tile.ResetObjectMetadata();

        return funds;
    }

    /// <summary>Whether <paramref name="objectType"/> goes through <see cref="ActionToPickupResource"/>.</summary>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> for a loose resource, a campfire and a barrel.</returns>
    public static bool IsPickupResource(Mp2ObjectType objectType) =>
        objectType is Mp2ObjectType.Resource or Mp2ObjectType.Campfire or Mp2ObjectType.Barrel;

    /// <summary>
    /// Searching a skeleton, <c>ActionToSkeleton()</c> (<c>heroes_action.cpp:866</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:866-909</c>。骷髅只有<b>神器</b>可拿：<see cref="Tile.DoesTileContainValuableItems"/>
    /// 问的就是「神器车道空不空」。背包满时折成金币（<see cref="ArtifactGold.InsteadOf"/>，骷髅是 1000），
    /// 这与宝箱、沉船幸存者一致。
    /// </para>
    /// <para>
    /// 名册登记在<b>所有分支之外</b>：空骷髅也记一次访问，第二次来靠神器车道为空判定。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the remains lie on.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What was found; see <see cref="ObjectPickupReward"/>.</returns>
    public ObjectPickupReward ActionToSkeleton(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Mp2ObjectType objectType = tile.GetNonActionObjectType(world);
        ObjectPickupReward reward = default;

        if (tile.DoesTileContainValuableItems(world))
        {
            if (_hero.BagArtifacts.IsFull)
            {
                uint gold = ArtifactGold.InsteadOf(objectType);

                if (gold > 0)
                {
                    Funds funds = new Funds(Resource.Gold, gold);
                    Kingdom.AddFundsResource(funds);
                    reward = new ObjectPickupReward(funds, ArtifactType.Unknown);
                }
            }
            else
            {
                ArtifactType artifact = tile.GetArtifactFromTile(world);

                if (artifact != ArtifactType.Unknown && _hero.BagArtifacts.Push(artifact))
                {
                    reward = new ObjectPickupReward(default, artifact);
                }
            }

            tile.ResetObjectMetadata();
        }

        Kingdom.SetVisited(tileIndex, objectType);

        return reward;
    }

    /// <summary>
    /// Searching a wagon, <c>ActionToWagon()</c> (<c>heroes_action.cpp:911</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:911-962</c>。货车有两个车道：<c>metadata[0]</c> 是神器、<c>metadata[1]</c> 是
    /// 资源类型、<c>metadata[2]</c> 是数量。所以<b>先看神器再看资源</b>：神器车道非空就只给神器，资源留着不算。
    /// </para>
    /// <para>
    /// <b>背包满时货车不给金币</b>——它只弹一句「别人先找到了，车厢是空的」，然后照样清空瓦片。这与骷髅、宝箱、
    /// 沉船幸存者的折金待遇不同，是原版的不一致，照抄。结果就是：满包开货车等于白扔一件神器，<b>别在港口满包</b>。
    /// </para>
    /// <para>
    /// 名册登记同样在分支之外。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the wagon stands on.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What was found; see <see cref="ObjectPickupReward"/>.</returns>
    public ObjectPickupReward ActionToWagon(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Mp2ObjectType objectType = tile.GetNonActionObjectType(world);
        ObjectPickupReward reward = default;

        if (tile.DoesTileContainValuableItems(world))
        {
            ArtifactType artifact = tile.GetArtifactFromTile(world);

            if (artifact != ArtifactType.Unknown)
            {
                // No gold conversion here: a full bag simply gets nothing and the wagon is emptied anyway.
                if (!_hero.BagArtifacts.IsFull && _hero.BagArtifacts.Push(artifact))
                {
                    reward = new ObjectPickupReward(default, artifact);
                }
            }
            else
            {
                Funds funds = tile.GetFundsFromTile(world);

                if (funds.GetValidItemsCount() > 0)
                {
                    Kingdom.AddFundsResource(funds);
                    reward = new ObjectPickupReward(funds, ArtifactType.Unknown);
                }
            }

            tile.ResetObjectMetadata();
        }

        Kingdom.SetVisited(tileIndex, objectType);

        return reward;
    }

    /// <summary>
    /// Searching flotsam, <c>ActionToFlotSam()</c> (<c>heroes_action.cpp:964</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:964-993</c>。浮木只有资源：<c>metadata[0]</c> 是木材、<c>metadata[1]</c> 是金币，
    /// 两样都可能为 0，于是 <see cref="Tile.GetFundsFromTile"/> 直接答一个「木材 + 金币」的袋子。
    /// </para>
    /// <para>
    /// 与营火/桶一样<b>不登记名册</b>，清元数据即消失。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the flotsam floats on.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What was found; an empty bag for an already-searched tile.</returns>
    public Funds ActionToFlotsam(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Funds funds = tile.GetFundsFromTile(world);

        if (funds.GetValidItemsCount() > 0)
        {
            Kingdom.AddFundsResource(funds);
        }

        tile.ResetObjectMetadata();

        return funds;
    }

    /// <summary>
    /// Rescuing a shipwreck survivor, <c>ActionToShipwreckSurvivor()</c> (<c>heroes_action.cpp:1570</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1570-1609</c>。两句话：背包没满就收下他送的神器，满了就折成 1000 金币
    /// （<see cref="ArtifactGold.InsteadOf"/>）。<b>没有「已经救过」的分支</b>——一个人只能救一次，靠的是清元数据。
    /// </para>
    /// <para>
    /// 与骷髅不同，这里<b>无条件清元数据</b>（参考的 <c>resetObjectMetadata</c> 在 if/else 之外），哪怕神器车道本来
    /// 就是空的；也不登记名册。两处都是因为它没有「空着但仍要记一笔」的情形。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the survivor clings to.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What the survivor gave; see <see cref="ObjectPickupReward"/>.</returns>
    public ObjectPickupReward ActionToShipwreckSurvivor(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        ObjectPickupReward reward;

        if (_hero.BagArtifacts.IsFull)
        {
            Funds funds = new Funds(Resource.Gold, ArtifactGold.InsteadOf(tile.GetNonActionObjectType(world)));

            if (funds.Gold > 0)
            {
                Kingdom.AddFundsResource(funds);
                reward = new ObjectPickupReward(funds, ArtifactType.Unknown);
            }
            else
            {
                reward = default;
            }
        }
        else
        {
            ArtifactType artifact = tile.GetArtifactFromTile(world);

            reward = artifact != ArtifactType.Unknown && _hero.BagArtifacts.Push(artifact)
                ? new ObjectPickupReward(default, artifact)
                : default;
        }

        tile.ResetObjectMetadata();

        return reward;
    }

    /// <summary>
    /// Breaking into the jail, <c>ActionToJail()</c> (<c>heroes_action.cpp:3402</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3402-3434</c>。两支：王国还有名额就放人，满了就说一句「可惜」走开。
    /// </para>
    /// <para>
    /// <b>顺序是先看名额再看牢里有没有人</b>：参考的 <c>AllowRecruitHero</c> 在函件开头就问，所以满编的王国
    /// 连「这块地有没有囚犯」都不查。照抄，别把两个判断调个个儿——那样满编国王会先看到「牢里没人」的文案。
    /// </para>
    /// <para>
    /// <b>不查钱</b>：<c>AllowRecruitHero( false )</c>，囚犯不要钱。
    /// </para>
    /// <para>
    /// <b>不清元数据、不记名册</b>：参考里没有这两步。监狱本身没有 <c>metadata</c>，而「有没有囚犯」由
    /// <see cref="World.FromJailHeroes"/> 按 <see cref="HeroModes.Jail"/> 模式位回答——放出来的人已经不是囚犯了，
    /// 再来一次自然无人可放。监狱作为物件仍然留在瓦片上（英雄站上去时被
    /// <see cref="Tile.SetHero"/> 寄存起来），空牢就这么一直立在那儿。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the jail stands on.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What happened; see <see cref="JailOutcome"/>.</returns>
    public JailOutcome ActionToJail(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!Kingdom.AllowRecruitHero(false))
        {
            return JailOutcome.NoSeat;
        }

        Hero? prisoner = world.FromJailHeroes(tileIndex);

        if (prisoner is null)
        {
            return JailOutcome.NoPrisoner;
        }

        if (!prisoner.Recruit(Kingdom, tileIndex, world))
        {
            // 名额刚查过，唯一能走到这里的情形是这位英雄已经有主人了——地图数据的问题。
            Debug.Assert(false, "A jailed hero refused to join the kingdom.");

            return JailOutcome.None;
        }

        return JailOutcome.Released;
    }

    /// <summary>
    /// A visit to the gazebo, <c>ActionToExperienceObject()</c> (<c>heroes_action.cpp:1521</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1521-1540</c>。函数名叫「经验对象」，但 dispatch 里只有凉亭一支
    /// （<c>case MP2::OBJ_GAZEBO</c>，<c>heroes_action.cpp:3934</c>），其它对象进来会断言，所以端口只做凉亭。
    /// </para>
    /// <para>
    /// 与喷泉一类一次性建筑一样记<b>瓦片级</b>本地名册，不是「同类型只算一次」：地图上可以有多个凉亭，
    /// 每个都各给一次 1000 点。加经验走 <see cref="Hero.IncreaseExperience"/>，会连带升级与抽技能。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile of the gazebo.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns><see langword="false"/> when the hero has already been here.</returns>
    public bool ActionToExperienceObject(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);

        if (_hero.IsVisited(tile))
        {
            return false;
        }

        _hero.SetVisited(tileIndex, Mp2ObjectType.Gazebo);
        Kingdom.SetVisited(tileIndex, Mp2ObjectType.Gazebo);
        _hero.IncreaseExperience(GazeboExperience, world.MapSeed);

        return true;
    }

    /// <summary>
    /// A visit to the tree of knowledge, <c>ActionToTreeKnowledge()</c> (<c>heroes_action.cpp:2946</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2946-3026</c>。<b>给的经验正好是「当前等级到下一级还差多少」</b>
    /// （<c>GetExperienceFromLevel( level ) - GetExperienceFromLevel( level - 1 )</c>），不是固定值，也不是
    /// 一半——所以它总能把英雄推上一级，且只推一级。
    /// </para>
    /// <para>
    /// 两条名册各写一次，顺序也不能换：<b>先</b>不论付不付得起都登记<b>王国</b>名册（参考的
    /// <c>SetVisited( dst_index, Visit::GLOBAL )</c>），<b>再</b>在真的加经验时登记<b>英雄本地</b>名册。
    /// 于是「钱不够」的英雄下次还能再来，而「已经学过」的这一次只看本地名册。
    /// </para>
    /// <para>
    /// 参考的免费树会先问一句「要不要」，还专门注了一句「原作没有拒绝的可能，这是我们加的改进」；端口把
    /// 「问」交给界面，这里视为接受。付费树付不起时只返回 <see cref="TreeKnowledgeOutcome.CannotPay"/>，
    /// 不扣钱、不加经验。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile of the tree.</param>
    /// <param name="world">The world holding the tile and the seed of the level-up draws.</param>
    /// <returns>What the visit did; see <see cref="TreeKnowledgeOutcome"/>.</returns>
    public TreeKnowledgeOutcome ActionToTreeKnowledge(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Mp2ObjectType objectType = tile.GetNonActionObjectType(world);

        if (_hero.IsVisited(tileIndex, objectType))
        {
            return TreeKnowledgeOutcome.None;
        }

        Funds payment = tile.GetTreeOfKnowledgeRequirement(world);
        bool free = payment.GetValidItemsCount() == 0;

        // The kingdom learns about the trip even when the hero cannot pay; only the local book waits for the lesson.
        Kingdom.SetVisited(tileIndex, objectType);

        if (!free && !Kingdom.AllowPayment(payment))
        {
            return TreeKnowledgeOutcome.CannotPay;
        }

        if (!free)
        {
            Kingdom.OddFundsResource(payment);
        }

        _hero.SetVisited(tileIndex, objectType);
        _hero.IncreaseExperience(ExperienceOfCurrentLevel(_hero.Level), world.MapSeed);

        return TreeKnowledgeOutcome.Learned;
    }

    /// <summary>
    /// A visit to a shrine, <c>ActionToShrine()</c> (<c>heroes_action.cpp:995</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:995-1056</c>。四道门，顺序就是参考的顺序：<b>没有法术书</b>（什么都记不下）、
    /// <b>三级法术但没有智慧术</b>（听不懂）、<b>书里已经有了</b>（按 <c>skipBag</c> 只查书，不查卷轴）、
    /// 否则抄进书里。
    /// </para>
    /// <para>
    /// <b>无论哪一道门，结束时都登记王国名册</b>（参考的 <c>SetVisited( dst_index, Visit::GLOBAL )</c> 在
    /// 所有分支之外）。这和知识树不同：神殿不会因为「这次没学到」而留到下次——它的名册只服务 AI 与界面提示，
    /// 人类英雄再来一次照样会得到同一段对话，判定靠「书里有没有」。
    /// </para>
    /// <para>
    /// 三个圆环的对话在参考里按法术等级分成三套，端口不生成文本，所以只要等级能算出来就足够；法术本身从
    /// <see cref="Tile.GetSpellFromTile"/> 读。金字塔也能读出法术，但它走的是带守卫的
    /// <c>ActionToPyramid</c>（<c>heroes_action.cpp:3902</c>），不进这里。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile of the shrine.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What the shrine did; see <see cref="ShrineOutcome"/>.</returns>
    public ShrineOutcome ActionToShrine(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Spell spell = tile.GetSpellFromTile(world);

        if (spell.Type == SpellType.None)
        {
            return ShrineOutcome.None;
        }

        ShrineOutcome outcome;

        if (!_hero.HasSpellBook)
        {
            outcome = ShrineOutcome.NoSpellBook;
        }
        else if (spell.Level() == 3 && !_hero.HasSecondarySkill(SecondarySkill.Wisdom))
        {
            outcome = ShrineOutcome.TooHard;
        }
        else if (_hero.HasSpell(spell, true))
        {
            outcome = ShrineOutcome.AlreadyKnown;
        }
        else
        {
            _hero.MagicBook.Append(spell);
            outcome = ShrineOutcome.Learned;
        }

        // The kingdom learns about the trip even when nothing was learned: the reference sets the flag outside every branch.
        Kingdom.SetVisited(tileIndex, tile.GetNonActionObjectType(world));

        return outcome;
    }

    /// <summary>
    /// A visit to a witch's hut, <c>ActionToWitchsHut()</c> (<c>heroes_action.cpp:1058</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1058-1107</c>。三道门：<b>满八个技能</b>（没地方记）、<b>已经会了</b>
    /// （不重复教）、否则按基础等级学会。和神殿一样，无论走哪道门都登记王国名册。
    /// </para>
    /// <para>
    /// 参考在学会的是侦察术时会顺手 <c>Scout</c>+<c>ScoutRadar</c> 开一片雾；端口还没有迷雾，这一步留给
    /// 迷雾系统，技能本身照常记下。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile of the hut.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>What the hut did; see <see cref="WitchsHutOutcome"/>.</returns>
    public WitchsHutOutcome ActionToWitchsHut(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        SecondarySkillLevel skill = tile.GetSecondarySkillFromWitchsHut(world);

        WitchsHutOutcome outcome;

        if (!skill.IsValid)
        {
            outcome = WitchsHutOutcome.Broken;
        }
        else if (_hero.HasMaxSecondarySkill())
        {
            outcome = WitchsHutOutcome.NoRoom;
        }
        else if (_hero.HasSecondarySkill(skill.Skill))
        {
            outcome = WitchsHutOutcome.AlreadyKnown;
        }
        else
        {
            _hero.LearnSkill(skill);
            outcome = WitchsHutOutcome.Learned;
        }

        Kingdom.SetVisited(tileIndex, tile.GetNonActionObjectType(world));

        return outcome;
    }

    /// <summary>What a gazebo pays, <c>ActionToExperienceObject()</c> (<c>heroes_action.cpp:1529</c>).</summary>
    private const uint GazeboExperience = 1000;

    /// <summary>
    /// The experience one level costs at <paramref name="level"/>, the tree of knowledge's gift.
    /// </summary>
    /// <param name="level">The current level of the hero, one-based.</param>
    /// <returns>
    /// <c>GetExperienceFromLevel( level ) - GetExperienceFromLevel( level - 1 )</c>, i.e. the length of the
    /// current level, not the whole road to it.
    /// </returns>
    private static uint ExperienceOfCurrentLevel(int level) =>
        HeroExperience.FromLevel(level) - HeroExperience.FromLevel(level - 1);

    /// <summary>
    /// Facing a magical barrier, <c>ActionToBarrier()</c> (<c>heroes_action.cpp:3650</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3650-3679</c>。这一支<b>什么都不改</b>：只是查
    /// <c>kingdom.isTravellerTentVisited( getBarrierColorFromTile( tile ) )</c> 然后弹两句不同的话。字面上
    /// 「念对咒语屏障就消散」，实际屏障的放行发生在移动判定里（同一颜色的帐篷进过，就能穿过）；淡出动画也只是表现。
    /// </para>
    /// <para>
    /// 端口把这两句话折成一个谓词：返回 <see langword="true"/> 表示这个英雄的王国能过去，界面层再决定显示哪一句。
    /// 屏障本身不在名册里，也不属于三条寿命中的任何一条——帐篷的解锁才是唯一的开关。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the barrier stands on (the hero stands in front of it).</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns><see langword="true"/> when the matching tent has already been visited.</returns>
    public bool ActionToBarrier(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int barrierColor = world.GetTile(tileIndex).GetBarrierColorFromTile(world);

        return Kingdom.IsTravellerTentVisited(barrierColor);
    }

    /// <summary>
    /// Stepping into the arena, <c>ActionToArena()</c> (<c>heroes_action.cpp:3353</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3353-3366</c>。竞技场只给一次：<c>isObjectTypeVisited</c> 命中就「守卫把你挡回去」，
    /// 否则记**本地**名册、加一点主技能。它属于「永久」对象（三条寿命清理都不含），这一张图里每位英雄只能来一次。
    /// </para>
    /// <para>
    /// 参考在这里弹出 <c>Dialog::SelectSkillFromArena()</c> 让玩家四选一；端口把选择提到参数上
    /// （<paramref name="skill"/>），由界面层先问再调用——这样这一支保持纯状态变更，便于测试。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the arena stands on.</param>
    /// <param name="skill">Which primary skill the player picked.</param>
    /// <returns><see langword="false"/> when the guards turn the hero away.</returns>
    public bool ActionToArena(int tileIndex, PrimarySkill skill)
    {
        if (_hero.IsObjectTypeVisited(Mp2ObjectType.Arena))
        {
            return false;
        }

        _hero.SetVisited(tileIndex, Mp2ObjectType.Arena);
        _hero.IncreasePrimarySkill(skill);

        return true;
    }

    /// <summary>
    /// A visit to the black cat, <c>actionToBlackCat()</c> (<c>heroes_action.cpp:3702</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3702-3738</c>。黑猫是一族里唯一「同时改两个表」的对象：<b>+3 士气、-2 运气</b>
    /// （<see cref="GameStatic.GetObjectMoraleEffect"/> 与 <see cref="GameStatic.GetObjectLuckEffect"/>）。
    /// 它记的是英雄<b>本地</b>名册（参考的 <c>SetVisited( dst_index )</c> 用默认的 <c>Visit::LOCAL</c>），
    /// 而且属于「永久」寿命——三条清理谓词都不含它，所以除非主动清名册，这座城市只值一次。
    /// </para>
    /// <para>
    /// 第二次再来只提示「上次那一口还记着」：端口返回 <see langword="false"/>，数值不动。士气与运气都不在这里
    /// 加减，仍由 <see cref="Hero.GetMorale"/> / <see cref="Hero.GetLuck"/> 按访问类型重算。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the cat sits on.</param>
    /// <returns><see langword="true"/> when the visit counted.</returns>
    public bool ActionToBlackCat(int tileIndex)
    {
        if (_hero.IsObjectTypeVisited(Mp2ObjectType.BlackCat))
        {
            return false;
        }

        _hero.SetVisited(tileIndex, Mp2ObjectType.BlackCat);

        return true;
    }

    /// <summary>
    /// A visit to Xanadu, <c>ActionToXanadu()</c> (<c>heroes_action.cpp:2577</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2577-2617</c>。三道门：<b>来过</b>（管家不认同一个学生两次）、<b>不够格</b>
    /// （参见 <see cref="GameStatic.IsHeroWorthyToVisitXanadu"/>）、否则四主技能各加一点。
    /// </para>
    /// <para>
    /// <b>只有真正受训才登记名册</b>：不够格的拜访不算一次访问，下次够格了还能来。这与神殿、女巫小屋「怎么都记」
    /// 相反，和知识树「先记王国」的差别在于这里记的是<b>英雄本地</b>名册（参考的 <c>hero.SetVisited( dst_index )</c>
    /// 是 LOCAL），因为 Xanadu 是「同一个学生只教一次」。
    /// </para>
    /// <para>
    /// 加点是固定的四项全加，不抽签也不看职业。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile of the house.</param>
    /// <returns>What the butler did; see <see cref="XanaduOutcome"/>.</returns>
    public XanaduOutcome ActionToXanadu(int tileIndex)
    {
        if (_hero.IsVisited(tileIndex, Mp2ObjectType.Xanadu))
        {
            return XanaduOutcome.AlreadyVisited;
        }

        if (!GameStatic.IsHeroWorthyToVisitXanadu(_hero))
        {
            return XanaduOutcome.NotWorthy;
        }

        _hero.IncreasePrimarySkill(PrimarySkill.Attack);
        _hero.IncreasePrimarySkill(PrimarySkill.Defense);
        _hero.IncreasePrimarySkill(PrimarySkill.Knowledge);
        _hero.IncreasePrimarySkill(PrimarySkill.Power);
        _hero.SetVisited(tileIndex, Mp2ObjectType.Xanadu);

        return XanaduOutcome.Trained;
    }

    /// <summary>
    /// A visit to a windmill, a water wheel, a lean-to or a magic garden, <c>ActionToObjectResource()</c>
    /// (<c>heroes_action.cpp:795</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:795-867</c>。四个对象共用一个函数，因为它们的差别只有消息文案：钱与资源都存在
    /// 瓦片的 <c>metadata</c> 里（<see cref="Tile.GetFundsFromTile"/>），取完就
    /// <see cref="Tile.ResetObjectMetadata"/> 清零。所以「本周拿过了」不是查访问名册，而是**看瓦片空不空**。
    /// </para>
    /// <para>
    /// 参考结尾还有一句 <c>hero.setVisitedForAllies( dst_index )</c>：把这一格登记进<b>所有盟友</b>的王国名册，
    /// 让同一方的其他英雄与 AI 知道这格已经空了。端口没有 <c>Players</c> 的盟友表，先只登记自己所属的王国——
    /// 在单方场景里两者等价，多方盟友的差别等 <c>Players</c> 落地再补。登记用的是瓦片上的基础对象值
    /// （参考的 <c>tile.getMainObjectType( false )</c> 再摘掉动作位），不是调用方传进来的值——名册讲的是非动作值，
    /// 见 <see cref="Tile.GetNonActionObjectType"/>（ADR-133）。
    /// </para>
    /// <para>
    /// 无论拿没拿到（包括「这周已经空了」），元数据都会被清、名册都会登记——参考的第二句写在 <c>if</c> 外面。
    /// 每周把风车/水车重新填满的是 <c>Maps::updateObjectInfoTile</c>（<c>maps_tiles_helper.cpp:1655</c>），
    /// 那属于「周」的恢复流程，端口还没接上，所以本方法的第二次访问会一直答「没有资源」。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object visited; only the four resource objects do anything.</param>
    /// <param name="tileIndex">The tile the object stands on.</param>
    /// <param name="world">The world holding the tile; needed to read through a hero standing on it.</param>
    /// <returns>What the tile paid; an empty bag when the object had nothing left.</returns>
    public Funds ActionToObjectResource(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!IsResourceObject(objectType))
        {
            return default;
        }

        Tile tile = world.GetTile(tileIndex);
        Funds funds = tile.GetFundsFromTile(world);

        if (funds.GetValidItemsCount() > 0)
        {
            Kingdom.AddFundsResource(funds);
        }

        tile.ResetObjectMetadata();
        Kingdom.SetVisited(tileIndex, tile.GetNonActionObjectType(world));

        return funds;
    }

    /// <summary>Whether <paramref name="objectType"/> goes through <see cref="ActionToObjectResource"/>.</summary>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> for the windmill, the water wheel, the lean-to and the magic garden.</returns>
    public static bool IsResourceObject(Mp2ObjectType objectType) =>
        objectType is Mp2ObjectType.Windmill or Mp2ObjectType.WaterWheel or Mp2ObjectType.LeanTo or Mp2ObjectType.MagicGarden;

    /// <summary>
    /// Entering a traveller's tent, <c>ActionToTravellersTent()</c> (<c>heroes_action.cpp:3681</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3681-3697</c>。两步：从瓦片读出帐篷的颜色（<see cref="Tile.GetBarrierColorFromTile"/>），
    /// 在王国上点亮那一色位（<see cref="Kingdom.MarkTravellerTentVisited"/>）。帐篷是<b>永久</b>解锁，
    /// 不进访问名册，日/周清理都不碰它。
    /// </para>
    /// <para>
    /// 参考没有「已经来过」的分支——再来一次只是同一位置位。返回值是颜色，供界面层拼「蓝色帐篷」这类标题。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the tent stands on.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>The colour index of the tent, <c>0</c> blue … <c>5</c> purple.</returns>
    public int ActionToTravellersTent(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int tentColor = world.GetTile(tileIndex).GetBarrierColorFromTile(world);
        Kingdom.MarkTravellerTentVisited(tentColor);

        return tentColor;
    }

    /// <summary>
    /// A visit to the stables, <c>ActionToStables()</c> (<c>heroes_action.cpp:3313</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3313-3350</c>。两句独立：没来过就登记<b>本地</b>访问并加 400 移动点
    /// （<see cref="GameStatic.GetMovementPointBonus"/>，与「马厩给 500」的常见说法不同，参考里它和水坑共用
    /// 一个 <c>case</c>）；若队伍里有骑兵，再走一遍「免费升级骑兵」的流程（<see cref="ActionToUpgradeArmyObject"/>）。
    /// </para>
    /// <para>
    /// 两句的<b>记录</b>是分开的：访问记录只管加移动点，升级只看队伍里有没有骑兵——所以「来过一次、后来才招到
    /// 骑兵」的人回来照样能升级，只是拿不到第二次移动点。马厩属于一周寿命的对象，英雄
    /// <see cref="Hero.ActionNewWeek"/> 之后可以重来。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the stables stand on.</param>
    /// <returns>Whether the fresh horse was taken and what happened to the cavalry.</returns>
    public StablesResult ActionToStables(int tileIndex)
    {
        bool tookFreshHorse = false;

        if (!_hero.IsObjectTypeVisited(Mp2ObjectType.Stables))
        {
            _hero.SetVisited(tileIndex, Mp2ObjectType.Stables);
            _hero.IncreaseMovePoints(GameStatic.GetMovementPointBonus(Mp2ObjectType.Stables));
            tookFreshHorse = true;
        }

        ArmyUpgradeResult upgrade = _hero.Army.HasMonster(MonsterType.Cavalry)
            ? ActionToUpgradeArmyObject(Mp2ObjectType.Stables)
            : new ArmyUpgradeResult(ArmyUpgradeOutcome.NothingToUpgrade, []);

        return new StablesResult(tookFreshHorse, upgrade);
    }

    /// <summary>
    /// Looking at an obelisk, <c>ActionToObelisk()</c> (<c>heroes_action.cpp:2924</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2924-2946</c>。这一支本身不做事，全部落在
    /// <see cref="Kingdom.ActionObelisk"/>（ADR-121）：「这座石阵本王国看过没有」、全局登记、按
    /// 「已访问石阵数 / 地图石阵总数」重算寻宝图。
    /// </para>
    /// <para>
    /// 石阵总数由世界数（<see cref="World.CountObeliskOnMaps"/>），王国不认识世界，与日期、周数一样提到参数上。
    /// 返回值 <see langword="false"/> 同时覆盖「已经看过」——参考在那一支只弹一句提示，端口把这条信息交回调用方。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the obelisk stands on.</param>
    /// <param name="world">The world holding the tile and the obelisk count.</param>
    /// <returns><see langword="true"/> when this obelisk opened a new piece of the puzzle.</returns>
    public bool ActionToObelisk(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return Kingdom.ActionObelisk(tileIndex, world.CountObeliskOnMaps());
    }

    /// <summary>Whether <paramref name="objectType"/> goes through <see cref="ActionToGoodMoraleObject"/>.</summary>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> for the buoy, the oasis, the watering hole and the temple.</returns>
    public static bool IsGoodMoraleObject(Mp2ObjectType objectType) =>
        objectType is Mp2ObjectType.Buoy or Mp2ObjectType.Oasis or Mp2ObjectType.WateringHole or Mp2ObjectType.Temple;

    /// <summary>
    /// Hearing the sirens, <c>ActionToSirens()</c> (<c>heroes_action.cpp:3366</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3366-3400</c>。一支「兵少了换经验」的对象：兵怎么少由
    /// <c>Army::ActionToSirens()</c>（端口 <see cref="FHeroes2.Game.Army.Army.ActionToSirens"/>）决定（30%、
    /// 至少 1 个、独苗不动），这里只管名册与经验。
    /// </para>
    /// <para>
    /// <b>只算一次</b>：参考用 <c>hero.SetVisited( dst_index )</c> 记<b>英雄级</b>名册，不是王国级——同一个英雄
    /// 听过一次就够了，换一个英雄来还得再听。<see cref="Hero.IsObjectTypeVisited"/> 不认瓦片，只认对象类型，
    /// 所以地图上有第二群塞伦也一样挡住。
    /// </para>
    /// <para>
    /// <b>淹死的人还是记一次名册</b>：参考的 <c>SetVisited</c> 在 <c>else</c> 分支末尾，兵太少、一个都没少时
    /// 也照样登记。所以 <see cref="SirensReward.IsEmpty"/> 为真但 <see cref="SirensReward.AlreadyVisited"/> 为假，
    /// 调用方据此说「你的小小军队挺住了」而不是「你来过了」。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the sirens sit on.</param>
    /// <param name="world">The world, for the map seed of the level-ups the experience may trigger.</param>
    /// <returns>What the visit cost and paid; see <see cref="SirensReward"/>.</returns>
    public SirensReward ActionToSirens(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_hero.IsObjectTypeVisited(Mp2ObjectType.Sirens))
        {
            return new SirensReward(0, true);
        }

        uint experience = _hero.Army.ActionToSirens();

        _hero.SetVisited(tileIndex, Mp2ObjectType.Sirens);

        if (experience > 0)
        {
            _hero.IncreaseExperience(experience, world.MapSeed);
        }

        return new SirensReward(experience, false);
    }

    /// <summary>
    /// Reading a sign, <c>ActionToSign()</c> (<c>heroes_action.cpp:1243</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1243-1251</c>，全部逻辑在 <c>showSignMessage( tileIndex )</c> 里：从
    /// <c>world.GetMapObject( tileIndex )</c> 取出 <c>MapSign</c>，把它的留言显示出来。它<b>不改任何状态</b>——
    /// 不记名册、不动元数据、不加经验，所以一块牌子可以反复读。
    /// </para>
    /// <para>
    /// 端口把它折成「取文本」：留言本来就在 <see cref="World.GetMapSign"/> 里（<c>world_loadmap.cpp</c> 的信息块
    /// 已经搬完），动作层只需要把人领到那儿。标题（「告示牌」）与弹窗是 UI 的事。
    /// </para>
    /// <para>
    /// 参考在 <c>sign == nullptr</c> 时 <c>assert</c>，端口返回 <see langword="null"/>：调用方本来就不该把英雄
    /// 领到没有牌子的瓦片上，但崩溃没有意义。
    /// </para>
    /// <para>
    /// <b>空牌子不会编内容</b>：原版给空留言的牌子随机抽一句话，端口还没有那张语言表，于是如实返回空字符串。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the sign stands on.</param>
    /// <param name="world">The world holding the sign.</param>
    /// <returns>The text of the sign, or <see langword="null"/> when the tile carries no sign.</returns>
    public string? ActionToSign(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return world.GetMapSign(tileIndex)?.Message.Text;
    }

    /// <summary>
    /// Climbing an observation tower, <c>ActionToObservationTower()</c> (<c>heroes_action.cpp:2521</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2521-2539</c>。整个函数只有三件事：弹一句固定文案、按
    /// <see cref="MapFog.ObservationTowerRevealDistance"/>（19 格）揭开以塔为中心的圆、把雷达的渲染区域挪到
    /// 那片圆上。
    /// </para>
    /// <para>
    /// <b>没有名册，也没有元数据</b>：塔可以反复爬，每次都会重新揭（第二次自然揭不出新东西）。参考也不清
    /// <c>metadata</c>，塔本身留在瓦片上。
    /// </para>
    /// <para>
    /// 端口把「揭」交给 <see cref="World.ClearFog"/>，返回值是这次真正被揭开的瓦片数。文案与雷达刷新是 UI 的
    /// 事——参考那句话是无条件显示的，所以调用方不该因为返回 0 就不吭声。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the tower stands on.</param>
    /// <param name="world">The world holding the tile and the fog.</param>
    /// <returns>Number of tiles the tower uncovered; <c>0</c> when the area was already visible.</returns>
    public int ActionToObservationTower(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return world.ClearFog(tileIndex, MapFog.ObservationTowerRevealDistance, _hero.Color);
    }

    /// <summary>
    /// Buying the maps of a retired captain, <c>ActionToMagellanMaps()</c> (<c>heroes_action.cpp:2759</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2759-2809</c>，四道门按顺序：
    /// <b>王国已经买过</b>（<c>isObjectTypeVisited( objectType, Visit::GLOBAL )</c>）→ 说一句「你已经把我所知的
    /// 海图都拿走了」；<b>钱不够</b>（<see cref="PaymentConditions.GetMagellansMapsPurchasePrice"/>，1000 金）→
    /// 船长叹气；<b>玩家点了否</b> → 什么也不发生；<b>点了是</b> → 揭海上所有瓦片、扣钱、记名册。
    /// </para>
    /// <para>
    /// <b>「买过了」是<b>王国级</b>的</b>：参考用 <c>Visit::GLOBAL</c> 加 <c>setVisitedForAllies</c>，所以同阵营
    /// 的任何一位英雄都不用再买。端口没有同盟，<see cref="Kingdom.IsVisited(Mp2ObjectType)"/> 就是那张王国名册，
    /// 按<b>类型</b>问而不是按瓦片问——地图上有第二个船长也照样挡住。
    /// </para>
    /// <para>
    /// <b>两段式</b>：参考在同一次调用里弹「是/否」再结算。端口把确认留给调用方，与
    /// <see cref="ArtifactPickupOutcome.NeedsPayment"/> 同一套做法——第一次带 <paramref name="confirmed"/> =
    /// <see langword="false"/> 进来只会拿到 <see cref="MagellansMapsOutcome.NeedsConfirmation"/>，玩家点了「是」
    /// 再带着 <see langword="true"/> 回来才真的扣钱开图。这样「没答应就扣钱」这种错误不可能发生。
    /// </para>
    /// <para>
    /// <b>顺序决定文案</b>：「买过了」必须排在「钱不够」前面，否则一个既买过又只剩几块钱的王国会被告知
    /// 「你钱不够」。参考就是这个顺序，端口照抄。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the captain sits on.</param>
    /// <param name="world">The world whose sea is revealed.</param>
    /// <param name="confirmed">Whether the player has already answered "yes" to the price.</param>
    /// <returns>What happened; see <see cref="MagellansMapsOutcome"/>.</returns>
    public MagellansMapsOutcome ActionToMagellanMaps(int tileIndex, World world, bool confirmed = false)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (Kingdom.IsVisited(Mp2ObjectType.MagellansMaps))
        {
            return MagellansMapsOutcome.AlreadyBought;
        }

        Funds payment = PaymentConditions.GetMagellansMapsPurchasePrice();

        if (!Kingdom.AllowPayment(payment))
        {
            return MagellansMapsOutcome.NotEnoughGold;
        }

        if (!confirmed)
        {
            return MagellansMapsOutcome.NeedsConfirmation;
        }

        world.ActionForMagellanMaps(_hero.Color);

        Kingdom.OddFundsResource(payment);
        Kingdom.SetVisited(tileIndex, Mp2ObjectType.MagellansMaps);

        return MagellansMapsOutcome.Bought;
    }

    /// <summary>
    /// Walking into the stone liths, <c>ActionToTeleports()</c> (<c>heroes_action.cpp:1940</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1940-1979</c>，除了音效与镜头，全部工作是三步：找出口、<see cref="Hero.Scout"/>
    /// 出口那一格、<see cref="Hero.MoveToDest"/> 把英雄挪过去，最后清空路线。出口由
    /// <see cref="World.NextTeleport"/> 挑——同一张精灵、同样的水陆，一对（或更多）立石里随一个。找不到时它把
    /// <b>入口本身</b>还回来，那就是「这对立石只剩一头」，参考打一句警告就收场，端口返回
    /// <see cref="TeleportOutcome.NoDestination"/>。
    /// </para>
    /// <para>
    /// <b>先 Scout 再落脚</b>：顺序不能反。参考踩上去的第一件事是让出口周围亮相，然后英雄才挪过去；反过来的话
    /// 那一帧的雾会按<b>旧</b>位置算。端口照抄这个顺序（<see cref="Hero.Scout"/> 的瓦片号本来就是调用方给的，
    /// 见 ADR-130）。
    /// </para>
    /// <para>
    /// <b>路线必须清掉</b>：传送是瞬间位移，脚下那条通往别处的路线已经没有意义，留着会让下一次走动把英雄从新位置
    /// 沿旧路线拉回去。参考在 <c>Move2Dest</c> 之后紧接着 <c>ResetPath()</c>，端口同样。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the liths stand on, the entrance.</param>
    /// <param name="world">The world holding the liths and the fog.</param>
    /// <returns>Whether the hero moved; see <see cref="TeleportOutcome"/>.</returns>
    public TeleportOutcome ActionToTeleports(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int destination = world.NextTeleport(tileIndex);

        if (destination == tileIndex)
        {
            return TeleportOutcome.NoDestination;
        }

        MoveThroughTeleport(world, destination);

        return TeleportOutcome.Moved;
    }

    /// <summary>
    /// Being swallowed by a whirlpool, <c>ActionToWhirlpools()</c> (<c>heroes_action.cpp:1981</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1981-2020</c>。位移与立石完全一样（找出口 → <see cref="Hero.Scout"/> →
    /// <see cref="Hero.MoveToDest"/> → 清路线，见 <see cref="ActionToTeleports"/>），多出来的是<b>沉兵</b>：
    /// <c>WhirlpoolTroopLoseEffect()</c>（<c>heroes_action.cpp:300</c>，端口 <see cref="LoseTroopsToWhirlpool"/>）。
    /// </para>
    /// <para>
    /// 沉兵的规则：最弱的那一叠兵，三分之一的概率被吞掉<b>一半血量</b>。注意是血量不是数量——剩余血量要按单兵血量
    /// <b>向上取整</b>折回「几只」（<see cref="Troop.GetCountFromHitPoints"/>，<c>monster.cpp:840</c>），所以一只
    /// 20 点血的兵受 10 点伤之后仍然是「一只」。两条豁免：军队只有一叠、且那叠只有一只时什么都不损失（漩涡不该
    /// 吞掉玩家唯一的兵）；投骰没中时也什么都不损失，但<b>照样传送</b>。
    /// </para>
    /// <para>
    /// <b>随机数怎么进</b>：参考用全局的 <c>Rand::Get( 1, 3 )</c>。端口有同样的 <see cref="Rand.Get(int, int)"/>，
    /// 但全局伪随机在测试里没法钉住，所以这里多收一个可选的 <see cref="Pcg32"/>，缺省用线程里那一份——测试传一个
    /// 固定种子就能确定地走到「命中」那一支。与 <see cref="Army.Army.Reset"/> 收 <c>generator</c>、
    /// <see cref="ActionToTreasureChest"/> 收 <c>keepGold</c> 是同一套做法：把「由谁决定」留一个口子。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the whirlpool is on, the entrance.</param>
    /// <param name="world">The world holding the whirlpool and the fog.</param>
    /// <param name="generator">Generator for the one-in-three roll; the thread generator when absent.</param>
    /// <returns>Whether the hero moved; see <see cref="TeleportOutcome"/>.</returns>
    public TeleportOutcome ActionToWhirlpools(int tileIndex, World world, Pcg32? generator = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        int destination = world.NextWhirlpool(tileIndex);

        if (destination == tileIndex)
        {
            return TeleportOutcome.NoDestination;
        }

        MoveThroughTeleport(world, destination);
        LoseTroopsToWhirlpool(generator);

        return TeleportOutcome.Moved;
    }

    /// <summary>
    /// The three state changes both teleport objects make, in the order of the reference: uncover the
    /// destination, put the hero on it, drop the route he was walking.
    /// </summary>
    /// <param name="world">The world the hero stands on.</param>
    /// <param name="destination">The tile he comes out on.</param>
    private void MoveThroughTeleport(World world, int destination)
    {
        _hero.Scout(world, destination);
        _hero.MoveToDest(destination, world);
        _hero.Path.Reset();
    }

    /// <summary>
    /// Drowning the share of the weakest stack that a whirlpool takes, <c>WhirlpoolTroopLoseEffect()</c>
    /// (<c>heroes_action.cpp:300</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes_action.cpp:300-340</c>。顺序与四个早退都在这里：没有兵、只有一叠独苗、投骰没中、
    /// 那一叠只剩一只（此时整叠消失而不是「剩零只」）。真正的减法作用在<b>血量</b>上，再把血量折回数量，
    /// 见 <see cref="Troop.GetCountFromHitPoints"/>。
    /// </remarks>
    /// <param name="generator">Generator for the one-in-three roll; the thread generator when absent.</param>
    private void LoseTroopsToWhirlpool(Pcg32? generator)
    {
        Troop? weakest = _hero.Army.GetWeakestTroop();

        if (weakest is null || weakest.Count == 0)
        {
            return;
        }

        if (_hero.Army.OccupiedSlotCount == 1 && weakest.Count == 1)
        {
            return;
        }

        if ((generator is null ? Rand.Get(1, 3) : Rand.GetWithGen(1, 3, generator)) != 1)
        {
            return;
        }

        if (weakest.Count == 1)
        {
            weakest.Reset();

            return;
        }

        uint hitPoints = weakest.GetHitPoints();

        weakest.Count = Troop.GetCountFromHitPoints(
            weakest.Monster,
            hitPoints - ((hitPoints * GameStatic.LostOnWhirlpoolPercent) / 100));
    }

    /// <summary>Whether <paramref name="objectType"/> goes through <see cref="ActionToGoodLuckObject"/>.</summary>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> for the fountain, the faerie ring, the idol and the mermaid.</returns>
    public static bool IsGoodLuckObject(Mp2ObjectType objectType) =>
        objectType is Mp2ObjectType.Fountain or Mp2ObjectType.FaerieRing or Mp2ObjectType.Idol or Mp2ObjectType.Mermaid;
}

/// <summary>
/// What a teleport object did with the hero, <see cref="HeroActions.ActionToTeleports"/> and
/// <see cref="HeroActions.ActionToWhirlpools"/>.
/// </summary>
/// <remarks>
/// <para>
/// 参考没有返回值：对象没有另一头时它打一句警告、放个音效然后 <c>return</c>，成功时也只是继续往下走。端口折成两态，
/// 调用方靠它决定「要不要把镜头挪到新位置」。
/// </para>
/// <para>
/// <b>去哪儿不用返回</b>：落点就是 <see cref="Hero.MapIndex"/>，多返回一个瓦片号只会让两个真相并存。沉兵同理，
/// 军队状态自己会说。
/// </para>
/// </remarks>
public enum TeleportOutcome
{
    /// <summary>
    /// The object has no other end — the liths stands alone, the whirlpool is the only one on the map — so the
    /// hero stayed where he was. The reference warns and returns as well.
    /// </summary>
    NoDestination = 0,

    /// <summary>The hero stands on the other end now, fog uncovered, route cleared.</summary>
    Moved,
}

/// <summary>
/// What <see cref="HeroActions.ActionToTreeKnowledge"/> did at the tree.
/// </summary>
/// <remarks>
/// 参考没有返回值——它靠弹窗把四种情况说给玩家听。端口折成这张表：<see cref="None"/> 是「树记得你来过」，
/// <see cref="Learned"/> 是「加了一级的经验」，<see cref="CannotPay"/> 是「钱不够，下次再来」。
/// </remarks>
public enum TreeKnowledgeOutcome
{
    /// <summary>The hero has already learned here; nothing changed.</summary>
    None = 0,

    /// <summary>The experience of one level was granted (and paid for, when the tree asks).</summary>
    Learned,

    /// <summary>The tree asks for a payment the treasury cannot cover; nothing was paid or granted.</summary>
    CannotPay,
}

/// <summary>
/// What <see cref="HeroActions.ActionToArtifact"/> did with the artifact on a tile.
/// </summary>
/// <remarks>
/// 参考 <c>ActionToArtifact()</c>（<c>heroes_action.cpp:1611</c>）是一整段带对话的流程，端口把它折成一张结果表：
/// 「拿到了」与五种「没拿到，原因是××」。<see cref="NeedsPayment"/> 与 <see cref="NotSupported"/> 都是留给
/// 调用方的缺口——前者等玩家掏钱后再来一次，后者等技能/战斗/阵营这些前置落地。
/// </remarks>
public enum ArtifactPickupOutcome
{
    /// <summary>The tile holds no artifact (already taken, or not an artifact object).</summary>
    None = 0,

    /// <summary>The artifact went into the bag and the tile was emptied.</summary>
    Picked,

    /// <summary>The bag has no free slot; the artifact stays on the tile.</summary>
    BagFull,

    /// <summary>The artifact is a magic book and the hero already owns one.</summary>
    AlreadyHasSpellBook,

    /// <summary>The artifact is guarded by a gold/resource price, which the caller has to collect.</summary>
    NeedsPayment,

    /// <summary>
    /// The artifact is guarded by a skill, a fight or an alignment — none of those checks exist in the port yet.
    /// </summary>
    NotSupported,
}

/// <summary>
/// What <see cref="HeroActions.ActionToShrine"/> did at the shrine.
/// </summary>
/// <remarks>
/// 参考没有返回值——它靠三套对话（按法术等级分）把结果说给玩家听。端口折成这张表：四道门各一个成员，
/// <see cref="None"/> 留给「这片瓦片上没有神殿」。
/// </remarks>
public enum ShrineOutcome
{
    /// <summary>The tile does not teach a spell; nothing was read and nothing was recorded.</summary>
    None = 0,

    /// <summary>The hero has no spell book, so there is nothing to write the spell into.</summary>
    NoSpellBook,

    /// <summary>The spell is of the third level and the hero has no wisdom; it cannot be understood.</summary>
    TooHard,

    /// <summary>The spell book already holds it (scrolls are not consulted).</summary>
    AlreadyKnown,

    /// <summary>The spell went into the book.</summary>
    Learned,
}

/// <summary>
/// What <see cref="HeroActions.ActionToWitchsHut"/> did at the hut.
/// </summary>
/// <remarks>
/// 参考三道门各一段对话；端口折成这张表。<see cref="None"/> 是「这片瓦片上没有小屋」，<see cref="Broken"/>
/// 是「有 hut 但车道是空的」（参考在那里断言）。
/// </remarks>
public enum WitchsHutOutcome
{
    /// <summary>The tile does not hold a witch's hut; nothing was recorded.</summary>
    None = 0,

    /// <summary>The hut holds no skill; a broken map, which the reference asserts on.</summary>
    Broken,

    /// <summary>The hero already has eight skills; there is no slot to write the new one into.</summary>
    NoRoom,

    /// <summary>The hero already has the skill at some level.</summary>
    AlreadyKnown,

    /// <summary>The skill was learned at the basic level.</summary>
    Learned,
}

/// <summary>
/// What <see cref="HeroActions.ActionToXanadu"/> got from the butler of Xanadu.
/// </summary>
/// <remarks>
/// 参考三种情形各一段对话，端口折成这张表。
/// </remarks>
public enum XanaduOutcome
{
    /// <summary>The hero is not on the tile of Xanadu; nothing was recorded.</summary>
    None = 0,

    /// <summary>The butler remembers the hero and refuses to teach him twice.</summary>
    AlreadyVisited,

    /// <summary>The hero is neither famous nor diplomatic enough; the tile stays unvisited for a later try.</summary>
    NotWorthy,

    /// <summary>The hero was trained and got one point in each primary skill.</summary>
    Trained,
}

/// <summary>
/// What <see cref="HeroActions.ActionToJail"/> did with the prisoner.
/// </summary>
/// <remarks>
/// <para>
/// 参考只有两段文案（放人 / 名额已满），端口再加一条「牢里没人」——参考对空牢什么也不显示，直接结束，
/// 而调用方需要一个能区分「放成功了」与「什么也没发生」的返回值。
/// </para>
/// </remarks>
public enum JailOutcome
{
    /// <summary>Nothing happened; the hero refused for a reason that should not occur.</summary>
    None = 0,

    /// <summary>The prisoner joined the kingdom and stands on the jail tile.</summary>
    Released,

    /// <summary>The kingdom already has <see cref="Kingdom.Kingdom.MaxHeroes"/> heroes; the jail is untouched.</summary>
    NoSeat,

    /// <summary>No jailed hero sits on that tile.</summary>
    NoPrisoner,
}

/// <summary>
/// What <see cref="HeroActions.ActionToSirens"/> took from the army and paid back.
/// </summary>
/// <remarks>
/// 参考没有返回值——它按「兵少了多少」选两段文案之一。端口把两个信息都留下：
/// <see cref="Experience"/> 是淹死的兵值多少经验，<see cref="AlreadyVisited"/> 是「这个英雄听过一次了」。
/// 两者都为「空」时不加经验，但原因不同，调用方要说的那句话也不同。
/// </remarks>
/// <param name="Experience">Experience granted, or <c>0</c> when no stack lost anyone.</param>
/// <param name="AlreadyVisited">Whether the hero had already heard the song; the army was left alone.</param>
public readonly record struct SirensReward(uint Experience, bool AlreadyVisited)
{
    /// <summary>Whether the visit cost nothing and paid nothing.</summary>
    public bool IsEmpty => Experience == 0 && !AlreadyVisited;
}

/// <summary>
/// What <see cref="HeroActions.ActionToMagellanMaps"/> did about the captain's maps.
/// </summary>
/// <remarks>
/// <para>
/// 参考只有三段文案（买过了 / 钱不够 / 成交），「玩家点了否」是第四种但什么也不显示。端口把「还没问」单独拿出来
/// 当 <see cref="NeedsConfirmation"/>：那是唯一一个「调用方什么都不该做、只需去问一句」的返回值。
/// </para>
/// </remarks>
public enum MagellansMapsOutcome
{
    /// <summary>Nothing happened; the tile holds no captain.</summary>
    None = 0,

    /// <summary>The kingdom already owns a set of these maps; nothing was charged.</summary>
    AlreadyBought,

    /// <summary>The treasury cannot pay <see cref="PaymentConditions.GetMagellansMapsPurchasePrice"/>.</summary>
    NotEnoughGold,

    /// <summary>Nothing was charged yet — ask the player whether the maps are worth the price.</summary>
    NeedsConfirmation,

    /// <summary>The sea was revealed, the gold was spent and the visit was recorded for the kingdom.</summary>
    Bought,
}

/// <summary>
/// What <see cref="HeroActions.ActionToTreasureChest"/> took out of a chest.
/// </summary>
/// <remarks>
/// 参考把三样东西装进 <c>std::tuple&lt;std::optional&lt;uint32_t&gt;, std::optional&lt;uint32_t&gt;,
/// std::optional&lt;Artifact&gt;&gt;</c>（<c>heroes_action.cpp:1801</c>），三项互斥但都可能为空。端口用一个
/// 值类型表达同样的三元组：0 表示没钱/没经验，<see cref="ArtifactType.Unknown"/> 表示没神器。
/// </remarks>
/// <param name="Gold">The gold paid, or <c>0</c>.</param>
/// <param name="Experience">The experience paid, or <c>0</c>.</param>
/// <param name="Artifact">The artifact paid, or <see cref="ArtifactType.Unknown"/>.</param>
public readonly record struct TreasureChestReward(uint Gold, uint Experience, ArtifactType Artifact)
{
    /// <summary>Whether the chest gave nothing at all.</summary>
    public bool IsEmpty => Gold == 0 && Experience == 0 && Artifact == ArtifactType.Unknown;
}

/// <summary>
/// What <see cref="HeroActions.ActionToSkeleton"/>, <see cref="HeroActions.ActionToWagon"/> and
/// <see cref="HeroActions.ActionToShipwreckSurvivor"/> took from the object.
/// </summary>
/// <remarks>
/// <para>
/// 这三支的产物都是「一袋资源<b>或</b>一件神器」，所以用 <see cref="Funds"/> 而不是金币数值——货车与骷髅都可能
/// 给木材、水晶这类非金币资源。参考没有返回值，它把这袋东西直接拼进对话框，端口折成这个值类型。
/// </para>
/// <para>
/// 与 <see cref="TreasureChestReward"/> 的区别只在有没有经验：那三支从不给经验，宝箱可以。
/// </para>
/// </remarks>
/// <param name="Funds">The resources paid, or an empty bag.</param>
/// <param name="Artifact">The artifact paid, or <see cref="ArtifactType.Unknown"/>.</param>
public readonly record struct ObjectPickupReward(Funds Funds, ArtifactType Artifact)
{
    /// <summary>Whether the object gave nothing at all.</summary>
    public bool IsEmpty => Funds.GetValidItemsCount() == 0 && Artifact == ArtifactType.Unknown;
}
