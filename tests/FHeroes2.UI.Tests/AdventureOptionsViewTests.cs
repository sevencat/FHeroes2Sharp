using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

[Collection(GlobalArchiveCollection.Name)]
public sealed class AdventureOptionsViewTests
{
    private const byte Canvas = 3;
    private const byte Button = 50;
    private const byte CancelButton = 60;

    private static readonly Size ButtonSize = new(59, 27);
    private static readonly Size CancelSize = new(45, 22);

    [Fact]
    public void TryCreate_LaysTheFourButtonsAndTheCancelButtonOut()
    {
        AdventureOptionsView view = CreateView();

        // Four buttons of 59x27: two columns 20 pixels apart, 30 pixels of padding, and 15 pixels down
        // from the active area of 198x146 at (221,167).
        Assert.Equal(4, view.Buttons.Count);
        Assert.Equal(new Point(251, 182), TopLeftOf(view.Buttons[0]));
        Assert.Equal(new Point(330, 182), TopLeftOf(view.Buttons[1]));
        Assert.Equal(new Point(251, 229), TopLeftOf(view.Buttons[2]));
        Assert.Equal(new Point(330, 229), TopLeftOf(view.Buttons[3]));

        // The cancel button sits at the bottom centre, 11 pixels above the border.
        Assert.Equal(new Point(297, 280), TopLeftOf(view.Cancel));
    }

    [Fact]
    public void TryCreate_NamesTheButtonsInPanelOrder()
    {
        AdventureOptionsView view = CreateView();

        Assert.Equal(DialogButton.World, view.Buttons.ValueOf(0));
        Assert.Equal(DialogButton.Puzzle, view.Buttons.ValueOf(1));
        Assert.Equal(DialogButton.Info, view.Buttons.ValueOf(2));
        Assert.Equal(DialogButton.Dig, view.Buttons.ValueOf(3));
    }

    [Fact]
    public void TryCreate_TakesReleasedAndPressedFramesInPairs()
    {
        AdventureOptionsView view = CreateView();

        Rect button = view.Buttons[0].Area;
        Rect cancel = view.Cancel.Area;

        Assert.Equal(ButtonSize.Width, button.Width);
        Assert.Equal(ButtonSize.Height, button.Height);
        Assert.Equal(CancelSize.Width, cancel.Width);
        Assert.Equal(CancelSize.Height, cancel.Height);
    }

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheButtons()
    {
        Assert.Null(AdventureOptionsView.TryCreate(null, CreateSprites(5, CancelSize, CancelButton)));
        Assert.Null(AdventureOptionsView.TryCreate(
            CreateSprites(6, ButtonSize, Button), CreateSprites(5, CancelSize, CancelButton)));
        Assert.Null(AdventureOptionsView.TryCreate(
            CreateSprites(8, ButtonSize, Button), CreateSprites(4, CancelSize, CancelButton)));
    }

    [Fact]
    public void Draw_PaintsTheWindowTheButtonsAndTheCancelButton()
    {
        AdventureOptionsView view = CreateView();
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(Canvas);

        // The stone frame of the window comes from the archive, so the game data has to be open.
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

        // The frame is 16 pixels thick from (205,151) and the interior is tiled stone. The first button
        // covers (251,182)-(309,208) and the cancel button (297,280)-(341,301).
        Assert.NotEqual(Canvas, canvas[206, 300]);
        Assert.NotEqual(Canvas, canvas[250, 170]);
        Assert.Equal(Button, canvas[270, 195]);
        Assert.Equal(CancelButton, canvas[310, 290]);
        Assert.Equal(Canvas, canvas[0, 0]);
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        AdventureOptionsView view = CreateView();

        Assert.Throws<ArgumentNullException>(() => view.Draw(null!));
    }

    [Fact]
    public void SetLeftButton_AnswersTheButtonThatWasPressed()
    {
        AdventureOptionsView view = CreateView();

        Assert.Equal(DialogButton.None, view.SetLeftButton(true, new Point(260, 190)));
        Assert.Equal(DialogButton.World, view.SetLeftButton(false, new Point(260, 190)));
    }

    [Fact]
    public void SetLeftButton_ReadsTheRightButtonOfTheGroup()
    {
        AdventureOptionsView view = CreateView();

        view.SetLeftButton(true, new Point(340, 190));

        Assert.Equal(DialogButton.Puzzle, view.SetLeftButton(false, new Point(340, 190)));
    }

    [Fact]
    public void SetLeftButton_IgnoresAReleaseOutsideTheButton()
    {
        AdventureOptionsView view = CreateView();

        view.SetLeftButton(true, new Point(260, 190));

        // Pressed on the world button, released on the puzzle one: neither counts.
        Assert.Equal(DialogButton.None, view.SetLeftButton(false, new Point(340, 190)));
    }

    [Fact]
    public void SetLeftButton_AnswersCancelFromTheCancelButton()
    {
        AdventureOptionsView view = CreateView();

        Assert.Equal(DialogButton.None, view.SetLeftButton(true, new Point(320, 290)));
        Assert.Equal(DialogButton.Cancel, view.SetLeftButton(false, new Point(320, 290)));
    }

    [Fact]
    public void SetLeftButton_IgnoresAReleaseOutsideTheDialog()
    {
        AdventureOptionsView view = CreateView();

        view.SetLeftButton(true, new Point(260, 190));

        Assert.Equal(DialogButton.None, view.SetLeftButton(false, new Point(0, 0)));
    }

    private static AdventureOptionsView CreateView() =>
        AdventureOptionsView.TryCreate(
            CreateSprites(8, ButtonSize, Button), CreateSprites(5, CancelSize, CancelButton))!;

    /// <summary>Opens the game data the stone frame of the window is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

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

    private static Point TopLeftOf(Widgets.ButtonBase button) => new(button.Area.X, button.Area.Y);
}
