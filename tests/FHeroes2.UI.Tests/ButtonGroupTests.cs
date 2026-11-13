using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

public sealed class ButtonGroupTests
{
    private const byte ReleasedColor = 5;
    private const byte PressedColor = 7;

    [Fact]
    public void CreateButton_AddsTheButtonAndItsValue()
    {
        ButtonGroup group = CreateGroup(out ButtonSprite okay, out ButtonSprite cancel);

        Assert.Equal(2, group.Count);
        Assert.Same(okay, group[0]);
        Assert.Same(cancel, group[1]);
        Assert.Equal(DialogButton.Ok, group.ValueOf(0));
        Assert.Equal(DialogButton.Cancel, group.ValueOf(1));
        Assert.Equal(new Rect(10, 10, 20, 10), okay.Area);
        Assert.Equal(new Rect(40, 10, 20, 10), cancel.Area);
    }

    [Fact]
    public void Click_ReturnsTheValueWhenPressAndReleaseAreInside()
    {
        ButtonGroup group = CreateGroup(out ButtonSprite okay, out _);

        Assert.Equal(DialogButton.Ok, Click(group, new Point(15, 15), new Point(15, 15)));
        Assert.False(okay.IsPressed);
    }

    [Fact]
    public void Click_IsRejectedWhenTheReleaseIsOutside()
    {
        // MouseClickLeft( area ) wants both ends of the gesture inside: a drag that leaves the button
        // and is released elsewhere is not a click.
        ButtonGroup group = CreateGroup(out ButtonSprite okay, out _);

        Assert.Equal(DialogButton.None, Click(group, new Point(15, 15), new Point(100, 100)));
        Assert.False(okay.IsPressed);
    }

    [Fact]
    public void Click_IsRejectedWhenThePressIsOutside()
    {
        ButtonGroup group = CreateGroup(out ButtonSprite okay, out _);

        Assert.Equal(DialogButton.None, Click(group, new Point(100, 100), new Point(15, 15)));
        Assert.False(okay.IsPressed);
    }

    [Fact]
    public void Click_ReturnsTheValueOfTheSecondButton()
    {
        ButtonGroup group = CreateGroup(out _, out _);

        Assert.Equal(DialogButton.Cancel, Click(group, new Point(45, 15), new Point(45, 15)));
    }

    [Fact]
    public void PressedLook_FollowsTheButtonThePressStartedOn()
    {
        ButtonGroup group = CreateGroup(out ButtonSprite okay, out ButtonSprite cancel);

        group.SetCursor(new Point(15, 15));
        group.SetLeftButton(true);

        Assert.True(okay.IsPressed);
        Assert.False(cancel.IsPressed);

        // Dragging onto the neighbour does not light it up: its area never saw the press.
        group.SetCursor(new Point(45, 15));
        Assert.False(okay.IsPressed);
        Assert.False(cancel.IsPressed);

        // Walking back restores the pressed look, like drawOnState( isMouseLeftButtonPressedAndHeldInArea ).
        group.SetCursor(new Point(15, 15));
        Assert.True(okay.IsPressed);

        group.SetLeftButton(false);
        Assert.False(okay.IsPressed);
    }

    [Fact]
    public void DisabledGroup_IgnoresTheMouseUntilItIsEnabledAgain()
    {
        ButtonGroup group = CreateGroup(out ButtonSprite okay, out _);
        group.Disable();

        Assert.False(okay.IsEnabled);
        Assert.Equal(DialogButton.None, Click(group, new Point(15, 15), new Point(15, 15)));
        Assert.False(okay.IsPressed);

        group.Enable();
        Assert.True(okay.IsEnabled);
        Assert.Equal(DialogButton.Ok, Click(group, new Point(15, 15), new Point(15, 15)));
    }

    [Fact]
    public void HotKey_AnswersTheMatchingButtonOfTheGroup()
    {
        ButtonGroup group = new();
        group.CreateButton(0, 0, CreateSprite(10, 10), CreateSprite(10, 10), DialogButton.Yes);
        group.CreateButton(20, 0, CreateSprite(10, 10), CreateSprite(10, 10), DialogButton.No);

        Assert.Equal(DialogButton.Yes, group.ProcessHotKey(ButtonHotKey.Okay));
        Assert.Equal(DialogButton.No, group.ProcessHotKey(ButtonHotKey.Cancel));
    }

    [Fact]
    public void HotKey_SingleOkayOrCancelButtonAcceptsEitherKey()
    {
        // Game::HotKeyCloseWindow(): a dialog with one OK or CANCEL button closes with both keys.
        ButtonGroup group = new();
        group.CreateButton(0, 0, CreateSprite(10, 10), CreateSprite(10, 10), DialogButton.Ok);

        Assert.Equal(DialogButton.Ok, group.ProcessHotKey(ButtonHotKey.Okay));
        Assert.Equal(DialogButton.Ok, group.ProcessHotKey(ButtonHotKey.Cancel));
    }

    [Fact]
    public void HotKey_SingleYesButtonOnlyAnswersTheOkayKey()
    {
        ButtonGroup group = new();
        group.CreateButton(0, 0, CreateSprite(10, 10), CreateSprite(10, 10), DialogButton.Yes);

        Assert.Equal(DialogButton.Yes, group.ProcessHotKey(ButtonHotKey.Okay));
        Assert.Equal(DialogButton.None, group.ProcessHotKey(ButtonHotKey.Cancel));
    }

    [Fact]
    public void HotKey_SkipsDisabledButtons()
    {
        ButtonGroup group = CreateGroup(out ButtonSprite okay, out ButtonSprite cancel);
        okay.Disable();

        Assert.True(cancel.IsEnabled);
        Assert.Equal(DialogButton.None, group.ProcessHotKey(ButtonHotKey.Okay));
        Assert.Equal(DialogButton.Cancel, group.ProcessHotKey(ButtonHotKey.Cancel));
    }

    [Fact]
    public void Draw_PaintsTheButtonsInTheOrderTheyWereAdded()
    {
        ButtonGroup group = new();
        group.CreateButton(0, 0, CreateSprite(4, 4, 11), CreateSprite(4, 4, 11), DialogButton.Ok);
        group.CreateButton(2, 2, CreateSprite(4, 4, 12), CreateSprite(4, 4, 12), DialogButton.Cancel);

        PixelBuffer target = new(8, 8);
        group.Draw(target);

        Assert.Equal(11, target[0, 0]);
        Assert.Equal(12, target[2, 2]);
        Assert.Equal(12, target[5, 5]);
    }

    [Fact]
    public void Draw_UsesThePressedLookWhileTheButtonIsHeld()
    {
        ButtonGroup group = new();
        ButtonSprite button = group.CreateButton(
            0, 0, CreateSprite(4, 4, ReleasedColor), CreateSprite(4, 4, PressedColor), DialogButton.Ok);

        PixelBuffer target = new(8, 8);
        group.SetCursor(new Point(1, 1));
        group.SetLeftButton(true);
        group.Draw(target);

        Assert.True(button.IsPressed);
        Assert.Equal(PressedColor, target[1, 1]);
        Assert.Equal(PressedColor, target[3, 3]);
    }

    private static ButtonGroup CreateGroup(out ButtonSprite okay, out ButtonSprite cancel)
    {
        ButtonGroup group = new();

        okay = group.CreateButton(
            10, 10, CreateSprite(20, 10), CreateSprite(20, 10), DialogButton.Ok);
        cancel = group.CreateButton(
            40, 10, CreateSprite(20, 10), CreateSprite(20, 10), DialogButton.Cancel);

        return group;
    }

    private static DialogButton Click(ButtonGroup group, Point press, Point release)
    {
        group.SetCursor(press);
        group.SetLeftButton(true);
        group.SetCursor(release);

        return group.SetLeftButton(false);
    }

    private static Sprite CreateSprite(int width, int height, byte color = ReleasedColor)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);
        return new Sprite(image);
    }
}
