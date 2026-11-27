using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="CheckboxWidget"/>, the <c>renderCheckbox()</c> of the auto playtest dialog
/// (<c>game_auto_playtest.cpp:360-381</c>).
/// </summary>
public sealed class CheckboxWidgetTests
{
    private const byte BoxColor = 40;
    private const byte TickColor = 60;
    private const byte Background = 3;

    [Fact]
    public void TheAreaIsTheBoxAtItsPosition()
    {
        CheckboxWidget box = new(CreateBox(10, 9), CreateTick(2, 2), new Point(30, 20));

        Assert.Equal(new Rect(30, 20, 10, 9), box.Area);
        Assert.Equal(new Point(30, 20), box.Position);
        Assert.False(box.IsChecked);
        Assert.True(box.IsActive);
    }

    [Fact]
    public void AnUncheckedBoxDrawsOnlyTheBox()
    {
        CheckboxWidget box = new(CreateBox(10, 9), CreateTick(2, 2), new Point(5, 5));

        PixelBuffer target = Paint(box);

        Assert.Equal(BoxColor, target[5, 5]);
        Assert.Equal(BoxColor, target[14, 13]);
        Assert.Equal(Background, target[4, 4]);
    }

    [Fact]
    public void ACheckedBoxDrawsTheTickAtItsOwnFrameOffset()
    {
        CheckboxWidget box = new(CreateBox(10, 9), CreateTick(2, 2), new Point(5, 5), isChecked: true);

        PixelBuffer target = Paint(box);

        // 勾是 1x1 的测试图，偏移 2,2：它落在 (7,7)，框的左上角 (5,5) 还是底图的颜色。
        Assert.Equal(TickColor, target[7, 7]);
        Assert.Equal(BoxColor, target[5, 5]);
        Assert.Equal(BoxColor, target[6, 6]);
    }

    [Fact]
    public void AnInactiveBoxNeverShowsTheTickAndIsDarkened()
    {
        CheckboxWidget box = new(CreateBox(10, 9), CreateTick(2, 2), new Point(5, 5), isChecked: true)
        {
            IsActive = false,
        };

        PixelBuffer target = Paint(box);

        // 不活动时框被 DARKENING 压暗，勾根本不画。
        Assert.Equal(PaletteTable.Get(PaletteId.Darkening)[BoxColor], target[5, 5]);
        Assert.Equal(PaletteTable.Get(PaletteId.Darkening)[BoxColor], target[7, 7]);
    }

    [Fact]
    public void TheEvilInterfaceRecoloursBoxAndTickAlike()
    {
        CheckboxWidget box = new(CreateBox(10, 9), CreateTick(2, 2), new Point(5, 5), isChecked: true)
        {
            IsEvilInterface = true,
        };

        PixelBuffer target = Paint(box);

        // 参考在框与勾都画完之后才套 evil 调色板，所以两者都变了色。
        Assert.Equal(PaletteTable.Get(PaletteId.GoodToEvilInterface)[BoxColor], target[5, 5]);
        Assert.Equal(PaletteTable.Get(PaletteId.GoodToEvilInterface)[TickColor], target[7, 7]);
    }

    [Fact]
    public void ToggleFlipsAnActiveBoxOnly()
    {
        CheckboxWidget box = new(CreateBox(10, 9), CreateTick(2, 2), new Point(0, 0));

        Assert.True(box.Toggle());
        Assert.True(box.IsChecked);

        Assert.True(box.Toggle());
        Assert.False(box.IsChecked);

        box.IsActive = false;

        // 参考里灰掉的「音效」复选点了不进任何分支，端口因此同样不翻。
        Assert.False(box.Toggle());
        Assert.False(box.IsChecked);
    }

    [Fact]
    public void ABoxWithoutATickStillWorksWhenUnchecked()
    {
        CheckboxWidget box = new(CreateBox(10, 9), tick: null, new Point(2, 2));

        Assert.True(box.Toggle());

        // 归档缺帧时参考画 errorImage，端口的选择是「画不出来就不画」，但状态照翻。
        PixelBuffer target = new(20, 20);
        target.Fill(Background);

        box.Draw(target);

        Assert.True(box.IsChecked);
        Assert.Equal(BoxColor, target[2, 2]);
    }

    private static PixelBuffer Paint(CheckboxWidget box)
    {
        PixelBuffer target = new(20, 20);
        target.Fill(Background);

        box.Draw(target);

        return target;
    }

    private static Sprite CreateBox(int width, int height)
    {
        PixelBuffer image = new(width, height);
        image.Fill(BoxColor);

        return new Sprite(image);
    }

    private static Sprite CreateTick(int offsetX, int offsetY)
    {
        PixelBuffer image = new(1, 1);
        image.Fill(TickColor);

        return new Sprite(image, offsetX, offsetY);
    }
}
