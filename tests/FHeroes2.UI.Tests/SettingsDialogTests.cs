using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The settings dialog: the row layout, the value cycling and the button interaction.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
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
        Assert.True(DrawWithTheWindowArtwork(dialog, target));

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
        Assert.True(DrawWithTheWindowArtwork(dialog, target));

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
        Assert.True(DrawWithTheWindowArtwork(dialog, target));
    }

    [Fact]
    public void LongValue_DropsTheChevrons()
    {
        // Forty glyphs of six pixels are 240 px wide: no room left for the arrows in a 200 px row.
        SettingsDialog dialog = CreateDialog(["Language"], out _, values: [new string('W', 40)]);

        Assert.True(dialog.LeftChevron(0).IsEmpty);
        Assert.True(dialog.RightChevron(0).IsEmpty);
    }

    [Fact]
    public void LongText_StaysInsideTheRowPaddings()
    {
        // A translated label or value can be wider than the window; both are clipped at the
        // paddings, so the frame and the space next to it keep their own pixels.
        SettingsDialog dialog = CreateDialog(
            [new string('W', 40)], out _, values: [new string('W', 40)]);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        Assert.True(DrawWithTheWindowArtwork(dialog, target));

        Rect row = dialog.OptionArea(0);
        for (int y = row.Y; y < row.Bottom; ++y)
        {
            for (int x = row.X; x < row.X + SettingsDialog.RowPadding; ++x)
            {
                Assert.NotEqual((byte)2, target[x, y]);
            }

            for (int x = row.Right - SettingsDialog.RowPadding; x < row.Right; ++x)
            {
                Assert.NotEqual((byte)2, target[x, y]);
            }
        }

        // The value is pushed back to the padding instead of running over the label, so the first
        // pixel of the text band is painted.
        Assert.Equal((byte)2, target[row.X + SettingsDialog.RowPadding, row.Y]);
    }

    [Fact]
    public void Option_RejectsAnUnknownRow()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);

        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.Option(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.OptionArea(-1));
    }

    [Fact]
    public void Show_ThrowsWhenNoEngineIsAttached()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out _);

        Assert.Throws<InvalidOperationException>(() =>
            dialog.Show(new PixelBuffer(640, 480), new LocalEvent(() => 0)));
    }

    [Fact]
    public void Show_ReturnsOkWhenTheEngineClicksIt()
    {
        SettingsDialog dialog = CreateDialog(["Language"], out Rect okayArea);
        Point centre = new(okayArea.X + (okayArea.Width / 2), okayArea.Y + (okayArea.Height / 2));

        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            LocalEvent events = new(() => 0);
            FakeEngine engine = new()
            {
                OnFrame = (state, frame) =>
                {
                    if (frame == 0)
                    {
                        state.OnMouseMotion(centre);
                        state.OnMouseButton(true, MouseButtonType.Left, centre);
                    }
                    else
                    {
                        state.OnMouseButton(false, MouseButtonType.Left, centre);
                    }
                },
            };

            events.Engine = engine;

            Assert.Equal(DialogButton.Ok, dialog.Show(new PixelBuffer(640, 480), events));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    /// <summary>An event engine that plays a scripted frame at a time and stops after a few of them.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public int Frames { get; private set; }

        public Action<LocalEvent, int>? OnFrame { get; set; }

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (Frames >= 8)
            {
                return false;
            }

            OnFrame?.Invoke(state, Frames++);

            return true;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    /// <summary>
    /// Builds a dialog with a content area of the given width, one row per label and a 60x25 OK
    /// button. Every row offers the same values, two short ones unless the caller passes others.
    /// </summary>
    private static SettingsDialog CreateDialog(
        string[] labels, out Rect okayArea, int width = 200, string[]? values = null)
    {
        IGlyphSource titleFont = new FakeGlyphSource(1);
        IGlyphSource optionFont = new FakeGlyphSource(2);

        Rect content = new(Content.X, Content.Y, width, Content.Height);
        StandardWindow window = StandardWindow.Create(content);

        ButtonGroup buttons = ButtonGroup.CreateStandard(content, DialogButton.Ok, new FixedArtwork());

        string[] rowValues = values ?? ["English", "zh_CN"];
        SettingsOption[] options = [.. labels.Select(label => new SettingsOption(label, rowValues))];

        SettingsDialog dialog = new(window, buttons, titleFont, optionFont, "Settings", options);

        okayArea = buttons[0].Area;

        return dialog;
    }

    /// <summary>Draws the dialog with the real window artwork.</summary>
    /// <param name="dialog">Dialog to paint.</param>
    /// <param name="target">Frame buffer to paint into.</param>
    /// <returns><see langword="false"/> when the machine has no game data.</returns>
    private static bool DrawWithTheWindowArtwork(SettingsDialog dialog, PixelBuffer target)
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return false;
        }

        GameAssets.Current = assets;

        try
        {
            dialog.Draw(target);
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
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
