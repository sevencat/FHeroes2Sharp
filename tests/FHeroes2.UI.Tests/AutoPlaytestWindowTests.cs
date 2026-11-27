using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="AutoPlaytestWindow"/>, the setup window of <c>openMapAutoPlayTest()</c>
/// (<c>game_auto_playtest.cpp:386-660</c>).
/// </summary>
/// <remarks>
/// 全部用合成图：滑条只关心帧数与高度，复选只关心方框尺寸，玩家图标只要尺寸对得上，按钮 30×20。
/// 640×480 的屏幕让活动区落在 (45, 15)，于是每个偏移都能直接算出来。
/// </remarks>
public sealed class AutoPlaytestWindowTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TheWindowIsFiveHundredFiftyByFourHundredFiftyCentred()
    {
        AutoPlaytestWindow window = Create();

        Assert.Equal(new Rect(45, 15, 550, 450), window.Window.ActiveArea);
    }

    [Fact]
    public void TheRowsSitOnTheOffsetsOfTheReference()
    {
        AutoPlaytestWindow window = Create();

        // 活动区 x=45、y=15；标签栏 250、滑条 150、值列再右 57。
        Assert.Equal(new Point(305, 85), window.PlaythroughSlider.Position);
        Assert.Equal(new Point(305, 130), window.DaySlider.Position);
        Assert.Equal(new Point(305, 220), window.AnimationSpeedSlider.Position);

        Assert.Equal(new Rect(308, 190, 12, 12), window.AnimationCheckbox.Area);
        Assert.Equal(new Rect(308, 250, 12, 12), window.SoundsCheckbox.Area);

        // 两个玩家：图标 30 宽、间距 80，整排在自己那行里居中。
        Assert.Equal(new Rect(265, 280, 30, 26), window.ColorIconArea(0));
        Assert.Equal(new Rect(345, 280, 30, 26), window.ColorIconArea(1));
        Assert.Equal(new Rect(265, 340, 30, 26), window.ClassIconArea(0));
        Assert.Equal(new Rect(345, 340, 30, 26), window.ClassIconArea(1));
    }

    [Fact]
    public void Ok_ReturnsTheValuesOfTheSlidersAndCheckboxes()
    {
        AutoPlaytestWindow window = Create();

        Assert.True(window.ProcessEvents(new LocalEvent(), ButtonHotKey.Okay));

        Assert.Equal(AutoPlaytestOutcome.Accepted, window.Result);
        Assert.NotNull(window.Settings);

        // 构造时给的 4 轮、30 天、速度 8、动画开、音效关。
        Assert.Equal(4, window.Settings!.MaxPlaythroughs);
        Assert.Equal(30, window.Settings.MaxDays);
        Assert.Equal(8, window.Settings.AnimationSpeed);
        Assert.True(window.Settings.AnimationEnabled);
        Assert.False(window.Settings.EnvironmentSounds);
    }

    [Fact]
    public void Cancel_LeavesNoSettings()
    {
        AutoPlaytestWindow window = Create();

        Assert.True(window.ProcessEvents(new LocalEvent(), ButtonHotKey.Cancel));

        Assert.Equal(AutoPlaytestOutcome.Cancelled, window.Result);
        Assert.Null(window.Settings);
    }

    [Fact]
    public void DraggingTheDaySliderWritesTheValueBack()
    {
        AutoPlaytestWindow window = Create();

        // 在槽的右半段按下：滑块自己把拇指移过去，窗口把值抄下来。
        Rect groove = window.DaySlider.GrooveArea;
        Point pressed = new(groove.X + groove.Width - 4, groove.Y + (groove.Height / 2));

        Assert.False(window.ProcessEvents(Press(pressed)));

        int value = window.DaySlider.CurrentValue;

        Assert.True(value > 30);

        Assert.True(window.ProcessEvents(new LocalEvent(), ButtonHotKey.Okay));
        Assert.Equal(value, window.Settings!.MaxDays);
    }

    [Fact]
    public void TurningAnimationOffDisablesTheSpeedSliderAndTheSoundCheckbox()
    {
        AutoPlaytestWindow window = Create();

        Assert.True(window.AnimationSpeedSlider.IsEnabled);
        Assert.True(window.SoundsCheckbox.IsActive);

        Assert.False(window.ProcessEvents(Click(Middle(window.AnimationCheckbox.Area))));

        Assert.False(window.AnimationCheckbox.IsChecked);
        Assert.False(window.AnimationSpeedSlider.IsEnabled);
        Assert.False(window.SoundsCheckbox.IsActive);

        // 打开回来：速度滑条与音效复选都恢复，音效的值没有被那次点击改掉。
        Assert.False(window.ProcessEvents(Click(Middle(window.AnimationCheckbox.Area))));

        Assert.True(window.AnimationCheckbox.IsChecked);
        Assert.True(window.AnimationSpeedSlider.IsEnabled);
        Assert.True(window.SoundsCheckbox.IsActive);
        Assert.False(window.SoundsCheckbox.IsChecked);
    }

    [Fact]
    public void TheSoundCheckboxIgnoresClicksWhileAnimationIsOff()
    {
        AutoPlaytestWindow window = Create();

        _ = window.ProcessEvents(Click(Middle(window.AnimationCheckbox.Area)));

        int frames = 0;

        while (!window.SoundsCheckbox.IsChecked && frames++ < 4)
        {
            _ = window.ProcessEvents(Click(Middle(window.SoundsCheckbox.Area)));
        }

        // 参考在这条分支上挂着 && isAnimationEnabled()，端口把它收进了控件：点了也不会翻。
        Assert.False(window.SoundsCheckbox.IsChecked);
    }

    [Fact]
    public void RightClickingAColourIconAsksForTheOpponentsHelp()
    {
        AutoPlaytestWindow window = Create();

        Assert.False(window.ProcessEvents(RightPress(Middle(window.ColorIconArea(1)))));

        DialogPopup? popup = window.TakePopup();

        Assert.NotNull(popup);
        Assert.Equal("Opponents", popup!.Value.Header);
        Assert.Equal("Green player", popup.Value.Body);
    }

    [Fact]
    public void RightClickingAClassIconExplainsTheClass()
    {
        AutoPlaytestWindow window = Create();

        Assert.False(window.ProcessEvents(RightPress(Middle(window.ClassIconArea(0)))));

        DialogPopup? popup = window.TakePopup();

        Assert.NotNull(popup);
        Assert.Equal("Class", popup!.Value.Header);
        Assert.Equal(AutoPlaytestWindowText.Default.ClassHelp, popup.Value.Body);
    }

    [Fact]
    public void RightClickingASliderAsksForItsHelp()
    {
        AutoPlaytestWindow window = Create();

        Rect groove = window.PlaythroughSlider.GrooveArea;

        Assert.False(window.ProcessEvents(RightPress(new Point(groove.X + 20, groove.Y + (groove.Height / 2)))));

        DialogPopup? popup = window.TakePopup();

        Assert.NotNull(popup);
        Assert.Equal("Number of playthroughs:", popup!.Value.Header);
        Assert.Equal("Set the number of playthroughs.", popup.Value.Body);
    }

    [Fact]
    public void MissingSliderFramesRefuseToBuildTheWindow()
    {
        AutoPlaytestArtwork artwork = CreateArtwork();

        AutoPlaytestArtwork incomplete = new()
        {
            TitleBoxFrames = artwork.TitleBoxFrames,
            SliderFrames = [],
            CheckboxBox = artwork.CheckboxBox,
            PlayerIcons = artwork.PlayerIcons,
            Buttons = artwork.Buttons,
        };

        Assert.Null(AutoPlaytestWindow.TryCreate(CreatePlayers(), new PlaytestSettings(), null, incomplete,
            Font(), Font(), Font(), null, Screen));
    }

    private static AutoPlaytestWindow Create()
    {
        AutoPlaytestWindow? window = AutoPlaytestWindow.TryCreate(
            CreatePlayers(),
            new PlaytestSettings(maxPlaythroughs: 4, maxDays: 30, animationSpeed: 8, animationEnabled: true,
                environmentSounds: false),
            null, CreateArtwork(), Font(), Font(), Font(), null, Screen);

        return Assert.IsType<AutoPlaytestWindow>(window);
    }

    private static IReadOnlyList<AutoPlaytestPlayer> CreatePlayers() =>
    [
        new AutoPlaytestPlayer(PlayerColor.Blue, MapRace.Knight),
        new AutoPlaytestPlayer(PlayerColor.Green, MapRace.Warlock),
    ];

    private static AutoPlaytestArtwork CreateArtwork() => new()
    {
        TitleBoxFrames = [Solid(200, 30, 10)],
        SliderFrames =
        [
            Solid(15, 20, 1),
            Solid(200, 20, 2),
            Solid(15, 11, 3),
            Solid(15, 20, 4),
            Solid(15, 20, 5),
            Solid(15, 20, 6),
            Solid(15, 20, 7),
        ],
        CheckboxBox = Solid(12, 12, 8),
        CheckboxTick = Solid(8, 8, 9),
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

    /// <summary>A press inside <paramref name="position"/>, the state a drag starts with.</summary>
    private static LocalEvent Press(Point position)
    {
        var events = new LocalEvent();

        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);

        return events;
    }

    /// <summary>A full press and release inside the same point.</summary>
    private static LocalEvent Click(Point position)
    {
        var events = new LocalEvent();

        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

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
        public DialogButtonArtwork Get(DialogButton button) =>
            new(Solid(30, 20, 30), Solid(30, 20, 31));
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
