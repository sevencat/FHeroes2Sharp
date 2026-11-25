using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Battle;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The six line battle log of <see cref="BattleStatusLog"/>, a port of <c>Battle::StatusListBox</c>
/// (<c>battle_interface.cpp:803-980</c>).
/// </summary>
public sealed class BattleStatusLogTests
{
    /// <summary>A font whose every character is six pixels wide, so 98 characters fill one 592 pixel row.</summary>
    private sealed class FixedFont : IGlyphSource
    {
        public const int CharacterWidth = 6;

        public int LineHeight => 17;

        public int SpaceWidth => CharacterWidth;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => character == GlyphText.LineSeparator ? 0 : CharacterWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = new Sprite(CharacterWidth, LineHeight);

            return true;
        }
    }

    /// <summary>A source without any artwork, the state of a build without game data.</summary>
    private sealed class EmptySprites : IViewWorldSpriteSource
    {
        public Sprite? GetSprite(IcnType icn, int index) => null;
    }

    /// <summary>The <c>TEXTBAK2</c>/<c>DROPLISL</c> frames the window needs, in the sizes the real archive has.</summary>
    private sealed class LogSprites : IViewWorldSpriteSource
    {
        /// <summary>Palette index every pixel of the stretched background ends up as.</summary>
        public const byte FillValue = 5;

        public Sprite? GetSprite(IcnType icn, int index) => (icn, index) switch
        {
            (IcnType.Textbak2, 0) => CreateBackground(),
            (IcnType.Droplisl, 6 or 7 or 8 or 9 or 10 or 11 or 12) => new Sprite(17, 17),
            (IcnType.Droplisl, 13) => new Sprite(12, 15),
            _ => null,
        };

        private static Sprite CreateBackground()
        {
            PixelBuffer image = new(640, 480);
            ImageOps.Clear(image, FillValue);

            return new Sprite(image);
        }
    }

    private static BattleStatusLog Create(IViewWorldSpriteSource? sprites = null) =>
        new(sprites ?? new EmptySprites(), new FixedFont());

    [Fact]
    public void SetPosition_PutsTheBottomEdgeOnTheStatusBar()
    {
        BattleStatusLog log = Create();

        log.SetPosition(0, 443);

        // 98 rows of content plus 16 rows of frame on every side; the width loses the same 32 columns.
        Assert.Equal(0, log.Frame.X);
        Assert.Equal(313, log.Frame.Y);
        Assert.Equal(608, log.Frame.Width);
        Assert.Equal(98, log.Frame.Height);

        Assert.Equal(16, log.Area.X);
        Assert.Equal(329, log.Area.Y);
        Assert.Equal(608, log.Area.Width);
        Assert.Equal(98, log.Area.Height);
    }

    [Fact]
    public void SetPosition_LeavesSixRowsOfSeventeenAndAFourRowOverlap()
    {
        BattleStatusLog log = Create();

        log.SetPosition(0, 443);

        Assert.Equal(BattleStatusLog.MaxElements, log.VisibleItemCount);
        Assert.Equal(592, log.ItemsArea.Width);
        Assert.Equal(BattleStatusLog.MaxElements * BattleStatusLog.ElementHeight, log.ItemsArea.Height);
    }

    [Fact]
    public void SetPosition_LaysTheScrollBarBetweenTheTwoDroplislButtons()
    {
        BattleStatusLog log = Create(new LogSprites());

        log.SetPosition(0, 443);

        Assert.Equal(617, log.ScrollBar.Area.X);
        Assert.Equal(339, log.ScrollBar.Area.Y);
        Assert.Equal(12, log.ScrollBar.Area.Width);
        Assert.Equal(73, log.ScrollBar.Area.Height);
    }

    [Fact]
    public void SetOpen_RebuildsTheSliderFromTheDroplislFrame()
    {
        BattleStatusLog log = Create(new LogSprites());

        log.SetPosition(0, 443);

        Assert.Null(log.ScrollBar.Slider);

        log.SetOpen(true);

        Assert.True(log.IsOpen);
        Assert.NotNull(log.ScrollBar.Slider);
        Assert.Equal(73, log.ScrollBar.Slider!.Height);

        log.SetOpen(false);

        Assert.False(log.IsOpen);
        Assert.Null(log.ScrollBar.Slider);
    }

    [Fact]
    public void AMessageThatFitsTakesOneRow()
    {
        BattleStatusLog log = Create();

        log.AddMessage("Move to 24");

        Assert.Single(log.Messages);
        Assert.Equal("Move to 24", log.Messages[0]);
    }

    [Fact]
    public void AnEmptyMessageIsIgnored()
    {
        BattleStatusLog log = Create();

        log.AddMessage(string.Empty);

        Assert.Empty(log.Messages);
    }

    [Fact]
    public void ATooLongMessageIsSplitBeforeItsLastFittingSpace()
    {
        BattleStatusLog log = Create();

        string first = new('a', 60);
        string second = new('b', 60);

        log.AddMessage($"{first} {second}");

        // 121 characters are 726 pixels and one row fits 98, so the split lands on the only space.
        Assert.Equal(2, log.Messages.Count);
        Assert.Equal(first, log.Messages[0]);
        Assert.Equal(second, log.Messages[1]);
    }

    [Fact]
    public void TheSplitTakesAsManyWordsAsFitTheRow()
    {
        BattleStatusLog log = Create();
        string word = new('a', 30);

        log.AddMessage($"{word} {word} {word} {word}");

        // Four words are 738 pixels; the first three are 92 characters, i.e. 552 pixels.
        Assert.Equal(2, log.Messages.Count);
        Assert.Equal($"{word} {word} {word}", log.Messages[0]);
        Assert.Equal(word, log.Messages[1]);
    }

    [Fact]
    public void AWordWiderThanTheRowIsCutWithTheEllipsis()
    {
        BattleStatusLog log = Create();

        log.AddMessage(new string('x', 120));

        Assert.Single(log.Messages);
        Assert.EndsWith(GlyphText.TruncationSymbol, log.Messages[0]);
        Assert.Equal(98, log.Messages[0].Length);
    }

    [Fact]
    public void TheNewestRowStaysVisible()
    {
        BattleStatusLog log = Create();

        log.SetPosition(0, 443);

        for (int i = 0; i < 8; ++i)
        {
            log.AddMessage($"m{i}");
        }

        Assert.Equal(8, log.Size);
        Assert.Equal(2, log.TopId);
        Assert.Equal(7, log.CurrentId);
    }

    [Fact]
    public void RedrawPaintsTheFrameOverTheBattlefield()
    {
        BattleStatusLog log = Create(new LogSprites());

        log.SetPosition(0, 443);
        log.SetOpen(true);
        log.AddMessage("Move to 24");

        PixelBuffer frame = new(ScreenBuffer.DefaultWidth, ScreenBuffer.DefaultHeight);

        log.Redraw(frame);

        // The frame starts at (0, 313); the row text is font work, so the background is what is checked here.
        Assert.Equal(LogSprites.FillValue, frame.Pixels[(313 * ScreenBuffer.DefaultWidth) + 1]);
    }

    [Fact]
    public void RedrawWithoutArtworkLeavesTheFrameAlone()
    {
        BattleStatusLog log = Create();

        log.SetPosition(0, 443);
        log.SetOpen(true);
        log.AddMessage("Move to 24");

        PixelBuffer frame = new(ScreenBuffer.DefaultWidth, ScreenBuffer.DefaultHeight);

        log.Redraw(frame);

        Assert.Equal(0, frame.Pixels[(313 * ScreenBuffer.DefaultWidth) + 1]);
    }
}
