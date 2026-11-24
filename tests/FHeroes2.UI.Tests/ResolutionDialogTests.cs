using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The row text of the resolution chooser (<c>dialog_resolution.cpp:61-72</c>): the frame buffer size, then the window
/// size or the magnification. The row of the current window starts selected, the same
/// <c>listBox.SetCurrent( resolutionsIndex )</c> of <c>dialog_resolution.cpp:255-268</c>.
/// </summary>
public sealed class ResolutionDialogTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void AWindowThatMatchesTheFrameBufferHasNoFactor()
    {
        Assert.Equal("640 x 480", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 640, 480)));
    }

    [Fact]
    public void AMagnifiedWindowPrintsTheIntegerPartAndOneDecimal()
    {
        Assert.Equal("640 x 480 (x2.0)", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 1280, 960)));
        Assert.Equal("640 x 480 (x1.5)", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 960, 720)));
        Assert.Equal("640 x 480 (x3.7)", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 2400, 1800)));
    }

    [Fact]
    public void OnlyOneAxisBeingScaledPrintsNoFactor()
    {
        // 参考的条件是 `screenWidth != gameWidth && screenHeight != gameHeight`（两轴都不同才算缩放），
        // 图形设置页的 drawResolution() 却用 `||`，两处不一致，这里按参考的行打印照搬 AND。
        Assert.Equal("640 x 480", ResolutionDialog.FormatRow(new ResolutionInfo(640, 480, 1280, 480)));
    }

    [Fact]
    public void TheFactorIsTheHorizontalOne()
    {
        // 参考只除宽，竖向上限不参与。
        Assert.Equal("800 x 600 (x1.6)", ResolutionDialog.FormatRow(new ResolutionInfo(800, 600, 1280, 1024)));
    }

    [Fact]
    public void TheRowOfTheCurrentWindowStartsSelected()
    {
        ResolutionInfo current = new(640, 480, 1280, 960);

        ResolutionInfo[] resolutions =
        [
            new(640, 480, 2560, 1600),
            current,
            new(640, 480, 640, 480),
        ];

        ResolutionDialog dialog = Create(resolutions, current);

        Assert.True(dialog.IsSelected);
        Assert.Equal(current, dialog.SelectedResolution);
    }

    [Fact]
    public void AWindowThatIsNotAModeStartsAtTheTopRow()
    {
        // 端口开窗时把 640x480 放大 2 倍（1280x960），这不是一个显示模式；参考的
        // `getAvailableResolutions()` 同样只列模式，所以找不到对应行时停在第一行。
        ResolutionInfo[] resolutions =
        [
            new(640, 480, 2560, 1600),
            new(640, 480, 640, 480),
        ];

        ResolutionDialog dialog = Create(resolutions, new ResolutionInfo(640, 480, 1280, 960));

        Assert.True(dialog.IsSelected);
        Assert.Equal(resolutions[0], dialog.SelectedResolution);
    }

    [Fact]
    public void ARightClickOnARowExplainsThatRow()
    {
        ResolutionInfo resolution = new(640, 480, 1280, 960);
        ResolutionDialog dialog = Create([resolution, new(640, 480, 640, 480)], resolution);

        dialog.ActionListPressRight(new ResolutionRow(resolution));

        DialogPopup? popup = dialog.TakePopup();

        Assert.NotNull(popup);
        Assert.Equal("640 x 480 (x2.0)", popup.Value.Header);
        Assert.Contains("2.0", popup.Value.Body, StringComparison.Ordinal);
        Assert.Null(dialog.Popup);
    }

    [Fact]
    public void ARightClickOnAFrameBufferRowSaysSelectThisGameResolution()
    {
        ResolutionInfo resolution = new(640, 480, 640, 480);
        ResolutionDialog dialog = Create([resolution], resolution);

        dialog.ActionListPressRight(new ResolutionRow(resolution));

        Assert.Equal("Select this game resolution.", dialog.TakePopup()?.Body);
    }

    private static ResolutionDialog Create(IReadOnlyList<ResolutionInfo> resolutions,
        ResolutionInfo currentResolution) =>
        ResolutionDialog.TryCreate(Screen, Artwork(), Font(), Font(), Font(), resolutions, currentResolution)
        ?? throw new InvalidOperationException("The dialog should have been created.");

    private static ResolutionArtwork Artwork() => new()
    {
        ScrollBarFrames = [.. Enumerable.Range(0, 5).Select(_ => new Sprite(65, 65))],
        ScrollBarBackground = new Sprite(65, 65),
        Buttons = new FakeButtonArtworkSource(),
    };

    private static IGlyphSource Font() => new FakeGlyphSource(6, 15);

    private sealed class FakeButtonArtworkSource : IDialogButtonArtworkSource
    {
        private readonly DialogButtonArtwork _artwork;

        public FakeButtonArtworkSource()
        {
            Sprite sprite = new(new PixelBuffer(60, 25), 0, 0);
            _artwork = new DialogButtonArtwork(sprite, sprite);
        }

        public DialogButtonArtwork Get(DialogButton button) => _artwork;
    }

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly int _advance;

        public FakeGlyphSource(int advance, int lineHeight)
        {
            _advance = advance;
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

        public int SpaceWidth => _advance;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => _advance;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = new Sprite(new PixelBuffer(_advance, LineHeight), 0, 0);
            return true;
        }
    }
}
