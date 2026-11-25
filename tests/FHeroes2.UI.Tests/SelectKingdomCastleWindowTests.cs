using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="SelectKingdomCastleWindow"/> and <see cref="SelectKingdomCastleDialog"/>, the reference's
/// <c>SelectKingdomCastle</c> / <c>selectKingdomCastle()</c> (<c>dialog_selectitems.cpp:791-904</c>).
/// </summary>
/// <remarks>
/// 归进 <see cref="GlobalArchiveCollection"/>：最后一条测试要用真实的 <c>Assets.Current</c>，而其它读归档的测试
/// 也在换这个全局量，并行跑会互相清掉对方的数据。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class SelectKingdomCastleWindowTests
{
    private const byte Canvas = 3;

    private const byte PlateColour = 9;

    private const byte IconColour = 7;

    [Fact]
    public void DialogSizeKeepsRoomForFiveToTwelveRows()
    {
        // ItemSelectionWindow 里面的 100 + itemsOffsetY * n，夹在 5 行与 12 行之间，再受屏幕高度减 150 限制。
        Assert.Equal(new Size(350, 275), SelectKingdomCastleDialog.DialogSize(3));
        Assert.Equal(new Size(350, 330), SelectKingdomCastleDialog.DialogSize(20));
        Assert.Equal(new Size(350, 380), SelectKingdomCastleDialog.DialogSize(8, new Size(800, 600)));
    }

    [Fact]
    public void DialogSizeRejectsANegativeCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectKingdomCastleDialog.DialogSize(-1));
    }

    [Fact]
    public void TryCreateReturnsNullForAnEmptyList()
    {
        SelectKingdomCastleWindow? window = SelectKingdomCastleDialog.TryCreate(Array.Empty<Castle>(),
            new StubSprites(), default, "Town Portal", "Select town to port to.", CreateArtwork(),
            CreateFont(), CreateFont());

        Assert.Null(window);
    }

    [Fact]
    public void TryCreateRejectsNullArguments()
    {
        StubSprites sprites = new();

        Assert.Throws<ArgumentNullException>(() => SelectKingdomCastleDialog.TryCreate(null!, sprites, default,
            "Town Portal", "Select town to port to.", CreateArtwork(), CreateFont(), CreateFont()));
        Assert.Throws<ArgumentNullException>(() => SelectKingdomCastleDialog.TryCreate(CreateTowns(), null!, default,
            "Town Portal", "Select town to port to.", CreateArtwork(), CreateFont(), CreateFont()));
        Assert.Throws<ArgumentNullException>(() => SelectKingdomCastleDialog.TryCreate(CreateTowns(), sprites, default,
            "Town Portal", "Select town to port to.", null!, CreateFont(), CreateFont()));
        Assert.Throws<ArgumentNullException>(() => SelectKingdomCastleDialog.TryCreate(CreateTowns(), sprites, default,
            "Town Portal", "Select town to port to.", CreateArtwork(), null!, CreateFont()));
        Assert.Throws<ArgumentNullException>(() => SelectKingdomCastleDialog.TryCreate(CreateTowns(), sprites, default,
            "Town Portal", "Select town to port to.", CreateArtwork(), CreateFont(), null!));
    }

    [Fact]
    public void WindowListsTheTownsInTheOrderTheCallerGaveThem()
    {
        SelectKingdomCastleWindow window = CreateWindow(CreateTowns());

        // 350x275 居中于 640x480：(640 - 350) / 2 = 145、(480 - 275) / 2 = 102。
        Assert.Equal(new Rect(145, 102, 350, 275), window.Window.ActiveArea);
        Assert.Equal(3, window.Towns.Count);
        Assert.Equal(100, window.Towns[0].Id);
        Assert.Equal(300, window.Towns[2].Id);

        // 列表区在活动区里 inset 10、top 30、width - 40、height - 70，再减去描述行的高与间隔；
        // 行区再 inset 5。行高 35，所以五行放得下。
        Assert.Equal(window.Window.ActiveArea.X + 10, window.ListArea.X);
        Assert.Equal(window.Window.ActiveArea.Width - 40, window.ListArea.Width);
        Assert.Equal(window.ListArea.X + 5, window.ItemsArea.X);
        Assert.Equal(window.ListArea.Y + 5, window.ItemsArea.Y);
        Assert.Equal(5, window.ItemsArea.Height / SelectKingdomCastleWindow.RowHeight);
        Assert.Equal(5, window.VisibleItemCount);
    }

    [Fact]
    public void WindowStartsOnTheFirstRowUnlessATownIsAskedFor()
    {
        Assert.Equal(100, CreateWindow(CreateTowns()).SelectedPositionIndex);

        Assert.Equal(300, CreateWindow(CreateTowns(), castlePositionIndex: 300).SelectedPositionIndex);
    }

    [Fact]
    public void SelectTownTakesAGateTileIndexAndLeavesTheSelectionOnATownThatIsNotListed()
    {
        SelectKingdomCastleWindow window = CreateWindow(CreateTowns());

        window.SelectTown(300);
        Assert.Equal(300, window.SelectedPositionIndex);

        // 参考的 SetCurrent( castlePositionIndex ) 收的是城堡的坐标，列表里没有就什么都不改。
        window.SelectTown(12345);
        Assert.Equal(300, window.SelectedPositionIndex);
    }

    [Fact]
    public void RowPaintsThePlateCentredThirtyFiveIntoTheRowAndTheIconFourPixelsIn()
    {
        SelectKingdomCastleWindow window = CreateWindow(CreateTowns());
        PixelBuffer image = CreateBuffer(400, 200, Canvas);
        int rowTop = window.ItemsArea.Y;

        window.RedrawItem(window.Towns[0], 10, rowTop, current: false, image);

        // 54x30 的底板以 x + 35 居中、以行中点 17 居中：10 + 35 - 27 = 18、rowTop + 17 - 15 = rowTop + 2。
        Assert.Equal(PlateColour, image[18, rowTop + 2]);
        Assert.Equal(Canvas, image[17, rowTop + 2]);

        // 城堡图标是 drawCastleIcon( castle, castleIcon, { 4, 4 } )，即底板左上角往内四像素，46x22 大小。
        Assert.Equal(IconColour, image[22, rowTop + 6]);
        Assert.Equal(IconColour, image[22 + 45, rowTop + 6 + 21]);

        // 底板右侧留给 CSLMARKER 的那一条，没有标记时是底板底色。
        Assert.Equal(PlateColour, image[18 + 40, rowTop + 2]);
    }

    [Fact]
    public void RowLetsTheTownNameStartSeventyFivePixelsIntoIt()
    {
        SelectKingdomCastleWindow window = CreateWindow(CreateTowns());
        PixelBuffer image = CreateBuffer(400, 200, Canvas);
        int rowTop = window.ItemsArea.Y;
        int textTop = rowTop - (CreateFont().LineHeight / 2) + SelectKingdomCastleWindow.RowMiddleOffsetY + 2;

        window.RedrawItem(window.Towns[0], 10, rowTop, current: false, image);

        // 假字体的每个字形都是实心的 6x12，从 x + 75 起画，行内垂直居中。
        Assert.Equal(1, image[10 + SelectKingdomCastleWindow.TextOffsetX, textTop]);
        Assert.Equal(Canvas, image[9 + SelectKingdomCastleWindow.TextOffsetX, textTop]);
    }

    [Fact]
    public void RightClickOnARowAsksForTheQuickInfoOfThatTown()
    {
        List<Castle> described = new();
        SelectKingdomCastleWindow window = CreateWindow(CreateTowns(), castleInfo: described.Add);
        Point point = new(window.ItemsArea.X + 20, window.ItemsArea.Y + 10);

        window.ProcessEvents(RightClick(point));

        Assert.Single(described);
        Assert.Equal(100, described[0].TileIndex);
    }

    [Fact]
    public void TheRealArchivesHaveTheArtworkAndThePlatesTheWindowNeeds()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            IcnViewWorldSpriteSource sprites = new(assets);

            // 小按钮是 generateDefaultImages() 现拼的，没有归档条目；装配能过说明 Assets.GetFileName 的接线在。
            Assert.NotNull(ItemSelectionWindowArtwork.TryCreate(sprites, evilInterface: false));
            Assert.NotNull(ItemSelectionWindowArtwork.TryCreate(sprites, evilInterface: true));

            Sprite plate = Assert.IsType<Sprite>(
                sprites.GetSprite(IcnType.Locators, SelectKingdomCastleWindow.TownFrame));

            Assert.Equal(SelectKingdomCastleWindow.PlateWidth, plate.Width);
            Assert.Equal(SelectKingdomCastleWindow.PlateHeight, plate.Height);

            // 每族城堡图标都要取得到。宽度按帧自带（大部分 46，个别族多一个像素），高度统一 22。
            foreach (MapRace race in new[]
                     {
                         MapRace.Knight, MapRace.Barbarian, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard,
                         MapRace.Necromancer,
                     })
            {
                if (IconBar.CastleFrame(race, hasFort: true) is { } frame)
                {
                    Sprite icon = Assert.IsType<Sprite>(sprites.GetSprite(IcnType.Locators, frame));

                    Assert.InRange(icon.Width, 46, 47);
                    Assert.Equal(22, icon.Height);
                }
            }
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    private static IReadOnlyList<Castle> CreateTowns() =>
    [
        new Castle(100, MapRace.Knight) { Name = "Ravenshore" },
        new Castle(200, MapRace.Wizard) { Name = "Celeste" },
        new Castle(300, MapRace.Necromancer) { Name = "Corge" },
    ];

    private static SelectKingdomCastleWindow CreateWindow(IReadOnlyList<Castle> towns,
        int castlePositionIndex = -1, Action<Castle>? castleInfo = null)
    {
        return Assert.IsType<SelectKingdomCastleWindow>(SelectKingdomCastleDialog.TryCreate(towns,
            CreateSprites(), default, "Town Portal", "Select town to port to.", CreateArtwork(),
            CreateFont(), CreateFont(), castlePositionIndex, castleInfo: castleInfo));
    }

    private static StubSprites CreateSprites()
    {
        StubSprites sprites = new();

        foreach (MapRace race in Enum.GetValues<MapRace>())
        {
            // 有城与无城的两帧都可能被问，测试的城堡是哪种取决于 Castle::isCastle()，两帧都放上。
            foreach (bool hasFort in new[] { true, false })
            {
                if (IconBar.CastleFrame(race, hasFort) is { } frame)
                {
                    sprites.Add(IcnType.Locators, frame, new Sprite(CreateBuffer(46, 22, IconColour)));
                }
            }
        }

        sprites.Add(IcnType.Locators, SelectKingdomCastleWindow.TownFrame,
            new Sprite(CreateBuffer(SelectKingdomCastleWindow.PlateWidth,
                SelectKingdomCastleWindow.PlateHeight, PlateColour)));

        return sprites;
    }

    private static ItemSelectionWindowArtwork CreateArtwork()
    {
        return new ItemSelectionWindowArtwork
        {
            ScrollBarBackground = new Sprite(CreateBuffer(560, 320, 9)),
            ScrollUpReleased = new Sprite(CreateBuffer(14, 15, 11)),
            ScrollUpPressed = new Sprite(CreateBuffer(14, 15, 12)),
            ScrollDownReleased = new Sprite(CreateBuffer(14, 15, 13)),
            ScrollDownPressed = new Sprite(CreateBuffer(14, 15, 14)),
            ScrollSlider = new Sprite(CreateBuffer(10, 25, 15)),
            Okay = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 16)),
                new Sprite(CreateBuffer(40, 20, 17))),
            Cancel = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 18)),
                new Sprite(CreateBuffer(40, 20, 19))),
        };
    }

    private static PixelBuffer CreateBuffer(int width, int height, byte value)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill(value);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new FakeGlyphSource();

    private static LocalEvent RightClick(Point point)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, point);

        return events;
    }

    /// <summary>The sprites a test needs, and only those.</summary>
    private sealed class StubSprites : IViewWorldSpriteSource
    {
        private readonly Dictionary<(IcnType Icn, int Index), Sprite> _sprites = [];

        public void Add(IcnType icn, int index, Sprite sprite) => _sprites[(icn, index)] = sprite;

        public Sprite? GetSprite(IcnType icn, int index) =>
            _sprites.TryGetValue((icn, index), out Sprite? sprite) ? sprite : null;
    }

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = new(CreateBuffer(6, 12, 1));

        public int LineHeight => 12;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
