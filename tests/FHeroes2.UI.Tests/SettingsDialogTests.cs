using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The settings dialog: the row layout, the value cycling and the button interaction.
/// </summary>
public sealed class SettingsDialogTests
{
    private static readonly Rect Content = new(100, 100, 200, 120);

    [Fact]
    public void Rows_AreLaidOutBelowTheTitle()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);

        Assert.Equal(Content.Y + SettingsDialog.TitleOffsetY + 15 + SettingsDialog.RowGap, dialog.OptionArea(0).Y);
        Assert.Equal(15, dialog.OptionArea(0).Height);
        Assert.Equal(Content.Width, dialog.OptionArea(0).Width);
    }

    [Fact]
    public void ClickingTheRightHalf_SelectsTheNextValue()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);
        Rect row = dialog.OptionArea(0);

        dialog.SetCursor(new Point(row.Right - 10, row.Y + 2));
        dialog.SetLeftButton(true);
        dialog.SetLeftButton(false);

        Assert.Equal(1, dialog.Option(0).SelectedIndex);
        Assert.Equal("zh_CN", dialog.Option(0).Value);
    }

    [Fact]
    public void ClickingTheLeftHalf_SelectsThePreviousValue_AndWrapsAround()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);
        Rect row = dialog.OptionArea(0);

        dialog.SetCursor(new Point(row.X + 10, row.Y + 2));
        dialog.SetLeftButton(true);
        dialog.SetLeftButton(false);

        // There are only two values, so stepping back from the first one lands on the last one.
        Assert.Equal(1, dialog.Option(0).SelectedIndex);
    }

    [Fact]
    public void ReleasingOutsideTheRow_DoesNotChangeIt()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);
        Rect row = dialog.OptionArea(0);

        dialog.SetCursor(new Point(row.Right - 10, row.Y + 2));
        dialog.SetLeftButton(true);
        dialog.SetCursor(new Point(row.X + 10, row.Y + 60));
        dialog.SetLeftButton(false);

        Assert.Equal(0, dialog.Option(0).SelectedIndex);
    }

    [Fact]
    public void ClickingOk_ReturnsOk()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out Rect okayArea);
        Point centre = new(okayArea.X + (okayArea.Width / 2), okayArea.Y + (okayArea.Height / 2));

        dialog.SetCursor(centre);
        dialog.SetLeftButton(true);
        dialog.SetCursor(centre);

        Assert.Equal(DialogButton.Ok, dialog.SetLeftButton(false));
    }

    [Fact]
    public void Escape_CancelsTheDialog()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);

        Assert.Equal(DialogButton.Cancel, dialog.Cancel());
    }

    [Fact]
    public void Enter_AnswersThroughTheButtonRow()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);

        Assert.Equal(DialogButton.Ok, dialog.ProcessHotKey(ButtonHotKey.Okay));
    }

    [Fact]
    public void GetRequiredHeight_AddsTheTitleTheRowsAndTheButton()
    {
        SettingsDialog dialog = CreateDialog(["Language", "Interface"], out _);

        int expected = SettingsDialog.TitleOffsetY + 15 + SettingsDialog.RowGap +
            (2 * dialog.RowHeight) + SettingsDialog.RowGap + 25;

        Assert.Equal(expected, dialog.GetRequiredHeight(25));
    }

    [Fact]
    public void Drawing_PaintsTheTitleTheRowsAndTheButton()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out Rect okayArea);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        dialog.Draw(target);

        // The title font is solid colour 1 and the option font solid colour 2, so the regions can be
        // told apart even though every glyph looks the same.
        Rect row = dialog.OptionArea(0);
        Assert.Contains((byte)1, target.Row(dialog.Window.ActiveArea.Y + SettingsDialog.TitleOffsetY).ToArray());
        Assert.Equal((byte)2, target[row.X + SettingsDialog.RowPadding, row.Y]);
    }

    [Fact]
    public void Chevrons_BracketTheValueInsideTheRow()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);
        Rect row = dialog.OptionArea(0);
        Rect left = dialog.LeftChevron(0);
        Rect right = dialog.RightChevron(0);

        // "English" is seven glyphs of six pixels in the fake font.
        const int valueWidth = 7 * 6;

        Assert.Equal(row.Right - SettingsDialog.RowPadding - SettingsDialog.ChevronWidth, right.X);
        Assert.Equal(right.X - SettingsDialog.ChevronGap - valueWidth - SettingsDialog.ChevronGap - SettingsDialog.ChevronWidth, left.X);
        Assert.Equal(SettingsDialog.ChevronWidth, left.Width);
        Assert.Equal(SettingsDialog.ChevronHeight, left.Height);
        Assert.Equal(left.Y, right.Y);
        Assert.Equal(row.Y + ((row.Height - SettingsDialog.ChevronHeight) / 2), left.Y);
    }

    [Fact]
    public void Drawing_PaintsBothChevronsWithTheFontWhite()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        dialog.Draw(target);

        // The tip of '<' is on the left of its area, the tip of '>' on the right; the arms are
        // ChevronWidth - 1 rows above and below the centre.
        Rect left = dialog.LeftChevron(0);
        int centreY = left.Y + (left.Height / 2);
        Assert.Equal(SettingsDialog.ChevronColor, target[left.X, centreY]);
        Assert.Equal(SettingsDialog.ChevronColor, target[left.Right - 1, centreY - (left.Width - 1)]);

        Rect right = dialog.RightChevron(0);
        Assert.Equal(SettingsDialog.ChevronColor, target[right.Right - 1, centreY]);
        Assert.Equal(SettingsDialog.ChevronColor, target[right.X, centreY - (right.Width - 1)]);
    }

    [Fact]
    public void NarrowRow_DropsTheChevrons()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _, width: 40);

        Assert.True(dialog.LeftChevron(0).IsEmpty);
        Assert.True(dialog.RightChevron(0).IsEmpty);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        dialog.Draw(target);
    }

    [Fact]
    public void Option_RejectsAnUnknownRow()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);

        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.Option(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.OptionArea(-1));
    }

    /// <summary>
    /// Builds a dialog with a content area of the given width, one row per label and a 60x25 OK
    /// button.
    /// </summary>
    private static SettingsDialog CreateDialog(string[] labels, out Rect okayArea, int width = 200)
    {
        IGlyphSource titleFont = new FakeGlyphSource(1);
        IGlyphSource optionFont = new FakeGlyphSource(2);

        Rect content = new(Content.X, Content.Y, width, Content.Height);
        StandardWindow window = StandardWindow.Create(CreateChrome(), content);

        ButtonGroup buttons = ButtonGroup.CreateStandard(content, DialogButton.Ok, new FixedArtwork());

        SettingsOption[] options = [.. labels.Select(label => new SettingsOption(label, ["English", "zh_CN"]))];

        SettingsDialog dialog = new(window, buttons, titleFont, optionFont, "Settings", options);

        okayArea = buttons[0].Area;

        return dialog;
    }

    /// <summary>Flat sprites of the sizes the window needs, so drawing the frame does not crash.</summary>
    private static StandardWindowChrome CreateChrome()
    {
        // Sizes of the artwork as read from heroes2.agg.
        Sprite horizontal = new(new PixelBuffer(476, 215));
        Sprite vertical = new(new PixelBuffer(319, 456));
        Sprite background = new(new PixelBuffer(640, 480));

        return StandardWindowChrome.TryCreate([horizontal], [vertical], [background])!;
    }

    private sealed class FixedArtwork : IDialogButtonArtworkSource
    {
        public DialogButtonArtwork Get(DialogButton button) =>
            new(new Sprite(new PixelBuffer(60, 25)), new Sprite(new PixelBuffer(60, 25)));
    }

    /// <summary>A glyph source whose glyphs are solid blocks; the colour tells fonts apart.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
        {
            PixelBuffer image = new(6, 15);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight => 15;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
