using FHeroes2.Core.Geometry;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="InputStringLayout"/>, the arithmetic of <c>Dialog::inputString()</c>
/// (<c>dialog_selectcount.cpp:227-304</c>).
/// </summary>
/// <remarks>
/// 参考把这几个数和算式直接写在对话框体里，没有函数可测；端口抽出来的目的就是让「框多高、输入区落在哪、文本能点的
/// 范围多大」在没有素材时也能钉住。坐标都是 640x480 的逻辑像素，框高不含底部那排按钮（<see cref="FrameBox"/>
/// 自己加）。
/// </remarks>
public sealed class InputStringLayoutTests
{
    private static readonly Rect Area = new(48, 40, 544, 400);

    [Fact]
    public void TheFrameLeavesRoomForTheTitleTheTextAndTheKeyboardButton()
    {
        // 10 + title + text + 10 + input + 20.
        Assert.Equal(130, InputStringLayout.FrameHeight(0, 0, 90));
        Assert.Equal(220, InputStringLayout.FrameHeight(30, 60, 90));
    }

    [Fact]
    public void TheTitleStartsTwelvePixelsUnderTheTopOfTheFrame()
    {
        Assert.Equal(new Point(Area.X, Area.Y + 12), InputStringLayout.TitlePosition(Area));
    }

    [Fact]
    public void TheBodyFollowsTheTitle()
    {
        Assert.Equal(new Point(Area.X, Area.Y + 12 + 30), InputStringLayout.BodyPosition(Area, 30));
        Assert.Equal(new Point(Area.X, Area.Y + 12), InputStringLayout.BodyPosition(Area, 0));
    }

    [Fact]
    public void TheInputAreaIsCentredAndTenPixelsUnderTheText()
    {
        Rect area = InputStringLayout.InputArea(Area, 289, 90, 30, 60);

        Assert.Equal(Area.X + ((Area.Width - 289) / 2), area.X);
        Assert.Equal(Area.Y + 10 + 30 + 60 + 10, area.Y);
        Assert.Equal(289, area.Width);
        Assert.Equal(90, area.Height);
    }

    [Fact]
    public void ASingleLineTextKeepsTheInsetsOfTheArtwork()
    {
        Rect input = new(100, 200, 289, 90);

        Rect text = InputStringLayout.TextInputArea(input, isMultiLine: false);

        Assert.Equal(new Rect(113, 201, 263, 87), text);
    }

    [Fact]
    public void AMultiLineTextKeepsFivePixelsOnEverySide()
    {
        Rect input = new(100, 200, 226, 265);

        Rect text = InputStringLayout.TextInputArea(input, isMultiLine: true);

        Assert.Equal(new Rect(105, 205, 216, 255), text);
    }

    [Fact]
    public void TheClickableAreaIsWiderSoAClickPastTheEndStillLandsInTheText()
    {
        Rect text = new(113, 201, 263, 87);

        Assert.Equal(new Rect(113, 201, 267, 87), InputStringLayout.ClickArea(text));
    }

    [Fact]
    public void TheVirtualKeyboardButtonIsCentredAndThirtyPixelsAboveTheButtonRow()
    {
        Point position = InputStringLayout.VirtualKeyboardPosition(Area, new Size(84, 24));

        Assert.Equal(Area.X + ((Area.Width - 84) / 2), position.X);
        Assert.Equal(Area.Y + Area.Height - 24 - 30, position.Y);
    }
}
