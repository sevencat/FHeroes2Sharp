using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Dialogs;
using Xunit;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The hero quick info box, <c>showQuickInfo( const HeroBase &amp; )</c>
/// (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:693-911</c>).
/// </summary>
public sealed class HeroQuickInfoTests
{
    private const byte GlyphColour = 8;

    private static readonly Size Frame = new(30, 30);
    private static readonly Size Flag = new(20, 14);

    [Fact]
    public void ContentSitsWhereTheReferencePutsIt()
    {
        HeroQuickInfoLayout layout = Layout(100, 50);

        Assert.Equal(new Rect(128, 60, 146, 144), layout.Content);
        Assert.Equal(new Point(128, 62), layout.Name);
        Assert.Equal(new Rect(100, 50, 236, 160), layout.Box);
    }

    [Fact]
    public void PortraitFrameAndPortraitAreCentredInTheContent()
    {
        HeroQuickInfoLayout layout = Layout(0, 0);

        // 146 - 30 = 116，一半是 58。
        Assert.Equal(new Point(28 + 58, 10 + 12), layout.PortraitFrame);
        Assert.Equal(new Point(28 + 58, 10 + 16), layout.GetPortrait(Frame));
    }

    [Fact]
    public void IconsMoveDownWhenThereIsOnlyOne()
    {
        HeroQuickInfoLayout layout = Layout(0, 0);

        Assert.Equal(new Point(28 + 120, 10 + 20), layout.GetLuck(1));
        Assert.Equal(new Point(28 + 120, 10 + 13), layout.GetLuck(3));
        Assert.Equal(new Point(28 + 10, 10 + 20), layout.GetMorale(1));
        Assert.Equal(new Point(28 + 10, 10 + 13), layout.GetMorale(2));
    }

    [Fact]
    public void FlagsSitEitherSideOfTheMiddleGap()
    {
        HeroQuickInfoLayout layout = Layout(0, 0);

        // 左旗从 (146 - 40) / 2 = 53 往左伸一个旗宽，右旗从 (146 + 40) / 2 = 93 往右伸。
        Assert.Equal(new Point(28 + 53 - 20, 10 + 13), layout.LeftFlag);
        Assert.Equal(new Point(28 + 93, 10 + 13), layout.RightFlag);
    }

    [Fact]
    public void StatsStartUnderThePortraitFrame()
    {
        HeroQuickInfoLayout layout = Layout(0, 0);

        // 相框高 30，旗子那一行再让 13。
        Assert.Equal(new Point(28 + 10, 10 + 13 + 30), layout.Stats);
        Assert.Equal(new Point(38, 53), layout.GetStatLabel(0));
        Assert.Equal(new Point(38, 53 + 55), layout.GetStatLabel(5));
        Assert.Equal(new Point(38 + 89, 53 + 55), layout.GetStatValue(5));
    }

    [Fact]
    public void ArmyLineSitsAtTheBottomLeft()
    {
        HeroQuickInfoLayout layout = Layout(0, 0);

        Assert.Equal(new Point(28 - 7, 10 + 118), layout.Army);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(-3, 3)]
    [InlineData(-1, 1)]
    public void IconCountFollowsTheValue(int value, int expected) =>
        Assert.Equal(expected, HeroQuickInfoView.GetIconCount(value));

    [Fact]
    public void FlagFramesAreEvenPerColor()
    {
        Assert.Equal(0, HeroQuickInfoArtwork.GetLeftFlagIndex(PlayerColor.Blue));
        Assert.Equal(2, HeroQuickInfoArtwork.GetLeftFlagIndex(PlayerColor.Green));
        Assert.Equal(10, HeroQuickInfoArtwork.GetLeftFlagIndex(PlayerColor.Purple));
        Assert.Equal(HeroQuickInfoArtwork.NeutralFlagIndex, HeroQuickInfoArtwork.GetLeftFlagIndex(PlayerColor.None));
    }

    [Fact]
    public void AColorTheReferenceCannotPaintHasNoFlag() =>
        Assert.Null(HeroQuickInfoArtwork.GetLeftFlagIndex(PlayerColor.Blue | PlayerColor.Green));

    [Fact]
    public void TheBoxIsRefusedWhenTheInterfaceIsTooSmall()
    {
        using Archives? archives = Archives.Open();

        if (archives is null || archives.Artwork is not { } artwork || archives.Sprites is not { } sprites)
        {
            return;
        }

        Hero hero = LordKilburn();

        Assert.Null(HeroQuickInfoView.TryCreate(
            Subject(hero),
            fullInfo: true,
            artwork,
            Font(),
            sprites,
            new Point(10, 10),
            new Rect(0, 0, 100, 100)));
    }

    [Fact]
    public void TheBoxIsPlacedNextToTheCursorAndPaintsItsFrame()
    {
        using Archives? archives = Archives.Open();

        if (archives is null || archives.Artwork is not { } artwork || archives.Sprites is not { } sprites)
        {
            return;
        }

        Hero hero = LordKilburn();
        Rect interfaceArea = new(0, 0, 640, 480);

        HeroQuickInfoView? view = HeroQuickInfoView.TryCreate(
            Subject(hero), fullInfo: true, artwork, Font(), sprites, new Point(300, 200), interfaceArea);

        Assert.NotNull(view);
        Assert.Equal(artwork.Box.Width, view.Area.Width);
        Assert.Equal(artwork.Box.Height, view.Area.Height);

        // 弹窗要留在界面里（getPopupWindowPosition 的规则）。
        Assert.InRange(view.Area.X, interfaceArea.X, interfaceArea.X + interfaceArea.Width - view.Area.Width);
        Assert.InRange(view.Area.Y, interfaceArea.Y, interfaceArea.Y + interfaceArea.Height - view.Area.Height);

        PixelBuffer target = new(interfaceArea.Width, interfaceArea.Height);
        view.Draw(target);

        // 底板左上角那一像素应当就是 QWIKHERO 帧 0 的左上角。
        Assert.Equal(artwork.Box.Image[0, 0], target[view.Area.X, view.Area.Y]);
    }

    [Fact]
    public void ADrawnBoxLeavesTheNameInsideTheContent()
    {
        using Archives? archives = Archives.Open();

        if (archives is null || archives.Artwork is not { } artwork || archives.Sprites is not { } sprites)
        {
            return;
        }

        Hero hero = LordKilburn();

        HeroQuickInfoView view = Assert.IsType<HeroQuickInfoView>(HeroQuickInfoView.TryCreate(
            Subject(hero), fullInfo: true, artwork, Font(), sprites, new Point(300, 200), new Rect(0, 0, 640, 480)));

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        // BlockFont 的每一格都是实心块，名字那一行必然有字。
        bool painted = false;
        for (int y = view.Layout.Name.Y; y < view.Layout.Name.Y + Font().LineHeight && !painted; ++y)
        {
            for (int x = view.Layout.Content.X; x < view.Layout.Content.X + view.Layout.Content.Width; ++x)
            {
                if (target[x, y] == GlyphColour)
                {
                    painted = true;

                    break;
                }
            }
        }

        Assert.True(painted, "the name should be drawn over the content box");
    }

    [Fact]
    public void AHeroBoxCarriesItsLevelAndItsMovePoints()
    {
        Hero hero = LordKilburn();
        HeroQuickInfoSubject subject = Subject(hero);

        Assert.Equal(hero.Level, subject.Level);
        Assert.Equal(hero.MovePoints, subject.MovePoints);
        Assert.Equal(hero.GetMaxMovePoints(), subject.MaxMovePoints);
        Assert.Equal(hero.Name, subject.Name);
        Assert.Equal(hero.Color, subject.Color);
    }

    [Fact]
    public void ACaptainBoxHasNeitherLevelNorMovePoints()
    {
        Castle castle = new(0, MapRace.Knight) { Name = "Ravenshore" };
        castle.ChangeColor(PlayerColor.Blue);
        Captain captain = new(castle);

        HeroQuickInfoSubject subject = HeroQuickInfoSubject.FromCaptain(captain, portrait: null);

        // 参考的 isActiveHero 为假：名字后面不加等级，六行四维只画五行。
        Assert.Null(subject.Level);
        Assert.Null(subject.MovePoints);
        Assert.Null(subject.MaxMovePoints);

        Assert.Equal("Ravenshore", subject.Name);
        Assert.Equal(PlayerColor.Blue, subject.Color);
        Assert.Equal(captain.GetAttack(), subject.Attack);
        Assert.Equal(captain.GetDefense(), subject.Defense);
        Assert.Equal(captain.GetPower(), subject.Power);
        Assert.Equal(captain.GetKnowledge(), subject.Knowledge);
        Assert.Equal(captain.SpellPoints, subject.SpellPoints);
        Assert.Equal((uint)captain.GetMaxSpellPoints(), subject.MaxSpellPoints);
        Assert.Same(castle.Army, subject.Army);
    }

    [Fact]
    public void CaptainPortraitsFollowTheRace()
    {
        Assert.Equal(IcnType.Port0090, CaptainPortrait.GetBigIcn(MapRace.Knight));
        Assert.Equal(IcnType.Port0091, CaptainPortrait.GetBigIcn(MapRace.Barbarian));
        Assert.Equal(IcnType.Port0092, CaptainPortrait.GetBigIcn(MapRace.Sorceress));
        Assert.Equal(IcnType.Port0093, CaptainPortrait.GetBigIcn(MapRace.Warlock));
        Assert.Equal(IcnType.Port0094, CaptainPortrait.GetBigIcn(MapRace.Wizard));
        Assert.Equal(IcnType.Port0095, CaptainPortrait.GetBigIcn(MapRace.Necromancer));

        Assert.Equal(0, CaptainPortrait.GetSmallFrameIndex(MapRace.Knight));
        Assert.Equal(5, CaptainPortrait.GetSmallFrameIndex(MapRace.Necromancer));

        // 旗子贴在种族自己的偏移上（captain.cpp:64-84）。
        Assert.Equal(new Point(43, 9), CaptainPortrait.GetFlagOffset(MapRace.Knight));
        Assert.Equal(new Point(42, 8), CaptainPortrait.GetFlagOffset(MapRace.Barbarian));
        Assert.Equal(new Point(41, 9), CaptainPortrait.GetFlagOffset(MapRace.Warlock));
        Assert.Equal(new Point(42, 10), CaptainPortrait.GetFlagOffset(MapRace.Wizard));
        Assert.Equal(new Point(42, 9), CaptainPortrait.GetFlagOffset(MapRace.Necromancer));
    }

    [Fact]
    public void ARaceWithoutACaptainHasNoPortrait()
    {
        Assert.Null(CaptainPortrait.GetBigIcn(MapRace.None));
        Assert.Null(CaptainPortrait.GetSmallFrameIndex(MapRace.None));
        Assert.Equal(default, CaptainPortrait.GetFlagOffset(MapRace.None));
    }

    [Fact]
    public void TheCaptainPortraitsComeFromTheArchives()
    {
        using Archives? archives = Archives.Open();

        if (archives is null)
        {
            return;
        }

        Castle castle = new(0, MapRace.Knight);
        castle.ChangeColor(PlayerColor.Green);
        Captain captain = new(castle);

        Sprite? big = CaptainPortrait.TryCreateBig(archives.Bundle, captain);
        Assert.NotNull(big);

        // 合成件就是 PORT0090 帧 0 的尺寸（城镇屏把它摆在 CrestArea 左上角，宽高可以比徽记略大一点）。
        IcnSpriteFile? portraits = archives.Bundle.TryReadIcn(IcnCatalog.GetFileName(IcnType.Port0090));
        Assert.NotNull(portraits);
        Assert.Equal(portraits.Sprites[0].Width, big.Width);
        Assert.Equal(portraits.Sprites[0].Height, big.Height);

        Sprite? small = CaptainPortrait.TryCreateSmall(archives.Bundle, MapRace.Knight);
        Assert.NotNull(small);

        IcnSpriteFile? miniCaptains = archives.Bundle.TryReadIcn(IcnCatalog.GetFileName(IcnType.Minicapt));
        Assert.NotNull(miniCaptains);
        Assert.True(miniCaptains.Count >= CaptainPortrait.SmallFrameCount);
    }

    [Fact]
    public void TheBigCaptainPortraitGetsItsColorFlag()
    {
        using Archives? archives = Archives.Open();

        if (archives is null || archives.Bundle.TryReadIcn(IcnCatalog.GetFileName(IcnType.Port0090)) is not { Count: > 0 } portraits ||
            archives.Bundle.TryReadIcn(IcnCatalog.GetFileName(IcnCatalog.GetFlagIcnId(PlayerColor.Green))) is not { Count: > 0 } flags)
        {
            return;
        }

        Castle castle = new(0, MapRace.Knight);
        castle.ChangeColor(PlayerColor.Green);
        Captain captain = new(castle);

        Sprite portrait = Assert.IsType<Sprite>(CaptainPortrait.TryCreateBig(archives.Bundle, captain));
        Sprite plain = portraits.Sprites[0];
        Sprite flag = flags.Sprites[0];
        Point offset = CaptainPortrait.GetFlagOffset(MapRace.Knight);

        // 参考把旗子贴到底图的拷贝上：原件不动，合成件在旗子的范围内必然与原图不同。
        int changed = 0;
        for (int y = 0; y < flag.Height; ++y)
        {
            for (int x = 0; x < flag.Width; ++x)
            {
                if (portrait.Image[x + offset.X, y + offset.Y] != plain.Image[x + offset.X, y + offset.Y])
                {
                    ++changed;
                }
            }
        }

        Assert.True(changed > 0, "the flag should be glued onto the portrait");
        Assert.Equal(plain.Image[0, 0], portrait.Image[0, 0]);
    }

    [Fact]
    public void ACaptainBoxDrawsTheSpellPointsRowAndLeavesTheMovePointsRowEmpty()
    {
        using Archives? archives = Archives.Open();

        if (archives is null || archives.Artwork is not { } artwork || archives.Sprites is not { } sprites)
        {
            return;
        }

        Castle castle = new(0, MapRace.Knight) { Name = "Ravenshore" };
        HeroQuickInfoSubject subject = HeroQuickInfoSubject.FromCaptain(new Captain(castle), portrait: null);

        HeroQuickInfoView view = Assert.IsType<HeroQuickInfoView>(HeroQuickInfoView.TryCreate(
            subject, fullInfo: true, artwork, Font(), sprites, new Point(300, 200), new Rect(0, 0, 640, 480)));

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        Assert.True(Painted(target, view.Layout.GetStatLabel(4)), "the spell points row should be drawn");
        Assert.False(Painted(target, view.Layout.GetStatLabel(5)), "a captain has no move points row");
    }

    [Fact]
    public void AHeroBoxDrawsTheMovePointsRow()
    {
        using Archives? archives = Archives.Open();

        if (archives is null || archives.Artwork is not { } artwork || archives.Sprites is not { } sprites)
        {
            return;
        }

        HeroQuickInfoView view = Assert.IsType<HeroQuickInfoView>(HeroQuickInfoView.TryCreate(
            Subject(LordKilburn()), fullInfo: true, artwork, Font(), sprites, new Point(300, 200), new Rect(0, 0, 640, 480)));

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        Assert.True(Painted(target, view.Layout.GetStatLabel(5)), "a hero's move points row should be drawn");
    }

    /// <summary>Whether a row of glyphs was painted at a point, <see cref="HeroQuickInfoLayout.StatNumberColumn"/> wide.</summary>
    private static bool Painted(PixelBuffer target, Point position)
    {
        for (int y = position.Y; y < position.Y + Font().LineHeight; ++y)
        {
            for (int x = position.X; x < position.X + HeroQuickInfoLayout.StatNumberColumn; ++x)
            {
                if (target[x, y] == GlyphColour)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HeroQuickInfoLayout Layout(int x, int y) =>
        HeroQuickInfoLayout.Create(new Rect(x, y, 236, 160), Frame, Flag);

    private static Hero LordKilburn() => new(Hero.LordKilburn, MapRace.Knight);

    private static HeroQuickInfoSubject Subject(Hero hero) => HeroQuickInfoSubject.FromHero(hero, portrait: null);

    private static IGlyphSource Font() => new BlockFont(8, 6, GlyphColour);

    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte colour)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(colour);
            _glyph = new Sprite(image, 0, 0);
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
    }

    private sealed class Archives : IDisposable
    {
        private readonly AssetBundle _bundle;

        private Archives(AssetBundle bundle, HeroQuickInfoArtwork? artwork, Mons32Sprites? sprites)
        {
            _bundle = bundle;
            Artwork = artwork;
            Sprites = sprites;
        }

        public HeroQuickInfoArtwork? Artwork { get; }

        public Mons32Sprites? Sprites { get; }

        public AssetBundle Bundle => _bundle;

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

            return new Archives(
                bundle, HeroQuickInfoArtwork.TryCreate(bundle, evilInterface: false), Mons32Sprites.TryCreate(bundle));
        }

        public void Dispose() => GameAssets.Current = null;
    }
}
