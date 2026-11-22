using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="PuzzleMapDialog"/>, the port of <c>drawPuzzle()</c>, <c>revealPuzzle()</c> and
/// <c>ShowStandardDialog()</c> of <c>kingdom/puzzle.cpp</c>. The window is driven by frames instead of a modal loop,
/// so everything it does can be checked on a plain buffer.
/// </summary>
public sealed class PuzzleMapDialogTests
{
    private const byte PictureColor = 5;
    private const byte PieceColor = 9;
    private const byte ThumbColor = 7;
    private const byte ExitReleasedColor = 3;
    private const byte ExitPressedColor = 4;

    private static readonly Rect RadarArea = new(464, 16, 144, 144);
    private static readonly Point PicturePosition = new(16, 16);

    [Fact]
    public void ExitButtonArea_IsCentredOnTheRadarAndSitsAboveItsBottomEdge()
    {
        PixelBuffer display = new(640, 480);

        PuzzleMapDialog dialog = CreateDialog(display);

        // Button buttonExit( radarArea.x + 32, radarArea.y + radarArea.height - 37, ... ) with the centring
        // setPosition() of puzzle.cpp:185-186.
        Rect area = dialog.ExitButtonArea;

        Assert.Equal(RadarArea.X + ((RadarArea.Width - 40) / 2), area.X);
        Assert.Equal(RadarArea.Bottom - 37, area.Y);
        Assert.True(RadarArea.Contains(area), "The exit button must sit on the radar.");
        Assert.True(dialog.IsExitButton(area.Center));
        Assert.False(dialog.IsExitButton(new Point(area.X - 1, area.Center.Y)));
    }

    [Fact]
    public void RenderRadarCover_PaintsTheThumbOverTheRadar()
    {
        PixelBuffer display = new(640, 480);

        PuzzleMapDialog dialog = CreateDialog(display);

        dialog.RenderRadarCover();

        Assert.Equal(ThumbColor, display[RadarArea.X + 10, RadarArea.Y + 10]);
        Assert.Equal(ThumbColor, display[RadarArea.Right - 1, RadarArea.Bottom - 1]);
    }

    [Fact]
    public void RenderExitButton_WithThePressedState_UsesTheSecondFrame()
    {
        PixelBuffer display = new(640, 480);

        PuzzleMapDialog dialog = CreateDialog(display);

        dialog.RenderExitButton();
        Assert.Equal(ExitReleasedColor, display[dialog.ExitButtonArea.Center.X, dialog.ExitButtonArea.Center.Y]);

        dialog.IsExitButtonPressed = true;
        dialog.RenderExitButton();
        Assert.Equal(ExitPressedColor, display[dialog.ExitButtonArea.Center.X, dialog.ExitButtonArea.Center.Y]);
    }

    [Fact]
    public void Render_WithTheJigsawHidden_PaintsThePiecesOverThePicture()
    {
        PixelBuffer display = new(640, 480);
        Puzzle puzzle = new();

        PuzzleMapDialog dialog = CreateDialog(display, puzzle);

        dialog.Render();

        Assert.Equal(PictureColor, display[PicturePosition.X, PicturePosition.Y]);
        Assert.Equal(PieceColor, display[PiecePosition(0).X, PiecePosition(0).Y]);
        Assert.Equal(PieceColor, display[PiecePosition(47).X, PiecePosition(47).Y]);
    }

    [Fact]
    public void Render_WithTheJigsawUncovered_ShowsThePictureOnly()
    {
        PixelBuffer display = new(640, 480);
        Puzzle puzzle = new();

        puzzle.Update(Puzzle.NumOfPuzzleTiles, Puzzle.NumOfPuzzleTiles);

        PuzzleMapDialog dialog = CreateDialog(display, puzzle);

        dialog.Render();

        for (int y = PicturePosition.Y; y < PicturePosition.Y + PuzzleMapDialog.PictureSize; ++y)
        {
            for (int x = PicturePosition.X; x < PicturePosition.X + PuzzleMapDialog.PictureSize; ++x)
            {
                Assert.Equal(PictureColor, display[x, y]);
            }
        }
    }

    [Fact]
    public void Render_WithAnUncoveredPieceAndNoPalette_Throws()
    {
        PixelBuffer display = new(640, 480);
        Puzzle puzzle = new();

        puzzle.Update(Puzzle.NumOfPuzzleTiles, Puzzle.NumOfPuzzleTiles);

        PuzzleMapDialog dialog = CreateDialog(display, puzzle);
        dialog.RevealAlpha = 200;

        Assert.Throws<InvalidOperationException>(dialog.Render);
    }

    [Fact]
    public void Render_WithAnUncoveredPieceAndAPalette_BlendsThePiece()
    {
        PixelBuffer display = new(640, 480);
        Puzzle puzzle = new();

        puzzle.Update(Puzzle.NumOfPuzzleTiles, Puzzle.NumOfPuzzleTiles);

        // A black palette keeps the test free of game data: every blend maps to the first entry, so the uncovered
        // piece must stop showing the picture underneath without ever matching either colour.
        Palette palette = Palette.FromGamePalette(new byte[768]);

        PuzzleMapDialog dialog = CreateDialog(display, puzzle, palette);
        dialog.RevealAlpha = PuzzleMapDialog.RevealStartAlpha;

        dialog.Render();

        Assert.NotEqual(PictureColor, display[PiecePosition(0).X, PiecePosition(0).Y]);
    }

    [Fact]
    public void AdvanceReveal_CountsDownAndReportsTheEnd()
    {
        PixelBuffer display = new(640, 480);

        PuzzleMapDialog dialog = CreateDialog(display);

        Assert.Equal(0, dialog.RevealAlpha);

        dialog.RestartReveal();
        Assert.Equal(PuzzleMapDialog.RevealStartAlpha, dialog.RevealAlpha);

        int ticks = 0;

        while (!dialog.AdvanceReveal())
        {
            ++ticks;

            Assert.True(ticks <= PuzzleMapDialog.RevealStartAlpha, "The animation never ended.");
        }

        // 250 down to 0 in steps of 10.
        Assert.Equal((PuzzleMapDialog.RevealStartAlpha / PuzzleMapDialog.RevealStep) - 1, ticks);
        Assert.Equal(0, dialog.RevealAlpha);

        // A finished animation stays finished.
        Assert.True(dialog.AdvanceReveal());
        Assert.Equal(0, dialog.RevealAlpha);
    }

    [Fact]
    public void Ctor_WithAMalformedPicture_Throws()
    {
        PixelBuffer display = new(640, 480);

        Assert.Throws<ArgumentException>(() => new PuzzleMapDialog(
            display, RadarArea, PicturePosition, new PixelBuffer(100, 100), new Puzzle(),
            CreatePieces(), CreateThumb(), CreateExitFrames()));
    }

    [Fact]
    public void Ctor_WithTheWrongNumberOfPieces_Throws()
    {
        PixelBuffer display = new(640, 480);

        Assert.Throws<ArgumentException>(() => new PuzzleMapDialog(
            display, RadarArea, PicturePosition, CreatePicture(), new Puzzle(),
            CreatePieces()[..10], CreateThumb(), CreateExitFrames()));
    }

    [Fact]
    public void Ctor_WithoutAPressedExitFrame_Throws()
    {
        PixelBuffer display = new(640, 480);

        Assert.Throws<ArgumentException>(() => new PuzzleMapDialog(
            display, RadarArea, PicturePosition, CreatePicture(), new Puzzle(),
            CreatePieces(), CreateThumb(), [CreateExitFrames()[0]]));
    }

    [Fact]
    public void Show_ThrowsWhenNoEngineIsAttached()
    {
        PixelBuffer display = new(640, 480);
        PuzzleMapDialog dialog = CreateDialog(display);

        Assert.Throws<InvalidOperationException>(() => dialog.Show(new LocalEvent(() => 0)));
    }

    [Fact]
    public void Show_ClosesOnAClickOnTheExitButton()
    {
        PixelBuffer display = new(640, 480);
        PuzzleMapDialog dialog = CreateDialog(display);
        Point point = dialog.ExitButtonArea.Center;

        LocalEvent events = new(() => 0);
        FakeEngine engine = new()
        {
            OnFrame = (state, frame) =>
            {
                if (frame == 0)
                {
                    state.OnMouseMotion(point);
                    state.OnMouseButton(true, MouseButtonType.Left, point);
                }
                else
                {
                    state.OnMouseButton(false, MouseButtonType.Left, point);
                }
            },
        };

        events.Engine = engine;

        dialog.Show(events);

        // Press, release and leave: the reference returns from revealPuzzle() on the same click.
        Assert.Equal(2, engine.Frames);

        // The button was repainted with its released frame, then again with the released state.
        Assert.False(dialog.IsExitButtonPressed);
        Assert.Equal(ExitReleasedColor, display[dialog.ExitButtonArea.Center.X, dialog.ExitButtonArea.Center.Y]);
    }

    [Fact]
    public void Show_ClosesOnEscape()
    {
        PixelBuffer display = new(640, 480);
        PuzzleMapDialog dialog = CreateDialog(display);

        LocalEvent events = new(() => 0);
        FakeEngine engine = new()
        {
            OnFrame = (state, frame) =>
                state.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down),
        };

        events.Engine = engine;

        dialog.Show(events);

        Assert.Equal(1, engine.Frames);
    }

    [Fact]
    public void Show_ReturnsWhenTheEngineStops()
    {
        PixelBuffer display = new(640, 480);
        PuzzleMapDialog dialog = CreateDialog(display);

        LocalEvent events = new(() => 0) { Engine = new FakeEngine { FramesBeforeStop = 0 } };

        dialog.Show(events);
    }

    /// <summary>An event engine that plays a scripted frame at a time and stops after a few of them.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public int FramesBeforeStop { get; init; } = 8;

        public int Frames { get; private set; }

        public Action<LocalEvent, int>? OnFrame { get; set; }

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (Frames >= FramesBeforeStop)
            {
                return false;
            }

            OnFrame?.Invoke(state, Frames++);

            return true;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    /// <summary>Builds the window the tests paint with.</summary>
    /// <param name="display">The frame the window paints into.</param>
    /// <param name="puzzle">The jigsaw state; an empty one when omitted.</param>
    /// <param name="palette">The palette of the reveal animation; none when omitted.</param>
    private static PuzzleMapDialog CreateDialog(PixelBuffer display, Puzzle? puzzle = null, Palette? palette = null) =>
        new(display,
            RadarArea,
            PicturePosition,
            CreatePicture(),
            puzzle ?? new Puzzle(),
            CreatePieces(),
            CreateThumb(),
            CreateExitFrames(),
            palette);

    /// <summary>Where <c>PUZZLE.ICN</c> puts the anchor of a piece: the reference writes <c>piece.x()</c> and the
    /// border width cancels out with the standard picture position, so the anchor is the screen position.</summary>
    /// <param name="index">Piece index.</param>
    private static Point PiecePosition(int index) => new(300 + (index * 2), 360);

    private static PixelBuffer CreatePicture()
    {
        PixelBuffer picture = new(PuzzleMapDialog.PictureSize, PuzzleMapDialog.PictureSize);
        picture.Fill(PictureColor);

        return picture;
    }

    private static Sprite[] CreatePieces()
    {
        Sprite[] pieces = new Sprite[Puzzle.NumOfPuzzleTiles];

        for (int i = 0; i < pieces.Length; ++i)
        {
            Point position = PiecePosition(i);

            pieces[i] = new Sprite(2, 2, position.X, position.Y);
            pieces[i].Image.Fill(PieceColor);
        }

        return pieces;
    }

    private static Sprite CreateThumb()
    {
        Sprite thumb = new(RadarArea.Width, RadarArea.Height);
        thumb.Image.Fill(ThumbColor);

        return thumb;
    }

    private static Sprite[] CreateExitFrames()
    {
        Sprite released = new(40, 20);
        released.Image.Fill(ExitReleasedColor);

        Sprite pressed = new(40, 20);
        pressed.Image.Fill(ExitPressedColor);

        return [released, pressed];
    }
}
