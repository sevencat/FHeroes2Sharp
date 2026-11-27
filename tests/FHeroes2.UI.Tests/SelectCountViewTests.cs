using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The <c>Dialog::SelectCount()</c> window (<c>dialog_selectcount.cpp:79-131</c>): a
/// <see cref="FrameBox"/> with a header, a <see cref="ValueSelectionElement"/> at 38 pixels from the
/// left of the text area and the OK/CANCEL row.
/// </summary>
/// <remarks>
/// The frame reads <c>BUYBUILD.ICN</c> itself, so the tests that build a window need the installed
/// archives and return when the machine has none.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class SelectCountViewTests
{
    private const byte Canvas = 0;
    private const byte FrameColor = 33;
    private const byte ButtonColor = 44;

    private static readonly Size EditBoxSize = new(68, 19);
    private static readonly Size ArrowSize = new(20, 10);

    [Fact]
    public void TryCreate_LaysOutTheWindow() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            // The frame is the 244 pixel wide text area of BUYBUILD.ICN: one 15 pixel line of the fake font,
            // the 10 + 30 of the layout and the 40 pixel button row, against a 70 pixel minimum.
            Assert.Equal(FrameBox.AreaWidth, view.Area.Width);

            int content = 15 + SelectCountView.ElementOffset + SelectCountView.SelectionAreaHeight +
                          FrameBox.ButtonAreaHeight;
            Assert.Equal(Math.Max(FrameBox.MinimumAreaHeight, content), view.Area.Height);

            Assert.Equal(view.Area.X + SelectCountView.SelectionInset, view.Value.Area.X);
            Assert.Equal(view.Area.Y + 15 + SelectCountView.ElementOffset, view.Value.Area.Y);
            Assert.Equal(new Size(94, 25), view.Value.Area.Size);
        }));

    [Fact]
    public void TryCreate_ReadsTheStepOfTheDialog() => Assert.True(TryWithArchives(
        () => SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick", 0, 100, 7), new FixedArtwork(), CreateFont(),
            CreateTownWindowFrames())!,
        view => Assert.Equal(7, view.Value.Step)));

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheNumberArtwork()
    {
        Assert.Null(SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick", 0, 5), new FixedArtwork(), CreateFont(), []));
    }

    [Fact]
    public void TryCreate_RejectsMissingArguments()
    {
        SelectCountDialog dialog = SelectCountDialog.Create("Pick", 0, 5);

        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            null!, new FixedArtwork(), CreateFont(), CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, null!, CreateFont(), CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, new FixedArtwork(), null!, CreateTownWindowFrames()));
        Assert.Throws<ArgumentNullException>(() => SelectCountView.TryCreate(
            dialog, new FixedArtwork(), CreateFont(), null!));
    }

    [Fact]
    public void SetLeftButton_OkWritesTheChosenNumber() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 3),
        view =>
        {
            Point okay = Center(view.Buttons[0].Area);

            Assert.Equal(DialogButton.None, view.SetLeftButton(true, okay));
            Assert.Equal(DialogButton.Ok, view.SetLeftButton(false, okay));

            Assert.Equal(3, view.Dialog.Result);
        }));

    [Fact]
    public void SetLeftButton_CancelLeavesNoNumber() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 3),
        view =>
        {
            Point cancel = Center(view.Buttons[1].Area);

            view.SetLeftButton(true, cancel);
            Assert.Equal(DialogButton.Cancel, view.SetLeftButton(false, cancel));

            Assert.Null(view.Dialog.Result);
        }));

    [Fact]
    public void SetLeftButton_ArrowThenOkReportsTheChangedNumber() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 3),
        view =>
        {
            Point up = Center(view.Value.UpButton.Area);

            view.SetLeftButton(true, up);
            view.SetLeftButton(false, up);

            Point okay = Center(view.Buttons[0].Area);
            view.SetLeftButton(true, okay);
            view.SetLeftButton(false, okay);

            Assert.Equal(4, view.Dialog.Result);
        }));

    [Fact]
    public void SetLeftButton_IgnoresTheHeaderArea() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            Assert.Equal(DialogButton.None,
                view.SetLeftButton(true, new Point(view.Area.X + 5, view.Area.Y + 5)));
            Assert.Equal(DialogButton.None,
                view.SetLeftButton(false, new Point(view.Area.X + 5, view.Area.Y + 5)));
            Assert.Null(view.Dialog.Result);
        }));

    [Fact]
    public void Draw_PaintsTheFrameAndTheButtons() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            PixelBuffer target = new(640, 480);

            view.Draw(target);

            // The frame artwork is opaque in the body of its columns but jagged along the outer edges,
            // so the test counts the pixels it paints instead of picking one.
            int painted = 0;

            for (int y = view.FrameArea.Y; y < view.FrameArea.Bottom; ++y)
            {
                for (int x = view.FrameArea.X; x < view.FrameArea.Right; ++x)
                {
                    painted += target[x, y] != Canvas ? 1 : 0;
                }
            }

            Assert.True(painted > 10_000, $"the frame only painted {painted} pixels");

            Point okay = Center(view.Buttons[0].Area);
            Assert.Equal(ButtonColor, target[okay.X, okay.Y]);
            Assert.Equal(Canvas, target[0, 0]);
        }));

    [Fact]
    public void Draw_RejectsAMissingTarget() => Assert.True(TryWithArchives(
        () => CreateView(),
        view => Assert.Throws<ArgumentNullException>(() => view.Draw(null!))));

    [Fact]
    public void TypingDigitsBuildsTheNumber() => Assert.True(TryWithArchives(
        () => SelectCountView.TryCreate(SelectCountDialog.Create("Pick", 0, 100), new FixedArtwork(),
            CreateFont(), CreateTownWindowFrames(), 2)!,
        view =>
        {
            // 参考读 typedValueBuf：先敲 1 再敲 2 就是 12，不是 1 然后 2。
            Assert.True(view.ProcessEvents(KeyPress(Key.Digit1)).Changed);
            Assert.Equal(1, view.Value.Value);

            Assert.True(view.ProcessEvents(KeyPress(Key.Digit2)).Changed);
            Assert.Equal(12, view.Value.Value);
        }));

    [Fact]
    public void ADigitOutsideTheRangeIsRefused() => Assert.True(TryWithArchives(
        () => SelectCountView.TryCreate(SelectCountDialog.Create("Pick", 1, 5), new FixedArtwork(),
            CreateFont(), CreateTownWindowFrames(), 2)!,
        view =>
        {
            // 缓冲是空的时候敲 9：5 的上限下 9 不合法，这一位被退回，数字不动。
            Assert.False(view.ProcessEvents(KeyPress(Key.Digit9)).Changed);
            Assert.Equal(2, view.Value.Value);
        }));

    [Fact]
    public void BackspaceTakesTheNumberBackToTheMinimum() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 4),
        view =>
        {
            Assert.True(view.ProcessEvents(KeyPress(Key.Backspace)).Changed);

            // 参考在 Backspace 分支答 clamp( 0, min, max )，这张图的下限是 1。
            Assert.Equal(1, view.Value.Value);
        }));

    [Fact]
    public void TheWheelOverTheEditBoxChangesTheNumber() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 2),
        view =>
        {
            LocalEvent wheel = new();

            wheel.OnMouseMotion(Center(view.Value.EditBox));
            wheel.OnMouseWheel(new Point(0, 1));

            Assert.True(view.ProcessEvents(wheel).Changed);
            Assert.Equal(3, view.Value.Value);
        }));

    [Fact]
    public void AClickOnTheEditBoxAsksForTheNumberPad() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            SelectCountFrame frame = view.ProcessEvents(Click(Center(view.Value.EditBox)));

            Assert.False(frame.Changed);
            Assert.Equal(ValueSelectionRequest.Numpad, frame.Request);
            Assert.Equal(DialogButton.None, frame.Answer);
        }));

    [Fact]
    public void ARightClickOnTheEditBoxAsksForTheRange() => Assert.True(TryWithArchives(
        () => CreateView(),
        view =>
        {
            SelectCountFrame frame = view.ProcessEvents(RightPress(Center(view.Value.EditBox)));

            Assert.Equal(ValueSelectionRequest.Range, frame.Request);
            Assert.Equal("The available values range from 1 to 5.", view.RangeMessage);
        }));

    [Fact]
    public void SetValueWritesTheAnswerOfTheNumberPad() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 1),
        view =>
        {
            view.SetValue(4);

            Assert.Equal(4, view.Value.Value);

            // 缓冲跟着清空：下一次敲数字是新的数，不会接在 4 后面。
            Assert.True(view.ProcessEvents(KeyPress(Key.Digit3)).Changed);
            Assert.Equal(3, view.Value.Value);
        }));

    [Fact]
    public void AnArrowClickClearsTheTypedDigits() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 1),
        view =>
        {
            view.ProcessEvents(KeyPress(Key.Digit2));
            Assert.Equal(2, view.Value.Value);

            // 箭头自己改了值之后，缓冲清空，否则再敲 3 会变成 23。
            LocalEvent click = Click(Center(view.Value.UpButton.Area));

            Assert.True(view.ProcessEvents(click).Changed);
            Assert.Equal(3, view.Value.Value);

            view.ProcessEvents(KeyPress(Key.Digit1));
            Assert.Equal(1, view.Value.Value);
        }));

    [Fact]
    public void AClickOnOkayReportsTheButton() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 3),
        view =>
        {
            SelectCountFrame frame = view.ProcessEvents(Click(Center(view.Buttons[0].Area)));

            Assert.Equal(DialogButton.Ok, frame.Answer);
            Assert.Equal(3, view.Dialog.Result);
        }));

    [Fact]
    public void TheWindowShowsMaxAtTheBottomOfTheRangeAndMinElsewhere() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 1),
        view =>
        {
            Assert.True(view.IsMaxVisible);
            Assert.False(view.IsMinVisible);

            view.SetValue(3);

            Assert.True(view.IsMinVisible);
            Assert.False(view.IsMaxVisible);
        }));

    [Fact]
    public void ClickingMaxJumpsToTheMaximumAndFlipsTheButton() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 1),
        view =>
        {
            // MAX 只在值等于下限时才显示，与参考的 SwitchMaxMinButtons 一致。
            Assert.True(view.IsMaxVisible);

            ButtonSprite max = Assert.IsType<ButtonSprite>(view.MaxButton);

            Assert.True(view.ProcessEvents(Click(Center(max.Area))).Changed);
            Assert.Equal(5, view.Value.Value);
            Assert.True(view.IsMinVisible);
        }));

    [Fact]
    public void ClickingMinJumpsBackToTheMinimum() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 2),
        view =>
        {
            Assert.True(view.IsMinVisible);

            ButtonSprite min = Assert.IsType<ButtonSprite>(view.MinButton);

            Assert.True(view.ProcessEvents(Click(Center(min.Area))).Changed);
            Assert.Equal(1, view.Value.Value);
            Assert.True(view.IsMaxVisible);

            // OKAY 交出的就是这颗按钮设下的值。
            _ = view.ProcessEvents(Click(Center(view.Buttons[0].Area)));
            Assert.Equal(1, view.Dialog.Result);
        }));

    [Fact]
    public void RightClickingMaxAndMinExplainsThem() => Assert.True(TryWithArchives(
        () => CreateView(initialValue: 1),
        view =>
        {
            ButtonSprite max = Assert.IsType<ButtonSprite>(view.MaxButton);

            SelectCountFrame frame = view.ProcessEvents(RightPress(Center(max.Area)));

            Assert.NotNull(frame.Popup);
            Assert.Equal("MAX", frame.Popup!.Value.Header);
            Assert.Equal("Click to select the maximum amount.", frame.Popup.Value.Body);

            view.SetValue(3);

            ButtonSprite min = Assert.IsType<ButtonSprite>(view.MinButton);
            frame = view.ProcessEvents(RightPress(Center(min.Area)));

            Assert.NotNull(frame.Popup);
            Assert.Equal("MIN", frame.Popup!.Value.Header);
            Assert.Equal("Click to select the minimum amount.", frame.Popup.Value.Body);
        }));

    [Fact]
    public void AWindowBuiltWithoutShortcutsHasNeitherButton() => Assert.True(TryWithArchives(
        () => SelectCountView.TryCreate(SelectCountDialog.Create("Pick", 1, 5), new FixedArtwork(), CreateFont(),
            CreateTownWindowFrames(), 2)!,
        view =>
        {
            Assert.Null(view.MaxButton);
            Assert.Null(view.MinButton);
            Assert.False(view.IsMaxVisible);
            Assert.False(view.IsMinVisible);
        }));

    [Fact]
    public void AWidgetAboveTheNumberMakesTheWindowTallerAndCentresIt() => Assert.True(TryWithArchives(
        () => CreateViewWithWidgets(top: new SolidElement(40, 20), bottom: null),
        view =>
        {
            // 上方部件占「高 20 + 间隔 10」，窗口因此比没有部件时高 30。
            Assert.Equal(15 + SelectCountView.ElementOffset + 20 + SelectCountView.SelectionAreaHeight +
                         SelectCountView.ElementOffset + FrameBox.ButtonAreaHeight, view.Area.Height);

            // 部件在文本区里水平居中，紧接在标题下面。
            Assert.Equal(view.Area.X + ((FrameBox.AreaWidth - 40) / 2), view.TopElementArea.X);
            Assert.Equal(view.Area.Y + 15 + SelectCountView.ElementOffset, view.TopElementArea.Y);

            // 数字在部件下方再隔一个 elementOffset。
            Assert.Equal(view.TopElementArea.Bottom + SelectCountView.ElementOffset, view.Value.Area.Y);
            Assert.Equal(Rect.Empty, view.BottomElementArea);
        }));

    [Fact]
    public void AWidgetBelowTheNumberSitsUnderIt() => Assert.True(TryWithArchives(
        () => CreateViewWithWidgets(top: null, bottom: new SolidElement(50, 18)),
        view =>
        {
            Assert.Equal(view.Value.Area.Bottom + SelectCountView.ElementOffset, view.BottomElementArea.Y);
            Assert.Equal(view.Area.X + ((FrameBox.AreaWidth - 50) / 2), view.BottomElementArea.X);
            Assert.Equal(Rect.Empty, view.TopElementArea);
        }));

    [Fact]
    public void BothWidgetsStackAroundTheNumber() => Assert.True(TryWithArchives(
        () => CreateViewWithWidgets(top: new SolidElement(40, 20), bottom: new SolidElement(50, 18)),
        view =>
        {
            Assert.True(view.TopElementArea.Bottom < view.Value.Area.Y);
            Assert.True(view.Value.Area.Bottom < view.BottomElementArea.Y);

            // 两个部件各占「高 + 间隔」，加上标题、数字与按钮行。
            Assert.Equal(15 + SelectCountView.ElementOffset + 20 + SelectCountView.SelectionAreaHeight + 18 +
                         (SelectCountView.ElementOffset * 2) + FrameBox.ButtonAreaHeight, view.Area.Height);
        }));

    [Fact]
    public void AClickOnTheWidgetIsHandedToIt() => Assert.True(TryWithArchives(
        () => CreateViewWithWidgets(top: new SolidElement(40, 20), bottom: null),
        view =>
        {
            SolidElement widget = Assert.IsType<SolidElement>(view.TopElement);

            // 参考只在「左键释放」与「右键按下」两种事件上转给部件。
            _ = view.ProcessEvents(Click(Center(view.TopElementArea)));

            Assert.Equal(1, widget.Clicks);
        }));

    [Fact]
    public void AWidgetOutsideTheClicksAreaSeesNothing() => Assert.True(TryWithArchives(
        () => CreateViewWithWidgets(top: new SolidElement(40, 20), bottom: null),
        view =>
        {
            SolidElement widget = Assert.IsType<SolidElement>(view.TopElement);

            // 点在数字框上：那是数字自己的区域，部件不该收到。
            _ = view.ProcessEvents(Click(Center(view.Value.EditBox)));

            Assert.Equal(0, widget.Clicks);
        }));

    private static LocalEvent KeyPress(Key key)
    {
        var events = new LocalEvent();

        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        return events;
    }

    private static LocalEvent Click(Point point)
    {
        var events = new LocalEvent();

        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        return events;
    }

    private static LocalEvent RightPress(Point point)
    {
        var events = new LocalEvent();

        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Right, point);

        return events;
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static SelectCountView CreateView(int initialValue = 2) =>
        SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick a number", 1, 5), new FixedArtwork(), CreateFont(),
            CreateTownWindowFrames(), initialValue, SelectCountShortcuts.TryCreate(isEvilInterface: false))!;

    private static SelectCountView CreateViewWithWidgets(DialogElement? top, DialogElement? bottom) =>
        SelectCountView.TryCreate(
            SelectCountDialog.Create("Pick a number", 1, 5), new FixedArtwork(), CreateFont(),
            CreateTownWindowFrames(), 2, shortcuts: null, topElement: top, bottomElement: bottom)!;

    /// <summary>TOWNWIND.ICN frames 4-8 with their real sizes.</summary>
    private static IReadOnlyList<Sprite> CreateTownWindowFrames()
    {
        Sprite[] frames = new Sprite[ValueSelectionElement.RequiredFrameCount];
        frames[ValueSelectionElement.EditBoxFrame] = new Sprite(Filled(EditBoxSize, FrameColor));

        for (int index = ValueSelectionElement.UpReleasedFrame; index < frames.Length; ++index)
        {
            frames[index] = new Sprite(Filled(ArrowSize, ButtonColor), 3, 2);
        }

        return frames;
    }

    private static PixelBuffer Filled(Size size, byte color)
    {
        PixelBuffer buffer = new(size.Width, size.Height);
        buffer.Fill(color);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new BlockGlyphSource();

    /// <summary>Opens the game data the message frame is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Runs a test against the real frame artwork.</summary>
    /// <param name="create">Builds the window.</param>
    /// <param name="assert">Checks it.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArchives(Func<SelectCountView> create, Action<SelectCountView> assert)
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

    /// <summary>OK and CANCEL in flat colours, so the drawn buttons can be found by pixel.</summary>
    private sealed class FixedArtwork : IDialogButtonArtworkSource
    {
        public DialogButtonArtwork Get(DialogButton button) =>
            new(new Sprite(Filled(new Size(60, 25), ButtonColor)), new Sprite(Filled(new Size(60, 25), 45)));
    }

    /// <summary>
    /// A caller widget of a fixed size that counts what reaches it, the <c>topUiElement</c> of the reference.
    /// </summary>
    private sealed class SolidElement : DialogElement
    {
        public SolidElement(int width, int height) =>
            Area = new Size(width, height);

        /// <summary>How many times the owner handed a frame to this element.</summary>
        public int Clicks { get; private set; }

        public override void Draw(PixelBuffer output, Point offset) =>
            ImageOps.BlitSprite(output, offset, new Sprite(Filled(new Size(Area.Width, Area.Height), FrameColor)));

        public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
        {
            Clicks++;

            return default;
        }
    }

    /// <summary>A 6x15 solid block font, which is enough for the header and the number.</summary>
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
