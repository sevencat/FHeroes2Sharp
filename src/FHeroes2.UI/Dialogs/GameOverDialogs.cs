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
/// Where the colour sign of <see cref="GameOverDialogs.ShowVanquished"/> comes from, the two <c>ICN::BRCREST</c>
/// frames the reference blits together (<c>game_startgame.cpp:256-282</c>).
/// </summary>
public interface IVanquishedSignArtworkSource
{
    /// <summary>The empty sign, <c>ICN::BRCREST</c> frame <c>6</c>.</summary>
    Sprite Border { get; }

    /// <summary>The crest of <paramref name="color"/>, <c>ICN::BRCREST</c> frames <c>0</c>-<c>5</c>.</summary>
    /// <param name="color">One of the six player colours.</param>
    /// <returns>The crest, expected to be non empty.</returns>
    Sprite Crest(PlayerColor color);
}

/// <summary>
/// The two result windows of the reference and the player sign:
/// <c>DialogWins()</c>/<c>DialogLoss()</c> (<c>game_over.cpp:71-246</c>) and
/// <c>Game::DialogPlayers()</c> (<c>game_startgame.cpp:251-286</c>).
/// </summary>
/// <remarks>
/// <para>
/// 三个窗口都是一句话加一个 OK：<c>Victory!</c>/<c>Defeat!</c> 用黄字标题、白字正文，与参考的
/// <c>FontType::normalYellow()</c> / <c>normalWhite()</c> 对应，字体由调用方给（端口没有全局字体单例）。
/// </para>
/// <para>
/// 参考在这三个函数里自己起音乐：胜利用 <c>MUS::VICTORY</c>、失败用 <c>MUS::LOSTGAME</c>、踢人窗口不换音乐。
/// 端口不碰音频，调用方可以先播——<see cref="GameOverResult.Requests"/> 已经把该播哪一首记成
/// <see cref="GameOverRequests.VictoryMusic"/>，两边对得上。
/// </para>
/// <para>
/// 胜/负窗口的文案由 <see cref="GameOver.GetVictoryMessage"/> / <see cref="GameOver.GetLossMessage"/> 产出；
/// 这两张表按完全相等匹配，没有对应句子的条件（比如 <see cref="GameOver.WinsAll"/>）参考根本不弹窗，这里返回
/// <see cref="DialogButton.None"/> 且一帧都不画。
/// </para>
/// </remarks>
public static class GameOverDialogs
{
    /// <summary>Title of the victory window, <see cref="GameOver.GetVictoryMessage"/>'s <c>"Victory!"</c>.</summary>
    public const string VictoryHeader = "Victory!";

    /// <summary>Title of the defeat window, <see cref="GameOver.GetLossMessage"/>'s <c>"Defeat!"</c>.</summary>
    public const string DefeatHeader = "Defeat!";

    /// <summary>Where the crest sits inside the border, the <c>Blit(..., 4, 4 )</c> of the reference.</summary>
    public const int CrestOffset = 4;

    /// <summary>
    /// Shows the victory window of <c>DialogWins()</c> (<c>game_over.cpp:71-155</c>).
    /// </summary>
    /// <param name="target">Frame to draw into and to submit; it must contain the scene behind the window.</param>
    /// <param name="condition">The verdict of <see cref="GameOverResult.CheckGameOver"/>.</param>
    /// <param name="info">The map information block the condition parameters come from.</param>
    /// <param name="headerFont">Font of the title; yellow in the reference.</param>
    /// <param name="bodyFont">Font of the body; white in the reference.</param>
    /// <param name="buttonArtwork">Artwork of the OK button.</param>
    /// <param name="world">World to look objects up in; <see cref="World.Current"/> when omitted.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// The button that closed the window, or <see cref="DialogButton.None"/> when the condition has no dialog or the
    /// client was asked to exit.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="target"/>, <paramref name="info"/>, a font or <paramref name="buttonArtwork"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static DialogButton ShowVictory(PixelBuffer target, uint condition, Mp2FileInfo info,
        IGlyphSource headerFont, IGlyphSource bodyFont, IDialogButtonArtworkSource buttonArtwork,
        World? world = null, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(info);

        return ShowMessage(
            target,
            VictoryHeader,
            GameOver.GetVictoryMessage(condition, info, world),
            headerFont,
            bodyFont,
            buttonArtwork,
            events);
    }

    /// <summary>
    /// Shows the defeat window of <c>DialogLoss()</c> (<c>game_over.cpp:157-246</c>).
    /// </summary>
    /// <param name="target">Frame to draw into and to submit; it must contain the scene behind the window.</param>
    /// <param name="condition">The verdict of <see cref="GameOverResult.CheckGameOver"/>.</param>
    /// <param name="info">The map information block the condition parameters come from.</param>
    /// <param name="headerFont">Font of the title; yellow in the reference.</param>
    /// <param name="bodyFont">Font of the body; white in the reference.</param>
    /// <param name="buttonArtwork">Artwork of the OK button.</param>
    /// <param name="world">World to look objects up in; <see cref="World.Current"/> when omitted.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// The button that closed the window, or <see cref="DialogButton.None"/> when the condition has no dialog or the
    /// client was asked to exit.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="target"/>, <paramref name="info"/>, a font or <paramref name="buttonArtwork"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static DialogButton ShowDefeat(PixelBuffer target, uint condition, Mp2FileInfo info,
        IGlyphSource headerFont, IGlyphSource bodyFont, IDialogButtonArtworkSource buttonArtwork,
        World? world = null, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(info);

        return ShowMessage(
            target,
            DefeatHeader,
            GameOver.GetLossMessage(condition, info, world),
            headerFont,
            bodyFont,
            buttonArtwork,
            events);
    }

    /// <summary>
    /// Tells a player that he is out of the game, <c>Game::DialogPlayers()</c>
    /// (<c>game_startgame.cpp:251-286</c>): the title, the sentence with <c>%{color}</c> filled in, and the colour
    /// crest of that player under it.
    /// </summary>
    /// <param name="target">Frame to draw into and to submit; it must contain the scene behind the window.</param>
    /// <param name="color">The colour whose player is addressed.</param>
    /// <param name="title">Title of the window, e.g. <c>"Beware!"</c>.</param>
    /// <param name="message">Body text; <c>%{color}</c> is replaced by the player name.</param>
    /// <param name="artwork">Source of the sign and its crest.</param>
    /// <param name="headerFont">Font of the title; yellow in the reference.</param>
    /// <param name="bodyFont">Font of the body; white in the reference.</param>
    /// <param name="buttonArtwork">Artwork of the OK button.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>The button that closed the window, or <see cref="DialogButton.None"/> when the client exits.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="color"/> is not one of the six player colours.</exception>
    public static DialogButton ShowVanquished(PixelBuffer target, PlayerColor color, string title, string message,
        IVanquishedSignArtworkSource artwork, IGlyphSource headerFont, IGlyphSource bodyFont,
        IDialogButtonArtworkSource buttonArtwork, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(buttonArtwork);

        Player? player = Players.Get(color);
        string body = message.Replace("%{color}", player?.Name ?? PlayerColors.Describe(color), StringComparison.Ordinal);

        MessageBox box = MessageBox.Create(
            title,
            body,
            headerFont,
            bodyFont,
            [new CustomImageDialogElement(ComposeSign(artwork, color))],
            buttons: DialogButton.Ok,
            buttonArtwork: buttonArtwork);

        return MessageBox.Show(target, box, events);
    }

    /// <summary>
    /// Blits a colour crest onto the empty sign, the <c>switch ( color )</c> of
    /// <c>game_startgame.cpp:256-282</c>.
    /// </summary>
    /// <param name="artwork">Source of the two frames.</param>
    /// <param name="color">One of the six player colours.</param>
    /// <returns>The composed sign.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="color"/> is not one of the six player colours.</exception>
    public static Sprite ComposeSign(IVanquishedSignArtworkSource artwork, PlayerColor color)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        if ((PlayerColors.All & color) == 0 || (color & (color - 1)) != 0)
        {
            throw new ArgumentException("A player sign needs exactly one player colour.", nameof(color));
        }

        Sprite border = artwork.Border;
        Sprite sign = new(new PixelBuffer(border.Width, border.Height), border.OffsetX, border.OffsetY);

        ImageOps.BlitSprite(sign, Point.Zero, border);
        ImageOps.BlitSprite(sign, new Point(CrestOffset, CrestOffset), artwork.Crest(color));

        return sign;
    }

    /// <summary>
    /// The shared "title, body, OK" path of <see cref="ShowVictory"/> and <see cref="ShowDefeat"/>.
    /// </summary>
    /// <param name="target">Frame to draw into and to submit.</param>
    /// <param name="header">Title of the window.</param>
    /// <param name="body">Body text; empty means the reference would not show the window at all.</param>
    /// <param name="headerFont">Font of the title.</param>
    /// <param name="bodyFont">Font of the body.</param>
    /// <param name="buttonArtwork">Artwork of the OK button.</param>
    /// <param name="events">Event state to pull from.</param>
    /// <returns>The button that closed the window, or <see cref="DialogButton.None"/>.</returns>
    private static DialogButton ShowMessage(PixelBuffer target, string header, string body, IGlyphSource headerFont,
        IGlyphSource bodyFont, IDialogButtonArtworkSource buttonArtwork, LocalEvent? events)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (body.Length == 0)
        {
            return DialogButton.None;
        }

        return MessageBox.Show(
            target,
            header,
            body,
            headerFont,
            bodyFont,
            buttons: DialogButton.Ok,
            buttonArtwork: buttonArtwork,
            events: events);
    }
}
