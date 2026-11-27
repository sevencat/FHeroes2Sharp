using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The split window, <c>Dialog::ArmySplitTroop()</c> (<c>dialog_selectcount.cpp:462-651</c>): the number, the row of
/// slot icons and the two answers the reference returns.
/// </summary>
/// <remarks>
/// 帧底图来自 <c>BUYBUILD</c>，所以造窗的用例需要真实的归档；机器上没有游戏数据时它们直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class ArmySplitViewTests
{
    private const byte Canvas = 0;
    private const byte FrameColor = 33;
    private const byte ButtonColor = 44;
    private const byte IconColor = 55;

    private static readonly Size EditBoxSize = new(68, 19);
    private static readonly Size ArrowSize = new(20, 10);
    private static readonly Size IconSize = new(30, 36);

    [Fact]
    public void TryCreate_LaysOutOneIconPerWayOfSplitting() => Assert.True(TryWithArchives(
        () => CreateView(freeSlots: 5),
        view =>
        {
            // freeSlots - 1 = 4 张图：2、3、4、5 格。
            Assert.Equal(4, view.SlotAreas.Count);

            // 数字框在文字区左边 70 像素处，标题与说明之后。
            Assert.Equal(view.Area.X + ArmySplitView.ValueInsetX, view.Value.Area.X);

            Assert.Equal(4, view.Value.Maximum);
            Assert.Equal(1, view.Value.Minimum);
            Assert.Equal(2, view.Value.Value);
        }));

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheIcons() => Assert.True(TryWithArchivesNullable(
        () => CreateView(freeSlots: 5, icons: 2),
        view => Assert.Null(view)));

    [Fact]
    public void WithoutAChosenSlotTheAnswerIsTwoSlotsAndNoFastSplit() => Assert.True(TryWithArchives(
        () => CreateView(freeSlots: 5),
        view =>
        {
            Assert.Null(view.SelectedSlot);
            Assert.Equal(2, view.SlotCount);
            Assert.False(view.UseFastSplit);
        }));

    [Fact]
    public void TheInitialFastSplitSelectsTheFirstIcon() => Assert.True(TryWithArchives(
        () => CreateView(freeSlots: 5, useFastSplit: true),
        view =>
        {
            Assert.Equal(0, view.SelectedSlot);
            Assert.Equal(2, view.SlotCount);
            Assert.True(view.UseFastSplit);
        }));

    [Fact]
    public void ClickingAnIconChoosesThatManySlots() => Assert.True(TryWithArchives(
        () => CreateView(freeSlots: 5),
        view =>
        {
            Point third = view.SlotAreas[2].Center;

            view.SetLeftButton(true, third);
            view.SetLeftButton(false, third);

            Assert.Equal(2, view.SelectedSlot);
            Assert.Equal(4, view.SlotCount);
            Assert.True(view.UseFastSplit);
        }));

    [Fact]
    public void TheNumberStartsAtTheSuggestedCountAndTheArrowsMoveIt() => Assert.True(TryWithArchives(
        () => CreateView(freeSlots: 5, initialCount: 3),
        view =>
        {
            Assert.Equal(3, view.Count);

            Point up = view.Value.UpButton.Area.Center;

            view.SetLeftButton(true, up);
            view.SetLeftButton(false, up);

            Assert.Equal(4, view.Count);
        }));

    [Fact]
    public void OkAndCancelComeFromTheButtonsAndTheHotKeys() => Assert.True(TryWithArchives(
        () => CreateView(freeSlots: 5),
        view =>
        {
            Point okay = view.Buttons[0].Area.Center;

            view.SetLeftButton(true, okay);
            Assert.Equal(DialogButton.Ok, view.SetLeftButton(false, okay));

            Assert.Equal(DialogButton.Cancel, view.ProcessHotKey(ButtonHotKey.Cancel));
        }));

    [Fact]
    public void Draw_PaintsTheFrameTheIconsAndTheSelectionBorder() => Assert.True(TryWithArchives(
        () => CreateView(freeSlots: 5, useFastSplit: true),
        view =>
        {
            PixelBuffer target = new(640, 480);

            view.Draw(target);

            int painted = 0;

            for (int y = view.FrameArea.Y; y < view.FrameArea.Bottom; ++y)
            {
                for (int x = view.FrameArea.X; x < view.FrameArea.Right; ++x)
                {
                    painted += target[x, y] != Canvas ? 1 : 0;
                }
            }

            Assert.True(painted > 1_000, $"the frame only painted {painted} pixels");

            Rect first = view.SlotAreas[0];

            Assert.Equal(IconColor, target[first.X + (first.Width / 2), first.Y + (first.Height / 2)]);
            Assert.Equal(ArmySplitView.SelectionBorderColor, target[first.X, first.Y]);
        }));

    private static ArmySplitView? CreateView(int freeSlots, int initialCount = 2, bool useFastSplit = false,
        int? icons = null) =>
        ArmySplitView.TryCreate(
            ArmySplitDialog.Create(freeSlots, 4, "Peasant", initialCount, useFastSplit),
            new FixedArtwork(), CreateFont(FrameColor), CreateFont(ButtonColor), CreateTownWindowFrames(),
            CreateIcons(icons ?? Math.Max(0, freeSlots - 1)));

    private static IReadOnlyList<Sprite> CreateIcons(int count)
    {
        List<Sprite> icons = new(count);

        for (int index = 0; index < count; ++index)
        {
            icons.Add(new Sprite(Filled(IconSize, IconColor)));
        }

        return icons;
    }

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

    private static IGlyphSource CreateFont(byte color) => new BlockGlyphSource(color);

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static bool TryWithArchives(Func<ArmySplitView?> create, Action<ArmySplitView> assert) =>
        TryWithArchivesNullable(create, view => assert(Assert.IsType<ArmySplitView>(view)));

    private static bool TryWithArchivesNullable(Func<ArmySplitView?> create, Action<ArmySplitView?> assert)
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

    private sealed class FixedArtwork : IDialogButtonArtworkSource
    {
        public DialogButtonArtwork Get(DialogButton button) =>
            new(new Sprite(Filled(new Size(60, 25), ButtonColor)), new Sprite(Filled(new Size(60, 25), 45)));
    }

    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockGlyphSource(byte color) => _glyph = new Sprite(Filled(new Size(6, 15), color));

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
