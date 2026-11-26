using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where every part of the high score screen goes, the constants of <c>game_highscores.cpp:78-91</c> plus the two button
/// positions of <c>openHighScores()</c> (<c>:381-385</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把这些偏移量写成文件级常量，全部相对屏幕中央那张 <c>HSBKG</c> 图的左上角（<c>top</c>）。端口保留同一套数字，
/// 让像素位置可以和参考逐格对照：四列从左到右是人名、地图名、天数、评分，最右边一列画兵种小图。
/// </para>
/// <para>
/// 两张表都画十行，行距 40；超过十行的容器数据不会画出来（读文件时已经截断，这里再兜一层）。
/// </para>
/// </remarks>
public static class HighScoresLayout
{
    /// <summary>Horizontal offset of the title inside the background, <c>50</c>.</summary>
    public const int TitleOffsetX = 50;

    /// <summary>Vertical offset of the title inside the background, <c>31</c>.</summary>
    public const int TitleOffsetY = 31;

    /// <summary>Top of the first row inside the background, <c>initialHighScoreEntryOffsetY</c>.</summary>
    public const int InitialEntryOffsetY = 72;

    /// <summary>Distance between two rows, <c>highScoreEntryStepY</c>.</summary>
    public const int EntryStepY = 40;

    /// <summary>Left edge of the player name column, <c>playerNameOffset</c>.</summary>
    public const int PlayerNameOffsetX = 88;

    /// <summary>Left edge of the scenario name column, <c>scenarioNameOffset</c>.</summary>
    public const int ScenarioNameOffsetX = 244;

    /// <summary>Left edge of the day count column, <c>dayCountOffset</c>.</summary>
    public const int DayCountOffsetX = 403;

    /// <summary>Left edge of the rating column, <c>ratingOffset</c>.</summary>
    public const int RatingOffsetX = 484;

    /// <summary>Left edge of the monster column, <c>monsterOffsetX</c>.</summary>
    public const int MonsterOffsetX = 554;

    /// <summary>Top of the first monster inside the background, <c>monsterOffsetY</c>.</summary>
    public const int MonsterOffsetY = 91;

    /// <summary>Left edge of the table switch button, <c>top.x + 8</c>.</summary>
    public const int OtherButtonOffsetX = 8;

    /// <summary>Top of both buttons, <c>top.y + 315</c>.</summary>
    public const int ButtonOffsetY = 315;

    /// <summary>Distance from the right edge of the background to the exit button, <c>back.width() - 36</c>.</summary>
    public const int ExitButtonRightMargin = 36;

    /// <summary>How many rows are drawn, <c>maxDisplayedEntries</c>.</summary>
    public const int MaximumDisplayedEntries = 10;

    /// <summary>Width the player name may take before it is cut, <c>scenarioNameOffset - playerNameOffset</c>.</summary>
    public const int PlayerNameWidth = ScenarioNameOffsetX - PlayerNameOffsetX;

    /// <summary>Width the map or campaign name may take before it is cut, <c>dayCountOffset - scenarioNameOffset</c>.</summary>
    public const int ScenarioNameWidth = DayCountOffsetX - ScenarioNameOffsetX;

    /// <summary>Top left corner of the background on a screen, <c>top</c> of the reference.</summary>
    /// <param name="screen">Size of the screen the background is centred on.</param>
    /// <param name="background">Size of the background itself.</param>
    /// <returns>The centred rectangle.</returns>
    public static Rect Top(Size screen, Size background) => new(
        (screen.Width - background.Width) / 2,
        (screen.Height - background.Height) / 2,
        background.Width,
        background.Height);

    /// <summary>Top of a row's text, <c>offsetY + initialHighScoreEntryOffsetY</c>.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="row">Zero based row index.</param>
    /// <returns>The y coordinate.</returns>
    public static int RowBaselineY(in Point top, int row) =>
        top.Y + InitialEntryOffsetY + (row * EntryStepY);

    /// <summary>Where the player name of a row starts.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="row">Zero based row index.</param>
    /// <returns>The position.</returns>
    public static Point PlayerNameAnchor(in Point top, int row) =>
        new(top.X + PlayerNameOffsetX, RowBaselineY(top, row));

    /// <summary>Where the map name of a row starts.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="row">Zero based row index.</param>
    /// <returns>The position.</returns>
    public static Point ScenarioNameAnchor(in Point top, int row) =>
        new(top.X + ScenarioNameOffsetX, RowBaselineY(top, row));

    /// <summary>Where the day count of a row starts.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="row">Zero based row index.</param>
    /// <returns>The position.</returns>
    public static Point DayCountAnchor(in Point top, int row) =>
        new(top.X + DayCountOffsetX, RowBaselineY(top, row));

    /// <summary>Where the rating of a row starts.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="row">Zero based row index.</param>
    /// <returns>The position.</returns>
    public static Point RatingAnchor(in Point top, int row) =>
        new(top.X + RatingOffsetX, RowBaselineY(top, row));

    /// <summary>Where the monster of a row is blitted, <c>sprite.x() + top.x + 554</c>.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="row">Zero based row index.</param>
    /// <returns>The position the sprite is anchored at, its own offsets included.</returns>
    public static Point MonsterAnchor(in Point top, int row) =>
        new(top.X + MonsterOffsetX, top.Y + MonsterOffsetY + (row * EntryStepY));

    /// <summary>Where the title image is blitted.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <returns>The position.</returns>
    public static Point TitleAnchor(in Point top) => new(top.X + TitleOffsetX, top.Y + TitleOffsetY);

    /// <summary>The area the table switch button occupies.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="size">Size of the button sprites.</param>
    /// <returns>The area.</returns>
    public static Rect OtherButtonArea(in Rect top, in Size size) =>
        new(top.X + OtherButtonOffsetX, top.Y + ButtonOffsetY, size.Width, size.Height);

    /// <summary>The area the exit button occupies.</summary>
    /// <param name="top">Top left corner of the background.</param>
    /// <param name="size">Size of the button sprites.</param>
    /// <returns>The area.</returns>
    public static Rect ExitButtonArea(in Rect top, in Size size) =>
        new(top.X + top.Width - ExitButtonRightMargin, top.Y + ButtonOffsetY, size.Width, size.Height);
}
