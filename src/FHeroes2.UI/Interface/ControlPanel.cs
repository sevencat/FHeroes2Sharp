using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The five dimmed buttons of the hidden interface, <c>Interface::ControlPanel</c>
/// (<c>gui/interface_cpanel.cpp:34-133</c>, <c>interface_cpanel.h:38-90</c>).
/// </summary>
/// <remarks>
/// <para>
/// When the player hides the interface the stone panels disappear but a strip of five faded icons stays in the
/// bottom right corner, so that the radar, the icons panel, the buttons, the status panel and the end turn button can
/// be brought back. The strip is 180x36: five 36x36 tiles in a row, blended at 50% opacity.
/// </para>
/// <para>
/// <b>Deviation.</b> The reference keeps an <c>AdventureMap &amp;</c> and calls
/// <c>EventSwitchShowRadar()</c> and friends from <see cref="QueueEventProcessing"/>; the port returns which tile was
/// clicked (<see cref="ControlPanelButton"/>) and lets the orchestrator run the event, the same way
/// <c>BorderDrag</c> hands the drag back to its host.
/// </para>
/// </remarks>
public sealed class ControlPanel
{
    /// <summary>Width of the strip, <c>width = 180</c>.</summary>
    public const int PanelWidth = 180;

    /// <summary>Height of the strip, <c>height = 36</c>.</summary>
    public const int PanelHeight = 36;

    /// <summary>Side of one of the five tiles, <c>rt_*.width = 36</c> and <c>rt_*.height = 36</c>.</summary>
    public const int ButtonSize = 36;

    /// <summary>Opacity the tiles are blended with, <c>const uint8_t alpha = 128</c>.</summary>
    public const byte TileAlpha = 128;

    /// <summary>Buttons of the good interface, <c>ICN::ADVBTNS</c>.</summary>
    public const string GoodInterfaceAssetName = "ADVBTNS.ICN";

    /// <summary>Buttons of the evil interface, <c>ICN::ADVEBTNS</c>.</summary>
    public const string EvilInterfaceAssetName = "ADVEBTNS.ICN";

    /// <summary>Frame of the radar tile, <c>Assets::getImage( icn, 4 )</c>.</summary>
    public const int RadarFrame = 4;

    /// <summary>Frame of the icons panel tile, <c>Assets::getImage( icn, 0 )</c>.</summary>
    public const int IconsFrame = 0;

    /// <summary>Frame of the buttons panel tile, <c>Assets::getImage( icn, 12 )</c>.</summary>
    public const int ButtonsFrame = 12;

    /// <summary>Frame of the status panel tile, <c>Assets::getImage( icn, 10 )</c>.</summary>
    public const int StatusFrame = 10;

    /// <summary>Frame of the end turn tile, <c>Assets::getImage( icn, 8 )</c>.</summary>
    public const int EndTurnFrame = 8;

    private readonly Sprite _radar;
    private readonly Sprite _icons;
    private readonly Sprite _buttons;
    private readonly Sprite _status;
    private readonly Sprite _end;

    /// <summary>
    /// Builds the strip from its five tiles, <c>ControlPanel( AdventureMap &amp; )</c> after <c>ResetTheme()</c>.
    /// </summary>
    /// <param name="radar">Frame 4 of the button set.</param>
    /// <param name="icons">Frame 0 of the button set.</param>
    /// <param name="buttons">Frame 12 of the button set.</param>
    /// <param name="status">Frame 10 of the button set.</param>
    /// <param name="endTurn">Frame 8 of the button set.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ControlPanel(Sprite radar, Sprite icons, Sprite buttons, Sprite status, Sprite endTurn)
    {
        ArgumentNullException.ThrowIfNull(radar);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(endTurn);

        _radar = radar;
        _icons = icons;
        _buttons = buttons;
        _status = status;
        _end = endTurn;

        Area = new Rect(0, 0, PanelWidth, PanelHeight);
        RadarRect = new Rect(0, 0, ButtonSize, ButtonSize);
        IconsRect = new Rect(ButtonSize, 0, ButtonSize, ButtonSize);
        ButtonsRect = new Rect(ButtonSize * 2, 0, ButtonSize, ButtonSize);
        StatusRect = new Rect(ButtonSize * 3, 0, ButtonSize, ButtonSize);
        EndTurnRect = new Rect(ButtonSize * 4, 0, ButtonSize, ButtonSize);
    }

    /// <summary>The whole strip, the <c>fheroes2::Rect</c> the reference inherits from.</summary>
    public Rect Area { get; private set; }

    /// <summary>Tile of the radar, <c>rt_radar</c>.</summary>
    public Rect RadarRect { get; private set; }

    /// <summary>Tile of the icons panel, <c>rt_icons</c>.</summary>
    public Rect IconsRect { get; private set; }

    /// <summary>Tile of the buttons panel, <c>rt_buttons</c>.</summary>
    public Rect ButtonsRect { get; private set; }

    /// <summary>Tile of the status panel, <c>rt_status</c>.</summary>
    public Rect StatusRect { get; private set; }

    /// <summary>Tile of the end turn button, <c>rt_end</c>.</summary>
    public Rect EndTurnRect { get; private set; }

    /// <summary>
    /// Reads the five tiles the strip needs, <c>ResetTheme()</c> + <c>Assets::getImage()</c>.
    /// </summary>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <param name="evilInterface">Whether the evil interface is enabled, <c>isEvilInterfaceEnabled()</c>.</param>
    /// <returns>The strip, or <see langword="null"/> when the archive misses the button set.</returns>
    public static ControlPanel? Create(AssetBundle? assets, bool evilInterface)
    {
        if (assets is null)
        {
            return null;
        }

        string assetName = evilInterface ? EvilInterfaceAssetName : GoodInterfaceAssetName;
        int highestFrame = Math.Max(Math.Max(RadarFrame, ButtonsFrame), Math.Max(StatusFrame, EndTurnFrame));

        if (assets.TryReadIcn(assetName) is not { Sprites.Count: > 0 } buttonSet
            || buttonSet.Sprites.Count <= highestFrame)
        {
            return null;
        }

        return new ControlPanel(
            buttonSet.Sprites[RadarFrame],
            buttonSet.Sprites[IconsFrame],
            buttonSet.Sprites[ButtonsFrame],
            buttonSet.Sprites[StatusFrame],
            buttonSet.Sprites[EndTurnFrame]);
    }

    /// <summary>
    /// Moves the strip and lays out its five tiles, <c>SetPos()</c>.
    /// </summary>
    /// <param name="x">Left edge of the strip.</param>
    /// <param name="y">Top edge of the strip.</param>
    /// <remarks>
    /// The reference assigns <c>rx_radar</c> from <c>x</c> and then offsets every following tile by a further 36
    /// pixels, so the tiles are always in this fixed order.
    /// </remarks>
    public void SetPos(int x, int y)
    {
        Area = new Rect(x, y, PanelWidth, PanelHeight);
        RadarRect = new Rect(x, y, ButtonSize, ButtonSize);
        IconsRect = new Rect(x + ButtonSize, y, ButtonSize, ButtonSize);
        ButtonsRect = new Rect(x + (ButtonSize * 2), y, ButtonSize, ButtonSize);
        StatusRect = new Rect(x + (ButtonSize * 3), y, ButtonSize, ButtonSize);
        EndTurnRect = new Rect(x + (ButtonSize * 4), y, ButtonSize, ButtonSize);
    }

    /// <summary>
    /// Reports which tile was clicked, <c>QueueEventProcessing()</c>.
    /// </summary>
    /// <param name="events">The frame's events.</param>
    /// <returns>
    /// The tile under the click, or <see cref="ControlPanelButton.None"/> when the click was somewhere else -
    /// the reference calls the matching <c>Event*()</c> of the orchestrator and returns <c>GameMode::CANCEL</c> for
    /// everything but the end turn button.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public ControlPanelButton QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.MouseClickLeft(RadarRect))
        {
            return ControlPanelButton.Radar;
        }

        if (events.MouseClickLeft(IconsRect))
        {
            return ControlPanelButton.Icons;
        }

        if (events.MouseClickLeft(ButtonsRect))
        {
            return ControlPanelButton.Buttons;
        }

        if (events.MouseClickLeft(StatusRect))
        {
            return ControlPanelButton.Status;
        }

        if (events.MouseClickLeft(EndTurnRect))
        {
            return ControlPanelButton.EndTurn;
        }

        return ControlPanelButton.None;
    }

    /// <summary>
    /// Paints the strip at 50% opacity, <c>_redraw()</c>.
    /// </summary>
    /// <param name="target">Frame buffer to paint into.</param>
    /// <param name="gamePalette">The game palette, needed by <see cref="ImageOps.AlphaBlit(Sprite, PixelBuffer, Point, byte, Palette)"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Redraw(PixelBuffer target, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(gamePalette);

        ImageOps.AlphaBlit(_radar, target, RadarRect.Position, TileAlpha, gamePalette);
        ImageOps.AlphaBlit(_icons, target, IconsRect.Position, TileAlpha, gamePalette);
        ImageOps.AlphaBlit(_buttons, target, ButtonsRect.Position, TileAlpha, gamePalette);
        ImageOps.AlphaBlit(_status, target, StatusRect.Position, TileAlpha, gamePalette);
        ImageOps.AlphaBlit(_end, target, EndTurnRect.Position, TileAlpha, gamePalette);
    }
}

/// <summary>
/// Which of the five tiles of the <see cref="ControlPanel"/> was clicked.
/// </summary>
/// <remarks>
/// The reference turns these into <c>AdventureMap::EventSwitchShowRadar()</c> and friends, and the last one into
/// <c>AdventureMap::EventEndTurn()</c>; the port reports the tile so the orchestrator stays in charge of the events.
/// </remarks>
public enum ControlPanelButton
{
    /// <summary>No tile was clicked.</summary>
    None,

    /// <summary>Show or hide the radar, <c>EventSwitchShowRadar()</c>.</summary>
    Radar,

    /// <summary>Show or hide the icons panel, <c>EventSwitchShowIcons()</c>.</summary>
    Icons,

    /// <summary>Show or hide the buttons panel, <c>EventSwitchShowButtons()</c>.</summary>
    Buttons,

    /// <summary>Show or hide the status panel, <c>EventSwitchShowStatus()</c>.</summary>
    Status,

    /// <summary>End the turn, <c>EventEndTurn()</c>.</summary>
    EndTurn,
}
