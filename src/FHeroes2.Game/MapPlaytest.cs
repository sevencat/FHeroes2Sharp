using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// What happened to one player of an automatic playtest, the <c>fheroes2::AutoPlaytest::PlayerState</c> of
/// <c>game_auto_playtest.h</c>.
/// </summary>
public enum PlaytestPlayerState
{
    /// <summary>The playtest stopped before this player won or lost, <c>TIME_LIMIT</c>.</summary>
    TimeLimit,

    /// <summary>This player was the last one standing, <c>WINNER</c>.</summary>
    Winner,

    /// <summary>This player lost their kingdom, <c>LOSER</c>.</summary>
    Loser,
}

/// <summary>
/// One line of a playtest report: a player, how their run ended and on which day.
/// </summary>
/// <param name="Color">The player.</param>
/// <param name="State">How it ended.</param>
/// <param name="Day">The day it ended on; the last day played for a time limit.</param>
public readonly record struct PlaytestPlayerResult(PlayerColor Color, PlaytestPlayerState State, uint Day);

/// <summary>
/// One playthrough of a map with every player run by the computer, the head of
/// <c>fheroes2::AutoPlaytest</c> (<c>game_auto_playtest.cpp:280-360</c>).
/// </summary>
/// <param name="Day">The last day played.</param>
/// <param name="Players">One line per player that was in play.</param>
public sealed record PlaytestReport(uint Day, IReadOnlyList<PlaytestPlayerResult> Players)
{
    /// <summary>The colour that was still standing, or <see cref="PlayerColor.None"/> when nobody was.</summary>
    public PlayerColor Winner =>
        Players.FirstOrDefault(player => player.State == PlaytestPlayerState.Winner).Color;
}

/// <summary>
/// What one playthrough of a series says about one colour: how many of the playthroughs it won.
/// </summary>
/// <param name="Color">The player.</param>
/// <param name="Wins">Playthroughs this colour won.</param>
/// <param name="Playthroughs">Playthroughs that were counted, i.e. the series without the time-limited runs.</param>
public readonly record struct PlaytestColorResult(PlayerColor Color, int Wins, int Playthroughs)
{
    /// <summary>The win rate in whole percent, <c>wins * 100 / playthroughCount</c> of the reference.</summary>
    public int Percentage => Playthroughs == 0 ? 0 : Wins * 100 / Playthroughs;
}

/// <summary>
/// The settings of an automatic playtest, the <c>AutoPlaytest</c> members of
/// <c>game_auto_playtest.h:31-200</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的三个滑条各有一个 <c>std::clamp</c> 的 setter；端口把它们收进构造函数，于是 <see cref="PlaytestSettings"/>
/// 的任何实例都已经是合法值，滑条与结果窗都不必再防越界。
/// </para>
/// <para>
/// 默认值照参考：跑 1 轮、每轮最多 336 天（48 周）、动画最快（10）、动画与音效都开。
/// </para>
/// </remarks>
public sealed record PlaytestSettings
{
    /// <summary>Fewest playthroughs, the <c>1</c> of <see cref="PlaytestSettings"/>.</summary>
    public const int MinimumPlaythroughs = 1;

    /// <summary>Most playthroughs, <c>AutoPlaytest::playthroughLimit</c>, <c>100</c>.</summary>
    public const int PlaythroughLimit = 100;

    /// <summary>Lowest animation speed, <c>1</c>.</summary>
    public const int MinimumAnimationSpeed = 1;

    /// <summary>Highest animation speed, <c>AutoPlaytest::animationLimit</c>, <c>10</c>.</summary>
    public const int AnimationSpeedLimit = 10;

    /// <summary>Days a playthrough runs by default, <c>_maxDaysInPlaythrough</c> of the reference.</summary>
    public const int DefaultDays = 336;

    /// <summary>
    /// Creates the settings, clamping each number into its slider's range the way the three setters do.
    /// </summary>
    /// <param name="maxPlaythroughs">Playthroughs to run, clamped to 1…<see cref="PlaythroughLimit"/>.</param>
    /// <param name="maxDays">Days per playthrough, clamped to 1…<see cref="MapPlaytest.DayLimit"/>.</param>
    /// <param name="animationSpeed">Animation speed, clamped to 1…<see cref="AnimationSpeedLimit"/>.</param>
    /// <param name="animationEnabled">Whether the playthroughs are drawn at all.</param>
    /// <param name="environmentSounds">Whether the playthroughs play their sounds.</param>
    public PlaytestSettings(
        int maxPlaythroughs = MinimumPlaythroughs,
        int maxDays = DefaultDays,
        int animationSpeed = AnimationSpeedLimit,
        bool animationEnabled = true,
        bool environmentSounds = true)
    {
        MaxPlaythroughs = Math.Clamp(maxPlaythroughs, MinimumPlaythroughs, PlaythroughLimit);
        MaxDays = Math.Clamp(maxDays, MapPlaytest.MinimumDays, MapPlaytest.DayLimit);
        AnimationSpeed = Math.Clamp(animationSpeed, MinimumAnimationSpeed, AnimationSpeedLimit);
        AnimationEnabled = animationEnabled;
        EnvironmentSounds = environmentSounds;
    }

    /// <summary>Playthroughs to run, <c>getMaxPlaythroughs()</c>.</summary>
    public int MaxPlaythroughs { get; }

    /// <summary>Days each playthrough may last, <c>getMaxDaysInPlaythrough()</c>.</summary>
    public int MaxDays { get; }

    /// <summary>How fast the playthroughs are drawn, <c>getAnimationSpeed()</c>.</summary>
    public int AnimationSpeed { get; }

    /// <summary>Whether the playthroughs are drawn, <c>isAnimationEnabled()</c>.</summary>
    public bool AnimationEnabled { get; }

    /// <summary>Whether the playthroughs play sounds, <c>areEnvironmentSoundsEnabled()</c>.</summary>
    public bool EnvironmentSounds { get; }
}

/// <summary>
/// The statistics of a series of playthroughs, the tally <c>displayResults()</c> builds
/// (<c>game_auto_playtest.cpp:107-175</c>).
/// </summary>
/// <remarks>
/// 参考先看每一轮有没有 <c>TIME_LIMIT</c>：有就整轮记进「到达天数上限」的计数并从胜率里剔除，否则把
/// 每一轮的 <c>WINNER</c> 累加到对应颜色。所以胜率的分母是 <b>总轮数</b>而不是「分出胜负的轮数」——一轮
/// 打满天数上限会让所有人的百分比一起下降，这是参考的口径，不是笔误。
/// </remarks>
public sealed class PlaytestSummary
{
    /// <summary>
    /// Counts the results of <paramref name="reports"/>. The first report decides which colours are listed
    /// and in what order, exactly like the reference taking its player row from <c>getResults().front()</c>.
    /// </summary>
    /// <param name="reports">One entry per playthrough, in the order they were run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reports"/> is <see langword="null"/>.</exception>
    public PlaytestSummary(IReadOnlyList<PlaytestReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        Reports = reports;
        TimeLimitCount = reports.Count(report => report.Players.Any(player => player.State == PlaytestPlayerState.TimeLimit));

        if (reports.Count == 0)
        {
            Colors = [];
            return;
        }

        List<PlaytestColorResult> colors = [];

        foreach (PlaytestPlayerResult player in reports[0].Players)
        {
            int wins = reports.Count(report =>
                report is { Players: var players }
                && !players.Any(entry => entry.State == PlaytestPlayerState.TimeLimit)
                && players.Any(entry => entry.Color == player.Color && entry.State == PlaytestPlayerState.Winner));

            colors.Add(new PlaytestColorResult(player.Color, wins, reports.Count));
        }

        Colors = colors;
    }

    /// <summary>The playthroughs that were counted.</summary>
    public IReadOnlyList<PlaytestReport> Reports { get; }

    /// <summary>How many playthroughs were run.</summary>
    public int Count => Reports.Count;

    /// <summary>How many playthroughs stopped at the day limit, <c>playthroughByTimeLimit</c>.</summary>
    public int TimeLimitCount { get; }

    /// <summary>One line per colour, in the order the first playthrough listed them.</summary>
    public IReadOnlyList<PlaytestColorResult> Colors { get; }

    /// <summary>An empty summary, what a playtest that never ran produces.</summary>
    public static PlaytestSummary Empty { get; } = new([]);
}


/// <summary>
/// Plays a map from start to a day limit with every kingdom run by the computer, the driver of the editor's
/// AUTO PLAYTEST (<c>editor_interface.cpp:2144-2150</c>, <c>game_auto_playtest.cpp:280-360</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的做法是「让 <c>Game::StartGame()</c> 自己跑，跑到天数上限就收」，因为试玩模式里所有玩家都是电脑、
/// 循环不需要等人。端口把这件事拆成这个驱动器：新开一个 <see cref="World"/> 装地图，让
/// <see cref="AdventureSession"/> 把每一位都当电脑跑，直到只剩一个王国、或者天数上限到了为止。
/// </para>
/// <para>
/// <b>不动正在跑的世界</b>：整个试玩期间 <see cref="World.Current"/> 指向这份临时的世界，结束时换回原来那个
/// ——参考同样是「拷一份地图、跑完恢复」（<c>editor_interface.cpp:3630-3660</c>）。
/// </para>
/// <para>
/// <b>没有人类玩家也能跑</b>：<see cref="AdventureSession.Start"/> 要求至少一位人类（那是给玩家开局用的），
/// 试玩走 <see cref="AdventureSession.StartPlaytest"/>，它允许全员电脑。
/// </para>
/// </remarks>
public static class MapPlaytest
{
    /// <summary>Fewest days a playthrough may run, the <c>1</c> of the reference's day slider.</summary>
    public const int MinimumDays = 1;

    /// <summary>Most days a playthrough may run, <c>AutoPlaytest::dayLimit</c>, <c>3360</c> (ten game years).</summary>
    public const int DayLimit = 3360;

    /// <summary>
    /// Plays <paramref name="settings"/> playthroughs of <paramref name="map"/> and tallies the results,
    /// <c>runPlayTest()</c> (<c>game_auto_playtest.cpp:258-358</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考每一轮都从头 <c>prepareMap()</c> 一次（重读地图、重摆玩家与职业），端口每轮都调一次
    /// <see cref="Run"/>，它同样新开世界、重载地图，所以两轮的初始局面一致；不同的只有 AI 抽到的随机数。
    /// </para>
    /// <para>
    /// 统计交给 <see cref="PlaytestSummary"/>，口径见那个类的说明。
    /// </para>
    /// </remarks>
    /// <param name="map">The map to play, usually the one the editor is editing.</param>
    /// <param name="settings">How many playthroughs, how long each one may last.</param>
    /// <param name="difficulty">Starting resources difficulty; the map header's own when omitted.</param>
    /// <returns>The tally of every playthrough, plus the reports themselves.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="map"/> or <paramref name="settings"/> is <see langword="null"/>.
    /// </exception>
    public static PlaytestSummary RunMany(Mp2Map map, PlaytestSettings settings, GameDifficulty? difficulty = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(settings);

        List<PlaytestReport> reports = [];

        for (int playthrough = 0; playthrough < settings.MaxPlaythroughs; ++playthrough)
        {
            reports.Add(Run(map, settings.MaxDays, difficulty));
        }

        PlaytestSummary summary = new(reports);

        GameLog.Info(
            $"Auto playtest finished: {summary.Count} playthrough(s), {summary.TimeLimitCount} hit the day limit.");

        return summary;
    }

    /// <summary>
    /// Plays one playthrough of <paramref name="map"/>.
    /// </summary>
    /// <param name="map">The map to play, usually the one the editor is editing.</param>
    /// <param name="maxDays">Day the run stops at when nobody has won yet.</param>
    /// <param name="difficulty">Starting resources difficulty; the map header's own when omitted.</param>
    /// <returns>One line per player.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDays"/> is less than <see cref="MinimumDays"/>.</exception>
    public static PlaytestReport Run(Mp2Map map, int maxDays, GameDifficulty? difficulty = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDays, MinimumDays);

        // 试玩里没有人在键盘前，地图头里「谁可以让人玩」的那张表必须清空：留着它，
        // ComputerColorsOf() 会把其中一位当成人类，World 建出来的王国就不是全 AI，轮到那一位时
        // StartPlaytest 的轮转表里没有它的 AI 回合可跑，试玩原地不动。复制一份地图再换头部，
        // 调用方手上那张（编辑器正在编辑的）保持原样。
        Mp2Map playtestMap = map with { Info = map.Info with { HumanPlayers = PlayerColor.None } };

        World previous = World.Current;

        try
        {
            var world = new World();

            World.Current = world;

            world.LoadMap(playtestMap, difficulty);

            PlaytestReport report = Play(world, playtestMap.Info, maxDays);

            GameLog.Info(
                $"Auto playtest of '{map.Info.Name}' stopped on day {report.Day}; " +
                $"{report.Players.Count(player => player.State == PlaytestPlayerState.Winner)} winner(s).");

            return report;
        }
        finally
        {
            World.Current = previous;
        }
    }

    /// <summary>Runs the turns until the day limit or a single surviving kingdom.</summary>
    /// <param name="world">The world the playtest loaded the map into.</param>
    /// <param name="info">The map header, the session's input.</param>
    /// <param name="maxDays">Day the run stops at.</param>
    /// <returns>The report.</returns>
    private static PlaytestReport Play(World world, Mp2FileInfo info, int maxDays)
    {
        AdventureSession? session = AdventureSession.StartPlaytest(world, info);

        if (session is null)
        {
            // 没有任何在局中的玩家：立刻按上限收摊，结果里只有空表。
            GameLog.Warn("The map has no player to play, the playtest stops at once.");

            return new PlaytestReport(world.DayCount, []);
        }

        PlayerColor colors = world.Kingdoms.GetColors();

        // 每一轮至少推进一位玩家，所以这个上限只是「不可能无限循环」的保险：天数上限乘以玩家数。
        long guard = (long)maxDays * Math.Max(1, PlayerColors.Count(colors)) + 4;

        while (world.DayCount <= (uint)maxDays && PlayerColors.Count(AliveColors(world, colors)) > 1 && guard-- > 0)
        {
            if (session.IsAiTurn)
            {
                _ = session.RunAiTurn();
            }

            session.EndTurn();
        }

        // EndTurn 走完一轮会立刻把日期推到上限 + 1，而那一天还没有任何玩家行动过：报告里的最后一天
        // 是玩家真的玩过的那一天。
        uint lastDay = world.DayCount > (uint)maxDays ? (uint)maxDays : world.DayCount;

        return BuildReport(world, colors, lastDay);
    }

    /// <summary>The colours that still have a kingdom in play.</summary>
    /// <param name="world">The world.</param>
    /// <param name="colors">The colours that started the game.</param>
    /// <returns>A mask of the kingdoms that have not been wiped out.</returns>
    private static PlayerColor AliveColors(World world, PlayerColor colors)
    {
        PlayerColor alive = PlayerColor.None;

        foreach (PlayerColor color in PlayerColors.ToList(colors))
        {
            if (world.GetKingdom(color).IsPlay)
            {
                alive |= color;
            }
        }

        return alive;
    }

    /// <summary>Turns the state of the world into the report of one playthrough.</summary>
    /// <param name="world">The world after the last turn.</param>
    /// <param name="colors">The colours that started the game.</param>
    /// <param name="lastDay">The last day a player actually played.</param>
    /// <returns>The report.</returns>
    private static PlaytestReport BuildReport(World world, PlayerColor colors, uint lastDay)
    {
        PlayerColor alive = AliveColors(world, colors);
        bool hasWinner = PlayerColors.Count(alive) == 1;

        List<PlaytestPlayerResult> players = [];

        foreach (PlayerColor color in PlayerColors.ToList(colors))
        {
            PlaytestPlayerState state = (alive & color) == 0
                ? PlaytestPlayerState.Loser
                : hasWinner
                    ? PlaytestPlayerState.Winner
                    : PlaytestPlayerState.TimeLimit;

            players.Add(new PlaytestPlayerResult(color, state, lastDay));
        }

        return new PlaytestReport(lastDay, players);
    }
}
