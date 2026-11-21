using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the town screen composition, <c>Castle::OpenDialog()</c> (<c>castle_dialog.cpp:325-398</c>).
/// </summary>
/// <remarks>
/// 需要 AGG 的几条在缺数据时直接返回；缺档案时 <see cref="CastleScreenArtwork.TryCreate"/> 返回 null 这一条在任何
/// 环境都会跑。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CastleScreenViewTests
{
    private const byte GlyphColour = 200;

    [Fact]
    public void TryCreateWithoutArchivesReturnsNull()
    {
        GameAssets.Current = null;

        Assert.Null(CastleScreenArtwork.TryCreate(PlayerColor.Blue));
    }

    [Theory]
    [InlineData(PlayerColor.Blue)]
    [InlineData(PlayerColor.Green)]
    [InlineData(PlayerColor.Red)]
    [InlineData(PlayerColor.Yellow)]
    [InlineData(PlayerColor.Orange)]
    [InlineData(PlayerColor.Purple)]
    public void TryCreateLoadsEveryPlayerColour(PlayerColor color)
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenArtwork? artwork = CastleScreenArtwork.TryCreate(color);

        Assert.NotNull(artwork);
        Assert.Equal(CastleScreenArtwork.ResourceIconCount, artwork.ResourceIcons.Count);
    }

    [Fact]
    public void TheLayoutIsTheReferenceOne()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        CastleScreenView view = new(artwork, Font());

        Assert.Equal(461, view.Layout.StatusBarArea.Y);
        Assert.Equal(new Rect(5, 262, artwork.Crest.Width, artwork.Crest.Height), view.Layout.CrestArea);

        Rect exit = view.Layout.GetExitButtonArea(artwork.ExitButton);
        Assert.Equal(new Point(553, 428), new Point(exit.X, exit.Y));

        // 命中区覆盖资源面板、下沿停在 EXIT 上方 3 像素（castle_dialog.cpp:383）。
        Assert.Equal(view.Layout.ResourcePanelArea.X, view.Layout.ResourceHitArea.X);
        Assert.Equal(view.Layout.ResourcePanelArea.Width, view.Layout.ResourceHitArea.Width);
        Assert.Equal(exit.Y - 3, view.Layout.ResourceHitArea.Bottom);
    }

    [Fact]
    public void ShowBuildsTheGarrisonBarOnlyWithoutAHero()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        CastleScreenView view = new(artwork, Font());
        view.Show(new Castle(0, MapRace.Knight), hero: null);

        Assert.NotNull(view.CastleArmyBar);
        Assert.Null(view.HeroArmyBar);
    }

    [Fact]
    public void FindBuildingAtUsesTheTownAreas()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        CastleScreenView view = new(artwork, Font());
        view.Show(castle, hero: null);

        Rect area = CastleBuildingInfo.GetArea(castle.Race, BuildingType.Castle)[0];

        Assert.Equal(BuildingType.Castle, view.FindBuildingAt(Middle(area)));
        Assert.Equal(BuildingType.Nothing, view.FindBuildingAt(new Point(1, 1)));
    }

    [Fact]
    public void TheStatusMessageFollowsTheReferencePriority()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        CastleScreenView view = new(artwork, Font());
        view.Show(castle, hero: null);

        Assert.Equal("Exit Castle", view.GetStatusMessage(Middle(view.Layout.GetExitButtonArea(artwork.ExitButton))));
        Assert.Equal("Show Income", view.GetStatusMessage(Middle(view.Layout.ResourceHitArea)));
        Assert.Equal("Show previous town", view.GetStatusMessage(Middle(view.Layout.PreviousCastleButton)));
        Assert.Equal("Show next town", view.GetStatusMessage(Middle(view.Layout.NextCastleButton)));
        Assert.Equal(string.Empty, view.GetStatusMessage(new Point(1, 1)));
    }

    [Fact]
    public void AVillageExitsAsATown()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        CastleScreenView view = new(artwork, Font());
        view.Show(new Castle(0, MapRace.Knight), hero: null);

        Assert.Equal("Exit Town", view.GetStatusMessage(Middle(view.Layout.GetExitButtonArea(artwork.ExitButton))));
    }

    [Fact]
    public void DrawPaintsThePanelOverTheTown()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        CastleScreenView view = new(artwork, Font());
        view.Show(castle, hero: null);

        PixelBuffer target = new(CastleDialogLayout.ScreenSize.Width, CastleDialogLayout.ScreenSize.Height);
        target.Fill(255);

        view.Draw(target, new CastleScreenFrame { Funds = Rich(), StatusText = "Day 1" });

        // 资源面板先填黑：面板内部必定被盖过。
        Assert.Equal(0, target[view.Layout.ResourceHitArea.X + 3, view.Layout.ResourceHitArea.Y + 3]);

        // 状态条把底部的 255 盖掉。
        Assert.NotEqual(255, target[view.Layout.StatusBarArea.X + 5, view.Layout.StatusBarArea.Y + 5]);
    }

    [Fact]
    public void DrawPaintsThePressedExitFrame()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        CastleScreenView view = new(artwork, Font());
        view.Show(castle, hero: null);

        Rect exit = view.Layout.GetExitButtonArea(artwork.ExitButton);

        PixelBuffer normal = new(CastleDialogLayout.ScreenSize.Width, CastleDialogLayout.ScreenSize.Height);
        view.Draw(normal, new CastleScreenFrame { Funds = Rich() });

        PixelBuffer pressed = new(CastleDialogLayout.ScreenSize.Width, CastleDialogLayout.ScreenSize.Height);
        view.Draw(pressed, new CastleScreenFrame { Funds = Rich(), ExitPressed = true });

        if (!TryFindDiffering(artwork.ExitButton, artwork.ExitPressed, out int x, out int y))
        {
            return;
        }

        // 两个帧在某个像素上不同，那一格就应当跟着按下状态变。
        Assert.Equal(artwork.ExitButton.Image[x, y], normal[exit.X + x, exit.Y + y]);
        Assert.Equal(artwork.ExitPressed.Image[x, y], pressed[exit.X + x, exit.Y + y]);
    }

    private static Point Middle(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static bool TryFindDiffering(Sprite first, Sprite second, out int x, out int y)
    {
        int width = Math.Min(first.Image.Width, second.Image.Width);
        int height = Math.Min(first.Image.Height, second.Image.Height);

        for (y = 0; y < height; ++y)
        {
            for (x = 0; x < width; ++x)
            {
                if (first.Image[x, y] != second.Image[x, y])
                {
                    return true;
                }
            }
        }

        x = 0;
        y = 0;

        return false;
    }

    private static Castle CastleWith(params BuildingType[] buildings)
    {
        Castle castle = new(0, MapRace.Knight);
        Funds treasury = Rich();

        foreach (BuildingType building in buildings)
        {
            castle.ActionNewDay();

            Assert.True(castle.BuyBuilding(building, ref treasury), $"{building} should be buyable");
        }

        return castle;
    }

    private static Funds Rich() => new()
    {
        Wood = 999_999,
        Mercury = 999_999,
        Ore = 999_999,
        Sulfur = 999_999,
        Crystal = 999_999,
        Gems = 999_999,
        Gold = 999_999,
    };

    private static IGlyphSource Font() => new BlockFont(8, 6, GlyphColour);

    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte colour)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;
            _glyph = Glyph(advance, lineHeight, colour);
        }

        public int LineHeight { get; }

        public int SpaceWidth { get; }

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }

        private static Sprite Glyph(int width, int height, byte colour)
        {
            PixelBuffer image = new(width, height);
            image.Fill(colour);

            return new Sprite(image, 0, 0);
        }
    }

    /// <summary>Opens the archives for the duration of one test and clears them afterwards.</summary>
    private sealed class Archives : IDisposable
    {
        private readonly AssetBundle _bundle;

        private Archives(AssetBundle bundle) => _bundle = bundle;

        public static Archives? Open()
        {
            if (AssetLocator.Resolve() is not { } directory)
            {
                return null;
            }

            AssetBundle? bundle = AssetBundle.Open(directory);
            if (bundle is null)
            {
                return null;
            }

            GameAssets.Current = bundle;

            return new Archives(bundle);
        }

        public void Dispose()
        {
            GameAssets.Current = null;
            _bundle.Dispose();
        }
    }
}
