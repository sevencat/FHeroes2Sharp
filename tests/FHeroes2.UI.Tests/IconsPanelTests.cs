using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="IconsPanel"/>, <c>Interface::IconsPanel</c> (<c>gui/interface_icons.cpp</c>).
/// </summary>
/// <remarks>
/// The panel resolves the colour of its selection frame through <c>KB.PAL</c> in its constructor, exactly like the
/// reference, so the tests need the game data and stay silent without it.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class IconsPanelTests : IDisposable
{
    private readonly Archives? _archives;

    public IconsPanelTests()
    {
        Settings.Reset();
        _archives = Archives.Open();
    }

    private bool GameDataAvailable => _archives is not null;

    public void Dispose() => _archives?.Dispose();

    [Fact]
    public void Constructor_RejectsNulls()
    {
        PixelBuffer display = new(800, 600);
        World world = WorldWith();
        StubSprites sprites = new();
        StubInterface stub = new(display);

        Assert.Throws<ArgumentNullException>(() => new IconsPanel(null!, display, world, sprites));
        Assert.Throws<ArgumentNullException>(() => new IconsPanel(stub, null!, world, sprites));
        Assert.Throws<ArgumentNullException>(() => new IconsPanel(stub, display, null!, sprites));
        Assert.Throws<ArgumentNullException>(() => new IconsPanel(stub, display, world, null!));
    }

    [Fact]
    public void Constructor_StartsFromTheIconsVisibilityOption()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        Settings.Get().SetShowIcons(true);

        (IconsPanel panel, _, _) = CreatePanel();

        Assert.True(panel.ShowIconsEnabled);
    }

    [Fact]
    public void SetPos_LaysTheTwoColumnsSideBySide()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, _, _) = CreatePanel();

        panel.SetPos(10, 20);

        Assert.Equal(new Rect(10, 20, IconsPanel.PanelWidth, 4 * IconsPanel.CursorHeight), panel.Area);
        Assert.Equal(new Rect(10, 20, IconsPanel.CursorWidth, 4 * IconsPanel.CursorHeight),
            panel.HeroesIcons.ItemsArea);
        Assert.Equal(new Rect(10 + IconsPanel.ColumnGap, 20, IconsPanel.CursorWidth,
            4 * IconsPanel.CursorHeight), panel.CastleIcons.ItemsArea);
        Assert.Equal(4, panel.HeroesIcons.IconsCount);
        Assert.Equal(4, panel.CastleIcons.IconsCount);
    }

    [Fact]
    public void SetPos_UsesSevenSlotsOnAnInterfaceExactlyThreeRowsTallerThanDefault()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, _, _) = CreatePanel(new PixelBuffer(800, 640 + (3 * IconsPanel.CursorHeight)));

        panel.SetPos(0, 0);

        Assert.Equal(7, panel.HeroesIcons.IconsCount);
        Assert.Equal(7 * IconsPanel.CursorHeight, panel.Area.Height);
    }

    [Fact]
    public void SetPos_UsesEightSlotsOnATallerInterface()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, _, _) = CreatePanel(new PixelBuffer(800, 640 + (8 * IconsPanel.CursorHeight)));

        panel.SetPos(0, 0);

        Assert.Equal(8, panel.HeroesIcons.IconsCount);
        Assert.Equal(8, panel.CastleIcons.IconsCount);
        Assert.Equal(8 * IconsPanel.CursorHeight, panel.Area.Height);
    }

    [Fact]
    public void SetPos_HidesAllButTwoSlotsWhenTheInterfaceIsHidden()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, _, _) = CreatePanel();

        panel.HideInterfaceEnabled = true;
        panel.SetPos(10, 20);

        Assert.Equal(IconsPanel.HiddenIconsCount, panel.HeroesIcons.IconsCount);
        Assert.Equal(IconsPanel.HiddenIconsCount * IconsPanel.CursorHeight, panel.Area.Height);

        // The columns live inside the border, so they move with it, as GetArea() does in the reference.
        Assert.Equal(panel.Area.X, panel.HeroesIcons.ItemsArea.X);
        Assert.Equal(panel.Area.Y, panel.HeroesIcons.ItemsArea.Y);
        Assert.Equal(panel.Area.X + IconsPanel.ColumnGap, panel.CastleIcons.ItemsArea.X);
    }

    [Fact]
    public void SetPos_PlacesTheScrollBarGrooveInsideEachColumn()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, _, _) = CreatePanel();

        panel.SetPos(10, 20);

        Rect expected = new(10 + IconsPanel.ScrollBarOffsetX, 20 + IconsPanel.ScrollBarOffsetY,
            IconsPanel.ScrollBarWidth, (4 * IconsPanel.CursorHeight) - IconsPanel.ScrollBarHeightInset);

        Assert.Equal(expected, panel.HeroesIcons.ScrollBar.Area);
        Assert.Equal(new Rect(expected.X + IconsPanel.ColumnGap, expected.Y, expected.Width, expected.Height),
            panel.CastleIcons.ScrollBar.Area);
        Assert.NotNull(panel.HeroesIcons.ScrollBar.Slider);
    }

    [Fact]
    public void SetPos_KeepsTheSelectedHero()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero hero = AddHero(world, Hero.LordKilburn);

        panel.SetPos(10, 20);
        panel.ResetIcons(HeroesCastlesIcons.Any);
        panel.Select(hero);
        Assert.True(panel.HeroesIcons.IsSelected);
        Assert.Same(hero, panel.HeroesIcons.GetCurrent());

        panel.SetPos(10, 20);

        Assert.Equal(1, panel.HeroesIcons.Size);
        Assert.True(panel.HeroesIcons.IsSelected);
        Assert.Same(hero, panel.HeroesIcons.GetCurrent());
    }

    [Fact]
    public void SavePosition_StoresTheFloatingPosition()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, _, _) = CreatePanel();

        panel.HideInterfaceEnabled = true;
        panel.SetPos(31, 47);
        panel.SavePosition();

        Assert.Equal(new Point(31, 47), Settings.Get().PosIcons);
    }

    [Fact]
    public void ResetIcons_FillsBothListsFromTheKingdom()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero hero = AddHero(world, Hero.LordKilburn);
        Castle castle = AddCastle(world);

        panel.SetPos(10, 20);
        panel.ResetIcons(HeroesCastlesIcons.Any);

        Assert.Equal(1, panel.HeroesIcons.Size);
        Assert.Equal(1, panel.CastleIcons.Size);

        panel.Select(hero);
        Assert.Same(hero, panel.HeroesIcons.GetCurrent());

        panel.Select(castle);
        Assert.Same(castle, panel.CastleIcons.GetCurrent());
    }

    [Fact]
    public void ResetIcons_UnselectsBothLists()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        AddHero(world, Hero.LordKilburn);
        AddCastle(world);

        panel.SetPos(10, 20);
        panel.ResetIcons(HeroesCastlesIcons.Any);

        Assert.False(panel.HeroesIcons.IsSelected);
        Assert.False(panel.CastleIcons.IsSelected);
    }

    [Fact]
    public void Select_HeroDropsTheCastleSelection()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero hero = AddHero(world, Hero.LordKilburn);
        Castle castle = AddCastle(world);

        panel.SetPos(10, 20);
        panel.ResetIcons(HeroesCastlesIcons.Any);
        panel.Select(castle);
        panel.Select(hero);

        Assert.True(panel.HeroesIcons.IsSelected);
        Assert.False(panel.CastleIcons.IsSelected);
        Assert.True(panel.IsSelected(HeroesCastlesIcons.Heroes));
        Assert.False(panel.IsSelected(HeroesCastlesIcons.Castles));
    }

    [Fact]
    public void Select_CastleDropsTheHeroSelection()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero hero = AddHero(world, Hero.LordKilburn);
        Castle castle = AddCastle(world);

        panel.SetPos(10, 20);
        panel.ResetIcons(HeroesCastlesIcons.Any);
        panel.Select(hero);
        panel.Select(castle);

        Assert.False(panel.HeroesIcons.IsSelected);
        Assert.True(panel.CastleIcons.IsSelected);
    }

    [Fact]
    public void SingleClick_AsksTheHostToFocusTheHero()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero hero = AddHero(world, Hero.LordKilburn);

        panel.ResetIcons(HeroesCastlesIcons.Heroes);
        panel.SetPos(10, 20);

        IconsPanelAction action = ClickRow(panel, panel.HeroesIcons, 0);

        Assert.Equal(IconsPanelActionKind.FocusHero, action.Kind);
        Assert.Same(hero, action.Hero);
    }

    [Fact]
    public void DoubleClick_AsksTheHostToOpenTheHeroDialog()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero hero = AddHero(world, Hero.LordKilburn);

        panel.ResetIcons(HeroesCastlesIcons.Heroes);
        panel.SetPos(10, 20);

        ClickRow(panel, panel.HeroesIcons, 0);
        IconsPanelAction action = ClickRow(panel, panel.HeroesIcons, 0);

        Assert.Equal(IconsPanelActionKind.OpenHeroDialog, action.Kind);
        Assert.Same(hero, action.Hero);
    }

    [Fact]
    public void RightClick_AsksForTheHeroQuickInfo()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero hero = AddHero(world, Hero.LordKilburn);

        panel.ResetIcons(HeroesCastlesIcons.Heroes);
        panel.SetPos(10, 20);

        Point position = RowPosition(panel.HeroesIcons, 0);
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        IconsPanelAction action = panel.QueueEventProcessing(events);

        Assert.Equal(IconsPanelActionKind.ShowHeroQuickInfo, action.Kind);
        Assert.Same(hero, action.Hero);
    }

    [Fact]
    public void SingleClickWithTheModifier_MovesTheHeroToTheFrontOfTheKingdom()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Hero first = AddHero(world, Hero.LordKilburn);
        Hero second = AddHero(world, Hero.LordKilburn + 1);

        panel.ResetIcons(HeroesCastlesIcons.Heroes);
        panel.SetPos(10, 20);

        IconsPanelAction action = ClickRow(panel, panel.HeroesIcons, 1, moveHeroPortraitPressed: true);

        Kingdom kingdom = world.GetKingdom(Settings.Get().Players.CurrentColor);

        Assert.Equal(IconsPanelActionKind.FocusHero, action.Kind);
        Assert.Same(second, action.Hero);
        Assert.Same(second, kingdom.Heroes[0]);
        Assert.Same(first, kingdom.Heroes[1]);
        Assert.Same(second, panel.HeroesIcons.GetCurrent());
    }

    [Fact]
    public void DoubleClickOnACastle_AsksTheHostToOpenTheCastleDialog()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, World world, _) = CreatePanel();
        Castle castle = AddCastle(world);

        panel.ResetIcons(HeroesCastlesIcons.Castles);
        panel.SetPos(10, 20);

        ClickRow(panel, panel.CastleIcons, 0);
        IconsPanelAction action = ClickRow(panel, panel.CastleIcons, 0);

        Assert.Equal(IconsPanelActionKind.OpenCastleDialog, action.Kind);
        Assert.Same(castle, action.Castle);
    }

    [Fact]
    public void IsVisible_FollowsTheTwoSettings()
    {
        Settings settings = Settings.Get();

        Assert.True(IconsPanel.IsVisible(settings));

        settings.SetShowIcons(false);
        Assert.True(IconsPanel.IsVisible(settings));

        settings.SetHideInterface(true);
        Assert.False(IconsPanel.IsVisible(settings));

        settings.SetShowIcons(true);
        Assert.True(IconsPanel.IsVisible(settings));
    }

    [Fact]
    public void HideIcons_StopsDrawingTheRows()
    {
        if (!GameDataAvailable)
        {
            return;
        }

        (IconsPanel panel, _, _) = CreatePanel();

        panel.HideIcons(HeroesCastlesIcons.Heroes);

        Assert.False(panel.HeroesIcons.Show);
        Assert.True(panel.CastleIcons.Show);
    }

    private static (IconsPanel Panel, World World, StubInterface Stub) CreatePanel(PixelBuffer? display = null)
    {
        display ??= new PixelBuffer(800, 600);

        World world = WorldWith();
        StubInterface stub = new(display);
        StubSprites sprites = new();

        sprites.Add(IcnType.Advbord, 0, PaintedSprite(640, 480));
        sprites.Add(IcnType.Scroll, 0, PaintedSprite(12, 15));
        sprites.Add(IcnType.Scroll, 1, PaintedSprite(12, 15));
        sprites.Add(IcnType.Scroll, 2, PaintedSprite(12, 15));
        sprites.Add(IcnType.Scroll, 3, PaintedSprite(12, 15));
        sprites.Add(IcnType.Scroll, 4, PaintedSprite(16, 20));

        IconsPanel panel = new(stub, display, world, sprites);

        return (panel, world, stub);
    }

    private static IconsPanelAction ClickRow<TRow>(IconsPanel panel, IconsBar<TRow> list, int row,
        bool moveHeroPortraitPressed = false)
        where TRow : class
    {
        Point position = RowPosition(list, row);

        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        panel.QueueEventProcessing(events, moveHeroPortraitPressed);

        events.OnMouseButton(false, MouseButtonType.Left, position);

        return panel.QueueEventProcessing(events, moveHeroPortraitPressed);
    }

    private static Point RowPosition<TRow>(IconsBar<TRow> list, int row)
        where TRow : class
    {
        Rect rows = list.ItemsArea;

        return new Point(rows.X + 5, rows.Y + (row * rows.Height / list.IconsCount) + 5);
    }

    private static Hero AddHero(World world, int id)
    {
        Hero hero = new(id, MapRace.Knight);
        world.GetKingdom(Settings.Get().Players.CurrentColor).AddHero(hero);

        return hero;
    }

    private static Castle AddCastle(World world)
    {
        Castle castle = new(0, MapRace.Knight);
        world.GetKingdom(Settings.Get().Players.CurrentColor).AddCastle(castle);

        return castle;
    }

    private static Sprite PaintedSprite(int width, int height)
    {
        PixelBuffer image = new(width, height);
        image.Fill(1);

        return new Sprite(image, 0, 0);
    }

    private static World WorldWith()
    {
        World world = new();
        world.GenerateUninitializedMap(72);
        world.NewDay();
        Settings.Get().Players.SetCurrentColor(PlayerColor.Blue);

        return world;
    }

    /// <summary>The sprites a test needs, and only those.</summary>
    private sealed class StubSprites : IViewWorldSpriteSource
    {
        private readonly Dictionary<(IcnType Icn, int Index), Sprite> _sprites = [];

        public void Add(IcnType icn, int index, Sprite sprite) => _sprites[(icn, index)] = sprite;

        public Sprite? GetSprite(IcnType icn, int index) =>
            _sprites.TryGetValue((icn, index), out Sprite? sprite) ? sprite : null;
    }

    /// <summary>An interface that records what the panel asks of its owner.</summary>
    private sealed class StubInterface : BaseInterface
    {
        private Redraw _rendered;

        public StubInterface(PixelBuffer display)
            : base(isEditor: false, display)
        {
        }

        public Redraw GetRenderedMask() => _rendered;

        public override void RedrawInterface(Redraw force) => _rendered |= force;

        public override void MouseCursorAreaClickLeft(int tileIndex)
        {
        }

        public override void MouseCursorAreaPressRight(int tileIndex)
        {
        }

        public override void MouseCursorAreaLongPressLeft(int tileIndex)
        {
        }

        public override void Reset()
        {
        }

        public override void UpdateCursor(int tileIndex)
        {
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
