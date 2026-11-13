namespace FHeroes2.Core.Geometry;

/// <summary>
/// Describes where a fixed size logical frame (640x480) is drawn inside an arbitrary
/// output surface. Integer scaling with letterboxing is preferred; fractional scaling is
/// only used when the window is smaller than the logical frame.
/// </summary>
public readonly record struct PresentationViewport(Rect Destination, float Scale, bool IsIntegerScale)
{
    public static PresentationViewport Compute(Size logical, Size output)
    {
        if (logical.IsEmpty || output.IsEmpty)
        {
            return new PresentationViewport(Rect.Empty, 0.0f, false);
        }

        int integerScale = Math.Min(output.Width / logical.Width, output.Height / logical.Height);
        if (integerScale >= 1)
        {
            Size scaled = logical * integerScale;
            int left = (output.Width - scaled.Width) / 2;
            int top = (output.Height - scaled.Height) / 2;

            return new PresentationViewport(new Rect(left, top, scaled.Width, scaled.Height), integerScale, true);
        }

        float scale = Math.Min((float)output.Width / logical.Width, (float)output.Height / logical.Height);
        int width = Math.Max(1, (int)MathF.Round(logical.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(logical.Height * scale));

        return new PresentationViewport(
            new Rect((output.Width - width) / 2, (output.Height - height) / 2, width, height),
            scale,
            false);
    }

    /// <summary>Maps a logical (game pixel) coordinate to an output (window) coordinate.</summary>
    public Point ToOutput(Point logicalPoint) =>
        new(Destination.X + (int)MathF.Round(logicalPoint.X * Scale), Destination.Y + (int)MathF.Round(logicalPoint.Y * Scale));
}
