using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="ArtifactSelectionWindow"/> and <see cref="ArtifactSelectDialog"/> against
/// <c>dialog_selectitems.cpp:537-572</c> and <c>:1162-1194</c>.
/// </summary>
public sealed class ArtifactSelectionWindowTests
{
    private const byte Canvas = 3;
    private const byte IconColor = 41;

    [Fact]
    public void DialogSizeLeavesTheDeductionOnTopOfTheScreenHeight()
    {
        Assert.Equal(new Size(370, 330), ArtifactSelectDialog.DialogSize());
        Assert.Equal(new Size(370, 450), ArtifactSelectDialog.DialogSize(new Size(800, 600)));
    }

    [Fact]
    public void TheListStartsWithTheMagicBookAndSkipsItLater()
    {
        List<int> artifacts = ArtifactSelectDialog.BuildArtifactList();

        Assert.Equal((int)ArtifactType.MagicBook, artifacts[0]);
        Assert.Single(artifacts, id => id == (int)ArtifactType.MagicBook);
        Assert.DoesNotContain((int)ArtifactType.Unknown, artifacts);
        Assert.DoesNotContain((int)ArtifactType.ArtifactCount, artifacts);
    }

    [Fact]
    public void TheVictoryListStartsWithTheUltimatePrizeAndLeavesTheScrollOut()
    {
        List<int> artifacts = ArtifactSelectDialog.BuildArtifactList(isForVictoryConditions: true);

        Assert.Equal((int)ArtifactType.EditorAnyUltimateArtifact, artifacts[0]);
        Assert.DoesNotContain((int)ArtifactType.SpellScroll, artifacts);
        Assert.Contains((int)ArtifactType.UltimateSword, artifacts);
    }

    [Fact]
    public void ASuccessionWarsMapLeavesTheExpansionArtifactsOut()
    {
        List<int> all = ArtifactSelectDialog.BuildArtifactList(MapVersion.PriceOfLoyalty);
        List<int> old = ArtifactSelectDialog.BuildArtifactList(MapVersion.SuccessionWars);

        Assert.Contains((int)ArtifactType.SpellScroll, all);
        Assert.DoesNotContain((int)ArtifactType.SpellScroll, old);
        Assert.True(old.Count < all.Count);
    }

    [Fact]
    public void TheLevelFilterKeepsOnlyThatTier()
    {
        List<int> ultimates = ArtifactSelectDialog.BuildArtifactList(level: ArtifactLevel.Ultimate);

        // 终极那八个都在，别的都不在；带等级筛选时列表里没有魔法书那一项。
        Assert.Equal(8, ultimates.Count);

        foreach (int id in ultimates)
        {
            Assert.True(ArtifactLevels.IsUltimate((ArtifactType)id));
        }

        Assert.DoesNotContain((int)ArtifactType.MagicBook, ultimates);

        List<int> treasures = ArtifactSelectDialog.BuildArtifactList(level: ArtifactLevel.Treasure);

        Assert.NotEmpty(treasures);

        foreach (int id in treasures)
        {
            Assert.Equal(ArtifactLevel.Treasure,
                ArtifactLevels.Level((ArtifactType)id, MapVersion.PriceOfLoyalty) & ArtifactLevel.Treasure);
        }
    }

    [Fact]
    public void TheWindowLaysItsRowsIntoTheListArea()
    {
        ArtifactSelectionWindow window = CreateWindow();

        // 370 宽的活动区在 640 上居中于 x=135，列表再左缩 10。
        Assert.Equal(new Rect(145, 105, 330, 260), window.ListArea);
        Assert.Equal(new Rect(150, 110, 320, 250), window.ItemsArea);
        Assert.Equal(250 / ArtifactSelectionWindow.RowHeight, window.VisibleItemCount);
    }

    [Fact]
    public void TryCreateStartsOnTheArtifactTheCallerAskedFor()
    {
        IReadOnlyList<int> ids = ArtifactSelectDialog.BuildArtifactList();
        ArtifactSelectionWindow? window = ArtifactSelectDialog.TryCreate(ArtifactType.UltimateSword,
            new FakeIcons(), CreateArtwork(), CreateFont(), CreateFont());

        Assert.NotNull(window);
        Assert.Equal(ids.Count, window.Artifacts.Count);
        Assert.Equal(ArtifactType.UltimateSword, window.SelectedArtifact);

        window.SelectArtifact(ArtifactType.Ankh);

        Assert.Equal(ArtifactType.Ankh, window.SelectedArtifact);
    }

    [Fact]
    public void TryCreateReturnsNullWhenTheLevelLeavesNothing()
    {
        // 等级掩码为 0 时没有任何神器匹配（( level & 0 ) == 0 恒成立），列表空、窗口就不开。
        ArtifactSelectionWindow? window = ArtifactSelectDialog.TryCreate(ArtifactType.Unknown,
            new FakeIcons(), CreateArtwork(), CreateFont(), CreateFont(), level: ArtifactLevel.None);

        Assert.Null(window);
    }

    [Fact]
    public void RedrawItemDrawsTheIconAndTheName()
    {
        ArtifactSelectionWindow window = CreateWindow();
        PixelBuffer image = CreateBuffer(640, 480, Canvas);

        window.RedrawItem(new ItemSelectionEntry((int)ArtifactType.UltimateSword), window.ItemsArea.X,
            window.ItemsArea.Y, current: false, image);

        int x = window.ItemsArea.X;
        int y = window.ItemsArea.Y;

        // 图标 32x32，中心落在 reference 的 middleImageOffsetX=22 与行中线上。
        Assert.Equal(IconColor,
            image[x + ArtifactSelectionWindow.IconOffsetX, y + ArtifactSelectionWindow.RowMiddleOffsetY]);
    }

    [Fact]
    public void ARightClickAsksForTheDescriptionOnce()
    {
        ArtifactSelectionWindow window = CreateWindow();
        Point point = new(window.ItemsArea.X + 60, window.ItemsArea.Y + 10);

        window.ProcessEvents(RightClick(point));

        Assert.Equal((ArtifactType)window.Artifacts[0].Id, window.TakeInfoRequest());
        Assert.Null(window.TakeInfoRequest());
        Assert.Equal(DialogButton.None, window.Result);
    }

    private static ArtifactSelectionWindow CreateWindow() =>
        new([(int)ArtifactType.MagicBook, (int)ArtifactType.UltimateSword, (int)ArtifactType.Ankh],
            new FakeIcons(), ArtifactSelectDialog.DialogSize(), CreateArtwork(), CreateFont(), CreateFont());

    private static ItemSelectionWindowArtwork CreateArtwork()
    {
        PixelBuffer groove = CreateBuffer(560, 320, 9);

        for (int y = 176; y < 306; ++y)
        {
            for (int x = 536; x < 552; ++x)
            {
                groove[x, y] = 40;
            }
        }

        return new ItemSelectionWindowArtwork
        {
            ScrollBarBackground = new Sprite(groove),
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

    private static PixelBuffer CreateBuffer(int width, int height, int value)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill((byte)value);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new FakeGlyphSource();

    private static LocalEvent RightClick(Point point)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, point);

        return events;
    }

    /// <summary>32x32 icons of one flat colour, which is enough to tell them from the shaded list.</summary>
    private sealed class FakeIcons : IArtifactIcons
    {
        public Sprite? GetMiniArtifact(uint frame) => new Sprite(CreateBuffer(32, 32, IconColor));
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
