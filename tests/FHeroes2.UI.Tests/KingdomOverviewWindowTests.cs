using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The blocking loop of <see cref="KingdomOverviewWindow"/> (<c>kingdom_overview.cpp:847-940</c>).
/// </summary>
/// <remarks>
/// The window has no factory yet - the artwork is assembled by the scene - so the tests build it out of single
/// coloured frames. Drawing needs the stone frame of the archives, which is why the drawing tests return early on a
/// machine without game data.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class KingdomOverviewWindowTests
{
    private const byte Plate = 40;

    [Fact]
    public void Show_ThrowsWhenNoEngineIsAttached()
    {
        KingdomOverviewWindow window = CreateWindow();

        Assert.Throws<InvalidOperationException>(() =>
            window.Show(new PixelBuffer(640, 480), new LocalEvent(() => 0)));
    }

    [Fact]
    public void Show_ReturnsFalseWhenTheEngineStops()
    {
        KingdomOverviewWindow window = CreateWindow();

        LocalEvent events = new(() => 0) { Engine = new FakeEngine { FramesBeforeStop = 0 } };

        Assert.False(window.Show(new PixelBuffer(640, 480), events));
        Assert.False(window.IsClosed);
    }

    [Fact]
    public void Show_ClosesOnEscape()
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            KingdomOverviewWindow window = CreateWindow();
            LocalEvent events = new(() => 0);

            events.Engine = new FakeEngine
            {
                OnFrame = (state, frame) =>
                    state.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down),
            };

            Assert.True(window.Show(new PixelBuffer(640, 480), events));
            Assert.True(window.IsClosed);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void Show_StopsWhenAFrameAsksForTheIncomeWindow()
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            KingdomOverviewWindow window = CreateWindow();

            // The income line sits 360 pixels under the active area and spans its whole width (kingdom_overview.cpp:860).
            Point point = new(window.ActiveArea.X + 100, window.ActiveArea.Y + 380);
            LocalEvent events = new(() => 0);

            events.Engine = new FakeEngine
            {
                OnFrame = (state, frame) =>
                {
                    if (frame == 0)
                    {
                        state.OnMouseMotion(point);
                        state.OnMouseButton(true, MouseButtonType.Right, point);
                    }
                },
            };

            // The income window is the caller's, so the loop hands the frame back instead of swallowing it.
            Assert.False(window.Show(new PixelBuffer(640, 480), events));
            Assert.True(window.Requests.Income);
            Assert.False(window.IsClosed);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    private static KingdomOverviewWindow CreateWindow() => new(
        new Kingdom(),
        new World(),
        CreateArtwork(),
        new BlockFont(8, 6, 200),
        new BlockFont(8, 6, 201));

    /// <summary>The window, made of single coloured frames; the lists are empty so only the window itself is used.</summary>
    private static KingdomOverviewArtwork CreateArtwork() => new()
    {
        Background = Plate640(),
        HeroHeader = Plate640(),
        CastleHeader = Plate640(),
        ItemsBackground = Plate640(),
        Overback = Plate640(),
        HeroRowBackground = Plate640(),
        CastleRowBackground = Plate640(),
        ScrollbarBackground = Plate640(),
        ScrollSlider = Plate640(),
        Lighthouse = Plate640(),
        NotBuiltMarker = Plate640(),
        ScrollUpReleased = Plate640(),
        ScrollUpPressed = Plate640(),
        ScrollDownReleased = Plate640(),
        ScrollDownPressed = Plate640(),
        HeroesButtonReleased = Plate640(),
        HeroesButtonPressed = Plate640(),
        TownsButtonReleased = Plate640(),
        TownsButtonPressed = Plate640(),
        ExitButtonReleased = Plate640(),
        ExitButtonPressed = Plate640(),
        Army = new ArmyBarSpriteSet(),
        Artifacts = new StubArtifacts(),
        Skills = new StubSkills(),
        Monsters = new StubMonsters(),
        Interface = new StubInterface(),
    };

    private static Sprite Plate640()
    {
        PixelBuffer image = new(640, 480);
        image.Fill(Plate);

        return new Sprite(image, 0, 0);
    }

    /// <summary>Opens the game data the stone frame of the window is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>A font of identical solid glyphs, so text measurement is predictable.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte color)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(color);
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

    private sealed class StubArtifacts : IArtifactsBarSprites
    {
        public Sprite? MiniPlate => Plate640();

        public Sprite? EmptySlot => Plate640();

        public Sprite? GetMiniArtifact(uint frame) => null;

        public Sprite? GetArtifact(uint frame) => null;
    }

    private sealed class StubSkills : ISkillBarSprites
    {
        public Sprite? SecondarySkillPlate => Plate640();

        public Sprite? MiniPlate => Plate640();

        public Sprite? GetPrimarySkillFrame(PrimarySkill skill) => null;

        public Sprite? GetSwapWindowFrame() => null;

        public Sprite? GetSecondarySkillFrame(SecondarySkill skill) => null;

        public Sprite? GetMiniSecondarySkillFrame(SecondarySkill skill) => null;
    }

    private sealed class StubMonsters : IMiniMonsterSprites
    {
        public Sprite? GetMiniMonster(MonsterType monster) => null;
    }

    private sealed class StubInterface : IViewWorldSpriteSource
    {
        public Sprite? GetSprite(IcnType icn, int index) => null;
    }

    /// <summary>An event engine that plays a scripted frame at a time and stops after a few of them.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public int FramesBeforeStop { get; init; } = 8;

        public int Frames { get; private set; }

        public Action<LocalEvent, int>? OnFrame { get; set; }

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (Frames >= FramesBeforeStop)
            {
                return false;
            }

            OnFrame?.Invoke(state, Frames++);

            return true;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }
}
