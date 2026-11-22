using FHeroes2.Core.Geometry;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The row text of the resolution chooser (<c>dialog_resolution.cpp:61-72</c>): the frame buffer size, then the window
/// size or the magnification.
/// </summary>
public sealed class ResolutionDialogTests
{
    [Fact]
    public void AWindowThatMatchesTheFrameBufferHasNoFactor()
    {
        Assert.Equal("640 x 480", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 640, 480)));
    }

    [Fact]
    public void AMagnifiedWindowPrintsTheIntegerPartAndOneDecimal()
    {
        Assert.Equal("640 x 480 (x2.0)", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 1280, 960)));
        Assert.Equal("640 x 480 (x1.5)", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 960, 720)));
        Assert.Equal("640 x 480 (x3.7)", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 2400, 1800)));
    }

    [Fact]
    public void OnlyOneAxisBeingScaledPrintsNoFactor()
    {
        // 参考的条件是 `screenWidth != gameWidth && screenHeight != gameHeight`（两轴都不同才算缩放），
        // 图形设置页的 drawResolution() 却用 `||`，两处不一致，这里按参考的行打印照搬 AND。
        Assert.Equal("640 x 480", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 1280, 480)));
    }

    [Fact]
    public void TheFactorIsTheHorizontalOne()
    {
        // 参考只除宽，竖向上限不参与。
        Assert.Equal("800 x 600 (x1.6)", ResolutionDialog.FormatRow(new ResolutionInfo(800, 600, 1280, 1024)));
    }
}
