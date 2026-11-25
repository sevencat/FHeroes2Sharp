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

    [Fact]
    public void CustomAnimation_IsTheSizeOfTheStaticImageAndDrawsTheFirstFrameOverIt()
    {
        Sprite background = new(30, 20);
        background.Image.Fill(FrameColor);

        Sprite first = new(8, 6, 2, 3);
        first.Image.Fill(IconColor);

        var element = new CustomAnimationDialogElement(background, new Point(4, 5),
            [first, new Sprite(1, 1)], firstFrame: 0, frameCount: 1, TimeSpan.FromMilliseconds(75));

        Assert.Equal(new Size(30, 20), element.Area);
        Assert.Equal(0, element.FrameNumber);

        PixelBuffer output = new(60, 50);

        element.Draw(output, new Point(10, 10));

        // The static image covers the element.
        Assert.Equal(FrameColor, output[10, 10]);
        Assert.Equal(FrameColor, output[10 + 29, 10 + 19]);

        // The frame sits at the animation position plus its own offset.
        Assert.Equal(IconColor, output[10 + 4 + 2, 10 + 5 + 3]);
        Assert.Equal(IconColor, output[10 + 4 + 2 + 7, 10 + 5 + 3 + 5]);
    }

    [Fact]
    public void CustomAnimation_AdvancesOneFramePerDelayAndRepaints()
    {
        Sprite background = new(10, 10);
        background.Image.Fill(FrameColor);

        Sprite first = new(4, 4);
        first.Image.Fill(IconColor);

        Sprite second = new(4, 4);
        second.Image.Fill(TextColor);

        var element = new CustomAnimationDialogElement(background, new Point(0, 0),
            [first, second], firstFrame: 0, frameCount: 2, TimeSpan.FromMilliseconds(50));

        PixelBuffer output = new(20, 20);
        element.Draw(output, new Point(0, 0));
        Assert.Equal(IconColor, output[0, 0]);

        Assert.True(element.Update(output, new Point(0, 0), TimeSpan.FromMilliseconds(50)));
        Assert.Equal(1, element.FrameNumber);
        Assert.Equal(TextColor, output[0, 0]);

        // Less than a delay later nothing happens.
        Assert.False(element.Update(output, new Point(0, 0), TimeSpan.FromMilliseconds(10)));
        Assert.Equal(1, element.FrameNumber);

        // The cycle wraps around to the first sprite.
        Assert.True(element.Update(output, new Point(0, 0), TimeSpan.FromMilliseconds(50)));
        Assert.Equal(0, element.FrameNumber);
        Assert.Equal(IconColor, output[0, 0]);
    }

    [Fact]
    public void CustomAnimation_RejectsMissingSpritesAndAnImpossibleRange()
    {
        Sprite background = new(10, 10);
        Sprite frame = new(4, 4);
        var duration = TimeSpan.FromMilliseconds(50);

        Assert.Throws<ArgumentNullException>(() =>
            new CustomAnimationDialogElement(null!, new Point(0, 0), [frame], 0, 1, duration));
        Assert.Throws<ArgumentNullException>(() =>
            new CustomAnimationDialogElement(background, new Point(0, 0), null!, 0, 1, duration));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CustomAnimationDialogElement(background, new Point(0, 0), [frame], -1, 1, duration));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CustomAnimationDialogElement(background, new Point(0, 0), [frame], 0, 0, duration));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CustomAnimationDialogElement(background, new Point(0, 0), [frame], 0, 1, TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() =>
            new CustomAnimationDialogElement(background, new Point(0, 0), [frame], 0, 2, duration));
    }

    [Fact]
    public void Monster_IsTheSizeOfThePlateAndComposesTheRaceBackgroundSixPixelsInside()
    {
        Sprite plate = new(40, 44);
        plate.Image.Fill(FrameColor);

        Sprite race = new(20, 24);
        race.Image.Fill(IconColor);

        // The picture carries its own anchor, like the artwork of the archives.
        Sprite portrait = new(10, 12, 3, 4);
        portrait.Image.Fill(TextColor);

        var element = new MonsterDialogElement(plate, race, portrait, "Pikeman", "A sturdy defender.");

        Assert.Equal(new Size(40, 44), element.Area);
        Assert.Equal(12, MonsterDialogElement.PlateFrame);
        Assert.Equal(6, MonsterDialogElement.PortraitOffset);

        PixelBuffer output = new(80, 80);

        element.Draw(output, new Point(10, 10));

        // The plate is the background of the element.
        Assert.Equal(FrameColor, output[10, 10]);
        Assert.Equal(FrameColor, output[10 + 39, 10 + 43]);

        // The race background starts six pixels in, the picture six plus its own anchor.
        Assert.Equal(IconColor, output[10 + 6, 10 + 6]);
        Assert.Equal(IconColor, output[10 + 6 + 19, 10 + 6 + 23]);
        Assert.Equal(TextColor, output[10 + 6 + 3, 10 + 6 + 4]);
        Assert.Equal(TextColor, output[10 + 6 + 3 + 9, 10 + 6 + 4 + 11]);
    }

    [Fact]
    public void Monster_DrawsOverACleanPlateEveryTime()
    {
        Sprite plate = new(20, 20);
        plate.Image.Fill(FrameColor);

        Sprite race = new(6, 6);
        race.Image.Fill(IconColor);

        var element = new MonsterDialogElement(plate, race, null, "Pikeman", "A sturdy defender.");

        PixelBuffer output = new(40, 40);

        element.Draw(output, new Point(0, 0));
        element.Draw(output, new Point(20, 20));

        // The second pass starts from the plate again: the artwork itself was never painted into.
        Assert.Equal(FrameColor, output[20, 20]);
        Assert.Equal(IconColor, output[20 + 6, 20 + 6]);
        Assert.Equal(FrameColor, plate.Image[6, 6]);
    }

    [Fact]
    public void Monster_ClicksAskForTheCreatureInfoAndClicksOutsideDoNothing()
    {
        Sprite plate = new(40, 44);
        var element = new MonsterDialogElement(plate, null, null, "Pikeman", "A sturdy defender.");

        LocalEvent rightClick = new(() => 0);
        rightClick.OnMouseMotion(new Point(10, 10));
        rightClick.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));

        DialogElementResult result = element.ProcessEvents(rightClick, new Point(0, 0));

        Assert.NotNull(result.Popup);
        Assert.Equal("Pikeman", result.Popup!.Value.Header);
        Assert.Equal("A sturdy defender.", result.Popup.Value.Body);
        Assert.False(result.RequiresRedraw);

        LocalEvent leftClick = new(() => 0);
        leftClick.OnMouseMotion(new Point(10, 10));
        leftClick.OnMouseButton(true, MouseButtonType.Left, new Point(10, 10));
        leftClick.OnMouseButton(false, MouseButtonType.Left, new Point(10, 10));

        Assert.NotNull(element.ProcessEvents(leftClick, new Point(0, 0)).Popup);

        // The same click with the element somewhere else.
        Assert.Null(element.ProcessEvents(rightClick, new Point(100, 100)).Popup);
        Assert.Null(element.ProcessEvents(leftClick, new Point(100, 100)).Popup);
    }

    [Fact]
    public void Monster_WithoutPopupTexts_AsksForNothing()
    {
        Sprite plate = new(40, 44);
        var element = new MonsterDialogElement(plate, null, null, string.Empty, string.Empty);

        LocalEvent rightClick = new(() => 0);
        rightClick.OnMouseMotion(new Point(10, 10));
        rightClick.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));

        // 生物资料窗还没转，调用方给两张空文本就表示「点了也不用报弹窗」，画像照常画。
        Assert.Null(element.ProcessEvents(rightClick, new Point(0, 0)).Popup);
    }

    [Fact]
    public void Monster_RejectsAMissingPlate()
    {
        Assert.Throws<ArgumentNullException>(() => new MonsterDialogElement(null!, null, null, "Pikeman", string.Empty));
    }
}
