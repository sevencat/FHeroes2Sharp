using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
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
    private const byte FrameColor = 7;
    private const byte IconColor = 9;
    private const byte TextColor = 60;

    private static readonly IGlyphSource SmallFont = new BlockFont(11, 5, TextColor);

    /// <summary>A font whose glyphs are solid five pixel blocks, with the metrics of <c>FontType::smallWhite()</c>.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte color)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight { get; }

        public int SpaceWidth { get; }

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }

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

    [Fact]
    public void Artifact_IsTheSizeOfItsFrameAndPutsTheIconSixPixelsInside()
    {
        Sprite frame = new(82, 82);
        frame.Image.Fill(FrameColor);

        Sprite icon = new(64, 64);
        icon.Image.Fill(IconColor);

        var element = new ArtifactDialogElement(frame, icon, "Sword of Judgement", "It judges.");

        Assert.Equal(new Size(82, 82), element.Area);

        PixelBuffer output = new(120, 120);

        element.Draw(output, new Point(10, 20));

        Assert.Equal(FrameColor, output[10, 20]);
        Assert.Equal(IconColor, output[10 + 6, 20 + 6]);
        Assert.Equal(IconColor, output[10 + 6 + 63, 20 + 6 + 63]);

        // The icon stops six pixels short of the frame on both sides.
        Assert.Equal(FrameColor, output[10 + 80, 20 + 80]);
    }

    [Fact]
    public void ArtifactRightClick_AsksForTheNameAndTheDescription()
    {
        var element = new ArtifactDialogElement(new Sprite(82, 82), new Sprite(64, 64), "Sword of Judgement",
            "It judges.");

        LocalEvent events = new(() => 0);
        events.OnMouseMotion(new Point(40, 40));
        events.OnMouseButton(true, MouseButtonType.Right, new Point(40, 40));

        DialogElementResult result = element.ProcessEvents(events, new Point(10, 20));

        Assert.NotNull(result.Popup);
        Assert.Equal("Sword of Judgement", result.Popup!.Value.Header);
        Assert.Equal("It judges.", result.Popup.Value.Body);
        Assert.False(result.RequiresRedraw);
    }

    [Fact]
    public void ArtifactLeftClick_DoesNothing()
    {
        var element = new ArtifactDialogElement(new Sprite(82, 82), new Sprite(64, 64), "Sword of Judgement",
            "It judges.");

        LocalEvent events = new(() => 0);
        events.OnMouseMotion(new Point(40, 40));
        events.OnMouseButton(true, MouseButtonType.Left, new Point(40, 40));

        Assert.Null(element.ProcessEvents(events, new Point(10, 20)).Popup);
    }

    [Fact]
    public void Spell_IsAsWideAsTheLabelAndAddsItsHeightUnderTheIcon()
    {
        Spell spell = new(SpellType.Bless);
        var element = new SpellDialogElement(new Sprite(23, 18), spell, 6, SmallFont);

        // Nine characters of a five pixel font: "Bless [6]" without the quotes.
        Assert.Equal($"{spell.GetName()} [6]", element.Label);
        Assert.Equal(new Size(45, 18 + 2 + 11), element.Area);
    }

    [Fact]
    public void Spell_WiderThanItsIconKeepsTheIconWider()
    {
        Spell spell = new(SpellType.Bless);
        var element = new SpellDialogElement(new Sprite(80, 18), spell, 0, SmallFont);

        // A free spell hides the brackets, and the icon is wider than the name, so the icon sets the width.
        Assert.Equal(spell.GetName(), element.Label);
        Assert.Equal(new Size(80, 18 + 2 + 11), element.Area);
    }

    [Fact]
    public void Spell_DrawsTheIconCentredOverTheLabel()
    {
        Sprite icon = new(23, 18);
        icon.Image.Fill(IconColor);

        var element = new SpellDialogElement(icon, new Spell(SpellType.Bless), 6, SmallFont);

        PixelBuffer output = new(80, 60);
        element.Draw(output, new Point(5, 5));

        // The 45 pixel label is the widest part, so the 23 pixel icon starts eleven pixels into it.
        Assert.Equal(IconColor, output[5 + 11, 5]);
        Assert.Equal(0, output[5 + 10, 5]);

        // The label sits two pixels under the icon.
        Assert.Equal(TextColor, output[5, 5 + 18 + 2]);
    }

    [Fact]
    public void SpellRightClick_AsksForTheNameAndTheDescription()
    {
        Spell spell = new(SpellType.Bless);
        var element = new SpellDialogElement(new Sprite(23, 18), spell, 6, SmallFont);

        LocalEvent events = new(() => 0);
        events.OnMouseMotion(new Point(10, 10));
        events.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));

        DialogElementResult result = element.ProcessEvents(events, new Point(5, 5));

        Assert.NotNull(result.Popup);
        Assert.Equal(spell.GetName(), result.Popup!.Value.Header);
        Assert.Equal(spell.GetDescription(), result.Popup.Value.Body);
    }

    [Fact]
    public void Spell_RejectsMissingSpritesAndFonts()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SpellDialogElement(null!, new Spell(SpellType.Bless), 6, SmallFont));
        Assert.Throws<ArgumentNullException>(() =>
            new SpellDialogElement(new Sprite(23, 18), new Spell(SpellType.Bless), 6, null!));
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
