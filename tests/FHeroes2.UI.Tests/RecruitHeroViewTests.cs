using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the recruit hero dialog, <c>Castle::DialogBuyHero()</c> (<c>castle_town.cpp:66-160</c>).
/// </summary>
/// <remarks>
/// 需要 AGG 数据的几条在缺数据时直接返回；布局与按钮的几条不依赖资产，任何环境都会跑。
/// </remarks>
public sealed class RecruitHeroViewTests
{
    private const byte HeaderColor = 90;
    private const byte BodyColor = 200;
    private const byte ButtonColor = 250;

    [Fact]
    public void ContentHeightAddsEveryPartOfTheDialog()
    {
        // 与建造框不同，标题不占用领先的 10 像素：15 + 10 + 105 + 10 + 30 + 10 + 45。
        Assert.Equal(225, RecruitHeroLayout.GetContentHeight(15, 105, 30, 45));
    }

    [Fact]
    public void LayoutPutsThePartsInTheReferenceOrder()
    {
        Rect area = new(100, 100, 244, 300);

        RecruitHeroLayout layout = RecruitHeroLayout.Create(area, new Size(111, 105), titleHeight: 15, descriptionHeight: 30);

        Assert.Equal(new Point(100, 102), layout.Title);

        // 111 像素宽的相框在 244 像素里居中：100 + (244 - 111) / 2 = 166。
        Assert.Equal(new Point(166, 125), layout.Frame);
        Assert.Equal(new Point(171, 131), layout.Portrait);
        Assert.Equal(new Rect(166, 125, 111, 105), layout.FrameArea);
        Assert.Equal(new Point(100, 242), layout.Description);
        Assert.Equal(new Point(100, 280), layout.Cost);
    }

    [Fact]
    public void TheButtonsFollowWhetherTheTownMayHire()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        RecruitHeroView mayHire = CreateView(assets, CreateDialog(canRecruit: true));

        Assert.True(mayHire.OkayEnabled);
        Assert.True(mayHire.Buttons[0].IsEnabled);
        Assert.True(mayHire.Buttons[1].IsEnabled);
        Assert.Equal(RecruitHeroDialog.Title, RecruitHeroDialog.Title);
        Assert.Equal(2, mayHire.Buttons.Count);

        RecruitHeroView mayNot = CreateView(assets, CreateDialog(canRecruit: false));

        Assert.False(mayNot.OkayEnabled);
        Assert.False(mayNot.Buttons[0].IsEnabled);
        Assert.True(mayNot.Buttons[1].IsEnabled);
    }

    [Fact]
    public void OkayUnderThePointerAnswersOk()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        RecruitHeroView view = CreateView(assets, CreateDialog(canRecruit: true));
        Point okay = Middle(view.Buttons[0].Area);
        Point cancel = Middle(view.Buttons[1].Area);

        // 按钮组在松开那一刻回答，按下只是记录。
        view.SetCursor(cancel);
        Assert.Equal(DialogButton.None, view.SetLeftButton(true));
        Assert.Equal(DialogButton.Cancel, view.SetLeftButton(false));

        view.SetCursor(okay);
        Assert.Equal(DialogButton.None, view.SetLeftButton(true));
        Assert.Equal(DialogButton.Ok, view.SetLeftButton(false));
    }

    [Fact]
    public void TheDialogIsPaintedInsideItsFrame()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        RecruitHeroView view = CreateView(assets, CreateDialog(canRecruit: true));
        PixelBuffer target = new(640, 480);
        target.Fill(0);

        view.Draw(target);

        bool painted = false;
        for (int y = view.FrameArea.Y; y < view.FrameArea.Bottom && !painted; ++y)
        {
            for (int x = view.FrameArea.X; x < view.FrameArea.Right; ++x)
            {
                if (target[x, y] != 0)
                {
                    painted = true;
                    break;
                }
            }
        }

        Assert.True(painted);
        Assert.Equal(0, target[0, 0]);
        Assert.Equal(0, target[639, 479]);
    }

    [Fact]
    public void TheFrameIsCentredInTheDialog()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        RecruitHeroView view = CreateView(assets, CreateDialog(canRecruit: true));
        RecruitHeroLayout layout = view.Layout;

        Assert.Equal(view.Area.X + ((view.Area.Width - 111) / 2), layout.Frame.X);

        // 标题在框的正上方，价格框在说明之下，两者都贴在左边缘。
        Assert.Equal(view.Area.X, layout.Title.X);
        Assert.Equal(view.Area.X, layout.Cost.X);
        Assert.True(layout.Title.Y < layout.Frame.Y);
        Assert.True(layout.Frame.Y < layout.Description.Y);
        Assert.True(layout.Description.Y < layout.Cost.Y);
        Assert.True(layout.Cost.Y + ResourceCostBox.GetHeight(view.Dialog.Cost) <= view.Area.Bottom);
    }

    /// <summary>A hero the dialog can show: Lord Kilburn, level one, nothing in the bag.</summary>
    /// <param name="canRecruit">Whether the town may hire, i.e. <c>AllowBuyHero()</c>.</param>
    /// <returns>The dialog.</returns>
    private static RecruitHeroDialog CreateDialog(bool canRecruit) =>
        RecruitHeroDialog.Create(new Hero(Hero.LordKilburn, MapRace.Knight), canRecruit);

    private static RecruitHeroView CreateView(AssetBundle assets, RecruitHeroDialog dialog)
    {
        RecruitHeroArtwork? artwork = RecruitHeroArtwork.TryCreate(assets);

        Assert.NotNull(artwork);

        RecruitHeroView? view = RecruitHeroView.TryCreate(
            dialog,
            artwork,
            portrait: null,
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor),
            new FakeGlyphSource(BodyColor),
            new FakeButtonArtworkSource());

        Assert.NotNull(view);

        return view;
    }

    private static Point Middle(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>A glyph source with the metrics of <c>FONT.ICN</c> (15 pixel line, 6 pixel advance).</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
        {
            PixelBuffer image = new(6, LineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight => 15;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }

    /// <summary>The same flat sprite for every standard button and both of its looks.</summary>
    private sealed class FakeButtonArtworkSource : IDialogButtonArtworkSource
    {
        private readonly DialogButtonArtwork _artwork;

        public FakeButtonArtworkSource()
        {
            PixelBuffer image = new(40, 20);
            image.Fill(ButtonColor);
            Sprite sprite = new(image, 0, 0);
            _artwork = new DialogButtonArtwork(sprite, sprite);
        }

        public DialogButtonArtwork Get(DialogButton button) => _artwork;
    }
}
