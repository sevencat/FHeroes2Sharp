using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Runtime generation of the original button artwork: <c>resizeButton()</c>,
/// <c>addButtonShine()</c> and <c>makeTransparentBackground()</c> of
/// <c>src/fheroes2/gui/ui_button.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// A text button of Heroes II is not a nine slice: the empty button of the interface is stretched
/// by repeating one third of its width and one fifth of its height, and the corners are kept as
/// they are. That is why a stretched button keeps its bevelled corners and its shine, and why the
/// result is pixel identical to the original at every supported size.
/// </para>
/// <para>
/// The reference reaches into the asset cache for the empty button and the background texture; the
/// port takes the sprites as parameters, so the UI layer stays independent of the archive layout
/// and the whole decoration can be unit tested with synthetic sprites.
/// </para>
/// </remarks>
public static class ButtonArtwork
{
    /// <summary>
    /// Stretches (or shrinks) <paramref name="original"/> to <paramref name="size"/>,
    /// <c>fheroes2::resizeButton()</c>.
    /// </summary>
    /// <remarks>
    /// Five cases exist in the reference and all of them are reproduced here: wider only, taller
    /// only, smaller in both directions, taller but less wide, and bigger in both directions. The
    /// last one is the only one with a hole in the middle, which is filled with the colour of the
    /// central pixel of the source image.
    /// </remarks>
    /// <param name="original">Empty button sprite, usually frame 0 or 1 of an <c>EMPTY_*</c> ICN.</param>
    /// <param name="size">Size the button has to become.</param>
    /// <returns>
    /// The stretched button. The sprite is always a fresh copy, so decorating it never modifies
    /// <paramref name="original"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="size"/> has a non positive side, or is too small to keep the two end pieces
    /// of the stretched edge (the reference asserts in the same situation).
    /// </exception>
    public static Sprite Resize(Sprite original, Size size)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height, nameof(size));

        int originalWidth = original.Width;
        int originalHeight = original.Height;

        if (originalWidth == size.Width && originalHeight == size.Height)
        {
            return original.Clone();
        }

        Sprite output = new(size.Width, size.Height);
        output.Reset();

        int middleWidth = originalWidth / 3;
        int middleHeight = originalHeight / 5;

        if (size.Width > originalWidth && size.Height == originalHeight)
        {
            // Only wider: repeat the central third of the width as often as needed.
            EnsurePositive(middleWidth, nameof(original), "The button is too narrow to be stretched.");

            int overallMiddleWidth = size.Width - (middleWidth * 2);
            int middleWidthCount = overallMiddleWidth / middleWidth;
            int middleWidthLeftOver = overallMiddleWidth - (middleWidthCount * middleWidth);

            Copy(original, 0, 0, output, 0, 0, middleWidth, originalHeight);

            int offsetX = middleWidth;

            for (int i = 0; i < middleWidthCount; ++i)
            {
                Copy(original, middleWidth, 0, output, offsetX, 0, middleWidth, originalHeight);
                offsetX += middleWidth;
            }

            if (middleWidthLeftOver > 0)
            {
                Copy(original, middleWidth, 0, output, offsetX, 0, middleWidthLeftOver, originalHeight);
                offsetX += middleWidthLeftOver;
            }

            Copy(original, originalWidth - middleWidth, 0, output, offsetX, 0, middleWidth, originalHeight);

            return output;
        }

        if (size.Height > originalHeight && size.Width == originalWidth)
        {
            // Only taller: repeat the central fifth of the height as often as needed.
            EnsurePositive(middleHeight, nameof(original), "The button is too short to be stretched.");

            int overallMiddleHeight = size.Height - (middleHeight * 2);
            int middleHeightCount = overallMiddleHeight / middleHeight;
            int middleHeightLeftOver = overallMiddleHeight - (middleHeightCount * middleHeight);

            Copy(original, 0, 0, output, 0, 0, originalWidth, middleHeight);

            int offsetY = middleHeight;

            for (int i = 0; i < middleHeightCount; ++i)
            {
                Copy(original, 0, middleHeight, output, 0, offsetY, originalWidth, middleHeight);
                offsetY += middleHeight;
            }

            if (middleHeightLeftOver > 0)
            {
                Copy(original, 0, middleHeight, output, 0, offsetY, originalWidth, middleHeightLeftOver);
                offsetY += middleHeightLeftOver;
            }

            Copy(original, 0, originalHeight - middleHeight, output, 0, offsetY, originalWidth, middleHeight);

            return output;
        }

        return ResizeMixed(original, size, output, middleWidth, middleHeight);
    }

    private static Sprite ResizeMixed(Sprite original, Size size, Sprite output, int middleWidth, int middleHeight)
    {
        int originalWidth = original.Width;
        int originalHeight = original.Height;

        if (size.Height <= originalHeight && size.Width <= originalWidth)
        {
            // Smaller in both directions: keep the four corners, drop the middle.
            Copy(original, 0, 0, output, 0, 0, size.Width / 2, size.Height / 2);

            int secondHalfHeight = size.Height - (size.Height / 2);
            int secondHalfWidth = size.Width - (size.Width / 2);

            Copy(original, 0, originalHeight - secondHalfHeight, output, 0, size.Height - secondHalfHeight,
                secondHalfWidth, secondHalfHeight);
            Copy(original, originalWidth - secondHalfWidth, 0, output, size.Width - secondHalfWidth, 0,
                secondHalfWidth, secondHalfHeight);
            Copy(original, originalWidth - secondHalfWidth, originalHeight - secondHalfHeight, output,
                size.Width - secondHalfWidth, size.Height - secondHalfHeight, secondHalfWidth, secondHalfHeight);

            return output;
        }

        if (size.Height > originalHeight && size.Width < originalWidth)
        {
            // Taller but less wide: the ends are squeezed horizontally and the middle repeats vertically.
            EnsurePositive(middleHeight, nameof(original), "The button is too short to be stretched.");

            if (size.Width < middleWidth * 2)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "The button cannot even fit its two end pieces.");
            }

            int overallMiddleHeight = size.Height - (middleHeight * 2);
            int middleHeightCount = overallMiddleHeight / middleHeight;
            int middleHeightLeftOver = overallMiddleHeight - (middleHeightCount * middleHeight);
            int rightSideWidth = size.Width - middleWidth;

            Copy(original, 0, 0, output, 0, 0, middleWidth, middleHeight);
            Copy(original, originalWidth - rightSideWidth, 0, output, middleWidth, 0, rightSideWidth, middleHeight);

            int offsetY = middleHeight;

            for (int i = 0; i < middleHeightCount; ++i)
            {
                Copy(original, 0, middleHeight, output, 0, offsetY, middleWidth, middleHeight);
                Copy(original, originalWidth - rightSideWidth, middleHeight, output, middleWidth, offsetY,
                    rightSideWidth, middleHeight);
                offsetY += middleHeight;
            }

            if (middleHeightLeftOver > 0)
            {
                Copy(original, 0, middleHeight, output, 0, offsetY, middleWidth, middleHeightLeftOver);
                Copy(original, originalWidth - rightSideWidth, middleHeight, output, middleWidth, offsetY,
                    rightSideWidth, middleHeightLeftOver);
                offsetY += middleHeightLeftOver;
            }

            Copy(original, 0, originalHeight - middleHeight, output, 0, offsetY, middleWidth, middleHeight);
            Copy(original, originalWidth - rightSideWidth, originalHeight - middleHeight, output, middleWidth,
                offsetY, rightSideWidth, middleHeight);

            return output;
        }

        return ResizeLarger(original, size, output, middleWidth, middleHeight);
    }

    private static Sprite ResizeLarger(Sprite original, Size size, Sprite output, int middleWidth, int middleHeight)
    {
        // Bigger in both directions: write the four corners and the four edges, then fill the hole
        // with the colour of the central pixel of the source.
        EnsurePositive(middleWidth, nameof(original), "The button is too narrow to be stretched.");
        EnsurePositive(middleHeight, nameof(original), "The button is too short to be stretched.");

        int originalWidth = original.Width;
        int originalHeight = original.Height;

        int overallMiddleWidth = size.Width - (middleWidth * 2);
        int middleWidthCount = overallMiddleWidth / middleWidth;
        int middleWidthLeftOver = overallMiddleWidth - (middleWidthCount * middleWidth);
        int overallMiddleHeight = size.Height - (middleHeight * 2);
        int middleHeightCount = overallMiddleHeight / middleHeight;
        int middleHeightLeftOver = overallMiddleHeight - (middleHeightCount * middleHeight);

        Copy(original, 0, 0, output, 0, 0, middleWidth, middleHeight);

        int rightPartWidth = originalWidth - (middleWidth * 2);

        Copy(original, originalWidth - rightPartWidth, 0, output, size.Width - rightPartWidth, 0,
            middleWidth, middleHeight);

        int offsetY = middleHeight;

        for (int i = 0; i < middleHeightCount; ++i)
        {
            Copy(original, 0, middleHeight, output, 0, offsetY, middleWidth, middleHeight);
            Copy(original, originalWidth - rightPartWidth, middleHeight, output, size.Width - rightPartWidth,
                offsetY, middleWidth, middleHeight);
            offsetY += middleHeight;
        }

        if (middleHeightLeftOver > 0)
        {
            Copy(original, 0, middleHeight, output, 0, offsetY, middleWidth, middleHeightLeftOver);
            Copy(original, originalWidth - rightPartWidth, middleHeight, output, size.Width - rightPartWidth,
                offsetY, middleWidth, middleHeightLeftOver);
            offsetY += middleHeightLeftOver;
        }

        int bottomPartHeight = originalHeight - (middleHeight * 4);

        Copy(original, 0, originalHeight - bottomPartHeight, output, 0, offsetY, middleWidth, bottomPartHeight);
        Copy(original, originalWidth - rightPartWidth, originalHeight - bottomPartHeight, output,
            size.Width - rightPartWidth, offsetY, middleWidth, bottomPartHeight);

        int offsetX = middleWidth;

        for (int i = 0; i < middleWidthCount; ++i)
        {
            Copy(original, middleWidth, 0, output, offsetX, 0, middleWidth, middleHeight);
            Copy(original, middleWidth, originalHeight - bottomPartHeight, output, offsetX, offsetY,
                middleWidth, middleHeight);
            offsetX += middleWidth;
        }

        if (middleWidthLeftOver > 0)
        {
            Copy(original, middleWidth, 0, output, offsetX, 0, middleWidthLeftOver, middleHeight);
            Copy(original, middleWidth, originalHeight - bottomPartHeight, output, offsetX, offsetY,
                middleWidthLeftOver, middleHeight);
            offsetX += middleWidthLeftOver;
        }

        // The centre of the plate is a flat colour, so one pixel of it fills the hole.
        int centralPosition = (originalWidth * originalHeight / 2) - 1 - (Math.Min(originalWidth, originalHeight) / 2);
        ImageOps.FillSprite(
            output,
            new Rect(middleWidth, middleHeight, offsetX - middleWidth, offsetY - middleHeight),
            original.Image.Pixels[centralPosition]);

        return output;
    }

    /// <summary>
    /// Draws the highlight pixels onto a stretched button, <c>fheroes2::addButtonShine()</c>.
    /// </summary>
    /// <remarks>
    /// The coordinates are absolute, so the reference only supports the two main templates; the
    /// other four are left untouched (the original <c>if</c> does the same).
    /// </remarks>
    /// <param name="button">Stretched button sprite; it is modified in place.</param>
    /// <param name="template">One of <see cref="DialogButtonTemplate.Good"/> or <see cref="DialogButtonTemplate.Evil"/>.</param>
    public static void AddShine(Sprite button, DialogButtonTemplate template)
    {
        ArgumentNullException.ThrowIfNull(button);

        bool isGoodButton = template == DialogButtonTemplate.Good;
        if (!isGoodButton && template != DialogButtonTemplate.Evil)
        {
            return;
        }

        const byte firstColor = 10;
        byte secondColor = isGoodButton ? (byte)37 : (byte)15;
        byte lastColor = isGoodButton ? (byte)39 : (byte)16;

        // Left-side shine.
        ImageOps.SetPixel(button, 11, 4, firstColor);
        ImageOps.SetPixel(button, 13, 4, firstColor);
        ImageOps.SetPixel(button, 9, 6, firstColor);
        ImageOps.SetPixel(button, 10, 5, secondColor);
        ImageOps.SetPixel(button, 12, 5, secondColor);
        ImageOps.SetPixel(button, 8, 7, lastColor);
        ImageOps.SetPixel(button, 15, 4, lastColor);

        // Right-side shine.
        int buttonWidth = button.Width;
        ImageOps.SetPixel(button, buttonWidth - 9, 4, firstColor);
        ImageOps.SetPixel(button, buttonWidth - 7, 4, firstColor);
        ImageOps.DrawLine(button, new Point(buttonWidth - 10, 5), new Point(buttonWidth - 11, 6), secondColor);
        ImageOps.SetPixel(button, buttonWidth - 8, 5, secondColor);

        int buttonHeight = button.Height;

        // To avoid overcrowding the '...' virtual keyboard button with decorations the reference
        // stops at 50 pixels of width.
        if (buttonWidth > 50)
        {
            // Longer left-side shine.
            ImageOps.DrawLine(button, new Point(11, 4), new Point(9, 6), firstColor);
            ImageOps.SetPixel(button, 8, 7, lastColor);
            ImageOps.SetPixel(button, 13, 4, secondColor);
            ImageOps.DrawLine(button, new Point(12, 5), new Point(9, 8), lastColor);
            ImageOps.DrawLine(button, new Point(8, 9), new Point(7, 10), firstColor);
            ImageOps.SetPixel(button, 6, 11, lastColor);

            // Longer right-side shine.
            ImageOps.SetPixel(button, buttonWidth - 10, 2, firstColor);
            ImageOps.SetPixel(button, buttonWidth - 8, 3, secondColor);
            ImageOps.SetPixel(button, buttonWidth - 11, 3, secondColor);
            ImageOps.SetPixel(button, buttonWidth - 12, 4, firstColor);
            ImageOps.DrawLine(button, new Point(buttonWidth - 13, 5), new Point(buttonWidth - 14, 6), lastColor);
            ImageOps.DrawLine(button, new Point(buttonWidth - 10, 5), new Point(buttonWidth - 12, 7), firstColor);
            ImageOps.DrawLine(button, new Point(buttonWidth - 13, 8), new Point(buttonWidth - 14, 9), secondColor);
            ImageOps.DrawLine(button, new Point(buttonWidth - 8, 5), new Point(buttonWidth - 10, 7), firstColor);
            ImageOps.SetPixel(button, buttonWidth - 11, 8, lastColor);

            // Bottom left-side shine.
            ImageOps.SetPixel(button, 11, buttonHeight - 6, lastColor);
            ImageOps.DrawLine(button, new Point(12, buttonHeight - 7), new Point(13, buttonHeight - 8), firstColor);
            ImageOps.SetPixel(button, 14, buttonHeight - 9, lastColor);

            // This is mainly to decorate the space bar button.
            if (buttonWidth > 96 || buttonHeight > 25)
            {
                ImageOps.SetPixel(button, 10, buttonHeight - 5, lastColor);

                ImageOps.SetPixel(button, 30, buttonHeight - 5, secondColor);
                ImageOps.SetPixel(button, 31, buttonHeight - 6, lastColor);
                ImageOps.DrawLine(button, new Point(32, buttonHeight - 5), new Point(34, buttonHeight - 7), firstColor);
                ImageOps.SetPixel(button, 35, buttonHeight - 8, secondColor);
                ImageOps.SetPixel(button, 36, buttonHeight - 9, lastColor);
                ImageOps.SetPixel(button, 34, buttonHeight - 5, secondColor);
                ImageOps.SetPixel(button, 35, buttonHeight - 6, lastColor);
            }

            if (buttonHeight > 25)
            {
                // Bottom right-side shine.
                ImageOps.DrawLine(button, new Point(buttonWidth - 20, buttonHeight - 5),
                    new Point(buttonWidth - 16, buttonHeight - 9), secondColor);
                ImageOps.SetPixel(button, buttonWidth - 15, buttonHeight - 10, lastColor);
                ImageOps.DrawLine(button, new Point(buttonWidth - 18, buttonHeight - 5),
                    new Point(buttonWidth - 14, buttonHeight - 9), firstColor);
                ImageOps.DrawLine(button, new Point(buttonWidth - 13, buttonHeight - 10),
                    new Point(buttonWidth - 12, buttonHeight - 11), secondColor);
                ImageOps.DrawLine(button, new Point(buttonWidth - 11, buttonHeight - 12),
                    new Point(buttonWidth - 10, buttonHeight - 13), lastColor);
                ImageOps.DrawLine(button, new Point(buttonWidth - 16, buttonHeight - 5),
                    new Point(buttonWidth - 12, buttonHeight - 9), secondColor);
                ImageOps.SetPixel(button, buttonWidth - 11, buttonHeight - 10, lastColor);
            }
        }
    }

    /// <summary>
    /// Replaces the pixels that only the pressed sprite covers with the button background, so a
    /// pressed button does not paint a hole where the released plate used to be,
    /// <c>fheroes2::makeTransparentBackground()</c>.
    /// </summary>
    /// <param name="released">Released sprite; it is not modified but its transform layer is read.</param>
    /// <param name="pressed">Pressed sprite; it is modified in place.</param>
    /// <param name="background">Texture behind the button, usually the window interior.</param>
    /// <remarks>
    /// The background is cropped from its centre, which is where the reference takes it from. Both
    /// sprites must carry a transform layer, as the reference asserts.
    /// </remarks>
    /// <exception cref="InvalidOperationException">One of the sprites has no transform layer.</exception>
    public static void MakeTransparentBackground(Sprite released, Sprite pressed, Sprite background)
    {
        ArgumentNullException.ThrowIfNull(released);
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(background);

        if (released.Transform is null || pressed.Transform is null)
        {
            throw new InvalidOperationException(
                "A transparent background can only be applied to sprites that carry a transform layer.");
        }

        if (released.Width != pressed.Width || released.Height != pressed.Height)
        {
            throw new ArgumentException("The released and pressed sprites must have the same size.", nameof(pressed));
        }

        int offsetX = (background.Width - pressed.Width) / 2;
        int offsetY = (background.Height - pressed.Height) / 2;
        int pressedStride = pressed.Image.Stride;
        int releasedStride = released.Image.Stride;

        for (int y = 0; y < pressed.Height; ++y)
        {
            int backgroundY = offsetY + y;
            if (backgroundY < 0 || backgroundY >= background.Height)
            {
                continue;
            }

            for (int x = 0; x < pressed.Width; ++x)
            {
                int index = (y * pressedStride) + x;

                // Only pixels that appear when the button is pressed become opaque background.
                if (pressed.Transform[index] != 1 || released.Transform[(y * releasedStride) + x] != 0)
                {
                    continue;
                }

                int backgroundX = offsetX + x;
                if (backgroundX < 0 || backgroundX >= background.Width)
                {
                    continue;
                }

                pressed.Image[x, y] = background.Image[backgroundX, backgroundY];
                pressed.Transform[index] = 0;
            }
        }
    }

    private static void Copy(Sprite source, int sourceX, int sourceY, Sprite destination, int destinationX,
        int destinationY, int width, int height) =>
        ImageOps.Copy(source, new Rect(sourceX, sourceY, width, height), destination, new Point(destinationX, destinationY));

    private static void EnsurePositive(int value, string parameter, string message)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameter, message);
        }
    }
}
