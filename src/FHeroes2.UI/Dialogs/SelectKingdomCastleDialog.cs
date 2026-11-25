using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The factory and the loop of <c>Dialog::selectKingdomCastle()</c>
/// (<c>dialog_selectitems.cpp:846-904</c>): the list of towns Town Portal asks for a destination with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which towns.</b> The reference walks the castles of a kingdom and can skip the ones a hero stands in,
/// because Town Portal may not send the hero to the town he is already in. It then opens
/// <see cref="SelectKingdomCastleWindow"/> and returns the selected town's gate tile index, or -1 when the
/// player cancelled. An empty list shows the "There are no castles to select from." warning instead of an
/// empty window.
/// </para>
/// <para>
/// <b>Size.</b> 350 pixels wide, five rows minimum and twelve maximum, and never taller than the display minus
/// <see cref="DialogHeightDeduction"/> so the buttons stay on screen.
/// </para>
/// </remarks>
public static class SelectKingdomCastleDialog
{
    /// <summary>Width of the window, <c>{ 350, ... }</c>.</summary>
    public const int DialogWidth = 350;

    /// <summary>Room the display keeps free under the window, <c>dialogHeightDeduction = 150</c>.</summary>
    public const int DialogHeightDeduction = 150;

    /// <summary>Rows the window always has room for, <c>100 + itemsOffsetY * 5</c>.</summary>
    public const int MinimumRows = 5;

    /// <summary>Rows the window never grows past, <c>100 + itemsOffsetY * 12</c>.</summary>
    public const int MaximumRows = 12;

    /// <summary>Title of the warning an empty list shows, <c>_( "Warning" )</c>.</summary>
    public const string EmptyListHeading = "Warning";

    /// <summary>Body of that warning, <c>_( "There are no castles to select from." )</c>.</summary>
    public const string EmptyListMessage = "There are no castles to select from.";

    /// <summary>
    /// Height of the window for a number of towns, the reference's
    /// <c>std::clamp( 100 + itemsOffsetY * castles.size(), 100 + itemsOffsetY * 5, maxHeight )</c>.
    /// </summary>
    /// <param name="castleCount">Number of towns the list holds.</param>
    /// <param name="screen">Screen the window is centred on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>Size of the active area.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="castleCount"/> is negative.</exception>
    public static Size DialogSize(int castleCount, Size? screen = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(castleCount);

        Size display = screen ?? ScreenBuffer.DefaultSize;
        int rows = Math.Clamp(castleCount, MinimumRows, MaximumRows);
        int maximumHeight = Math.Min(100 + (SelectKingdomCastleWindow.RowHeight * MaximumRows),
            display.Height - DialogHeightDeduction);

        return new Size(DialogWidth, Math.Min(100 + (SelectKingdomCastleWindow.RowHeight * rows), maximumHeight));
    }

    /// <summary>
    /// Builds the window, or <see langword="null"/> when there is nothing to show. The caller is expected to
    /// show <see cref="EmptyListHeading"/> / <see cref="EmptyListMessage"/> in that case, as the reference does.
    /// </summary>
    /// <param name="castles">The towns to list, in the order they are shown.</param>
    /// <param name="sprites">Where the town icons, the scroll bar and the buttons come from.</param>
    /// <param name="treasury">Resources of the towns' owner, which decide the build markers.</param>
    /// <param name="title">Heading of the window.</param>
    /// <param name="description">Optional explanation under the heading.</param>
    /// <param name="artwork">Frames of the scroll bar and the two small buttons.</param>
    /// <param name="titleFont">Yellow font of the heading.</param>
    /// <param name="font">White font of the description and the rows.</param>
    /// <param name="castlePositionIndex">
    /// Town to select on open, i.e. the tile index of its gate (<c>castlePositionIndex</c> of the reference);
    /// -1 leaves the first row selected.
    /// </param>
    /// <param name="evilInterface">Whether the evil interface is in effect.</param>
    /// <param name="castleInfo">What a right click on a row opens, the reference's castle quick info.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The laid out window, or <see langword="null"/> for an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="castles"/>, <paramref name="sprites"/>, <paramref name="artwork"/>,
    /// <paramref name="titleFont"/> or <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public static SelectKingdomCastleWindow? TryCreate(IReadOnlyList<CastleTown> castles,
        IViewWorldSpriteSource sprites, Funds treasury, string title, string description,
        ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font,
        int castlePositionIndex = -1, bool evilInterface = false, Action<CastleTown>? castleInfo = null,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(castles);

        if (castles.Count == 0)
        {
            return null;
        }

        SelectKingdomCastleWindow window = new(castles, sprites, treasury, DialogSize(castles.Count, screen),
            title, description, artwork, titleFont, font, evilInterface, castleInfo, screen);

        window.SelectTown(castlePositionIndex);

        return window;
    }

    /// <summary>
    /// Runs the window until one of its buttons closes it, the <c>while ( le.HandleEvents() )</c> loop of
    /// <c>Dialog::selectKingdomCastle()</c> (<c>dialog_selectitems.cpp:886-903</c>).
    /// </summary>
    /// <remarks>
    /// The frame is composed from scratch on every iteration: <see cref="ItemSelectionWindow.Draw"/> paints the
    /// window over <paramref name="target"/>, so the caller must hand in a buffer that already holds the scene
    /// behind it.
    /// </remarks>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <param name="window">The window to run.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// Tile index of the selected town's gate, or -1 when the window was cancelled or the client asked to quit.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="window"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public static int Show(PixelBuffer target, SelectKingdomCastleWindow window, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(window);

        LocalEvent input = events ?? LocalEvent.Current;
        IEventEngine engine = input.Engine ?? throw new InvalidOperationException(
            "SelectKingdomCastleDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        while (!window.IsClosed)
        {
            // The window owns the frame: no presentation and no sleep inside HandleEvents, the loop submits
            // the frame after drawing and then waits, like the reference's render() + 1 ms pair.
            if (!input.HandleEvents(sleepAfterEventProcessing: false))
            {
                return -1;
            }

            window.ProcessEvents(input, ReadHotKey(input));

            if (window.IsClosed)
            {
                break;
            }

            window.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return window.Result == DialogButton.Ok ? window.SelectedPositionIndex : -1;
    }

    /// <summary>The hot key of the frame, <c>DEFAULT_OKAY</c> and <c>DEFAULT_CANCEL</c>.</summary>
    /// <param name="events">Input of the frame.</param>
    /// <returns>The key the two buttons answer, or <see langword="null"/> when neither was pressed.</returns>
    private static ButtonHotKey? ReadHotKey(LocalEvent events)
    {
        if (events.IsKeyPressed(Key.Enter) || events.IsKeyPressed(Key.Space))
        {
            return ButtonHotKey.Okay;
        }

        return events.IsKeyPressed(Key.Escape) ? ButtonHotKey.Cancel : null;
    }
}
