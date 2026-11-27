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
/// The editor's file menu: the seven generated plates of <c>BUTTONS_EDITOR_FILE_DIALOG</c>, the small cancel button
/// and the actions of <c>EditorInterface::eventFileDialog()</c> (<c>editor_interface.cpp:2068-2181</c>).
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorFileDialogTests
{
    private const byte Canvas = 3;
    private const byte Button = 50;
    private const byte CancelButton = 60;

    private static readonly Size ButtonSize = new(90, 34);
    private static readonly Size CancelSize = new(45, 22);
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TryCreate_LaysTheSevenButtonsOutInThreeColumns()
    {
        EditorFileDialog dialog = Create();

        Assert.Equal(EditorFileDialog.ButtonCount, dialog.Buttons.Count);

        // 七颗是三列三行的最后一格空着（ui_window.cpp:505-511）：前六颗正好两行，第七颗自己一行。
        Assert.Equal(dialog.Buttons[0].Area.Y, dialog.Buttons[1].Area.Y);
        Assert.Equal(dialog.Buttons[0].Area.Y, dialog.Buttons[2].Area.Y);
        Assert.True(dialog.Buttons[1].Area.X > dialog.Buttons[0].Area.X);
        Assert.True(dialog.Buttons[2].Area.X > dialog.Buttons[1].Area.X);

        // 最后一行只有一颗，参考把它居中（ui_window.cpp 的「不满一行居中」），不是靠左。
        Rect active = dialog.Window.ActiveArea;
        Rect last = dialog.Buttons[6].Area;

        Assert.True(last.Y > dialog.Buttons[0].Area.Y);
        Assert.Equal(active.X + ((active.Width - last.Width) / 2), last.X);
    }

    [Fact]
    public void TryCreate_NamesTheButtonsInTheOrderOfTheReference()
    {
        EditorFileDialog dialog = Create();

        Assert.Equal(DialogButton.NewGame, dialog.Buttons.ValueOf(0));
        Assert.Equal(DialogButton.LoadGame, dialog.Buttons.ValueOf(1));
        Assert.Equal(DialogButton.EditorStartMap, dialog.Buttons.ValueOf(2));
        Assert.Equal(DialogButton.SaveGame, dialog.Buttons.ValueOf(3));
        Assert.Equal(DialogButton.EditorToMainMenu, dialog.Buttons.ValueOf(4));
        Assert.Equal(DialogButton.Quit, dialog.Buttons.ValueOf(5));
        Assert.Equal(DialogButton.EditorAutoPlaytest, dialog.Buttons.ValueOf(6));

        Assert.Equal(EditorFileDialog.ButtonCount, EditorFileDialog.ButtonValues.Count);
        Assert.Equal(EditorFileDialog.ButtonCount, EditorFileDialog.ButtonLabels.Count);
    }

    [Fact]
    public void TryCreate_PutsTheCancelButtonAtTheBottomCentre()
    {
        EditorFileDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;
        Rect cancel = dialog.Cancel.Area;

        Assert.Equal(active.X + ((active.Width - cancel.Width) / 2), cancel.X);
        Assert.Equal(active.Bottom - EditorFileDialog.CancelOffset.Y, cancel.Bottom);
    }

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheFrames()
    {
        Assert.Null(EditorFileDialog.TryCreate(Screen, null, CreateSprites(2, CancelSize, CancelButton)));
        Assert.Null(EditorFileDialog.TryCreate(Screen, CreateSprites(EditorFileDialog.ButtonFrameCount, ButtonSize, Button), null));
        Assert.Null(EditorFileDialog.TryCreate(
            Screen,
            CreateSprites(EditorFileDialog.ButtonFrameCount - 1, ButtonSize, Button),
            CreateSprites(2, CancelSize, CancelButton)));
        Assert.Null(EditorFileDialog.TryCreate(
            Screen,
            CreateSprites(EditorFileDialog.ButtonFrameCount, ButtonSize, Button),
            CreateSprites(1, CancelSize, CancelButton)));
    }

    [Fact]
    public void ProcessEvents_AnswersTheClickedButton()
    {
        EditorFileDialog dialog = Create();

        Assert.Equal(EditorFileResult.NewMap, Click(dialog, dialog.NewMapRoi));
        Assert.Equal(EditorFileResult.LoadMap, Click(dialog, dialog.LoadMapRoi));
        Assert.Equal(EditorFileResult.StartMap, Click(dialog, dialog.StartMapRoi));
        Assert.Equal(EditorFileResult.SaveMap, Click(dialog, dialog.SaveMapRoi));
        Assert.Equal(EditorFileResult.MainMenu, Click(dialog, dialog.MainMenuRoi));
        Assert.Equal(EditorFileResult.Quit, Click(dialog, dialog.QuitRoi));
        Assert.Equal(EditorFileResult.AutoPlaytest, Click(dialog, dialog.AutoPlaytestRoi));
        Assert.Equal(EditorFileResult.Cancel, Click(dialog, dialog.Cancel.Area));
    }

    [Fact]
    public void ProcessEvents_IgnoresAReleaseOutsideTheButton()
    {
        EditorFileDialog dialog = Create();
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Left, new Point(dialog.NewMapRoi.X + 4, dialog.NewMapRoi.Y + 4));
        events.OnMouseButton(false, MouseButtonType.Left, new Point(dialog.LoadMapRoi.X + 4, dialog.LoadMapRoi.Y + 4));

        Assert.Equal(EditorFileResult.None, dialog.ProcessEvents(events));
    }

    [Fact]
    public void ProcessEvents_AnswersTheHotKeys()
    {
        Assert.Equal(EditorFileResult.Cancel, HotKey(Key.Escape, KeyModifier.None));
        Assert.Equal(EditorFileResult.Cancel, HotKey(Key.Enter, KeyModifier.None));
        Assert.Equal(EditorFileResult.NewMap, HotKey(Key.N, KeyModifier.Ctrl));
        Assert.Equal(EditorFileResult.LoadMap, HotKey(Key.L, KeyModifier.Ctrl));
        Assert.Equal(EditorFileResult.SaveMap, HotKey(Key.S, KeyModifier.Ctrl));
        Assert.Equal(EditorFileResult.Quit, HotKey(Key.Q, KeyModifier.Ctrl));
        Assert.Equal(EditorFileResult.MainMenu, HotKey(Key.M, KeyModifier.Ctrl));
    }

    [Fact]
    public void RightClicksAskForTheExplanations()
    {
        EditorFileDialog dialog = Create();

        AssertExplanation(dialog, dialog.NewMapRoi, "New Map");
        AssertExplanation(dialog, dialog.LoadMapRoi, "Load Map");
        AssertExplanation(dialog, dialog.SaveMapRoi, "Save Map");
        AssertExplanation(dialog, dialog.QuitRoi, "Quit");
        AssertExplanation(dialog, dialog.AutoPlaytestRoi, "Auto Playtest");
        AssertExplanation(dialog, dialog.MainMenuRoi, "Main Menu");
        AssertExplanation(dialog, dialog.StartMapRoi, "Start Map");
        AssertExplanation(dialog, dialog.Cancel.Area, "Cancel");
    }

    [Fact]
    public void FromDialogButton_MapsTheSevenButtons()
    {
        Assert.Equal(EditorFileResult.NewMap, EditorFileDialog.FromDialogButton(DialogButton.NewGame));
        Assert.Equal(EditorFileResult.LoadMap, EditorFileDialog.FromDialogButton(DialogButton.LoadGame));
        Assert.Equal(EditorFileResult.StartMap, EditorFileDialog.FromDialogButton(DialogButton.EditorStartMap));
        Assert.Equal(EditorFileResult.SaveMap, EditorFileDialog.FromDialogButton(DialogButton.SaveGame));
        Assert.Equal(EditorFileResult.MainMenu, EditorFileDialog.FromDialogButton(DialogButton.EditorToMainMenu));
        Assert.Equal(EditorFileResult.Quit, EditorFileDialog.FromDialogButton(DialogButton.Quit));
        Assert.Equal(EditorFileResult.AutoPlaytest, EditorFileDialog.FromDialogButton(DialogButton.EditorAutoPlaytest));

        // 这颗菜单不用的位（游戏那张文件菜单的）什么都不算。
        Assert.Equal(EditorFileResult.None, EditorFileDialog.FromDialogButton(DialogButton.QuickSave));
        Assert.Equal(EditorFileResult.None, EditorFileDialog.FromDialogButton(DialogButton.None));
    }

    [Fact]
    public void Draw_PaintsTheWindowTheButtonsAndTheCancelButton()
    {
        EditorFileDialog dialog = Create();
        PixelBuffer canvas = new(Screen.Width, Screen.Height);
        canvas.Fill(Canvas);

        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            dialog.Draw(canvas);
        }
        finally
        {
            GameAssets.Current = null;
        }

        Point first = CentreOf(dialog.NewMapRoi);

        Assert.Equal(Button, canvas[first.X, first.Y]);
        Assert.Equal(CancelButton, canvas[CentreOf(dialog.Cancel.Area).X, CentreOf(dialog.Cancel.Area).Y]);
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
            // 真路径：七块牌子由 ButtonFactory 用标签压出来，取消键取 BUTTON_SMALL_CANCEL 的头两帧。
            List<Sprite> frames = ButtonFactory.MakeSymmetricBackgroundSprites(
                EditorFileDialog.ButtonLabels, isEvilInterface: false, EditorFileDialog.MinimumButtonWidth);
            string cancelName = GameAssets.GetFileName(IcnType.ButtonSmallCancelGood);

            Sprite? released = GameAssets.GetImage(cancelName, 0);
            Sprite? pressed = GameAssets.GetImage(cancelName, 1);

            Assert.NotNull(released);
            Assert.NotNull(pressed);

            EditorFileDialog? dialog = EditorFileDialog.TryCreate(Screen, frames, [released!, pressed!]);

            Assert.NotNull(dialog);
            Assert.Equal(EditorFileDialog.ButtonCount, dialog!.Buttons.Count);
            Assert.True(dialog.Buttons[0].Area.Width >= EditorFileDialog.MinimumButtonWidth);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        Assert.Throws<ArgumentNullException>(() => Create().Draw(null!));
    }

    [Fact]
    public void ProcessEvents_RejectsAMissingEventState()
    {
        Assert.Throws<ArgumentNullException>(() => Create().ProcessEvents(null!));
    }

    private static EditorFileDialog Create() => EditorFileDialog.TryCreate(
        Screen,
        CreateSprites(EditorFileDialog.ButtonFrameCount, ButtonSize, Button),
        CreateSprites(2, CancelSize, CancelButton))!;

    private static EditorFileResult Click(EditorFileDialog dialog, Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + 4, area.Y + 4);

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return dialog.ProcessEvents(events);
    }

    private static EditorFileResult HotKey(Key key, KeyModifier modifier)
    {
        LocalEvent events = new();

        events.OnKeyboard(key, modifier, KeyboardEventState.Down);

        return Create().ProcessEvents(events);
    }

    private static void AssertExplanation(EditorFileDialog dialog, Rect area, string header)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, new Point(area.X + 4, area.Y + 4));

        Assert.Equal(EditorFileResult.None, dialog.ProcessEvents(events));

        DialogPopup popup = dialog.Popup ?? throw new InvalidOperationException($"A popup was expected over {header}.");

        Assert.Equal(header, popup.Header);
        Assert.False(string.IsNullOrWhiteSpace(popup.Body));

        // TakePopup 读完就清掉：说明框每帧由宿主重建，留着会一直弹。
        Assert.NotNull(dialog.TakePopup());
        Assert.Null(dialog.Popup);
    }

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
}
