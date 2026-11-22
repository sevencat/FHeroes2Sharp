using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Input;

// The protected BorderWindow.Redraw() method shadows the Redraw mask of the interface inside this class.
using RedrawMask = FHeroes2.UI.Interface.Redraw;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The minimap of the adventure screen, <c>Interface::Radar</c> (<c>gui/interface_radar.h:34-97</c>,
/// <c>gui/interface_radar.cpp:142-499</c>).
/// </summary>
/// <remarks>
/// <para>
/// A 144x144 panel, one pixel per map tile at the smallest zoom of <c>144 / world.w()</c> (up to 4x4 pixels per tile on
/// a 36x36 map). <see cref="RedrawObjects"/> walks the queued rectangle, asks every tile for its main object and paints
/// the palette index of the ground, the road, the owner or the flat "artifact" grey; <see cref="RedrawCursor"/> outlines
/// the part of the world the game area shows on top of that. When the interface is hidden the panel floats over the map
/// and can be dragged by its border, which is why it is a <see cref="BorderWindow"/>.
/// </para>
/// <para>
/// <b>Deviations.</b> The reference reads the global <c>world</c>, <c>Settings::Get()</c> and <c>Display::instance()</c>;
/// the port injects the world through <see cref="Build"/>, keeps the "show radar", "hide interface" and "evil interface"
/// switches as properties and paints into the frame buffer it was given. <c>_redraw()</c> asks
/// <c>Players::FriendColors()</c> in the reference; the port takes the friend colours as a parameter because it has no
/// global player list. <see cref="SavePosition"/> keeps the offsets in the settings of the session; writing them to
/// <c>.fh2config</c> waits for the configuration writer. The View World half of the file — the copy constructor,
/// <c>RedrawForViewWorld()</c> and
/// <c>QueueEventProcessingForWorldView()</c> — needs <c>ViewWorld::ZoomROIs</c> and joins this class with H.
/// </para>
/// </remarks>
public sealed class Radar : BorderWindow
{
    /// <summary>Edge of the panel in pixels, <c>fheroes2::radarWidthPx</c>.</summary>
    public const int PanelSize = UiConstants.RadarWidthPx;

    /// <summary>Palette index of the cursor frame, <c>RADARCOLOR</c>.</summary>
    public const byte RadarColor = 0xB5;

    /// <summary>Palette index of the desert ground, <c>COLOR_DESERT</c>.</summary>
    public const byte DesertColor = 0x76;

    /// <summary>Palette index of the snow ground, <c>COLOR_SNOW</c>.</summary>
    public const byte SnowColor = 0x0D;

    /// <summary>Palette index of the swamp ground, <c>COLOR_SWAMP</c>.</summary>
    public const byte SwampColor = 0x68;

    /// <summary>Palette index of the wasteland ground, <c>COLOR_WASTELAND</c>.</summary>
    public const byte WastelandColor = 0xCE;

    /// <summary>Palette index of the beach ground, <c>COLOR_BEACH</c>.</summary>
    public const byte BeachColor = 0x29;

    /// <summary>Palette index of the lava ground, <c>COLOR_LAVA</c>.</summary>
    public const byte LavaColor = 0x20;

    /// <summary>Palette index of the dirt ground, <c>COLOR_DIRT</c>.</summary>
    public const byte DirtColor = 0x36;

    /// <summary>Palette index of the grass ground, <c>COLOR_GRASS</c>.</summary>
    public const byte GrassColor = 0x62;

    /// <summary>Palette index of water, <c>COLOR_WATER</c>.</summary>
    public const byte WaterColor = 0x4D;

    /// <summary>Palette index of a road, <c>COLOR_ROAD</c>.</summary>
    public const byte RoadColor = 0x7A;

    /// <summary>Palette index of an artifact or a resource pile, <c>COLOR_GRAY</c>.</summary>
    public const byte GrayColor = 0x10;

    /// <summary>Palette index of the blue kingdom, <c>COLOR_BLUE</c>.</summary>
    public const byte BlueColor = 0x47;

    /// <summary>Palette index of the green kingdom, <c>COLOR_GREEN</c>.</summary>
    public const byte GreenColor = 0x5D;

    /// <summary>Palette index of the red kingdom, <c>COLOR_RED</c>.</summary>
    public const byte RedColor = 0xBD;

    /// <summary>Palette index of the yellow kingdom, <c>COLOR_YELLOW</c>.</summary>
    public const byte YellowColor = 0x70;

    /// <summary>Palette index of the orange kingdom, <c>COLOR_ORANGE</c>.</summary>
    public const byte OrangeColor = 0xCA;

    /// <summary>Palette index of the purple kingdom, <c>COLOR_PURPLE</c>.</summary>
    public const byte PurpleColor = 0x87;

    /// <summary>Palette index of an unknown ground or of unexplored space, <c>COLOR_BLACK</c>.</summary>
    public const byte BlackColor = 0x00;

    /// <summary>Palette index of a kingdom the reference cannot name, <c>COLOR_WHITE</c>.</summary>
    public const byte WhiteColor = 0x0A;

    /// <summary>Distance between the dots of the cursor frame, the last argument of <c>DrawBorder( ., RADARCOLOR, 6 )</c>.</summary>
    public const uint CursorSkip = 6;

    /// <summary>How much darker a mountain or a tree is than the ground under it, <c>fillColor += 3</c>.</summary>
    public const byte HillShadeOffset = 3;

    /// <summary>Logo of the good interface, <c>ICN::HEROLOGO</c>.</summary>
    public const string GoodLogoAssetName = "HEROLOGO.ICN";

    /// <summary>Logo of the evil interface, <c>ICN::HEROLOGE</c>.</summary>
    public const string EvilLogoAssetName = "HEROLOGE.ICN";

    private readonly BaseInterface _interface;
    private readonly PixelBuffer _display;
    private readonly PixelBuffer _map = new(PanelSize, PanelSize);
    private readonly Sprite? _logo;
    private MovableSprite? _cursorArea;
    private World? _world;
    private Rect _roi;
    private double _zoom = 1.0;
    private bool _hide = true;

    /// <summary>Builds the radar of an interface, <c>Radar( BaseInterface &amp; )</c>.</summary>
    /// <param name="interface">Owner of the radar, asked for the game area and for repaints.</param>
    /// <param name="display">Frame buffer the radar is painted into.</param>
    /// <param name="logo">Image shown while the radar is folded away, usually <c>HEROLOGO.ICN</c> frame 0.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The map image is a single layer buffer without a transform layer, like the reference does with
    /// <c>_map._disableTransformLayer()</c>: every pixel of it is opaque and is copied to the screen as it is.
    /// </remarks>
    public Radar(BaseInterface @interface, PixelBuffer display, Sprite? logo = null)
        : base(display, new Rect(0, 0, PanelSize, PanelSize), artwork: null)
    {
        ArgumentNullException.ThrowIfNull(@interface);
        ArgumentNullException.ThrowIfNull(display);

        _interface = @interface;
        _display = display;
        _logo = logo;
    }

    /// <summary>Reads the radar logo of the current theme, the <c>ICN::HEROLOGO</c> lookup of <c>_redraw()</c>.</summary>
    /// <param name="interface">Owner of the radar.</param>
    /// <param name="display">Frame buffer the radar is painted into.</param>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <param name="evilInterface">Whether the evil interface is enabled, <c>isEvilInterfaceEnabled()</c>.</param>
    /// <returns>The radar; without the logo the folded panel simply shows nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="interface"/> or <paramref name="display"/> is <see langword="null"/>.</exception>
    public static Radar Create(BaseInterface @interface, PixelBuffer display, AssetBundle? assets, bool evilInterface)
    {
        string logoName = evilInterface ? EvilLogoAssetName : GoodLogoAssetName;
        Sprite? logo = assets?.TryReadIcn(logoName) is { Sprites.Count: > 0 } set ? set.Sprites[0] : null;

        return new Radar(@interface, display, logo);
    }

    /// <summary>The generated 144x144 map image, <c>_map</c>.</summary>
    public PixelBuffer MapImage => _map;

    /// <summary>The rectangle of the world the image is generated for, <c>_roi</c>.</summary>
    public Rect RenderArea => _roi;

    /// <summary>Pixels per tile, <c>_zoom</c>; 1 on a 144x144 map, 4 on a 36x36 one.</summary>
    public double Zoom => _zoom;

    /// <summary>The world the radar shows, set by <see cref="Build"/>.</summary>
    public World? World => _world;

    /// <summary>Whether the radar is folded away, <c>_hide</c>.</summary>
    public bool IsHidden => _hide;

    /// <summary>The cursor frame, <c>_cursorArea</c>; <see langword="null"/> when the whole map is on screen.</summary>
    public MovableSprite? CursorArea => _cursorArea;

    /// <summary>
    /// Whether the radar is shown at all, <c>Settings::Get().ShowRadar()</c>.
    /// </summary>
    /// <remarks>
    /// The port keeps the switch next to the panel instead of reading the settings on every call, so the host stays
    /// the only owner of the settings file.
    /// </remarks>
    public bool ShowRadar { get; set; } = true;

    /// <summary>Reads the world and sizes the radar for it, <c>Build()</c>.</summary>
    /// <param name="world">The map to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The reference reads the global <c>world</c> here and in every drawing method; the port hands it over once, when
    /// the interface is built, and keeps it for the whole life of the panel.
    /// </remarks>
    public void Build(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
        SetZoom();
        _roi = new Rect(0, 0, world.Width, world.Height);
    }

    /// <summary>Moves the radar, <c>SetPos()</c>.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    public override void SetPos(int x, int y)
    {
        SetPosition(x, y);
    }

    /// <summary>Saves the radar position, <c>SavePosition()</c>.</summary>
    /// <remarks>
    /// The reference writes the four panel offsets to <c>.fh2config</c> later, when the configuration is saved; the
    /// port keeps them in <see cref="FHeroes2.Game.System.Settings"/> for the session only, since the writer is not
    /// ported yet.
    /// </remarks>
    public override void SavePosition() => FHeroes2.Game.System.Settings.Get().SetPosRadar(GetRect.Position);

    /// <summary>Queues a radar repaint, <c>SetRedraw()</c>.</summary>
    /// <param name="redrawMode">
    /// <see cref="RedrawMask.Radar"/> for a full regeneration, <see cref="RedrawMask.RadarCursor"/> to repaint only the
    /// cursor over the image that is already there.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="redrawMode"/> carries other bits.</exception>
    /// <remarks>
    /// The reference asserts that the mask holds nothing but the two radar bits; the port turns the assertion into an
    /// exception, since a wrong mask would silently queue half an interface repaint.
    /// </remarks>
    public void SetRedraw(RedrawMask redrawMode)
    {
        const RedrawMask radarMask = RedrawMask.Radar | RedrawMask.RadarCursor;

        if ((redrawMode & ~radarMask) != RedrawMask.None)
        {
            throw new ArgumentException($"Only the radar bits may be queued here, got {redrawMode}.", nameof(redrawMode));
        }

        _interface.SetRedraw(redrawMode);
    }

    /// <summary>Limits the next regeneration to a part of the world, <c>SetRenderArea()</c>.</summary>
    /// <param name="roi">Rectangle of the world to regenerate, in tiles.</param>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> has not been called.</exception>
    /// <remarks>
    /// The rectangle is clipped to the world. With the interface hidden and the radar folded away nothing is rendered,
    /// so the request is dropped instead of being remembered.
    /// </remarks>
    public void SetRenderArea(Rect roi)
    {
        World world = WorldOrThrow;

        if (!HideInterfaceEnabled || ShowRadar)
        {
            _roi = roi.Intersect(new Rect(0, 0, world.Width, world.Height));
        }
    }

    /// <summary>Folds or unfolds the radar, <c>SetHide()</c>.</summary>
    /// <param name="hide">Whether the radar shows its logo instead of the map.</param>
    public void SetHide(bool hide)
    {
        _hide = hide;
    }

    /// <summary>Paints the radar, <c>_redraw()</c>.</summary>
    /// <param name="redrawMapObjects">Whether the map image is regenerated, or the previous one is reused.</param>
    /// <param name="friends">
    /// Colours that see a tile together, <c>Players::FriendColors()</c>: a tile counts as explored only when all of
    /// them have uncovered it.
    /// </param>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> has not been called.</exception>
    /// <remarks>
    /// <para>
    /// The reference spells the method with a leading underscore to keep it apart from the public panel methods; an
    /// underscore is not a C# convention, so the port spells it out.
    /// </para>
    /// <para>
    /// With the interface hidden the floating border is painted first; a folded radar paints its logo and drops the
    /// queued rectangle, because there is no image to generate. Otherwise the cursor is folded away, the map image is
    /// copied to the screen and the cursor is painted on top of it.
    /// </para>
    /// </remarks>
    public void RedrawRadar(bool redrawMapObjects, PlayerColor friends)
    {
        World world = WorldOrThrow;
        Rect worldRect = new(0, 0, world.Width, world.Height);

        if (HideInterfaceEnabled)
        {
            if (ShowRadar)
            {
                base.Redraw();
            }
            else
            {
                // Nothing of the radar is on screen, but the image has to be complete for the moment it is shown.
                _roi = worldRect;
                return;
            }
        }

        if (_hide)
        {
            if (_logo is not null)
            {
                ImageOps.Blit(_display, Area.Position, _logo.Image, _logo.Image.Bounds);
            }

            _roi = worldRect;
            return;
        }

        _cursorArea = null;

        if (redrawMapObjects)
        {
            RedrawObjects(friends, ViewWorldMode.OnlyVisible);
        }

        ImageOps.Blit(_display, Area.Position, _map, _map.Bounds);
        RedrawCursor();
    }

    /// <summary>Paints the radar for the map editor, <c>redrawForEditor()</c>.</summary>
    /// <param name="renderMapObjects">Whether the map image is regenerated, or the previous one is reused.</param>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> has not been called.</exception>
    /// <remarks>The editor shows the whole map and never fogs anything, so the friend colours are empty.</remarks>
    public void RedrawForEditor(bool renderMapObjects)
    {
        _cursorArea = null;

        if (renderMapObjects)
        {
            RedrawObjects(PlayerColor.None, ViewWorldMode.ViewAll);
            ImageOps.Blit(_display, Area.Position, _map, _map.Bounds);
        }

        RedrawCursor();
    }

    /// <summary>Generates the 144x144 map image, <c>RedrawObjects()</c>.</summary>
    /// <param name="playerColor">Colours the fog is tested against, <see cref="Tile.IsFogForAll"/>.</param>
    /// <param name="flags">How much of the map the mode uncovers.</param>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> has not been called.</exception>
    /// <remarks>
    /// <para>
    /// Every tile of the queued rectangle is classified by its main object. A hero, a lighthouse, an alchemist lab, a
    /// mine or a sawmill take the colour of their owner; artifacts and resources the flat <see cref="GrayColor"/>;
    /// everything else the colour of the castle on it, of the road, or of the ground, with hills and trees
    /// <see cref="HillShadeOffset"/> darker than the ground under them.
    /// </para>
    /// <para>
    /// The image is cleared to black only when the rectangle covers the whole world, which is what lets a partial
    /// regeneration keep the rest of the previous image. When the loop is done the rectangle is widened to the whole
    /// world again, so a later <see cref="RedrawRadar"/> regenerates everything unless <see cref="SetRenderArea"/>
    /// queues a smaller one.
    /// </para>
    /// <para>
    /// The action object types and their non action twins share one body in the reference; they are merged here through
    /// <see cref="Mp2ObjectTypes.NonActionType"/>, and a multi tile object without the action bit keeps its owner on the
    /// main tile that <see cref="Tile.GetIndexOfMainTile"/> finds.
    /// </para>
    /// </remarks>
    public void RedrawObjects(PlayerColor playerColor, ViewWorldMode flags)
    {
        World world = WorldOrThrow;

        bool revealAll = flags == ViewWorldMode.ViewAll;
        bool revealMines = revealAll || flags == ViewWorldMode.ViewMines;
        bool revealHeroes = revealAll || flags == ViewWorldMode.ViewHeroes;
        bool revealTowns = revealAll || flags == ViewWorldMode.ViewTowns;
        bool revealArtifacts = revealAll || flags == ViewWorldMode.ViewArtifacts;
        bool revealResources = revealAll || flags == ViewWorldMode.ViewResources;
        bool revealOnlyVisible = revealAll || flags == ViewWorldMode.OnlyVisible;

        Rect worldRect = new(0, 0, world.Width, world.Height);

        if (_roi == worldRect)
        {
            Array.Fill(_map.Pixels, BlackColor);
        }

        for (int y = _roi.Y; y < _roi.Bottom; ++y)
        {
            for (int x = _roi.X; x < _roi.Right; ++x)
            {
                Tile tile = world.GetTile(x, y);
                bool visibleTile = revealAll || !tile.IsFogForAll(playerColor);

                byte fillColor = BlackColor;

                // Only the "visible only" and hero modes look through a hero standing on the tile; the other modes want
                // the hero itself, which is what the cached type holds.
                Mp2ObjectType objectType = tile.GetMainObjectType(world, revealOnlyVisible || revealHeroes);

                switch (Mp2ObjectTypes.NonActionType(objectType))
                {
                case Mp2ObjectType.Hero:
                    if (visibleTile || revealHeroes)
                    {
                        if (world.Heroes.GetAtMapIndex(tile.Index) is { } hero)
                        {
                            fillColor = GetPaletteIndexOfColor(hero.Color);
                            break;
                        }
                    }

                    continue;

                case Mp2ObjectType.Lighthouse:
                case Mp2ObjectType.AlchemistLab:
                case Mp2ObjectType.Mine:
                case Mp2ObjectType.Sawmill:
                    if (visibleTile || revealMines)
                    {
                        int mainTileIndex = Mp2ObjectTypes.IsActionObject(objectType)
                            ? tile.Index
                            : Tile.GetIndexOfMainTile(tile, world);

                        if (mainTileIndex >= 0)
                        {
                            fillColor = GetPaletteIndexOfColor(world.ColorCapturedObject(mainTileIndex));
                            break;
                        }
                    }

                    continue;

                case Mp2ObjectType.Artifact:
                    if (visibleTile || revealArtifacts)
                    {
                        fillColor = GrayColor;
                        break;
                    }

                    continue;

                case Mp2ObjectType.Resource:
                    if (visibleTile || revealResources)
                    {
                        fillColor = GrayColor;
                        break;
                    }

                    continue;

                default:
                    if (visibleTile)
                    {
                        // Towns can be partly covered by non action objects, so the castle table is asked first: it
                        // knows every tile of the town sprite.
                        if (world.Castles.Get(x, y) is { } castle)
                        {
                            fillColor = GetPaletteIndexOfColor(castle.Color);
                        }
                        else if (tile.IsRoad)
                        {
                            fillColor = RoadColor;
                        }
                        else
                        {
                            fillColor = GetPaletteIndexOfGround(tile.Ground);

                            if (objectType is Mp2ObjectType.Mountains or Mp2ObjectType.Trees)
                            {
                                fillColor += HillShadeOffset;
                            }
                        }
                    }
                    else if (revealTowns)
                    {
                        if (world.Castles.Get(x, y) is { } hiddenCastle)
                        {
                            fillColor = GetPaletteIndexOfColor(hiddenCastle.Color);
                        }
                    }
                    else
                    {
                        // The tile is still fogged and the mode does not uncover towns: the black that is already
                        // there stays.
                        continue;
                    }

                    break;
                }

                FillTile(x, y, fillColor);
            }
        }

        // Widen the rectangle to the whole world, so a later redraw regenerates the complete image.
        _roi = worldRect;
    }

    /// <summary>Paints the rectangle of the world the game area shows, <c>RedrawCursor()</c>.</summary>
    /// <param name="roiRectangle">
    /// The rectangle of the world to outline, in tiles; <see langword="null"/> asks the game area, which is what the
    /// adventure screen does and the View World window does not.
    /// </param>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> has not been called.</exception>
    /// <remarks>
    /// A rectangle that covers the whole world hides the cursor: there is nothing left to point at. Otherwise the frame
    /// is rebuilt only when its size changes and is placed at the scaled corner of the rectangle.
    /// </remarks>
    public void RedrawCursor(Rect? roiRectangle = null)
    {
        World world = WorldOrThrow;

        if (HideInterfaceEnabled && !ShowRadar)
        {
            return;
        }

        Rect worldSize = new(0, 0, world.Width, world.Height);

        if (worldSize.Width < 1 || worldSize.Height < 1)
        {
            return;
        }

        Rect viewable = roiRectangle ?? _interface.GetGameArea().VisibleTileRoi;

        if (viewable.Width > worldSize.Width && viewable.Height > worldSize.Height)
        {
            // The whole map is on screen, so there is nothing to outline.
            _cursorArea = null;
            return;
        }

        Rect radarWorldArea = viewable.Intersect(worldSize);

        Size cursorSize = new(
            radarWorldArea.Width * Area.Width / worldSize.Width,
            radarWorldArea.Height * Area.Height / worldSize.Height);

        if (_cursorArea?.Sprite is not { } sprite
            || sprite.Width != cursorSize.Width
            || sprite.Height != cursorSize.Height)
        {
            if (cursorSize.Width < 1 || cursorSize.Height < 1)
            {
                _cursorArea = null;
                return;
            }

            Sprite frame = new(cursorSize.Width, cursorSize.Height);
            frame.Reset();
            ImageOps.DrawBorder(frame.Image, RadarColor, CursorSkip);
            BorderDrag.MakePaintedPixelsVisible(frame);

            _cursorArea = new MovableSprite(frame);
        }

        _cursorArea.SetPosition(
            Area.X + (radarWorldArea.X * Area.Width / worldSize.Width),
            Area.Y + (radarWorldArea.Y * Area.Height / worldSize.Height));
        _cursorArea.Show();

        if (_cursorArea.Sprite is { } cursorSprite)
        {
            ImageOps.BlitSprite(_display, new Point(_cursorArea.X, _cursorArea.Y), cursorSprite);
        }
    }

    /// <summary>Reports what the player did with the radar, <c>QueueEventProcessing()</c>.</summary>
    /// <param name="events">The frame's events.</param>
    /// <returns>The action; <see cref="RadarAction.None"/> when the click was somewhere else.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Build"/> has not been called.</exception>
    /// <remarks>
    /// <para>
    /// A left click (or a held left button) centres the game area on the clicked tile, <c>( pt - rect ) * world.size /
    /// rect.size</c> with the integer division of the reference; the cursor is repainted only when the area really
    /// moved.
    /// </para>
    /// <para>
    /// <b>Hiding.</b> The reference first runs <c>BorderWindow::QueueEventProcessing()</c> and swallows the click when
    /// the player dragged the floating panel. The port keeps the drag on the base class, which hands back a
    /// <see cref="BorderDrag"/> the orchestrator owns, so this signature collides with the base member and hides it with
    /// <c>new</c>: call the base first to get the reference's order.
    /// </para>
    /// </remarks>
    public new RadarAction QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseCursorPosInArea(Area))
        {
            return RadarAction.None;
        }

        World world = WorldOrThrow;

        if (events.MouseClickLeft(Area) || events.IsMouseLeftButtonPressedInArea(Area))
        {
            Point cursor = events.CursorPosition;

            GameArea gamearea = _interface.GetGameArea();
            Point previous = gamearea.VisibleTileRoi.Position;

            gamearea.SetCenter(new Point(
                (cursor.X - Area.X) * world.Width / Area.Width,
                (cursor.Y - Area.Y) * world.Height / Area.Height));

            if (gamearea.VisibleTileRoi.Position != previous)
            {
                SetRedraw(RedrawMask.RadarCursor);
                gamearea.SetRedraw();

                return RadarAction.MapCentered;
            }

            return RadarAction.None;
        }

        if (events.IsMouseRightButtonPressedInArea(Area))
        {
            // The reference opens the "World Map" message box itself; the port reports it, since the dialogs belong to
            // the host.
            return RadarAction.ShowTooltip;
        }

        return RadarAction.None;
    }

    /// <summary>The palette index of <paramref name="ground"/>, <c>GetPaletteIndexFromGround()</c>.</summary>
    /// <param name="ground">Ground of a tile.</param>
    /// <returns>The palette index of the ground, <see cref="BlackColor"/> for an unknown one.</returns>
    public static byte GetPaletteIndexOfGround(MapGround ground) => ground switch
    {
        MapGround.Desert => DesertColor,
        MapGround.Snow => SnowColor,
        MapGround.Swamp => SwampColor,
        MapGround.Wasteland => WastelandColor,
        MapGround.Beach => BeachColor,
        MapGround.Lava => LavaColor,
        MapGround.Dirt => DirtColor,
        MapGround.Grass => GrassColor,
        MapGround.Water => WaterColor,
        _ => BlackColor,
    };

    /// <summary>The palette index of <paramref name="color"/>, <c>GetPaletteIndexFromColor()</c>.</summary>
    /// <param name="color">A kingdom colour, or a set of them.</param>
    /// <returns>The palette index of the colour, <see cref="WhiteColor"/> for anything else.</returns>
    /// <remarks>
    /// A neutral object belongs to nobody, which the reference paints with the white of an unnamed kingdom; a set of
    /// several colours ends there as well, since the reference switches on single colours only.
    /// </remarks>
    public static byte GetPaletteIndexOfColor(PlayerColor color) => color switch
    {
        PlayerColor.Blue => BlueColor,
        PlayerColor.Green => GreenColor,
        PlayerColor.Red => RedColor,
        PlayerColor.Yellow => YellowColor,
        PlayerColor.Orange => OrangeColor,
        PlayerColor.Purple => PurpleColor,
        _ => WhiteColor,
    };

    /// <summary>Paints one tile of the image, the scaling tail of the <c>RedrawObjects()</c> loop.</summary>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <param name="fillColor">Palette index to paint.</param>
    /// <remarks>
    /// The reference indexes the image with <c>x * zoom</c> and fills the block up to <c>( x + 1 ) * zoom</c>, which is
    /// what the multiplication of the tile grows to; tiles never overlap because the next one starts exactly there.
    /// </remarks>
    private void FillTile(int x, int y, byte fillColor)
    {
        int startX = (int)(x * _zoom);
        int endX = (int)((x + 1) * _zoom);
        int startY = (int)(y * _zoom);
        int endY = (int)((y + 1) * _zoom);

        int width = _map.Width;
        int height = _map.Height;
        byte[] pixels = _map.Pixels;

        for (int py = startY; py < endY && py < height; ++py)
        {
            int rowOffset = py * width;

            for (int px = startX; px < endX && px < width; ++px)
            {
                pixels[rowOffset + px] = fillColor;
            }
        }
    }

    private World WorldOrThrow =>
        _world ?? throw new InvalidOperationException("Radar.Build() has not been called yet.");

    private void SetZoom()
    {
        World world = WorldOrThrow;
        _zoom = (double)Area.Width / world.Width;
    }
}

/// <summary>
/// How the player used the <see cref="Radar"/>, the answer of its <c>queueEventProcessing()</c>.
/// </summary>
/// <remarks>
/// The reference centres the game area and opens the "World Map" message box itself; the port reports both so the
/// orchestrator can run them, the same way <see cref="ControlPanel"/> and <see cref="ButtonsPanel"/> do.
/// </remarks>
public enum RadarAction
{
    /// <summary>The click was somewhere else.</summary>
    None,

    /// <summary>The game area was centred on the clicked tile.</summary>
    MapCentered,

    /// <summary>A right press that asks for the "World Map" tooltip.</summary>
    ShowTooltip,
}
