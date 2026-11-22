using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Audio;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The adventure screen: the panels around the scrolling map and what the player does with them,
/// <c>Interface::AdventureMap</c> (<c>game/game_interface.h:73-184</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Status: everything but the turn loop is ported.</b> This file holds the constructor, <c>reset()</c> and
/// <c>redraw()</c> of <c>game/game_interface.cpp</c> plus <c>SetFocus()</c>, <c>ResetFocus()</c>,
/// <c>updateFocus()</c> and <c>redrawFocus()</c> of <c>gui/interface_focus.cpp</c>;
/// <see cref="AdventureMap.Events.cs"/> holds <c>gui/interface_events.cpp</c>, <see cref="AdventureMap.Mouse.cs"/>
/// the mouse handlers of <c>game/game_startgame.cpp</c>, and <see cref="AdventureMap.Cursor.cs"/> its
/// <c>updateCursor()</c>.
/// </para>
/// <para>
/// <b>Assembly.</b> <see cref="TryCreate"/> is the port of the reference constructor: the screen builds its own
/// panels, so that they paint into its frame and queue their repaints on it. The only difference is where the
/// artwork comes from — the reference reads the global <c>AGG</c> cache, the port is handed the opened bundle and
/// returns <see langword="null"/> when it is missing.
/// </para>
/// </remarks>
public sealed partial class AdventureMap : BaseInterface
{
    private readonly PixelBuffer _display;
    private readonly AudioManager _audioManager;
    private readonly IAdventureHost _host;

    /// <summary>
    /// The dialogs the events open themselves, i.e. the ones whose whole content the port can already draw; the
    /// rest still go through <see cref="_host"/>.
    /// </summary>
    private AdventureMapDialogs _dialogs = null!;

    /// <summary>
    /// The dialog layer the events and the map itself open their windows through.
    /// </summary>
    /// <remarks>
    /// 参考把这些成员留在 <c>Interface::AdventureMap</c> 内部，端口拆成单独一层；场景和测试要观察或接管某个窗口
    /// （例如速览面板的阻塞循环），从这里进去。
    /// </remarks>
    public AdventureMapDialogs Dialogs => _dialogs;

    /// <summary>
    /// Panels of the screen, built by <see cref="TryCreate"/> rather than by the constructor: C# cannot pass
    /// <see langword="this"/> to an argument of its own constructor, and the panels must be attached to this very
    /// screen — their repaint requests and their focus both go through it.
    /// </summary>
    private IconsPanel _iconsPanel = null!;

    /// <inheritdoc cref="_iconsPanel"/>
    private StatusPanel _statusPanel = null!;

    /// <summary>The bar of the four icons above the end-turn button, <c>_buttonsPanel</c>.</summary>
    private ButtonsPanel? _buttonsPanel;

    /// <summary>The alternative panel of the hide-interface mode, <c>_controlPanel</c>.</summary>
    private ControlPanel? _controlPanel;

    /// <summary>The <c>ADVBORD.ICN</c> frame the border is composed of, <c>AGG::GetICN( ICN::ADVBORD, 0 )</c>.</summary>
    private Sprite? _borderArtwork;

    /// <summary>The interface flavour the screen was reset with, <c>_isCurrentInterfaceEvil</c>.</summary>
    private bool _isCurrentInterfaceEvil;

    /// <summary>Whether repaints are only collected, <c>_lockRedraw</c>.</summary>
    private bool _lockRedraw;

    /// <summary>Builds the empty screen; the panels are attached by <see cref="TryCreate"/>.</summary>
    /// <param name="display">The frame the screen paints into.</param>
    /// <param name="audioManager">The sound card, which the focus changes the music on.</param>
    /// <param name="host">The scene, which opens the dialogs the events ask for.</param>
    private AdventureMap(PixelBuffer display, AudioManager audioManager, IAdventureHost host)
        : base(false, display)
    {
        _display = display;
        _audioManager = audioManager;
        _host = host;
    }

    /// <summary>
    /// Builds the adventure screen and its panels, <c>Interface::AdventureMap::AdventureMap()</c>
    /// (<c>game/game_interface.cpp:38-61</c>).
    /// </summary>
    /// <param name="display">The frame the screen paints into.</param>
    /// <param name="assets">The opened game data every panel loads its artwork from.</param>
    /// <param name="audioManager">The sound card, which the focus changes the music on.</param>
    /// <param name="host">The scene, which opens the dialogs the events ask for.</param>
    /// <returns>The screen, or <see langword="null"/> when the game data it needs is missing.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="display"/>, <paramref name="audioManager"/> or <paramref name="host"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The reference constructor reads the global archives and the settings; the port takes the bundle as an
    /// argument and reads the settings the same way, because the interface flavour is a preference, not an asset.
    /// </para>
    /// <para>
    /// The radar and the map viewport are built by <see cref="BaseInterface"/>. The border artwork is loaded once
    /// here: both the legacy <c>GameBorder</c> and the hide-interface <c>ControlPanel</c> are composed from the same
    /// <c>ADVBORD.ICN</c> frame.
    /// </para>
    /// </remarks>
    public static AdventureMap? TryCreate(
        PixelBuffer display,
        AssetBundle? assets,
        AudioManager audioManager,
        IAdventureHost host)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(audioManager);
        ArgumentNullException.ThrowIfNull(host);

        // The status panel cannot show an army without the monster portraits, so the whole screen is unavailable.
        if (assets is null || Mons32Sprites.TryCreate(assets) is not { } monsters)
        {
            return null;
        }

        World world = World.Current;
        bool evilInterface = Settings.Get().IsEvilInterfaceEnabled;
        IcnType buttonSet = evilInterface ? IcnType.Advebtns : IcnType.Advbtns;

        AdventureMap map = new(display, audioManager, host);
        IcnViewWorldSpriteSource sprites = new(assets);

        map._dialogs = new AdventureMapDialogs(display, assets);
        map._iconsPanel = new IconsPanel(map, display, world, sprites, evilInterface);
        map._statusPanel = new StatusPanel(map, display, world, sprites, monsters);
        map._buttonsPanel = CreateButtonsPanel(display, sprites, buttonSet);
        map._controlPanel = CreateControlPanel(sprites, buttonSet);
        map._borderArtwork = MapFrame.Load(assets);

        return map;
    }

    /// <summary>
    /// Collects the sixteen frames the buttons bar is drawn from, <c>ButtonsPanel( AdventureMap &amp; )</c>
    /// (<c>gui/interface_buttons.cpp:40-56</c>).
    /// </summary>
    /// <returns>The panel, or <see langword="null"/> when the archives lack one of the frames.</returns>
    private static ButtonsPanel? CreateButtonsPanel(
        PixelBuffer display, IViewWorldSpriteSource sprites, IcnType buttonSet)
    {
        Sprite[] frames = new Sprite[ButtonsPanel.RequiredFrameCount];

        for (int i = 0; i < frames.Length; ++i)
        {
            if (sprites.GetSprite(buttonSet, i) is not { } frame)
            {
                return null;
            }

            frames[i] = frame;
        }

        return new ButtonsPanel(display,
            new Rect(0, 0, ButtonsPanel.PanelWidth, ButtonsPanel.PanelHeight), frames);
    }

    /// <summary>
    /// Cuts the five tiles of the hide-interface strip out of the button set, <c>ControlPanel::ResetTheme()</c>
    /// (<c>gui/interface_cpanel.cpp:38-58</c>).
    /// </summary>
    /// <returns>The panel, or <see langword="null"/> when the archives lack one of the tiles.</returns>
    private static ControlPanel? CreateControlPanel(IViewWorldSpriteSource sprites, IcnType buttonSet)
    {
        if (sprites.GetSprite(buttonSet, ControlPanel.RadarFrame) is not { } radar ||
            sprites.GetSprite(buttonSet, ControlPanel.IconsFrame) is not { } icons ||
            sprites.GetSprite(buttonSet, ControlPanel.ButtonsFrame) is not { } buttons ||
            sprites.GetSprite(buttonSet, ControlPanel.StatusFrame) is not { } status ||
            sprites.GetSprite(buttonSet, ControlPanel.EndTurnFrame) is not { } endTurn)
        {
            return null;
        }

        return new ControlPanel(radar, icons, buttons, status, endTurn);
    }

    /// <summary>The hero and castle bar of the left edge, <c>getIconsPanel()</c>.</summary>
    public IconsPanel GetIconsPanel() => _iconsPanel;

    /// <summary>The bottom panel, <c>getStatusPanel()</c>.</summary>
    public StatusPanel GetStatusPanel() => _statusPanel;

    /// <summary>The bar of the four icons, <c>getButtonsPanel()</c>; <see langword="null"/> without game data.</summary>
    public ButtonsPanel? GetButtonsPanel() => _buttonsPanel;

    /// <summary>The hide-interface panel, <c>getControlPanel()</c>; <see langword="null"/> without game data.</summary>
    public ControlPanel? GetControlPanel() => _controlPanel;

    /// <inheritdoc />
    /// <remarks>
    /// The viewport swallows the clicks that land on the strip, because the strip is drawn over the map. The base
    /// interface has no strip at all, hence the <see langword="null"/> there.
    /// </remarks>
    public override Rect? ControlPanelAreaForInput => _controlPanel?.Area;

    /// <summary>
    /// Whether the screen is drawn with the evil artwork, <c>isEvilInterfaceEnabled()</c> as of the last
    /// <see cref="Reset"/>.
    /// </summary>
    /// <remarks>
    /// The reference reads the setting everywhere it needs it; the port keeps the value the screen was laid out
    /// with, because the cursor art and the dimension-door picture must not change under a running game.
    /// </remarks>
    public bool IsCurrentInterfaceEvil => _isCurrentInterfaceEvil;

    /// <summary>
    /// Centres the map on a hero and makes it the focus, <c>SetFocus( Heroes *, bool )</c>
    /// (<c>gui/interface_focus.cpp:45-86</c>).
    /// </summary>
    /// <param name="hero">The hero to focus on.</param>
    /// <param name="retainScrollBarPosition">Whether the icons bar keeps its scroll position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The reference asserts that the hero belongs to the current player. The port does not: a hero out of turn is a
    /// bug of the caller, and the panels cope with it.
    /// </para>
    /// <para>
    /// The route is recalculated only for a hero the human drives: <c>Heroes::calculatePath()</c> runs the player's
    /// pathfinder, which must not be used for an AI hero.
    /// </para>
    /// </remarks>
    public void SetFocus(Hero hero, bool retainScrollBarPosition)
    {
        ArgumentNullException.ThrowIfNull(hero);

        Settings settings = Settings.Get();

        if (settings.Players.Current is not { } player)
        {
            return;
        }

        PlayerFocus focus = player.Focus;

        if (focus.GetHeroes() is { } focusedHero && !ReferenceEquals(focusedHero, hero))
        {
            // Focus has been changed to another hero. Hide the path of the previous hero.
            focusedHero.ShowPath(false);
        }

        // Heroes::calculatePath() uses PlayerWorldPathfinder and should not be used for an AI-controlled hero
        if (!hero.IsMoveEnabled && hero.IsControlHuman)
        {
            hero.CalculatePath(World.Current);
        }

        hero.ShowPath(true);
        focus.Set(hero);

        SetRedraw(Redraw.Buttons);

        if (!retainScrollBarPosition)
        {
            _iconsPanel.Select(hero);
        }

        GameArea.SetCenter(GetCenterOf(hero.MapIndex));
        _statusPanel.SetState(StatusType.Army);

        if (AdventureSounds.UpdateSoundsOnFocusUpdate && hero.MapIndex >= 0)
        {
            AdventureSounds.EnvironmentSoundMixer(World.Current, settings, _audioManager);
            _audioManager.PlayMusicAsync(
                MusicThemes.FromGround(World.Current.GetTile(hero.MapIndex).Ground),
                MusicPlaybackMode.ResumeAndPlayInfinite);
        }
    }

    /// <summary>
    /// Centres the map on a castle and makes it the focus, <c>SetFocus( Castle * )</c>
    /// (<c>gui/interface_focus.cpp:88-118</c>).
    /// </summary>
    /// <param name="castle">The castle to focus on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> is <see langword="null"/>.</exception>
    public void SetFocus(CastleTown castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        Settings settings = Settings.Get();

        if (settings.Players.Current is not { } player)
        {
            return;
        }

        PlayerFocus focus = player.Focus;

        focus.GetHeroes()?.ShowPath(false);

        focus.Set(castle);

        SetRedraw(Redraw.Buttons);

        _iconsPanel.Select(castle);
        GameArea.SetCenter(GetCenterOf(castle.TileIndex));
        _statusPanel.SetState(StatusType.Funds);

        if (AdventureSounds.UpdateSoundsOnFocusUpdate)
        {
            AdventureSounds.EnvironmentSoundMixer(World.Current, settings, _audioManager);
            _audioManager.PlayMusicAsync(
                MusicThemes.FromGround(World.Current.GetTile(castle.TileIndex).Ground),
                MusicPlaybackMode.ResumeAndPlayInfinite);
        }
    }

    /// <summary>
    /// Re-applies the focus the current player already has, <c>updateFocus()</c> (<c>gui/interface_focus.cpp:120-144</c>).
    /// </summary>
    /// <remarks>
    /// The reference dispatches on <c>Focus::first</c> and then re-reads the object; the port's
    /// <see cref="PlayerFocus"/> would answer both questions, but the two-step keeps the reference's shape: a focus
    /// whose object is gone does nothing.
    /// </remarks>
    public void UpdateFocus()
    {
        if (Settings.Get().Players.Current is not { } player)
        {
            return;
        }

        switch (player.Focus.Kind)
        {
        case FocusKind.Castle:
            if (InterfaceFocus.GetFocusCastle() is { } castle)
            {
                SetFocus(castle);
            }

            break;

        case FocusKind.Heroes:
            if (InterfaceFocus.GetFocusHeroes() is { } hero)
            {
                SetFocus(hero, false);
            }

            break;

        default:
            break;
        }
    }

    /// <summary>
    /// Picks a new focus for the current player, <c>ResetFocus( int, bool )</c> (<c>gui/interface_focus.cpp:146-221</c>).
    /// </summary>
    /// <param name="priority">What to prefer, which is <see cref="GameFocus"/> plus <see cref="GameFocus.FirstHero"/>.</param>
    /// <param name="retainScrollBarPosition">Whether the icons bar keeps its scroll position.</param>
    public void ResetFocus(int priority, bool retainScrollBarPosition)
    {
        Settings settings = Settings.Get();

        if (settings.Players.Current is not { } player)
        {
            return;
        }

        PlayerFocus focus = player.Focus;
        Kingdom kingdom = World.Current.Kingdoms.GetKingdom(player.Color);

        if (!retainScrollBarPosition)
        {
            _iconsPanel.ResetIcons(HeroesCastlesIcons.Any);
        }

        switch (priority)
        {
        case (int)GameFocus.FirstHero:
            // Find first hero excluding sleeping ones.
            Hero? firstAwake = kingdom.Heroes.FirstOrDefault(hero => !hero.Modes.HasFlag(HeroModes.Sleeper));

            if (firstAwake != null)
            {
                SetFocus(firstAwake, false);
            }
            else
            {
                // There are no non-sleeping heroes. Focus on the castle instead.
                ResetFocus((int)GameFocus.Castle, retainScrollBarPosition);
            }

            break;

        case (int)GameFocus.Heroes:
            if (focus.GetHeroes() is { } hero && hero.Color == player.Color)
            {
                // Set focus on the previously focused hero.
                SetFocus(hero, retainScrollBarPosition);
            }
            else if (kingdom.Heroes.Count > 0)
            {
                // Reset scrollbar here because the current focused hero might have
                // lost a battle and is not in the heroes list anymore.
                _iconsPanel.ResetIcons(HeroesCastlesIcons.Heroes);
                SetFocus(kingdom.Heroes[0], false);
            }
            else if (kingdom.Castles.Count > 0)
            {
                // There are no heroes left in the kingdom. Reset the heroes scrollbar and focus on the first castle.
                _iconsPanel.SetRedraw(HeroesCastlesIcons.Heroes);
                SetFocus(kingdom.Castles[0]);
            }
            else
            {
                focus.Reset();
            }

            break;

        case (int)GameFocus.Castle:
            if (focus.GetCastle() is { } castle && castle.Color == player.Color)
            {
                // Focus on the previously focused castle.
                SetFocus(castle);
            }
            else if (kingdom.Castles.Count > 0)
            {
                // The previously focused castle is lost, so we update the castles scrollbar.
                _iconsPanel.ResetIcons(HeroesCastlesIcons.Castles);
                SetFocus(kingdom.Castles[0]);
            }
            else if (kingdom.Heroes.Count > 0)
            {
                // There are no castles left in the kingdom. Reset the castles scrollbar and focus on the first hero.
                _iconsPanel.SetRedraw(HeroesCastlesIcons.Castles);
                SetFocus(kingdom.Heroes[0], false);
            }
            else
            {
                focus.Reset();
            }

            break;

        default:
            focus.Reset();

            break;
        }
    }

    /// <summary>
    /// Brings the icons bar and the panels in line with the focus, <c>redrawFocus()</c>
    /// (<c>gui/interface_focus.cpp:255-287</c>).
    /// </summary>
    public void RedrawFocus()
    {
        GameFocus type = InterfaceFocus.GetFocusType();

        if (type != GameFocus.Heroes && _iconsPanel.IsSelected(HeroesCastlesIcons.Heroes))
        {
            _iconsPanel.ResetIcons(HeroesCastlesIcons.Heroes);
            _iconsPanel.SetRedraw();
        }
        else if (type == GameFocus.Heroes && !_iconsPanel.IsSelected(HeroesCastlesIcons.Heroes))
        {
            _iconsPanel.Select(InterfaceFocus.GetFocusHeroes());
            _iconsPanel.SetRedraw();
        }

        if (type != GameFocus.Castle && _iconsPanel.IsSelected(HeroesCastlesIcons.Castles))
        {
            _iconsPanel.ResetIcons(HeroesCastlesIcons.Castles);
            _iconsPanel.SetRedraw();
        }
        else if (type == GameFocus.Castle && !_iconsPanel.IsSelected(HeroesCastlesIcons.Castles))
        {
            _iconsPanel.Select(InterfaceFocus.GetFocusCastle());
            _iconsPanel.SetRedraw();
        }

        SetRedraw(Redraw.GameArea | Redraw.RadarCursor);

        if (type == GameFocus.Heroes)
        {
            _iconsPanel.SetRedraw(HeroesCastlesIcons.Heroes);
        }
        else if (type == GameFocus.Castle)
        {
            _iconsPanel.SetRedraw(HeroesCastlesIcons.Castles);
        }

        _statusPanel.SetRedraw();
    }

    /// <summary>
    /// Paints whatever is queued, <c>AdventureMap::redraw()</c> (<c>game/game_interface.cpp:154-247</c>).
    /// </summary>
    /// <param name="force">Parts to paint even if nothing queued them.</param>
    /// <remarks>
    /// <para>
    /// The mask is the queued parts plus <paramref name="force"/>. With the hide-interface mode on, the panels float
    /// over the map and are painted whenever they are shown, not only when they are dirty; the map itself is painted
    /// under them, and the control panel is blended on top of it.
    /// </para>
    /// <para>
    /// <c>autoplayTest</c> (the automated turn the reference plays while the map is being validated) is not ported,
    /// so the game area is always painted in full.
    /// </para>
    /// </remarks>
    public override void RedrawInterface(Redraw force)
    {
        if (_lockRedraw)
        {
            // While the screen is locked the requests are only collected, so that they are all served at once by the
            // frame that unlocks it.
            SetRedraw(force);

            return;
        }

        Settings settings = Settings.Get();

        Redraw combined = GetRedrawMask() | force;
        bool hideInterface = settings.IsHideInterface;

        if (combined.HasFlag(Redraw.GameArea))
        {
            GameArea.Redraw(_display, RedrawLevelType.All);

            if (hideInterface && settings.ShowControlPanel && _controlPanel is not null)
            {
                _controlPanel.Redraw(_display, GetGamePalette());
            }
        }

        if ((hideInterface && settings.IsShowRadar) || (combined & (Redraw.RadarCursor | Redraw.Radar)) != 0)
        {
            Radar.RedrawRadar(combined.HasFlag(Redraw.Radar), Players.HumanColors());
        }

        if ((hideInterface && settings.IsShowIcons) || combined.HasFlag(Redraw.Icons))
        {
            _iconsPanel.RedrawPanel();
        }
        else if (combined.HasFlag(Redraw.Heroes))
        {
            _iconsPanel.RedrawIcons(HeroesCastlesIcons.Heroes);
        }
        else if (combined.HasFlag(Redraw.Castles))
        {
            _iconsPanel.RedrawIcons(HeroesCastlesIcons.Castles);
        }

        if ((hideInterface && settings.IsShowButtons) || combined.HasFlag(Redraw.Buttons))
        {
            _buttonsPanel?.Redraw(_display);
        }

        if ((hideInterface && settings.IsShowStatus) || combined.HasFlag(Redraw.Status))
        {
            _statusPanel.RedrawStatus();
        }

        if (combined.HasFlag(Redraw.Border) && _borderArtwork is not null)
        {
            GameBorder.Redraw(_display, _borderArtwork, _isCurrentInterfaceEvil, hideInterface, false);
        }

        ClearRedraw();
    }

    /// <summary>
    /// Lays out the panels for the current settings, <c>AdventureMap::reset()</c>
    /// (<c>game/game_interface.cpp:63-121</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// With the hide-interface mode off the panels sit in a column down the right edge of the map and the map area is
    /// the fixed 640×480 window inside the border. With the mode on the panels float where the player dragged them,
    /// or are stacked at their default place when they have no stored position yet, and the map fills the whole
    /// display.
    /// </para>
    /// <para>
    /// The map keeps the tile it was showing: the centre is translated from the old map area to the new one, so
    /// switching the mode does not jump half a screen.
    /// </para>
    /// </remarks>
    public override void Reset()
    {
        Settings settings = Settings.Get();

        _isCurrentInterfaceEvil = settings.IsEvilInterfaceEnabled;

        bool isHideInterface = settings.IsHideInterface;

        Radar.HideInterfaceEnabled = isHideInterface;
        _iconsPanel.HideInterfaceEnabled = isHideInterface;
        _statusPanel.HideInterfaceEnabled = isHideInterface;

        if (_buttonsPanel is not null)
        {
            _buttonsPanel.HideInterfaceEnabled = isHideInterface;
        }

        if (isHideInterface)
        {
            settings.SetShowControlPanel(true);

            if (_controlPanel is not null)
            {
                _controlPanel.SetPos(_display.Width - ControlPanel.PanelWidth - GameBorder.BorderWidth, 0);
            }

            Point radarPosition = settings.PosRadar;
            Point buttonsPosition = settings.PosButtons;
            Point iconsPosition = settings.PosIcons;
            Point statusPosition = settings.PosStatus;

            if (IsFloatingPositionValid(radarPosition)
                && IsFloatingPositionValid(buttonsPosition)
                && IsFloatingPositionValid(iconsPosition)
                && IsFloatingPositionValid(statusPosition))
            {
                Radar.SetPos(radarPosition.X, radarPosition.Y);
                _iconsPanel.SetPos(iconsPosition.X, iconsPosition.Y);
                _buttonsPanel?.SetPos(buttonsPosition.X, buttonsPosition.Y);
                _statusPanel.SetPos(statusPosition.X, statusPosition.Y);
            }
            else
            {
                // The player has never moved the panels: dock them to the right edge, each one below the previous.
                // The x coordinate may be past the display - the panels clamp themselves to the edge.
                Radar.SetPos(0, 0);

                int nextX = _display.Width;
                Rect radarRect = Radar.GetRect;

                _iconsPanel.SetPos(nextX, radarRect.Y + radarRect.Height);

                Rect iconsRect = _iconsPanel.GetRect;

                _buttonsPanel?.SetPos(nextX, iconsRect.Y + iconsRect.Height);

                Rect buttonsRect = _buttonsPanel?.GetRect ?? iconsRect;

                _statusPanel.SetPos(nextX, buttonsRect.Y + buttonsRect.Height);
            }
        }
        else
        {
            int nextX = _display.Width - GameBorder.BorderWidth - GameBorder.RadarWidth;

            Radar.SetPos(nextX, GameBorder.BorderWidth);

            Rect radarArea = Radar.Area;

            _iconsPanel.SetPos(nextX, radarArea.Y + radarArea.Height + GameBorder.BorderWidth);

            Rect iconsArea = _iconsPanel.Area;

            _buttonsPanel?.SetPos(nextX, iconsArea.Y + iconsArea.Height + GameBorder.BorderWidth);

            Rect buttonsArea = _buttonsPanel?.Area ?? iconsArea;

            _statusPanel.SetPos(nextX, buttonsArea.Y + buttonsArea.Height);
        }

        Point previousCenter = GameArea.CurrentCenterInPixels;
        Rect previousArea = GameArea.Roi;

        GameArea.Generate(new Size(_display.Width, _display.Height), isHideInterface);

        Rect newArea = GameArea.Roi;

        GameArea.SetCenterInPixels(
            previousCenter
            + new Point(newArea.X + (newArea.Width / 2), newArea.Y + (newArea.Height / 2))
            - new Point(previousArea.X + (previousArea.Width / 2), previousArea.Y + (previousArea.Height / 2)));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The reference has a second parameter for the keyboard cursor, which the port passes to
    /// <see cref="UpdateCursor(int, bool)"/> explicitly: the interface of <see cref="BaseInterface"/> predates it.
    /// </remarks>
    public override void UpdateCursor(int tileIndex) => UpdateCursor(tileIndex, false);

    /// <summary>Whether a stored floating position has been filled in yet, <c>isPosValid()</c>.</summary>
    /// <param name="position">The stored position.</param>
    /// <returns><see langword="true"/> for anything but the never-moved negative coordinates.</returns>
    private static bool IsFloatingPositionValid(Point position) => position.X >= 0 && position.Y >= 0;

    /// <summary>The palette the alpha blended control panel tiles need.</summary>
    /// <returns>The game palette.</returns>
    /// <exception cref="InvalidOperationException">No game data has been loaded.</exception>
    /// <remarks>
    /// The reference reads the palette out of the renderer; the port keeps it on <see cref="MapSprites"/>, the same
    /// place the map renderer takes it from.
    /// </remarks>
    private static Palette GetGamePalette()
    {
        return MapSprites.GamePalette
               ?? throw new InvalidOperationException("The adventure screen needs the game palette to blend the control panel.");
    }

    /// <summary>
    /// Collects repaints instead of serving them, <c>AdventureMap::RedrawLocker</c>
    /// (<c>game/game_interface.h:64-88</c>).
    /// </summary>
    /// <remarks>
    /// Used when a whole frame is produced at once: the parts are requested by the code that builds the frame and
    /// all served by the single <see cref="RedrawInterface"/> call that follows. The reference deletes the copy
    /// constructor; the port makes the lock a mutable class, which <see cref="IDisposable"/> already implies.
    /// </remarks>
    public sealed class RedrawLocker : IDisposable
    {
        private readonly AdventureMap _adventureMap;

        /// <summary>Takes the lock, <c>RedrawLocker( AdventureMap & )</c>.</summary>
        /// <param name="adventureMap">The screen to lock.</param>
        /// <exception cref="ArgumentNullException"><paramref name="adventureMap"/> is <see langword="null"/>.</exception>
        public RedrawLocker(AdventureMap adventureMap)
        {
            ArgumentNullException.ThrowIfNull(adventureMap);

            _adventureMap = adventureMap;
            _adventureMap._lockRedraw = true;
        }

        /// <summary>Releases the lock, <c>~RedrawLocker()</c>.</summary>
        public void Dispose() => _adventureMap._lockRedraw = false;
    }

    /// <summary>The centre of a tile in map pixels, <c>Maps::GetPoint()</c>.</summary>
    private static Point GetCenterOf(int tileIndex)
    {
        MapGeometry geometry = World.Current.Geometry
                               ?? throw new InvalidOperationException("The adventure screen needs a map of non-zero size.");

        return geometry.GetPoint(tileIndex);
    }
}
