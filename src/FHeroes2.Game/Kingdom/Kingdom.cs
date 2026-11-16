using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
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
public sealed class Kingdom
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
    private readonly Recruits _recruits = new();
    private readonly Puzzle _puzzleMaps = new();

    private Funds _funds;
    private PlayerColor _color = PlayerColor.None;
    private int _lostTownDays = GameOverLostDays + 1;

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

        _castles.Clear();
        _heroes.Clear();

        // 参考 kingdom.cpp:132：酒馆的那一对也要清，否则换一张图还会推荐上一张图的英雄。
        _recruits.Reset();

        // 参考 kingdom.cpp:134：谜题只清格子，四条区带的顺序不重洗。
        _puzzleMaps.Reset();
    }

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
    /// Four of the reference's five sources are here: mines, and the three things a town pays for. The artifact
    /// income and the estates skill are not, because a hero carries neither a bag nor skills yet (ADR-112); both
    /// are zero in this port whatever the bit says, and the port is only exact while that stays true.
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

        return totalIncome;
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
}
