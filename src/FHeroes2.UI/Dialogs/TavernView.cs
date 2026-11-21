using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The tavern window, <c>Castle::_openTavern()</c> (<c>castle_tavern.cpp:39-54</c>): the barkeep of
/// <c>TAVWIN.ICN</c> over the rumor of the week, with a single OK button.
/// </summary>
/// <remarks>
/// <para>
/// The reference builds three dialog elements and hands them to
/// <c>fheroes2::showStandardTextMessage( GetStringBuilding( BUILD_TAVERN ), "", Dialog::OK, elements )</c>:
/// an <c>AnimationDialogElement( ICN::TAVWIN, { 0, 1 }, 0, Game::getAnimationDelayValue(
/// Game::DelayType::CASTLE_TAVERN_DELAY ) )</c> and a <c>TextDialogElement</c> holding the lead line
/// and the rumor as one <c>MultiFontText</c>. That is exactly what <see cref="MessageBox"/> packs, so the
/// port keeps the composition in one place and adds no window class of its own - the model stays in
/// <see cref="TavernDialog"/>.
/// </para>
/// <para>
/// <b>The two paragraphs stay two runs.</b> The reference puts the lead in the normal white font and the
/// rumor in the white font of the language the rumor was written in. The port has no per-language
/// fonts (the localization subsystem is not ported, ADR-139), so both runs use
/// <see cref="FontType.NormalWhite"/> and only the <c>"\n\n"</c> between them shows.
/// </para>
/// <para>
/// <b>The animation range is passed in, not looked up.</b> <c>TAVWIN</c> has two background frames and
/// twenty frames of the barkeep, and the reference maps a frame counter to <c>2 + n % 20</c> through
/// <c>ICN::getAnimatedIcnIndex()</c> (<c>agg/icn.cpp:314-317</c>). The caller reads the ICN file and
/// hands the sprites over, like every other artwork in the port.
/// </para>
/// </remarks>
public static class TavernView
{
    /// <summary>
    /// Time one frame of the barkeep stays on screen, <c>Game::DelayType::CASTLE_TAVERN_DELAY</c>
    /// (<c>game_delays.cpp:52</c>) is 75 ms.
    /// </summary>
    public const int FrameDelayMilliseconds = 75;

    /// <summary>The frames drawn under the animation, <c>{ 0, 1 }</c> at <c>castle_tavern.cpp:50</c>.</summary>
    public static IReadOnlyList<int> BackgroundFrames { get; } = [0, 1];

    /// <summary>First frame of the animation; frames 0 and 1 are the background (<c>agg/icn.cpp:315</c>).</summary>
    public const int FirstAnimatedFrame = 2;

    /// <summary>Number of animated frames, <c>2 + frame % 20</c> of the reference.</summary>
    public const int AnimatedFrameCount = 20;

    /// <summary>
    /// Composes the window for <paramref name="dialog"/>.
    /// </summary>
    /// <param name="dialog">The rumor to tell and the single OK button.</param>
    /// <param name="tavwin">Sprites of <c>TAVWIN.ICN</c>, in file order.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c> in the reference.</param>
    /// <param name="bodyFont">Font of the body, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="buttonArtwork">Source of the OK sprite.</param>
    /// <param name="startY">Top of the frame, or a negative value to center it on the screen.</param>
    /// <param name="screen">Screen the frame is centered on; 640x480 when omitted.</param>
    /// <returns>
    /// The window, or <see langword="null"/> when <paramref name="tavwin"/> has no animation frames or the
    /// archive has no message frame.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dialog"/>, <paramref name="tavwin"/>, <paramref name="headerFont"/>,
    /// <paramref name="bodyFont"/> or <paramref name="buttonArtwork"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The archive has no message frame, see <see cref="FrameBox.Create"/>.
    /// </exception>
    public static MessageBox? TryCreate(TavernDialog dialog, IReadOnlyList<Sprite> tavwin,
        IGlyphSource headerFont, IGlyphSource bodyFont, IDialogButtonArtworkSource buttonArtwork, int startY = -1,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(tavwin);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(buttonArtwork);

        int frameCount = Math.Min(AnimatedFrameCount, tavwin.Count - FirstAnimatedFrame);

        if (frameCount <= 0 || !FrameBox.IsArtworkAvailable)
        {
            return null;
        }

        AnimationDialogElement barkeep = new(tavwin, BackgroundFrames, FirstAnimatedFrame, frameCount,
            TimeSpan.FromMilliseconds(FrameDelayMilliseconds));

        MultiFontText text = new();
        text.Add(new Text(TavernDialog.Lead + "\n\n", FontType.NormalWhite()));
        text.Add(new Text(dialog.Rumor.Text, FontType.NormalWhite()));

        return MessageBox.Create(TavernDialog.Title, string.Empty, headerFont, bodyFont,
            [barkeep, new TextDialogElement(text)], startY, screen, dialog.Buttons, buttonArtwork);
    }
}
