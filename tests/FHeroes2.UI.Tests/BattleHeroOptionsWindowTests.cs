using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Battle;
using FHeroes2.UI.Input;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the hero and captain options, <c>DialogBattleHero()</c> (<c>battle_dialogs.cpp:855-1085</c>).
/// </summary>
/// <remarks>
/// 需要 AGG 数据的绘图测试在缺数据时直接返回；几何、标题、按钮与输入的处理不依赖档案，任何环境都会跑。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class BattleHeroOptionsWindowTests
{
    private static readonly Size Screen = new(640, 480);

    /// <summary>Real <c>VGENBKG</c> size, measured from the retail archives.</summary>
    private static readonly Size Backdrop = new(266, 219);

    [Fact]
    public void TheDialogIsCentredWithTheShadowBias()
    {
        Rect frame = BattleHeroOptionsWindow.FrameFor(Screen, Backdrop);

        Assert.Equal(new Rect(179, 123, 266, 219), frame);
        Assert.Equal(new Rect(194, 123, 251, 219), BattleHeroOptionsWindow.ActiveAreaFor(frame));
    }

    [Fact]
    public void TheFourButtonsSitOnTheReferenceOffsets()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue, HasBook = true });

        Assert.Equal(new Rect(209, 271, 43, 48), window.CastButton.Area);
        Assert.Equal(new Rect(268, 271, 43, 48), window.RetreatButton.Area);
        Assert.Equal(new Rect(327, 271, 43, 48), window.SurrenderButton.Area);
        Assert.Equal(new Rect(386, 271, 43, 48), window.CloseButton.Area);
    }

    [Fact]
    public void TheTitleNamesTheCommander()
    {
        Hero hero = new(1, MapRace.Knight);

        Assert.Equal($"{hero.Name} the {RaceNames.Get(hero.Race)}", BattleHeroOptionsWindow.TitleFor(hero));

        Castle town = new(0, MapRace.Knight) { Name = "Ravenshore" };
        Assert.Equal("Captain of Ravenshore", BattleHeroOptionsWindow.TitleFor(new Captain(town)));
    }

    [Fact]
    public void TheColourBlockSkipsTheNeutralEntry()
    {
        Assert.Equal(1, BattleHeroOptionsWindow.ColorBlockFrame(PlayerColor.None));
        Assert.Equal(1, BattleHeroOptionsWindow.ColorBlockFrame(PlayerColor.Blue));
        Assert.Equal(6, BattleHeroOptionsWindow.ColorBlockFrame(PlayerColor.Purple));
    }

    [Fact]
    public void MissingArtworkYieldsNoDialog() =>
        Assert.Null(BattleHeroOptionsWindow.TryCreate(new DialogSprites(false), new StubCommander(),
            true, true, true, PlayerColor.None, Screen));

    [Fact]
    public void AMissingButtonFrameYieldsNoDialog() =>
        Assert.Null(BattleHeroOptionsWindow.TryCreate(new DialogSprites(true, 14), new StubCommander(),
            true, true, true, PlayerColor.None, Screen));

    [Fact]
    public void CastNeedsASpellBookAndAFreshRound()
    {
        Assert.True(Create(new StubCommander { Color = PlayerColor.Blue, HasBook = true }).CastButton.IsEnabled);
        Assert.False(Create(new StubCommander { Color = PlayerColor.Blue }).CastButton.IsEnabled);
        Assert.False(Create(new StubCommander
        {
            Color = PlayerColor.Blue,
            HasBook = true,
            Modes = HeroModes.Spellcasted,
        }).CastButton.IsEnabled);
    }

    [Fact]
    public void RetreatAndSurrenderFollowTheArena()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue },
            canRetreat: false, canSurrender: true);

        Assert.False(window.RetreatButton.IsEnabled);
        Assert.True(window.SurrenderButton.IsEnabled);
        Assert.True(window.CloseButton.IsEnabled);
    }

    [Fact]
    public void ACaptainHasNeitherRetreatNorSurrender()
    {
        // CanRetreatOpponent/CanSurrenderOpponent already answer false for a captain (battle_arena.cpp:899-910);
        // the dialog only mirrors those flags, it does not second guess them.
        BattleHeroOptionsWindow window = Create(new StubCommander
        {
            Color = PlayerColor.Blue,
            Heroes = false,
        }, canRetreat: false, canSurrender: false);

        Assert.True(window.IsCaptain);
        Assert.True(window.IsControllable);
        Assert.False(window.RetreatButton.IsEnabled);
        Assert.False(window.SurrenderButton.IsEnabled);
        Assert.False(window.CanOpenHeroScreen);
    }

    [Fact]
    public void AnEnemyCommanderIsReadOnly()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue, HasBook = true },
            currentColor: PlayerColor.Red);

        Assert.True(window.IsReadOnly);
        Assert.False(window.CastButton.IsEnabled);
        Assert.False(window.RetreatButton.IsEnabled);
        Assert.False(window.SurrenderButton.IsEnabled);
        Assert.True(window.CloseButton.IsEnabled);
    }

    [Fact]
    public void WithoutButtonsTheWindowWaitsForTheRightMouseButton()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue }, buttons: false);
        LocalEvent events = new();

        Assert.True(window.IsReadOnly);
        Assert.False(window.CloseButton.IsEnabled);

        // The right button is not held: the reference leaves the loop at once.
        Assert.Equal(BattleHeroOptionsAction.Close, window.ProcessEvents(events));

        events.OnMouseButton(true, MouseButtonType.Right, new Point(10, 10));
        Assert.Equal(BattleHeroOptionsAction.None, window.ProcessEvents(events));

        events.OnMouseButton(false, MouseButtonType.Right, new Point(10, 10));
        Assert.Equal(BattleHeroOptionsAction.Close, window.ProcessEvents(events));
    }

    [Fact]
    public void TheCloseButtonReportsClose()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue, HasBook = true });
        LocalEvent events = new();
        Point at = window.CloseButton.Area.Position + new Point(5, 5);

        events.OnMouseButton(true, MouseButtonType.Left, at);
        events.OnMouseButton(false, MouseButtonType.Left, at);

        Assert.Equal(BattleHeroOptionsAction.Close, window.ProcessEvents(events));
    }

    [Fact]
    public void ACastClickReportsCastSpell()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue, HasBook = true });
        LocalEvent events = new();
        Point at = window.CastButton.Area.Position + new Point(5, 5);

        events.OnMouseButton(true, MouseButtonType.Left, at);
        events.OnMouseButton(false, MouseButtonType.Left, at);

        Assert.Equal(BattleHeroOptionsAction.CastSpell, window.ProcessEvents(events));
    }

    [Fact]
    public void ThePortraitIsOnlyAButtonForAHerosOwnTurn()
    {
        BattleHeroOptionsWindow heroWindow = Create(new StubCommander { Color = PlayerColor.Blue });
        LocalEvent events = new();
        Point at = BattleHeroOptionsWindow.PortraitAreaFor(heroWindow.Area).Position + new Point(5, 5);

        events.OnMouseButton(true, MouseButtonType.Left, at);
        events.OnMouseButton(false, MouseButtonType.Left, at);

        Assert.Equal(BattleHeroOptionsAction.None, heroWindow.ProcessEvents(events));
        Assert.True(heroWindow.PortraitClicked);

        BattleHeroOptionsWindow enemyWindow = Create(new StubCommander { Color = PlayerColor.Blue },
            currentColor: PlayerColor.Red);
        LocalEvent enemyEvents = new();

        enemyEvents.OnMouseButton(true, MouseButtonType.Left, at);
        enemyEvents.OnMouseButton(false, MouseButtonType.Left, at);

        Assert.Equal(BattleHeroOptionsAction.None, enemyWindow.ProcessEvents(enemyEvents));
        Assert.False(enemyWindow.PortraitClicked);
    }

    [Fact]
    public void TheStatusLineFollowsTheCursor()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue, HasBook = true });
        LocalEvent events = new();

        Assert.Equal("Hero's Options", window.StatusMessage);

        events.OnMouseMotion(window.CastButton.Area.Position + new Point(5, 5));
        window.ProcessEvents(events);
        Assert.Equal("Cast Spell", window.StatusMessage);

        events.OnMouseMotion(window.CloseButton.Area.Position + new Point(5, 5));
        window.ProcessEvents(events);
        Assert.Equal("Cancel", window.StatusMessage);

        events.OnMouseMotion(BattleHeroOptionsWindow.PortraitAreaFor(window.Area).Position + new Point(5, 5));
        window.ProcessEvents(events);
        Assert.Equal("Hero Screen", window.StatusMessage);

        events.OnMouseMotion(new Point(0, 0));
        window.ProcessEvents(events);
        Assert.Equal("Hero's Options", window.StatusMessage);
    }

    [Fact]
    public void ACaptainsStatusIsItsOwnOptions()
    {
        BattleHeroOptionsWindow window = Create(new StubCommander { Color = PlayerColor.Blue, Heroes = false });
        LocalEvent events = new();

        events.OnMouseMotion(new Point(0, 0));
        window.ProcessEvents(events);

        Assert.Equal("Captain's Options", window.StatusMessage);
    }

    [Fact]
    public void TheDialogPaintsTheBackdropFromTheArchives()
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
            StubCommander commander = new() { Color = PlayerColor.Blue, HasBook = true };

            BattleHeroOptionsWindow? window = BattleHeroOptionsWindow.TryCreate(sprites, commander, true, true, true,
                PlayerColor.Blue, Screen);
            Assert.NotNull(window);

            Sprite? backdrop = sprites.GetSprite(IcnType.Vgenbkg, 0);
            Assert.NotNull(backdrop);

            if (!TryFindOpaque(backdrop, out int x, out int y))
            {
                return;
            }

            PixelBuffer target = new(Screen.Width, Screen.Height);
            window.Draw(target);

            Assert.Equal(backdrop.Image[x, y], target[window.Frame.X + x, window.Frame.Y + y]);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    private static BattleHeroOptionsWindow Create(IArmyCommander commander, bool buttons = true,
        bool canRetreat = true, bool canSurrender = true, PlayerColor? currentColor = null) =>
        Assert.IsType<BattleHeroOptionsWindow>(BattleHeroOptionsWindow.TryCreate(new DialogSprites(), commander,
            buttons, canRetreat, canSurrender, currentColor ?? commander.GetColor(), Screen));

    private static bool TryFindOpaque(Sprite sprite, out int x, out int y)
    {
        x = 0;

        for (y = 0; y < sprite.Height; ++y)
        {
            for (x = 0; x < sprite.Width; ++x)
            {
                if (sprite.Image[x, y] != 0 && (sprite.Transform is null || sprite.Transform[(y * sprite.Width) + x] == 0))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>A commander with the parts <see cref="IArmyCommander"/> asks for and nothing else.</summary>
    private sealed class StubCommander : IArmyCommander
    {
        public PlayerColor Color { get; init; } = PlayerColor.None;

        public bool Heroes { get; init; } = true;

        public bool HasBook { get; init; }

        public HeroModes Modes { get; init; } = HeroModes.None;

        public ArtifactBag BagArtifacts { get; } = new();

        public int GetAttack() => 1;

        public int GetDefense() => 2;

        public int GetPower() => 3;

        public int GetMorale() => Morale.Normal;

        public int GetLuck() => Luck.Normal;

        public uint GetSecondarySkillValue(SecondarySkill skill) => 0;

        public double GetMagicStrategicValue(double armyStrength) => armyStrength;

        public PlayerControl GetControl() => PlayerControl.None;

        public PlayerColor GetColor() => Color;

        public bool IsHeroes => Heroes;

        public bool HasSpellBook => HasBook;

        public int SpellPoints => 10;

        public int GetMaxSpellPoints() => 20;
    }

    /// <summary>Stand-in artwork: one opaque frame per archive the dialog reads.</summary>
    private sealed class DialogSprites : IViewWorldSpriteSource
    {
        private readonly Dictionary<(IcnType Icn, int Index), Sprite> _frames = new();

        public DialogSprites(bool withBackground = true, int missingFrame = -1)
        {
            if (withBackground)
            {
                _frames[(IcnType.Vgenbkg, 0)] = new Sprite(Backdrop.Width, Backdrop.Height);
            }

            for (int index = 1; index <= 16; ++index)
            {
                if (index != missingFrame)
                {
                    _frames[(IcnType.Viewgen, index)] = new Sprite(43, 48);
                }
            }

            _frames[(IcnType.Port0000, 0)] = new Sprite(101, 93);
        }

        public Sprite? GetSprite(IcnType icn, int index) => _frames.GetValueOrDefault((icn, index));
    }
}
