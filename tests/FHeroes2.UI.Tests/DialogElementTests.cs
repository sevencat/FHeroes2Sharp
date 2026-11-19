using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The elements a message box can be built from, <c>fheroes2::DialogElement</c> and its first subclasses
/// (<c>src/fheroes2/gui/ui_dialog.h:56-163</c>).
/// </summary>
/// <remarks>
/// Only the paths that do not need the bitmap fonts are exercised here: text measurement lives in
/// <c>FHeroes2.Game.Gui.Text</c> and needs the real archive, which the UI tests deliberately do not open. The
/// layout of the text itself is pinned by the game tests.
/// </remarks>
public sealed class DialogElementTests
{
    [Fact]
    public void CustomImage_IsAsBigAsTheImageAndCopiesItWithoutItsOffset()
    {
        Sprite image = new(20, 12);

        for (int y = 0; y < image.Height; ++y)
        {
            image.Image.Row(y).Fill((byte)(y + 1));
        }

        var element = new CustomImageDialogElement(image);

        Assert.Equal(new Size(20, 12), element.Area);

        PixelBuffer output = new(40, 30);

        element.Draw(output, new Point(5, 7));

        for (int y = 0; y < image.Height; ++y)
        {
            Assert.Equal((byte)(y + 1), output.Row(7 + y)[5]);
            Assert.Equal(0, output.Row(7 + y)[4]);
        }
    }

    [Fact]
    public void ResourceWithoutQuantity_IsExactlyTheSizeOfTheIcon()
    {
        Sprite icon = new(30, 18);

        var element = new ResourceDialogElement(icon, string.Empty, "Gold", "The most common resource.");

        Assert.Equal(new Size(30, 18), element.Area);

        PixelBuffer output = new(60, 40);
        element.Draw(output, new Point(3, 4));

        // The icon is drawn at the offset as is, the pixel itself comes from the sprite.
        Assert.Equal(icon.Image.Row(0)[0], output.Row(4)[3]);
    }

    [Fact]
    public void ResourceRightClick_AsksForTheResourcePopup()
    {
        Sprite icon = new(30, 18);

        var element = new ResourceDialogElement(icon, string.Empty, "Gold", "The most common resource.");

        LocalEvent events = new(() => 0);
        events.OnMouseMotion(new Point(10, 10));
        events.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));

        DialogElementResult result = element.ProcessEvents(events, new Point(0, 0));

        Assert.False(result.RequiresRedraw);
        Assert.NotNull(result.Popup);
        Assert.Equal("Gold", result.Popup!.Value.Header);
        Assert.Equal("The most common resource.", result.Popup.Value.Body);
    }

    [Fact]
    public void ResourceLeftClick_DoesNothing()
    {
        Sprite icon = new(30, 18);

        var element = new ResourceDialogElement(icon, string.Empty, "Gold", "The most common resource.");

        LocalEvent events = new(() => 0);
        events.OnMouseMotion(new Point(10, 10));
        events.OnMouseButton(true, MouseButtonType.Left, new Point(10, 10));

        DialogElementResult result = element.ProcessEvents(events, new Point(0, 0));

        Assert.Null(result.Popup);
        Assert.False(result.RequiresRedraw);
    }

    [Fact]
    public void ResourceRightClickOutsideTheElement_DoesNothing()
    {
        Sprite icon = new(30, 18);

        var element = new ResourceDialogElement(icon, string.Empty, "Gold", "The most common resource.");

        LocalEvent events = new(() => 0);
        events.OnMouseMotion(new Point(10, 10));
        events.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));

        // The same click, but the layout put the element far away from the cursor.
        DialogElementResult result = element.ProcessEvents(events, new Point(100, 100));

        Assert.Null(result.Popup);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OptionItem_RejectsAnImpossibleTextWidth(int textMaxWidth)
    {
        Sprite icon = new(30, 30);
        PixelBuffer output = new(200, 120);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OptionItem.Draw(output, new Rect(10, 30, 30, 30), icon, "Audio", "Settings", textMaxWidth));
    }

    [Fact]
    public void OptionItem_UsesTheReferenceRowWidths()
    {
        // UiOptionTextWidth::TWO_ELEMENTS_ROW / THREE_ELEMENTS_ROW ( ui_option_item.h:32-36 ).
        Assert.Equal(113, OptionItem.TwoElementsRowWidth);
        Assert.Equal(87, OptionItem.ThreeElementsRowWidth);
        Assert.Equal(12, OptionItem.TitleVerticalOffset);
        Assert.Equal(6, OptionItem.ValueVerticalOffset);
    }
}
