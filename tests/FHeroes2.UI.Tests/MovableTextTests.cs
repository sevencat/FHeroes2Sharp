using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Behaviour of the <c>MovableText</c> port (<c>src/fheroes2/gui/ui_tool.cpp:186</c>): what it paints,
/// where it stops at the region of interest, and the shift that keeps diacritics inside the region.
/// </summary>
public sealed class MovableTextTests
{
    /// <summary>Paints the whole text when the region covers it.</summary>
    [Fact]
    public void Draw_PaintsTheWholeTextWhenTheRoiCoversIt()
    {
        StubText text = new(10, 5);
        MovableText movable = new();
        movable.Update(text);

        PixelBuffer output = CreateBuffer(20, 20);
        movable.Draw(3, 4, output);

        Assert.False(movable.IsHidden);
        Assert.Equal(new Rect(3, 4, 10, 5), movable.DrawnArea);
        Assert.Equal(new Rect(3, 4, 10, 5), text.LastRoi);
        Assert.Equal(1, output[3, 4]);
        Assert.Equal(1, output[12, 8]);
        Assert.Equal(0, output[13, 4]);
        Assert.Equal(0, output[3, 9]);
    }

    /// <summary>Stops at the region of interest, the <c>textArea ^ roi</c> of the reference.</summary>
    [Fact]
    public void Draw_ClipsToTheRoi()
    {
        MovableText movable = new();
        movable.Update(new StubText(10, 5));

        PixelBuffer output = CreateBuffer(20, 20);
        movable.DrawInRoi(0, 0, output, new Rect(6, 2, 4, 3));

        Assert.Equal(new Rect(6, 2, 4, 3), movable.DrawnArea);
    }

    /// <summary>The text is pushed down so that its top edge lines up with the region, as in the reference.</summary>
    [Fact]
    public void Draw_ShiftsTheTextDownWhenTheRoiStartsBelowIt()
    {
        StubText text = new(10, 5);
        MovableText movable = new();
        movable.Update(text);

        PixelBuffer output = CreateBuffer(20, 20);
        movable.DrawInRoi(0, -3, output, new Rect(0, 0, 20, 20));

        // The text keeps its top edge on the region and grows downwards by the shift.
        Assert.Equal(0, text.LastY);
        Assert.Equal(new Rect(0, 0, 10, 5), movable.DrawnArea);
    }

    /// <summary>Nothing is painted outside the region, not even the part of the text that would fit.</summary>
    [Fact]
    public void Draw_DoesNothingWhenTheTextIsOutsideTheRoi()
    {
        MovableText movable = new();
        movable.Update(new StubText(10, 5));

        PixelBuffer output = CreateBuffer(20, 20);
        movable.DrawInRoi(0, 0, output, new Rect(15, 15, 5, 5));

        Assert.True(movable.IsHidden);
        Assert.Equal(Rect.Empty, movable.DrawnArea);
        Assert.Equal(0, output[0, 0]);
    }

    /// <summary>An empty text leaves the buffer alone.</summary>
    [Fact]
    public void Draw_WithEmptyTextPaintsNothing()
    {
        MovableText movable = new();
        movable.Update(new StubText(0, 0));

        PixelBuffer output = CreateBuffer(20, 20);
        movable.Draw(1, 1, output);

        Assert.True(movable.IsHidden);
        Assert.Equal(0, output[1, 1]);
    }

    /// <summary>Hiding clears the recorded area, so the host stops marking it dirty.</summary>
    [Fact]
    public void Hide_ClearsTheDrawnArea()
    {
        MovableText movable = new();
        movable.Update(new StubText(10, 5));

        movable.Draw(0, 0, CreateBuffer(20, 20));
        Assert.False(movable.IsHidden);

        movable.Hide();

        Assert.True(movable.IsHidden);
        Assert.Equal(Rect.Empty, movable.DrawnArea);
    }

    private static PixelBuffer CreateBuffer(int width, int height) => new(width, height);

    /// <summary>A text of a fixed size that fills its region with <c>1</c> and records where it was drawn.</summary>
    private sealed class StubText : TextBase
    {
        private readonly int _width;
        private readonly int _height;

        public StubText(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public Rect LastRoi { get; private set; } = Rect.Empty;

        public int LastY { get; private set; }

        public override bool IsEmpty => _width <= 0 || _height <= 0;

        public override string Content => string.Empty;

        public override int Width() => _width;

        public override int Height() => _height;

        public override int Width(int maxWidth) => Math.Min(_width, maxWidth);

        public override int Height(int maxWidth) => _height;

        public override int Rows(int maxWidth) => 1;

        public override Rect Area() => IsEmpty ? Rect.Empty : new Rect(0, 0, _width, _height);

        public override void DrawInRoi(int x, int y, PixelBuffer output, in Rect imageRoi)
        {
            LastRoi = imageRoi;
            LastY = y;

            for (int row = imageRoi.Y; row < imageRoi.Bottom; ++row)
            {
                for (int column = imageRoi.X; column < imageRoi.Right; ++column)
                {
                    output[column, row] = 1;
                }
            }
        }

        public override void DrawInRoi(int x, int y, int maxWidth, PixelBuffer output, in Rect imageRoi) =>
            DrawInRoi(x, y, output, imageRoi);

        public override void FitToOneRow(int maxWidth)
        {
        }
    }
}
