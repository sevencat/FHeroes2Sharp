using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="ControlPanel"/>, <c>Interface::ControlPanel</c> (<c>gui/interface_cpanel.cpp:34-133</c>).
/// </summary>
public sealed class ControlPanelTests
{
    private const byte Black = 1;
    private const byte White = 2;
    private const byte MidGrey = 40;

    [Fact]
    public void SetPos_LaysOutFiveTilesInARow()
    {
        ControlPanel panel = CreateSyntheticPanel();

        panel.SetPos(460, 444);

        Assert.Equal(new Rect(460, 444, 180, 36), panel.Area);
        Assert.Equal(new Rect(460, 444, 36, 36), panel.RadarRect);
        Assert.Equal(new Rect(496, 444, 36, 36), panel.IconsRect);
        Assert.Equal(new Rect(532, 444, 36, 36), panel.ButtonsRect);
        Assert.Equal(new Rect(568, 444, 36, 36), panel.StatusRect);
        Assert.Equal(new Rect(604, 444, 36, 36), panel.EndTurnRect);
    }

    [Theory]
    [InlineData(478, 462, ControlPanelButton.Radar)]
    [InlineData(514, 462, ControlPanelButton.Icons)]
    [InlineData(550, 462, ControlPanelButton.Buttons)]
    [InlineData(586, 462, ControlPanelButton.Status)]
    [InlineData(622, 462, ControlPanelButton.EndTurn)]
    [InlineData(400, 462, ControlPanelButton.None)]
    [InlineData(478, 400, ControlPanelButton.None)]
    public void QueueEventProcessing_ReportsTheClickedTile(int x, int y, ControlPanelButton expected)
    {
        ControlPanel panel = CreateSyntheticPanel();
        panel.SetPos(460, 444);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, new Point(x, y));
        events.OnMouseButton(false, MouseButtonType.Left, new Point(x, y));

        Assert.Equal(expected, panel.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_IgnoresTheRightButton()
    {
        ControlPanel panel = CreateSyntheticPanel();
        panel.SetPos(460, 444);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Right, new Point(478, 462));
        events.OnMouseButton(false, MouseButtonType.Right, new Point(478, 462));

        Assert.Equal(ControlPanelButton.None, panel.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_ConsumesTheClick()
    {
        ControlPanel panel = CreateSyntheticPanel();
        panel.SetPos(460, 444);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, new Point(478, 462));
        events.OnMouseButton(false, MouseButtonType.Left, new Point(478, 462));

        Assert.Equal(ControlPanelButton.Radar, panel.QueueEventProcessing(events));

        // The click was taken, so a second look at the same frame finds nothing.
        Assert.Equal(ControlPanelButton.None, panel.QueueEventProcessing(events));
    }

    [Fact]
    public void Redraw_BlendsTheTilesAtHalfOpacity()
    {
        ControlPanel panel = CreateSyntheticPanel();
        panel.SetPos(10, 20);

        PixelBuffer target = new(220, 80);
        target.Fill(Black);

        panel.Redraw(target, CreatePalette());

        // White (6 bit 63) over black at 50% is 31, the mid grey entry of the palette. The first tile covers
        // x 10..45 and the last one x 154..189, both y 20..55.
        Assert.Equal(MidGrey, target[10, 20]);
        Assert.Equal(MidGrey, target[44, 54]);
        Assert.Equal(MidGrey, target[188, 54]);

        // The strip is exactly 180x36, so nothing beyond it is painted.
        Assert.Equal(Black, target[9, 20]);
        Assert.Equal(Black, target[190, 54]);
        Assert.Equal(Black, target[10, 19]);
        Assert.Equal(Black, target[10, 56]);
    }

    [Fact]
    public void Redraw_RejectsNullArguments()
    {
        ControlPanel panel = CreateSyntheticPanel();

        Assert.Throws<ArgumentNullException>(() => panel.Redraw(null!, CreatePalette()));
        Assert.Throws<ArgumentNullException>(() => panel.Redraw(new PixelBuffer(8, 8), null!));
    }

    [Fact]
    public void Create_ReturnsNullWithoutArchive()
    {
        Assert.Null(ControlPanel.Create(null, false));
        Assert.Null(ControlPanel.Create(null, true));
    }

    [Fact]
    public void Create_ReadsTheButtonSetFromTheArchive()
    {
        if (AssetLocator.Resolve() is not { } directory)
        {
            return;
        }

        using AssetBundle? assets = AssetBundle.Open(directory);
        if (assets is null)
        {
            return;
        }

        ControlPanel? good = ControlPanel.Create(assets, false);
        ControlPanel? evil = ControlPanel.Create(assets, true);

        Assert.NotNull(good);
        Assert.NotNull(evil);

        // The tiles are drawn at 36x36 whatever the archive holds, so a frame smaller than that is a porting bug.
        good!.SetPos(0, 0);
        Assert.Equal(ControlPanel.ButtonSize, good.Area.Width / 5);
    }

    private static ControlPanel CreateSyntheticPanel()
    {
        return new ControlPanel(
            CreateTile(White),
            CreateTile(White),
            CreateTile(White),
            CreateTile(White),
            CreateTile(White));
    }

    private static Sprite CreateTile(byte color)
    {
        PixelBuffer image = new(ControlPanel.ButtonSize, ControlPanel.ButtonSize);
        image.Fill(color);

        return new Sprite(image);
    }

    /// <summary>
    /// Palette with black, white and the 6-bit grey 31 a 50% blend of the two lands on.
    /// </summary>
    private static Palette CreatePalette()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];

        raw[(White * 3) + 0] = 252;
        raw[(White * 3) + 1] = 252;
        raw[(White * 3) + 2] = 252;

        raw[(MidGrey * 3) + 0] = 124;
        raw[(MidGrey * 3) + 1] = 124;
        raw[(MidGrey * 3) + 2] = 124;

        return new Palette(raw);
    }
}
