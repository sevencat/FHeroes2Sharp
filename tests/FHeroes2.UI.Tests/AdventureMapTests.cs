using FHeroes2.Assets;
using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Audio;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using FHeroes2.UI.Interface;
using Xunit;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The adventure screen itself: the layout <c>AdventureMap::reset()</c> produces, the repaint mask
/// <c>AdventureMap::redraw()</c> drains and the cursor it shows (<c>game/game_interface.cpp:63-173</c>,
/// <c>game/game_startgame.cpp:477-730</c>).
/// </summary>
/// <remarks>
/// The panels resolve the colours of their buttons through <c>KB.PAL</c>, so these tests need the game data and
/// stay silent without it; they swap <see cref="GameAssets.Current"/> and therefore share the archive collection.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class AdventureMapTests : IDisposable
{
    private readonly AssetBundle? _assets;
    private readonly AssetBundle? _previousAssets;
    private readonly World _previousWorld;

    public AdventureMapTests()
    {
        Settings.Reset();
        _previousWorld = World.Current;
        _previousAssets = GameAssets.Current;
        _assets = OpenGameData();
    }

    public void Dispose()
    {
        GameAssets.Current = null;
        _assets?.Dispose();
        GameAssets.Current = _previousAssets;
        World.Current = _previousWorld;
        CursorState.Current = null;
        MapSprites.Reset();
    }

    [Fact]
    public void Create_BuildsEveryPanelOfTheReferenceConstructor()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // AdventureMap::AdventureMap() constructs the icons bar, the status panel, the buttons bar, the hide-interface
        // strip and loads the border artwork; only the radar comes from the base interface.
        Assert.NotNull(map.GetIconsPanel());
        Assert.NotNull(map.GetStatusPanel());
        Assert.NotNull(map.GetButtonsPanel());
        Assert.NotNull(map.GetControlPanel());
        Assert.NotNull(map.GetRadar());
    }

    [Fact]
    public void Create_WithoutGameData_ReturnsNull()
    {
        Settings.Reset();

        AdventureMap? map = AdventureMap.TryCreate(new PixelBuffer(640, 480), null,
            new AudioManager(new StubAudioDevice(), new StubAudioSource(), Settings.Get()), new StubHost());

        Assert.Null(map);
    }

    [Fact]
    public void ControlPanelAreaForInput_IsTheStripOfTheHideInterfaceMode()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        ControlPanel panel = Assert.IsType<ControlPanel>(map.GetControlPanel());
        Rect? area = map.ControlPanelAreaForInput;

        // The viewport asks for this area to swallow the clicks meant for the strip: it must be the strip itself,
        // not a copy of it.
        Assert.NotNull(area);
        Assert.Equal(panel.Area, area.Value);
    }

    [Fact]
    public void Reset_WithTheClassicLayout_StacksThePanelsDownTheRightEdge()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Settings.Get().SetHideInterface(false);

        map.Reset();

        Rect radar = map.GetRadar().GetRect;
        Rect icons = map.GetIconsPanel().GetRect;
        Rect status = map.GetStatusPanel().GetRect;

        // _radar.SetPos( px, borderWidth ) and then each panel below the previous one, all at the same px.
        Assert.Equal(radar.X, icons.X);
        Assert.Equal(radar.X, status.X);
        Assert.True(radar.Y < icons.Y);
        Assert.True(icons.Y < status.Y);
    }

    [Fact]
    public void Reset_WithTheHideInterfaceMode_UsesTheStoredFloatingPositions()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Settings settings = Settings.Get();
        settings.SetHideInterface(true);
        settings.SetPosRadar(new Point(10, 20));
        settings.SetPosButtons(new Point(30, 40));
        settings.SetPosIcons(new Point(50, 60));
        settings.SetPosStatus(new Point(70, 80));

        map.Reset();

        Rect radar = map.GetRadar().GetRect;
        Rect icons = map.GetIconsPanel().GetRect;
        Rect status = map.GetStatusPanel().GetRect;

        // The docking layout puts every panel at the same x, so a strictly increasing x proves that the stored
        // positions were used. The buttons panel is not built here; its position only has to be valid.
        Assert.True(radar.X < icons.X);
        Assert.True(icons.X < status.X);
        Assert.True(radar.Y < icons.Y);
        Assert.True(icons.Y < status.Y);
    }

    [Fact]
    public void Reset_WithoutStoredPositions_DocksTheFloatingPanelsFromTheTopLeftCorner()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // The configuration default is (-1, -1) for every floating panel, which reset() reads as "never moved".
        Settings.Get().SetHideInterface(true);

        map.Reset();

        Rect radar = map.GetRadar().GetRect;
        Rect icons = map.GetIconsPanel().GetRect;
        Rect status = map.GetStatusPanel().GetRect;

        Assert.True(radar.X < icons.X);
        Assert.Equal(icons.X, status.X);
        Assert.True(radar.Y < icons.Y);
        Assert.True(icons.Y < status.Y);
    }

    [Fact]
    public void RedrawLocker_CollectsTheRequestsInsteadOfServingThem()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Settings.Get().SetHideInterface(false);

        using (new AdventureMap.RedrawLocker(map))
        {
            map.RedrawInterface(Redraw.Buttons);

            Assert.True(map.GetRedrawMask().HasFlag(Redraw.Buttons));
        }

        // Released: the queued parts are served and the mask is drained.
        map.RedrawInterface(Redraw.None);

        Assert.Equal(Redraw.None, map.GetRedrawMask());
    }

    [Fact]
    public void UpdateCursor_WithoutARegisteredCursorDoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        CursorState.Current = null;

        map.UpdateCursor(-1);
    }

    [Fact]
    public void GetCursorTileIndex_IsThePointerOutsideTheMap()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        Assert.Equal(CursorTheme.Pointer, map.GetCursorTileIndex(-1));
        Assert.Equal(CursorTheme.Pointer, map.GetCursorTileIndex(int.MaxValue));
    }

    [Fact]
    public void MouseCursorAreaClickLeft_WithoutACursorTheme_DoesNothing()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        CursorState.Current = null;

        // No cursor means no theme, and every branch of the reference switch needs one.
        map.MouseCursorAreaClickLeft(0);
        map.MouseCursorAreaClickLeft(-1);

        Assert.Empty(LastHost!.QuickInfoTiles);
    }

    [Fact]
    public void MouseCursorAreaPressRight_OutsideTheMap_DoesNotAskForAQuickInfo()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        map.MouseCursorAreaPressRight(-1);
        map.MouseCursorAreaPressRight(int.MaxValue);

        Assert.Empty(LastHost!.QuickInfoTiles);
    }

    [Fact]
    public void MouseCursorAreaPressRight_OnAnEmptyTile_AsksForTheTileQuickInfo()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();
        World world = World.Current;

        // The map is generated without objects, so every tile is "the default branch": fog or not, the reference
        // shows the tile window.
        map.MouseCursorAreaPressRight(0);

        Tile tile = Assert.Single(LastHost!.QuickInfoTiles);
        Assert.Equal(0, tile.Index);
        Assert.Same(world.GetTile(0), tile);
    }

    [Fact]
    public void MouseCursorAreaLongPressLeft_WithoutAHero_KeepsTheFocus()
    {
        if (!HasGameData)
        {
            return;
        }

        AdventureMap map = CreateMap();

        // An empty tile has no hero to switch to; the reference's EventSwitchFocusedHero() returns right away.
        map.MouseCursorAreaLongPressLeft(0);
        map.MouseCursorAreaLongPressLeft(-1);

        Assert.Null(InterfaceFocus.GetFocusHeroes());
    }

    [Fact]
    public void RedrawInterface_WithTheGameData_PaintsTheWholeScreen()
    {
        if (!HasGameData)
        {
            return;
        }

        InitializeMapSprites();

        AdventureMap map = CreateMap();

        Settings.Get().SetHideInterface(false);

        map.Reset();

        // Interface::AdventureMap::StartGame() (game_startgame.cpp:740-750) prepares the first frame this way: the
        // radar is generated, the icons bar is emptied and the status panel is reset. The turn loop itself is not
        // ported, so the scene has to do this by hand.
        map.GetRadar().Build(World.Current);
        map.GetRadar().SetHide(true);
        map.GetIconsPanel().HideIcons(HeroesCastlesIcons.Any);
        map.GetStatusPanel().Reset();

        PixelBuffer display = LastDisplay!;
        display.Fill(0);

        // game_interface.cpp:154-247: this one call paints the border, the radar, the map viewport, the icons bar,
        // the buttons bar and the status panel.
        map.RedrawInterface(Redraw.All);

        int painted = display.Pixels.Count(pixel => pixel != 0);

        Assert.True(painted > 1000, $"Only {painted} of {display.Pixels.Length} pixels were painted.");
    }

    [Fact]
    public void RedrawInterface_WithTheHideInterfaceMode_PaintsTheFloatingPanelsOverTheMap()
    {
        if (!HasGameData)
        {
            return;
        }

        InitializeMapSprites();

        AdventureMap map = CreateMap();

        Settings settings = Settings.Get();
        settings.SetHideInterface(true);
        settings.SetShowControlPanel(true);
        settings.SetShowRadar(true);
        settings.SetShowIcons(true);
        settings.SetShowButtons(true);
        settings.SetShowStatus(true);

        map.Reset();

        // Same preparation as StartGame(), except that the radar is left unfolded so that this frame regenerates the
        // map image instead of painting the folded logo.
        map.GetRadar().Build(World.Current);
        map.GetRadar().SetHide(false);
        map.GetIconsPanel().HideIcons(HeroesCastlesIcons.Any);
        map.GetStatusPanel().Reset();

        // The floating panels are painted whenever they are shown, and the control panel is alpha blended over the
        // map right after the viewport, so this frame needs the game palette as well as the artwork.
        map.RedrawInterface(Redraw.All);

        PixelBuffer display = LastDisplay!;
        int painted = display.Pixels.Count(pixel => pixel != 0);

        Assert.True(painted > 1000, $"Only {painted} of {display.Pixels.Length} pixels were painted.");
    }

    /// <summary>Whether the archives could be opened; every test here needs them.</summary>
    private bool HasGameData => _assets is not null;

    /// <summary>What the last <see cref="CreateMap"/> handed to the screen, to check the dialogs it asked for.</summary>
    private StubHost? LastHost { get; set; }

    /// <summary>The frame the last <see cref="CreateMap"/> painted into.</summary>
    private PixelBuffer? LastDisplay { get; set; }

    private AdventureMap CreateMap()
    {
        World world = new();
        world.GenerateUninitializedMap(72);
        world.NewDay();
        World.Current = world;

        Settings.Get().Players.SetCurrentColor(PlayerColor.Blue);

        PixelBuffer display = new(640, 480);
        LastDisplay = display;

        StubHost host = new();
        LastHost = host;

        // The screen now builds its own panels, the way the reference constructor does.
        AdventureMap? map = AdventureMap.TryCreate(display, _assets,
            new AudioManager(new StubAudioDevice(), new StubAudioSource(), Settings.Get()), host);

        return map ?? throw new InvalidOperationException("The game data does not hold the adventure screen artwork.");
    }

    /// <summary>
    /// Installs the bundle and the palette the map renderer reads out of its process wide globals, which
    /// <c>GameHost.LoadGameData()</c> does once at start up.
    /// </summary>
    private void InitializeMapSprites()
    {
        byte[]? paletteData = _assets?.TryRead("KB.PAL");

        MapSprites.Initialize(_assets, paletteData is null ? null : Palette.FromGamePalette(paletteData));
    }

    /// <summary>
    /// Opens the game archive and hands it to the asset layer; the button colours resolve through <c>KB.PAL</c>,
    /// so every test here needs real data.
    /// </summary>
    private static AssetBundle? OpenGameData()
    {
        if (AssetLocator.Resolve() is not { } directory)
        {
            return null;
        }

        AssetBundle? bundle = AssetBundle.Open(directory);
        GameAssets.Current = bundle;

        return bundle;
    }

    /// <summary>A sound card that never makes a noise.</summary>
    private sealed class StubAudioDevice : IAudioDevice
    {
        public bool IsValid => false;

        public bool IsMusicPlaying => false;

        public int Play(ReadOnlySpan<byte> wave, bool loop, AudioLoopEffectInfo? position = null) => -1;

        public void SetPosition(int channel, short angle, byte distance)
        {
        }

        public void StopSound(int channel)
        {
        }

        public void StopAllSounds()
        {
        }

        public bool IsSoundPlaying(int channel) => false;

        public bool PlayMusicFromDatabase(ulong uid, MusicPlaybackMode mode) => false;

        public void PlayMusic(ulong uid, ReadOnlySpan<byte> midi, MusicPlaybackMode mode)
        {
        }

        public void PlayMusic(ulong uid, string filePath, MusicPlaybackMode mode)
        {
        }

        public void StopMusic()
        {
        }
    }

    /// <summary>An archive set without a single tune.</summary>
    private sealed class StubAudioSource : IAudioSource
    {
        public bool IsExpansionArchiveAvailable => false;

        public IReadOnlyList<string> MusicDirectories => Array.Empty<string>();

        public byte[]? TryGetSoundBody(M82Sound sound) => null;

        public byte[]? TryGetMidi(XmiTrack track, bool ignoreExpansion) => null;
    }

    /// <summary>The scene the adventure screen asks for dialogs; it records what was asked for.</summary>
    private sealed class StubHost : IAdventureHost
    {
        /// <summary>The tiles whose quick info was requested, in order.</summary>
        public List<Tile> QuickInfoTiles { get; } = new();

        public void ShowMessage(string header, string body)
        {
        }

        public bool ShowYesNoMessage(string header, string body) => false;

        public void ShowQuickInfo(Tile tile) => QuickInfoTiles.Add(tile);

        public void ShowQuickInfo(CastleTown castle)
        {
        }

        public void ShowQuickInfo(Hero hero)
        {
        }

        public void OpenHeroesDialog(Hero hero)
        {
        }

        public void OpenCastleDialog(CastleTown castle)
        {
        }

        public bool OpenSpellBook(Hero hero, out Spell spell)
        {
            spell = new Spell(0);

            return false;
        }

        public void OpenKingdomOverview()
        {
        }

        public void OpenPuzzleMaps()
        {
        }

        public void OpenViewWorld()
        {
        }

        public void OpenScenarioInformation()
        {
        }

        public AdventureOption ShowAdventureOptions(bool openFromHero) => AdventureOption.Cancel;

        public AdventureEventResult ShowFileOptions() => AdventureEventResult.Cancel;

        public void ShowSystemOptions()
        {
        }

        public string? SelectSaveFile() => null;

        public bool SaveGame(string fileName) => false;

        public AdventureEventResult CheckGameOver() => AdventureEventResult.Cancel;
    }
}
