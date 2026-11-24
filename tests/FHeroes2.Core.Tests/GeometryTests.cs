using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void Rect_Intersect_ReturnsOverlap()
    {
        Rect first = new(10, 20, 100, 50);
        Rect second = new(50, 0, 100, 100);

        Rect intersection = first.Intersect(second);

        Assert.Equal(new Rect(50, 20, 60, 50), intersection);
    }

    [Fact]
    public void Rect_Intersect_Disjoint_ReturnsEmpty()
    {
        Rect intersection = new Rect(0, 0, 10, 10).Intersect(new Rect(20, 20, 5, 5));

        Assert.True(intersection.IsEmpty);
        Assert.Equal(Rect.Empty, intersection);
    }

    [Fact]
    public void Rect_Intersect_TouchingEdges_IsEmpty()
    {
        // Half open rectangles do not share pixels when they only touch.
        Rect intersection = new Rect(0, 0, 10, 10).Intersect(new Rect(10, 0, 10, 10));

        Assert.True(intersection.IsEmpty);
    }

    [Fact]
    public void Rect_Union_CoversBoth()
    {
        Rect union = Rect.Union(new Rect(10, 10, 10, 10), new Rect(-5, 30, 10, 10));

        Assert.Equal(new Rect(-5, 10, 25, 30), union);
    }

    [Fact]
    public void Rect_Contains_UsesHalfOpenEdges()
    {
        Rect rect = new(4, 4, 6, 6);

        Assert.True(rect.Contains(new Point(4, 4)));
        Assert.True(rect.Contains(new Point(9, 9)));
        Assert.False(rect.Contains(new Point(10, 9)));
        Assert.False(rect.Contains(new Point(9, 10)));
    }

    [Fact]
    public void Rect_OffsetAndInflate_KeepSizeConsistent()
    {
        Rect rect = new(10, 10, 20, 30);

        Assert.Equal(new Rect(15, 20, 20, 30), rect.Offset(5, 10));
        Assert.Equal(new Rect(8, 8, 24, 34), rect.Inflate(2, 2));
        Assert.Equal(new Point(20, 25), rect.Center);
        Assert.Equal(600, rect.Area);
    }

    [Theory]
    [InlineData(1280, 960, 2, 0, 0)]
    [InlineData(1920, 1440, 3, 0, 0)]
    [InlineData(2560, 1440, 3, 320, 0)]
    public void PresentationViewport_IntegerMultipleWindow_FillsItWithoutLetterbox(int width, int height, int scale, int expectedX, int expectedY)
    {
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(width, height));

        Assert.True(viewport.IsIntegerScale);
        Assert.Equal(scale, viewport.Scale);
        Assert.Equal(expectedX, viewport.Destination.X);
        Assert.Equal(expectedY, viewport.Destination.Y);
        Assert.Equal(ScreenBufferDefaults.Width * scale, viewport.Destination.Width);
        Assert.Equal(ScreenBufferDefaults.Height * scale, viewport.Destination.Height);
    }

    [Theory]
    [InlineData(1280, 800, 1067, 800, 106, 0)]
    [InlineData(1920, 1080, 1440, 1080, 240, 0)]
    [InlineData(1600, 900, 1200, 900, 200, 0)]
    public void PresentationViewport_WindowThatIsNotAMultiple_UsesTheLargestUniformScale(
        int width, int height, int expectedWidth, int expectedHeight, int expectedX, int expectedY)
    {
        // The window is not an integer multiple of the frame: an integer scale would leave a
        // quarter-sized picture in the middle of the window (ADR-211), so the largest scale that
        // fits is used instead.
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(width, height));

        Assert.False(viewport.IsIntegerScale);
        Assert.Equal(new Rect(expectedX, expectedY, expectedWidth, expectedHeight), viewport.Destination);
    }

    [Fact]
    public void PresentationViewport_SmallerThanLogical_UsesFractionalScaling()
    {
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(320, 240));

        Assert.False(viewport.IsIntegerScale);
        Assert.Equal(0.5f, viewport.Scale);
        Assert.Equal(new Rect(0, 0, 320, 240), viewport.Destination);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(639, 479)]
    [InlineData(320, 240)]
    [InlineData(1, 478)]
    public void PresentationViewport_TryToLogical_InvertsIntegerScaling(int x, int y)
    {
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(1280, 960));

        Assert.True(viewport.TryToLogical(viewport.ToOutput(new Point(x, y)), out Point logical));
        Assert.Equal(new Point(x, y), logical);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(639, 479)]
    [InlineData(320, 240)]
    public void PresentationViewport_TryToLogical_StaysWithinOnePixelOfAFractionalScale(int x, int y)
    {
        // A 1280x800 window is not an integer multiple, so the picture is scaled by 1.67: rounding the
        // pixel back can land on the neighbour, never further.
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(1280, 800));

        Assert.True(viewport.TryToLogical(viewport.ToOutput(new Point(x, y)), out Point logical));
        Assert.InRange(logical.X, x - 1, x + 1);
        Assert.InRange(logical.Y, y - 1, y + 1);
    }

    [Fact]
    public void PresentationViewport_TryToLogical_ClampsTheRoundedEdge()
    {
        // The destination is rounded to whole pixels, so its far edge lies a fraction of a game pixel
        // past 639; a click there must still map to the last column, not to 640.
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(1280, 800));

        Rect destination = viewport.Destination;
        Point farCorner = new(destination.Right - 1, destination.Bottom - 1);

        Assert.True(viewport.TryToLogical(farCorner, out Point logical));
        Assert.Equal(new Point(ScreenBufferDefaults.Width - 1, ScreenBufferDefaults.Height - 1), logical);
    }

    [Fact]
    public void PresentationViewport_TryToLogical_RejectsTheLetterbox()
    {
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(2560, 1440));

        // 2560x1440 gives 3x with a 320 pixel wide letterbox on each side.
        Assert.False(viewport.TryToLogical(new Point(100, 720), out Point left));
        Assert.Equal(default, left);

        Assert.False(viewport.TryToLogical(new Point(2400, 720), out _));

        Assert.True(viewport.TryToLogical(new Point(320, 0), out Point corner));
        Assert.Equal(new Point(0, 0), corner);

        Assert.True(viewport.TryToLogical(new Point(1280, 720), out Point middle));
        Assert.Equal(new Point(320, 240), middle);
    }

    [Fact]
    public void PresentationViewport_TryToLogical_RejectsAnEmptyViewport()
    {
        Assert.False(PresentationViewport.Empty.TryToLogical(new Point(0, 0), out _));
    }

    /// <summary>Local constants so the test does not depend on the imaging namespace.</summary>
    private static class ScreenBufferDefaults
    {
        public const int Width = 640;

        public const int Height = 480;
    }
}
