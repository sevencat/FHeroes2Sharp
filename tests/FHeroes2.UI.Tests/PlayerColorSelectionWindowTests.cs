using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The colour picker of <c>Dialog::selectPlayerColor()</c> (<c>dialog_selectitems.cpp:2109-2185</c>): one plate per
/// available colour, a neutral one, and the two answers the reference gives.
/// </summary>
/// <remarks>
/// 帧底图从归档读（<c>BUYBUILD</c>），所以造窗的用例需要真实的游戏数据；没有就直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class PlayerColorSelectionWindowTests
{
    private const byte ButtonColor = 44;

    private static readonly Size CrestSize = new(30, 30);

    [Fact]
    public void TheWindowWidensWithTheNumberOfColours() => Assert.True(TryWithArchives(
        () => CreateWindow(PlayerColor.Blue | PlayerColor.Red),
        window =>
        {
            // 两个可用颜色 + 中立 = 三格：30 + 70 * 3 = 240，仍小于最小宽度 250。
            Assert.Equal(3, window.Cells.Count);
            Assert.Equal(PlayerColorSelectionWindow.MinimumWidth, window.ActiveArea.Width);
            Assert.Equal(PlayerColor.None, window.Cells[0].Color);
        }));

    [Fact]
    public void EveryAvailableColourGetsAPlateAndNothingElse() => Assert.True(TryWithArchives(
        () => CreateWindow(PlayerColor.Blue | PlayerColor.Red | PlayerColor.Green),
        window =>
        {
            Assert.Equal(4, window.Cells.Count);
            Assert.Equal(PlayerColor.Blue, window.Cells[1].Color);
            Assert.Equal(PlayerColor.Green, window.Cells[2].Color);
            Assert.Equal(PlayerColor.Red, window.Cells[3].Color);
        }));

    [Fact]
    public void ClickingAPlateThenOkayAnswersThatColour() => Assert.True(TryWithArchives(
        () => CreateWindow(PlayerColor.Blue | PlayerColor.Red),
        window =>
        {
            var plate = window.Cells[2].Area.Center;

            Assert.Null(window.SetLeftButton(true, plate));
            Assert.Null(window.SetLeftButton(false, plate));
            Assert.Equal(window.Cells[2].Color, window.Current);

            Point okay = window.Buttons[0].Area.Center;

            window.SetLeftButton(true, okay);
            Assert.Equal(window.Cells[2].Color, window.SetLeftButton(false, okay));
        }));

    [Fact]
    public void CancelAnswersTheColourTheObjectAlreadyHad() => Assert.True(TryWithArchives(
        () => CreateWindow(PlayerColor.Blue | PlayerColor.Red, original: PlayerColor.Red),
        window =>
        {
            Point cancel = window.Buttons[1].Area.Center;

            window.SetLeftButton(true, cancel);
            Assert.Equal(PlayerColor.Red, window.SetLeftButton(false, cancel));
        }));

    [Fact]
    public void TheNeutralPlateAnswersNone() => Assert.True(TryWithArchives(
        () => CreateWindow(PlayerColor.Blue),
        window =>
        {
            Point neutral = window.Cells[0].Area.Center;

            window.SetLeftButton(true, neutral);
            window.SetLeftButton(false, neutral);

            Point okay = window.Buttons[0].Area.Center;

            window.SetLeftButton(true, okay);
            Assert.Equal(PlayerColor.None, window.SetLeftButton(false, okay));
        }));

    [Fact]
    public void TheHotKeysAnswerTheSameTwo() => Assert.True(TryWithArchives(
        () => CreateWindow(PlayerColor.Blue | PlayerColor.Red, original: PlayerColor.Red),
        window =>
        {
            Assert.Equal(PlayerColor.Red, window.ProcessHotKey(ButtonHotKey.Cancel));
        }));

    [Fact]
    public void TryCreateReturnsNullWithoutThePlates() => Assert.True(TryWithArchives(
        () => PlayerColorSelectionWindow.TryCreate(PlayerColor.None, PlayerColor.Blue, new EmptySprites(),
            new FixedArtwork(), CreateFont(), CreateFont()),
        window => Assert.Null(window)));

    private static PlayerColorSelectionWindow CreateWindow(PlayerColor available,
        PlayerColor original = PlayerColor.None) =>
        PlayerColorSelectionWindow.TryCreate(original, available, new FakeCrests(), new FixedArtwork(),
            CreateFont(), CreateFont())!;

    /// <summary>
    /// The three plates and the two frames, as flat colours. The idle frame happens to be the texture the frame box
    /// reads, which is enough for the window to build without a second artwork source.
    /// </summary>
    private sealed class FakeCrests : IColorCrestSprites
    {
        private readonly Sprite _plate = new(Filled(CrestSize, 21), 0, 0);

        public Sprite? GetCrown(PlayerColor color) => _plate;

        public Sprite? GetNeutralCrown() => _plate;

        public Sprite? GetSelectionFrame() => new(Filled(CrestSize, 22));

        public Sprite? GetIdleFrame() => new(Filled(CrestSize, 23));
    }

    [Fact]
    public void TheWindowBuildsWithoutTheNeutralPlate() => Assert.True(TryWithArchives(
        () => PlayerColorSelectionWindow.TryCreate(PlayerColor.None, PlayerColor.Blue | PlayerColor.Red,
            new NoNeutralCrest(), new FixedArtwork(), CreateFont(), CreateFont()),
        window => Assert.Equal(3, window.Cells.Count)));

    /// <summary>Every plate except the neutral one, which the 1996 data does not have.</summary>
    private sealed class NoNeutralCrest : IColorCrestSprites
    {
        private readonly Sprite _plate = new(Filled(CrestSize, 21));

        public Sprite? GetCrown(PlayerColor color) => _plate;

        public Sprite? GetNeutralCrown() => null;

        public Sprite? GetSelectionFrame() => new(Filled(CrestSize, 22));

        public Sprite? GetIdleFrame() => new(Filled(CrestSize, 23));
    }

    /// <summary>No plates at all, so <see cref="PlayerColorSelectionWindow.TryCreate"/> has to refuse.</summary>
    private sealed class EmptySprites : IColorCrestSprites
    {
        public Sprite? GetCrown(PlayerColor color) => null;

        public Sprite? GetNeutralCrown() => null;

        public Sprite? GetSelectionFrame() => null;

        public Sprite? GetIdleFrame() => null;
    }

    private static PixelBuffer Filled(Size size, byte color)
    {
        PixelBuffer buffer = new(size.Width, size.Height);
        buffer.Fill(color);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new BlockGlyphSource();

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static bool TryWithArchives(Func<PlayerColorSelectionWindow?> create,
        Action<PlayerColorSelectionWindow> assert)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            PlayerColorSelectionWindow? window = create();

            if (window is null)
            {
                Assert.Null(window);

                return true;
            }

            assert(window);
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
            new(new Sprite(Filled(new Size(60, 25), ButtonColor)), new Sprite(Filled(new Size(60, 25), 45)));
    }

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
