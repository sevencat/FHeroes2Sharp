using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="StandardWindow.RenderScrollbarBackground"/>: the groove a list scrollbar sits in
/// (<c>ui_window.cpp:400-431</c>).
/// </summary>
public sealed class StandardWindowScrollbarTests
{
    private const byte Background = 7;
    private const byte Groove = 33;

    [Fact]
    public void RenderScrollbarBackground_PaintsTheGrooveInThreeParts()
    {
        PixelBuffer output = CreateOutput();
        Rect roi = new(40, 20, 16, 120);

        StandardWindow.RenderScrollbarBackground(output, roi, CreateFrame());

        // The 19 pixel cap, the 88 pixel body and the 19 pixel foot, in that order (:406-431).
        Assert.Equal(Groove, output[roi.X, roi.Y]);
        Assert.Equal(Groove, output[roi.X, roi.Y + 19]);
        Assert.Equal(Groove, output[roi.X, roi.Y + 118]);
        Assert.Equal(Groove, output[roi.X + 15, roi.Y + 118]);

        // The four shadow rows under the foot, and nothing beyond them.
        Assert.NotEqual(Background, output[roi.X, roi.Y + roi.Height]);
        Assert.Equal(Background, output[roi.X, roi.Y + roi.Height + 4]);
    }

    [Fact]
    public void RenderScrollbarBackground_ShadesTheLeftSideAndUnderTheFoot()
    {
        PixelBuffer output = CreateOutput();
        Rect roi = new(40, 20, 16, 120);

        StandardWindow.RenderScrollbarBackground(output, roi, CreateFrame());

        // Four steps of shadow on the left of the column and under its foot (:433-439).
        for (int index = 0; index < 4; ++index)
        {
            Assert.NotEqual(Background, output[roi.X - index - 1, roi.Y + index + 1]);
            Assert.NotEqual(Background, output[roi.X - index - 1, roi.Y + roi.Height + index]);
        }

        // The groove itself keeps its colour: the shadow never covers it.
        Assert.Equal(Groove, output[roi.X, roi.Y]);
    }

    [Fact]
    public void RenderScrollbarBackground_RejectsNullArguments()
    {
        PixelBuffer output = CreateOutput();
        Rect roi = new(40, 20, 16, 120);

        Assert.Throws<ArgumentNullException>(() => StandardWindow.RenderScrollbarBackground(null!, roi,
            CreateFrame()));
        Assert.Throws<ArgumentNullException>(() => StandardWindow.RenderScrollbarBackground(output, roi, null!));
    }

    private static PixelBuffer CreateOutput()
    {
        PixelBuffer output = new(120, 200);
        output.Fill(Background);
        return output;
    }

    /// <summary>
    /// Builds a stand-in for <c>ADVBORD.ICN</c> frame 0: the groove pieces are read at x 536 and y 176/196/285, so
    /// the frame has to be large enough to hold them.
    /// </summary>
    private static Sprite CreateFrame()
    {
        PixelBuffer image = new(560, 320);
        image.Fill(Groove);
        return new Sprite(image, 0, 0);
    }
}
