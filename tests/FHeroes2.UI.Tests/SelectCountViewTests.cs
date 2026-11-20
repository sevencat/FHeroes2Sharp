using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The <c>Dialog::SelectCount()</c> window (<c>dialog_selectcount.cpp:79-131</c>): a
/// <see cref="FrameBox"/> with a header, a <see cref="ValueSelectionElement"/> at 38 pixels from the
/// left of the text area and the OK/CANCEL row.
/// </summary>
public sealed class SelectCountViewTests
{
    private const byte Canvas = 0;
    private const byte FrameColor = 33;
    private const byte ButtonColor = 44;

    private static readonly Size EditBoxSize = new(68, 19);
    private static readonly Size ArrowSize = new(20, 10);

    [Fact]
    public void TryCreate_LaysOutTheWindow()
    {
        SelectCountView view = CreateView();

        // The frame is the 244 pixel wide text area of BUYBUILD.ICN: one 15 pixel line of the fake font,
        // the 10 + 30 of the layout and the 40 pixel button row, against a 70 pixel minimum.
        Assert.Equal(FrameBox.AreaWidth, view.Area.Width);

        int content = 15 + SelectCountView.ElementOffset + SelectCountView.SelectionAreaHeight +
                      FrameBox.ButtonAreaHeight;
        Assert.Equal(Math.Max(FrameBox.MinimumAreaHeight, content), view.Area.Height);

        Assert.Equal(view.Area.X + SelectCountView.SelectionInset, view.Value.Area.X);
        Assert.Equal(view.Area.Y + 15 + SelectCountView.ElementOffset, view.Value.Area.Y);
        Assert.Equal(new Size(94, 25), view.Value.Area.Size);
    }

    [Fact]
    public void TryCreate_ReadsTheStepOfTheDialog()
    {
        SelectCountDialog dialog = SelectCountDialog.Create("Pick", 0, 100, 7);
        SelectCountView view = SelectCountView.TryCreate(
            dialog, CreateChrome(), new FixedArtwork(), CreateFont(), CreateTownWindowFrames())!;

        Assert.Equal(7, view.Value.Step);
    }

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheNumberArtwork()
    {
        SelectCountDialog dialog = SelectCountDialog.Create("Pick", 0, 5);

        Assert.Null(SelectCountView.TryCreate(
            dialog, CreateChrome(), new FixedArtwork(), CreateFont(), []));
    }

    [Fact]
    public void TryCreate_RejectsMissingArguments()
    {
        SelectCountDialog dialog = SelectCountDialog.Create("Pick", 0, 5);

        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            null!, CreateChrome(), new FixedArtwork(), CreateFont(), CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, null!, new FixedArtwork(), CreateFont(), CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, CreateChrome(), null!, CreateFont(), CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, CreateChrome(), new FixedArtwork(), null!, CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, CreateChrome(), new FixedArtwork(), CreateFont(), null!));
    }

    [Fact]
    public void SetLeftButton_OkWritesTheChosenNumber()
    {
        SelectCountView view = CreateView(initialValue: 3);
        Point okay = Center(view.Buttons[0].Area);

        Assert.Equal(DialogButton.None, view.SetLeftButton(true, okay));
        Assert.Equal(DialogButton.Ok, view.SetLeftButton(false, okay));

        Assert.Equal(3, view.Dialog.Result);
    }

    [Fact]
    public void SetLeftButton_CancelLeavesNoNumber()
    {
        SelectCountView view = CreateView(initialValue: 3);
        Point cancel = Center(view.Buttons[1].Area);

        view.SetLeftButton(true, cancel);
        Assert.Equal(DialogButton.Cancel, view.SetLeftButton(false, cancel));

        Assert.Null(view.Dialog.Result);
    }

    [Fact]
    public void SetLeftButton_ArrowThenOkReportsTheChangedNumber()
    {
        SelectCountView view = CreateView(initialValue: 3);
        Point up = Center(view.Value.UpButton.Area);

        view.SetLeftButton(true, up);
        view.SetLeftButton(false, up);

        Point okay = Center(view.Buttons[0].Area);
        view.SetLeftButton(true, okay);
        view.SetLeftButton(false, okay);

        Assert.Equal(4, view.Dialog.Result);
    }

    [Fact]
    public void SetLeftButton_IgnoresTheHeaderArea()
    {
        SelectCountView view = CreateView();

        Assert.Equal(DialogButton.None, view.SetLeftButton(true, new Point(view.Area.X + 5, view.Area.Y + 5)));
        Assert.Equal(DialogButton.None, view.SetLeftButton(false, new Point(view.Area.X + 5, view.Area.Y + 5)));
        Assert.Null(view.Dialog.Result);
    }

    [Fact]
    public void Draw_PaintsTheFrameAndTheButtons()
    {
        SelectCountView view = CreateView();
        PixelBuffer target = new(640, 480);

        view.Draw(target);

        Assert.Equal(FrameColor, target[view.FrameArea.X + 2, view.FrameArea.Y + 2]);
        Assert.Equal(ButtonColor, target[Center(view.Buttons[0].Area).X, Center(view.Buttons[0].Area).Y]);
        Assert.Equal(Canvas, target[0, 0]);
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        Assert.Throws<ArgumentNullException>(() => CreateView().Draw(null!));
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static SelectCountView CreateView(int initialValue = 2) =>
        SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick a number", 1, 5), CreateChrome(), new FixedArtwork(), CreateFont(),
            CreateTownWindowFrames(), initialValue)!;

    private static FrameBoxChrome CreateChrome()
    {
        // BUYBUILD.ICN is two 160 pixel wide columns; the middle pieces repeat.
        Sprite[] frames = new Sprite[FrameBoxChrome.RequiredSpriteCount];

        for (int index = 0; index < frames.Length; ++index)
        {
            frames[index] = new Sprite(Filled(new Size(160, index % 3 == 1 ? 35 : 99), FrameColor));
        }

        return FrameBoxChrome.TryCreate(frames)!;
    }

    /// <summary>TOWNWIND.ICN frames 4-8 with their real sizes.</summary>
    private static IReadOnlyList<Sprite> CreateTownWindowFrames()
    {
        Sprite[] frames = new Sprite[ValueSelectionElement.RequiredFrameCount];
        frames[ValueSelectionElement.EditBoxFrame] = new Sprite(Filled(EditBoxSize, FrameColor));

        for (int index = ValueSelectionElement.UpReleasedFrame; index < frames.Length; ++index)
        {
            frames[index] = new Sprite(Filled(ArrowSize, ButtonColor), 3, 2);
        }

        return frames;
    }

    private static PixelBuffer Filled(Size size, byte color)
    {
        PixelBuffer buffer = new(size.Width, size.Height);
        buffer.Fill(color);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new BlockGlyphSource();

    /// <summary>OK and CANCEL in flat colours, so the drawn buttons can be found by pixel.</summary>
    private sealed class FixedArtwork : IDialogButtonArtworkSource
    {
        public DialogButtonArtwork Get(DialogButton button) =>
            new(new Sprite(Filled(new Size(60, 25), ButtonColor)), new Sprite(Filled(new Size(60, 25), 45)));
    }

    /// <summary>A 6x15 solid block font, which is enough for the header and the number.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = new(Filled(new Size(6, 15), ButtonColor));

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
