using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The thumb of a scroll bar, <c>fheroes2::Scrollbar</c>
/// (<c>src/fheroes2/gui/ui_scrollbar.h:33-93</c>).
/// </summary>
public sealed class ScrollBarTests
{
    [Fact]
    public void SetRange_CentersTheThumbWhenThereIsNothingToScroll()
    {
        ScrollBar bar = new();
        bar.SetSlider(new Sprite(12, 20));
        bar.Area = new Rect(10, 20, 12, 100);

        bar.SetRange(5, 5);

        // room along the groove is 80, so the thumb is half a room down.
        Assert.Equal(new Point(10, 60), bar.Position);
        Assert.Equal(5, bar.CurrentIndex);
        Assert.False(bar.MoveToIndex(5));
    }

    [Fact]
    public void SetRange_PutsTheThumbAtTheStart()
    {
        ScrollBar bar = new();
        bar.SetSlider(new Sprite(12, 20));
        bar.Area = new Rect(10, 20, 12, 100);

        bar.SetRange(0, 9);

        Assert.Equal(new Point(10, 20), bar.Position);
        Assert.Equal(0, bar.CurrentIndex);
        Assert.Equal(0, bar.MinIndex);
        Assert.Equal(9, bar.MaxIndex);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(3, 46)]
    [InlineData(9, 100)]
    public void MoveToIndex_TruncatesTheOffsetLikeTheReference(int index, int expectedY)
    {
        ScrollBar bar = new();
        bar.SetSlider(new Sprite(12, 20));
        bar.Area = new Rect(10, 20, 12, 100);
        bar.SetRange(0, 9);

        bar.MoveToIndex(index);

        Assert.Equal(new Point(10, expectedY), bar.Position);
        Assert.Equal(index, bar.CurrentIndex);
    }

    [Fact]
    public void MoveToIndex_ClampsAndReportsNoMoveForTheSameIndex()
    {
        ScrollBar bar = new();
        bar.SetSlider(new Sprite(12, 20));
        bar.Area = new Rect(10, 20, 12, 100);
        bar.SetRange(0, 9);

        Assert.False(bar.MoveToIndex(-5));
        Assert.Equal(0, bar.CurrentIndex);
        Assert.Equal(new Point(10, 20), bar.Position);

        Assert.True(bar.MoveToIndex(9));
        Assert.False(bar.MoveToIndex(9));
        Assert.Equal(9, bar.CurrentIndex);
    }

    [Fact]
    public void HorizontalGroove_TravelsAlongX()
    {
        ScrollBar bar = new();
        bar.SetSlider(new Sprite(20, 12));
        bar.Area = new Rect(0, 0, 100, 12);
        bar.SetRange(0, 9);

        Assert.False(bar.IsVertical);
        Assert.Equal(new Point(0, 0), bar.Position);

        bar.MoveToIndex(9);

        Assert.Equal(new Point(80, 0), bar.Position);
    }

    [Fact]
    public void MoveToPosition_CentresTheThumbOnTheCursor()
    {
        ScrollBar bar = new();
        bar.SetSlider(new Sprite(12, 20));
        bar.Area = new Rect(10, 20, 12, 100);
        bar.SetRange(0, 9);

        // 40 pixels into an 80 pixel groove is index 5 of 0..9.
        bar.MoveToPosition(new Point(16, 70));

        Assert.Equal(5, bar.CurrentIndex);
        Assert.Equal(20 + (5 * 80 / 9), bar.Position.Y);

        // Nothing to scroll: the request is ignored.
        bar.SetRange(3, 3);
        bar.MoveToPosition(new Point(16, 100));
        Assert.Equal(3, bar.CurrentIndex);
    }

    [Fact]
    public void ForwardAndBackward_StopAtTheBounds()
    {
        ScrollBar bar = new();
        bar.SetSlider(new Sprite(12, 20));
        bar.Area = new Rect(10, 20, 12, 100);
        bar.SetRange(2, 4);

        bar.Backward();
        Assert.Equal(2, bar.CurrentIndex);

        bar.Forward();
        bar.Forward();
        bar.Forward();
        Assert.Equal(4, bar.CurrentIndex);
    }

    [Fact]
    public void SetRange_RejectsAnInvertedRange()
    {
        ScrollBar bar = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => bar.SetRange(5, 4));
    }
}
