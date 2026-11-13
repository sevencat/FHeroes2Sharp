using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The standard button row of a dialog, <c>ButtonGroup( const Rect &amp; area, const int buttonTypes )</c>
/// of <c>src/fheroes2/gui/ui_button.cpp</c>.
/// </summary>
public sealed class DialogButtonLayoutTests
{
    private static readonly Rect Window = new(100, 100, 200, 60);

    [Fact]
    public void OkAndCancel_AreSpreadWithAQuarterOfTheFreeWidth()
    {
        ButtonGroup group = ButtonGroup.CreateStandard(
            Window,
            DialogButton.Ok | DialogButton.Cancel,
            new FakeArtwork(okaySize: new Size(60, 25), cancelSize: new Size(70, 25)));

        Assert.Equal(2, group.Count);
        Assert.Equal(DialogButton.Ok, group.ValueOf(0));
        Assert.Equal(DialogButton.Cancel, group.ValueOf(1));

        // free width 70, padding 17: OK at x = 100 + 17, CANCEL at x = 300 - 70 - 17.
        Assert.Equal(new Rect(117, 135, 60, 25), group[0].Area);
        Assert.Equal(new Rect(213, 135, 70, 25), group[1].Area);
    }

    [Fact]
    public void YesAndNo_UseTheirOwnArtwork()
    {
        ButtonGroup group = ButtonGroup.CreateStandard(
            Window,
            DialogButton.Yes | DialogButton.No,
            new FakeArtwork(yesSize: new Size(50, 25), noSize: new Size(50, 25)));

        Assert.Equal(DialogButton.Yes, group.ValueOf(0));
        Assert.Equal(DialogButton.No, group.ValueOf(1));
        Assert.Equal(new Rect(125, 135, 50, 25), group[0].Area);
        Assert.Equal(new Rect(225, 135, 50, 25), group[1].Area);
    }

    [Fact]
    public void ASingleOk_IsCentredOnTheBottomEdge()
    {
        ButtonGroup group = ButtonGroup.CreateStandard(
            Window,
            DialogButton.Ok,
            new FakeArtwork(okaySize: new Size(60, 25)));

        Assert.Equal(1, group.Count);
        Assert.Equal(new Rect(170, 135, 60, 25), group[0].Area);
    }

    [Fact]
    public void ASingleCancel_IsCentredOnTheBottomEdge()
    {
        ButtonGroup group = ButtonGroup.CreateStandard(
            Window,
            DialogButton.Cancel,
            new FakeArtwork(cancelSize: new Size(70, 30)));

        Assert.Equal(1, group.Count);
        Assert.Equal(new Rect(165, 130, 70, 30), group[0].Area);
    }

    [Fact]
    public void AnUnsupportedCombination_ProducesAnEmptyGroup()
    {
        ButtonGroup group = ButtonGroup.CreateStandard(
            Window,
            DialogButton.Ok | DialogButton.Cancel | DialogButton.Yes,
            new FakeArtwork());

        Assert.Equal(0, group.Count);
    }

    [Fact]
    public void TheSpriteAnchor_ShiftsTheArea()
    {
        ButtonGroup group = ButtonGroup.CreateStandard(
            Window,
            DialogButton.Ok,
            new FakeArtwork(okaySize: new Size(60, 25), okayOffset: new Point(2, 3)));

        // The layout uses the sprite size, the button adds the anchor afterwards.
        Assert.Equal(new Rect(172, 138, 60, 25), group[0].Area);
    }

    [Fact]
    public void AClickOnThePlacedButton_ReturnsItsValue()
    {
        ButtonGroup group = ButtonGroup.CreateStandard(
            Window,
            DialogButton.Ok | DialogButton.Cancel,
            new FakeArtwork(okaySize: new Size(60, 25), cancelSize: new Size(70, 25)));

        group.SetCursor(new Point(220, 140));
        group.SetLeftButton(true);
        Assert.True(group[1].IsPressed);

        // Releasing outside is not a click and leaves neither button pressed.
        group.SetCursor(new Point(10, 10));
        Assert.Equal(DialogButton.None, group.SetLeftButton(false));
        Assert.False(group[1].IsPressed);

        group.SetCursor(new Point(220, 140));
        group.SetLeftButton(true);
        Assert.Equal(DialogButton.Cancel, group.SetLeftButton(false));
    }

    /// <summary>Artwork source that hands out blank sprites of a given size.</summary>
    private sealed class FakeArtwork : IDialogButtonArtworkSource
    {
        private readonly Size _okaySize;
        private readonly Size _cancelSize;
        private readonly Size _yesSize;
        private readonly Size _noSize;
        private readonly Point _okayOffset;

        public FakeArtwork(Size? okaySize = null, Size? cancelSize = null, Size? yesSize = null, Size? noSize = null,
            Point? okayOffset = null)
        {
            _okaySize = okaySize ?? new Size(60, 25);
            _cancelSize = cancelSize ?? new Size(60, 25);
            _yesSize = yesSize ?? new Size(60, 25);
            _noSize = noSize ?? new Size(60, 25);
            _okayOffset = okayOffset ?? Point.Zero;
        }

        public DialogButtonArtwork Get(DialogButton button)
        {
            Size size = button switch
            {
                DialogButton.Ok => _okaySize,
                DialogButton.Cancel => _cancelSize,
                DialogButton.Yes => _yesSize,
                _ => _noSize,
            };

            Point offset = button == DialogButton.Ok ? _okayOffset : Point.Zero;

            return new DialogButtonArtwork(
                new Sprite(new PixelBuffer(size.Width, size.Height), offset.X, offset.Y, new byte[size.Width * size.Height]),
                new Sprite(new PixelBuffer(size.Width, size.Height), offset.X, offset.Y, new byte[size.Width * size.Height]));
        }
    }
}
