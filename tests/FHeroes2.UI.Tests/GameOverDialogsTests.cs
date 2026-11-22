using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="GameOverDialogs"/>: <c>DialogWins()</c>, <c>DialogLoss()</c> and
/// <c>Game::DialogPlayers()</c> of the reference.
/// </summary>
/// <remarks>
/// The windows are drawn in a <see cref="MessageBox"/>, which reads <c>BUYBUILD.ICN</c> itself, so the tests that
/// really open one need the installed archives and return when the machine has none. The "no dialog at all" cases
/// and the sign composition do not touch the archives and always run.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class GameOverDialogsTests
{
    private const byte BorderColor = 10;
    private const byte CrestColor = 20;

    [Fact]
    public void ShowVictory_ShowsNothingForDefeatEveryone()
    {
        // 全灭对手的判定没有胜利文案，参考也就不弹窗。
        PixelBuffer target = new(640, 480);

        Assert.Equal(
            DialogButton.None,
            GameOverDialogs.ShowVictory(
                target,
                GameOver.WinsAll,
                Info(),
                new BlockGlyphSource(),
                new BlockGlyphSource(),
                new FakeArtwork()));
    }

    [Fact]
    public void ShowDefeat_ShowsNothingForAMaskWithoutWords()
    {
        PixelBuffer target = new(640, 480);

        Assert.Equal(
            DialogButton.None,
            GameOverDialogs.ShowDefeat(
                target,
                GameOver.LossAll | GameOver.LossTime,
                Info(),
                new BlockGlyphSource(),
                new BlockGlyphSource(),
                new FakeArtwork()));
    }

    [Fact]
    public void ComposeSign_PutsTheCrestFourPixelsInsideTheBorder()
    {
        Sprite sign = GameOverDialogs.ComposeSign(new FakeSignArtwork(), PlayerColor.Blue);

        Assert.Equal(new Size(30, 30), new Size(sign.Width, sign.Height));
        Assert.Equal(BorderColor, sign.Image[0, 0]);
        Assert.Equal(CrestColor, sign.Image[GameOverDialogs.CrestOffset, GameOverDialogs.CrestOffset]);
        Assert.Equal(BorderColor, sign.Image[GameOverDialogs.CrestOffset - 1, GameOverDialogs.CrestOffset]);
        Assert.Equal(CrestColor, sign.Image[GameOverDialogs.CrestOffset + 19, GameOverDialogs.CrestOffset + 19]);
    }

    [Theory]
    [InlineData(PlayerColor.Blue | PlayerColor.Red)]
    [InlineData(PlayerColor.None)]
    public void ComposeSign_RejectsAnythingButOneColour(PlayerColor color) =>
        Assert.Throws<ArgumentException>(() => GameOverDialogs.ComposeSign(new FakeSignArtwork(), color));

    [Fact]
    public void ShowVictory_ReturnsNoneWhenTheEngineStops() => Assert.True(TryWithArchives(
        () =>
        {
            PixelBuffer target = new(640, 480);
            LocalEvent events = new(() => 0) { Engine = new StoppingEngine() };

            return GameOverDialogs.ShowVictory(
                target,
                GameOver.WinsGold,
                Info(victoryParameters: [10, 0]),
                new BlockGlyphSource(),
                new BlockGlyphSource(),
                new FakeArtwork(),
                events: events);
        },
        answer => Assert.Equal(DialogButton.None, answer)));

    [Fact]
    public void ShowVanquished_DrawsThePlayerSign() => Assert.True(TryWithArchives(
        () =>
        {
            PixelBuffer target = new(640, 480);
            target.Fill(0);

            LocalEvent events = new(() => 0) { Engine = new StoppingEngine() };

            return GameOverDialogs.ShowVanquished(
                target,
                PlayerColor.Blue,
                "Beware!",
                "%{color} player, this is your last day to capture a town.",
                new FakeSignArtwork(),
                new BlockGlyphSource(),
                new BlockGlyphSource(),
                new FakeArtwork(),
                events);
        },
        answer => Assert.Equal(DialogButton.None, answer)));

    /// <summary>A map information block with the given victory parameters only.</summary>
    /// <param name="victoryParameters">The parameters of the victory condition.</param>
    /// <returns>The block.</returns>
    private static Mp2FileInfo Info(int[]? victoryParameters = null) => new()
    {
        FilePath = "gameover.mp2",
        Name = "GameOver",
        Description = string.Empty,
        Width = 36,
        Height = 36,
        Difficulty = GameDifficulty.Normal,
        VictoryConditionParameters = victoryParameters ?? [0, 0],
        LossConditionParameters = [0, 0],
    };

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Runs a test that needs the message frame artwork, or returns when the machine has none.</summary>
    /// <param name="create">Runs the window with an engine that stops at once.</param>
    /// <param name="assert">Checks the answer.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool TryWithArchives(Func<DialogButton> create, Action<DialogButton> assert)
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

    /// <summary>A 30x30 sign and a 20x20 crest in flat colours, so the blit position is easy to check.</summary>
    private sealed class FakeSignArtwork : IVanquishedSignArtworkSource
    {
        public Sprite Border { get; } = Solid(new Size(30, 30), BorderColor);

        public Sprite Crest(PlayerColor color) => Solid(new Size(20, 20), CrestColor);
    }

    /// <summary>The OK button is 60x25 in the test.</summary>
    private sealed class FakeArtwork : IDialogButtonArtworkSource
    {
        public DialogButtonArtwork Get(DialogButton button) =>
            new(Solid(new Size(60, 25), 40), Solid(new Size(60, 25), 45));
    }

    /// <summary>A 6x15 solid block font, which is enough for the title and one sentence.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = Solid(new Size(6, 15), 90);

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

    /// <summary>An event engine that has nothing to poll and asks the loop to stop.</summary>
    private sealed class StoppingEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            return false;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    private static Sprite Solid(Size size, byte color)
    {
        PixelBuffer image = new(size.Width, size.Height);
        image.Fill(color);

        return new Sprite(image, 0, 0);
    }
}
