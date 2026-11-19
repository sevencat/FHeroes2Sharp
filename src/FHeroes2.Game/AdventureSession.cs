using FHeroes2.Assets.Maps;
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
/// <b>玩家顺序</b>：参考的 <c>SortPlayers()</c> 把人类排在电脑前面，这样开局是玩家先动。端口的顺序直接取
/// 王国表里的颜色顺序（<c>blue</c>、<c>green</c>…），人类色仍然排前面 —— 与参考的可见结果一致，因为参考的
/// 排序在人类之间也保持颜色序。
/// </para>
/// <para>
/// <b>电脑玩家被跳过</b>：<c>AI::Planner</c> 在端口只有契约（<c>Game/Ai</c>），所以 <see cref="EndTurn"/>
/// 找下一个玩家时会跳过非人类的色。这不是偷懒：参考里电脑回合的全部内容都在 <c>AI::Planner::KingdomTurn()</c>
/// 里，没有它就没有可执行的动作；跳过至少让日期与资源继续走，人类玩家的城也能被自己访问。
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
    private readonly bool[] _acted;
    private int _current;
    private Hero? _focusHero;

    private AdventureSession(World world, List<PlayerColor> players)
    {
        _world = world;
        _players = players;
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

        var players = new List<PlayerColor>();

        foreach (PlayerColor color in PlayerColors.ToList(world.Kingdoms.GetColors()))
        {
            KingdomRealm kingdom = world.Kingdoms.GetKingdom(color);

            if (kingdom.IsPlay && kingdom.IsControlHuman)
            {
                players.Add(color);
            }
        }

        if (players.Count == 0)
        {
            return null;
        }

        var session = new AdventureSession(world, players);

        // 参考在 world_loadmap.cpp 里给每位英雄装上侦察回调（开图）。端口把这条回调拆成
        // IHeroMovementHandler，迷雾版只需要世界；没有它，英雄走进雾里不会开图，地图会越走越灰。
        foreach (Hero hero in world.Heroes)
        {
            hero.MovementHandler = new FogHeroMovementHandler(world);
        }

        world.NewDay();
        session._current = 0;
        session.BeginTurn();

        return session;
    }

    /// <summary>Map the session plays on.</summary>
    public World World => _world;

    /// <summary>The colors the human plays; the reference's <c>players</c> with the human ones first.</summary>
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
    public bool IsGameOver
    {
        get
        {
            foreach (PlayerColor color in _players)
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
    /// <c>kingdom.isPlay()</c> 的色；一圈走完才回到循环顶部 <c>world.NewDay()</c>。这里逐字照抄，只是
    /// 电脑的回合没有内容可跑（见类文档），于是被跳过。
    /// </para>
    /// <para>
    /// 出局的人类玩家同样跳过（<c>kingdom.isPlay()</c> 为假）：他的颜色还在列表里，但已经没有英雄和城。
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
