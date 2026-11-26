using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Highscores;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>What the high score screen asks its caller to do.</summary>
public enum HighScoresAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>The exit button was released on itself; the caller closes the screen.</summary>
    Exit,

    /// <summary>The table switch button was released on itself; the caller shows the other table.</summary>
    SwitchMode,
}

/// <summary>Which button a right click asked for, the <c>isMouseRightButtonPressedInArea()</c> cascade of the reference.</summary>
public enum HighScoresHelpTarget
{
    /// <summary>Nothing was hit.</summary>
    None,

    /// <summary>The exit button, whose help says "Exit this menu.".</summary>
    Exit,

    /// <summary>The table switch button, whose help names the table the click would open.</summary>
    Other,
}

/// <summary>The two text faces of the screen.</summary>
/// <param name="White">Rows and values, <c>FontType::normalWhite()</c>.</param>
/// <param name="Yellow">The row that was just registered, <c>FontType::normalYellow()</c>.</param>
public sealed record HighScoresFonts(IGlyphSource White, IGlyphSource Yellow);

/// <summary>
/// The high score screen, <c>redrawHighScoreScreen()</c> and the animation of <c>redrawMonstersAnimation()</c>
/// (<c>game_highscores.cpp:93-243</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一屏十行，每行四列文字加一只兵种小图；兵种按评分（标准榜）或天数（战役榜）反查，见
/// <see cref="HighScoreDataContainer.GetMonsterByRating"/> 与 <see cref="HighScoreDataContainer.GetMonsterByDay"/>。
/// 刚登记的那一行用黄色，其余用白色。
/// </para>
/// <para>
/// <b>按钮文案照参考</b>：屏幕边上的竖排按钮写的是<b>当前</b>榜的名字，点它换成另一张榜——参考
/// <c>game_highscores.cpp:376-381</c> 就是这么取素材的，<c>:424-426</c> 才是「换到另一张」。右键帮助反而说的是
/// <b>目标</b>榜（<c>:437-444</c>），调用方按 <see cref="HighScoresHelpTarget.Other"/> 自己挑文案。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：参考每帧只重画兵种所在的那块矩形（先存底再动画），端口每帧把整屏重画一遍，因为宿主本来
/// 就是每帧全量推屏；像素结果一致，少了一层 <c>ImageRestorer</c>。
/// </para>
/// </remarks>
public sealed class HighScoresView
{
    /// <summary>How many breathing frames a creature has, frames 0 to 6 of the reference's animation.</summary>
    public const int AnimationFrames = 7;

    private readonly HighScoresArtwork _artwork;
    private readonly HighScoresFonts _fonts;
    private readonly ButtonSprite _otherButton;
    private readonly ButtonSprite _exitButton;

    private HighScoreMode _mode;
    private IReadOnlyList<HighscoreData> _entries;
    private int _selectedIndex;
    private bool _pressedOther;
    private bool _pressedExit;
    private Point? _cursor;
    private uint _animationFrameId;

    private HighScoresView(
        HighScoresArtwork artwork,
        HighScoresFonts fonts,
        Size screen,
        HighScoreMode mode,
        IReadOnlyList<HighscoreData> entries,
        bool campaignAvailable,
        int selectedIndex)
    {
        _artwork = artwork;
        _fonts = fonts;
        _mode = mode;
        _entries = entries;
        _selectedIndex = selectedIndex;
        Area = HighScoresLayout.Top(
            screen, new Size(artwork.Background.Width, artwork.Background.Height));

        _otherButton = new ButtonSprite(artwork.StandardButton[0], artwork.StandardButton[1]);
        _exitButton = new ButtonSprite(artwork.ExitButton[0], artwork.ExitButton[1]);

        ApplyMode();

        if (!campaignAvailable)
        {
            // 参考 disable() 那个按钮：没有原版战役的地图文件时不给切到战役榜。
            _otherButton.Disable();
        }
    }

    /// <summary>The area the background covers on the screen, <c>top</c> plus the size of <c>HSBKG</c>.</summary>
    public Rect Area { get; }

    /// <summary>The table on screen.</summary>
    public HighScoreMode Mode => _mode;

    /// <summary>The rows of the table on screen.</summary>
    public IReadOnlyList<HighscoreData> Entries => _entries;

    /// <summary>The row drawn in yellow, <c>selectedScoreIndex</c>; <c>-1</c> for none.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>The table switch button.</summary>
    public ButtonBase OtherButton => _otherButton;

    /// <summary>The exit button.</summary>
    public ButtonBase ExitButton => _exitButton;

    /// <summary>How many times the animation advanced, <c>monsterAnimationFrameId</c>.</summary>
    public uint AnimationFrameId => _animationFrameId;

    /// <summary>Builds the screen.</summary>
    /// <param name="mode">The table to show first.</param>
    /// <param name="entries">The rows of that table.</param>
    /// <param name="artwork">The sprites; <see langword="null"/> makes the build fail.</param>
    /// <param name="fonts">The two faces.</param>
    /// <param name="selectedIndex">The row to draw in yellow; <c>-1</c> for none.</param>
    /// <param name="campaignAvailable">Whether the switch button is enabled, <c>Game::isSuccessionWarsCampaignPresent()</c>.</param>
    /// <param name="screen">The screen to centre the background on; 640x480 when omitted.</param>
    /// <returns>The screen, or <see langword="null"/> when the artwork is unusable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> or <paramref name="fonts"/> is <see langword="null"/>.</exception>
    public static HighScoresView? TryCreate(
        HighScoreMode mode,
        IReadOnlyList<HighscoreData> entries,
        HighScoresArtwork? artwork,
        HighScoresFonts fonts,
        int selectedIndex = -1,
        bool campaignAvailable = true,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(fonts);

        if (artwork is null || artwork.Background.Width <= 0)
        {
            return null;
        }

        Size area = screen ?? new Size(artwork.Background.Width, artwork.Background.Height);

        return new HighScoresView(artwork, fonts, area, mode, entries, campaignAvailable, selectedIndex);
    }

    /// <summary>
    /// Shows another table without rebuilding the screen, the <c>isInternalUpdate</c> trip of
    /// <c>Game::DisplayHighScores()</c> (<c>game_highscores.cpp:469-473</c>).
    /// </summary>
    /// <param name="mode">The table to show.</param>
    /// <param name="entries">Its rows.</param>
    /// <param name="selectedIndex">The row to draw in yellow; <c>-1</c> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    public void SetTable(HighScoreMode mode, IReadOnlyList<HighscoreData> entries, int selectedIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _mode = mode;
        _entries = entries;
        _selectedIndex = selectedIndex;
        _pressedOther = false;
        _pressedExit = false;

        ApplyMode();
    }

    /// <summary>Records where the mouse is, so the buttons can show their held look.</summary>
    /// <param name="point">Cursor in logical pixels, or <see langword="null"/> when it left the screen.</param>
    public void SetCursor(Point? point)
    {
        _cursor = point;
        UpdatePressedStates();
    }

    /// <summary>
    /// A left button transition over the screen, the <c>MouseClickLeft()</c> pair of the reference's loop
    /// (<c>game_highscores.cpp:412-426</c>).
    /// </summary>
    /// <param name="down">Whether the left button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns><see cref="HighScoresAction.Exit"/> or <see cref="HighScoresAction.SwitchMode"/> when a button was clicked.</returns>
    public HighScoresAction SetLeftButton(bool down, Point point)
    {
        if (down)
        {
            _pressedOther = _otherButton.IsEnabled && _otherButton.Area.Contains(point);
            _pressedExit = !_pressedOther && _exitButton.Area.Contains(point);
            UpdatePressedStates();

            return HighScoresAction.None;
        }

        bool other = _pressedOther && _otherButton.IsEnabled && _otherButton.Area.Contains(point);
        bool exit = _pressedExit && _exitButton.Area.Contains(point);

        _pressedOther = false;
        _pressedExit = false;
        UpdatePressedStates();

        if (exit)
        {
            return HighScoresAction.Exit;
        }

        return other ? HighScoresAction.SwitchMode : HighScoresAction.None;
    }

    /// <summary>
    /// The button a right click asks about, the <c>isMouseRightButtonPressedInArea()</c> cascade of
    /// <c>game_highscores.cpp:434-444</c>.
    /// </summary>
    /// <param name="point">Cursor position in logical pixels.</param>
    /// <returns>The button that was hit, <see cref="HighScoresHelpTarget.None"/> when nothing was.</returns>
    public HighScoresHelpTarget ProcessRightClick(Point point)
    {
        if (_exitButton.Area.Contains(point))
        {
            return HighScoresHelpTarget.Exit;
        }

        if (_otherButton.IsEnabled && _otherButton.Area.Contains(point))
        {
            return HighScoresHelpTarget.Other;
        }

        return HighScoresHelpTarget.None;
    }

    /// <summary>
    /// Advances the breathing animation once per <c>DelayType::MAPS_DELAY</c> (250 ms), the
    /// <c>Game::validateAnimationDelay( Game::DelayType::MAPS_DELAY )</c> of <c>game_highscores.cpp:446</c>.
    /// </summary>
    /// <returns><see langword="true"/> when the frame changed.</returns>
    public bool Update()
    {
        if (!GameDelays.ValidateAnimationDelay(DelayType.MapsDelay))
        {
            return false;
        }

        ++_animationFrameId;

        return true;
    }

    /// <summary>Paints the background, the title, ten rows and the two buttons, <c>:195-243</c>.</summary>
    /// <param name="target">Frame to draw into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Point top = Area.Position;

        ImageOps.Blit(target, top, _artwork.Background.Image, _artwork.Background.Image.Bounds);

        Sprite title = _mode == HighScoreMode.Campaign ? _artwork.CampaignTitle : _artwork.StandardTitle;
        ImageOps.BlitSprite(target, HighScoresLayout.TitleAnchor(top), title);

        int rows = Math.Min(HighScoresLayout.MaximumDisplayedEntries, _entries.Count);

        for (int row = 0; row < rows; ++row)
        {
            DrawRow(target, top, row, _entries[row]);
        }

        _otherButton.Draw(target);
        _exitButton.Draw(target);
    }

    /// <summary>
    /// Cuts a line until it fits a column, <c>Text::fitToOneRow()</c>; the reference measures with the font of the row.
    /// </summary>
    /// <param name="font">The face that measures the text.</param>
    /// <param name="text">The text to fit.</param>
    /// <param name="maxWidth">The widest the result may be.</param>
    /// <returns>The text itself, or a cut version ending in an ellipsis.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    public static string FitToOneRow(IGlyphSource font, string text, int maxWidth) =>
        GlyphText.FitToOneRow(font, text, maxWidth);

    /// <summary>
    /// Which creature illustrates a row, <c>getMonsterByRating()</c> or <c>getMonsterByDay()</c>
    /// (<c>game_highscores.cpp:98-115</c>).
    /// </summary>
    /// <param name="mode">The table the row belongs to.</param>
    /// <param name="rating">The score, or the day count of a campaign.</param>
    /// <returns>The creature.</returns>
    public static MonsterType MonsterFor(HighScoreMode mode, uint rating) =>
        mode == HighScoreMode.Campaign
            ? HighScoreDataContainer.GetMonsterByDay(rating)
            : HighScoreDataContainer.GetMonsterByRating(rating);

    /// <summary>
    /// The first of the nine <c>MINIMON</c> frames of a creature, <c>Monster::GetSpriteIndex() * 9</c>.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The frame index of its standing picture.</returns>
    public static int MonsterFrameBase(MonsterType monster)
    {
        // GetSpriteIndex() 是「编号减一」：零号是未知生物，不进 MINIMON。
        int index = (int)monster - 1;

        return index <= 0 ? 0 : index * HighScoresArtwork.FramesPerMonster;
    }

    /// <summary>
    /// Which breathing frame a row shows, the expression inside <c>redrawMonstersAnimation()</c>
    /// (<c>game_highscores.cpp:172-173</c>).
    /// </summary>
    /// <param name="topX">Left edge of the background.</param>
    /// <param name="topY">Top edge of the background.</param>
    /// <param name="row">Zero based row index.</param>
    /// <param name="dayCount">The day count of the row, part of the reference's de-synchronisation.</param>
    /// <param name="animationFrameId"><c>monsterAnimationFrameId</c>, bumped once per map delay.</param>
    /// <returns>The index into <see cref="MonsterAnimationSequence.Frames"/>, <c>0</c> to <c>6</c>.</returns>
    public static int AnimationFrame(int topX, int topY, int row, uint dayCount, uint animationFrameId)
    {
        ReadOnlySpan<byte> sequence = MonsterAnimationSequence.Frames;
        long offsetY = topY + (row * HighScoresLayout.EntryStepY);
        long index = (topX + offsetY + dayCount + animationFrameId) % sequence.Length;

        return sequence[(int)index];
    }

    /// <summary>Points both buttons at the sprites and the offsets of the table on screen.</summary>
    private void ApplyMode()
    {
        IReadOnlyList<Sprite> button = _mode == HighScoreMode.Campaign
            ? _artwork.CampaignButton
            : _artwork.StandardButton;

        _otherButton.SetSprite(button[0], button[1]);

        Rect other = HighScoresLayout.OtherButtonArea(Area, new Size(button[0].Width, button[0].Height));

        _otherButton.SetPosition(other.X, other.Y);

        Rect exit = HighScoresLayout.ExitButtonArea(
            Area, new Size(_exitButton.Released.Width, _exitButton.Released.Height));

        _exitButton.SetPosition(exit.X, exit.Y);
    }

    /// <summary>
    /// Sets the held look of both buttons from the cursor, the <c>drawOnState( isMouseLeftButtonPressedAndHeldInArea )</c>
    /// of <c>game_highscores.cpp:428-432</c>.
    /// </summary>
    private void UpdatePressedStates()
    {
        SetButtonPressed(_otherButton, IsHeld(_otherButton, _pressedOther));
        SetButtonPressed(_exitButton, IsHeld(_exitButton, _pressedExit));
    }

    private bool IsHeld(ButtonBase button, bool pressed) =>
        pressed && _cursor is { } point && button.Area.Contains(point);

    private static void SetButtonPressed(ButtonBase button, bool pressed)
    {
        if (pressed)
        {
            button.Press();
        }
        else
        {
            button.Release();
        }
    }

    /// <summary>Paints one row: the four columns and the creature, <c>game_highscores.cpp:212-242</c>.</summary>
    private void DrawRow(PixelBuffer target, in Point top, int row, HighscoreData data)
    {
        IGlyphSource font = row == _selectedIndex ? _fonts.Yellow : _fonts.White;

        string name = FitToOneRow(font, data.PlayerName, HighScoresLayout.PlayerNameWidth);
        string scenario = FitToOneRow(font, data.ScenarioName, HighScoresLayout.ScenarioNameWidth);

        GlyphText.DrawLine(font, target, HighScoresLayout.PlayerNameAnchor(top, row), name);
        GlyphText.DrawLine(font, target, HighScoresLayout.ScenarioNameAnchor(top, row), scenario);
        GlyphText.DrawLine(font, target, HighScoresLayout.DayCountAnchor(top, row), Format(data.DayCount));
        GlyphText.DrawLine(font, target, HighScoresLayout.RatingAnchor(top, row), Format(data.Rating));

        DrawMonster(target, top, row, data);
    }

    private static string Format(uint value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Blits the breathing frame of a row's creature, <c>game_highscores.cpp:170-180</c>.</summary>
    private void DrawMonster(PixelBuffer target, in Point top, int row, HighscoreData data)
    {
        int frame = MonsterFrameBase(MonsterFor(_mode, data.Rating))
            + 1
            + AnimationFrame(top.X, top.Y, row, data.DayCount, _animationFrameId);

        if (frame < 0 || frame >= _artwork.Monsters.Count)
        {
            return;
        }

        Sprite sprite = _artwork.Monsters[frame];
        Point anchor = HighScoresLayout.MonsterAnchor(top, row);

        // 参考把 sprite 自己的偏移加在落点上（sprite.x() + position.x + 554），BlitSprite 不带偏移，这里补上。
        ImageOps.BlitSprite(target, new Point(anchor.X + sprite.OffsetX, anchor.Y + sprite.OffsetY), sprite);
    }
}
