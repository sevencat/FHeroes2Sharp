using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The random map generator dialog (<c>dialog_random_map_generator.cpp:72-251</c>): the clamping rules, the six rows and
/// the answer it produces.
/// </summary>
public sealed class RandomMapGeneratorDialogTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void ThePlayerCountIsClampedIntoTheReferenceRange()
    {
        RandomMapGeneratorDialog low = RandomMapGeneratorDialog.Create(new RandomMapConfig { PlayerCount = 1 }, 72);
        RandomMapGeneratorDialog high = RandomMapGeneratorDialog.Create(new RandomMapConfig { PlayerCount = 9 }, 72);

        Assert.Equal(RandomMapGeneratorDialog.MinimumPlayers, low.PlayerCount);
        Assert.Equal(RandomMapGeneratorDialog.MaximumPlayers, high.PlayerCount);
    }

    [Fact]
    public void TheWaterPercentageNeverPassesTheLimitOfTheMap()
    {
        // 36×36 上两名玩家最多能有 (1296 - 3*200) * 100 / 1296 = 53% 的水。
        RandomMapGeneratorDialog dialog =
            RandomMapGeneratorDialog.Create(new RandomMapConfig { PlayerCount = 2, WaterPercentage = 100 }, 36);

        Assert.Equal(RandomMapSizing.MaximumWaterPercentage(2, 36), dialog.WaterPercentage);
        Assert.Equal(53, dialog.MaximumWaterPercentage);
    }

    [Fact]
    public void FewerPlayersAllowMoreWater()
    {
        RandomMapGeneratorDialog dialog =
            RandomMapGeneratorDialog.Create(new RandomMapConfig { PlayerCount = 2 }, 72);
        int wide = dialog.MaximumWaterPercentage;

        dialog.SetPlayerCount(6);

        // 玩家越多，留给「水」的地越少；水量也被一起夹下来。
        Assert.True(dialog.MaximumWaterPercentage < wide);
        Assert.True(dialog.WaterPercentage <= dialog.MaximumWaterPercentage);
    }

    [Fact]
    public void TheWaterSliderRangeFollowsThePlayerCount()
    {
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.Create(new RandomMapConfig(), 36));

        int before = view.WaterSlider.MaxValue;

        // 连点玩家数滑块的右箭头，把两名玩家推到六名；水量上限与滑块范围都要跟着变小。
        for (int step = 0; step < 4; ++step)
        {
            view.ProcessEvents(ClickEvent(view.PlayerCountSlider.RightButton.Area));
        }

        Assert.Equal(6, view.Dialog.PlayerCount);
        Assert.True(view.WaterSlider.MaxValue < before);
        Assert.Equal(view.Dialog.MaximumWaterPercentage, view.WaterSlider.MaxValue);
    }

    [Fact]
    public void TheWaterSliderReachesItsEnd()
    {
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.Create(new RandomMapConfig(), 36));

        for (int step = 0; step < 100; ++step)
        {
            view.ProcessEvents(ClickEvent(view.WaterSlider.RightButton.Area));
        }

        Assert.Equal(view.Dialog.MaximumWaterPercentage, view.Dialog.WaterPercentage);
    }

    [Fact]
    public void CancellingProducesNoConfiguration()
    {
        RandomMapGeneratorDialog dialog = RandomMapGeneratorDialog.CreateDefault(72);

        dialog.Cancel();

        Assert.Null(dialog.Result);
        Assert.False(dialog.IsAccepted);
    }

    [Fact]
    public void AcceptingKeepsEveryValueOfTheDialog()
    {
        RandomMapGeneratorDialog dialog = RandomMapGeneratorDialog.CreateDefault(72);

        dialog.SetPlayerCount(4);
        dialog.SetWaterPercentage(30);
        dialog.SetMonsterStrength(RandomMapMonsterStrength.Deadly);
        dialog.SetResourceDensity(RandomMapResourceDensity.Abundant);
        dialog.SetSeed(123456);
        dialog.Accept();

        Assert.True(dialog.IsAccepted);

        RandomMapConfig config = dialog.Result!;

        Assert.Equal(4, config.PlayerCount);
        Assert.Equal(30, config.WaterPercentage);
        Assert.Equal(RandomMapMonsterStrength.Deadly, config.MonsterStrength);
        Assert.Equal(RandomMapResourceDensity.Abundant, config.ResourceDensity);
        Assert.Equal(123456, config.Seed);
        Assert.Equal(RandomMapLayout.Mirrored, config.Layout);
    }

    [Fact]
    public void TheSeedIsClampedIntoTheField()
    {
        RandomMapGeneratorDialog dialog = RandomMapGeneratorDialog.CreateDefault(72);

        dialog.SetSeed(-5);
        Assert.Equal(RandomMapGeneratorDialog.MinimumSeed, dialog.Seed);

        dialog.SetSeed(2_000_000);
        Assert.Equal(RandomMapGeneratorDialog.MaximumSeed, dialog.Seed);
    }

    [Fact]
    public void WithoutEveryFrameThereIsNoWindow()
    {
        Assert.False(Artwork(frames: 3).IsComplete);
        Assert.Null(RandomMapGeneratorView.TryCreate(
            RandomMapGeneratorDialog.CreateDefault(72), RandomMapGeneratorDialogText.Default, Artwork(frames: 3),
            Font(), Font(), Screen));

        Assert.True(Artwork().IsComplete);
    }

    [Fact]
    public void TheSixRowsSitAtTheReferenceOffsets()
    {
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.CreateDefault(72));
        Rect active = view.Window.ActiveArea;

        int inputX = active.X + RandomMapGeneratorView.LabelInset + (active.Width / 3);

        Assert.Equal(RandomMapGeneratorView.ActiveWidth, active.Width);
        Assert.Equal(RandomMapGeneratorView.ActiveHeight, active.Height);
        Assert.Equal(new Point(inputX, active.Y + RandomMapGeneratorView.FirstRowOffset),
            view.PlayerCountSlider.Position);

        Assert.Equal(
            new Point(inputX, active.Y + RandomMapGeneratorView.FirstRowOffset + (2 * RandomMapGeneratorView.RowSpacing)),
            view.WaterSlider.Position);
    }

    [Fact]
    public void TheButtonsSitInTheTwoBottomCorners()
    {
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.CreateDefault(72));
        Rect active = view.Window.ActiveArea;

        ButtonSprite cancel = (ButtonSprite)view.Buttons[0];
        ButtonSprite okay = (ButtonSprite)view.Buttons[1];

        Assert.Equal(DialogButton.Cancel, view.Buttons.ValueOf(0));
        Assert.Equal(DialogButton.Ok, view.Buttons.ValueOf(1));

        // 参考：CANCEL 在右下、OK 在左下，各离角 30 / 10 像素。
        Assert.Equal(
            active.X + active.Width - RandomMapGeneratorView.ButtonOffset.X - cancel.Released.Width,
            cancel.Area.X);
        Assert.Equal(
            active.X + RandomMapGeneratorView.ButtonOffset.X,
            okay.Area.X);
    }

    [Fact]
    public void TheEscapeKeyCancelsTheDialog()
    {
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.CreateDefault(72));

        LocalEvent events = new();
        events.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);

        Assert.True(view.ProcessEvents(events, ButtonHotKey.Cancel));
        Assert.Null(view.Dialog.Result);
    }

    [Fact]
    public void TheEnterKeyAcceptsTheDialog()
    {
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.CreateDefault(72));

        LocalEvent events = new();
        events.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.True(view.ProcessEvents(events, ButtonHotKey.Okay));
        Assert.NotNull(view.Dialog.Result);
    }

    [Fact]
    public void TheWindowDrawsWithoutAnEngine() => Assert.True(TryWithArchives(() =>
    {
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.CreateDefault(72));
        PixelBuffer canvas = new(640, 480);

        view.Draw(canvas);

        Assert.Equal(640, canvas.Width);
        Assert.Equal(480, canvas.Height);
    }));

    [Fact]
    public void TheLayoutRowIsNotOperable()
    {
        // 参考把布局这一行涂暗、按钮禁用：生成器只会 MIRRORED。
        RandomMapGeneratorView view = Create(RandomMapGeneratorDialog.CreateDefault(72));
        RandomMapLayout before = view.Dialog.Layout;

        LocalEvent events = new();

        view.ProcessEvents(events);

        Assert.Equal(before, view.Dialog.Layout);
        Assert.Equal(RandomMapLayout.Mirrored, view.Dialog.Layout);
    }

    private static RandomMapGeneratorView Create(RandomMapGeneratorDialog dialog) =>
        RandomMapGeneratorView.TryCreate(
            dialog, RandomMapGeneratorDialogText.Default, Artwork(), Font(), Font(), Screen)
        ?? throw new InvalidOperationException("The window should have been created.");

    private static RandomMapGeneratorArtwork Artwork(int frames = 9) => new()
    {
        BackgroundFrames = [new Sprite(32, 32)],
        TitleBoxFrames = [new Sprite(120, 30)],
        SliderFrames = [.. Enumerable.Range(0, frames).Select(_ => new Sprite(30, 12))],
        ValueFrames = [.. Enumerable.Range(0, frames).Select(_ => new Sprite(68, 19))],
        DroplistFrames = [.. Enumerable.Range(0, frames).Select(_ => new Sprite(20, 20))],
        Buttons = new FakeButtonArtworkSource(),
    };

    private static FakeGlyphSource Font() => new(6, 12);

    /// <summary>One left click in the middle of <paramref name="area"/>.</summary>
    private static LocalEvent ClickEvent(Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

    /// <summary>
    /// Runs <paramref name="body"/> with the game archives installed, the <see cref="StandardWindow"/> drawing needs
    /// them; returns <see langword="true"/> without running it when the machine has no game data.
    /// </summary>
    private static bool TryWithArchives(Action body)
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            body();
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }

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

