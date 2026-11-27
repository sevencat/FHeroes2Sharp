using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the page painter of the map specifications window (ADR-303):
/// <c>editor_map_specs_window.cpp:2054-2210</c>.
/// </summary>
/// <remarks>
/// 需要真实美术件——石头底、名称框、<c>NGEXTRA</c> 的图标与光标、<c>DROPLISL</c> 的帧——所以整类跟着
/// 游戏数据门控。断言只看「有没有画到该画的地方」，不比像素值：美术件是数据，换一套素材不该让测试变红。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MapSpecsPageRendererTests : IDisposable
{
    private static readonly Rect Screen = new(0, 0, 640, 480);

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose() => Assets.Current = null;

    [Fact]
    public void TheArtworkProvidesEveryMandatoryFrame()
    {
        MapSpecsPageArtwork? artwork = OpenArtwork(isEvilInterface: false);

        Assert.NotNull(artwork);
        Assert.False(artwork.IsEvilInterface);
        Assert.True(artwork.Stone.Width > 0 && artwork.Stone.Height > 0);
        Assert.True(artwork.NameBox.Width > 0 && artwork.NameBox.Height > 0);
        Assert.True(artwork.PlayerShadow.Width > 0);
        Assert.True(artwork.DifficultyCursor.Width > 0);
        Assert.True(artwork.ConditionBackground.Width > 0);
        Assert.True(artwork.DropButton.Released.Width > 0);
        Assert.True(artwork.DropButton.Pressed.Width > 0);
    }

    [Fact]
    public void TheEvilVariantIsLoadedOnDemand()
    {
        MapSpecsPageArtwork? artwork = OpenArtwork(isEvilInterface: true);

        Assert.NotNull(artwork);
        Assert.True(artwork.IsEvilInterface);
    }

    [Fact]
    public void BothConditionPanelsEndAboveTheBottomEdge()
    {
        (MapSpecsPageArtwork artwork, MapSpecsPageLayout layout, MapSpecsState state) = Prepare();

        Assert.InRange(layout.VictoryPanelRoi.Bottom, 0, Screen.Bottom);
        Assert.InRange(layout.LossPanelRoi.Bottom, 0, Screen.Bottom);
        Assert.True(layout.DescriptionTextRoi.Right <= Screen.Right);

        // 玩家图标行与状态里的颜色数一致。
        Assert.Equal(PlayerColors.ToList(state.AvailablePlayers).Count, layout.PlayerRois.Count);
        Assert.Equal(MapSpecsPageLayout.DifficultyCount, layout.DifficultyRois.Count);
        Assert.Equal(artwork.NameBox.Width, layout.ScenarioBoxRoi.Width);
    }

    [Fact]
    public void DrawingFillsThePageAndTheNameFrame()
    {
        (MapSpecsPageArtwork artwork, MapSpecsPageLayout layout, MapSpecsState state) = Prepare();

        PixelBuffer empty = new(Screen.Width, Screen.Height);
        PixelBuffer page = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, page).Draw();

        // 活动区被石头底铺满。
        Assert.True(Differs(empty, page, layout.ActiveArea) > 0);

        // 名称框区域不是纯石头底。
        PixelBuffer stone = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, stone).DrawBackground();

        Assert.True(Differs(stone, page, layout.ScenarioBoxRoi) > 0);
        Assert.True(Differs(stone, page, layout.DescriptionTextRoi) > 0);
    }

    [Fact]
    public void ThePlayerIconsArePaintedWhereTheLayoutSays()
    {
        (MapSpecsPageArtwork artwork, MapSpecsPageLayout layout, MapSpecsState state) = Prepare();

        PixelBuffer page = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, page).Draw();

        PixelBuffer stone = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, stone).DrawBackground();

        Assert.True(Differs(stone, page, layout.PlayerRois[0]) > 0);
        Assert.True(Differs(stone, page, layout.DifficultyIconRois[0]) > 0);

        // 阴影在图标左下，也画上了。
        Assert.True(Differs(stone, page, layout.PlayerShadowRois[0]) > 0);
    }

    [Fact]
    public void TheDescriptionBoxIsShadedDarkerThanTheStone()
    {
        (MapSpecsPageArtwork artwork, MapSpecsPageLayout layout, MapSpecsState state) = Prepare();

        PixelBuffer page = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, page).Draw();

        PixelBuffer stone = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, stone).DrawBackground();

        Assert.True(Differs(stone, page, layout.DescriptionTextRoi) > (layout.DescriptionTextRoi.Width * layout.DescriptionTextRoi.Height) / 2);
    }

    [Fact]
    public void TheDifficultyCursorMovesWithTheDifficulty()
    {
        (MapSpecsPageArtwork artwork, MapSpecsPageLayout layout, MapSpecsState state) = Prepare();

        state.Difficulty = GameDifficulty.Easy;
        PixelBuffer easy = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, easy).DrawDifficulties();

        state.Difficulty = GameDifficulty.Expert;
        PixelBuffer expert = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, expert).DrawDifficulties();

        // 光标从第一格挪到第四格：第一格与第四格都不同，第二格不变。
        Assert.True(Differs(easy, expert, layout.DifficultyRois[0]) > 0);
        Assert.True(Differs(easy, expert, layout.DifficultyRois[3]) > 0);
        Assert.Equal(0, Differs(easy, expert, layout.DifficultyRois[1]));
    }

    [Fact]
    public void TheConditionRowsShowTheSelectedConditions()
    {
        (MapSpecsPageArtwork artwork, MapSpecsPageLayout layout, MapSpecsState state) = Prepare();

        state.VictoryCondition = MapVictoryCondition.DefeatEveryone;
        PixelBuffer victory = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, victory).DrawConditions();

        state.VictoryCondition = MapVictoryCondition.CaptureTown;
        PixelBuffer castle = new(Screen.Width, Screen.Height);
        new MapSpecsPageRenderer(artwork, layout, state, castle).DrawConditions();

        Assert.True(Differs(victory, castle, layout.VictoryTextRoi) > 0);

        // 失败条件那条不受胜利条件影响。
        Assert.Equal(0, Differs(victory, castle, layout.LossTextRoi));
    }

    private static MapSpecsPageArtwork? OpenArtwork(bool isEvilInterface)
    {
        Assets.Current = GameData.TryOpen();

        if (Assets.Current is null)
        {
            return null;
        }

        MapSpecsPageArtwork? artwork = MapSpecsPageArtwork.TryCreate(isEvilInterface);

        Assert.NotNull(artwork);

        return artwork;
    }

    private static (MapSpecsPageArtwork Artwork, MapSpecsPageLayout Layout, MapSpecsState State) Prepare()
    {
        MapSpecsPageArtwork artwork = OpenArtwork(isEvilInterface: false)!;

        Mp2Map map = SyntheticMap.Create(8, 8);
        map.Info = map.Info with
        {
            Name = "Beltway",
            Description = "Two roads through the hills.",
            KingdomColors = PlayerColor.Blue | PlayerColor.Red,
            HumanPlayers = PlayerColor.Blue,
            ComputerPlayers = PlayerColor.Red,
            VictoryCondition = MapVictoryCondition.DefeatEveryone,
            LossCondition = MapLossCondition.Everything,
        };

        MapSpecsState state = new(map);

        List<Size> playerIcons = [];
        foreach (PlayerColor color in PlayerColors.ToList(state.AvailablePlayers))
        {
            Sprite? icon = artwork.GetPlayerIcon(PlayerColors.GetIndex(color) + MapSpecsPageIcons.GetPlayerIconIndex(state, color));
            Assert.NotNull(icon);

            playerIcons.Add(new Size(icon.Width, icon.Height));
        }

        MapSpecsPageLayout layout = MapSpecsPageLayout.Create(
            Screen,
            new Size(artwork.NameBox.Width, artwork.NameBox.Height),
            playerIcons,
            artwork.DifficultyCursor.Height,
            new Size(artwork.DropButton.Released.Width, artwork.DropButton.Released.Height),
            new Size(artwork.ConditionBackground.Width, artwork.ConditionBackground.Height));

        return (artwork, layout, state);
    }

    private static int Differs(PixelBuffer first, PixelBuffer second, in Rect roi)
    {
        int count = 0;

        for (int y = roi.Y; y < roi.Bottom; ++y)
        {
            ReadOnlySpan<byte> left = first.Row(y);
            ReadOnlySpan<byte> right = second.Row(y);

            for (int x = roi.X; x < roi.Right; ++x)
            {
                if (left[x] != right[x])
                {
                    ++count;
                }
            }
        }

        return count;
    }
}
