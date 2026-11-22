using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The scenario info window of the adventure map and of the file menu, <c>Dialog::GameInfo()</c>
/// (<c>dialog_gameinfo.cpp:81-243</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口画的是「这一局是怎么算赢、怎么算输」：地图名与描述、制作者定的难度、玩家选的难度、评分、地图尺寸、
/// 参战玩家、胜负条件，加一个「关于」按钮读制作者的说明。参考每帧把整块重画一遍，端口照做——地图屏幕每帧
/// 本来就要重画自己那一片，省下的时间在 640×480 上不值一提。
/// </para>
/// <para>
/// 参考在「关于」按钮底下断言地图必须是复兴版（<c>dialog_gameinfo.cpp:110</c>）：只有复兴版的地图头里才有
/// 制作者说明。端口不 assert，只看字符串空不空——老格式的地图读进来就是空串，按钮自然不显示。
/// </para>
/// </remarks>
public static class GameInfoDialog
{
    /// <summary>
    /// Runs the window until the player closes it, the event loop of <c>Dialog::GameInfo()</c>
    /// (<c>dialog_gameinfo.cpp:205-242</c>).
    /// </summary>
    /// <param name="target">Frame to draw into; it must already contain the scene behind the window.</param>
    /// <param name="view">The plate and its buttons, built by <see cref="GameInfoView.TryCreate"/>.</param>
    /// <param name="info">The map information block the rows are built from.</param>
    /// <param name="gameDifficulty">The difficulty the player picked, the <c>Game::getDifficulty()</c>.</param>
    /// <param name="players">The players shown in the "opponents" row.</param>
    /// <param name="playersArtwork">The <c>NGEXTRA</c> artwork of the players panel.</param>
    /// <param name="nameFont">The font of the map name, the reference's <c>normalWhite()</c>.</param>
    /// <param name="smallFont">The font of everything else, the reference's <c>smallWhite()</c>.</param>
    /// <param name="headerFont">The yellow font of the popup titles.</param>
    /// <param name="bodyFont">The white font of the popup bodies, the reference's <c>normalWhite()</c>.</param>
    /// <param name="buttonArtwork">The artwork of the popup buttons.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public static void Show(PixelBuffer target, GameInfoView view, Mp2FileInfo info, GameDifficulty gameDifficulty,
        Players players, IPlayersInfoArtwork playersArtwork, IGlyphSource nameFont, IGlyphSource smallFont,
        IGlyphSource headerFont, IGlyphSource bodyFont, IDialogButtonArtworkSource buttonArtwork,
        LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(playersArtwork);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "GameInfoDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        // 参考把两句话在建窗口时就取好，之后只画（dialog_gameinfo.cpp:185-196）；port 同样取一次。
        string victoryText = GameOver.GetDescription(MapConditions.Wins(info), info);
        string lossText = GameOver.GetDescription(MapConditions.Loss(info), info);

        IReadOnlyList<GameInfoRow> rows = GameInfoView.BuildRows(
            info, gameDifficulty, victoryText, lossText, view.HasCreatorNotes, view.About?.Released.Width ?? 0);

        PlayersInfo panel = new();
        panel.UpdateInfo(players, view.PlayersInfoPosition, view.PlayersClassPosition, playersArtwork);

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            Draw(target, view, rows, panel, info, players, playersArtwork, nameFont, smallFont);

            view.Okay.UpdatePressState(eventState);
            view.About?.UpdatePressState(eventState);
            view.Okay.Draw(target);
            view.About?.Draw(target);

            engine.Render(target.Bounds);

            if (eventState.MouseClickLeft(view.Okay.Area) || eventState.IsKeyPressed(Key.Escape))
            {
                return;
            }

            if (view.About is { } about)
            {
                if (eventState.MouseClickLeft(about.Area))
                {
                    ShowBox(target, headerFont, bodyFont, buttonArtwork, eventState, "About", info.CreatorNotes,
                        DialogButton.Ok);
                }
                else if (eventState.IsMouseRightButtonPressedInArea(about.Area))
                {
                    ShowBox(target, headerFont, bodyFont, buttonArtwork, eventState, "About",
                        "Click to read notes from the map creator.", DialogButton.None);
                }
            }

            if (eventState.IsMouseRightButtonPressedInArea(view.DescriptionArea))
            {
                ShowBox(target, headerFont, bodyFont, buttonArtwork, eventState, "Map Description",
                    info.Description, DialogButton.None);
            }
            else if (eventState.IsMouseRightButtonPressedInArea(view.NameArea))
            {
                ShowBox(target, headerFont, bodyFont, buttonArtwork, eventState, info.Name, string.Empty,
                    DialogButton.None);
            }
            else if (eventState.IsMouseRightButtonPressedInArea(view.Okay.Area))
            {
                ShowBox(target, headerFont, bodyFont, buttonArtwork, eventState, "Okay", "Exit this menu.",
                    DialogButton.None);
            }
            else if (panel.ReadOnlyEventProcessing(eventState) is
                     { Action: PlayersInfoAction.ShowHelp } help)
            {
                ShowBox(target, headerFont, bodyFont, buttonArtwork, eventState, help.Title, help.Body,
                    DialogButton.None);
            }

            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }
    }

    /// <summary>Paints the plate, the rows and the players panel, <c>dialog_gameinfo.cpp:100-196</c>.</summary>
    private static void Draw(PixelBuffer target, GameInfoView view, IReadOnlyList<GameInfoRow> rows,
        PlayersInfo panel, Mp2FileInfo info, Players players, IPlayersInfoArtwork playersArtwork,
        IGlyphSource nameFont, IGlyphSource smallFont)
    {
        view.DrawFrame(target);

        for (int index = 0; index < rows.Count; ++index)
        {
            // 参考的第一行是地图名，用 normalWhite()，其余全是 smallWhite()（dialog_gameinfo.cpp:121、:140-196）。
            IGlyphSource font = index == 0 ? nameFont : smallFont;

            DrawRow(target, font, view, rows[index], isName: index == 0);
        }

        panel.RedrawInfo(true, target, info, players, playersArtwork);
    }

    /// <summary>Draws one row: centered in its column, fitted as the row asks.</summary>
    private static void DrawRow(PixelBuffer target, IGlyphSource font, GameInfoView view, GameInfoRow row,
        bool isName)
    {
        string text = row.Fit == GameInfoRowFit.SingleRow ? FitToOneRow(font, row.Text, row.Width - 2) : row.Text;

        List<string> lines = GlyphText.WrapLines(font, text, row.Width);

        if (row.Fit == GameInfoRowFit.Area && row.Height > 0)
        {
            int rowsThatFit = Math.Max(1, row.Height / font.LineHeight);

            if (lines.Count > rowsThatFit)
            {
                lines = lines.GetRange(0, rowsThatFit);
            }
        }

        int height = lines.Count * font.LineHeight;
        int y = row.Anchor == GameInfoRowAnchor.Bottom ? view.ShadowPosition.Y + row.Y - height
            : view.ShadowPosition.Y + row.Y;

        if (isName)
        {
            // dialog_gameinfo.cpp:124-138：有关于按钮时，名字先按整条 349 居中；放不下（会压到按钮下面）
            // 就靠右对齐到缩短后的那一截，右边留 1 像素。
            int width = GlyphText.Measure(font, text, keepTrailingSpaces: true);
            int noShiftWidth = GameInfoLayout.NameMaxWidth - (view.About?.Released.Width ?? 0) * 2 - 2;

            int x = view.HasCreatorNotes && width > noShiftWidth
                ? view.ShadowPosition.X + GameInfoLayout.NameX + row.Width - width - 1
                : view.ShadowPosition.X + GameInfoLayout.NameX + Math.Max(0, (GameInfoLayout.NameMaxWidth - width) / 2);

            GlyphText.DrawLine(font, target, new Point(x, y), text);

            return;
        }

        for (int index = 0; index < lines.Count; ++index)
        {
            int lineWidth = GlyphText.Measure(font, lines[index], keepTrailingSpaces: true);
            int x = view.ShadowPosition.X + row.X + Math.Max(0, (row.Width - lineWidth) / 2);

            GlyphText.DrawLine(font, target, new Point(x, y + (index * font.LineHeight)), lines[index]);
        }
    }

    /// <summary>Cuts the text down to one row, <c>Text::fitToOneRow()</c>: an ellipsis marks the cut.</summary>
    private static string FitToOneRow(IGlyphSource font, string text, int maxWidth)
    {
        if (maxWidth <= 0 || GlyphText.Measure(font, text, keepTrailingSpaces: true) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        int ellipsisWidth = GlyphText.Measure(font, ellipsis);

        int end = 0;
        int width = 0;

        while (end < text.Length)
        {
            int advance = font.GetAdvance(text[end]);

            if (width + advance + ellipsisWidth > maxWidth)
            {
                break;
            }

            width += advance;
            ++end;
        }

        return string.Concat(text.AsSpan(0, end), ellipsis);
    }

    /// <summary>Shows one of the four popups of the window, <c>fheroes2::showStandardTextMessage()</c>.</summary>
    private static void ShowBox(PixelBuffer target, IGlyphSource headerFont, IGlyphSource bodyFont,
        IDialogButtonArtworkSource buttonArtwork, LocalEvent events, string header, string body,
        DialogButton buttons)
    {
        MessageBox box = MessageBox.Create(header, body, headerFont, bodyFont, buttons: buttons,
            buttonArtwork: buttonArtwork);

        if (buttons == DialogButton.None)
        {
            // Dialog::ZERO：没有按钮的框关掉的方式是「随便点一下 / 随便按个键」，参考的 Dialog::Message
            // 在没有任何按钮时就是这么退出的（dialog.cpp 里的 while 只在按钮上才 break）。
            IEventEngine engine = events.Engine ?? throw new InvalidOperationException(
                "GameInfoDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

            box.Draw(target);
            engine.Render(target.Bounds);

            while (events.HandleEvents(sleepAfterEventProcessing: false))
            {
                if (events.MouseClickLeft() || events.MouseClickRight() || events.IsKeyPressed(Key.Escape) ||
                    events.IsKeyPressed(Key.Enter) || events.IsKeyPressed(Key.Space))
                {
                    return;
                }

                box.Draw(target);
                engine.Render(target.Bounds);
                engine.Sleep(LocalEvent.LoopSleepTimeMs);
            }

            return;
        }

        MessageBox.Show(target, box, events);
    }
}
