using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the scenario info screen asks the caller to do next.
/// </summary>
public enum ScenarioInfoResult
{
    /// <summary>The click did not hit anything that changes screens.</summary>
    None,

    /// <summary>The "Select Maps" button was pressed; the caller has to show the map list and call <see cref="ScenarioInfoDialog.SelectMap"/>.</summary>
    SelectMaps,

    /// <summary>The scenario was accepted: the players and the difficulty are already stored for the game.</summary>
    Started,

    /// <summary>The screen was dismissed and the caller has to go back to the main menu.</summary>
    Cancelled,
}

/// <summary>
/// The scenario info screen, <c>Game::SelectScenario()</c> / <c>ChooseNewMap()</c> of
/// <c>game_scenarioinfo.cpp:173-425</c>: the map name box with its "Select Maps" button, the row of difficulty icons,
/// the opponents and class panels, the difficulty rating and the two small buttons.
/// </summary>
/// <remarks>
/// <para>
/// 参考这一屏自己开 <c>LocalEvent</c> 循环；端口按 <see cref="ScenarioSelectionDialog"/> 的做法做成状态对象，宿主
/// 每帧调 <see cref="Draw"/>、把输入交给 <see cref="ProcessEvents"/>，于是主菜单那一层照旧负责事件泵与光标。
/// </para>
/// <para>
/// 和参考一样，这一屏直接写 <see cref="Settings"/> 的三样东西：当前地图（<see cref="Settings.CurrentMapInfo"/>）、
/// 玩家槽位（<see cref="Settings.Players"/>，换地图时先 <c>Init</c> 再 <see cref="ScenarioSetup.LoadPlayers"/>）与
/// 难度（<see cref="Settings.GameDifficulty"/>）。确认时再 <see cref="ScenarioSetup.SavePlayers"/>。
/// </para>
/// </remarks>
public sealed class ScenarioInfoDialog
{
    /// <summary>Width of the content area of the window, <c>StandardWindow( 388, 397 )</c>.</summary>
    public const int WindowWidth = 388;

    /// <summary>Height of the content area of the window.</summary>
    public const int WindowHeight = 397;

    /// <summary>Left column of each difficulty icon inside the content area, <c>coordDifficulty</c>.</summary>
    private static readonly int[] DifficultyLefts = { 8, 85, 161, 238, 315 };

    /// <summary>Top of the difficulty icons inside the content area.</summary>
    private const int DifficultyTop = 78;

    /// <summary>Horizontal step of the difficulty labels, which do not line up with the icons (<c>width = 77</c>).</summary>
    private const int DifficultyLabelStep = 77;

    /// <summary>The offset the difficulty cursor is drawn with, <c>levelCursorOffset</c>.</summary>
    private const int DifficultyCursorOffset = 3;

    /// <summary>Border of the map name box that text must respect, <c>boxBorder</c>.</summary>
    private const int BoxBorder = 6;

    private readonly IReadOnlyList<Mp2FileInfo> maps;
    private readonly Settings settings = Settings.Get();
    private readonly Players players;
    private readonly ScenarioInfoArtwork artwork;
    private readonly ScenarioInfoText texts;
    private readonly IGlyphSource font;
    private readonly IGlyphSource smallFont;
    private readonly Action<string, string> showMessage;

    private readonly StandardWindow window;
    private readonly Rect roi;
    private readonly Rect scenarioBoxArea;
    private readonly Rect[] difficultyAreas;
    private readonly Rect maxTextRoi;
    private readonly Rect centeredTextRoi;
    private readonly Rect ratingRoi;
    private readonly Point pointDifficultyInfo;
    private readonly Point pointOpponentInfo;
    private readonly Point pointClassInfo;

    private readonly ButtonSprite selectMapsButton;
    private readonly ButtonSprite okayButton;
    private readonly ButtonSprite cancelButton;
    private readonly PlayersInfo playersInfo;

    private int mapIndex;

    /// <summary>
    /// Builds the screen and applies the scenario it opens with to <see cref="Settings"/>.
    /// </summary>
    /// <param name="maps">The maps the screen may show, already filtered by the human player count.</param>
    /// <param name="artwork">The pictures of this interface style.</param>
    /// <param name="font">The normal white font of the labels and of the map name.</param>
    /// <param name="smallFont">The small white font of the difficulty names.</param>
    /// <param name="showMessage">Shows a help message; the caller owns the modal box, this screen only asks for it.</param>
    /// <param name="texts">The strings, <see cref="ScenarioInfoText.Default"/> by default.</param>
    public ScenarioInfoDialog(
        IReadOnlyList<Mp2FileInfo> maps,
        ScenarioInfoArtwork artwork,
        IGlyphSource font,
        IGlyphSource smallFont,
        Action<string, string> showMessage,
        ScenarioInfoText? texts = null)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(smallFont);
        ArgumentNullException.ThrowIfNull(showMessage);

        if (maps.Count == 0)
        {
            throw new ArgumentException("The scenario info screen needs at least one map.", nameof(maps));
        }

        this.maps = maps;
        this.artwork = artwork;
        this.texts = texts ?? ScenarioInfoText.Default;
        this.font = font;
        this.smallFont = smallFont;
        this.showMessage = showMessage;

        players = settings.Players;

        mapIndex = FindCurrentMapIndex();

        window = StandardWindow.CreateCentered(new Size(WindowWidth, WindowHeight));
        roi = window.ActiveArea;

        pointDifficultyInfo = new Point(roi.X + 8, roi.Y + 79);
        pointOpponentInfo = new Point(roi.X + 8, roi.Y + 181);
        pointClassInfo = new Point(roi.X + 8, roi.Y + 265);

        Sprite box = artwork.ScenarioBox;
        scenarioBoxArea = new Rect(roi.X + ((roi.Width - box.Width) / 2), roi.Y + 24, box.Width, box.Height);

        difficultyAreas = new Rect[DifficultyLefts.Length];
        for (int i = 0; i < difficultyAreas.Length; ++i)
        {
            // The reference reads the cursor's size once and reuses it for all five hit boxes.
            difficultyAreas[i] = new Rect(
                roi.X + DifficultyLefts[i],
                roi.Y + DifficultyTop,
                artwork.DifficultyCursor.Width,
                artwork.DifficultyCursor.Height);
        }

        selectMapsButton = new ButtonSprite(artwork.MapSelectReleased, artwork.MapSelectPressed);
        selectMapsButton.SetPosition(
            scenarioBoxArea.X + scenarioBoxArea.Width - 6 - artwork.MapSelectReleased.Width,
            scenarioBoxArea.Y + 5);

        okayButton = new ButtonSprite(artwork.OkayIcon, artwork.OkayIcon);
        cancelButton = new ButtonSprite(artwork.CancelIcon, artwork.CancelIcon);

        const int buttonOffsetX = 20;
        const int buttonOffsetY = 6;
        okayButton.SetPosition(roi.X + buttonOffsetX, roi.Y + roi.Height - artwork.OkayIcon.Height - buttonOffsetY);
        cancelButton.SetPosition(
            roi.X + roi.Width - artwork.CancelIcon.Width - buttonOffsetX,
            roi.Y + roi.Height - artwork.CancelIcon.Height - buttonOffsetY);

        // The name is centred between the two borders minus the select button, <c>maxTextRoi</c> / <c>centeredTextRoi</c>.
        int overallBoxTextAreaWidth = scenarioBoxArea.Width - (2 * BoxBorder);
        int maxTextAreaWidth = overallBoxTextAreaWidth - artwork.MapSelectReleased.Width;

        maxTextRoi = new Rect(scenarioBoxArea.X + BoxBorder, scenarioBoxArea.Y + 5, maxTextAreaWidth, 19);

        int halfBoxTextAreaWidth = overallBoxTextAreaWidth / 2;
        int rightSideAvailableTextWidth = (halfBoxTextAreaWidth > artwork.MapSelectReleased.Width)
            ? halfBoxTextAreaWidth - artwork.MapSelectReleased.Width
            : artwork.MapSelectReleased.Width - halfBoxTextAreaWidth;

        centeredTextRoi = new Rect(
            scenarioBoxArea.X + BoxBorder + artwork.MapSelectReleased.Width,
            scenarioBoxArea.Y + 5,
            2 * rightSideAvailableTextWidth,
            19);

        ratingRoi = new Rect(
            okayButton.Area.X + okayButton.Area.Width,
            okayButton.Area.Y,
            roi.Width - okayButton.Area.Width - cancelButton.Area.Width - 40,
            okayButton.Area.Height);

        playersInfo = new PlayersInfo();
        UpdatePlayersOfCurrentMap();
    }

    /// <summary>The maps the screen may show.</summary>
    public IReadOnlyList<Mp2FileInfo> Maps => maps;

    /// <summary>The map the screen currently shows.</summary>
    public Mp2FileInfo Map => maps[mapIndex];

    /// <summary>The index of <see cref="Map"/> inside <see cref="Maps"/>.</summary>
    public int MapIndex => mapIndex;

    /// <summary>The players the scenario will start with.</summary>
    public Players Players => players;

    /// <summary>The difficulty the scenario will start with.</summary>
    public GameDifficulty Difficulty => settings.GameDifficulty;

    /// <summary>The difficulty rating line, <c>Game::GetRating()</c> of the current map and difficulty.</summary>
    public string Rating =>
        texts.Rating(GameRating.GetRating(Map, Difficulty).ToString(CultureInfo.InvariantCulture));

    /// <summary>The content area of the window, <c>roi</c>.</summary>
    public Rect WindowArea => roi;

    /// <summary>The area of the "Select Maps" button.</summary>
    public Rect SelectMapsArea => selectMapsButton.Area;

    /// <summary>The area of the okay button.</summary>
    public Rect OkayArea => okayButton.Area;

    /// <summary>The area of the cancel button.</summary>
    public Rect CancelArea => cancelButton.Area;

    /// <summary>The areas of the five difficulty icons.</summary>
    public IReadOnlyList<Rect> DifficultyAreas => difficultyAreas;

    /// <summary>The area the difficulty rating text occupies.</summary>
    public Rect RatingArea => ratingRoi;

    /// <summary>
    /// Switches the screen to another map, reinitializes the slots and restores the parameters saved for that map,
    /// <c>Game::LoadPlayers()</c> after <c>conf.setCurrentMapInfo()</c> (<c>game_scenarioinfo.cpp:366-372</c>).
    /// </summary>
    /// <param name="index">The index into <see cref="Maps"/>.</param>
    public void SelectMap(int index)
    {
        if ((uint)index >= (uint)maps.Count || index == mapIndex)
        {
            return;
        }

        mapIndex = index;
        UpdatePlayersOfCurrentMap();
    }

    /// <summary>Sets the difficulty, <c>conf.setGameDifficulty()</c>.</summary>
    /// <param name="difficulty">The difficulty the cursor moves to.</param>
    public void SelectDifficulty(GameDifficulty difficulty)
    {
        if (difficulty == settings.GameDifficulty || !Enum.IsDefined(difficulty))
        {
            return;
        }

        ScenarioSetup.SaveDifficulty(difficulty);
    }

    /// <summary>Accepts the scenario: remembers the slots and starts the map, <c>GameMode::STARTGAME</c>.</summary>
    public void Accept()
    {
        ScenarioSetup.SavePlayers(Map.FilePath, players);
        ScenarioSetup.SaveDifficulty(Difficulty);
    }

    /// <summary>Extra input the host has to route into this screen before drawing it.</summary>
    /// <param name="events">The current frame's input.</param>
    /// <returns>What the host has to do, <see cref="ScenarioInfoResult.None"/> when the click only changed this screen.</returns>
    public ScenarioInfoResult ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        selectMapsButton.UpdatePressState(events);
        okayButton.UpdatePressState(events);
        cancelButton.UpdatePressState(events);

        if (events.IsMouseLeftButtonPressedInArea(selectMapsButton.Area))
        {
            return ScenarioInfoResult.SelectMaps;
        }

        if (events.IsMouseLeftButtonPressedInArea(okayButton.Area))
        {
            Accept();
            return ScenarioInfoResult.Started;
        }

        if (events.IsMouseLeftButtonPressedInArea(cancelButton.Area))
        {
            return ScenarioInfoResult.Cancelled;
        }

        for (int i = 0; i < difficultyAreas.Length; ++i)
        {
            if (events.IsMouseLeftButtonPressedInArea(difficultyAreas[i]))
            {
                SelectDifficulty((GameDifficulty)i);
                break;
            }
        }

        if (playersInfo.QueueEventProcessing(events, Map, players) is { Handled: true } answer
            && answer.Action == PlayersInfoAction.ShowHelp)
        {
            showMessage(answer.Title, answer.Body);
        }

        if (events.IsMouseRightButtonPressedInArea(roi))
        {
            ShowRightClickHelp(events);
        }

        return ScenarioInfoResult.None;
    }

    /// <summary>
    /// Draws the whole screen onto a frame that already holds the main menu backdrop, <c>drawMainMenuScreen()</c> plus
    /// the window of <c>ChooseNewMap()</c>.
    /// </summary>
    /// <param name="target">The frame to draw into.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        window.Draw(target);

        // The map name box with its shadow, <c>scenarioBoxRoi</c>.
        Sprite box = artwork.ScenarioBox;
        ImageOps.BlitSprite(target, new Point(scenarioBoxArea.X, scenarioBoxArea.Y), box);
        ImageOps.AddGradientShadow(box, target, new Point(scenarioBoxArea.X, scenarioBoxArea.Y), new Point(-5, 5));

        DrawStaticInfo(target);

        // Difficulty icons and their names.
        for (int i = 0; i < difficultyAreas.Length; ++i)
        {
            Sprite icon = artwork.GetDifficultyIcon((GameDifficulty)i);
            Rect area = difficultyAreas[i];

            ImageOps.BlitSprite(target, new Point(area.X, area.Y), icon);
            ImageOps.AddGradientShadow(icon, target, new Point(area.X, area.Y), new Point(-5, 5));

            string name = texts.Difficulty((GameDifficulty)i);
            int nameWidth = GlyphText.Measure(smallFont, name);

            // The labels follow the irregular icon row: 77 pixels apart, plus one pixel for the normal difficulty
            // because its icon sits one pixel to the left of the grid.
            int nameCenter = pointDifficultyInfo.X + 31 + (DifficultyLabelStep * i) + (i == (int)GameDifficulty.Normal ? 1 : 0);
            GlyphText.DrawLine(smallFont, target, new Point(nameCenter - (nameWidth / 2), pointDifficultyInfo.Y + 69), name);
        }

        // The cursor that marks the current difficulty.
        Sprite cursor = artwork.DifficultyCursor;
        Rect selected = difficultyAreas[(int)Difficulty];
        ImageOps.BlitSprite(target, new Point(selected.X - DifficultyCursorOffset, selected.Y - DifficultyCursorOffset), cursor);

        playersInfo.RedrawInfo(displayInGameInfo: false, target, Map, players, artwork);

        DrawMapTitle(target);
        DrawRating(target);

        selectMapsButton.Draw(target);
        okayButton.Draw(target);
        cancelButton.Draw(target);
    }

    private void UpdatePlayersOfCurrentMap()
    {
        Mp2FileInfo map = Map;

        settings.SetCurrentMapFileInfo(map);
        players.Init(map);
        ScenarioSetup.LoadPlayers(map.FilePath, players);
        playersInfo.UpdateInfo(players, pointOpponentInfo, pointClassInfo, artwork);
    }

    private int FindCurrentMapIndex()
    {
        Mp2FileInfo? current = settings.CurrentMapFileInfo;

        if (current is null || string.IsNullOrEmpty(current.FilePath))
        {
            return 0;
        }

        for (int i = 0; i < maps.Count; ++i)
        {
            if (maps[i].FilePath == current.FilePath && maps[i].Name == current.Name)
            {
                return i;
            }
        }

        // The map the last game used is gone from the folder; fall back to the first one.
        return 0;
    }

    private void DrawStaticInfo(PixelBuffer target)
    {
        DrawCenteredText(font, texts.ScenarioLabel, roi.Y + 9, target);
        DrawCenteredText(font, texts.DifficultyLabel, roi.Y + 59, target);
        DrawCenteredText(font, texts.OpponentsLabel, roi.Y + 164, target);
        DrawCenteredText(font, texts.ClassLabel, roi.Y + 248, target);
    }

    private void DrawCenteredText(IGlyphSource source, string text, int y, PixelBuffer target)
    {
        int width = GlyphText.Measure(source, text);
        GlyphText.DrawLine(source, target, new Point(roi.X + ((roi.Width - width) / 2), y), text);
    }

    private void DrawMapTitle(PixelBuffer target)
    {
        string title = Map.Name;
        int width = GlyphText.Measure(font, title);

        if (width > centeredTextRoi.Width)
        {
            string fitted = GlyphText.FitToOneRow(font, title, maxTextRoi.Width);
            int fittedWidth = GlyphText.Measure(font, fitted);

            GlyphText.DrawLine(
                font,
                target,
                new Point(maxTextRoi.X + (maxTextRoi.Width - fittedWidth), maxTextRoi.Y + 3),
                fitted);

            return;
        }

        GlyphText.DrawLine(
            font,
            target,
            new Point(centeredTextRoi.X + ((centeredTextRoi.Width - width) / 2), centeredTextRoi.Y + 3),
            title);
    }

    private void DrawRating(PixelBuffer target)
    {
        string rating = Rating;
        int width = GlyphText.Measure(font, rating);

        GlyphText.DrawLine(font, target, new Point(roi.X + ((roi.Width - width) / 2), roi.Y + 372), rating);
    }

    private void ShowRightClickHelp(LocalEvent events)
    {
        if (selectMapsButton.Area.Contains(events.CursorPosition))
        {
            showMessage(texts.ScenarioHelpTitle, texts.ScenarioHelp);
            return;
        }

        foreach (Rect area in difficultyAreas)
        {
            if (area.Contains(events.CursorPosition))
            {
                showMessage(texts.DifficultyHelpTitle, texts.DifficultyHelp);
                return;
            }
        }

        if (ratingRoi.Contains(events.CursorPosition))
        {
            showMessage(texts.RatingHelpTitle, texts.RatingHelp);
            return;
        }

        if (okayButton.Area.Contains(events.CursorPosition))
        {
            showMessage(texts.OkayHelpTitle, texts.OkayHelp);
            return;
        }

        if (cancelButton.Area.Contains(events.CursorPosition))
        {
            showMessage(texts.CancelHelpTitle, texts.CancelHelp);
        }
    }
}
