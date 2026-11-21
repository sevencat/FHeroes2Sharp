using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The frame every Heroes II message is drawn in, a port of <c>Dialog::ResizableFrameBox</c>
/// (<c>src/fheroes2/dialog/dialog_box.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The frame is built from the two columns of <c>BUYBUILD.ICN</c>: the top and the bottom are single
/// 99 and 81 pixel tall pieces, the middle is a 35 pixel tall slice of the column art (taken from row
/// 10 of the sprite, where the pattern starts) repeated as often as the requested height needs. The
/// columns are placed on both sides of the 244 pixel wide text area, so the artwork - not a nine slice
/// grid - decides what the box looks like.
/// </para>
/// <para>
/// The layout is the arithmetic of the reference, including its asymmetries: the columns are 161 and
/// 145 pixels wide, therefore the seam sits one pixel left of the middle of a 288 pixel window and the
/// text area starts at <c>x = 198</c> on a 640 pixel wide screen. <see cref="Area"/> never shrinks
/// below two middle slices (70 pixels), which is why a one line message still gets the smallest box the
/// original has.
/// </para>
/// <para>
/// Like the reference (<c>topHeight()</c>, <c>leftWidth()</c>, <c>redraw()</c> of <c>dialog_box.cpp</c>)
/// the six pieces are read from the archive by the frame itself, <c>BUYBUILE.ICN</c> when the evil
/// interface is enabled. Nothing is cached: every frame asks <see cref="GameAssets.GetImage"/>, which
/// keeps its own cache.
/// </para>
/// </remarks>
public sealed class FrameBox
{
    /// <summary>Width of the text area, <c>fheroes2::boxAreaWidthPx</c>.</summary>
    public const int AreaWidth = 244;

    /// <summary>Horizontal distance between the text area and the outside of the frame, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int BorderWidth = 16;

    /// <summary>Gap between the border and the text area, <c>offsetFromBorder</c> of the reference.</summary>
    public const int CornerOffset = 6;

    /// <summary>Width of the whole window, <c>boxAreaWidthPx + (borderWidthPx + offsetFromBorder) * 2</c>.</summary>
    public const int WindowWidth = AreaWidth + ((BorderWidth + CornerOffset) * 2);

    /// <summary>Height of one middle slice, <c>activeAreaHeight</c> of the reference.</summary>
    public const int ActiveAreaHeight = 35;

    /// <summary>Extra height a window with buttons reserves at the bottom, <c>buttonHeight</c> of the reference.</summary>
    public const int ButtonAreaHeight = 40;

    /// <summary>
    /// First row of the middle sprite that is drawn: the columns have a 10 pixel tall decorative
    /// top, and the repeatable pattern starts below it.
    /// </summary>
    public const int MiddleSourceOffsetY = 10;

    /// <summary>Smallest text area height: the two slices that are always drawn.</summary>
    public const int MinimumAreaHeight = ActiveAreaHeight * 2;

    /// <summary>Frame of the right column's top piece; the middle and the bottom follow it.</summary>
    public const int RightFrame = 0;

    /// <summary>Frame of the left column's top piece; the middle and the bottom follow it.</summary>
    public const int LeftFrame = 4;

    /// <summary>Number of sprites the table must have: the left column ends at <c>6</c>.</summary>
    public const int RequiredSpriteCount = LeftFrame + 3;

    private readonly Piece[] _pieces;

    private FrameBox(Rect area, Rect frameArea, Point origin, int middleFragmentCount, int middleFragmentHeight,
        Piece[] pieces)
    {
        Area = area;
        FrameArea = frameArea;
        Origin = origin;
        MiddleFragmentCount = middleFragmentCount;
        MiddleFragmentHeight = middleFragmentHeight;
        _pieces = pieces;
    }

    /// <summary>Area the caller may draw text into, the <c>GetArea()</c> of the reference.</summary>
    public Rect Area { get; }

    /// <summary>Bounding box of every blit, i.e. the part of the screen the frame covers.</summary>
    public Rect FrameArea { get; }

    /// <summary>Top left corner of the composed artwork; the columns are placed relative to it.</summary>
    public Point Origin { get; }

    /// <summary>Number of repeated middle slices, <c>_middleFragmentCount</c> of the reference.</summary>
    public int MiddleFragmentCount { get; }

    /// <summary>Total height the middle slices fill, <c>_middleFragmentHeight</c> of the reference.</summary>
    public int MiddleFragmentHeight { get; }

    /// <summary>
    /// Whether the archive holds <c>BUYBUILD.ICN</c> (or <c>BUYBUILE.ICN</c> for the evil interface) with
    /// every column piece, i.e. whether <see cref="Create"/> can compose a frame.
    /// </summary>
    public static bool IsArtworkAvailable =>
        GameAssets.GetImageCount(ArtworkFileName) >= RequiredSpriteCount;

    /// <summary>
    /// Archive entry the pieces are read from, <c>ICN::BUYBUILD</c> (<c>ICN::BUYBUILE</c> while the evil
    /// interface is enabled). Callers that want to know whether a frame can be drawn read it from here.
    /// </summary>
    public static string ArtworkFileName =>
        IcnCatalog.GetFileName(Settings.Get().IsEvilInterfaceEnabled ? IcnType.Buybuile : IcnType.Buybuild);

    /// <summary>
    /// Composes a frame for a text area of <paramref name="height"/> pixels.
    /// </summary>
    /// <param name="height">Requested text area height; the result is never shorter than <see cref="MinimumAreaHeight"/>.</param>
    /// <param name="showButtons">
    /// Reserves <see cref="ButtonAreaHeight"/> pixels for a row of buttons, like
    /// <c>ResizableFrameBox( ..., showButtons )</c>.
    /// </param>
    /// <param name="startY">
    /// Vertical position of the top row of pieces, or a negative value to center the middle of the
    /// frame on the screen (the default of the reference).
    /// </param>
    /// <param name="screen">Screen the frame is centered on; 640x480 when omitted.</param>
    /// <exception cref="InvalidOperationException">
    /// The archive has no frame artwork, i.e. <see cref="GameAssets.Current"/> is not set or holds no
    /// usable <see cref="ArtworkFileName"/>. The reference asserts in <c>Assets::getImage</c>.
    /// </exception>
    public static FrameBox Create(int height, bool showButtons = false, int startY = -1, Size? screen = null)
    {
        Columns chrome = Columns.Load();
        Size surface = screen ?? ScreenBuffer.DefaultSize;
        int contentHeight = Math.Max(height, 0) + (showButtons ? ButtonAreaHeight : 0);

        int middleFragmentCount = contentHeight <= MinimumAreaHeight
            ? 0
            : 1 + ((contentHeight - MinimumAreaHeight) / ActiveAreaHeight);
        int middleFragmentHeight = contentHeight <= MinimumAreaHeight ? 0 : contentHeight - MinimumAreaHeight;

        int leftOffset = chrome.LeftWidth - (WindowWidth / 2);
        int originX = ((surface.Width - WindowWidth) / 2) - leftOffset;
        int originY = startY >= 0
            ? startY
            : ((surface.Height - middleFragmentHeight) / 2) - chrome.TopHeight;

        Rect area = new(
            originX + ((WindowWidth - AreaWidth) / 2) + leftOffset,
            originY + (chrome.TopHeight - ActiveAreaHeight),
            AreaWidth,
            MinimumAreaHeight + middleFragmentHeight);

        List<Piece> pieces = new(4 + (middleFragmentCount * 2));
        Rect frameArea = Rect.Empty;

        Add(pieces, ref frameArea, chrome.LeftTop,
            new Point(originX + chrome.LeftWidth - chrome.LeftTop.Width, originY));
        Add(pieces, ref frameArea, chrome.RightTop, new Point(originX + chrome.LeftWidth, originY));

        int cursorY = originY + chrome.LeftTop.Height;
        int beforeMiddle = cursorY;
        int remaining = middleFragmentHeight;

        for (int index = 0; index < middleFragmentCount; ++index)
        {
            int chunkHeight = Math.Min(ActiveAreaHeight, remaining);
            if (chunkHeight <= 0)
            {
                break;
            }

            Add(pieces, ref frameArea, chrome.LeftMiddle,
                new Rect(0, MiddleSourceOffsetY, chrome.LeftMiddle.Width, chunkHeight),
                new Point(originX + chrome.LeftWidth - chrome.LeftMiddle.Width, cursorY));
            Add(pieces, ref frameArea, chrome.RightMiddle,
                new Rect(0, MiddleSourceOffsetY, chrome.RightMiddle.Width, chunkHeight),
                new Point(originX + chrome.LeftWidth, cursorY));

            remaining -= chunkHeight;
            cursorY += chunkHeight;
        }

        cursorY = beforeMiddle + middleFragmentHeight;

        Add(pieces, ref frameArea, chrome.LeftBottom,
            new Point(originX + chrome.LeftWidth - chrome.LeftBottom.Width, cursorY));
        Add(pieces, ref frameArea, chrome.RightBottom, new Point(originX + chrome.LeftWidth, cursorY));

        return new FrameBox(area, frameArea, new Point(originX, originY), middleFragmentCount,
            middleFragmentHeight, [.. pieces]);
    }

    /// <summary>Draws the frame, column by column, exactly in the order of the reference.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (Piece piece in _pieces)
        {
            // BlitSprite() offsets the destination by the origin of the source rectangle, so the
            // sprite local rectangle is subtracted to land the slice where the layout put it.
            ImageOps.BlitSprite(
                target,
                new Point(piece.Destination.X - piece.Source.X, piece.Destination.Y - piece.Source.Y),
                piece.Sprite,
                piece.Source);
        }
    }

    private static void Add(List<Piece> pieces, ref Rect frameArea, Sprite sprite, Point destination) =>
        Add(pieces, ref frameArea, sprite, sprite.Image.Bounds, destination);

    private static void Add(
        List<Piece> pieces, ref Rect frameArea, Sprite sprite, in Rect source, Point destination)
    {
        pieces.Add(new Piece(sprite, source, destination));

        Rect bounds = new(destination, source.Size);
        frameArea = frameArea.IsEmpty ? bounds : Rect.Union(frameArea, bounds);
    }

    /// <summary>One blit of the composition: a sprite local rectangle and the screen pixel it lands on.</summary>
    private readonly record struct Piece(Sprite Sprite, Rect Source, Point Destination);

    /// <summary>
    /// The six pieces of the frame and the widths the layout is computed from, i.e. the
    /// <c>topHeight()</c>, <c>bottomHeight()</c>, <c>leftWidth()</c> and <c>rightWidth()</c> helpers of
    /// <c>dialog_box.cpp</c>.
    /// </summary>
    private sealed class Columns
    {
        private Columns(Sprite rightTop, Sprite rightMiddle, Sprite rightBottom, Sprite leftTop,
            Sprite leftMiddle, Sprite leftBottom)
        {
            RightTop = rightTop;
            RightMiddle = rightMiddle;
            RightBottom = rightBottom;
            LeftTop = leftTop;
            LeftMiddle = leftMiddle;
            LeftBottom = leftBottom;
        }

        public Sprite RightTop { get; }

        public Sprite RightMiddle { get; }

        public Sprite RightBottom { get; }

        public Sprite LeftTop { get; }

        public Sprite LeftMiddle { get; }

        public Sprite LeftBottom { get; }

        /// <summary>Width of the left column, <c>leftWidth()</c>: the widest of its three pieces.</summary>
        public int LeftWidth => Math.Max(LeftTop.Width, Math.Max(LeftMiddle.Width, LeftBottom.Width));

        /// <summary>Width of the right column, <c>rightWidth()</c>.</summary>
        public int RightWidth => Math.Max(RightTop.Width, Math.Max(RightMiddle.Width, RightBottom.Width));

        /// <summary>Height of the top row of pieces, the <c>topHeight()</c> of the reference.</summary>
        public int TopHeight => Math.Max(LeftTop.Height, RightTop.Height);

        /// <summary>Height of the bottom row of pieces, the <c>bottomHeight()</c> of the reference.</summary>
        public int BottomHeight => Math.Max(LeftBottom.Height, RightBottom.Height);

        /// <summary>Reads the six pieces the way <c>redraw()</c> does: frames 0-2 and 4-6 of the table.</summary>
        /// <exception cref="InvalidOperationException">The archive has no usable frame artwork.</exception>
        public static Columns Load()
        {
            string fileName = ArtworkFileName;

            Sprite LoadFrame(int frame) => GameAssets.GetImage(fileName, frame) ?? throw new InvalidOperationException(
                $"The message frame {fileName} frame {frame} is not available: open the game data before " +
                "creating a frame box.");

            return new Columns(
                LoadFrame(RightFrame),
                LoadFrame(RightFrame + 1),
                LoadFrame(RightFrame + 2),
                LoadFrame(LeftFrame),
                LoadFrame(LeftFrame + 1),
                LoadFrame(LeftFrame + 2));
        }
    }
}
