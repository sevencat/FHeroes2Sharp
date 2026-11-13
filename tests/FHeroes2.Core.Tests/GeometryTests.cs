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
    [InlineData(1920, 1080, 2, 320, 60)]
    [InlineData(2560, 1440, 3, 320, 0)]
    [InlineData(1600, 900, 1, 480, 210)]
    public void PresentationViewport_IntegerScale_LetterboxesAndCenters(int width, int height, int scale, int expectedX, int expectedY)
    {
        PresentationViewport viewport = PresentationViewport.Compute(
            new Size(ScreenBufferDefaults.Width, ScreenBufferDefaults.Height),
            new Size(width, height));

        Assert.True(viewport.IsIntegerScale);
        Assert.Equal(scale, viewport.Scale);
        Assert.Equal(expectedX, viewport.Destination.X);
        Assert.Equal(expectedY, viewport.Destination.Y);
        Assert.Equal(ScreenBufferDefaults.Width * scale, viewport.Destination.Width);
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

    /// <summary>Local constants so the test does not depend on the imaging namespace.</summary>
    private static class ScreenBufferDefaults
    {
        public const int Width = 640;

        public const int Height = 480;
    }
}
