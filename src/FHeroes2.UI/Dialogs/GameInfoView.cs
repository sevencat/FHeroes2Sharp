using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.System;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>Where a row of the scenario info window sits vertically.</summary>
/// <remarks>
/// 参考有两行标签（<c>Rating</c>、<c>Map Size</c>）是按底边对齐画的：
/// <c>text.draw( x, y + 78 - text.height( width ), width, display )</c>（<c>dialog_gameinfo.cpp:147-150</c>），
/// 端口把这件事记在行上，画的时候才用字体高度换算，不必在建行时就知道字体。
/// </remarks>
public enum GameInfoRowAnchor
{
    /// <summary><see cref="GameInfoRow.Y"/> is the top of the row.</summary>
    Top,

    /// <summary><see cref="GameInfoRow.Y"/> is the bottom of the row.</summary>
    Bottom,
}

/// <summary>How a row has to be squeezed into its column, the <c>fitToOneRow()</c>/<c>fitToArea()</c> pair.</summary>
public enum GameInfoRowFit
{
    /// <summary>Drawn as is; long text simply overflows, as the reference's plain <c>draw()</c> does.</summary>
    None,

    /// <summary>Cut down to one row with an ellipsis, <c>Text::fitToOneRow()</c>.</summary>
    SingleRow,

    /// <summary>Wrapped and, if it still does not fit, cut to the row count of the box, <c>Text::fitToArea()</c>.</summary>
    Area,
}

/// <summary>One line of text of the scenario info window, before it is drawn.</summary>
/// <param name="Text">The text; may contain line breaks.</param>
/// <param name="X">Left edge of the column, relative to the window.</param>
/// <param name="Y">Top or bottom edge of the row, see <paramref name="Anchor"/>.</param>
/// <param name="Width">Width of the column the text is centered in.</param>
/// <param name="Anchor">Whether <paramref name="Y"/> is the top or the bottom.</param>
/// <param name="Fit">How the text is made to fit, if at all.</param>
/// <param name="Height">Height of the box for <see cref="GameInfoRowFit.Area"/>; ignored otherwise.</param>
public readonly record struct GameInfoRow(
    string Text, int X, int Y, int Width, GameInfoRowAnchor Anchor = GameInfoRowAnchor.Top,
    GameInfoRowFit Fit = GameInfoRowFit.None, int Height = 0);

/// <summary>
/// The scenario info window as it is laid out: the <c>SCENIBKG</c> plate, the okay and about buttons and the
/// position of every text column, <c>Dialog::GameInfo()</c> (<c>dialog_gameinfo.cpp:52-201</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把文字按编译期常量直接画在 <c>SCENIBKG</c> 上。端口把这批常量收在 <see cref="GameInfoLayout"/> 里，
/// <see cref="BuildRows"/> 按地图信息把它们展开成待画的文字，于是「显示哪些行、写什么、放在哪一列」
/// 可以脱离图片单独测。
/// </para>
/// <para>
/// 窗口位置与参考一致：横向给影子留出 <see cref="GameInfoLayout.ShadowOffsetX"/> 的余量再居中，纵向直接居中，
/// 整套文字相对窗口左上角（参考的 <c>shadowOffset</c>）摆。注意参考把窗口画在 <c>shadowOffset</c> 上而不是
/// <c>dialogOffset</c> 上，两者差半个影子高度，看起来像是一个小失误，端口照搬。
/// </para>
/// </remarks>
public sealed class GameInfoView
{
    /// <summary>Archive entry of the scenario info plate, the <c>ICN::SCENIBKG</c> of the reference.</summary>
    public const string BackgroundAssetName = "SCENIBKG.ICN";

    /// <summary>Archive entry of the evil interface plate, the <c>ICN::SCENIBKG_EVIL</c>.</summary>
    public const string BackgroundEvilAssetName = "SCENIBKG_EVIL.ICN";

    /// <summary>Archive entry of the about button, the <c>ICN::BUTTON_MAP_ABOUT_GOOD</c>.</summary>
    public const string AboutAssetName = "BUTTON_MAP_ABOUT_GOOD.ICN";

    /// <summary>Archive entry of the evil about button, the <c>ICN::BUTTON_MAP_ABOUT_EVIL</c>.</summary>
    public const string AboutEvilAssetName = "BUTTON_MAP_ABOUT_EVIL.ICN";

    /// <summary>Archive entry of the okay button, the <c>ICN::BUTTON_SMALL_OKAY_GOOD</c>.</summary>
    public const string OkayAssetName = "BUTTON_SMALL_OKAY_GOOD.ICN";

    /// <summary>Archive entry of the evil okay button, the <c>ICN::BUTTON_SMALL_OKAY_EVIL</c>.</summary>
    public const string OkayEvilAssetName = "BUTTON_SMALL_OKAY_EVIL.ICN";

    private readonly Sprite _window;
    private readonly int _aboutWidth;

    private GameInfoView(PixelBuffer display, Sprite window, Sprite okayReleased, Sprite okayPressed,
        Sprite? aboutReleased, Sprite? aboutPressed)
    {
        _window = window;
        _aboutWidth = aboutReleased?.Width ?? 0;

        // dialog_gameinfo.cpp:95-96：横向先给影子让位再居中，文字全部相对 shadowOffset 摆。
        int x = (display.Width - window.Width - GameInfoLayout.ShadowOffsetX) / 2;
        int y = (display.Height - window.Height) / 2;
        Position = new Point(x, y);
        ShadowPosition = new Point(x + GameInfoLayout.ShadowOffsetX, y + (GameInfoLayout.ShadowOffsetY / 2));

        Okay = new ButtonSprite(okayReleased, okayPressed,
            offsetX: ShadowPosition.X + (GameInfoLayout.DialogContentWidth / 2) - (okayReleased.Width / 2),
            offsetY: ShadowPosition.Y + GameInfoLayout.OkayButtonY);

        if (aboutReleased is not null && aboutPressed is not null)
        {
            About = new ButtonSprite(aboutReleased, aboutPressed,
                offsetX: ShadowPosition.X + GameInfoLayout.AboutButtonRight - aboutReleased.Width,
                offsetY: ShadowPosition.Y + GameInfoLayout.AboutButtonY);
        }
    }

    /// <summary>Top left corner of the plate, i.e. the reference's <c>dialogOffset</c>.</summary>
    public Point Position { get; }

    /// <summary>Where the plate is drawn and everything else is placed from, the reference's <c>shadowOffset</c>.</summary>
    public Point ShadowPosition { get; }

    /// <summary>The plate itself, the <c>SCENIBKG</c> frame 0.</summary>
    public Sprite Window => _window;

    /// <summary>The okay button, at the bottom centre of the plate.</summary>
    public ButtonSprite Okay { get; }

    /// <summary>The about button; <see langword="null"/> when the map has no creator notes.</summary>
    public ButtonSprite? About { get; }

    /// <summary>Whether the map carries creator notes, i.e. <c>!mapInfo.creatorNotes.empty()</c>.</summary>
    public bool HasCreatorNotes => About is not null;

    /// <summary>Where the map name is drawn, already shrunk for the about button.</summary>
    public Rect NameArea => new(
        ShadowPosition.X + GameInfoLayout.NameX,
        ShadowPosition.Y + GameInfoLayout.NameY,
        GameInfoLayout.NameMaxWidth - _aboutWidth,
        19);

    /// <summary>Where the map description is drawn.</summary>
    public Rect DescriptionArea => new(
        ShadowPosition.X + GameInfoLayout.DescriptionX,
        ShadowPosition.Y + GameInfoLayout.DescriptionY,
        GameInfoLayout.DescriptionWidth,
        GameInfoLayout.DescriptionHeight);

    /// <summary>Where the row of player slots goes, i.e. the first point given to <c>PlayersInfo::UpdateInfo()</c>.</summary>
    public Point PlayersInfoPosition => new(
        ShadowPosition.X + GameInfoLayout.PlayerInfoRowOffset, ShadowPosition.Y + GameInfoLayout.PlayerInfoRowY);

    /// <summary>Where the row of player classes goes, i.e. the second point of <c>UpdateInfo()</c>.</summary>
    public Point PlayersClassPosition => new(
        ShadowPosition.X + GameInfoLayout.PlayerInfoRowOffset, ShadowPosition.Y + GameInfoLayout.PlayerInfoClassY);

    /// <summary>
    /// Composes the window from the frames of the archive.
    /// </summary>
    /// <param name="display">The frame the window is centered in.</param>
    /// <param name="background">Frames of <see cref="BackgroundAssetName"/>.</param>
    /// <param name="about">Frames of <see cref="AboutAssetName"/>.</param>
    /// <param name="okay">Frames of <see cref="OkayAssetName"/>.</param>
    /// <param name="hasCreatorNotes">Whether the map has notes, i.e. whether the about button is shown.</param>
    /// <returns><see langword="null"/> when an input is missing or too short.</returns>
    public static GameInfoView? TryCreate(PixelBuffer display, IReadOnlyList<Sprite>? background,
        IReadOnlyList<Sprite>? about, IReadOnlyList<Sprite>? okay, bool hasCreatorNotes)
    {
        ArgumentNullException.ThrowIfNull(display);

        if (background is not { Count: > 0 } || okay is not { Count: > 1 })
        {
            return null;
        }

        if (hasCreatorNotes && about is not { Count: > 1 })
        {
            return null;
        }

        return new GameInfoView(display, background[0], okay[0], okay[1],
            hasCreatorNotes ? about![0] : null, hasCreatorNotes ? about![1] : null);
    }

    /// <summary>
    /// The texts of the window, in drawing order, <c>dialog_gameinfo.cpp:118-196</c>.
    /// </summary>
    /// <param name="info">The map information block, for the name, description, size and difficulty.</param>
    /// <param name="gameDifficulty">The difficulty the player picked, the <c>Game::getDifficulty()</c>.</param>
    /// <param name="victoryText">The victory conditions sentence, the <c>GameOver::GetActualDescription()</c>.</param>
    /// <param name="lossText">The loss conditions sentence.</param>
    /// <param name="hasCreatorNotes">Whether the about button takes a bite out of the name column.</param>
    /// <param name="aboutButtonWidth">Width of the about button, <c>0</c> when it is not shown.</param>
    /// <returns>The rows, ready to be drawn with a small white font except for the name.</returns>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    public static IReadOnlyList<GameInfoRow> BuildRows(Mp2FileInfo info, GameDifficulty gameDifficulty,
        string victoryText, string lossText, bool hasCreatorNotes, int aboutButtonWidth)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(victoryText);
        ArgumentNullException.ThrowIfNull(lossText);

        // 参考把名字那一行让出关于按钮的宽度（dialog_gameinfo.cpp:119），没有笔记时整条都用。
        int nameWidth = GameInfoLayout.NameMaxWidth - (hasCreatorNotes ? aboutButtonWidth : 0);

        return
        [
            new GameInfoRow(info.Name, GameInfoLayout.NameX, GameInfoLayout.NameY, nameWidth,
                Fit: GameInfoRowFit.SingleRow),

            new GameInfoRow("Map\nDifficulty", GameInfoLayout.MapDifficultyX, GameInfoLayout.LabelY,
                GameInfoLayout.ValuesBoxWidth),
            new GameInfoRow("Game\nDifficulty", GameInfoLayout.GameDifficultyX, GameInfoLayout.LabelY,
                GameInfoLayout.ValuesBoxWidth),
            new GameInfoRow("Rating", GameInfoLayout.RatingX, GameInfoLayout.BottomLabelY,
                GameInfoLayout.ValuesBoxWidth, GameInfoRowAnchor.Bottom),
            new GameInfoRow("Map Size", GameInfoLayout.MapSizeX, GameInfoLayout.BottomLabelY,
                GameInfoLayout.ValuesBoxWidth, GameInfoRowAnchor.Bottom),

            new GameInfoRow(Difficulty.GetName(info.Difficulty), GameInfoLayout.MapDifficultyX,
                GameInfoLayout.ValueY, GameInfoLayout.ValuesBoxWidth),
            new GameInfoRow(Difficulty.GetName(gameDifficulty), GameInfoLayout.GameDifficultyX,
                GameInfoLayout.ValueY, GameInfoLayout.ValuesBoxWidth),
            new GameInfoRow($"{GameRating.GetRating(info, gameDifficulty)} %", GameInfoLayout.RatingX,
                GameInfoLayout.ValueY, GameInfoLayout.ValuesBoxWidth),
            new GameInfoRow(MapSizes.GetName(info.Width), GameInfoLayout.MapSizeX, GameInfoLayout.ValueY,
                GameInfoLayout.ValuesBoxWidth),

            new GameInfoRow(info.Description, GameInfoLayout.DescriptionX, GameInfoLayout.DescriptionY,
                GameInfoLayout.DescriptionWidth, Fit: GameInfoRowFit.Area,
                Height: GameInfoLayout.DescriptionHeight),

            new GameInfoRow("Opponents", 0, GameInfoLayout.OpponentsLabelY, GameInfoLayout.DialogContentWidth),
            new GameInfoRow("Class", 0, GameInfoLayout.ClassLabelY, GameInfoLayout.DialogContentWidth),

            new GameInfoRow("Victory\nConditions", GameInfoLayout.ConditionLabelX,
                GameInfoLayout.VictoryLabelY, GameInfoLayout.ConditionLabelWidth),
            new GameInfoRow(victoryText, GameInfoLayout.ConditionDescriptionX,
                GameInfoLayout.VictoryDescriptionY, GameInfoLayout.ConditionDescriptionWidth),

            new GameInfoRow("Loss\nConditions", GameInfoLayout.ConditionLabelX,
                GameInfoLayout.LossLabelY, GameInfoLayout.ConditionLabelWidth),
            new GameInfoRow(lossText, GameInfoLayout.ConditionDescriptionX,
                GameInfoLayout.LossDescriptionY, GameInfoLayout.ConditionDescriptionWidth),
        ];
    }

    /// <summary>
    /// Blits the plate and the two buttons, <c>dialog_gameinfo.cpp:100-201</c>.
    /// </summary>
    /// <param name="target">The frame to draw into.</param>
    /// <remarks>
    /// 参考用 <c>ImageRestorer</c> 存下窗口底下的一条再画；端口没有还原层，调用方每帧从场景重画。
    /// 影子是 <c>SCENIBKG</c> 自带的那 16×8 偏移，端口照搬，不额外生成影子精灵。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void DrawFrame(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, ShadowPosition, _window);
        Okay.Draw(target);
        About?.Draw(target);
    }
}

/// <summary>
/// The fixed coordinates of the scenario info window, the anonymous enum of
/// <c>dialog_gameinfo.cpp:52-78</c>.
/// </summary>
/// <remarks>
/// 名字与参考一一对应；参考用前几个常量算出来的列位置，这里写成算好的结果，免得加减藏在表达式里对不上。
/// </remarks>
public static class GameInfoLayout
{
    /// <summary>Width of a column of label/value pairs.</summary>
    public const int ValuesBoxWidth = 80;

    /// <summary>Width of the plate content, the <c>DIALOG_CONTENT_WIDTH</c>.</summary>
    public const int DialogContentWidth = 420;

    /// <summary>Margin between two columns.</summary>
    public const int BoxInnerMargin = 11;

    /// <summary>Distance from the plate border to the first column.</summary>
    public const int RowOffset = 18 + 16;

    /// <summary>Left edge of the "map difficulty" column.</summary>
    public const int MapDifficultyX = RowOffset;

    /// <summary>Left edge of the "game difficulty" column.</summary>
    public const int GameDifficultyX = RowOffset + ValuesBoxWidth + BoxInnerMargin;

    /// <summary>Left edge of the "rating" column.</summary>
    public const int RatingX = RowOffset + (ValuesBoxWidth * 2) + (BoxInnerMargin * 2);

    /// <summary>Left edge of the "map size" column.</summary>
    public const int MapSizeX = RowOffset + (ValuesBoxWidth * 3) + (BoxInnerMargin * 3);

    /// <summary>Horizontal shadow offset of the plate, the <c>DIALOG_SHADOW_OFFSET_X</c>.</summary>
    public const int ShadowOffsetX = 16;

    /// <summary>Vertical shadow offset of the plate, the <c>DIALOG_SHADOW_OFFSET_Y</c>.</summary>
    public const int ShadowOffsetY = 16;

    /// <summary>Left edge of the map name.</summary>
    public const int NameX = 37;

    /// <summary>Top edge of the map name.</summary>
    public const int NameY = 32;

    /// <summary>Width the map name may take, before the about button takes its bite.</summary>
    public const int NameMaxWidth = 349;

    /// <summary>Top edge of the two-line labels over the values.</summary>
    public const int LabelY = 56;

    /// <summary>Bottom edge of the one-line labels over the values.</summary>
    public const int BottomLabelY = 78;

    /// <summary>Top edge of the values themselves.</summary>
    public const int ValueY = 84;

    /// <summary>Left edge of the map description.</summary>
    public const int DescriptionX = 18 + 11 + 6;

    /// <summary>Top edge of the map description.</summary>
    public const int DescriptionY = 107;

    /// <summary>Width of the map description.</summary>
    public const int DescriptionWidth = 350;

    /// <summary>Height of the map description.</summary>
    public const int DescriptionHeight = 37;

    /// <summary>Top edge of the "opponents" label.</summary>
    public const int OpponentsLabelY = 152;

    /// <summary>Top edge of the row of player slots.</summary>
    public const int PlayerInfoRowY = 165;

    /// <summary>Distance from the plate border to the player slots.</summary>
    public const int PlayerInfoRowOffset = 18 + 6;

    /// <summary>Top edge of the "class" label.</summary>
    public const int ClassLabelY = 229;

    /// <summary>Top edge of the row of player classes.</summary>
    public const int PlayerInfoClassY = 240;

    /// <summary>Left edge of the condition labels.</summary>
    public const int ConditionLabelX = 18;

    /// <summary>Width of the condition labels.</summary>
    public const int ConditionLabelWidth = 93;

    /// <summary>Left edge of the condition sentences.</summary>
    public const int ConditionDescriptionX = 18 + 93 + 6;

    /// <summary>Width of the condition sentences.</summary>
    public const int ConditionDescriptionWidth = 266;

    /// <summary>Top edge of the victory label.</summary>
    public const int VictoryLabelY = 347;

    /// <summary>Top edge of the victory sentence.</summary>
    public const int VictoryDescriptionY = 350;

    /// <summary>Top edge of the loss label.</summary>
    public const int LossLabelY = 392;

    /// <summary>Top edge of the loss sentence.</summary>
    public const int LossDescriptionY = 398;

    /// <summary>Top edge of the okay button.</summary>
    public const int OkayButtonY = 426;

    /// <summary>Right edge of the about button.</summary>
    public const int AboutButtonRight = 401;

    /// <summary>Top edge of the about button.</summary>
    public const int AboutButtonY = 36;
}
