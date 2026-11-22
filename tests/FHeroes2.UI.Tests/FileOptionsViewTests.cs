using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The file menu: the two column layout of <c>dialog_file.cpp</c>, the disabled restart button and the
/// blocking <see cref="FileOptionsDialog.Show"/> loop.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class FileOptionsViewTests
{
    private const byte Canvas = 3;
    private const byte Button = 50;
    private const byte CancelButton = 60;

    private static readonly Size ButtonSize = new(80, 56);
    private static readonly Size CancelSize = new(45, 22);

    [Fact]
    public void TryCreate_LaysTheSixButtonsOutInThreeColumns()
    {
        FileOptionsView view = CreateView();

        Assert.Equal(6, view.Buttons.Count);

        // Six buttons are an even count of at most six, so the reference makes two rows of three
        // (ui_window.cpp:505-511), not three rows of two.
        Assert.Equal(view.Buttons[0].Area.Y, view.Buttons[1].Area.Y);
        Assert.Equal(view.Buttons[0].Area.Y, view.Buttons[2].Area.Y);
        Assert.True(view.Buttons[1].Area.X > view.Buttons[0].Area.X);
        Assert.True(view.Buttons[2].Area.X > view.Buttons[1].Area.X);

        Assert.True(view.Buttons[3].Area.Y > view.Buttons[0].Area.Y);
        Assert.Equal(view.Buttons[3].Area.Y, view.Buttons[5].Area.Y);
    }

    [Fact]
    public void TryCreate_NamesTheButtonsInPanelOrder()
    {
        FileOptionsView view = CreateView();

        Assert.Equal(DialogButton.NewGame, view.Buttons.ValueOf(0));
        Assert.Equal(DialogButton.LoadGame, view.Buttons.ValueOf(1));
        Assert.Equal(DialogButton.Restart, view.Buttons.ValueOf(2));
        Assert.Equal(DialogButton.SaveGame, view.Buttons.ValueOf(3));
        Assert.Equal(DialogButton.QuickSave, view.Buttons.ValueOf(4));
        Assert.Equal(DialogButton.Quit, view.Buttons.ValueOf(5));
    }

    [Fact]
    public void TryCreate_DisablesTheRestartButton()
    {
        FileOptionsView view = CreateView();

        Assert.False(view.Buttons[FileOptionsView.RestartButtonIndex].IsEnabled);
        Assert.True(view.Buttons[0].IsEnabled);

        Assert.True(FileOptionsView.TryCreate(
            CreateSprites(FileOptionsView.ButtonsFrameCount, ButtonSize, Button),
            CreateSprites(5, CancelSize, CancelButton),
            isRestartEnabled: true)!.Buttons[FileOptionsView.RestartButtonIndex].IsEnabled);
    }

    [Fact]
    public void TryCreate_PutsTheCancelButtonAtTheBottomCentre()
    {
        FileOptionsView view = CreateView();
        Rect active = view.ActiveArea;
        Rect cancel = view.Cancel.Area;

        // Same rule as the adventure options dialog: centred, lifted 11 pixels off the bottom border.
        Assert.Equal(active.X + ((active.Width - cancel.Width) / 2), cancel.X);
        Assert.True(cancel.Bottom <= active.Bottom);
    }

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheFrames()
    {
        Assert.Null(FileOptionsView.TryCreate(null, CreateSprites(5, CancelSize, CancelButton)));
        Assert.Null(FileOptionsView.TryCreate(
            CreateSprites(FileOptionsView.ButtonsFrameCount - 1, ButtonSize, Button), CreateSprites(5, CancelSize, CancelButton)));
        Assert.Null(FileOptionsView.TryCreate(
            CreateSprites(FileOptionsView.ButtonsFrameCount, ButtonSize, Button), CreateSprites(4, CancelSize, CancelButton)));
    }

    [Fact]
    public void SetLeftButton_AnswersThePressedButton()
    {
        FileOptionsView view = CreateView();
        Point point = CentreOf(view.Buttons[4].Area);

        Assert.Equal(DialogButton.None, view.SetLeftButton(true, point));
        Assert.Equal(DialogButton.QuickSave, view.SetLeftButton(false, point));
    }

    [Fact]
    public void SetLeftButton_IgnoresAReleaseOutsideTheButton()
    {
        FileOptionsView view = CreateView();

        view.SetLeftButton(true, CentreOf(view.Buttons[0].Area));

        // Pressed on "new game", released on "load game": neither counts.
        Assert.Equal(DialogButton.None, view.SetLeftButton(false, CentreOf(view.Buttons[1].Area)));
    }

    [Fact]
    public void SetLeftButton_IgnoresTheDisabledRestartButton()
    {
        FileOptionsView view = CreateView();
        Point point = CentreOf(view.Buttons[FileOptionsView.RestartButtonIndex].Area);

        view.SetLeftButton(true, point);

        Assert.Equal(DialogButton.None, view.SetLeftButton(false, point));
    }

    [Fact]
    public void SetLeftButton_AnswersCancelFromTheCancelButton()
    {
        FileOptionsView view = CreateView();
        Point point = CentreOf(view.Cancel.Area);

        Assert.Equal(DialogButton.None, view.SetLeftButton(true, point));
        Assert.Equal(DialogButton.Cancel, view.SetLeftButton(false, point));
    }

    [Fact]
    public void Draw_PaintsTheWindowTheButtonsAndTheCancelButton()
    {
        FileOptionsView view = CreateView();
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(Canvas);

        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            view.Draw(canvas);
        }
        finally
        {
            GameAssets.Current = null;
        }

        Assert.NotEqual(Canvas, canvas[CentreOf(view.Buttons[0].Area).X, CentreOf(view.Buttons[0].Area).Y]);
        Assert.Equal(Button, canvas[CentreOf(view.Buttons[0].Area).X, CentreOf(view.Buttons[0].Area).Y]);
        Assert.Equal(CancelButton, canvas[CentreOf(view.Cancel.Area).X, CentreOf(view.Cancel.Area).Y]);
        Assert.Equal(Canvas, canvas[0, 0]);
    }

    [Fact]
    public void TryCreate_AcceptsTheButtonsGeneratedFromTheArchive()
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            // The real path: ButtonFactory makes the twelve plates from the labels, the archive gives
            // the cancel button. Without game data the dialog cannot exist at all.
            List<Sprite> frames = ButtonFactory.MakeSymmetricBackgroundSprites(
                FileOptionsView.ButtonLabels, isEvilInterface: false, FileOptionsView.MinimumButtonWidth);
            IcnSpriteFile? system = assets.TryReadIcn(FileOptionsView.CancelAssetName);

            Assert.NotNull(system);

            FileOptionsView? view = FileOptionsView.TryCreate(frames, system.Sprites);

            Assert.NotNull(view);
            Assert.Equal(6, view!.Buttons.Count);
            Assert.True(view.Buttons[0].Area.Width >= FileOptionsView.MinimumButtonWidth);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        FileOptionsView view = CreateView();

        Assert.Throws<ArgumentNullException>(() => view.Draw(null!));
    }

    [Fact]
    public void FromDialogButton_MapsTheSixButtons()
    {
        Assert.Equal(FileOptionsResult.NewGame, FileOptionsDialog.FromDialogButton(DialogButton.NewGame));
        Assert.Equal(FileOptionsResult.LoadGame, FileOptionsDialog.FromDialogButton(DialogButton.LoadGame));
        Assert.Equal(FileOptionsResult.SaveGame, FileOptionsDialog.FromDialogButton(DialogButton.SaveGame));
        Assert.Equal(FileOptionsResult.QuickSave, FileOptionsDialog.FromDialogButton(DialogButton.QuickSave));
        Assert.Equal(FileOptionsResult.Quit, FileOptionsDialog.FromDialogButton(DialogButton.Quit));

        // The restart button has nothing to do and cancel is cancel.
        Assert.Equal(FileOptionsResult.Cancel, FileOptionsDialog.FromDialogButton(DialogButton.Restart));
        Assert.Equal(FileOptionsResult.Cancel, FileOptionsDialog.FromDialogButton(DialogButton.Cancel));
        Assert.Equal(FileOptionsResult.Cancel, FileOptionsDialog.FromDialogButton(DialogButton.None));
    }

    [Fact]
    public void Show_ThrowsWhenNoEngineIsAttached()
    {
        FileOptionsView view = CreateView();

        Assert.Throws<InvalidOperationException>(() =>
            FileOptionsDialog.Show(new PixelBuffer(640, 480), view, new LocalEvent(() => 0)));
    }

    [Fact]
    public void Show_ReturnsTheResultOfTheClickedButton()
    {
        FileOptionsView view = CreateView();
        Point point = CentreOf(view.Buttons[4].Area);
        PixelBuffer canvas = new(640, 480);

        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            LocalEvent events = new(() => 0);
            FakeEngine engine = new()
            {
                OnFrame = (state, frame) =>
                {
                    if (frame == 0)
                    {
                        state.OnMouseMotion(point);
                        state.OnMouseButton(true, MouseButtonType.Left, point);
                    }
                    else
                    {
                        state.OnMouseButton(false, MouseButtonType.Left, point);
                    }
                },
            };

            events.Engine = engine;

            Assert.Equal(FileOptionsResult.QuickSave, FileOptionsDialog.Show(canvas, view, events));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void Show_AnswersQuitWhenTheEngineStops()
    {
        FileOptionsView view = CreateView();
        PixelBuffer canvas = new(640, 480);

        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            LocalEvent events = new(() => 0) { Engine = new FakeEngine { FramesBeforeStop = 0 } };

            Assert.Equal(FileOptionsResult.Quit, FileOptionsDialog.Show(canvas, view, events));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    private static FileOptionsView CreateView() => FileOptionsView.TryCreate(
        CreateSprites(FileOptionsView.ButtonsFrameCount, ButtonSize, Button),
        CreateSprites(5, CancelSize, CancelButton))!;

    /// <summary>Opens the game data the stone frame of the window is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static Point CentreOf(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static Sprite[] CreateSprites(int count, Size size, byte color)
    {
        Sprite[] sprites = new Sprite[count];

        for (int index = 0; index < count; ++index)
        {
            PixelBuffer image = new(size.Width, size.Height);
            image.Fill(color);
            sprites[index] = new Sprite(image, 0, 0);
        }

        return sprites;
    }

    /// <summary>An event engine that plays a scripted frame at a time and stops after a few of them.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public int FramesBeforeStop { get; init; } = 8;

        public int Frames { get; private set; }

        public Action<LocalEvent, int>? OnFrame { get; set; }

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (Frames >= FramesBeforeStop)
            {
                return false;
            }

            OnFrame?.Invoke(state, Frames++);

            return true;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }
}
