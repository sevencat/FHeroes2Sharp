using FHeroes2.Assets.Maps;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.System;
using KingdomRealm = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game;

/// <summary>
/// 一局冒险模式的回合骨架：谁在行动、什么时候换人、什么时候换天。
/// </summary>
/// <remarks>
/// <para>
/// <b>参考里的位置</b>：这不是一个类，而是 <c>Interface::AdventureMap::StartGame()</c>
/// （<c>game_startgame.cpp:732-790</c>）的那个 <c>while</c> 循环，加上 <c>humanTurn()</c>
/// （<c>:1007-1060</c>）开头的两件事。端口把「循环」与「画面」分开：循环在这里，画面在
/// <c>FHeroes2.App</c> 的场景里 —— 参考里两者挤在同一个类上，是因为它的事件循环就是界面循环。
/// </para>
/// <para>
/// <b>一天的结构</b>（<c>StartGame()</c>）：<c>world.NewDay()</c> 把日期推进一格，然后每个在局中的
/// 玩家轮流行动；人类玩家的回合一直持续到他按下「结束回合」。所以「新的一天」发生在<b>所有玩家都行动完
/// 之后</b>，而 <see cref="EndTurn"/> 只负责把人换掉。
/// </para>
/// <para>
/// <b>玩家顺序</b>：参考的 <c>SortPlayers()</c> 把人类排在电脑前面，这样开局是玩家先动。端口按同样的两段
/// 拼表（先人类、后电脑），段内取王国表的颜色序（<c>blue</c>、<c>green</c>…）—— 与参考的可见结果一致。
/// </para>
/// <para>
/// <b>电脑玩家也在轮转里</b>：参考的 <c>StartGame()</c> 对每个 <c>kingdom.isPlay()</c> 的色分派人类或电脑
/// （<c>game_startgame.cpp:844-974</c>）。端口照抄这张表，但电脑的回合内容要靠调用方驱动：
/// <see cref="EndTurn"/> 只把当前色换成电脑，<see cref="RunAiTurn"/> 才是那一回合的
/// <c>AI::Planner::KingdomTurn()</c>（<c>:938</c>）。分工的原因是参考的电脑回合自带画面循环
/// （光标、状态栏沙漏、走子动画），而端口的画面在 <c>FHeroes2.App</c> 的场景里 —— 场景每帧看一眼
/// <see cref="IsAiTurn"/>，是电脑就把这一回合跑掉。
/// </para>
/// <para>
/// <b>偏差</b>：参考的电脑回合里英雄走子是<b>逐格动画</b>（<c>AI::HeroesMove()</c> 自己带着事件循环与重绘，
/// <c>ai_hero_action.cpp:2245-2402</c>）；端口的 <c>AiHeroAction.HeroesMove()</c> 是纯规则、一口气走完，
/// 所以电脑英雄在画面上是瞬移的。要动画就得让 AI 的动作链可暂停（协作式调度），那是画面层的后续工作，
/// 记在 <c>bd 归档（PROGRESS.md，FHeroes2Sharp-jwk）</c> §7.3。
/// </para>
/// <para>
/// <b>回合开始的两件事</b>（<c>humanTurn()</c> <c>:1039-1043</c>）：先
/// <see cref="Kingdom.ActionNewDayResourceUpdate"/> 结算收入与当天的事件，再
/// <see cref="Kingdom.ActionBeforeTurn"/> 重算英雄路线。顺序不能反 —— 收入影响买兵，路线只看移动力，
/// 但参考就是这个顺序，照抄省得以后对不上。
/// </para>
/// </remarks>
public sealed class AdventureSession
{
    private readonly World _world;
    private readonly List<PlayerColor> _players;
    private readonly List<PlayerColor> _humans;
    private readonly bool[] _acted;
    private int _current;
    private Hero? _focusHero;

    private AdventureSession(World world, List<PlayerColor> players, List<PlayerColor> humans)
    {
        _world = world;
        _players = players;
        _humans = humans;
        _acted = new bool[players.Count];
    }

    /// <summary>
    /// 开一局：找到人类玩家的颜色，推进到第一天，让第一个人开始行动。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>world.NewDay()</c> 在这里调，对应 <c>StartGame()</c> 循环的第一句
    /// （<c>game_startgame.cpp:739</c>）。参考对新游戏与读档分开处理（读档时日期已经在对的位置），
    /// 端口还没有读档，所以只有一条路。
    /// </para>
    /// <para>
    /// 没有人类玩家的地图（全是电脑，或者一个色都没有）返回 <see langword="null"/>：这时没有什么可
    /// 交互的，调用方据此退回主菜单。
    /// </para>
    /// <para>
    /// 在局中的电脑色同样进轮转表，排在人类之后（参考的 <c>SortPlayers()</c>）。
    /// </para>
    /// </remarks>
    /// <param name="world">已经载入地图、建好王国与英雄的世界。</param>
    /// <param name="info">该地图的文件头，用来建玩家槽位。</param>
    /// <returns>The session, or <see langword="null"/> when the map has no human player.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="world"/> or <paramref name="info"/> is <see langword="null"/>.
    /// </exception>
    public static AdventureSession? Start(World world, Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(info);

        // 参考里这一步属于剧本选择（players.cpp:487 的 SetStartGame()），紧挨着 World::LoadMap。端口把它放在
        // 「开一局」这里：槽位表是进程级的静态表，Hero.GetControl() 与以后的 AI 都问它，但地图本身不该因为
        // 被读进来就改写全局状态（两个世界同时装载时会互相覆盖，测试正是这样）。
        Settings.Get().Players.Init(info);
        Settings.Get().Players.SetStartGame();

        var humans = new List<PlayerColor>();
        var computers = new List<PlayerColor>();

        foreach (PlayerColor color in PlayerColors.ToList(world.Kingdoms.GetColors()))
        {
            KingdomRealm kingdom = world.Kingdoms.GetKingdom(color);

            if (!kingdom.IsPlay)
            {
                continue;
            }

            (kingdom.IsControlHuman ? humans : computers).Add(color);
        }

        if (humans.Count == 0)
        {
            return null;
        }

        // 人类在前、电脑在后（参考的 SortPlayers()），两段内部保持颜色序。
        var session = new AdventureSession(world, [.. humans, .. computers], humans);

        // 开局清一遍迷雾，而且对<b>每个</b>玩家都清（game_startgame.cpp:769-781）。
        // 这不只是给人类看的：AI 的 Planner::ScanMap() 用 tile.isFog( color ) 过滤瓦片
        // （ai_planner_kingdom.cpp:430），只清人类的话电脑英雄在满屏大雾里找不到任何目标，
        // 回合空转、原地不动。参考在这里清的是英雄、城堡与矿场的周围（World::ClearFog）。
        foreach (PlayerColor color in session._players)
        {
            world.ClearFog(color);
        }

        // 参考在 world_loadmap.cpp 里给每位英雄装上侦察回调（开图），而「走到对象上之后做什么」的分岔在
        // Heroes::Action() 里按 GetKingdom().isControlAI() 做（heroes_action.cpp:3778-3785）。端口把这道分岔
        // 放到安装时：人类英雄只要开图（另外三件事都要问界面层，见 bd 归档（PROGRESS.md，FHeroes2Sharp-jwk） §7.4），电脑英雄装 AI 版，
        // 走一步就把脚下的对象处理掉。
        foreach (Hero hero in world.Heroes)
        {
            AttachMovementHandler(world, hero);
        }

        world.NewDay();
        session._current = 0;
        session.BeginTurn();

        return session;
    }

    /// <summary>
    /// Installs the movement handler a hero walks with, <c>IHeroMovementHandler</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考没有这个接缝：<c>Heroes::Action()</c> 自己按 <c>GetKingdom().isControlAI()</c> 分岔
    /// （<c>heroes_action.cpp:3778-3785</c>）。端口把分岔挪到安装时，于是<b>每一位</b>英雄入局时都得装一次——
    /// 开局装载是一次，之后每雇到一位英雄再一次。
    /// </para>
    /// <para>
    /// 漏装的后果不是崩溃，而是<b>卡死</b>：英雄保持默认的 <see cref="NullHeroMovementHandler"/>，走到不能
    /// 踏入的对象前会照付每一步的移动力、却什么也不做，于是一回合里反复撞同一格直到移动力耗尽，对象永远
    /// 不会消失（冒烟里就是「停在未占领的矿前不动」）。
    /// </para>
    /// </remarks>
    /// <param name="world">The world the hero lives in.</param>
    /// <param name="hero">The hero that just joined a kingdom.</param>
    internal static void AttachMovementHandler(World world, Hero hero)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(hero);

        hero.MovementHandler = world.Kingdoms.GetKingdom(hero.Color).IsControlHuman
            ? new FogHeroMovementHandler(world)
            : new AiHeroMovementHandler(world);
    }

    /// <summary>Map the session plays on.</summary>
    public World World => _world;

    /// <summary>
    /// Every color in the rotation, the reference's <c>SortedPlayers()</c>: the human ones first, then the computer.
    /// </summary>
    public IReadOnlyList<PlayerColor> Players => _players;

    /// <summary>The color whose turn it is, <c>Interface::AdventureMap::_statusPanel</c>'s player.</summary>
    public PlayerColor CurrentColor => _players[_current];

    /// <summary>
    /// The realm of <see cref="CurrentColor"/>.
    /// </summary>
    /// <remarks>
    /// 参考里处处是 <c>world.GetKingdom( color )</c>；端口把「当前色」收在这里，场景与状态栏就不必各自
    /// 记住是哪一位玩家的回合。
    /// </remarks>
    public KingdomRealm Kingdom => _world.Kingdoms.GetKingdom(CurrentColor);

    /// <summary>
    /// Whether the computer plays <see cref="CurrentColor"/>, <c>kingdom.GetControl() == CONTROL_AI</c>
    /// (<c>game_startgame.cpp:893</c>).
    /// </summary>
    /// <remarks>
    /// 调用方据此决定这一回合由谁跑：人类的是界面的一帧一帧（<c>HumanTurn()</c>），电脑的是
    /// <see cref="RunAiTurn"/>（<c>AI::Planner::KingdomTurn()</c>）。
    /// </remarks>
    public bool IsAiTurn => !Kingdom.IsControlHuman;

    /// <summary>
    /// The hero the cursor and the status bar show, <c>Interface::AdventureMap::GetFocus()</c>.
    /// </summary>
    /// <remarks>
    /// 参考的焦点存在 <c>Interface::Basic</c> 的 <c>_statusPanel</c> 里，随「下一个英雄」按钮与点击而变；
    /// 端口放在会话上，因为它是「谁在行动」的一部分，脱离界面也该有意义（例如存档与 AI 的 <c>GetFocus</c>）。
    /// </remarks>
    public Hero? FocusHero => _focusHero;

    /// <summary>Days since the start of the game, <c>world.CountDay()</c>.</summary>
    public uint DayCount => _world.DayCount;

    /// <summary>Whether every human kingdom is out of the game, <c>Game::CheckGameOver()</c>.</summary>
    /// <remarks>
    /// 只看人类：电脑全部出局不等于这局结束，那正是玩家赢了。
    /// </remarks>
    public bool IsGameOver
    {
        get
        {
            foreach (PlayerColor color in _humans)
            {
                if (!_world.Kingdoms.GetKingdom(color).IsLoss)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Ends the turn of <see cref="CurrentColor"/> and hands the map to the next player, or to a new day,
    /// <c>Interface::AdventureMap::HumanTurn()</c>'s <c>END_TURN</c> (<c>game_startgame.cpp:1030</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考里 <c>humanTurn()</c> 返回 <c>END_TURN</c> 之后，<c>StartGame()</c> 的循环接着找下一个
    /// <c>kingdom.isPlay()</c> 的色；一圈走完才回到循环顶部 <c>world.NewDay()</c>。这里逐字照抄。
    /// </para>
    /// <para>
    /// 换到的下一位可能是电脑：本方法只负责把他的回合开始（收入、当天事件、路线）安排上，
    /// 回合本身由调用方看着 <see cref="IsAiTurn"/> 用 <see cref="RunAiTurn"/> 跑掉。
    /// </para>
    /// <para>
    /// 出局的玩家同样跳过（<c>kingdom.isPlay()</c> 为假）：他的颜色还在列表里，但已经没有英雄和城。
    /// </para>
    /// </remarks>
    public void EndTurn()
    {
        if (IsGameOver)
        {
            return;
        }

        _acted[_current] = true;

        for (int offset = 1; offset <= _players.Count; ++offset)
        {
            int index = (_current + offset) % _players.Count;

            if (_acted[index] || !_world.Kingdoms.GetKingdom(_players[index]).IsPlay)
            {
                continue;
            }

            _current = index;
            BeginTurn();

            return;
        }

        // 一圈走完：新的一天。参考在 StartGame() 的循环顶部做这件事，位置等价。
        _world.NewDay();
        Array.Clear(_acted);
        _current = 0;

        // 当前色可能已经被淘汰且没有行动过（例如唯一的城被夺走），这时继续往后找。
        if (_world.Kingdoms.GetKingdom(_players[_current]).IsPlay)
        {
            BeginTurn();
        }
        else
        {
            EndTurn();
        }
    }

    /// <summary>
    /// Plays the turn of a computer player, the <c>AI::Planner::Get().KingdomTurn( kingdom )</c> of
    /// <c>game_startgame.cpp:938</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在这个分支里先换光标、把状态栏切成「电脑回合」、重绘一帧（<c>:897-918</c>），然后才调 AI；
    /// 那三件都是画面的事，归场景，这里只做规则那一半。回合开始的两件事（收入与当天事件、重算路线）在
    /// <see cref="EndTurn"/> 换到这一位时已经由 <see cref="BeginTurn"/> 做过，与参考的
    /// <c>ActionNewDayResourceUpdate( nullptr )</c> 加 <c>ActionBeforeTurn()</c>（<c>:926-930</c>）同序。
    /// </para>
    /// <para>
    /// 参考的 <c>AIWorldPathfinder::Get()</c> 是全局单例，端口按世界构造、第一次取用时交给
    /// <see cref="AiPlanner"/>（<c>World.AiPathfinder</c>），所以这里先取一次，免得回合里的距离查询撞上
    /// 「尚未附加」。
    /// </para>
    /// </remarks>
    /// <returns>
    /// What the AI wants next: <see cref="AiTurnOutcome.EndTurn"/> when it is done, or
    /// <see cref="AiTurnOutcome.QuitGame"/> when it gave up - the reference's <c>QUIT_GAME</c> leaves the game.
    /// </returns>
    /// <exception cref="InvalidOperationException"><see cref="IsAiTurn"/> is <see langword="false"/>.</exception>
    public AiTurnOutcome RunAiTurn()
    {
        if (!IsAiTurn)
        {
            throw new InvalidOperationException(
                $"AdventureSession.RunAiTurn — {CurrentColor} 不是电脑玩家，这一回合该由界面跑");
        }

        _ = _world.AiPathfinder;

        return AiPlanner.Instance.KingdomTurn(Kingdom);
    }

    /// <summary>
    /// Moves the focus to the next hero of <see cref="Kingdom"/>, wrapping around, the reference's
    /// <c>TAB</c> key in <c>HumanTurn()</c>.
    /// </summary>
    /// <returns>The new focus, or <see langword="null"/> when the kingdom has no hero.</returns>
    public Hero? NextHero()
    {
        IReadOnlyList<Hero> heroes = Kingdom.Heroes;

        if (heroes.Count == 0)
        {
            _focusHero = null;

            return null;
        }

        int current = _focusHero is null ? -1 : IndexOfHero(heroes, _focusHero);

        _focusHero = heroes[(current + 1) % heroes.Count];

        return _focusHero;
    }

    /// <summary>
    /// Makes <paramref name="hero"/> the focus, the reference's <c>Interface::AdventureMap::SetFocus()</c>.
    /// </summary>
    /// <remarks>
    /// 只接受当前王国的英雄：参考的焦点永远是「自己人」，点击对手的英雄只会得到一次侦探术式的提示，而
    /// 那属于 <c>Heroes::Action</c>，不是焦点。
    /// </remarks>
    /// <param name="hero">The hero to focus, or <see langword="null"/> to clear the focus.</param>
    /// <returns><see langword="true"/> when the focus changed.</returns>
    public bool SetFocus(Hero? hero)
    {
        if (hero is not null && IndexOfHero(Kingdom.Heroes, hero) < 0)
        {
            return false;
        }

        if (ReferenceEquals(_focusHero, hero))
        {
            return false;
        }

        _focusHero = hero;

        return true;
    }

    /// <summary>
    /// 给 <paramref name="hero"/> 铺一条通往 <paramref name="destinationIndex"/> 的路线并让他起步，
    /// 参考的 <c>Interface::AdventureMap::MouseLeftClick()</c> 里
    /// <c>hero-&gt;GetPath().SetPath( world.getPath( ... ) )</c> 加 <c>hero-&gt;SetMove( true )</c> 那一对。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只接受 <see cref="Kingdom"/> 的英雄：点到对手的英雄不会给对手铺路，这是参考的行为，也是常识。
    /// </para>
    /// <para>
    /// 目标不可达时 <c>world.GetPath()</c> 返回空路线，于是旧路线被清掉、英雄停在原地 —— 与参考点空地
    /// 的情形一致，所以这里不把空路线当失败之外的特殊情况处理。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero to walk.</param>
    /// <param name="destinationIndex">Destination tile index on the world map.</param>
    /// <returns><see langword="true"/> when a non-empty route was set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public bool SetPath(Hero hero, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (IndexOfHero(Kingdom.Heroes, hero) < 0 || destinationIndex < 0 || destinationIndex >= _world.Size)
        {
            return false;
        }

        hero.Path.SetPath(_world.GetPath(hero, destinationIndex));
        hero.SetMove(!hero.Path.IsEmpty);

        return !hero.Path.IsEmpty;
    }

    /// <summary>
    /// 回合开始：结算收入与当天的事件，再重算英雄路线，<c>humanTurn()</c> 的
    /// <c>:1039-1043</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 收入用 <c>world.CountCapturedMines( resource, color )</c> 与 <c>world.GetEventsDate( color )</c>
    /// 取值，与参考的 <c>kingdom.ActionNewDayResourceUpdate( ... )</c> 一致；事件对话框还不存在，所以
    /// 回调传 <see langword="null"/>（参考的电脑玩家也这么传）。
    /// </para>
    /// <para>
    /// 焦点跟着回合走：轮到谁，焦点就是他的第一个英雄 —— 参考的 <c>_statusPanel.SetHero( ... )</c> 在
    /// 换人时也要重新挑一个，否则画面上会留着上一位玩家的英雄。
    /// </para>
    /// </remarks>
    private void BeginTurn()
    {
        KingdomRealm kingdom = Kingdom;

        kingdom.ActionNewDayResourceUpdate(
            _world.DayCount,
            resourceType => _world.CountCapturedMines(resourceType, kingdom.Color),
            _world.GetEventsDate(kingdom.Color),
            isAiControlled: !kingdom.IsControlHuman);

        kingdom.ActionBeforeTurn(_world);

        IReadOnlyList<Hero> heroes = kingdom.Heroes;

        _focusHero = heroes.Count > 0 ? heroes[0] : null;
    }

    private static int IndexOfHero(IReadOnlyList<Hero> heroes, Hero hero)
    {
        for (int i = 0; i < heroes.Count; ++i)
        {
            if (ReferenceEquals(heroes[i], hero))
            {
                return i;
            }
        }

        return -1;
    }
}
