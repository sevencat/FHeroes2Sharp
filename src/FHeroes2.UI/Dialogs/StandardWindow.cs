using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The bordered stone window every Heroes II dialog is drawn in, a port of
/// <c>fheroes2::StandardWindow</c> (<c>src/fheroes2/gui/ui_window.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// A standard window is not a nine slice frame: the four corners are 22 pixel squares cut out of
/// <see cref="StandardWindowChrome.Vertical"/>, another 21 pixels of the same artwork continue each
/// corner along the border (<see cref="BorderEdgeOffset"/> is the point where the repeatable part of
/// the border starts), and the interior is filled by tiling
/// <see cref="StandardWindowChrome.Background"/>. The seams between the corner pieces, the border and
/// the background are broken up by <see cref="ImageOps.CreateDitheringTransition"/>, and the whole
/// window is dropped on an <see cref="ImageOps.AddGradientShadowForArea"/> shadow that hangs 16
/// pixels off the left and the bottom side - that asymmetry is why <see cref="WindowArea"/> and
/// <see cref="TotalArea"/> differ on the left only.
/// </para>
/// <para>
/// The reference renders in its constructor and restores the covered pixels when the object dies.
/// The port does not need a restorer: scenes redraw the whole frame every time, so a window is just
/// a layout plus a <see cref="Draw"/> call.
/// </para>
/// <para>
/// Only the good interface artwork is supported, because the port has no interface type setting yet;
/// the evil variants (<c>SURDRBKE</c>, <c>WINLOSEE</c>, <c>STONEBAK_EVIL</c>) plug into the same
/// <see cref="StandardWindowChrome"/> once it exists.
/// </para>
/// </remarks>
public sealed class StandardWindow
{
    /// <summary>Width of the border and of the shadow, <c>borderSize</c> of the reference.</summary>
    public const int BorderWidth = 16;

    /// <summary>
    /// Distance from the window edge to the first repeatable pixel of a border,
    /// <c>borderEdgeOffset</c> of the reference. Everything before it belongs to a corner.
    /// </summary>
    public const int BorderEdgeOffset = 43;

    /// <summary>Height of a dithered seam, <c>transitionSize</c> of the reference.</summary>
    public const int TransitionSize = 10;

    /// <summary>Distance from the window edge to the tiled interior, <c>backgroundOffset</c>.</summary>
    public const int BackgroundOffset = 22;

    /// <summary>Space between the columns of a symmetric button group, <c>buttonsHorizontalGap</c>.</summary>
    public const int ButtonsHorizontalGap = 37;

    /// <summary>Space between the rows of a symmetric button group, <c>buttonsVerticalGap</c>.</summary>
    public const int ButtonsVerticalGap = 10;

    private readonly StandardWindowChrome _chrome;
    private readonly bool _hasBackground;

    private StandardWindow(StandardWindowChrome chrome, Rect activeArea, Rect windowArea, Rect totalArea,
        bool hasBackground)
    {
        _chrome = chrome;
        _hasBackground = hasBackground;
        ActiveArea = activeArea;
        WindowArea = windowArea;
        TotalArea = totalArea;
    }

    /// <summary>Artwork the window is composed of.</summary>
    public StandardWindowChrome Chrome => _chrome;

    /// <summary>
    /// Whether the interior is filled with the stone texture. Without it the corner pieces grow by the
    /// background margin and the window is a plain frame (<c>renderBackground = false</c>).
    /// </summary>
    public bool HasBackground => _hasBackground;

    /// <summary>Area the caller draws controls into, <c>GetArea()</c> of the reference.</summary>
    public Rect ActiveArea { get; }

    /// <summary>The frame itself: <see cref="BorderWidth"/> pixels around <see cref="ActiveArea"/>.</summary>
    public Rect WindowArea { get; }

    /// <summary><see cref="WindowArea"/> plus the shadow strip, i.e. the pixels the window overwrites.</summary>
    public Rect TotalArea { get; }

    /// <summary>
    /// Creates a window around an explicit area, the
    /// <c>StandardWindow( x, y, width, height, renderBackground, output )</c> constructor.
    /// </summary>
    /// <param name="chrome">Border and background artwork.</param>
    /// <param name="activeArea">Area the caller wants to draw into.</param>
    /// <param name="renderBackground">Whether the stone interior is tiled.</param>
    /// <returns>The laid out window; <see cref="Draw"/> paints it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chrome"/> is <see langword="null"/>.</exception>
    public static StandardWindow Create(StandardWindowChrome chrome, Rect activeArea, bool renderBackground = true)
    {
        ArgumentNullException.ThrowIfNull(chrome);

        Rect windowArea = new(
            activeArea.X - BorderWidth,
            activeArea.Y - BorderWidth,
            activeArea.Width + (BorderWidth * 2),
            activeArea.Height + (BorderWidth * 2));

        Rect totalArea = new(
            windowArea.X - BorderWidth,
            windowArea.Y,
            windowArea.Width + BorderWidth,
            windowArea.Height + BorderWidth);

        return new StandardWindow(chrome, activeArea, windowArea, totalArea, renderBackground);
    }

    /// <summary>
    /// Creates a window of the given size in the middle of the screen, the
    /// <c>StandardWindow( width, height, renderBackground, output )</c> constructor. Like the reference,
    /// an odd difference between the screen and the window size leaves the extra pixel on the right and
    /// the bottom side.
    /// </summary>
    /// <param name="chrome">Border and background artwork.</param>
    /// <param name="size">Size of the area the caller wants to draw into.</param>
    /// <param name="renderBackground">Whether the stone interior is tiled.</param>
    /// <param name="screen">Screen to center on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The laid out window; <see cref="Draw"/> paints it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chrome"/> is <see langword="null"/>.</exception>
    public static StandardWindow CreateCentered(StandardWindowChrome chrome, Size size, bool renderBackground = true,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(chrome);

        Size bounds = screen ?? ScreenBuffer.DefaultSize;
        Rect activeArea = new(
            (bounds.Width - size.Width) / 2,
            (bounds.Height - size.Height) / 2,
            size.Width,
            size.Height);

        return Create(chrome, activeArea, renderBackground);
    }

    /// <summary>
    /// Computes the active area of a dialog whose size is driven by a symmetric button group, the
    /// <c>getSymmetricDialogActiveArea()</c> of <c>ui_window.cpp:50-102</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An even group of at most six buttons is laid out in two columns (three rows for more than six
    /// buttons), an odd group of at most six in a single column, and the dialog grows to fit it: the
    /// padding around the buttons differs between the two layouts, and the two column layout also
    /// reserves the room a cancel button needs under the group.
    /// </para>
    /// <para>
    /// The reference places a single column at the right hand side of the main menu background instead
    /// of centring it. The port has no main menu yet, so a single column is centred like any other
    /// dialog; only the placement differs, the size is the one of the reference.
    /// </para>
    /// </remarks>
    /// <param name="buttons">Group whose first button gives the size of every button.</param>
    /// <param name="isSingleColumn">Whether the group is arranged in one column, <c>isSingleColumn</c>.</param>
    /// <param name="extraHeight">Room for a header or another element above the buttons, <c>extraHeight</c>.</param>
    /// <param name="screen">Screen to centre on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The area the dialog content goes into.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buttons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The group has no buttons.</exception>
    public static Rect ActiveAreaForSymmetricButtons(ButtonGroup buttons, bool isSingleColumn, int extraHeight,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        if (buttons.Count == 0)
        {
            throw new ArgumentException("A dialog cannot be sized around an empty button group.", nameof(buttons));
        }

        Rect buttonArea = buttons[0].Area;
        int buttonCount = buttons.Count;

        int widthPadding = isSingleColumn ? 52 : 60;
        int dialogWidth = widthPadding;

        int heightPadding = isSingleColumn ? 43 : 26;

        // The reference assumes the height of the cancel button under a multi column group as 25 pixels
        // plus both gaps and the one pixel the border takes.
        int cancelButtonAreaHeight = isSingleColumn ? 0 : 25 + ButtonsVerticalGap + 10 + 1;
        int dialogHeight = cancelButtonAreaHeight + heightPadding + extraHeight;

        if (isSingleColumn || (buttonCount % 2 != 0 && buttonCount <= 6))
        {
            dialogWidth += buttonArea.Width;
            dialogHeight += (buttonArea.Height * buttonCount) + ((buttonCount - 1) * ButtonsVerticalGap);
        }
        else if (buttonCount == 2)
        {
            dialogWidth += (buttonArea.Width * 2) + ButtonsHorizontalGap;
            dialogHeight += buttonArea.Height;
        }
        else if (buttonCount > 6)
        {
            int buttonGaps = 2 * ButtonsVerticalGap;
            dialogWidth += (3 * buttonArea.Width) + ((((buttonCount + 2) / 3) - 1) * buttonGaps);
            dialogHeight += (buttonArea.Height * 3) + (2 * buttonGaps);
        }
        else
        {
            int buttonGaps = 2 * ButtonsVerticalGap;
            dialogWidth += ((buttonCount / 2) * buttonArea.Width) + (((buttonCount / 2) - 1) * buttonGaps);
            dialogHeight += (buttonArea.Height * 2) + buttonGaps;
        }

        Size bounds = screen ?? ScreenBuffer.DefaultSize;

        return new Rect((bounds.Width - dialogWidth) / 2, (bounds.Height - dialogHeight) / 2, dialogWidth,
            dialogHeight);
    }

    /// <summary>
    /// Creates a dialog sized around a symmetric button group, the
    /// <c>StandardWindow( ButtonGroup &amp;, isSingleColumn, extraHeight, output )</c> constructor
    /// (<c>ui_window.cpp:129-143</c>).
    /// </summary>
    /// <param name="chrome">Border and background artwork.</param>
    /// <param name="buttons">Group the dialog is sized around; <see cref="PlaceSymmetricButtons"/> lays it out.</param>
    /// <param name="isSingleColumn">Whether the group is arranged in one column.</param>
    /// <param name="extraHeight">Room for a header or another element above the buttons.</param>
    /// <param name="screen">Screen to centre on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The laid out window, with the stone interior like the reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chrome"/> or <paramref name="buttons"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The group has no buttons.</exception>
    public static StandardWindow CreateForSymmetricButtons(StandardWindowChrome chrome, ButtonGroup buttons,
        bool isSingleColumn = false, int extraHeight = 0, Size? screen = null) =>
        Create(chrome, ActiveAreaForSymmetricButtons(buttons, isSingleColumn, extraHeight, screen));

    /// <summary>
    /// Places the buttons of a symmetric group inside <see cref="ActiveArea"/>, the
    /// <c>renderSymmetricButtons()</c> of <c>ui_window.cpp:476-538</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The group is read as a grid: one column for an odd count of at most six, two columns for an even
    /// count, three columns for more than six, exactly like <see cref="ActiveAreaForSymmetricButtons"/>
    /// sized it. Two column layouts use the doubled vertical gap so the space between the buttons is the
    /// same in both directions, and a last row that is not full is centred.
    /// </para>
    /// <para>
    /// The reference also draws the buttons; the port only positions them, because the scene draws every
    /// frame from scratch. <paramref name="offsetY"/> moves the whole group down, which is how the
    /// reference leaves room for a header above it.
    /// </para>
    /// </remarks>
    /// <param name="buttons">Group to place.</param>
    /// <param name="offsetY">Vertical offset of the whole group, <c>offsetY</c> of the reference.</param>
    /// <param name="isSingleColumn">Whether the group is arranged in one column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buttons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The group has no buttons.</exception>
    public void PlaceSymmetricButtons(ButtonGroup buttons, int offsetY = 0, bool isSingleColumn = false)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        if (buttons.Count == 0)
        {
            throw new ArgumentException("A dialog cannot be sized around an empty button group.", nameof(buttons));
        }

        int buttonWidth = buttons[0].Area.Width;
        int buttonHeight = buttons[0].Area.Height;
        int buttonCount = buttons.Count;

        int rows;
        int columns;
        Point buttonsOffset;
        int horizontalGap = ButtonsHorizontalGap;

        if (isSingleColumn || (buttonCount % 2 != 0 && buttonCount <= 6))
        {
            rows = buttonCount;
            columns = 1;
            buttonsOffset = new Point(25, 22);
        }
        else if (buttonCount == 2)
        {
            rows = 1;
            columns = 2;
            buttonsOffset = new Point(30, 15);
        }
        else if (buttonCount > 6)
        {
            columns = 3;
            rows = (buttonCount + 2) / 3;
            buttonsOffset = new Point(30, 15);
            horizontalGap = 2 * ButtonsVerticalGap;
        }
        else
        {
            rows = 2;
            columns = buttonCount / 2;
            buttonsOffset = new Point(30, 15);
            horizontalGap = 2 * ButtonsVerticalGap;
        }

        // The extra height always goes above the buttons.
        int groupOffsetY = buttonsOffset.Y + offsetY;

        int verticalGap = isSingleColumn ? ButtonsVerticalGap : 2 * ButtonsVerticalGap;
        int lastRowHorizontalOffset = 0;

        if (columns > 1 && rows > 1)
        {
            int missingInLastRow = (columns * rows) - buttonCount;
            lastRowHorizontalOffset = missingInLastRow * (buttonWidth + horizontalGap) / 2;
        }

        for (int index = 0; index < buttonCount; ++index)
        {
            int row = index / columns;
            int column = index % columns;
            int rowOffsetX = row == rows - 1 ? lastRowHorizontalOffset : 0;

            buttons[index].SetPosition(
                rowOffsetX + ActiveArea.X + (column * buttonWidth) + buttonsOffset.X + (column * horizontalGap),
                ActiveArea.Y + (row * (buttonHeight + verticalGap)) + groupOffsetY);
        }
    }

    /// <summary>
    /// Returns where an item of the given size has to go inside <see cref="ActiveArea"/>, the
    /// <c>_getRenderPos()</c> of <c>ui_window.cpp:540-591</c>.
    /// </summary>
    /// <param name="offset">Distance from the edge or the centre, <c>offset</c> of the reference.</param>
    /// <param name="itemSize">Size of the item that is placed.</param>
    /// <param name="padding">Which edge or corner to align to.</param>
    /// <returns>The top left corner of the item.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="padding"/> is not a known value.</exception>
    public Point RenderPosition(Point offset, Size itemSize, WindowPadding padding)
    {
        int x = ActiveArea.X;
        int y = ActiveArea.Y;

        switch (padding)
        {
            case WindowPadding.TopLeft:
            case WindowPadding.CenterLeft:
            case WindowPadding.BottomLeft:
                x += offset.X;
                break;

            case WindowPadding.TopCenter:
            case WindowPadding.CenterCenter:
            case WindowPadding.BottomCenter:
                x += ((ActiveArea.Width - itemSize.Width) / 2) + offset.X;
                break;

            case WindowPadding.TopRight:
            case WindowPadding.CenterRight:
            case WindowPadding.BottomRight:
                x += ActiveArea.Width - itemSize.Width - offset.X;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(padding), padding, "Unknown window padding.");
        }

        switch (padding)
        {
            case WindowPadding.TopLeft:
            case WindowPadding.TopCenter:
            case WindowPadding.TopRight:
                y += offset.Y;
                break;

            case WindowPadding.CenterLeft:
            case WindowPadding.CenterCenter:
            case WindowPadding.CenterRight:
                y += ((ActiveArea.Height - itemSize.Height) / 2) + offset.Y;
                break;

            case WindowPadding.BottomLeft:
            case WindowPadding.BottomCenter:
            case WindowPadding.BottomRight:
                y += ActiveArea.Height - itemSize.Height - offset.Y;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(padding), padding, "Unknown window padding.");
        }

        return new Point(x, y);
    }

    /// the tiled interior, the four borders with their dithered seams and, last of all, the shadow.
    /// </summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Sprite horizontalSprite = _chrome.Horizontal;
        Sprite verticalSprite = _chrome.Vertical;

        // Offset from the window edges to the copied background, and the size of the corners.
        int cornerSize = _hasBackground ? BackgroundOffset : BorderWidth;

        int horizontalSpriteWidth = horizontalSprite.Width - BorderWidth;
        int horizontalSpriteHeight = horizontalSprite.Height - BorderWidth;
        int verticalSpriteHeight = verticalSprite.Height;
        int verticalSpriteWidth = verticalSprite.Width;

        int windowX = WindowArea.X;
        int windowY = WindowArea.Y;
        int windowWidth = WindowArea.Width;
        int windowHeight = WindowArea.Height;

        // Render the corners. They are the same in the artwork, so only the vertical sprite is used.
        int rightCornerOffsetX = windowX + windowWidth - cornerSize;
        int bottomCornerOffsetY = windowY + windowHeight - cornerSize;
        int rightCornerSpriteOffsetX = verticalSpriteWidth - cornerSize;
        int bottomCornerSpriteOffsetY = verticalSpriteHeight - cornerSize;

        Blit(target, verticalSprite, 0, 0, windowX, windowY, cornerSize, cornerSize);
        Blit(target, verticalSprite, rightCornerSpriteOffsetX, 0, rightCornerOffsetX, windowY, cornerSize,
            cornerSize);
        Blit(target, verticalSprite, 0, bottomCornerSpriteOffsetY, windowX, bottomCornerOffsetY, cornerSize,
            cornerSize);
        Blit(target, verticalSprite, rightCornerSpriteOffsetX, bottomCornerSpriteOffsetY, rightCornerOffsetX,
            bottomCornerOffsetY, cornerSize, cornerSize);

        // Render the part of the border that continues a corner. It is not repeated to fill the length.
        int extraCornerSize = BorderEdgeOffset - cornerSize;
        Point cornerOffset = new(windowX + cornerSize, windowY + cornerSize);
        int rightBorderEdgeOffset = verticalSpriteWidth - BorderEdgeOffset;
        int bottomBorderEdgeOffset = verticalSpriteHeight - BorderEdgeOffset;

        Blit(target, verticalSprite, cornerSize, 0, cornerOffset.X, windowY, extraCornerSize, cornerSize);
        Blit(target, verticalSprite, 0, cornerSize, windowX, cornerOffset.Y, cornerSize, extraCornerSize);
        Blit(target, verticalSprite, rightBorderEdgeOffset, 0, rightCornerOffsetX - extraCornerSize, windowY,
            extraCornerSize, cornerSize);
        Blit(target, verticalSprite, rightCornerSpriteOffsetX, cornerSize, rightCornerOffsetX, cornerOffset.Y,
            cornerSize, extraCornerSize);
        Blit(target, verticalSprite, cornerSize, bottomCornerSpriteOffsetY, cornerOffset.X, bottomCornerOffsetY,
            extraCornerSize, cornerSize);
        Blit(target, verticalSprite, 0, bottomBorderEdgeOffset, windowX, bottomCornerOffsetY - extraCornerSize,
            cornerSize, extraCornerSize);
        Blit(target, verticalSprite, rightBorderEdgeOffset, bottomCornerSpriteOffsetY,
            rightCornerOffsetX - extraCornerSize, bottomCornerOffsetY, extraCornerSize, cornerSize);
        Blit(target, verticalSprite, rightCornerSpriteOffsetX, bottomBorderEdgeOffset, rightCornerOffsetX,
            bottomCornerOffsetY - extraCornerSize, cornerSize, extraCornerSize);

        if (_hasBackground)
        {
            RenderBackgroundImage(target, WindowArea, BackgroundOffset);

            // Dithered seams from the borders to the background in the corners.
            Transition(target, verticalSprite, cornerSize, cornerSize, cornerOffset.X, cornerOffset.Y,
                extraCornerSize, TransitionSize, false, true);
            Transition(target, verticalSprite, cornerSize, cornerSize, cornerOffset.X, cornerOffset.Y,
                TransitionSize, extraCornerSize, true, true);

            Transition(target, verticalSprite, rightBorderEdgeOffset, cornerSize,
                rightCornerOffsetX - extraCornerSize, cornerOffset.Y, extraCornerSize, TransitionSize, false, true);
            Transition(target, verticalSprite, rightCornerSpriteOffsetX - TransitionSize, cornerSize,
                rightCornerOffsetX - TransitionSize, cornerOffset.Y, TransitionSize, extraCornerSize, true, false);

            Transition(target, verticalSprite, cornerSize, bottomCornerSpriteOffsetY - TransitionSize,
                cornerOffset.X, bottomCornerOffsetY - TransitionSize, extraCornerSize, TransitionSize, false,
                false);
            Transition(target, verticalSprite, cornerSize, bottomBorderEdgeOffset, cornerOffset.X,
                bottomCornerOffsetY - extraCornerSize, TransitionSize, extraCornerSize, true, true);

            Transition(target, verticalSprite, rightBorderEdgeOffset,
                bottomCornerSpriteOffsetY - TransitionSize, rightCornerOffsetX - extraCornerSize,
                bottomCornerOffsetY - TransitionSize, extraCornerSize, TransitionSize, false, false);
            Transition(target, verticalSprite, rightCornerSpriteOffsetX - TransitionSize, bottomBorderEdgeOffset,
                rightCornerOffsetX - TransitionSize, bottomCornerOffsetY - extraCornerSize, TransitionSize,
                extraCornerSize, true, false);
        }

        // Render the vertical borders.
        int doubleBorderEdgeOffset = BorderEdgeOffset * 2;
        int verticalSpriteCopyHeight = Math.Min(windowHeight, verticalSpriteHeight) - doubleBorderEdgeOffset;
        int verticalSpriteCopies = (windowHeight - doubleBorderEdgeOffset - 1 - TransitionSize) /
            (bottomBorderEdgeOffset - BorderEdgeOffset - TransitionSize);
        int topBorderEdgeOffset = windowY + BorderEdgeOffset;

        Blit(target, verticalSprite, 0, BorderEdgeOffset, windowX, topBorderEdgeOffset, cornerSize,
            verticalSpriteCopyHeight);
        Blit(target, verticalSprite, rightCornerSpriteOffsetX, BorderEdgeOffset, rightCornerOffsetX,
            topBorderEdgeOffset, cornerSize, verticalSpriteCopyHeight);

        // Dithered seams to the background.
        if (_hasBackground)
        {
            Transition(target, verticalSprite, cornerSize, BorderEdgeOffset, cornerOffset.X, topBorderEdgeOffset,
                TransitionSize, verticalSpriteCopyHeight, true, true);
            Transition(target, verticalSprite, rightCornerSpriteOffsetX - TransitionSize, BorderEdgeOffset,
                rightCornerOffsetX - TransitionSize, topBorderEdgeOffset, TransitionSize, verticalSpriteCopyHeight,
                true, false);
        }

        // If the border is too short to reach the bottom, repeat its central part behind a dithered seam.
        if (verticalSpriteCopies > 0)
        {
            int toOffsetY = BorderEdgeOffset + verticalSpriteCopyHeight;
            int outputY = windowY + toOffsetY - TransitionSize;

            Transition(target, verticalSprite, 0, BorderEdgeOffset, windowX, outputY, cornerSize, TransitionSize,
                false, false);
            Transition(target, verticalSprite, rightCornerSpriteOffsetX, BorderEdgeOffset, rightCornerOffsetX, outputY,
                cornerSize, TransitionSize, false, false);

            int stepY = verticalSpriteCopyHeight - TransitionSize;
            int fromOffsetY = BorderEdgeOffset + TransitionSize;

            for (int i = 0; i < verticalSpriteCopies; ++i)
            {
                int copyHeight = Math.Min(verticalSpriteCopyHeight, windowHeight - BorderEdgeOffset - toOffsetY);
                int toY = windowY + toOffsetY;

                Blit(target, verticalSprite, 0, fromOffsetY, windowX, toY, cornerSize, copyHeight);
                Blit(target, verticalSprite, rightCornerSpriteOffsetX, fromOffsetY, rightCornerOffsetX, toY,
                    cornerSize, copyHeight);

                if (_hasBackground)
                {
                    Transition(target, verticalSprite, cornerSize, fromOffsetY, cornerOffset.X, toY, TransitionSize,
                        copyHeight, true, true);
                    Transition(target, verticalSprite, rightCornerSpriteOffsetX - TransitionSize, fromOffsetY,
                        rightCornerOffsetX - TransitionSize, toY, TransitionSize, copyHeight, true, false);
                }

                toOffsetY += stepY;
            }
        }

        // Dithered transitions to the bottom corners.
        int verticalSpriteBottomCornerEdgeY = bottomBorderEdgeOffset - TransitionSize;
        int outputBottomCornerEdgeY = windowY + windowHeight - BorderEdgeOffset - TransitionSize;

        Transition(target, verticalSprite, 0, verticalSpriteBottomCornerEdgeY, windowX, outputBottomCornerEdgeY,
            cornerSize, TransitionSize, false, false);
        Transition(target, verticalSprite, rightCornerSpriteOffsetX, verticalSpriteBottomCornerEdgeY,
            rightCornerOffsetX, outputBottomCornerEdgeY, cornerSize, TransitionSize, false, false);

        // Render the horizontal borders. 'SURDRBKG.ICN' carries 16 pixels of shadow on its left and bottom
        // side, so the copied part starts at 'BorderEdgeOffset + BorderWidth' and not at the sprite origin.
        int horizontalSpriteCopyWidth = Math.Min(windowWidth, horizontalSpriteWidth) - doubleBorderEdgeOffset;
        int horizontalSpriteCopies = (windowWidth - doubleBorderEdgeOffset - 1 - TransitionSize) /
            (horizontalSpriteWidth - doubleBorderEdgeOffset - TransitionSize);
        int bottomBorderSpriteOffsetY = horizontalSpriteHeight - cornerSize;
        int horizontalSpriteCopyStartX = BorderEdgeOffset + BorderWidth;
        int leftBorderEdgeOffset = windowX + BorderEdgeOffset;

        Blit(target, horizontalSprite, horizontalSpriteCopyStartX, 0, leftBorderEdgeOffset, windowY,
            horizontalSpriteCopyWidth, cornerSize);
        Blit(target, horizontalSprite, horizontalSpriteCopyStartX, bottomBorderSpriteOffsetY, leftBorderEdgeOffset,
            bottomCornerOffsetY, horizontalSpriteCopyWidth, cornerSize);

        // Dithered seams to the background.
        if (_hasBackground)
        {
            Transition(target, horizontalSprite, horizontalSpriteCopyStartX, cornerSize, leftBorderEdgeOffset,
                cornerOffset.Y, horizontalSpriteCopyWidth, TransitionSize, false, true);
            Transition(target, horizontalSprite, horizontalSpriteCopyStartX,
                bottomBorderSpriteOffsetY - TransitionSize, leftBorderEdgeOffset,
                bottomCornerOffsetY - TransitionSize, horizontalSpriteCopyWidth, TransitionSize, false, false);
        }

        // If the border is too short to reach the right corner, repeat its central part behind a seam.
        if (horizontalSpriteCopies > 0)
        {
            int toOffsetX = BorderEdgeOffset + horizontalSpriteCopyWidth;
            int outputX = windowX + toOffsetX - TransitionSize;

            Transition(target, horizontalSprite, horizontalSpriteCopyStartX, 0, outputX, windowY, TransitionSize,
                cornerSize, true, false);
            Transition(target, horizontalSprite, horizontalSpriteCopyStartX, bottomBorderSpriteOffsetY, outputX,
                bottomCornerOffsetY, TransitionSize, cornerSize, true, false);

            int stepX = horizontalSpriteCopyWidth - TransitionSize;
            int fromOffsetX = horizontalSpriteCopyStartX + TransitionSize;

            for (int i = 0; i < horizontalSpriteCopies; ++i)
            {
                int copyWidth = Math.Min(horizontalSpriteCopyWidth, windowWidth - BorderEdgeOffset - toOffsetX);
                int toX = windowX + toOffsetX;

                Blit(target, horizontalSprite, fromOffsetX, 0, toX, windowY, copyWidth, cornerSize);
                Blit(target, horizontalSprite, fromOffsetX, bottomBorderSpriteOffsetY, toX, bottomCornerOffsetY,
                    copyWidth, cornerSize);

                if (_hasBackground)
                {
                    Transition(target, horizontalSprite, fromOffsetX, cornerSize, toX, cornerOffset.Y, copyWidth,
                        TransitionSize, false, true);
                    Transition(target, horizontalSprite, fromOffsetX, bottomBorderSpriteOffsetY - TransitionSize, toX,
                        bottomCornerOffsetY - TransitionSize, copyWidth, TransitionSize, false, false);
                }

                toOffsetX += stepX;
            }
        }

        // Dithered transitions to the right corners.
        int horizontalSpriteRightCornerEdgeX = horizontalSprite.Width - BorderEdgeOffset - TransitionSize;
        int outputRightCornerEdgeX = windowX + windowWidth - BorderEdgeOffset - TransitionSize;

        Transition(target, horizontalSprite, horizontalSpriteRightCornerEdgeX, 0, outputRightCornerEdgeX, windowY,
            TransitionSize, cornerSize, true, false);
        Transition(target, horizontalSprite, horizontalSpriteRightCornerEdgeX, bottomBorderSpriteOffsetY,
            outputRightCornerEdgeX, bottomCornerOffsetY, TransitionSize, cornerSize, true, false);

        ImageOps.AddGradientShadowForArea(target, new Point(windowX, windowY), windowWidth, windowHeight,
            BorderWidth);
    }

    /// <summary>
    /// Draws the four small gems into the frame of the window, the
    /// <c>StandardWindow::applyGemDecoratedCorners()</c> of <c>ui_window.cpp:366-398</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gem is a 9x9 square cut out of <c>REDBACK.ICN</c> at (20, 2) and blitted four times along the
    /// frame, 4 pixels from the left and 2 from the top, mirrored vertically at the bottom edge. The
    /// original switches to <c>EVIL_DIALOG_PLAIN_CORNERS</c> and <c>WINLOSEE.ICN</c> when the evil
    /// interface is enabled; the port has no interface type setting (see <see cref="StandardWindowChrome"/>),
    /// so only the good look is drawn.
    /// </para>
    /// <para>
    /// Called after <see cref="Draw"/>, because the gems sit on top of the frame the window just painted.
    /// </para>
    /// </remarks>
    /// <param name="target">Indexed frame buffer the window was drawn into.</param>
    /// <param name="assets">Game data the gem artwork is read from.</param>
    /// <returns><see langword="false"/> when the archive has no usable <c>REDBACK.ICN</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="target"/> or <paramref name="assets"/> is <see langword="null"/>.
    /// </exception>
    public bool TryApplyGemDecoratedCorners(PixelBuffer target, AssetBundle assets)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(assets);

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Redback)) is not { Count: > 0 } file)
        {
            return false;
        }

        const int GemSideLength = 9;
        const int GemSourceX = 20;
        const int GemSourceY = 2;

        Sprite gem = file.Sprites[0];

        if (gem.Width < GemSourceX + GemSideLength || gem.Height < GemSourceY + GemSideLength)
        {
            return false;
        }

        Rect area = WindowArea;
        int leftX = area.X + 4;
        int rightX = area.X + area.Width - 2 - GemSideLength;
        int topY = area.Y + 2;
        int bottomY = area.Y + area.Height - GemSideLength - 4;

        Blit(target, gem, GemSourceX, GemSourceY, leftX, topY, GemSideLength, GemSideLength);
        Blit(target, gem, GemSourceX, GemSourceY, rightX, topY, GemSideLength, GemSideLength);
        Blit(target, gem, GemSourceX, GemSourceY, leftX, bottomY, GemSideLength, GemSideLength);
        Blit(target, gem, GemSourceX, GemSourceY, rightX, bottomY, GemSideLength, GemSideLength);

        return true;
    }

    /// <summary>
    /// Tiles <see cref="StandardWindowChrome.Background"/> into the interior, the
    /// <c>StandardWindow::renderBackgroundImage()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// The texture is 640x480 and therefore larger than any dialog of the game, so the two repeat loops
    /// only run for windows that are bigger than the artwork. They are ported anyway, because a full
    /// screen window (the adventure map dialog) needs them.
    /// </remarks>
    private void RenderBackgroundImage(PixelBuffer target, Rect area, int borderOffset)
    {
        Sprite backgroundSprite = _chrome.Background;
        int backgroundSpriteWidth = backgroundSprite.Width;
        int backgroundSpriteHeight = backgroundSprite.Height;

        int backgroundWidth = area.Width - (borderOffset * 2);
        int backgroundHeight = area.Height - (borderOffset * 2);
        int backgroundHorizontalCopies = (backgroundWidth - 1 - TransitionSize) /
            (backgroundSpriteWidth - TransitionSize);
        int backgroundVerticalCopies = (backgroundHeight - 1 - TransitionSize) /
            (backgroundSpriteHeight - TransitionSize);

        int backgroundCopyWidth = Math.Min(backgroundSpriteWidth, backgroundWidth);
        int backgroundCopyHeight = Math.Min(backgroundSpriteHeight, backgroundHeight);
        int backgroundOffsetX = area.X + borderOffset;
        int backgroundOffsetY = area.Y + borderOffset;

        // A plain copy is enough: the background has no transparent pixels.
        Blit(target, backgroundSprite, 0, 0, backgroundOffsetX, backgroundOffsetY, backgroundCopyWidth,
            backgroundCopyHeight);

        // If the texture is narrower than the interior, blend a seam and repeat what is already drawn.
        if (backgroundHorizontalCopies > 0)
        {
            int toOffsetX = borderOffset + backgroundSpriteWidth;

            Transition(target, backgroundSprite, 0, 0, area.X + toOffsetX - TransitionSize, backgroundOffsetY,
                TransitionSize, backgroundCopyHeight, true, false);

            int stepX = backgroundSpriteWidth - TransitionSize;
            int fromOffsetX = borderOffset + TransitionSize;

            for (int i = 0; i < backgroundHorizontalCopies; ++i)
            {
                CopyArea(target, area.X + fromOffsetX, backgroundOffsetY, area.X + toOffsetX, backgroundOffsetY,
                    Math.Min(backgroundSpriteWidth, area.Width - borderOffset - toOffsetX), backgroundCopyHeight);
                toOffsetX += stepX;
            }
        }

        // The same vertically, but in full width and with the screen itself as the transition source.
        if (backgroundVerticalCopies > 0)
        {
            int toOffsetY = borderOffset + backgroundSpriteHeight;

            ImageOps.CreateDitheringTransition(
                new Sprite(target),
                new Point(backgroundOffsetX, backgroundOffsetY),
                target,
                new Point(backgroundOffsetX, area.Y + toOffsetY - TransitionSize),
                new Size(backgroundWidth, TransitionSize),
                false,
                false);

            int stepY = backgroundSpriteHeight - TransitionSize;
            int fromOffsetY = borderOffset + TransitionSize;

            for (int i = 0; i < backgroundVerticalCopies; ++i)
            {
                CopyArea(target, backgroundOffsetX, area.Y + fromOffsetY, backgroundOffsetX, area.Y + toOffsetY,
                    backgroundWidth,
                    Math.Min(backgroundSpriteHeight, area.Height - borderOffset - toOffsetY));
                toOffsetY += stepY;
            }
        }
    }

    /// <summary>
    /// Copies a rectangle of <paramref name="target"/> onto itself, the in place <c>Copy()</c> the
    /// repeated background is built with. Rows are walked from the top so a copy that overlaps its
    /// source reads the pixels the reference reads.
    /// </summary>
    private static void CopyArea(PixelBuffer target, int sourceX, int sourceY, int destinationX,
        int destinationY, int width, int height) =>
        ImageOps.Blit(target, new Point(destinationX, destinationY), target, new Rect(sourceX, sourceY, width, height));

    /// <summary>
    /// Blits a sprite local rectangle, <c>Blit( in, inX, inY, out, outX, outY, width, height )</c>.
    /// </summary>
    private static void Blit(PixelBuffer target, Sprite sprite, int sourceX, int sourceY, int destinationX,
        int destinationY, int width, int height) =>
        // BlitSprite() adds the origin of the source rectangle to the destination, so it is subtracted to
        // land the piece exactly where the layout put it.
        ImageOps.BlitSprite(
            target,
            new Point(destinationX - sourceX, destinationY - sourceY),
            sprite,
            new Rect(sourceX, sourceY, width, height));

    /// <summary>
    /// Draws a dithered seam, <c>CreateDitheringTransition( in, inPos, out, outPos, size, ... )</c>.
    /// </summary>
    private static void Transition(PixelBuffer target, Sprite source, int sourceX, int sourceY, int destinationX,
        int destinationY, int width, int height, bool vertical, bool reverse) =>
        ImageOps.CreateDitheringTransition(
            source,
            new Point(sourceX, sourceY),
            target,
            new Point(destinationX, destinationY),
            new Size(width, height),
            vertical,
            reverse);
}

