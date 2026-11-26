using FHeroes2.App.Fonts;
using FHeroes2.App.Localization;
using FHeroes2.Assets;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Highscores;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// A finished game that still has to be entered on the board, the data the reference passes to
/// <c>openHighScores()</c> from <c>GameOver::Result</c> and <c>world</c> (<c>game_highscores.cpp:296-337</c>).
/// </summary>
/// <param name="ScenarioName">The map name, or the campaign name of a finished campaign.</param>
/// <param name="DayCount">How many days the game lasted, <c>world.CountDay()</c> or <c>getDaysPassed()</c>.</param>
/// <param name="Rating">The score, from <see cref="HighScoreBoard.GetStandardRating"/> or <see cref="HighScoreBoard.GetCampaignRating"/>.</param>
/// <param name="MapSeed">The seed of the map, <c>world.GetMapSeed()</c>.</param>
public readonly record struct HighScoreCompletion(string ScenarioName, uint DayCount, uint Rating, uint MapSeed);

/// <summary>
/// The high score screen as a window the host pumps until it closes, <c>Game::DisplayHighScores()</c>
/// (<c>game_highscores.cpp:462-476</c>) plus the name prompt of a fresh win (<c>:286-347</c>).
/// </summary>
/// <remarks>
/// <para>
/// 与参考的差别只有一处：参考的 <c>Dialog::inputString()</c> 是阻塞调用，自己转循环；端口不能在一层模态里再开一层
/// （<see cref="GameHost.RunModal"/> 拒绝嵌套），所以名字输入框以 <see cref="InputStringView"/> 的形式压在本屏上，
/// 输入、光标、闪烁都先交给它，关掉之后再登记成绩。对玩家是同一个顺序：先问名字，再把新行用黄色显示出来。
/// </para>
/// <para>
/// 换榜不再重建屏幕：参考靠 <see cref="GameHost.RunModal"/> 外层的循环再开一次，端口在同一屏里换表
/// （<see cref="HighScoresView.SetTable"/>），像素和参考的第二次 <c>openHighScores()</c> 一致。
/// </para>
/// <para>
/// <b>右键帮助</b>是参考的 <c>showStandardTextMessage()</c>，端口的模态里没有消息窗，只写一行日志（见
/// <see cref="LastHelpText"/> 供测试与宿主查询）。
/// </para>
/// </remarks>
internal sealed class HighScoresScreen : IModalScreen
{
    /// <summary>Body of the name prompt, <c>_( "Your Name" )</c>.</summary>
    public const string PlayerNameMessageId = "Your Name";

    /// <summary>Help of the exit button, <c>_( "Exit this menu." )</c>.</summary>
    public const string ExitHelpMessageId = "Exit this menu.";

    /// <summary>Title of the help of the switch button on the standard table, <c>_( "Campaign" )</c>.</summary>
    public const string CampaignHelpTitleId = "Campaign";

    /// <summary>Body of the help of the switch button on the standard table.</summary>
    public const string CampaignHelpMessageId = "View High Scores for Campaigns.";

    /// <summary>Title of the help of the switch button on the campaign table, <c>_( "Standard" )</c>.</summary>
    public const string StandardHelpTitleId = "Standard";

    /// <summary>Body of the help of the switch button on the campaign table.</summary>
    public const string StandardHelpMessageId = "View High Scores for Standard Maps.";

    private static readonly Point OffScreen = new(-1, -1);

    private readonly HighScoresView _view;
    private readonly HighScoreBoard _board;
    private readonly HighScoreCompletion? _completion;

    private InputStringView? _nameInput;
    private Point? _cursor;
    private bool _leftDown;
    private readonly GameText? _text;

    private HighScoresScreen(
        HighScoresView view,
        HighScoreBoard board,
        InputStringView? nameInput,
        HighScoreCompletion? completion,
        GameText? text)
    {
        _view = view;
        _board = board;
        _nameInput = nameInput;
        _completion = completion;
        _text = text;
    }

    /// <inheritdoc/>
    public bool IsFinished { get; private set; }

    /// <summary>The screen below, for the host's own drawing checks.</summary>
    public HighScoresView View => _view;

    /// <summary>The tables being shown.</summary>
    public HighScoreBoard Board => _board;

    /// <summary>Whether the name prompt is still on top of the table.</summary>
    public bool IsNaming => _nameInput is not null;

    /// <summary>The help text of the last right click, or <see langword="null"/> when none was asked for.</summary>
    public string? LastHelpText { get; private set; }

    /// <summary>
    /// Builds the screen, reads the tables once and — for a finished game that still makes the board — asks for the name.
    /// </summary>
    /// <param name="assets">The archives the artwork comes from.</param>
    /// <param name="fonts">The fonts of the rows, the title and the name prompt.</param>
    /// <param name="screen">The screen to centre the background on.</param>
    /// <param name="board">The tables; its mode is set to <paramref name="isCampaign"/>.</param>
    /// <param name="isCampaign">Whether the campaign table is shown first.</param>
    /// <param name="error">Why the screen cannot be shown, or <see langword="null"/>.</param>
    /// <param name="campaignAvailable">
    /// Whether the switch button is enabled, <c>Game::isSuccessionWarsCampaignPresent()</c>.
    /// </param>
    /// <param name="completion">The finished game to enter, or <see langword="null"/> when only viewing.</param>
    /// <param name="text">Translations of the name prompt; the English ids are used without one.</param>
    /// <returns>The screen, or <see langword="null"/> when the artwork is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/>, <paramref name="fonts"/> or <paramref name="board"/> is <see langword="null"/>.</exception>
    public static HighScoresScreen? TryCreate(
        AssetBundle assets,
        GameFonts fonts,
        Size screen,
        HighScoreBoard board,
        bool isCampaign,
        out string? error,
        bool campaignAvailable = true,
        HighScoreCompletion? completion = null,
        GameText? text = null)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(board);

        error = null;

        HighScoresArtwork? artwork = HighScoresArtwork.TryCreate(assets);

        if (artwork is null)
        {
            error = "The game data lacks HSBKG, HISCORE or MINIMON, the artwork of the high score screen.";

            return null;
        }

        // :267-276：文件只读一次；读不出来（第一次玩）就用十行默认榜。
        bool loaded = board.Load();

        board.Mode = isCampaign ? HighScoreMode.Campaign : HighScoreMode.Standard;

        GameLog.Info(
            $"High score screen: {(isCampaign ? "campaign" : "standard")} table, {board.Entries.Count} rows, "
            + (loaded ? $"read from '{board.FileName}'." : $"default table written for '{board.FileName}'."));

        HighScoresView? view = HighScoresView.TryCreate(
            board.Mode,
            board.Entries,
            artwork,
            new HighScoresFonts(fonts.White, fonts.Yellow),
            selectedIndex: -1,
            campaignAvailable: campaignAvailable,
            screen: screen);

        if (view is null)
        {
            error = "The high score screen could not be composed from the archives.";

            return null;
        }

        InputStringView? nameInput = null;
        HighScoreCompletion? pending = null;

        if (completion is { } finished)
        {
            if (board.Accepts(finished.Rating))
            {
                string body = text?.Get(PlayerNameMessageId) ?? PlayerNameMessageId;
                InputStringDialog dialog = InputStringDialog.Create(
                    string.Empty, body, HighScoreBoard.PlayerNameLength);

                nameInput = InputStringView.TryCreate(dialog, fonts.Yellow, fonts.White, screen);

                if (nameInput is null)
                {
                    // 输入框缺图（BUYBUIL 第 3 帧或 SYSTEM 的两对按钮）时不能让成绩丢掉：直接记默认名。
                    GameLog.Warn("The name prompt cannot be shown; the game is entered as the unknown hero.");

                    int index = board.Register(
                        finished.ScenarioName, string.Empty, finished.DayCount, finished.Rating, finished.MapSeed);

                    Save(board, index, finished);

                    view.SetTable(board.Mode, board.Entries, index);
                }
                else
                {
                    pending = finished;
                }
            }
            else
            {
                // :309-330：没上榜就直接回主菜单，参考连名字都不问。
                GameLog.Info(
                    $"The score {finished.Rating} does not make the "
                    + $"{(isCampaign ? "campaign" : "standard")} table; the board is shown unchanged.");
            }
        }

        return new HighScoresScreen(view, board, nameInput, pending, text);
    }

    /// <inheritdoc/>
    public void SetCursor(Point? position)
    {
        _cursor = position;

        if (_nameInput is { } input)
        {
            input.SetCursor(position);

            return;
        }

        _view.SetCursor(position);
    }

    /// <inheritdoc/>
    public void SetLeftButton(bool down)
    {
        Point point = _cursor ?? OffScreen;

        if (_nameInput is { } input)
        {
            // 输入框压在榜上时，点击只找它的输入区与两颗按钮（参考的阻塞 inputString 自己转循环）。
            bool pressed = down && !_leftDown;
            _leftDown = down;

            input.SetLeftButton(down);

            if (pressed)
            {
                input.ClickLeft(point);
            }

            CloseNameInputIfDone();

            return;
        }

        _leftDown = down;

        switch (_view.SetLeftButton(down, point))
        {
            case HighScoresAction.Exit:
                GameLog.Info("The high score screen was closed by its exit button.");
                IsFinished = true;
                break;

            case HighScoresAction.SwitchMode:
                SwitchMode();
                break;

            default:
                break;
        }
    }

    /// <inheritdoc/>
    public void SetRightButton(bool down)
    {
        if (!down || _nameInput is not null)
        {
            return;
        }

        HighScoresHelpTarget target = _view.ProcessRightClick(_cursor ?? OffScreen);

        LastHelpText = target switch
        {
            HighScoresHelpTarget.Exit => GetText(ExitHelpMessageId),
            HighScoresHelpTarget.Other => GetText(
                _view.Mode == HighScoreMode.Campaign ? StandardHelpMessageId : CampaignHelpMessageId),
            _ => null,
        };

        if (LastHelpText is { } help)
        {
            GameLog.Info($"High score screen help: {help}");
        }
    }

    /// <inheritdoc/>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (_nameInput is { } input)
        {
            input.ProcessHotKey(hotKey);
            CloseNameInputIfDone();

            return;
        }

        // 参考的循环只有 Game::HotKeyCloseWindow() 一个热键（ESC）能关掉这一屏。
        if (hotKey == ButtonHotKey.Cancel)
        {
            GameLog.Info("The high score screen was closed by ESC.");
            IsFinished = true;
        }
    }

    /// <inheritdoc/>
    public void PressKey(Key key, KeyModifier modifier)
    {
        _nameInput?.PressKey(key, modifier);
    }

    /// <inheritdoc/>
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        if (_nameInput is { } input)
        {
            input.Update();

            return;
        }

        _view.Update();
    }

    /// <inheritdoc/>
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _view.Draw(frame.Pixels);
        _nameInput?.Draw(frame.Pixels);

        frame.InvalidateAll();
    }

    /// <summary>Switches to the other table and updates the two buttons with it, <c>game_highscores.cpp:424-426</c>.</summary>
    private void SwitchMode()
    {
        HighScoreMode mode = _view.Mode == HighScoreMode.Campaign
            ? HighScoreMode.Standard
            : HighScoreMode.Campaign;

        _board.Mode = mode;
        _view.SetTable(mode, _board.Entries, -1);

        GameLog.Info(
            $"The high score screen switched to the {(mode == HighScoreMode.Campaign ? "campaign" : "standard")} table.");
    }

    /// <summary>Registers the finished game once the name prompt has closed, <c>:317-341</c>.</summary>
    private void CloseNameInputIfDone()
    {
        if (_nameInput is not { IsClosed: true } input)
        {
            return;
        }

        string? typed = input.Dialog.Result;
        _nameInput = null;

        if (_completion is not { } finished)
        {
            return;
        }

        int index = _board.Register(
            finished.ScenarioName, typed ?? string.Empty, finished.DayCount, finished.Rating, finished.MapSeed);

        Save(_board, index, finished);

        _view.SetTable(_board.Mode, _board.Entries, index);
    }

    /// <summary>Writes the tables back, the <c>highScoreDataContainer.save()</c> of <c>:340-341</c>.</summary>
    private static void Save(HighScoreBoard board, int index, in HighScoreCompletion completion)
    {
        bool saved = board.Save();
        string table = board.Mode == HighScoreMode.Campaign ? "campaign" : "standard";

        GameLog.Info(
            $"High score '{completion.ScenarioName}' ({completion.DayCount} days, {completion.Rating}) "
            + (index < 0 ? $"fell off the {table} table." : $"took row {index + 1} of the {table} table.")
            + (saved ? $" '{board.FileName}' written." : $" '{board.FileName}' could not be written."));
    }

    private string GetText(string messageId) => _text?.Get(messageId) ?? messageId;
}
