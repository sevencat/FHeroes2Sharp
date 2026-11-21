using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The <c>Dialog::SelectCount()</c> window (<c>dialog_selectcount.cpp:79-131</c>): a
/// <see cref="FrameBox"/> with a header, a <see cref="ValueSelectionElement"/> at 38 pixels from the
/// left of the text area and the OK/CANCEL row.
/// </summary>
/// <remarks>
/// The frame reads <c>BUYBUILD.ICN</c> itself, so the tests that build a window need the installed
/// archives and return when the machine has none.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class SelectCountViewTests
{
    private const byte Canvas = 0;
    private const byte FrameColor = 33;
    private const byte ButtonColor = 44;

    private static readonly Size EditBoxSize = new(68, 19);
    private static readonly Size ArrowSize = new(20, 10);

    [Fact]
    public void TryCreate_LaysOutTheWindow() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            // The frame is the 244 pixel wide text area of BUYBUILD.ICN: one 15 pixel line of the fake font,
            // the 10 + 30 of the layout and the 40 pixel button row, against a 70 pixel minimum.
            Assert.Equal(FrameBox.AreaWidth, view.Area.Width);

            int content = 15 + SelectCountView.ElementOffset + SelectCountView.SelectionAreaHeight +
                          FrameBox.ButtonAreaHeight;
            Assert.Equal(Math.Max(FrameBox.MinimumAreaHeight, content), view.Area.Height);

            Assert.Equal(view.Area.X + SelectCountView.SelectionInset, view.Value.Area.X);
            Assert.Equal(view.Area.Y + 15 + SelectCountView.ElementOffset, view.Value.Area.Y);
            Assert.Equal(new Size(94, 25), view.Value.Area.Size);
        }));

    [Fact]
    public void TryCreate_ReadsTheStepOfTheDialog() => Assert.True(TryWithArchives(
        () => SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick", 0, 100, 7), new FixedArtwork(), CreateFont(),
            CreateTownWindowFrames())!,
        view => Assert.Equal(7, view.Value.Step)));

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheNumberArtwork()
    {
        Assert.Null(SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick", 0, 5), new FixedArtwork(), CreateFont(), []));
    }

    [Fact]
    public void TryCreate_RejectsMissingArguments()
    {
        SelectCountDialog dialog = SelectCountDialog.Create("Pick", 0, 5);

        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            null!, new FixedArtwork(), CreateFont(), CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, null!, CreateFont(), CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, new FixedArtwork(), null!, CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, new FixedArtwork(), CreateFont(), null!));
    }

    [Fact]
    public void SetLeftButton_OkWritesTheChosenNumber() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 3),
        view =>
        {
            Point okay = Center(view.Buttons[0].Area);

            Assert.Equal(DialogButton.None, view.SetLeftButton(true, okay));
            Assert.Equal(DialogButton.Ok, view.SetLeftButton(false, okay));

            Assert.Equal(3, view.Dialog.Result);
        }));

    [Fact]
    public void SetLeftButton_CancelLeavesNoNumber() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 3),
        view =>
        {
            Point cancel = Center(view.Buttons[1].Area);

            view.SetLeftButton(true, cancel);
            Assert.Equal(DialogButton.Cancel, view.SetLeftButton(false, cancel));

            Assert.Null(view.Dialog.Result);
        }));

    [Fact]
    public void SetLeftButton_ArrowThenOkReportsTheChangedNumber() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 3),
        view =>
        {
            Point up = Center(view.Value.UpButton.Area);

            view.SetLeftButton(true, up);
            view.SetLeftButton(false, up);

            Point okay = Center(view.Buttons[0].Area);
            view.SetLeftButton(true, okay);
            view.SetLeftButton(false, okay);

            Assert.Equal(4, view.Dialog.Result);
        }));

    [Fact]
    public void SetLeftButton_IgnoresTheHeaderArea() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            Assert.Equal(DialogButton.None,
                view.SetLeftButton(true, new Point(view.Area.X + 5, view.Area.Y + 5)));
            Assert.Equal(DialogButton.None,
                view.SetLeftButton(false, new Point(view.Area.X + 5, view.Area.Y + 5)));
            Assert.Null(view.Dialog.Result);
        }));

    [Fact]
    public void Draw_PaintsTheFrameAndTheButtons() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            PixelBuffer target = new(640, 480);

            view.Draw(target);

            // The frame artwork is opaque in the body of its columns but jagged along the outer edges,
            // so the test counts the pixels it paints instead of picking one.
            int painted = 0;

            for (int y = view.FrameArea.Y; y < view.FrameArea.Bottom; ++y)
            {
                for (int x = view.FrameArea.X; x < view.FrameArea.Right; ++x)
                {
                    painted += target[x, y] != Canvas ? 1 : 0;
                }
            }

            Assert.True(painted > 10_000, $"the frame only painted {painted} pixels");

            Point okay = Center(view.Buttons[0].Area);
            Assert.Equal(ButtonColor, target[okay.X, okay.Y]);
            Assert.Equal(Canvas, target[0, 0]);
        }));

    [Fact]
    public void Draw_RejectsAMissingTarget() => Assert.True(TryWithArchives(
        () => CreateView(),
        view => Assert.Throws<ArgumentNullException>(() => view.Draw(null!))));

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static SelectCountView CreateView(int initialValue = 2) =>
        SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick a number", 1, 5), new FixedArtwork(), CreateFont(),
            CreateTownWindowFrames(), initialValue)!;

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

    /// <summary>Opens the game data the message frame is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Runs a test against the real frame artwork.</summary>
    /// <param name="create">Builds the window.</param>
    /// <param name="assert">Checks it.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArchives(Func<SelectCountView> create, Action<SelectCountView> assert)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            assert(create());
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }

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
