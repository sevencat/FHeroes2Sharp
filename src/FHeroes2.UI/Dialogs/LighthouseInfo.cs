using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The "Lighthouses" box of <c>showLighthouseInfo()</c> (<c>gui/ui_kingdom.cpp:51-90</c>): a title, the sentence
/// "For every lighthouse controlled, your ships will move further each day.", the lighthouse itself with its light
/// animation and the number the kingdom owns.
/// </summary>
/// <remarks>
/// <para>
/// The reference assembles the lighthouse out of six frames of <c>OBJNMUL2</c> — three shadow pieces and the three
/// storeys — into one 160x(64+top) image, then animates the light on top of it with the nine frames the ICN table
/// gives for <c>OBJNMUL2</c> at index 61 (<c>agg/icn.cpp:549-553</c>: <c>61 + frame % 9 + 1</c>, so frames 62-70),
/// and opens a standard text message with the two elements.
/// </para>
/// <para>
/// As in <see cref="KingdomIncome"/>, the box is a <see cref="MessageBox"/> with elements and the artwork is handed
/// in instead of being read from the global archive. The header is <c>MP2::StringObject( OBJ_LIGHTHOUSE, 2 )</c>,
/// i.e. the plural, which <see cref="FHeroes2.Assets.Maps.Mp2ObjectName"/> does not produce because it names one
/// object at a time.
/// </para>
/// <para>
/// The reference's buttons are <c>Dialog::ZERO</c> from the kingdom overview's right click and <c>Dialog::OK</c> from
/// the left click on the lighthouse; the box here is the <c>ZERO</c> variant, exactly like <see cref="KingdomIncome"/>:
/// the caller decides when the box lives, and the OK variant is not ported yet.
/// </para>
/// </remarks>
public static class LighthouseInfo
{
    /// <summary>Side of a map tile in pixels, <c>fheroes2::tileWidthPx</c>.</summary>
    public const int TileWidth = UiConstants.TileWidthPx;

    /// <summary>Title of the box, <c>StringObject( MP2::OBJ_LIGHTHOUSE, 2 )</c>.</summary>
    public const string Header = "Lighthouses";

    /// <summary>Body of the box, <c>_("For every lighthouse controlled, your ships will move further each day.")</c>.</summary>
    public const string Body = "For every lighthouse controlled, your ships will move further each day.";

    /// <summary>The shadow above the lighthouse, <c>ICN::OBJNMUL2</c> frame 60.</summary>
    public const int ShadowTopFrame = 60;

    /// <summary>The middle shadow, <c>ICN::OBJNMUL2</c> frame 71.</summary>
    public const int ShadowMiddleFrame = 71;

    /// <summary>The shadow below the lighthouse, <c>ICN::OBJNMUL2</c> frame 72.</summary>
    public const int ShadowBottomFrame = 72;

    /// <summary>The top storey, <c>ICN::OBJNMUL2</c> frame 59.</summary>
    public const int LighthouseTopFrame = 59;

    /// <summary>The middle storey, <c>ICN::OBJNMUL2</c> frame 61.</summary>
    public const int LighthouseMiddleFrame = 61;

    /// <summary>The bottom storey, <c>ICN::OBJNMUL2</c> frame 73.</summary>
    public const int LighthouseBottomFrame = 73;

    /// <summary>The frame whose position the light animation uses, <c>ICN::OBJNMUL2</c> frame 62.</summary>
    public const int LighthouseLightFrame = 62;

    /// <summary>Index the reference passes to <c>ICN::getAnimatedIcnIndex()</c>, <c>0x3D</c>.</summary>
    public const int AnimationIndexOffset = 61;

    /// <summary>First animated frame, <c>animationOffset + 1</c> (<c>agg/icn.cpp:553</c>).</summary>
    public const int FirstAnimatedFrame = 62;

    /// <summary>Number of animated frames, the <c>% 9</c> of <c>agg/icn.cpp:553</c>.</summary>
    public const int AnimatedFrameCount = 9;

    /// <summary>
    /// Length of one frame of the light, <c>getAnimationDelayValue( Game::DelayType::MAPS_DELAY )</c>
    /// (<c>game_delays.cpp:59</c>); the caller may pass the scaled value of <c>GameDelays</c> instead.
    /// </summary>
    public const int FrameDelayMilliseconds = 250;

    /// <summary>
    /// Composes the box for a kingdom that owns <paramref name="lighthouseCount"/> lighthouses.
    /// </summary>
    /// <param name="lighthouseCount">
    /// <c>world.CountCapturedObject( MP2::OBJ_LIGHTHOUSE, kingdom.GetColor() )</c>.
    /// </param>
    /// <param name="objnmul2">Sprites of <c>OBJNMUL2.ICN</c>, in file order.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c> in the reference.</param>
    /// <param name="bodyFont">Font of the message, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="header">Translated title; <see cref="Header"/> when omitted.</param>
    /// <param name="body">Translated message; <see cref="Body"/> when omitted.</param>
    /// <param name="frameDelayMilliseconds">Length of one light frame; <see cref="FrameDelayMilliseconds"/> when omitted.</param>
    /// <param name="startY">Top of the frame, or a negative value to center it.</param>
    /// <param name="screen">Screen the box is centered on; 640x480 when omitted.</param>
    /// <returns>The box with the animated lighthouse and the count.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="objnmul2"/>, <paramref name="headerFont"/> or <paramref name="bodyFont"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="objnmul2"/> has fewer than 74 frames.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameDelayMilliseconds"/> is not positive.</exception>
    public static MessageBox Create(uint lighthouseCount, IReadOnlyList<Sprite> objnmul2, IGlyphSource headerFont,
        IGlyphSource bodyFont, string header = Header, string body = Body,
        int frameDelayMilliseconds = FrameDelayMilliseconds, int startY = -1, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(objnmul2);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        if (objnmul2.Count <= LighthouseBottomFrame)
        {
            throw new ArgumentException(
                $"The lighthouse artwork must have at least {LighthouseBottomFrame + 1} frames, it had {objnmul2.Count}.",
                nameof(objnmul2));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameDelayMilliseconds);

        Sprite shadowTop = objnmul2[ShadowTopFrame];
        Sprite shadowMiddle = objnmul2[ShadowMiddleFrame];
        Sprite shadowBottom = objnmul2[ShadowBottomFrame];
        Sprite lighthouseTop = objnmul2[LighthouseTopFrame];
        Sprite lighthouseMiddle = objnmul2[LighthouseMiddleFrame];
        Sprite lighthouseBottom = objnmul2[LighthouseBottomFrame];
        Sprite lighthouseLight = objnmul2[LighthouseLightFrame];

        // The top storey is the tallest piece and sets where the tower stands on the two-tile-tall picture (:63).
        int topOffset = TileWidth - lighthouseTop.Height;

        // The six pieces into one blank picture, each at the position the reference copies it to (:65-76).
        Sprite combined = new(5 * TileWidth, (2 * TileWidth) + lighthouseTop.Height);

        ImageOps.BlitSprite(combined,
            new Point(shadowTop.OffsetX, TileWidth + shadowTop.OffsetY - topOffset), shadowTop);
        ImageOps.BlitSprite(combined,
            new Point(shadowMiddle.OffsetX, (2 * TileWidth) + shadowMiddle.OffsetY - topOffset), shadowMiddle);
        ImageOps.BlitSprite(combined,
            new Point(TileWidth + shadowBottom.OffsetX, (2 * TileWidth) + shadowBottom.OffsetY - topOffset),
            shadowBottom);
        ImageOps.BlitSprite(combined,
            new Point((2 * TileWidth) + lighthouseTop.OffsetX, lighthouseTop.OffsetY - topOffset), lighthouseTop);
        ImageOps.BlitSprite(combined,
            new Point((2 * TileWidth) + lighthouseMiddle.OffsetX,
                TileWidth + lighthouseMiddle.OffsetY - topOffset), lighthouseMiddle);
        ImageOps.BlitSprite(combined,
            new Point((2 * TileWidth) + lighthouseBottom.OffsetX,
                (2 * TileWidth) + lighthouseBottom.OffsetY - topOffset), lighthouseBottom);

        CustomAnimationDialogElement lighthouse = new(
            combined,
            new Point((2 * TileWidth) + lighthouseLight.OffsetX, TileWidth + lighthouseLight.OffsetY - topOffset),
            objnmul2,
            FirstAnimatedFrame,
            AnimatedFrameCount,
            TimeSpan.FromMilliseconds(frameDelayMilliseconds));

        TextDialogElement count = new(new Text(
            lighthouseCount.ToString(CultureInfo.InvariantCulture), FontType.NormalWhite()));

        return MessageBox.Create(header, body, headerFont, bodyFont, [lighthouse, count], startY, screen);
    }
}
