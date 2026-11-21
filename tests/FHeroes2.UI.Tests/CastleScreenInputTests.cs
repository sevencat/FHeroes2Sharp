using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the town screen's click handling, <c>castle_dialog.cpp:493-776</c>.
/// </summary>
/// <remarks>
/// 每条都要真实的 <c>CREST</c> 与 <c>TREASURY</c> 帧才能算出区域，所以缺档案时直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CastleScreenInputTests
{
    private const byte GlyphColour = 200;

    [Fact]
    public void APressAndReleaseOnExitClosesTheScreen()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);
        Point exit = Middle(view.ExitButtonArea);

        input.SetCursor(exit);
        input.SetLeftButton(true);
        Assert.Equal(CastleScreenTarget.Exit, input.HitTest(exit));

        input.SetLeftButton(false);

        CastleScreenAction? action = input.TakeAction();
        Assert.NotNull(action);
        Assert.Equal(CastleScreenTarget.Exit, action.Value.Target);
        Assert.Equal(BuildingType.Nothing, action.Value.Building);
    }

    [Fact]
    public void AClickIsConsumedOnce()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);

        input.SetCursor(Middle(view.ExitButtonArea));
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        Assert.NotNull(input.TakeAction());
        Assert.Null(input.TakeAction());
    }

    [Fact]
    public void APressThatLeavesExitBeforeTheReleaseDoesNothing()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);

        input.SetCursor(Middle(view.ExitButtonArea));
        input.SetLeftButton(true);

        input.SetCursor(new Point(320, 200));
        input.SetLeftButton(false);

        Assert.Null(input.TakeAction());
    }

    [Fact]
    public void TheExitFrameIsPressedOnlyWhileThePointerStaysOnIt()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);
        Point exit = Middle(view.ExitButtonArea);

        input.SetCursor(exit);
        input.SetLeftButton(true);
        Assert.True(input.ExitPressed);

        input.SetCursor(new Point(320, 200));
        Assert.False(input.ExitPressed);

        input.SetCursor(exit);
        Assert.True(input.ExitPressed);

        input.SetLeftButton(false);
        Assert.False(input.ExitPressed);
    }

    [Fact]
    public void AReleaseWithoutAPressDoesNothing()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);

        input.SetCursor(Middle(view.ExitButtonArea));
        input.SetLeftButton(false);

        Assert.Null(input.TakeAction());
    }

    [Fact]
    public void TheArrowsSwitchTownsOnlyWhenTheKingdomHasSeveral()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);

        input.SetCursor(Middle(view.Layout.PreviousCastleButton));
        input.SetLeftButton(true);
        input.SetLeftButton(false);
        Assert.Equal(CastleScreenTarget.PreviousCastle, input.TakeAction()?.Target);

        input.CanSwitchCastle = false;
        Assert.Equal(CastleScreenTarget.None, input.HitTest(Middle(view.Layout.PreviousCastleButton)));

        input.SetCursor(Middle(view.Layout.NextCastleButton));
        input.SetLeftButton(true);
        input.SetLeftButton(false);
        Assert.Null(input.TakeAction());
    }

    [Fact]
    public void TheIncomePanelIsAPlainArea()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);

        input.SetCursor(Middle(view.Layout.ResourceHitArea));
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        Assert.Equal(CastleScreenTarget.Income, input.TakeAction()?.Target);
    }

    [Fact]
    public void TheHeroPortraitIsOnlyHitWithAHeroInTown()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput withoutHero = Input(out CastleScreenView emptyView, hero: null);
        Point portrait = Middle(emptyView.Layout.HeroPortraitArea);

        Assert.Equal(CastleScreenTarget.None, withoutHero.HitTest(portrait));

        CastleScreenInput withHero = Input(out CastleScreenView view, hero: new Hero(1, MapRace.Knight));
        Point occupied = Middle(view.Layout.HeroPortraitArea);

        withHero.SetCursor(occupied);
        withHero.SetLeftButton(true);
        withHero.SetLeftButton(false);

        Assert.Equal(CastleScreenTarget.Hero, withHero.TakeAction()?.Target);
    }

    [Fact]
    public void ABuildingClickReportsTheBuilding()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out CastleScreenView view, hero: null);
        Rect area = CastleBuildingInfo.GetArea(view.Castle!.Race, BuildingType.Castle)[0];

        input.SetCursor(Middle(area));
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        CastleScreenAction? action = input.TakeAction();

        Assert.NotNull(action);
        Assert.Equal(CastleScreenTarget.Building, action.Value.Target);
        Assert.Equal(BuildingType.Castle, action.Value.Building);
    }

    [Fact]
    public void EmptyGroundIsNoTarget()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        CastleScreenInput input = Input(out _, hero: null);

        input.SetCursor(new Point(1, 1));
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        Assert.Equal(CastleScreenTarget.None, input.HitTest(new Point(1, 1)));
        Assert.Null(input.TakeAction());
    }

    private static CastleScreenInput Input(out CastleScreenView view, Hero? hero)
    {
        Castle castle = new(0, MapRace.Knight);
        Funds treasury = Rich();
        Assert.True(castle.BuyBuilding(BuildingType.Castle, ref treasury));

        CastleScreenArtwork artwork = CastleScreenArtwork.TryCreate(PlayerColor.Blue)!;
        view = new CastleScreenView(artwork, Font());
        view.Show(castle, hero);

        return new CastleScreenInput(view);
    }

    private static Point Middle(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

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
