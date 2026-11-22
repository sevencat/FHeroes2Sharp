using FHeroes2.Assets.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// What the player asked the game to switch to when a scenario ended, the four
/// <c>fheroes2::GameMode</c> values <c>GameOver::Result::checkGameOver()</c> can return.
/// </summary>
/// <remarks>
/// <para>
/// 参考回的是整个 <c>fheroes2::GameMode</c> 枚举（二十多个场景）；这里只留它在这一处真正会返回的四个。端口的场景切换由
/// App 的 <c>SceneKind</c> 负责，游戏层不该认识主菜单与高分榜界面，所以这是「游戏层回答、界面层解释」的最小集合。
/// </para>
/// </remarks>
public enum GameOverMode
{
    /// <summary>The game goes on, <c>GameMode::CANCEL</c>.</summary>
    Cancel,

    /// <summary>The player lost and goes back to the main menu, <c>GameMode::MAIN_MENU</c>.</summary>
    MainMenu,

    /// <summary>The player won and the standard high score screen follows, <c>GameMode::HIGHSCORES_STANDARD</c>.</summary>
    HighScoresStandard,

    /// <summary>A campaign scenario is done and the next one may be chosen, <c>GameMode::COMPLETE_CAMPAIGN_SCENARIO</c>.</summary>
    CompleteCampaignScenario,
}

/// <summary>
/// What <see cref="GameOverResult.CheckGameOver"/> would have shown the player, collected for the caller to serve.
/// </summary>
/// <remarks>
/// <para>
/// 参考在 <c>checkGameOver()</c> 里直接弹窗、直接放片子、直接重置音频。游戏层不能画像素也不能放音乐，于是这些副作用被
/// 拆成一个纯数据的对象：<see cref="Vanquished"/> 对应 <c>Game::DialogPlayers()</c>，<see cref="LossNotice"/> 与
/// <see cref="WinsNotice"/> 对应 <c>DialogLoss()</c> / <c>DialogWins()</c>，三个 <c>Video</c>/<c>Music</c> 标志对应
/// 参考的 <c>Video::ShowVideo()</c> 与 <c>AudioManager</c>。
/// </para>
/// <para>
/// 顺序有意义：参考对被消灭的玩家逐个弹窗，全部弹完才判自己的胜负，所以 <see cref="Vanquished"/> 是列表而不是单个颜色。
/// </para>
/// </remarks>
public sealed class GameOverRequests
{
    private readonly List<PlayerColor> _vanquished = [];

    /// <summary>
    /// The colors that were eliminated during this call, each of them a <c>DialogPlayers()</c> of its own.
    /// </summary>
    public IReadOnlyList<PlayerColor> Vanquished => _vanquished;

    /// <summary>The condition to explain in the loss dialog, <c>DialogLoss( result )</c>; <see langword="null"/> for none.</summary>
    public uint? LossNotice { get; internal set; }

    /// <summary>The condition to celebrate in the victory dialog, <c>DialogWins( result )</c>; <see langword="null"/> for none.</summary>
    public uint? WinsNotice { get; internal set; }

    /// <summary>Whether the losing movie plays, <c>Video::ShowVideo( "LOSE.SMK", PLAY_CUTSCENE_LOOP )</c>.</summary>
    public bool LoseVideo { get; internal set; }

    /// <summary>Whether the winning movie plays, <c>Video::ShowVideo( "WIN.SMK", PLAY_CUTSCENE_WAIT )</c>.</summary>
    public bool WinVideo { get; internal set; }

    /// <summary>
    /// Whether the victory music starts before the high score screen, <c>AudioManager::PlayMusicAsync( MUS::VICTORY )</c>.
    /// </summary>
    public bool VictoryMusic { get; internal set; }

    /// <summary>Whether there is nothing for the caller to do.</summary>
    public bool IsEmpty =>
        _vanquished.Count == 0
        && LossNotice is null
        && WinsNotice is null
        && !LoseVideo
        && !WinVideo
        && !VictoryMusic;

    /// <summary>Adds a color to <see cref="Vanquished"/>; the verdict is the only producer of these.</summary>
    /// <param name="color">The color that just left the game.</param>
    internal void AddVanquished(PlayerColor color) => _vanquished.Add(color);

    /// <summary>Forgets everything collected so far.</summary>
    public void Clear()
    {
        _vanquished.Clear();
        LossNotice = null;
        WinsNotice = null;
        LoseVideo = false;
        WinVideo = false;
        VictoryMusic = false;
    }
}

/// <summary>
/// The end-of-scenario verdict, <c>GameOver::Result</c> (<c>game_over.cpp:464-702</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考是个单例（<c>Result::Get()</c> 里的 <c>static Result gresult</c>），这里照 <see cref="World.Current"/> 的样式给出
/// <see cref="Current"/>，同时保留公开构造函数，测试可以直接造一个不受全局状态影响的实例。
/// </para>
/// <para>
/// <b>为什么一次调用可能「没结论」。</b>多人局里一个玩家全灭不代表整局结束：只要还有别的人类玩家在局中，参考只弹一个
/// 淘汰通知就回 <c>CANCEL</c>，等下一个玩家回合再判。所以 <see cref="CheckGameOver"/> 的返回值与
/// <see cref="Requests"/> 必须一起看，只看返回值会漏掉「有人出局」这件事。
/// </para>
/// </remarks>
public sealed partial class GameOverResult
{
    private PlayerColor _colors = PlayerColor.None;

    /// <summary>The verdict of the running game, <c>GameOver::Result::Get()</c>.</summary>
    public static GameOverResult Current { get; set; } = new();

    /// <summary>
    /// The colors still in the game as far as the verdict is concerned, <c>_colors</c>; seeded by <see cref="Reset"/> and
    /// pruned by every <see cref="CheckGameOver"/>.
    /// </summary>
    public PlayerColor Colors => _colors;

    /// <summary>
    /// The condition the last <see cref="CheckGameOver"/> found, <c>result</c>; <see cref="GameOver.ConditionNone"/> after
    /// <see cref="Reset"/> and after a verdict that found nothing.
    /// </summary>
    public uint Condition { get; private set; } = GameOver.ConditionNone;

    /// <summary>What the caller still has to show for the last verdict.</summary>
    public GameOverRequests Requests { get; } = new();

    /// <summary>
    /// Starts the bookkeeping over for a new scenario, <c>Result::Reset()</c> (<c>game_over.cpp:475-479</c>).
    /// </summary>
    /// <remarks>
    /// 参考取的是 <c>Game::GetKingdomColors()</c>，而那个函数就是 <c>Settings::Get().GetPlayers().GetColors()</c>
    /// （<c>game.cpp:451</c>）——玩家表里的颜色，不是 <c>World::GetKingdomColors()</c> 那套「地图上实际还在的颜色」，
    /// 两者在开局、读档和玩家中途出局之后都可能不同。
    /// </remarks>
    public void Reset()
    {
        _colors = Settings.Get().Players.GetColors();
        Condition = GameOver.ConditionNone;

        Requests.Clear();
    }

    /// <summary>
    /// Decides whether the scenario ended for somebody, <c>Result::checkGameOver()</c>
    /// (<c>game_over.cpp:481-682</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 三件事按参考的顺序做：先把出局的颜色挑出来并从 <see cref="Colors"/> 里摘掉（每个出局者一条
    /// <see cref="GameOverRequests.Vanquished"/>），再按单机/多人分两套规则判胜负，最后把要弹的窗口与要放的片子放进
    /// <see cref="Requests"/>。
    /// </para>
    /// <para>
    /// <b>单机</b>先判负再判胜，因为参考的注释写得很清楚：王国已经没了就无所谓别的条件。全灭导致的失败有一个例外——若
    /// 王国还有城、或者「丢城倒计时」还没归零，说明玩家是被 <c>Interface::AdventureMap::HumanTurn()</c> 的倒计时收走的，
    /// 那个流程自己已经提示过，这里不再弹第二次。
    /// </para>
    /// <para>
    /// <b>多人</b>只判当前颜色的胜负，而且 <c>LOSS_TOWN</c> 在多人局里根本不触发（参考的注释：还不支持）。全灭本身不是
    /// 终局，除非所有人类玩家都已出局；淘汰通知也只弹一次——靠「调用时当前玩家是否还在 <see cref="Colors"/> 里」判断，
    /// 这就是 <c>isCurrentPlayerWasActive</c> 的用处。
    /// </para>
    /// <para>
    /// 参考的 <c>TYPE_AUTO_PLAYTEST</c> 分支（把结果记进 <c>AutoPlaytest</c> 单例）没有移植：端口没有那个单例，也没有
    /// 自动试玩模式。它只影响「谁来记战报」，不影响判定。
    /// </para>
    /// </remarks>
    /// <returns>What the caller should switch to; <see cref="GameOverMode.Cancel"/> when the game goes on.</returns>
    public GameOverMode CheckGameOver()
    {
        Settings settings = Settings.Get();
        World world = World.Current;

        PlayerColor humanColors = Players.HumanColors();
        bool isSinglePlayer = PlayerColors.Count(humanColors) == 1;
        PlayerColor currentColor = settings.Players.CurrentColor;
        bool wasCurrentPlayerActive = (_colors & currentColor) != 0;

        Requests.Clear();

        int activeHumanColors = 0;

        foreach (PlayerColor color in PlayerColors.Ordered)
        {
            if ((_colors & color) == 0)
            {
                continue;
            }

            if (!world.GetKingdom(color).IsPlay)
            {
                // 参考 game_over.cpp:499-506：电脑出局一定要说；人类出局只在「多人局里不是当前玩家」时说，
                // 当前玩家自己的出局由后面的失败对话框解释。
                if ((humanColors & color) == 0 || (!isSinglePlayer && color != currentColor))
                {
                    Requests.AddVanquished(color);
                }

                _colors = (PlayerColor)(_colors & ~color);
            }
            else if ((humanColors & color) != 0)
            {
                ++activeHumanColors;
            }
        }

        if (isSinglePlayer)
        {
            return CheckSinglePlayer(world, humanColors);
        }

        return CheckMultiPlayer(world, currentColor, activeHumanColors, wasCurrentPlayerActive);
    }

    /// <summary>The single player half of <see cref="CheckGameOver"/>, <c>game_over.cpp:515-567</c>.</summary>
    /// <param name="world">The world being judged.</param>
    /// <param name="humanColors">The colors a person plays; exactly one bit here.</param>
    /// <returns>The verdict.</returns>
    private GameOverMode CheckSinglePlayer(World world, PlayerColor humanColors)
    {
        Kingdom.Kingdom kingdom = world.GetKingdom(humanColors);
        bool isAiAutoControlMode = Players.Get(humanColors)?.IsAiAutoControlMode ?? false;

        if (!kingdom.IsControlHuman && !isAiAutoControlMode)
        {
            return GameOverMode.Cancel;
        }

        // 先判负：王国已经没了（或倒计时/被占目标城之类的条件满足）时，赢的条件再成立也不算数。
        Condition = !kingdom.IsPlay ? GameOver.LossAll : world.CheckKingdomLoss(kingdom);

        if (Condition != GameOver.ConditionNone)
        {
            // 参考 game_over.cpp:537-541：全灭且无城、倒计时为 0 的情况已在 HumanTurn() 结尾提示过。
            if (Condition != GameOver.LossAll || kingdom.Castles.Count > 0 || kingdom.LostTownDays != 0)
            {
                Requests.LossNotice = Condition;
            }

            Requests.LoseVideo = true;

            return GameOverMode.MainMenu;
        }

        Condition = world.CheckKingdomWins(kingdom);

        if (Condition == GameOver.ConditionNone)
        {
            return GameOverMode.Cancel;
        }

        Requests.WinsNotice = Condition;

        if (Settings.Get().IsGameType(GameTypes.Campaign))
        {
            return GameOverMode.CompleteCampaignScenario;
        }

        Requests.WinVideo = true;
        Requests.VictoryMusic = true;

        return GameOverMode.HighScoresStandard;
    }

    /// <summary>The multiplayer half of <see cref="CheckGameOver"/>, <c>game_over.cpp:568-679</c>.</summary>
    /// <param name="world">The world being judged.</param>
    /// <param name="currentColor">The color whose turn it is.</param>
    /// <param name="activeHumanColors">How many human colors are still playing.</param>
    /// <param name="wasCurrentPlayerActive">Whether <paramref name="currentColor"/> was in <see cref="Colors"/> on entry.</param>
    /// <returns>The verdict.</returns>
    private GameOverMode CheckMultiPlayer(
        World world,
        PlayerColor currentColor,
        int activeHumanColors,
        bool wasCurrentPlayerActive)
    {
        Condition = EvaluateCurrentPlayer(world, currentColor, activeHumanColors);

        if ((Condition & GameOver.Loss) != 0)
        {
            // 参考 game_over.cpp:651-665：全灭本身不是终局，只有全部人类玩家都出局才收场。
            bool endGame = Condition != GameOver.LossAll || activeHumanColors == 0;

            if (ShouldShowLossNotice(world, currentColor, wasCurrentPlayerActive))
            {
                Requests.LossNotice = Condition;
            }

            if (endGame)
            {
                Requests.LoseVideo = true;

                return GameOverMode.MainMenu;
            }

            return GameOverMode.Cancel;
        }

        if ((Condition & GameOver.Wins) != 0)
        {
            Requests.WinsNotice = Condition;
            Requests.WinVideo = true;

            return GameOverMode.HighScoresStandard;
        }

        return GameOverMode.Cancel;
    }

    /// <summary>
    /// The condition of the player whose turn it is, the lambda of <c>game_over.cpp:569-611</c>.
    /// </summary>
    /// <param name="world">The world being judged.</param>
    /// <param name="currentColor">The color whose turn it is.</param>
    /// <param name="activeHumanColors">How many human colors are still playing.</param>
    /// <returns>The condition; <see cref="GameOver.ConditionNone"/> when nothing applies.</returns>
    private static uint EvaluateCurrentPlayer(World world, PlayerColor currentColor, int activeHumanColors)
    {
        if (activeHumanColors == 0)
        {
            return GameOver.LossAll;
        }

        // 当前颜色根本不在局中（例如它的回合还没开始就出局了）：没有可判的对象。
        if ((PlayerColors.All & currentColor) == 0)
        {
            return GameOver.ConditionNone;
        }

        Kingdom.Kingdom kingdom = world.GetKingdom(currentColor);
        bool isAiAutoControlMode = Players.Get(currentColor)?.IsAiAutoControlMode ?? false;

        if (!kingdom.IsControlHuman && !isAiAutoControlMode)
        {
            return GameOver.ConditionNone;
        }

        if (!kingdom.IsPlay)
        {
            return GameOver.LossAll;
        }

        uint condition = world.CheckKingdomWins(kingdom);

        if (condition != GameOver.ConditionNone)
        {
            return condition;
        }

        condition = world.CheckKingdomLoss(kingdom);

        // LOSS_TOWN 在多人局里还不支持；参考 game_over.cpp:605-608 只放行另外三种。
        if (condition == GameOver.LossHero || condition == GameOver.LossTime || (condition & GameOver.LossEnemyWins) != 0)
        {
            return condition;
        }

        return GameOver.ConditionNone;
    }

    /// <summary>
    /// Whether the loss dialog of a multiplayer game is due, the lambda of <c>game_over.cpp:618-649</c>.
    /// </summary>
    /// <param name="world">The world being judged.</param>
    /// <param name="currentColor">The color whose turn it is.</param>
    /// <param name="wasCurrentPlayerActive">Whether <paramref name="currentColor"/> was in <see cref="Colors"/> on entry.</param>
    /// <returns><see langword="true"/> when the player has to be told that he lost.</returns>
    private bool ShouldShowLossNotice(World world, PlayerColor currentColor, bool wasCurrentPlayerActive)
    {
        if ((PlayerColors.All & currentColor) == 0)
        {
            return false;
        }

        Kingdom.Kingdom kingdom = world.GetKingdom(currentColor);
        bool isAiAutoControlMode = Players.Get(currentColor)?.IsAiAutoControlMode ?? false;

        if (!kingdom.IsControlHuman && !isAiAutoControlMode)
        {
            return false;
        }

        if (Condition == GameOver.LossAll && kingdom.Castles.Count == 0 && kingdom.LostTownDays == 0)
        {
            return false;
        }

        // 全灭引起的失败会被反复判出来，只弹第一次。
        return wasCurrentPlayerActive;
    }
}
