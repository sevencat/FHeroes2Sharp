using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="AutoPlaytestResultsWindow"/>, the <c>displayResults()</c> of
/// <c>game_auto_playtest.cpp:107-256</c>.
/// </summary>
public sealed class AutoPlaytestResultsWindowTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TheWindowIsFiveHundredByTwoHundredTwentyCentred()
    {
        AutoPlaytestResultsWindow window = Create();

        Assert.Equal(new Rect(70, 130, 500, 220), window.Window.ActiveArea);
    }

    [Fact]
    public void ThePlayerIconsSitUnderTheTitleBox()
    {
        AutoPlaytestResultsWindow window = Create();

        // 活动区 (70,130)；标题框 200×30 居中于 x=220，标题行在 y=140；图标排从标题框底 + 10 = 180 起。
        // 两个玩家的排宽 = 2*80 + 18 = 178，居中后起点 x = 70 + (500-160+18)/2 = 249。
        Assert.Equal(new Rect(249, 180, 30, 26), window.PlayerIconArea(0));
        Assert.Equal(new Rect(329, 180, 30, 26), window.PlayerIconArea(1));
    }

    [Fact]
    public void TheThreeTextRowsSitTwentyPixelsApart()
    {
        AutoPlaytestResultsWindow window = Create();

        // 图标高 26，百分数行在 180+26+10 = 216 起，之后每行再 20。
        Assert.Equal(216, window.PercentRowTop);
        Assert.Equal(236, window.CountRowTop);
        Assert.Equal(256, window.LimitRowTop);
    }

    [Fact]
    public void ThePercentIsTheWinRateOfEachColour()
    {
        // 两轮：一轮蓝胜、一轮绿胜，于是各 50%。
        PlaytestSummary summary = new(
        [
            Report(PlaytestPlayerState.Winner, PlaytestPlayerState.Loser),
            Report(PlaytestPlayerState.Loser, PlaytestPlayerState.Winner),
        ]);

        AutoPlaytestResultsWindow window = Create(summary);

        Assert.Equal(2, window.Summary.Count);
        Assert.Equal(50, window.Summary.Colors[0].Percentage);
        Assert.Equal(50, window.Summary.Colors[1].Percentage);
    }

    [Fact]
    public void AnEmptyTallyOpensNoWindow()
    {
        Assert.Null(AutoPlaytestResultsWindow.TryCreate(PlaytestSummary.Empty, null, CreateArtwork(), Font(), Font(),
            Font(), Screen));
    }

    [Fact]
    public void OkayClosesTheWindow()
    {
        AutoPlaytestResultsWindow window = Create();

        Assert.True(window.ProcessEvents(new LocalEvent(), ButtonHotKey.Okay));
        Assert.True(window.IsClosed);
    }

    [Fact]
    public void EscapeClosesTheSingleButtonWindowAsWell()
    {
        AutoPlaytestResultsWindow window = Create();

        // 只有一颗按钮时，ButtonGroup 对两个热键都答那颗按钮（参考的 HotKeyCloseWindow()）。
        Assert.True(window.ProcessEvents(new LocalEvent(), ButtonHotKey.Cancel));
    }

    [Fact]
    public void RightClickingAColourIconNamesTheOpponent()
    {
        AutoPlaytestResultsWindow window = Create();

        Assert.False(window.ProcessEvents(RightPress(Middle(window.PlayerIconArea(1))), null));

        DialogPopup? popup = window.TakePopup();

        Assert.NotNull(popup);
        Assert.Equal("Opponents", popup!.Value.Header);
        Assert.Equal("Green player", popup.Value.Body);
    }

    [Fact]
    public void RightClickingAPercentageExplainsTheWinRate()
    {
        AutoPlaytestResultsWindow window = Create();

        Rect icon = window.PlayerIconArea(0);
        Point point = new(icon.X + (icon.Width / 2), window.PercentRowTop + 4);

        Assert.False(window.ProcessEvents(RightPress(point), null));

        DialogPopup? popup = window.TakePopup();

        Assert.NotNull(popup);
        Assert.Equal("Blue", popup!.Value.Header);
        Assert.Contains("Won", popup.Value.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlaythroughLinesFollowTheReferenceWording()
    {
        AutoPlaytestResultsText text = AutoPlaytestResultsText.Default;

        Assert.Equal("1 playthrough", text.PlaythroughCount(1));
        Assert.Equal("3 playthroughs", text.PlaythroughCount(3));
        Assert.Equal("2 playthrough(s) reached the specified time limit", text.TimeLimitLine(2));
        Assert.Equal("Won 42% of playthroughs.", text.Won(PlayerColor.Blue, 42));
    }

    private static AutoPlaytestResultsWindow Create() => Create(new PlaytestSummary([Report(PlaytestPlayerState.Winner,
        PlaytestPlayerState.Loser)]));

    private static AutoPlaytestResultsWindow Create(PlaytestSummary summary)
    {
        AutoPlaytestResultsWindow? window = AutoPlaytestResultsWindow.TryCreate(summary, null, CreateArtwork(), Font(),
            Font(), Font(), Screen);

        return Assert.IsType<AutoPlaytestResultsWindow>(window);
    }

    /// <summary>One playthrough of the two colours the tests use.</summary>
    /// <param name="blue">How blue ended.</param>
    /// <param name="green">How green ended.</param>
    /// <returns>The report.</returns>
    private static PlaytestReport Report(PlaytestPlayerState blue, PlaytestPlayerState green) =>
        new(1,
        [
            new PlaytestPlayerResult(PlayerColor.Blue, blue, 1),
            new PlaytestPlayerResult(PlayerColor.Green, green, 1),
        ]);

    private static AutoPlaytestResultsArtwork CreateArtwork() => new()
    {
        TitleBoxFrames = [Solid(200, 30, 10)],
        PlayerIcons = new FakePlayerIcons(),
        Buttons = new FakeButtons(),
    };

    private static IGlyphSource Font() => new BlankFont();

    private static Sprite Solid(int width, int height, byte color)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image);
    }

    private static Point Middle(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static LocalEvent RightPress(Point position)
    {
        var events = new LocalEvent();

        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        return events;
    }

    /// <summary>Solid pictures of a fixed size: the tests only care about layout, not about pixels.</summary>
    private sealed class FakePlayerIcons : IPlayersInfoArtwork
    {
        public Sprite GetIcon(uint frame) => Solid(30, 26, (byte)(frame % 200 + 20));
    }

    private sealed class FakeButtons : IDialogButtonArtworkSource
    {
        public DialogButtonArtwork Get(DialogButton button) => new(Solid(30, 20, 30), Solid(30, 20, 31));
    }

    /// <summary>A font whose glyphs are empty: every character advances one pixel and draws nothing.</summary>
    private sealed class BlankFont : IGlyphSource
    {
        public int LineHeight => 1;

        public int SpaceWidth => 1;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => 1;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = Solid(1, 1, 0);

            return true;
        }
    }
}
