using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Builds the slider of a scroll bar out of a single piece of artwork, a port of
/// <c>fheroes2::generateScrollbarSlider</c> (<c>src/fheroes2/gui/ui_scrollbar.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The original scroll bar artwork is one fixed length piece, which on a long list leaves a thumb
/// that says nothing about how much of the list is visible. The reference therefore stretches the
/// piece: the start cap is copied verbatim, the middle is repeated as often as needed and the end
/// cap finishes the slider. The length is
/// <c>areaLength * perView / max( perView, total )</c>, clamped to
/// <see cref="MinimumLength"/> so a very long list still leaves something to grab.
/// </para>
/// <para>
/// Two edge cases return the original artwork untouched: an area too short for it and a list that
/// needs no scaling at all. Both mirror the guards of the reference, so a caller never has to check
/// the sizes itself.
/// </para>
/// </remarks>
public static class ScrollSlider
{
    /// <summary>
    /// Shortest slider the generator produces, <c>minimumSliderLength</c> of the reference. Without
    /// the floor a list of a thousand entries would leave a one pixel thumb.
    /// </summary>
    public const int MinimumLength = 15;

    /// <summary>
    /// Builds the slider sprite.
    /// </summary>
    /// <param name="original">The artwork the slider is composed of, e.g. frame 3 of <c>ESCROLL.ICN</c>.</param>
    /// <param name="horizontal">
    /// Whether the slider grows along the X axis; <see langword="false"/> makes a vertical slider,
    /// which is the one of a list.
    /// </param>
    /// <param name="areaLength">Length of the groove the slider moves in, in pixels.</param>
    /// <param name="visibleCount">Number of entries visible at once, <c>elementCountPerView</c>.</param>
    /// <param name="totalCount">Number of entries of the whole list.</param>
    /// <param name="startArea">Cap of the slider inside <paramref name="original"/>.</param>
    /// <param name="middleArea">
    /// Middle piece that is repeated to stretch the slider, inside <paramref name="original"/>.
    /// </param>
    /// <returns>
    /// A new sprite of <paramref name="original"/>'s cross size and the computed length, or
    /// <paramref name="original"/> itself when nothing has to be scaled.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is <see langword="null"/>.</exception>
    public static Sprite Create(
        Sprite original,
        bool horizontal,
        int areaLength,
        int visibleCount,
        int totalCount,
        in Rect startArea,
        in Rect middleArea)
    {
        ArgumentNullException.ThrowIfNull(original);

        int currentLength = horizontal ? original.Width : original.Height;
        int step = horizontal ? middleArea.Width : middleArea.Height;

        if (step < 1 || areaLength < currentLength || visibleCount < 1)
        {
            return original;
        }

        if (areaLength * visibleCount == currentLength * totalCount)
        {
            return original;
        }

        int middleLength = (areaLength * visibleCount / Math.Max(visibleCount, totalCount)) - currentLength;

        int width = original.Width;
        int height = original.Height;

        if (horizontal)
        {
            width = Math.Max(MinimumLength, Math.Max(width + middleLength, startArea.Width * 2));
        }
        else
        {
            height = Math.Max(MinimumLength, Math.Max(height + middleLength, startArea.Height * 2));
        }

        Sprite output = new(width, height);

        Copy(original, startArea, output, startArea.Position, startArea.Width, startArea.Height);

        if (middleLength < 0)
        {
            // The slider has to shrink: the end cap slides over the middle of the artwork.
            return Shrink(original, output, startArea, horizontal, width, height);
        }

        return Stretch(original, output, startArea, middleArea, horizontal, step, middleLength);
    }

    /// <summary>
    /// Position of the slider for <paramref name="scroll"/>: the top of the groove for the first
    /// entry and flush with its far edge for the last one, so the thumb never leaves the groove.
    /// </summary>
    /// <param name="groove">The area the slider moves in.</param>
    /// <param name="sliderLength">Length of the slider itself, along the axis of the groove.</param>
    /// <param name="scroll">Index of the first visible entry.</param>
    /// <param name="visibleCount">Number of entries visible at once.</param>
    /// <param name="totalCount">Number of entries of the whole list.</param>
    /// <param name="horizontal">Whether the groove runs along the X axis.</param>
    public static Point SliderPosition(
        in Rect groove, int sliderLength, int scroll, int visibleCount, int totalCount, bool horizontal)
    {
        int travel = (horizontal ? groove.Width : groove.Height) - sliderLength;
        int steps = totalCount - visibleCount;

        if (travel <= 0 || steps <= 0)
        {
            return groove.Position;
        }

        int offset = travel * Math.Clamp(scroll, 0, steps) / steps;

        return horizontal
            ? new Point(groove.X + offset, groove.Y)
            : new Point(groove.X, groove.Y + offset);
    }

    /// <summary>
    /// Cuts a slider shorter than its artwork by copying the end cap over the middle, the
    /// <c>middleLength &lt; 0</c> branch of the reference.
    /// </summary>
    private static Sprite Shrink(
        Sprite original, Sprite output, in Rect startArea, bool horizontal, int width, int height)
    {
        if (horizontal)
        {
            int copyWidth = width - startArea.Width;
            Copy(
                original,
                new Rect(startArea.X + original.Width - copyWidth, startArea.Y, copyWidth, startArea.Height),
                output,
                new Point(startArea.Width, startArea.Y),
                copyWidth,
                startArea.Height);
        }
        else
        {
            int copyHeight = height - startArea.Height;
            Copy(
                original,
                new Rect(startArea.X, startArea.Y + original.Height - copyHeight, startArea.Width, copyHeight),
                output,
                new Point(startArea.X, startArea.Height),
                startArea.Width,
                copyHeight);
        }

        return output;
    }

    /// <summary>
    /// Stretches a slider by repeating the middle piece, the rest of the reference function.
    /// </summary>
    private static Sprite Stretch(
        Sprite original,
        Sprite output,
        in Rect startArea,
        in Rect middleArea,
        bool horizontal,
        int step,
        int middleLength)
    {
        int offset = horizontal
            ? startArea.X + startArea.Width
            : startArea.Y + startArea.Height;

        int chunks = middleLength / step;

        for (int index = 0; index < chunks; ++index)
        {
            Copy(
                original,
                middleArea,
                output,
                horizontal ? new Point(offset, startArea.Y) : new Point(startArea.X, offset),
                middleArea.Width,
                middleArea.Height);

            offset += step;
        }

        int leftover = middleLength - (chunks * step);

        if (leftover > 0)
        {
            Copy(
                original,
                middleArea,
                output,
                horizontal ? new Point(offset, startArea.Y) : new Point(startArea.X, offset),
                horizontal ? leftover : middleArea.Width,
                horizontal ? middleArea.Height : leftover);

            offset += leftover;
        }

        if (horizontal)
        {
            int endWidth = original.Width - startArea.Width;
            Copy(
                original,
                new Rect(startArea.X + startArea.Width, startArea.Y, endWidth, startArea.Height),
                output,
                new Point(offset, startArea.Y),
                endWidth,
                startArea.Height);
        }
        else
        {
            int endHeight = original.Height - startArea.Height;
            Copy(
                original,
                new Rect(startArea.X, startArea.Y + startArea.Height, startArea.Width, endHeight),
                output,
                new Point(startArea.X, offset),
                startArea.Width,
                endHeight);
        }

        return output;
    }

    private static void Copy(
        Sprite source, in Rect sourceArea, Sprite destination, Point destinationOffset, int width, int height) =>
        ImageOps.Copy(
            source,
            new Rect(sourceArea.X, sourceArea.Y, width, height),
            destination,
            destinationOffset);
}
