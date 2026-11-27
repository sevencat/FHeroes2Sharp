using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The strings the auto playtest results window draws, <c>_( "autoPlaytest|..." )</c> of
/// <c>src/fheroes2/game/game_auto_playtest.cpp</c>.
/// </summary>
public sealed record AutoPlaytestResultsText
{
    /// <summary>Title of the window, <c>_( "Auto Playtest Results" )</c> (<c>:159</c>).</summary>
    public required string Title { get; init; }

    /// <summary>The word after the percentage, <c>%</c> of <c>:200</c>.</summary>
    public required string PercentSuffix { get; init; }

    /// <summary>The singular form, <c>_( "1 playthrough" )</c> (<c>:208</c>).</summary>
    public required string OnePlaythrough { get; init; }

    /// <summary>The plural form with the <c>%{count}</c> placeholder, <c>_( "%{count} playthroughs" )</c>.</summary>
    public required string PlaythroughsFormat { get; init; }

    /// <summary>Ending of the day limit line, <c>_( " playthrough(s) reached the specified time limit" )</c> (<c>:213</c>).</summary>
    public required string TimeLimitSuffix { get; init; }

    /// <summary>Title of the OKAY help (<c>:238</c>).</summary>
    public required string OkayTitle { get; init; }

    /// <summary>Body of the OKAY help (<c>:238</c>).</summary>
    public required string OkayHelp { get; init; }

    /// <summary>Title of a right click on a player icon (<c>:243</c>).</summary>
    public required string OpponentsTitle { get; init; }

    /// <summary>Body of a right click on a player icon, <c>%{color}</c> is filled with the colour's name.</summary>
    public required string ColorPlayerFormat { get; init; }

    /// <summary>Body of a right click on a percentage, <c>_( "Won %{percent}% of playthroughs." )</c> (<c>:250</c>).</summary>
    public required string WonFormat { get; init; }

    /// <summary>The untranslated strings of the reference.</summary>
    public static AutoPlaytestResultsText Default { get; } = new()
    {
        Title = "Auto Playtest Results",
        PercentSuffix = "%",
        OnePlaythrough = "1 playthrough",
        PlaythroughsFormat = "%{count} playthroughs",
        TimeLimitSuffix = " playthrough(s) reached the specified time limit",
        OkayTitle = "Okay",
        OkayHelp = "Click to close the dialog.",
        OpponentsTitle = "Opponents",
        ColorPlayerFormat = "%{color} player",
        WonFormat = "Won %{percent}% of playthroughs.",
    };

    /// <summary>Fills the colour name into <see cref="ColorPlayerFormat"/>.</summary>
    /// <param name="color">The player.</param>
    /// <returns>The text of the help box.</returns>
    public string ColorPlayer(PlayerColor color) =>
        ColorPlayerFormat.Replace("%{color}", PlayerColors.Describe(color), StringComparison.Ordinal);

    /// <summary>The line that counts the playthroughs, <c>_n()</c> of <c>:208</c>.</summary>
    /// <param name="count">How many playthroughs were run.</param>
    /// <returns>The line.</returns>
    public string PlaythroughCount(int count) =>
        count == 1
            ? OnePlaythrough
            : PlaythroughsFormat.Replace("%{count}", count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal);

    /// <summary>The line that counts the playthroughs that hit the day limit, <c>:213</c>.</summary>
    /// <param name="count">How many of them did.</param>
    /// <returns>The line.</returns>
    public string TimeLimitLine(int count) =>
        count.ToString(System.Globalization.CultureInfo.InvariantCulture) + TimeLimitSuffix;

    /// <summary>The help box of a percentage, <c>:249-252</c>.</summary>
    /// <param name="color">The player.</param>
    /// <param name="percentage">The win rate in whole percent.</param>
    /// <returns>The text.</returns>
    public string Won(PlayerColor color, int percentage) =>
        WonFormat
            .Replace("%{percent}", percentage.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace("%{color}", PlayerColors.Describe(color), StringComparison.Ordinal);
}

/// <summary>
/// The artwork the auto playtest results window is assembled from: the metallic title box, the player icons and
/// the single OKAY button.
/// </summary>
public sealed class AutoPlaytestResultsArtwork
{
    /// <summary>Frame 0 is the metallic box behind the title.</summary>
    public required IReadOnlyList<Sprite> TitleBoxFrames { get; init; }

    /// <summary><c>NGEXTRA</c>, for the colour icons and their shadow.</summary>
    public required IPlayersInfoArtwork PlayerIcons { get; init; }

    /// <summary>Source of the OKAY button.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window needs was supplied.</summary>
    public bool IsComplete => TitleBoxFrames.Count > 0;
}

/// <summary>
/// The tally of a series of playthroughs, <c>displayResults()</c> (<c>game_auto_playtest.cpp:107-256</c>).
/// </summary>
/// <remarks>
/// <para>
/// 500×220 的活动区。标题框离顶端 10；玩家图标排的起点是「标题框底 + 10」，整排按
/// <c>(active.width - playerCount * 80 + 18) / 2</c> 居中——那个 <c>+18</c> 是参考给最后一格留下的宽度，照抄。
/// 图标下面一行是胜率百分数（黄字、在图标宽度里居中），再往下 20 像素是轮数行、再 20 像素是天数上限行，两行都在
/// 活动区里居中。
/// </para>
/// <para>
/// <b>空结果不开窗</b>：参考的 <c>displayResults()</c> 第一句就是 <c>if ( getResults().empty() ) return;</c>，
/// 端口的 <see cref="TryCreate"/> 因此拒绝空表，调用方据此什么都不做。
/// </para>
/// <para>
/// 参考在这一屏还会放胜利音乐（<c>AudioManager::PlayMusic( MUS::VICTORY, ... )</c>）；端口没有音乐队列，
/// 这一步归调用方。
/// </para>
/// </remarks>
public sealed class AutoPlaytestResultsWindow
{
    /// <summary>Width of the active area, the <c>500</c> of <c>:178</c>.</summary>
    public const int WindowWidth = 500;

    /// <summary>Height of the active area, the <c>220</c> of <c>:178</c>.</summary>
    public const int WindowHeight = 220;

    /// <summary>Top offset of the title box, <c>activeArea.y + 10</c> of <c>:185</c>.</summary>
    public const int TitleOffset = 10;

    /// <summary>Horizontal distance between two players, <c>playerStepX</c> of <c>:70</c>.</summary>
    public const int PlayerStepX = 80;

    /// <summary>Room of the last cell the centring reserves, the <c>+ 18</c> of <c>:186</c>.</summary>
    public const int LastCellExtraWidth = 18;

    /// <summary>Gap between the icon row and the percentages, <c>:202</c>.</summary>
    public const int PercentRowGap = 10;

    /// <summary>Distance between the two text lines, the two <c>+= 20</c> of <c>:206-211</c>.</summary>
    public const int TextRowSpacing = 20;

    /// <summary>Distance of the OKAY button from the bottom edge, <c>{ 6, 6 }</c> of <c>:217</c>.</summary>
    public static readonly Point ButtonOffset = new(6, 6);

    private readonly PlaytestSummary _summary;
    private readonly AutoPlaytestResultsText _text;
    private readonly AutoPlaytestResultsArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _bodyFont;
    private readonly IGlyphSource _valueFont;
    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly Rect[] _playerAreas;
    private readonly int _percentRow;
    private readonly int _countRow;
    private readonly int _limitRow;

    private DialogPopup? _popup;

    private AutoPlaytestResultsWindow(PlaytestSummary summary, AutoPlaytestResultsText text,
        AutoPlaytestResultsArtwork artwork, IGlyphSource titleFont, IGlyphSource bodyFont, IGlyphSource valueFont,
        Size screen)
    {
        _summary = summary;
        _text = text;
        _artwork = artwork;
        _titleFont = titleFont;
        _bodyFont = bodyFont;
        _valueFont = valueFont;

        _window = StandardWindow.CreateCentered(new Size(WindowWidth, WindowHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;
        Sprite box = artwork.TitleBoxFrames[0];
        int titleY = active.Y + TitleOffset;

        int playerCount = summary.Colors.Count;
        int offsetX = active.X + ((active.Width - (playerCount * PlayerStepX) + LastCellExtraWidth) / 2);
        int playerY = titleY + box.Height + PercentRowGap;

        Sprite first = playerCount > 0
            ? artwork.PlayerIcons.GetIcon(ColorIconFrame(summary.Colors[0].Color))
            : new Sprite(1, 1);

        _playerAreas = new Rect[playerCount];

        for (int index = 0; index < playerCount; ++index)
        {
            Sprite icon = artwork.PlayerIcons.GetIcon(ColorIconFrame(summary.Colors[index].Color));

            _playerAreas[index] = new Rect(offsetX + (index * PlayerStepX), playerY, icon.Width, icon.Height);
        }

        _percentRow = playerY + first.Height + PercentRowGap;
        _countRow = _percentRow + TextRowSpacing;
        _limitRow = _countRow + TextRowSpacing;

        _buttons = new ButtonGroup();

        DialogButtonArtwork okay = artwork.Buttons.Get(DialogButton.Ok);
        Point okayPosition = _window.RenderPosition(
            ButtonOffset, new Size(okay.Released.Width, okay.Released.Height), WindowPadding.BottomCenter);

        _buttons.CreateButton(okayPosition.X, okayPosition.Y, okay.Released, okay.Pressed, DialogButton.Ok);
    }

    /// <summary>The tally being shown.</summary>
    public PlaytestSummary Summary => _summary;

    /// <summary>The window the content is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OKAY button.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>Where the colour icon of <see cref="PlaytestSummary.Colors"/>[<paramref name="index"/>] is.</summary>
    /// <param name="index">Index into <see cref="PlaytestSummary.Colors"/>.</param>
    /// <returns>The area, <c>playerRects[i]</c> of the reference.</returns>
    public Rect PlayerIconArea(int index) => _playerAreas[index];

    /// <summary>Top of the row of percentages, <c>offsetY</c> right after the icons.</summary>
    public int PercentRowTop => _percentRow;

    /// <summary>Top of the playthrough count line.</summary>
    public int CountRowTop => _countRow;

    /// <summary>Top of the day limit line.</summary>
    public int LimitRowTop => _limitRow;

    /// <summary>Whether the author closed the window.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The message a right click asked for, or <see langword="null"/>.</summary>
    public DialogPopup? PendingPopup => _popup;

    /// <summary>Takes the message a right click asked for, if any.</summary>
    /// <returns>The message, or <see langword="null"/> when none was asked for.</returns>
    public DialogPopup? TakePopup()
    {
        DialogPopup? popup = _popup;
        _popup = null;

        return popup;
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="summary">The tally to show; an empty one yields <see langword="null"/>, as the reference returns.</param>
    /// <param name="text">Labels the window draws; <see cref="AutoPlaytestResultsText.Default"/> when omitted.</param>
    /// <param name="artwork">Sprites of every part of the window.</param>
    /// <param name="titleFont">Font of the title, <c>normalWhite()</c>.</param>
    /// <param name="bodyFont">Font of the two statistics lines, <c>normalWhite()</c>.</param>
    /// <param name="valueFont">Font of the percentages, <c>normalYellow()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The window, or <see langword="null"/> when there is nothing to show or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static AutoPlaytestResultsWindow? TryCreate(PlaytestSummary summary, AutoPlaytestResultsText? text,
        AutoPlaytestResultsArtwork artwork, IGlyphSource titleFont, IGlyphSource bodyFont, IGlyphSource valueFont,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(valueFont);

        // displayResults() 的第一句：没有结果就不开窗。
        if (summary.Count == 0 || summary.Colors.Count == 0 || !artwork.IsComplete)
        {
            return null;
        }

        return new AutoPlaytestResultsWindow(summary, text ?? AutoPlaytestResultsText.Default, artwork, titleFont,
            bodyFont, valueFont, screen ?? ScreenBuffer.DefaultSize);
    }

    /// <summary>
    /// Processes one frame of input, the body of the <c>while ( le.HandleEvents() )</c> loop of <c>:231-256</c>.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="hotKey">Standard-button key pressed this frame, if any.</param>
    /// <returns><see langword="true"/> when the window has closed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, ButtonHotKey? hotKey = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        _popup = null;

        _buttons.SetCursor(events.CursorPosition);

        DialogButton answer = hotKey is { } key ? _buttons.ProcessHotKey(key) : DialogButton.None;

        if (answer == DialogButton.None)
        {
            answer = _buttons.SetLeftButton(events.IsMouseLeftButtonPressed);
        }

        if (answer == DialogButton.Ok)
        {
            IsClosed = true;

            return true;
        }

        ShowHelp(events);

        return false;
    }

    /// <summary>Draws the window and everything in it.</summary>
    /// <param name="target">Indexed frame buffer of the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        DrawTitle(target);
        DrawPlayers(target);
        DrawStatistics(target);

        _buttons.Draw(target);
        _buttons.DrawShadows(target);
    }

    /// <summary>Fills <see cref="_popup"/> for a right click, the <c>showStandardTextMessage()</c> calls of <c>:236-254</c>.</summary>
    /// <param name="events">Input state of the frame.</param>
    private void ShowHelp(LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup(_text.OkayTitle, _text.OkayHelp);

            return;
        }

        for (int index = 0; index < _playerAreas.Length; ++index)
        {
            if (events.IsMouseRightButtonPressedInArea(_playerAreas[index]))
            {
                PlaytestColorResult color = _summary.Colors[index];

                _popup = new DialogPopup(_text.OpponentsTitle, _text.ColorPlayer(color.Color));

                return;
            }
        }

        // 参考的第二圈判的是同一批矩形（拿百分数的说明），所以只有先判颜色那一圈才分得开；这里改成
        // 「右键在图标下方那一行上」——那一行才是百分数所在的位置。
        for (int index = 0; index < _playerAreas.Length; ++index)
        {
            Rect percentArea = new(_playerAreas[index].X, _percentRow, _playerAreas[index].Width, TextRowSpacing);

            if (!events.IsMouseRightButtonPressedInArea(percentArea))
            {
                continue;
            }

            PlaytestColorResult color = _summary.Colors[index];

            _popup = new DialogPopup(PlayerColors.Describe(color.Color), _text.Won(color.Color, color.Percentage));

            return;
        }
    }

    /// <summary>Frame of <c>NGEXTRA</c> that shows the colour of <paramref name="color"/>, <c>Color::GetIndex() + 3</c>.</summary>
    /// <param name="color">The player.</param>
    /// <returns>The frame index.</returns>
    private static uint ColorIconFrame(PlayerColor color) => (uint)(PlayerColors.IndexOf(color) + 3);

    private void DrawTitle(PixelBuffer target)
    {
        Sprite box = _artwork.TitleBoxFrames[0];
        Rect active = _window.ActiveArea;
        Rect roi = new(active.X + ((active.Width - box.Width) / 2), active.Y + TitleOffset, box.Width, box.Height);

        ImageOps.BlitSprite(target, roi.Position, box);
        ImageOps.AddGradientShadow(box, target, roi.Position, new Point(-5, 5));

        Rect textRoi = new(roi.X + 6, roi.Y + 5, roi.Width - 12, roi.Height - 11);
        string title = GlyphText.FitToOneRow(_titleFont, _text.Title, textRoi.Width);

        GlyphText.DrawLine(_titleFont, target, new Point(textRoi.X, textRoi.Y + 3), title);
    }

    private void DrawPlayers(PixelBuffer target)
    {
        Sprite shadow = _artwork.PlayerIcons.GetIcon(PlayerIconShadowFrame);

        for (int index = 0; index < _summary.Colors.Count; ++index)
        {
            Rect area = _playerAreas[index];

            ImageOps.AddGradientShadow(shadow, target, new Point(area.X - 5, area.Y + 3), new Point(0, 0));
            ImageOps.BlitSprite(target, area.Position,
                _artwork.PlayerIcons.GetIcon(ColorIconFrame(_summary.Colors[index].Color)));

            string percent = _summary.Colors[index].Percentage.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + _text.PercentSuffix;

            DrawCentered(target, _valueFont, percent, area.X, _percentRow, area.Width);
        }
    }

    /// <summary>Frame 61 of <c>NGEXTRA</c>, <c>playerIconShadow</c> of <c>:190</c>.</summary>
    private const uint PlayerIconShadowFrame = 61;

    private void DrawStatistics(PixelBuffer target)
    {
        Rect active = _window.ActiveArea;

        DrawCentered(target, _bodyFont, _text.PlaythroughCount(_summary.Count), active.X, _countRow, active.Width);
        DrawCentered(target, _bodyFont, _text.TimeLimitLine(_summary.TimeLimitCount), active.X, _limitRow,
            active.Width);
    }

    /// <summary>
    /// Draws a line centred in a box, the <c>text.draw( x, y, width, display )</c> overload of the reference.
    /// </summary>
    /// <param name="target">Frame buffer.</param>
    /// <param name="font">Font to draw with.</param>
    /// <param name="text">The line.</param>
    /// <param name="x">Left edge of the box.</param>
    /// <param name="y">Top of the line.</param>
    /// <param name="width">Width of the box, in which the line is centred.</param>
    private static void DrawCentered(PixelBuffer target, IGlyphSource font, string text, int x, int y, int width)
    {
        int textWidth = GlyphText.Measure(font, text);
        int offsetX = x + Math.Max(0, (width - textWidth) / 2);

        GlyphText.DrawLine(font, target, new Point(offsetX, y), text);
    }
}
