using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the building dialog of a town, <c>Dialog::BuyBuilding()</c> (<c>buildinginfo.cpp:435-537</c>),
/// and of the price box under it, <c>Resource::BoxSprite</c> (<c>resource.cpp:603-664</c>).
/// </summary>
/// <remarks>
/// 需要 AGG 数据的几条在缺数据时直接返回；布局与价格框的几条不依赖资产，任何环境都会跑。
/// </remarks>
public sealed class BuildDialogTests
{
    private const byte HeaderColor = 90;
    private const byte BodyColor = 200;
    private const byte ButtonColor = 250;

    /// <summary>Enough of everything for the dialog tests that only care about the status.</summary>
    private static readonly Funds Rich =
        new(ore: 100, wood: 100, mercury: 100, sulfur: 100, crystal: 100, gems: 100, gold: 100_000);

    [Fact]
    public void ContentHeightAddsEveryPartOfTheDialog()
    {
        Assert.Equal(215, BuildDialogLayout.GetContentHeight(70, 30, 15, 15, 45, requirementsPresent: true));
        Assert.Equal(175, BuildDialogLayout.GetContentHeight(70, 30, 15, 15, 45, requirementsPresent: false));
    }

    [Fact]
    public void LayoutPutsThePartsInTheReferenceOrder()
    {
        Rect area = new(100, 100, 244, 300);

        BuildDialogLayout layout = BuildDialogLayout.Create(
            area,
            new Size(135, 70),
            new Size(100, 60),
            new Size(80, 13),
            descriptionHeight: 30,
            new Size(60, 15),
            requirementTextHeight: 15,
            requirementsPresent: true);

        Assert.Equal(new Point(154, 110), layout.Frame);
        Assert.Equal(new Point(172, 111), layout.Image);
        Assert.Equal(new Point(182, 170), layout.Name);
        Assert.Equal(new Point(100, 192), layout.Description);
        Assert.Equal(new Point(192, 232), layout.RequirementTitle);
        Assert.Equal(new Point(100, 247), layout.RequirementText);
        Assert.Equal(new Point(100, 270), layout.Cost);
    }

    [Fact]
    public void WithoutARequirementThePriceFollowsTheDescription()
    {
        BuildDialogLayout layout = BuildDialogLayout.Create(
            new Rect(100, 100, 244, 300),
            new Size(135, 70),
            new Size(100, 60),
            new Size(80, 13),
            descriptionHeight: 30,
            new Size(60, 15),
            requirementTextHeight: 15,
            requirementsPresent: false);

        Assert.Null(layout.RequirementTitle);
        Assert.Null(layout.RequirementText);
        Assert.Equal(new Point(100, 230), layout.Cost);
    }

    [Theory]
    [InlineData(0, ResourceCostBox.SingleRowHeight)]
    [InlineData(1, ResourceCostBox.SingleRowHeight)]
    [InlineData(3, ResourceCostBox.SingleRowHeight)]
    [InlineData(4, ResourceCostBox.DoubleRowHeight)]
    [InlineData(6, ResourceCostBox.DoubleRowHeight)]
    [InlineData(7, ResourceCostBox.TripleRowHeight)]
    public void PriceBoxHeightFollowsTheNumberOfRows(int count, int height) =>
        Assert.Equal(height, ResourceCostBox.GetHeight(count));

    [Fact]
    public void PriceEntriesUseTheIconOrderOfTheReference()
    {
        Funds cost = new(ore: 3, wood: 1, mercury: 2, sulfur: 4, crystal: 5, gems: 6, gold: 7);

        ResourceCostBox.Entry[] expected =
        [
            new(ResourceCostBox.WoodFrame, 1),
            new(ResourceCostBox.OreFrame, 3),
            new(ResourceCostBox.MercuryFrame, 2),
            new(ResourceCostBox.SulfurFrame, 4),
            new(ResourceCostBox.CrystalFrame, 5),
            new(ResourceCostBox.GemsFrame, 6),
            new(ResourceCostBox.GoldFrame, 7),
        ];

        Assert.Equal(expected, ResourceCostBox.GetEntries(cost).ToArray());
    }

    [Fact]
    public void SevenPricesTakeThreeRows()
    {
        // 三个一行的循环跑满两行，最后一项单独占第三行的中间一格（244 / 3 = 81，格子中心 40 + 81）。
        Assert.Equal(new Point(40, 35), ResourceCostBox.GetEntryPosition(244, 7, 0).Center);
        Assert.Equal(new Point(40, 80), ResourceCostBox.GetEntryPosition(244, 7, 3).Center);
        Assert.Equal(new Point(121, 125), ResourceCostBox.GetEntryPosition(244, 7, 6).Center);
    }

    [Fact]
    public void FitToOneRowKeepsATextThatFits()
    {
        Assert.Equal("AAA", BuildDialogView.FitToOneRow(new FakeGlyphSource(BodyColor), "AAA", 125));
        Assert.Equal(new string('A', 20), BuildDialogView.FitToOneRow(new FakeGlyphSource(BodyColor), new string('A', 20), 125));
    }

    [Fact]
    public void FitToOneRowCutsWithAnEllipsis()
    {
        // 六像素一个字符：17 个字符加省略号是 120 像素，18 个字符就超过了 125。
        Assert.Equal(
            string.Concat(new string('A', 17), "..."),
            BuildDialogView.FitToOneRow(new FakeGlyphSource(BodyColor), new string('A', 21), 125));
    }

    [Fact]
    public void ARichTownCanBuild()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        Castle castle = CreateTown(MapRace.Knight);
        BuildingInfo info = new(castle, BuildingType.Well, Rich);

        Assert.Equal(BuildingStatus.AllowBuild, info.Status);

        BuildDialogView view = CreateView(assets, info);

        Assert.True(view.OkayEnabled);
        Assert.True(view.Buttons[0].IsEnabled);
        Assert.Equal("Well", view.Name);
        Assert.Null(view.Requirement);
        Assert.Equal(info.Description, view.Description);
    }

    [Fact]
    public void APoorTownIsToldWhatItCannotAfford()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        Castle castle = CreateTown(MapRace.Knight);
        BuildingInfo info = new(castle, BuildingType.Well, new Funds());

        Assert.Equal(BuildingStatus.LackResources, info.Status);

        BuildDialogView view = CreateView(assets, info);

        Assert.False(view.OkayEnabled);
        Assert.False(view.Buttons[0].IsEnabled);
        Assert.StartsWith(info.Description, view.Description, StringComparison.Ordinal);
        Assert.Contains("Cannot afford the Well.", view.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void APrerequisiteIsWrittenOnItsOwnRow()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        Castle castle = CreateTown(MapRace.Wizard);
        BuildingInfo info = new(castle, BuildingType.Spec, Rich);

        Assert.Equal(BuildingStatus.RequiresBuild, info.Status);

        BuildDialogView view = CreateView(assets, info);

        Assert.False(view.OkayEnabled);
        Assert.Equal("Mage Guild, Level 1", view.Requirement);
        Assert.NotNull(view.Layout.RequirementTitle);
        Assert.NotNull(view.Layout.RequirementText);
    }

    [Fact]
    public void ADisabledBuildingSkipsTheRequirementRow()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        Castle castle = CreateTown(MapRace.Knight);
        BuildingInfo info = new(castle, BuildingType.MageGuild2, Rich);

        Assert.Equal(BuildingStatus.BuildDisabled, info.Status);

        BuildDialogView view = CreateView(assets, info);

        Assert.False(view.OkayEnabled);
        Assert.Null(view.Requirement);
        Assert.Null(view.Layout.RequirementTitle);
        Assert.Equal("This building has been disabled.", view.Description);
    }

    [Fact]
    public void DrawPaintsTheFrameAndLeavesTheRestAlone()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        Castle castle = CreateTown(MapRace.Knight);
        BuildingInfo info = new(castle, BuildingType.Well, Rich);
        BuildDialogView view = CreateView(assets, info);

        Assert.True(view.FrameArea.X >= 0);
        Assert.True(view.FrameArea.Y >= 0);
        Assert.True(view.FrameArea.Right <= 640);
        Assert.True(view.FrameArea.Bottom <= 480);

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

    /// <summary>Builds a town with its palisade up, as a map's town would arrive.</summary>
    /// <param name="race">The race of the town.</param>
    /// <returns>The town, ready to build today.</returns>
    private static Castle CreateTown(MapRace race)
    {
        Castle castle = new(0, race);
        Funds treasury = Rich;

        Assert.True(castle.BuyBuilding(BuildingType.Castle, ref treasury));

        // Buying the castle spends the day's building permit; the dialog tests want it back.
        castle.ActionNewDay();

        return castle;
    }

    private static BuildDialogView CreateView(AssetBundle assets, BuildingInfo info)
    {
        BuildDialogArtwork? artwork = BuildDialogArtwork.TryCreate(assets, info.Castle.Race);

        Assert.NotNull(artwork);

        BuildDialogView? view = BuildDialogView.TryCreate(
            info,
            artwork,
            new FakeGlyphSource(HeaderColor),
            new FakeGlyphSource(BodyColor),
            new FakeButtonArtworkSource());

        Assert.NotNull(view);

        return view;
    }

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
