using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The main window of the system options (<c>dialog_system_options.cpp</c>): layout, the five leaves and the four
/// settings that are changed in place.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class SystemOptionsDialogTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void WithoutEveryFrameThereIsNoWindow()
    {
        Assert.False(Artwork(spanel: 21).IsComplete);
        Assert.Null(SystemOptionsDialog.TryCreate(Screen, Artwork(spanel: 21)));

        Assert.False(Artwork(cspanel: 7).IsComplete);
        Assert.False(Artwork(gameOption: 1).IsComplete);
        Assert.True(Artwork().IsComplete);
    }

    [Fact]
    public void TheWindowIsTheSizeOfTheReference()
    {
        SystemOptionsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(SystemOptionsDialog.Width, active.Width);
        Assert.Equal(SystemOptionsDialog.Height, active.Height);
        Assert.Equal(289, active.Width);
        Assert.Equal((OptionItem.OptionsStepY * 3) + 52, active.Height);
    }

    [Fact]
    public void TheNineIconsSitAtTheReferenceOffsets()
    {
        SystemOptionsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;

        Assert.Equal(SystemOptionsDialog.LanguageArea.Offset(active.X, active.Y), dialog.LanguageRoi);
        Assert.Equal(SystemOptionsDialog.GraphicsArea.Offset(active.X, active.Y), dialog.GraphicsRoi);
        Assert.Equal(SystemOptionsDialog.AudioArea.Offset(active.X, active.Y), dialog.AudioRoi);
        Assert.Equal(SystemOptionsDialog.HeroSpeedArea.Offset(active.X, active.Y), dialog.HeroSpeedRoi);
        Assert.Equal(SystemOptionsDialog.EnemySpeedArea.Offset(active.X, active.Y), dialog.EnemySpeedRoi);
        Assert.Equal(SystemOptionsDialog.HotKeysArea.Offset(active.X, active.Y), dialog.HotKeysRoi);
        Assert.Equal(SystemOptionsDialog.InterfaceArea.Offset(active.X, active.Y), dialog.InterfaceRoi);
        Assert.Equal(SystemOptionsDialog.TextSupportArea.Offset(active.X, active.Y), dialog.TextSupportRoi);
        Assert.Equal(SystemOptionsDialog.BattlesArea.Offset(active.X, active.Y), dialog.BattlesRoi);

        Assert.Equal(92, dialog.GraphicsRoi.X - dialog.LanguageRoi.X);
        Assert.Equal(110, dialog.HeroSpeedRoi.Y - dialog.LanguageRoi.Y);
        Assert.Equal(110, dialog.InterfaceRoi.Y - dialog.HeroSpeedRoi.Y);
    }

    [Fact]
    public void ClickingAnEntryThatOpensAPageReturnsIt()
    {
        SystemOptionsDialog dialog = Create();

        Assert.Equal(SystemOptionsAction.Language, Click(dialog, dialog.LanguageRoi));
        Assert.Equal(SystemOptionsAction.Graphics, Click(dialog, dialog.GraphicsRoi));
        Assert.Equal(SystemOptionsAction.AudioSettings, Click(dialog, dialog.AudioRoi));
        Assert.Equal(SystemOptionsAction.HotKeys, Click(dialog, dialog.HotKeysRoi));
        Assert.Equal(SystemOptionsAction.InterfaceSettings, Click(dialog, dialog.InterfaceRoi));
    }

    [Fact]
    public void EnterLeavesTheWindow()
    {
        SystemOptionsDialog dialog = Create();

        LocalEvent enter = new();
        enter.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(SystemOptionsAction.Exit, dialog.ProcessEvents(enter));

        LocalEvent escape = new();
        escape.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);

        Assert.Equal(SystemOptionsAction.Exit, dialog.ProcessEvents(escape));
    }

    [Fact]
    public void TheHeroSpeedCyclesInPlace()
    {
        Settings settings = Settings.Get();
        settings.SetHeroesMoveSpeed(10);
        SystemOptionsDialog dialog = Create();

        try
        {
            // 到顶后回到 1，参考的 `% 10 + 1`。
            Assert.Equal(SystemOptionsAction.None, Click(dialog, dialog.HeroSpeedRoi));
            Assert.Equal(1, settings.HeroesMoveSpeed);
            Assert.True(dialog.IsDirty);

            Click(dialog, dialog.HeroSpeedRoi);
            Assert.Equal(2, settings.HeroesMoveSpeed);
        }
        finally
        {
            settings.SetHeroesMoveSpeed(4);
        }
    }

    [Fact]
    public void TheWheelOverTheHeroSpeedStepsIt()
    {
        Settings settings = Settings.Get();
        settings.SetHeroesMoveSpeed(5);
        SystemOptionsDialog dialog = Create();

        try
        {
            Assert.Equal(SystemOptionsAction.None, Wheel(dialog, dialog.HeroSpeedRoi, up: true));
            Assert.Equal(6, settings.HeroesMoveSpeed);

            Wheel(dialog, dialog.HeroSpeedRoi, up: false);
            Assert.Equal(5, settings.HeroesMoveSpeed);
        }
        finally
        {
            settings.SetHeroesMoveSpeed(4);
        }
    }

    [Fact]
    public void TheEnemySpeedWrapsAroundAndCanHideTheMoves()
    {
        Settings settings = Settings.Get();
        int wasSpeed = settings.AiMoveSpeed;
        SystemOptionsDialog dialog = Create();

        try
        {
            settings.SetAiMoveSpeed(10);
            Click(dialog, dialog.EnemySpeedRoi);

            Assert.Equal(0, settings.AiMoveSpeed);

            Click(dialog, dialog.EnemySpeedRoi);
            Assert.Equal(1, settings.AiMoveSpeed);
        }
        finally
        {
            settings.SetAiMoveSpeed(wasSpeed);
        }
    }

    [Fact]
    public void TheTextSupportModeTogglesInPlace()
    {
        Settings settings = Settings.Get();
        bool wasEnabled = settings.IsTextSupportMode;
        SystemOptionsDialog dialog = Create();

        try
        {
            Assert.Equal(SystemOptionsAction.None, Click(dialog, dialog.TextSupportRoi));
            Assert.NotEqual(wasEnabled, settings.IsTextSupportMode);
            Assert.True(dialog.IsDirty);
        }
        finally
        {
            settings.SetTextSupportMode(wasEnabled);
        }
    }

    [Fact]
    public void TheBattlesEntryWalksThroughTheThreeModes()
    {
        Settings settings = Settings.Get();
        bool wasResolve = settings.IsBattleAutoResolve;
        bool wasSpellcast = settings.IsBattleAutoSpellcast;
        SystemOptionsDialog dialog = Create();

        try
        {
            settings.SetBattleAutoResolve(false);
            settings.SetBattleAutoSpellcast(false);

            Click(dialog, dialog.BattlesRoi);
            Assert.True(settings.IsBattleAutoResolve);
            Assert.True(settings.IsBattleAutoSpellcast);

            Click(dialog, dialog.BattlesRoi);
            Assert.True(settings.IsBattleAutoResolve);
            Assert.False(settings.IsBattleAutoSpellcast);

            Click(dialog, dialog.BattlesRoi);
            Assert.False(settings.IsBattleAutoResolve);
            Assert.True(dialog.IsDirty);
        }
        finally
        {
            settings.SetBattleAutoResolve(wasResolve);
            settings.SetBattleAutoSpellcast(wasSpellcast);
        }
    }

    [Fact]
    public void RightClicksAskForTheExplanations()
    {
        SystemOptionsDialog dialog = Create();

        RightClick(dialog, dialog.HeroSpeedRoi);

        DialogPopup first = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Hero Speed", first.Header);
        Assert.NotNull(dialog.TakePopup());
        Assert.Null(dialog.Popup);

        RightClick(dialog, dialog.BattlesRoi);

        DialogPopup second = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Battles", second.Header);
    }

    private static SystemOptionsDialog Create() =>
        SystemOptionsDialog.TryCreate(Screen, Artwork())
        ?? throw new InvalidOperationException("The window should have been created.");

    private static SystemOptionsAction Click(SystemOptionsDialog dialog, Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + 4, area.Y + 4);

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return dialog.ProcessEvents(events);
    }

    private static SystemOptionsAction RightClick(SystemOptionsDialog dialog, Rect area)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, new Point(area.X + 4, area.Y + 4));

        return dialog.ProcessEvents(events);
    }

    private static SystemOptionsAction Wheel(SystemOptionsDialog dialog, Rect area, bool up)
    {
        LocalEvent events = new();

        events.OnMouseMotion(new Point(area.X + 4, area.Y + 4));
        events.OnMouseWheel(new Point(0, up ? 1 : -1));

        return dialog.ProcessEvents(events);
    }

    private static SystemOptionsArtwork Artwork(int spanel = 22, int cspanel = 8, int gameOption = 2) =>
        new()
        {
            SpanelFrames = [.. Enumerable.Range(0, spanel).Select(_ => new Sprite(65, 65))],
            CspanelFrames = [.. Enumerable.Range(0, cspanel).Select(_ => new Sprite(65, 65))],
            GameOptionFrames = [.. Enumerable.Range(0, gameOption).Select(_ => new Sprite(65, 65))],
            Buttons = new FakeButtonArtworkSource(),
        };

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
}
