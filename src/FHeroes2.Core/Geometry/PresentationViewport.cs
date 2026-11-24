namespace FHeroes2.Core.Geometry;

/// <summary>
/// Describes where a fixed size logical frame (640x480) is drawn inside an arbitrary
/// output surface.
/// </summary>
/// <remarks>
/// <para>
/// A window that is an exact multiple of the logical frame is scaled by that integer and covers the
/// window completely, which keeps every game pixel square and the same size. Any other window is
/// filled with the largest uniform scale that fits: a 1280x800 window gets a 1067x800 picture instead
/// of the 640x480 one an integer scale would have to settle for. The frame keeps its 4:3 aspect ratio,
/// so a 16:10 window keeps black bars left and right - stretching the picture to the window instead
/// would make the interface taller or wider than the reference draws it.
/// </para>
/// <para>
/// <b>Why not always the integer scale.</b> The window size is whatever the configuration file says
/// (<c>videomode</c>, see <c>ADR-204</c> and <c>ADR-211</c>); a window that was resized to 1280x800
/// would otherwise show a quarter-sized game in the middle of a black screen.
/// </para>
/// </remarks>
public readonly record struct PresentationViewport(Rect Destination, float Scale, bool IsIntegerScale, Size Logical)
{
    /// <summary>Where the logical frame goes when there is nothing to draw into.</summary>
    public static PresentationViewport Empty => new(Rect.Empty, 0.0f, false, Size.Empty);

    /// <summary>Fits a logical frame into an output surface.</summary>
    /// <param name="logical">Size of the frame every scene draws into, 640x480 in the port.</param>
    /// <param name="output">Size of the window (or of its renderer's output).</param>
    /// <returns>
    /// The destination rectangle, centred, and the scale that fills it. A window that is an exact
    /// multiple of the frame reports <see cref="IsIntegerScale"/>.
    /// </returns>
    /// <remarks>
    /// The destination is rounded to whole pixels: the picture is at most one pixel shorter than the
    /// window on the constrained axis, never larger.
    /// </remarks>
    public static PresentationViewport Compute(Size logical, Size output)
    {
        if (logical.IsEmpty || output.IsEmpty)
        {
            return Empty;
        }

        // Largest uniform scale that still fits; an exact multiple of the frame gives a whole number,
        // so 1280x960 stays pixel perfect.
        float scale = Math.Min((float)output.Width / logical.Width, (float)output.Height / logical.Height);
        int width = Math.Max(1, (int)MathF.Round(logical.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(logical.Height * scale));
        bool integer = MathF.Abs(scale - MathF.Round(scale)) < 0.0005f;

        return new PresentationViewport(
            new Rect((output.Width - width) / 2, (output.Height - height) / 2, width, height),
            scale,
            integer,
            logical);
    }

    /// <summary>Maps a logical (game pixel) coordinate to an output (window) coordinate.</summary>
    public Point ToOutput(Point logicalPoint) =>
        new(Destination.X + (int)MathF.Round(logicalPoint.X * Scale), Destination.Y + (int)MathF.Round(logicalPoint.Y * Scale));

    /// <summary>
    /// Maps an output (window) coordinate back onto the logical frame, the inverse of
    /// <see cref="ToOutput"/> that input handling needs.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the point lies in the letterbox area or the viewport is empty,
    /// so a click on the black border never reaches a widget.
    /// </returns>
    public bool TryToLogical(Point outputPoint, out Point logicalPoint)
    {
        if (Scale <= 0.0f || Destination.IsEmpty || !Destination.Contains(outputPoint))
        {
            logicalPoint = default;
            return false;
        }

        // The destination is rounded to pixels, so its far edge can map a hair past the last game
        // pixel; clamp it instead of handing a scene a coordinate it has to reject.
        logicalPoint = new Point(
            Math.Clamp((int)MathF.Floor((outputPoint.X - Destination.X) / Scale), 0, Logical.Width - 1),
            Math.Clamp((int)MathF.Floor((outputPoint.Y - Destination.Y) / Scale), 0, Logical.Height - 1));

        return true;
    }
}
