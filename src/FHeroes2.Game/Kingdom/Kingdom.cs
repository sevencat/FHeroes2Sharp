using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The realm of one player: a treasury and the two rosters, the reference's <c>Kingdom</c> (<c>kingdom.h:71</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>kingdom.h:71</c> struct Kingdom（<c>GetFunds</c> 135、<c>isLoss</c> 98、<c>isLosingGame</c> 147、
/// <c>canRecruitHeroes</c> 108、<c>hasHeroes</c> 115、<c>GetColor</c> 128、<c>GetLostTownDays</c> 156、
/// <c>GetHeroes</c>/<c>GetCastles</c> 178–193）、<c>kingdom.cpp:103</c>（<c>Init</c>）、<c>:124</c>（<c>clear</c>）、
/// <c>:271/284</c>（<c>AddHero</c>/<c>RemoveHero</c>）、<c>:311/322</c>（<c>AddCastle</c>/<c>RemoveCastle</c>）、
/// <c>:344-374</c>（五个计数）、<c>:384</c>（<c>AllowPayment</c>）、<c>:443/449</c>（<c>AddFundsResource</c>/
/// <c>OddFundsResource</c>）、<c>:225</c>（<c>ActionNewDayResourceUpdate</c>）、<c>:564</c>（<c>GetIncome</c>）、
/// <c>:865</c>（<c>_getKingdomStartingResources</c>）。
/// </para>
/// <para>
/// 本批只搬「账本 + 名册 + 计数 + 每日入账」这几层，凡是需要别的系统的一律留给后续批次，逐条列在 ADR-111/112/114：
/// 每日推进（<c>ActionNewDay</c> 的访问记录与胜负判定，要瓦片、对象类型与 <c>Players</c>）、周产
/// （<c>ActionNewWeek</c>，要 <c>Settings</c> 与招募池；周类型已有 <see cref="World.WeekType"/>）、招募
/// （<c>Recruits</c>，要英雄池与随机数）、解谜地图（<c>puzzle_maps</c>）、战役奖励与投降英雄
/// （<c>Players</c>/<c>Campaign</c>）、胜负判定（<c>LossPostActions</c>，要 <c>Players</c>）。
/// </para>
/// </remarks>
public sealed partial class Kingdom
{
    /// <summary>
    /// How many heroes a kingdom may have at once, <c>GameStatic::GetKingdomMaxHeroes()</c>.
    /// </summary>
    /// <remarks>参考：<c>src/fheroes2/game/game_static.cpp</c> GameStatic::GetKingdomMaxHeroes（恒为 8）。</remarks>
    public const int MaxHeroes = 8;

    /// <summary>
    /// Days a kingdom may stand without a town before the game ends it, <c>Game::GetGameOverLostDays()</c>.
    /// </summary>
    /// <remarks>参考：<c>src/fheroes2/game/game_static.cpp</c> GameStatic::GetGameOverLostDays（恒为 7）。</remarks>
    public const int GameOverLostDays = 7;

    private readonly List<CastleTown> _castles = [];
    private readonly List<Hero> _heroes = [];

    /// <summary>The objects any hero of the kingdom has already visited, newest first, <c>visit_object</c>.</summary>
    private readonly List<IndexObject> _visitedObjects = [];

    /// <summary>The monsters a Crystal Ball has shown this kingdom, <c>_monstersUnderVision</c>.</summary>
    private readonly HashSet<int> _monstersUnderVision = [];

    private readonly Recruits _recruits = new();
    private readonly Puzzle _puzzleMaps = new();

    private Funds _funds;
    private PlayerColor _color = PlayerColor.None;
    private int _lostTownDays = GameOverLostDays + 1;
    private int _visitedTentsColors;
    private KingdomModes _modes;
    private bool _isAiControlled;
    private bool _isOutOfGame;

    /// <summary>
    /// The color of the kingdom, <c>_color</c>; <see cref="PlayerColor.None"/> before <see cref="Init"/>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:128</c> GetColor。</remarks>
    public PlayerColor Color => _color;

    /// <summary>
    /// The treasury, <c>resource</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.h:135</c> GetFunds。返回 <c>ref</c> 是因为城堡的下单接口本来就吃 <c>ref Funds treasury</c>
    /// （ADR-107/108 的 <c>BuyBuilding</c>/<c>RecruitMonster</c>），账本必须能被它们当场改写。
    /// </para>
    /// <para>
    /// <c>Funds</c> 是可变结构（字段全公开），所以本属性返回可写引用；不要把它存成局部变量再改，那是在改副本。
    /// </para>
    /// </remarks>
    public ref Funds Treasury => ref _funds;

    /// <summary>
    /// How many days the kingdom has been without a town, <c>lost_town_days</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:156</c> GetLostTownDays；只在 <see cref="AddCastle"/> 与 <see cref="Clear"/> 里重置。</remarks>
    public int LostTownDays => _lostTownDays;

    /// <summary>
    /// Whether the kingdom belongs to a player that is still in the game, <c>isPlay()</c> (<c>kingdom.h:113</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.h:113</c> isPlay——问 <c>Players::Get( color )->isPlay()</c>。端口还没有 <c>Players</c>，
    /// 用「颜色不是 <see cref="PlayerColor.None"/>」代替：<see cref="Init"/> 给的颜色为真，<see cref="Clear"/> 之后为假。
    /// 等 <c>Players</c> 落地时，这里换成真正的询问即可，调用方（<see cref="ActionNewDay"/> 与
    /// <see cref="ActionNewWeek"/>）不用改。
    /// </para>
    /// <para>
    /// 这条判断之所以挡在访问记录清理<b>之后</b>，是因为参考即使对已经出局的王国也要清掉那些记录（见
    /// <c>kingdom.cpp:198</c> 的注释）：存档里的访问表不能因为玩家出局就越积越多。
    /// </para>
    /// </remarks>
    public bool IsPlay => _color != PlayerColor.None && !_isOutOfGame;

    /// <summary>
    /// Whether a human plays this kingdom, <c>isControlHuman()</c> (<c>kingdom.h:107</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:107</c>→<c>Players::GetPlayerControl( color )</c>。端口在 <see cref="Init"/> 收到
    /// 「是不是电脑」并记下来（同一个参数也决定初始资源，ADR-111），此后由它回答。
    /// </remarks>
    public bool IsControlHuman => !_isAiControlled;

    /// <summary>
    /// The kingdom's modes, <c>BitModes</c> of <c>kingdom.h:60</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:60</c> 继承 <c>BitModes</c>，取值见 <c>kingdom.h:63-69</c>（<c>IDENTIFYHERO = 0x0002</c>、
    /// <c>KINGDOM_OVERVIEW_CASTLE_SELECTION = 0x0008</c>）。<see cref="ActionNewDay"/> 每天清掉「已鉴定」这一位。
    /// </remarks>
    public KingdomModes Modes => _modes;

    /// <summary>
    /// The row the hero list of the kingdom overview was scrolled to, <c>_topHeroInKingdomView</c>
    /// (<c>kingdom.h:180</c>).
    /// </summary>
    /// <remarks>
    /// The reference keeps this on the kingdom so the overview reopens where it was left; the view writes it back when
    /// it closes (<c>kingdom_overview.cpp:928</c>).
    /// </remarks>
    public int TopHeroInKingdomView { get; set; }

    /// <summary>
    /// The row the town list of the kingdom overview was scrolled to, <c>_topCastleInKingdomView</c>
    /// (<c>kingdom.h:181</c>).
    /// </summary>
    /// <remarks>See <see cref="TopHeroInKingdomView"/>; the reference writes it at <c>kingdom_overview.cpp:929</c>.</remarks>
    public int TopCastleInKingdomView { get; set; }

    /// <summary>
    /// The objects any hero of the kingdom has visited, newest first, <c>visit_object</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h</c> 的 <c>std::list&lt;IndexObject&gt; visit_object</c>。列表按「最新在前」维护，
    /// 所以按类型问「访问过没有」可以只走到第一次命中。
    /// </remarks>
    public IReadOnlyList<IndexObject> VisitedObjects => _visitedObjects;

    /// <summary>
    /// The towns of the kingdom, <c>castles</c>, in the order they joined.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:182/191</c> GetCastles。</remarks>
    public IReadOnlyList<CastleTown> Castles => _castles;

    /// <summary>
    /// The heroes of the kingdom, <c>heroes</c>, in the order they joined.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:178/187</c> GetHeroes。</remarks>
    public IReadOnlyList<Hero> Heroes => _heroes;

    /// <summary>
    /// Whether the kingdom has nothing left and is out of the game, <c>isLoss()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:98</c> isLoss——没有城<b>且</b>没有英雄才算出局。</remarks>
    public bool IsLoss => _castles.Count == 0 && _heroes.Count == 0;

    /// <summary>
    /// Whether the kingdom has lost its last town, <c>isLosingGame()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:147</c> isLosingGame。它比 <see cref="IsLoss"/> 宽：还有英雄在野也算，只要没有城。
    /// 倒计时由 <see cref="LostTownDays"/> 数，数到 0 才真的出局。
    /// </remarks>
    public bool IsLosingGame => _castles.Count == 0;

    /// <summary>
    /// Whether the kingdom still has a hero, <c>hasHeroes()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:115</c> hasHeroes。</remarks>
    public bool HasHeroes => _heroes.Count > 0;

    /// <summary>
    /// Whether some town of the kingdom could provide a new hero, <c>canRecruitHeroes()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:108</c> canRecruitHeroes——有一座「已是城堡」或者「还能盖城堡」的城就行。
    /// </remarks>
    public bool CanRecruitHeroes =>
        _castles.Exists(castle => castle.IsCastle || !castle.IsBuildingDisabled(BuildingType.Castle));

    /// <summary>
    /// Whether the kingdom may take one more hero, <c>AllowRecruitHero()</c> (<c>kingdom.cpp:499</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.cpp:499-502</c>——<c>heroes.size() &lt; GetMaxHeroes()</c>，再按需检查酒馆价
    /// <c>PaymentConditions::RecruitHero()</c> 付不付得起。
    /// </para>
    /// <para>
    /// <b>与 <see cref="CanRecruitHeroes"/> 不是一回事</b>：那个问「有没有城能出兵」，这个问「名额还够不够、
    /// 钱够不够」。名字像，判据完全不同，别互相替换。
    /// </para>
    /// <para>
    /// 监牢（<see cref="HeroActions.ActionToJail"/>）走的是<b>不查钱</b>的那一支：囚犯不要钱，只要名额。
    /// </para>
    /// </remarks>
    /// <param name="checkPayment">Whether the tavern price must also be affordable.</param>
    /// <returns><see langword="true"/> when one more hero may join the kingdom.</returns>
    public bool AllowRecruitHero(bool checkPayment = false) =>
        _heroes.Count < MaxHeroes && (!checkPayment || AllowPayment(PaymentConditions.RecruitHero()));

    /// <summary>
    /// The two heroes the kingdom's tavern offers, <c>GetRecruits()</c> (<c>kingdom.cpp:317</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.h:169</c> GetRecruits。参考的这个函数除了返回还<b>顺手刷新</b>一遍：槽位空着、或者占位的英雄
    /// 已经不可招募（被雇走、关进了监狱），就现找一位补上，第一周还要考虑战役奖励给的英雄。
    /// </para>
    /// <para>
    /// 端口的本批只返回已经结算好的那一对（<see cref="Recruits"/>，ADR-118）：刷新要 <c>Settings::isCampaignGameType</c>、
    /// 战役存档、<c>Players</c> 的「是否人类控制」以及 <c>World::CountWeek</c>，前四样都还没有；
    /// 补位本身（<c>AllHeroes.GetHeroForHire</c>）已经有了，等这一层落下来再把它接上去。
    /// </para>
    /// </remarks>
    /// <returns>The pair of recruits; both slots empty before the first settlement.</returns>
    public Recruits GetRecruits() => _recruits;

    /// <summary>
    /// The jigsaw the kingdom's obelisks uncover, <c>GetPuzzleMaps()</c> (<c>kingdom.h:180</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:180</c> GetPuzzleMaps。谜题的四条区带顺序在王国创建时就洗好，之后 <c>Update</c> 只翻格子；
    /// 谁去数已访问的石阵、什么时候调 <c>Update</c>，在参考里属于动作层（`heroes_action.cpp` 的石阵分支），未搬。
    /// </remarks>
    /// <returns>The kingdom's puzzle, all tiles hidden until the obelisks are visited.</returns>
    public Puzzle GetPuzzleMaps() => _puzzleMaps;

    /// <summary>
    /// The starting resources of a difficulty level, <c>_getKingdomStartingResources()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.cpp:865</c>。玩家与电脑两张表不同：低难度下玩家更富、电脑更穷，最高难度下玩家一无所有。
    /// 参考在 <c>Init</c> 里还会按 <c>Players::HandicapStatus</c> 再打折（<c>getHandicapDependentIncome</c>），
    /// 那份百分比表住在 <c>players.cpp</c>，本批未搬（ADR-111）。
    /// </para>
    /// <para>表在参考里是 <c>Cost{ gold, wood, mercury, ore, sulfur, crystal, gems }</c>，这里直接给 <see cref="Funds"/>。</para>
    /// </remarks>
    /// <param name="difficulty">Map difficulty.</param>
    /// <param name="isAiControlled">Whether the player of this kingdom is the computer.</param>
    /// <returns>The starting treasury.</returns>
    public static Funds GetStartingResources(GameDifficulty difficulty, bool isAiControlled)
    {
        if (isAiControlled)
        {
            return difficulty switch
            {
                // { 7500, 20, 5, 20, 5, 5, 5 } as gold, wood, mercury, ore, sulfur, crystal, gems.
                GameDifficulty.Easy or GameDifficulty.Normal => new Funds(20, 20, 5, 5, 5, 5, 7500),
                // { 10000, 30, 10, 30, 10, 10, 10 }; the reference's default branch says the same.
                _ => new Funds(30, 30, 10, 10, 10, 10, 10000),
            };
        }

        return difficulty switch
        {
            GameDifficulty.Easy => new Funds(30, 30, 10, 10, 10, 10, 10000),
            GameDifficulty.Normal => new Funds(20, 20, 5, 5, 5, 5, 7500),
            GameDifficulty.Hard => new Funds(10, 10, 2, 2, 2, 2, 5000),
            GameDifficulty.Expert => new Funds(5, 5, 0, 0, 0, 0, 2500),
            GameDifficulty.Impossible => new Funds(),
            // A difficulty the port does not know yet: the reference asserts and falls back to normal.
            _ => new Funds(20, 20, 5, 5, 5, 5, 7500),
        };
    }

    /// <summary>
    /// Wipes the kingdom and starts it as <paramref name="color"/>, <c>Kingdom::Init()</c>.
    /// </summary>
    /// <remarks>
    /// <para>参考：<c>kingdom.cpp:103</c> Init。</para>
    /// <para>
    /// 参考从 <c>Game::getDifficulty()</c> 与 <c>Players::Get( color )->getHandicapStatus()</c> 两个全局取参数，
    /// 端口把两者提到参数上（ADR-111）；因此这里收的是难度与「这个颜色是不是电脑」，而不是颜色一个。
    /// </para>
    /// </remarks>
    /// <param name="color">The color of the kingdom.</param>
    /// <param name="difficulty">Map difficulty, which decides the starting resources.</param>
    /// <param name="isAiControlled">Whether the computer plays this color.</param>
    public void Init(PlayerColor color, GameDifficulty difficulty, bool isAiControlled)
    {
        Clear();

        _color = color;
        _isAiControlled = isAiControlled;

        if ((color & PlayerColors.All) == 0)
        {
            // The reference logs and returns here: an unknown color gets a kingdom with no resources.
            GameLog.Warn($"Kingdom::Init: unknown player color {color}, the kingdom stays empty.");
            return;
        }

        _funds = GetStartingResources(difficulty, isAiControlled);
    }

    /// <summary>
    /// Forgets the treasury and both rosters, <c>Kingdom::clear()</c>.
    /// </summary>
    /// <remarks>
    /// <para>参考：<c>kingdom.cpp:124</c> clear。</para>
    /// <para>
    /// 城与英雄只从名册里去掉，<b>不会</b>被改色或解散：参考的 <c>clear</c> 也不碰它们，那两件事属于
    /// <c>LossPostActions</c>（<c>kingdom.cpp:156</c>），要 <c>Players</c>，未搬。
    /// </para>
    /// </remarks>
    public void Clear()
    {
        _color = PlayerColor.None;
        _funds = default;
        _lostTownDays = GameOverLostDays + 1;

        // kingdom.cpp:126/129：模式位与旅行者帐篷的记录也归零。
        _modes = KingdomModes.None;
        _visitedTentsColors = 0;

        _castles.Clear();
        _heroes.Clear();

        // kingdom.cpp:134：访问记录是「这张图上的格子」，换图之后必须清空。
        _visitedObjects.Clear();
        _monstersUnderVision.Clear();

        // 参考 kingdom.cpp:136：酒馆的那一对也要清，否则换一张图还会推荐上一张图的英雄。
        _recruits.Reset();

        // 参考 kingdom.cpp:138：谜题只清格子，四条区带的顺序不重洗。
        _puzzleMaps.Reset();

        _isAiControlled = false;
        _isOutOfGame = false;
    }

    /// <summary>
    /// The treasury, <c>Kingdom::GetFunds()</c> (<c>kingdom.h:120</c>).
    /// </summary>
    /// <remarks>
    /// 参考返回的是 <c>const Funds &amp;</c>，调用方拿它去算「这次进账还剩多少装得下」之类的差值；端口每次复制一份，
    /// 于是调用方改不到账本本身——那正是 <see cref="AddFundsResource"/> 的活。
    /// </remarks>
    /// <returns>What the kingdom owns right now.</returns>
    public Funds GetFunds() => _funds;

    /// <summary>
    /// Whether the treasury holds everything <paramref name="funds"/> asks for, <c>AllowPayment()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.cpp:384</c>。参考的写法是「每一项都够，或者那一项本来就要 0」，所以零需求永远满足，
    /// 哪怕账本是 0——这一点和 <c>Funds::operator&gt;=</c> 不同，不要换成 <c>&gt;=</c>。
    /// </remarks>
    /// <param name="funds">The price.</param>
    /// <returns><see langword="true"/> when the price can be paid.</returns>
    public bool AllowPayment(in Funds funds) =>
        (_funds.Wood >= funds.Wood || funds.Wood == 0)
        && (_funds.Mercury >= funds.Mercury || funds.Mercury == 0)
        && (_funds.Ore >= funds.Ore || funds.Ore == 0)
        && (_funds.Sulfur >= funds.Sulfur || funds.Sulfur == 0)
        && (_funds.Crystal >= funds.Crystal || funds.Crystal == 0)
        && (_funds.Gems >= funds.Gems || funds.Gems == 0)
        && (_funds.Gold >= funds.Gold || funds.Gold == 0);

    /// <summary>
    /// Adds <paramref name="funds"/> to the treasury and clamps it at zero, <c>AddFundsResource()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:443</c>。加完不裁剪的话，一次负收入就能把账本写成负数。</remarks>
    /// <param name="funds">The income.</param>
    public void AddFundsResource(in Funds funds)
    {
        _funds.Add(funds);
        _funds.Trim();
    }

    /// <summary>
    /// Subtracts <paramref name="funds"/> from the treasury and clamps it at zero, <c>OddFundsResource()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:449</c>。</remarks>
    /// <param name="funds">The expense.</param>
    public void OddFundsResource(in Funds funds)
    {
        _funds.Subtract(funds);
        _funds.Trim();
    }

    /// <summary>
    /// What the kingdom earns every day, <c>Kingdom::GetIncome()</c> (<c>kingdom.cpp:564</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference asks the world how many mines of each resource the colour owns; a kingdom does not know the
    /// world, so the caller hands that count in (<c>world.CountCapturedMines</c>, ADR-078). The seven resources are
    /// asked in the reference's order — wood, ore, mercury, sulfur, crystal, gems, gold — which only matters for
    /// the day the counts start depending on each other.
    /// </para>
    /// <para>
    /// Four of the reference's five sources are here: mines, the three things a town pays for, and the artifacts the
    /// heroes carry. The fifth, the estates skill (<c>kingdom.cpp:616-622</c>), still has nothing to read: a hero has
    /// an artifact bag (<see cref="Hero.BagArtifacts"/>) but no secondary skills yet (ADR-112, corrected by ADR-171).
    /// </para>
    /// <para>
    /// The artifact branch walks every hero's whole bag, empty slots included, exactly as the reference does: an empty
    /// slot is <see cref="ArtifactType.Unknown"/>, whose table row is empty, so it adds nothing. That also keeps the
    /// spell book — which is a bag slot, not a separate thing — from needing an exception.
    /// </para>
    /// <para>
    /// The reference then adds the computer's difficulty bonus and the human's handicap (<c>kingdom.cpp:623-645</c>);
    /// both live in <c>players.cpp</c> and are not ported yet.
    /// </para>
    /// </remarks>
    /// <param name="countCapturedMines">
    /// How many mines of one <see cref="Resource"/> constant the kingdom's colour owns.
    /// </param>
    /// <param name="type">Which sources to add up; everything by default.</param>
    /// <returns>The daily income, in funds.</returns>
    public Funds GetIncome(Func<int, uint> countCapturedMines, IncomeType type = IncomeType.All)
    {
        ArgumentNullException.ThrowIfNull(countCapturedMines);

        Funds totalIncome = default;

        if ((type & IncomeType.Captured) != 0)
        {
            // kingdom.cpp:570-574.
            int[] resources =
            [
                Resource.Wood, Resource.Ore, Resource.Mercury, Resource.Sulfur, Resource.Crystal, Resource.Gems, Resource.Gold,
            ];

            foreach (int resource in resources)
            {
                totalIncome += ProfitConditions.FromMine(resource) * countCapturedMines(resource);
            }
        }

        if ((type & IncomeType.Castles) != 0)
        {
            foreach (CastleTown castle in _castles)
            {
                // kingdom.cpp:583-591: the town itself, then the statue, then the warlock's dungeon.
                totalIncome += ProfitConditions.FromBuilding(castle.IsCastle ? BuildingType.Castle : BuildingType.Tent, MapRace.None);

                if (castle.IsBuild(BuildingType.Statue))
                {
                    totalIncome += ProfitConditions.FromBuilding(BuildingType.Statue, MapRace.None);
                }

                if (castle.IsBuild(BuildingType.Spec) && castle.Race == MapRace.Warlock)
                {
                    totalIncome += ProfitConditions.FromBuilding(BuildingType.Spec, MapRace.Warlock);
                }
            }
        }

        if ((type & IncomeType.Artifacts) != 0)
        {
            // kingdom.cpp:607-614: everything every hero carries, whatever the slot.
            foreach (Hero hero in _heroes)
            {
                foreach (ArtifactType artifact in hero.BagArtifacts.Slots)
                {
                    totalIncome += ProfitConditions.FromArtifact(artifact);
                }
            }
        }

        if ((type & IncomeType.HeroSkills) != 0)
        {
            // kingdom.cpp:604-610 — the Estates skill is paid in gold, one per level, per hero who has it.
            foreach (Hero hero in _heroes)
            {
                totalIncome.Gold += (int)hero.GetSecondarySkillValue(SecondarySkill.Estates);
            }
        }

        return totalIncome;
    }

    /// <summary>
    /// The daily income with the difficulty corrections of the reference, <c>Kingdom::GetIncome( type )</c>
    /// (<c>kingdom.cpp:564-646</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GetIncome(Func{int, uint}, IncomeType)"/> answers "how much do my mines, towns, artifacts and skills
    /// pay" — the five <c>INCOME_*</c> blocks of the reference. This overload is the whole function: it adds the
    /// campaign awards of a human player (<c>kingdom.cpp:612-621</c>), the computer's difficulty bonus
    /// (<c>:623-639</c>), and finally the handicap discount (<c>:641-645</c>).
    /// </para>
    /// <para>
    /// The order is not negotiable. The campaign bonus and the handicap are for humans, the difficulty bonus for the
    /// computer, and the handicap is applied <b>last</b>, on the total the AI bonus already fed into — a player with a
    /// handicap never loses income the difficulty gave him, because he never gets any.
    /// </para>
    /// <para>
    /// 参考在这里读三个全局（<c>Game::getDifficulty()</c>、战役存档、<c>Players::Get</c>），端口把难度与战役奖励放到
    /// 参数上；手牌折扣则仍从 <see cref="Players"/> 读——那是玩家表本身，不需要外部再传一次。
    /// </para>
    /// </remarks>
    /// <param name="countCapturedMines">How many mines of one <see cref="Resource"/> constant the color owns.</param>
    /// <param name="type">Which sources to add up.</param>
    /// <param name="difficulty">Map difficulty, <c>Game::getDifficulty()</c>.</param>
    /// <param name="campaignBonus">
    /// The <c>TYPE_RESOURCE_BONUS</c> awards of the campaign save, <c>CampaignSaveData::getObtainedCampaignAwards()</c>.
    /// Ignored unless the kingdom is human controlled.
    /// </param>
    /// <returns>The daily income after every correction.</returns>
    public Funds GetIncome(Func<int, uint> countCapturedMines, IncomeType type, GameDifficulty difficulty, Func<Funds>? campaignBonus = null)
    {
        Funds totalIncome = GetIncome(countCapturedMines, type);

        if (IsControlHuman && (type & IncomeType.CampaignBonus) != 0 && campaignBonus is not null)
        {
            totalIncome += campaignBonus();
        }

        if (IsControlAi)
        {
            totalIncome += GetResourceIncomeBonusForAi(difficulty, _castles, countCapturedMines);

            int goldBonus = (int)(totalIncome.Gold * GetGoldIncomeBonusForAi(difficulty));

            if (goldBonus != 0)
            {
                totalIncome.Gold += goldBonus;
            }
        }

        return ApplyHandicap(totalIncome, Players.Get(_color)?.Handicap ?? Player.HandicapStatus.None);
    }

    /// <summary>
    /// Books the day: the income of every source, then the events of the day,
    /// <c>Kingdom::ActionNewDayResourceUpdate()</c> (<c>kingdom.cpp:225</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference asks the world for three things — how many mines it owns, how many days have passed and
    /// which events fire today. A kingdom knows neither the world nor the date, so all three come in as
    /// arguments: the mine count through the same callback as <see cref="GetIncome"/>, the day as a number,
    /// the events as data. That keeps the day cycle one line in the caller
    /// (<c>kingdom.ActionNewDayResourceUpdate( world.DayCount, resource =&gt; world.CountCapturedMines( resource,
    /// kingdom.Color ), world.GetEventsDate( kingdom.Color ), isAiControlled )</c>) and this method testable
    /// without a map.
    /// </para>
    /// <para>
    /// Two rules of the reference are easy to miss and both are here: the income is <b>skipped on day one</b>
    /// (the player starts with the funds of <see cref="GetStartingResources"/> and earns nothing for the first
    /// day), and an event is skipped for a computer player unless the map marked it applicable to AI players.
    /// </para>
    /// <para>
    /// The event's money is clamped: <see cref="Resource.CalculateEventResourceUpdate"/> cuts a grant at the
    /// resource cap, and a negative event takes what the kingdom has rather than what it asks for, so
    /// <see cref="AddFundsResource"/> never goes below zero gold because of an event.
    /// </para>
    /// </remarks>
    /// <param name="dayCount">Days since the start of the game, <c>world.CountDay()</c>.</param>
    /// <param name="countCapturedMines">How many mines of one <see cref="Resource"/> constant the color owns.</param>
    /// <param name="events">The events that fire today, <c>world.GetEventsDate( color )</c>.</param>
    /// <param name="isAiControlled">Whether the computer plays this kingdom, <c>GetControl() == CONTROL_AI</c>.</param>
    /// <param name="displayEventDialog">
    /// Told about every event that fires, with the funds it actually granted, so the interface can show it. The
    /// reference passes <c>std::function&lt;void( const EventDate &amp;, const Funds &amp; )&gt;</c>; a
    /// <see langword="null"/> callback means "apply silently", which is what the AI does.
    /// </param>
    public void ActionNewDayResourceUpdate(
        uint dayCount,
        Func<int, uint> countCapturedMines,
        IReadOnlyList<EventDate> events,
        bool isAiControlled,
        Action<EventDate, Funds>? displayEventDialog = null)
    {
        ArgumentNullException.ThrowIfNull(countCapturedMines);
        ArgumentNullException.ThrowIfNull(events);

        // kingdom.cpp:227-230: skip the income for the first day.
        if (dayCount > 1)
        {
            AddFundsResource(GetIncome(countCapturedMines));
        }

        // kingdom.cpp:234-245: the events of the day, in map order.
        foreach (EventDate eventDate in events)
        {
            if (isAiControlled && !eventDate.IsApplicableForAIPlayers)
            {
                continue;
            }

            Funds fundsUpdate = Resource.CalculateEventResourceUpdate(_funds, eventDate.Resources);

            AddFundsResource(fundsUpdate);
            displayEventDialog?.Invoke(eventDate, fundsUpdate);
        }
    }

    /// <summary>
    /// Puts <paramref name="castle"/> into the roster, <c>Kingdom::AddCastle()</c>.
    /// </summary>
    /// <remarks>
    /// <para>参考：<c>kingdom.cpp:311</c>。</para>
    /// <para>
    /// 参考在这里不改色：改色由占领流程负责（<c>Castle::ChangeColor</c>，ADR-108），本方法只管名册。加城会把
    /// 「失城天数」倒计时重置回最大，这是参考里唯一的重置点。
    /// </para>
    /// </remarks>
    /// <param name="castle">The town; a town that is already in the roster is ignored.</param>
    public void AddCastle(CastleTown castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        if (!_castles.Contains(castle))
        {
            _castles.Add(castle);
        }

        _lostTownDays = GameOverLostDays + 1;
    }

    /// <summary>
    /// Takes <paramref name="castle"/> out of the roster, <c>Kingdom::RemoveCastle()</c>.
    /// </summary>
    /// <remarks>
    /// <para>参考：<c>kingdom.cpp:322</c>。</para>
    /// <para>
    /// 参考随后会检查焦点、判负并跑 <c>LossPostActions</c>；端口只做名册这一半，判负留给调用方看
    /// <see cref="IsLoss"/>，因为出局流程要 <c>Players</c>（ADR-111）。
    /// </para>
    /// </remarks>
    /// <param name="castle">The town; a town that is not in the roster is ignored.</param>
    public void RemoveCastle(CastleTown castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        _castles.Remove(castle);
    }

    /// <summary>
    /// Puts <paramref name="hero"/> into the roster, <c>Kingdom::AddHero()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:271</c>；重复加入被忽略。</remarks>
    /// <param name="hero">The hero.</param>
    public void AddHero(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (!_heroes.Contains(hero))
        {
            _heroes.Add(hero);

            // 参考里颜色是 <c>world.LoadMapMP2</c> 之后由 <c>Players</c> 逐个设上的（<c>heroes.cpp</c> 的
            // <c>SetColor</c>）；端口还没有 <c>Players</c>，而「名册里的英雄」与「服务于这个颜色的英雄」在
            // 端口里必须一致——<see cref="Heroes.HeroActions"/> 正是靠颜色找王国（ADR-122）。于是这里替
            // Players 把那一半做掉：进谁的名册就服谁的颜色。
            hero.SetColor(_color);
        }
    }

    /// <summary>
    /// Takes <paramref name="hero"/> out of the roster, <c>Kingdom::RemoveHero()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:284</c>；判负与出局流程同 <see cref="RemoveCastle"/>，未搬。</remarks>
    /// <param name="hero">The hero.</param>
    public void RemoveHero(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        _heroes.Remove(hero);
    }

    /// <summary>
    /// 把英雄挪到名册最前，<c>interface_icons.cpp:106-124</c> 里对 <c>GetHeroes()</c> 的 <c>std::rotate</c>。
    /// </summary>
    /// <remarks>
    /// 图标栏按住 <c>WORLD_MOVE_HERO_PORTRAIT_UP_MODIFIER</c> 点击头像时靠它换序；参考直接改 <c>GetHeroes()</c>
    /// 的向量，端口的 <see cref="Heroes"/> 是只读视图，所以这里单开一个方法。名册里没有这个英雄、或它已经在最前时
    /// 什么也不做。
    /// </remarks>
    /// <param name="hero">要挪到最前的英雄。</param>
    /// <returns>名册的顺序是否变了。</returns>
    public bool MoveHeroToFront(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        int index = _heroes.IndexOf(hero);

        if (index <= 0)
        {
            return false;
        }

        _heroes.RemoveAt(index);
        _heroes.Insert(0, hero);

        return true;
    }

    /// <summary>
    /// How many towns of the kingdom have their fort, <c>GetCountCastle()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:344</c>，谓词是 <c>Castle::PredicateIsCastle</c>（<c>isCastle()</c>）。</remarks>
    /// <returns>The count.</returns>
    public int CountCastles() => _castles.FindAll(castle => castle.IsCastle).Count;

    /// <summary>
    /// How many towns of the kingdom are plain towns without a fort, <c>GetCountTown()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:349</c>，谓词是 <c>Castle::PredicateIsTown</c>（<c>!isCastle()</c>）。</remarks>
    /// <returns>The count.</returns>
    public int CountTowns() => _castles.FindAll(castle => !castle.IsCastle).Count;

    /// <summary>
    /// How many marketplaces the kingdom holds, <c>GetCountMarketplace()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:354</c>；市场之间要靠它算换率。</remarks>
    /// <returns>The count.</returns>
    public int CountMarketplaces() => CountBuildings(BuildingType.Marketplace);

    /// <summary>
    /// How many thieves' guilds the kingdom holds, <c>GetCountThievesGuild()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:370</c>；情报的精度按它算。</remarks>
    /// <returns>The count.</returns>
    public int CountThievesGuilds() => CountBuildings(BuildingType.ThievesGuild);

    /// <summary>
    /// How many towns of the kingdom have the necromancy shrine, <c>GetCountNecromancyShrineBuild()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:360</c>，谓词是 <c>Castle::isNecromancyShrineBuild()</c>。</remarks>
    /// <returns>The count.</returns>
    public int CountNecromancyShrines() => _castles.FindAll(castle => castle.IsNecromancyShrineBuild).Count;

    /// <summary>
    /// How many towns of the kingdom have <paramref name="building"/>, <c>GetCountBuilding()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:365</c>；<c>building</c> 是位掩码，所以组合掩码（如 <c>DwellingMonster1</c> 全家）也能问。</remarks>
    /// <param name="building">The building, or an OR of buildings.</param>
    /// <returns>The count.</returns>
    public int CountBuildings(BuildingType building) => _castles.FindAll(castle => castle.IsBuild(building)).Count;

    /// <summary>
    /// Remembers that <paramref name="index"/> was visited, <c>Kingdom::SetVisited()</c> (<c>kingdom.cpp:666</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.cpp:666</c>。三件事必须一起看：<c>OBJ_NONE</c> 不记（没有对象的格子不算拜访）；同一个
    /// 「格子 + 对象类型」已经在表里时先删掉；插入在表头。三者合起来保证「最新在前」且不重复。
    /// </para>
    /// <para>
    /// 为什么按「格子 + 类型」而不是只按格子去重：同一个格子在清掉一个对象之后可能被摆上另一个（编辑器与
    /// 事件都会这么做），只按格子去重会把「新对象还没被访问」误判成旧对象的记录。
    /// </para>
    /// </remarks>
    /// <param name="index">Tile index on the map.</param>
    /// <param name="objectType">The object found there; <see cref="Mp2ObjectType.None"/> is ignored.</param>
    public void SetVisited(int index, Mp2ObjectType objectType)
    {
        if (objectType == Mp2ObjectType.None)
        {
            return;
        }

        var visit = new IndexObject(index, objectType);

        _visitedObjects.RemoveAll(entry => entry == visit);
        _visitedObjects.Insert(0, visit);
    }

    /// <summary>
    /// Whether this exact tile has been visited for this object, <c>isVisited( index, type )</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:262</c>。</remarks>
    /// <param name="index">Tile index on the map.</param>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> when the pair is in the visit list.</returns>
    public bool IsVisited(int index, Mp2ObjectType objectType) =>
        _visitedObjects.Exists(entry => entry.Index == index && entry.Object == objectType);

    /// <summary>
    /// Whether any object of <paramref name="objectType"/> has been visited, <c>isVisited( type )</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:267</c>。两种问法都是「有没有」：一次性的建筑（+1 攻击力之类）只要访问过一座就够了。
    /// </remarks>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> when an object of that type is in the visit list.</returns>
    public bool IsVisited(Mp2ObjectType objectType) =>
        _visitedObjects.Exists(entry => entry.Object == objectType);

    /// <summary>
    /// Whether <paramref name="tile"/> has been visited for the object it holds, <c>isVisited( tile )</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:272</c>。它问的是瓦片上缓存的主对象类型（<c>Tile::MainObjectType</c>），不是把英雄
    /// 拨开之后看到的那一个——访问记录记的本来就是「英雄当时踩到的东西」。
    /// </remarks>
    /// <param name="tile">The tile.</param>
    /// <returns><see langword="true"/> when the tile's main object is in the visit list.</returns>
    public bool IsVisited(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        return IsVisited(tile.Index, tile.MainObjectType);
    }

    /// <summary>
    /// How many objects of <paramref name="objectType"/> have been visited, <c>CountVisitedObjects()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.cpp:681</c>。石阵用它数：「已访问几座」就是拼图该揭开多少（<c>heroes_action.cpp</c> 的
    /// 石阵分支，ADR-121）。
    /// </remarks>
    /// <param name="objectType">The object type.</param>
    /// <returns>The number of distinct tiles of that type in the visit list.</returns>
    public int CountVisitedObjects(Mp2ObjectType objectType) =>
        _visitedObjects.FindAll(entry => entry.Object == objectType).Count;

    /// <summary>
    /// Remembers that the traveller's tent of one color has been entered, <c>markTravellerTentVisited()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:224-231</c>。记录是一张按颜色位的位图，<b>不</b>跟着访问记录走：帐篷是永久解锁，
    /// 一周/一天的生命周期与它无关。
    /// </remarks>
    /// <param name="barrierColor">The colour constant of the tent, <see cref="PlayerColor"/>.</param>
    public void MarkTravellerTentVisited(int barrierColor) => _visitedTentsColors |= 1 << barrierColor;

    /// <summary>
    /// Whether the traveller's tent of one color has been entered, <c>isTravellerTentVisited()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:229-232</c>。</remarks>
    /// <param name="barrierColor">The colour constant of the tent, <see cref="PlayerColor"/>.</param>
    /// <returns><see langword="true"/> when the tent has been entered.</returns>
    public bool IsTravellerTentVisited(int barrierColor) => (_visitedTentsColors & (1 << barrierColor)) != 0;

    /// <summary>
    /// Sets bits in the kingdom's modes, <c>SetModes()</c> of <c>BitModes</c>.
    /// </summary>
    /// <param name="modes">The bits to set.</param>
    public void SetModes(KingdomModes modes) => _modes |= modes;

    /// <summary>
    /// Clears bits in the kingdom's modes, <c>ResetModes()</c> of <c>BitModes</c>.
    /// </summary>
    /// <param name="modes">The bits to clear.</param>
    public void ResetModes(KingdomModes modes) => _modes &= ~modes;

    /// <summary>
    /// Remembers that a Crystal Ball showed the kingdom a monster stack, <c>addMonsterUnderVisionSpell()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.h:243-246</c>。和访问记录一样每天清空：它是「本回合看得见」而不是永久情报。
    /// </remarks>
    /// <param name="monsterIndex">Tile index of the monster stack.</param>
    public void AddMonsterUnderVision(int monsterIndex) => _monstersUnderVision.Add(monsterIndex);

    /// <summary>
    /// Whether a Crystal Ball is currently showing that monster stack, <c>isMonsterUnderVisionSpell()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.h:248-251</c>。</remarks>
    /// <param name="monsterIndex">Tile index of the monster stack.</param>
    /// <returns><see langword="true"/> when the stack is under the vision spell.</returns>
    public bool IsMonsterUnderVisionSpell(int monsterIndex) => _monstersUnderVision.Contains(monsterIndex);

    /// <summary>
    /// An obelisk has been reached, <c>heroes_action.cpp</c>'s <c>OBJ_OBELISK</c> branch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2924-2946</c>。这一支只做三件事：全局登记「这座石阵看过了」、按「已看过的石阵数 /
    /// 地图上的石阵总数」重算拼图、然后（界面侧）放音效并打开寻宝图。
    /// </para>
    /// <para>
    /// 端口把它放在王国上，因为前两件事都是王国状态；第三件是对话框，留给界面层。<b>返回值就是给界面层的</b>：
    /// <see langword="true"/> 表示这次揭开了一角，可以放音效、打开寻宝图；<see langword="false"/> 表示「你已经来过
    /// 这座石阵了」，参考在那一支里只弹一句提示。
    /// </para>
    /// <para>
    /// 为什么要**全局**登记：同一位石阵换个英雄来看不该再揭一次，所以它进的是王国名册（<c>Visit::GLOBAL</c>），
    /// 而不是英雄自己的记录。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the obelisk stands on.</param>
    /// <param name="totalObelisks">How many obelisks the map carries, <c>world.CountObeliskOnMaps()</c>.</param>
    /// <returns><see langword="true"/> when this visit uncovered part of the puzzle.</returns>
    public bool ActionObelisk(int tileIndex, uint totalObelisks)
    {
        if (IsVisited(tileIndex, Mp2ObjectType.Obelisk))
        {
            return false;
        }

        SetVisited(tileIndex, Mp2ObjectType.Obelisk);
        _puzzleMaps.Update((uint)CountVisitedObjects(Mp2ObjectType.Obelisk), totalObelisks);

        return true;
    }

    /// <summary>
    /// What the kingdom does when its turn begins, <c>Kingdom::ActionBeforeTurn()</c> (<c>kingdom.cpp:184</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考只有一件事：人类的英雄重算路线（<see cref="Hero.CalculatePath"/>），电脑的英雄清空路线。前者是因为
    /// 视线和移动力都变了，上一回合点出来的路线要重新定价；后者是因为电脑每回合自己规划
    /// （<c>AI::Planner</c>，端口的 <c>Game/Ai</c> 只有契约），留着旧路线只会误导它。
    /// </para>
    /// <para>
    /// 端口的王国不认识世界，所以世界提到参数上——与 <see cref="ActionNewDay"/> 一个路子。
    /// </para>
    /// </remarks>
    /// <param name="world">Map the kingdom plays on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public void ActionBeforeTurn(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        bool human = IsControlHuman;

        foreach (Hero hero in _heroes)
        {
            if (human)
            {
                hero.CalculatePath(world);
            }
            else
            {
                hero.Path.Reset();
            }
        }
    }

    /// <summary>
    /// The kingdom's part of a new day, <c>Kingdom::ActionNewDay()</c> (<c>kingdom.cpp:197</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.cpp:197</c>。顺序照抄：先清一天寿命的访问记录（<b>出局了也要清</b>，见参考自己的注释），
    /// 再问「还在不在局中」；失城倒计时只在第一天之后、没有城、且还剩天数时递减；出局或倒计时归零才跑
    /// <c>LossPostActions</c> 并结束；否则清「已鉴定」位与水晶球看到的怪。
    /// </para>
    /// <para>
    /// <c>LossPostActions</c>（<c>kingdom.cpp:156</c>）要 <c>Players</c>：它把英雄和城改色、从名册里删掉、
    /// 再看看要不要弹「你输了」。端口把它做成可选回调，传 <see langword="null"/> 时就什么都不做——仍然是
    /// 「出局了，别再往下走」这一半语义。等 <c>Players</c> 落地，调用方会把真正的流程接进来。
    /// </para>
    /// <para>
    /// 日期来自 <c>world.CountDay()</c>，端口的 <see cref="Kingdom"/> 不认识世界，于是提到参数上——与
    /// <see cref="ActionNewDayResourceUpdate"/> 一个路子。
    /// </para>
    /// </remarks>
    /// <param name="dayCount">Days since the start of the game, <c>world.CountDay()</c>.</param>
    /// <param name="lossPostActions">
    /// What the reference's <c>LossPostActions()</c> does, if the caller has a player layer to do it with.
    /// </param>
    public void ActionNewDay(uint dayCount, Action? lossPostActions = null)
    {
        _visitedObjects.RemoveAll(Visit.IsDayLife);

        // 参考在 world.NewDay 里分开调 kingdoms.NewDay() 与 vec_heroes.ActionNewDay()；端口没有全局英雄名册，
        // 王国的名册就是「在局中的英雄」的全部，于是在这里替世界把英雄的一天也走掉。英雄的 tick 不看 IsPlay：
        // 参考的 vec_heroes 只装活着的英雄，与王国是否已经出局无关。
        foreach (Hero hero in _heroes)
        {
            hero.ActionNewDay(dayCount);
        }

        if (!IsPlay)
        {
            return;
        }

        // kingdom.cpp:206-210：失城倒计时。第一天不算（那天的城还没结算），已经有城就不递减。
        bool countdownRanOut = false;

        if (dayCount > 1 && _castles.Count == 0 && _lostTownDays > 0)
        {
            --_lostTownDays;
            countdownRanOut = _lostTownDays == 0;
        }

        // kingdom.cpp:213-217：出局，或者倒计时归零。参考里 LossPostActions 会把玩家标成「不再参战」，
        // 所以第二天进来时 isPlay() 已经是假；端口没有那一步，于是只在「刚刚归零」这一天交出去一次。
        if (IsLoss || countdownRanOut)
        {
            // 参考的 LossPostActions 里第一件事就是把玩家的 isPlay 置假（Players::SetPlay(false)），
            // 于是「已经出局」的王国第二天不会再走到这里。端口在这里把那个状态记下来，回调只是它的补充。
            _isOutOfGame = true;
            lossPostActions?.Invoke();
            return;
        }

        // kingdom.cpp:221-224：「已鉴定」与水晶球看到的怪都只活一天。
        ResetModes(KingdomModes.IdentifyHero);
        _monstersUnderVision.Clear();
    }

    /// <summary>
    /// The kingdom's part of a new week, <c>Kingdom::ActionNewWeek()</c> (<c>kingdom.cpp:266</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>kingdom.cpp:266</c>。一周寿命的访问记录（风车、水车、各处怪物巢穴……）先清，同样<b>出局了也要清</b>；
    /// 然后跳过第一周；调试模式给人类玩家一份礼物；最后重排酒馆的那一对英雄。
    /// </para>
    /// <para>
    /// 那份礼物在参考里由编译期的 <c>IS_DEVEL()</c> 控制，端口把它提成 <paramref name="isDebugMode"/>——生产构建里
    /// 这段代码根本不存在，所以它不该是「运行时开关」，而是「调用方是否在调一个调试入口」。
    /// </para>
    /// <para>
    /// 结尾那句重排酒馆的 <c>GetRecruits()</c>（<c>kingdom.cpp:268</c>）要 <c>World</c> 才能补位，所以不带
    /// <see cref="World"/> 的这个重载只做「清访问记录 + 调试礼物」，补位交给
    /// <see cref="ActionNewWeek(World, uint, bool)"/>。
    /// </para>
    /// </remarks>
    /// <param name="weekCount">Weeks since the start of the game, <c>world.CountWeek()</c>.</param>
    /// <param name="isDebugMode">Whether the reference's <c>IS_DEVEL()</c> build is in effect.</param>
    public void ActionNewWeek(uint weekCount, bool isDebugMode = false)
    {
        _visitedObjects.RemoveAll(Visit.IsWeekLife);

        foreach (Hero hero in _heroes)
        {
            hero.ActionNewWeek();
        }

        if (!IsPlay)
        {
            return;
        }

        if (weekCount > 1 && isDebugMode && IsControlHuman)
        {
            // kingdom.cpp:277-282: { wood 20, mercury 20, ore 10, sulfur 10, crystal 10, gems 10, gold 5000 }.
            AddFundsResource(new Funds(20, 20, 10, 10, 10, 10, 5000));
            GameLog.Info($"debug gift granted to {_color}: 5000 gold, 20 wood, 20 mercury, 10 of each other resource.");
        }
    }

    /// <summary>
    /// The kingdom's part of a new week, including the tavern's new roster,
    /// <c>Kingdom::ActionNewWeek()</c> (<c>kingdom.cpp:248-269</c>).
    /// </summary>
    /// <remarks>
    /// 参考在最后一句 <c>GetRecruits()</c>（<c>:268</c>）里顺手补位：酒馆的两位英雄被雇走、关进监狱或者上周逃跑过，
    /// 这一周就要换人。补位要查英雄池，所以只有带 <see cref="World"/> 的这条重载才算真的「重排」。
    /// </remarks>
    /// <param name="world">The map and its hero pool.</param>
    /// <param name="weekCount">Weeks since the start of the game, <c>world.CountWeek()</c>.</param>
    /// <param name="isDebugMode">Whether the reference's <c>IS_DEVEL()</c> build is in effect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public void ActionNewWeek(World world, uint weekCount, bool isDebugMode = false)
    {
        ArgumentNullException.ThrowIfNull(world);

        ActionNewWeek(weekCount, isDebugMode);

        if (IsPlay)
        {
            GetRecruits(world);
        }
    }
}
