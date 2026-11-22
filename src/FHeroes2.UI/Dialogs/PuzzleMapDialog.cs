using System.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The window that shows the jigsaw the obelisks uncovered, <c>drawPuzzle()</c>, <c>revealPuzzle()</c> and
/// <c>ShowStandardDialog()</c> of <c>kingdom/puzzle.cpp:85-220</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference holds a modal loop: it fades the adventure map out, covers the radar with the small picture of the
/// whole jigsaw, fades the picture in, plays the reveal animation and finally waits for the exit button. The port has
/// no modal loops, so the window is a frame producer instead — the scene calls <see cref="Render"/> whenever the frame
/// is dirty and <see cref="AdvanceReveal"/> on every tick of the puzzle animation delay.
/// </para>
/// <para>
/// <b>Frame position.</b> <c>ShowStandardDialog()</c> paints the 448×448 picture into the 640×480 frame itself, at the
/// border offset; <c>ShowExtendedDialog()</c> puts the same picture into a movable window. Only the first form is
/// ported for now: the desktop client is a fixed 640×480 surface, and the windowed form needs <c>StandardWindow</c>
/// chrome that the hide-interface mode has not been wired to yet.
/// </para>
/// <para>
/// <b>Deviations.</b> The reference reads every frame from the global <c>AGG</c> cache and blends through the palette
/// of the display; the port takes the loaded frames and the palette as arguments, the same choice the panels of the
/// adventure screen make.
/// </para>
/// </remarks>
public sealed class PuzzleMapDialog
{
    /// <summary>Side of the picture, <c>result.resize( 448, 448 )</c> of the surface generator.</summary>
    public const int PictureSize = 448;

    /// <summary>Opacity the reveal animation starts at, <c>int alpha = 250</c> (<c>puzzle.cpp:122</c>).</summary>
    public const byte RevealStartAlpha = 250;

    /// <summary>How much the reveal animation loses per tick, <c>alpha -= 10</c> (<c>puzzle.cpp:158</c>).</summary>
    public const int RevealStep = 10;

    /// <summary>Gap between the exit button and the bottom of the radar, <c>- 37</c> (<c>puzzle.cpp:185</c>).</summary>
    private const int ExitButtonBottomOffset = 37;

    /// <summary>Shadow the exit button is given, <c>{ -5, 5 }</c> (<c>puzzle.cpp:187</c>).</summary>
    private static readonly Point ExitShadowOffset = new(-5, 5);

    private readonly PixelBuffer _display;
    private readonly Rect _radarArea;
    private readonly Point _picturePosition;
    private readonly PixelBuffer _picture;
    private readonly Puzzle _puzzle;
    private readonly Sprite[] _pieces;
    private readonly Sprite _radarThumb;
    private readonly Sprite[] _exitFrames;
    private readonly Palette? _blendPalette;

    /// <summary>Builds the window around the picture and its artwork.</summary>
    /// <param name="display">The frame the window paints into.</param>
    /// <param name="radarArea">Where the radar of the adventure screen sits, <c>radar.GetArea()</c>.</param>
    /// <param name="picturePosition">Top left corner of the picture in the frame, <c>borderWidthPx</c> in the standard window.</param>
    /// <param name="picture">The picture itself, <c>UltimateArtifact::GetPuzzleMapSurface()</c>, 448×448.</param>
    /// <param name="puzzle">Which tiles are uncovered.</param>
    /// <param name="pieces">The 48 frames of <c>PUZZLE.ICN</c>, in order.</param>
    /// <param name="radarThumb">The whole picture as shown on the radar, frame 0 of <c>VIEWPUZL</c>/<c>EVIWPUZL</c>.</param>
    /// <param name="exitFrames">
    /// The released and the pressed frame of the small exit button, <c>BUTTON_SMALL_EXIT_GOOD</c> or its evil twin.
    /// </param>
    /// <param name="blendPalette">
    /// The game palette the reveal animation blends through, or <see langword="null"/> when no animation is played: a
    /// window with a hidden tile and no palette cannot draw its pieces and says so instead of guessing colours.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="picture"/> is empty or is not <see cref="PictureSize"/>², <paramref name="pieces"/> is not the
    /// 48 frames of the jigsaw, or <paramref name="exitFrames"/> holds fewer than two frames.
    /// </exception>
    public PuzzleMapDialog(
        PixelBuffer display,
        Rect radarArea,
        Point picturePosition,
        PixelBuffer picture,
        Puzzle puzzle,
        Sprite[] pieces,
        Sprite radarThumb,
        Sprite[] exitFrames,
        Palette? blendPalette = null)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(puzzle);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(radarThumb);
        ArgumentNullException.ThrowIfNull(exitFrames);

        if (picture.Width != PictureSize || picture.Height != PictureSize)
        {
            throw new ArgumentException(
                $"The puzzle picture is {picture.Width}x{picture.Height} but the jigsaw needs {PictureSize}x{PictureSize}.",
                nameof(picture));
        }

        if (pieces.Length != Puzzle.NumOfPuzzleTiles)
        {
            throw new ArgumentException(
                $"The jigsaw has {Puzzle.NumOfPuzzleTiles} pieces but {pieces.Length} were given.", nameof(pieces));
        }

        if (exitFrames.Length < 2)
        {
            throw new ArgumentException("The exit button needs a released and a pressed frame.", nameof(exitFrames));
        }

        _display = display;
        _radarArea = radarArea;
        _picturePosition = picturePosition;
        _picture = picture;
        _puzzle = puzzle;
        _pieces = pieces;
        _radarThumb = radarThumb;
        _exitFrames = exitFrames;
        _blendPalette = blendPalette;

        // Button buttonExit( radarArea.x + 32, radarArea.y + radarArea.height - 37, ... ) followed by the centring
        // setPosition() of puzzle.cpp:185-186: the horizontal start is only a seed, the height is not.
        int width = exitFrames[0].Width;
        int height = exitFrames[0].Height;

        ExitButtonArea = new Rect(
            radarArea.X + ((radarArea.Width - width) / 2),
            radarArea.Y + radarArea.Height - ExitButtonBottomOffset,
            width,
            height);
    }

    /// <summary>Where the exit button sits, <c>buttonExit.area()</c>.</summary>
    public Rect ExitButtonArea { get; }

    /// <summary>
    /// Opacity of the uncovered pieces, <c>alpha</c> of <c>revealPuzzle()</c>: <c>0</c> reveals the picture fully and
    /// <c>255</c> leaves every piece opaque, which is why a window that plays no animation starts at <c>0</c>.
    /// </summary>
    public byte RevealAlpha { get; set; }

    /// <summary>Whether the exit button is drawn in its pressed state, <c>drawOnState()</c>.</summary>
    public bool IsExitButtonPressed { get; set; }

    /// <summary>Whether a point lands on the exit button, <c>le.MouseClickLeft( buttonExit.area() )</c>.</summary>
    /// <param name="point">Point in frame coordinates.</param>
    /// <returns><see langword="true"/> when the point is on the button.</returns>
    public bool IsExitButton(Point point) => ExitButtonArea.Contains(point);

    /// <summary>Paints the whole window: the radar cover, the exit button and the picture.</summary>
    /// <remarks>
    /// The radar cover is painted first and covers the button, so that a repeated call stays idempotent — the gradient
    /// shadow of the button is a transform, and a transform applied twice would darken the button further.
    /// </remarks>
    public void Render()
    {
        RenderRadarCover();
        RenderExitButton();
        RenderPicture();
    }

    /// <summary>Paints the small picture over the radar, <c>Copy( VIEWPUZL/EVIWPUZL, radarArea )</c>.</summary>
    public void RenderRadarCover() =>
        ImageOps.BlitSprite(_display, _radarArea.Position, _radarThumb);

    /// <summary>Paints the exit button and its shadow, <c>addGradientShadow()</c> plus <c>draw()</c>.</summary>
    public void RenderExitButton()
    {
        ImageOps.AddGradientShadow(_exitFrames[0], _display, ExitButtonArea.Position, ExitShadowOffset);
        ImageOps.BlitSprite(_display, ExitButtonArea.Position, IsExitButtonPressed ? _exitFrames[1] : _exitFrames[0]);
    }

    /// <summary>
    /// Paints the picture and the pieces on top of it, <c>drawPuzzle()</c> and the body of the
    /// <c>revealPuzzle()</c> loop (<c>puzzle.cpp:85-160</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A piece has to be blended and no <see cref="Palette"/> was given to the constructor.
    /// </exception>
    /// <remarks>
    /// A piece is placed by its own anchor, and the border width is taken off again: the frames of <c>PUZZLE.ICN</c>
    /// are cut for a 640×480 frame that already holds the border, so the reference writes
    /// <c>dstx + piece.x() - borderWidthPx</c> and the port follows it verbatim.
    /// </remarks>
    public void RenderPicture()
    {
        ImageOps.Blit(_display, _picturePosition, _picture, _picture.Bounds);

        for (int i = 0; i < Puzzle.NumOfPuzzleTiles; ++i)
        {
            Sprite piece = _pieces[i];
            Point position = new(
                _picturePosition.X + piece.OffsetX - GameBorder.BorderWidth,
                _picturePosition.Y + piece.OffsetY - GameBorder.BorderWidth);

            if (_puzzle[i])
            {
                BlitUncoveredPiece(piece, position);
            }
            else
            {
                ImageOps.BlitSprite(_display, position, piece);
            }
        }
    }

    /// <summary>
    /// Advances the reveal animation by one tick, the body of the <c>if</c> the <c>revealPuzzle()</c> loop runs on
    /// every animation delay (<c>puzzle.cpp:135-160</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the animation is over, <c>alpha &lt;= 0</c>.</returns>
    public bool AdvanceReveal()
    {
        if (RevealAlpha == 0)
        {
            return true;
        }

        RevealAlpha = RevealAlpha <= RevealStep ? (byte)0 : (byte)(RevealAlpha - RevealStep);

        return RevealAlpha == 0;
    }

    /// <summary>Starts the reveal animation from the top, <c>int alpha = 250</c>.</summary>
    public void RestartReveal() => RevealAlpha = RevealStartAlpha;

    /// <summary>How long one reveal step waits, <c>Game::PUZZLE_FADE_DELAY</c> of <c>puzzle.cpp:119</c>.</summary>
    public const int RevealStepDelayMs = 10;

    /// <summary>
    /// Runs the window until the player closes it, the reveal loop and the wait loop of
    /// <c>Dialog::PuzzleMap()</c> (<c>puzzle.cpp:119-210</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference splits the dialog in two: <c>revealPuzzle()</c> fades the uncovered pieces out while the exit
    /// button already works, then a plain wait loop keeps the button alive. Both loops do the same three things per
    /// frame - the pressed state of the button, the click that closes the window, and a repaint - so the port keeps
    /// them as two loops over one frame body.
    /// </para>
    /// <para>
    /// The right-click hint of the reference (<c>showStandardTextMessage( _( "Exit" ), _( "Exit this menu." ) )</c>)
    /// is not wired yet: the port's message box needs its artwork passed in, which this dialog does not own. A right
    /// click does nothing until the caller that owns the cursor and the font gives the hint a home.
    /// </para>
    /// <para>
    /// The window draws into the frame it was built with, so the caller has to pass that same frame to the
    /// presenter; the port submits the whole <see cref="IEventEngine.ScreenArea"/> because the radar, the button and
    /// the picture all live inside it.
    /// </para>
    /// </remarks>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public void Show(LocalEvent? events = null)
    {
        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "PuzzleMapDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        Stopwatch stepClock = Stopwatch.StartNew();

        while (RevealAlpha > 0)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return;
            }

            if (ApplyFrame(eventState, engine))
            {
                return;
            }

            if (stepClock.ElapsedMilliseconds >= RevealStepDelayMs)
            {
                stepClock.Restart();
                AdvanceReveal();
            }
        }

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            if (ApplyFrame(eventState, engine))
            {
                break;
            }
        }
    }

    /// <summary>
    /// One frame of <see cref="Show"/>: button state, close request, repaint and presentation.
    /// </summary>
    /// <param name="eventState">Event state of the frame.</param>
    /// <param name="engine">Engine the frame is presented through.</param>
    /// <returns><see langword="true"/> when the player asked to close the window.</returns>
    private bool ApplyFrame(LocalEvent eventState, IEventEngine engine)
    {
        IsExitButtonPressed = eventState.IsMouseLeftButtonPressedAndHeldInArea(ExitButtonArea);

        bool closing = eventState.MouseClickLeft(ExitButtonArea) || eventState.IsKeyPressed(Key.Escape);

        Render();
        engine.Render(engine.ScreenArea);

        if (!closing)
        {
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return closing;
    }

    /// <summary>Draws an uncovered piece, <c>fheroes2::AlphaBlit( piece, ..., pieceAlpha )</c>.</summary>
    /// <param name="piece">The piece.</param>
    /// <param name="position">Where its anchor goes.</param>
    /// <exception cref="InvalidOperationException">The piece has to be blended and there is no palette.</exception>
    private void BlitUncoveredPiece(Sprite piece, Point position)
    {
        if (RevealAlpha == 0)
        {
            // Fully revealed: the piece is gone and the picture shows through.
            return;
        }

        if (RevealAlpha == 255)
        {
            // An animation cannot start at full opacity, but a caller may set it: the reference degrades this to a
            // plain blit as well.
            ImageOps.BlitSprite(_display, position, piece);

            return;
        }

        if (_blendPalette is null)
        {
            throw new InvalidOperationException(
                "The reveal animation needs the game palette to blend the uncovered pieces through.");
        }

        ImageOps.AlphaBlitSpriteClipped(
            _display,
            position,
            piece,
            piece.Image.Bounds,
            _display.Bounds,
            RevealAlpha,
            false,
            _blendPalette);
    }
}
