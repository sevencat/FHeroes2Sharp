using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using CastleTown = FHeroes2.Game.Castle.Castle;
using RedrawMask = FHeroes2.UI.Interface.Redraw;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The two lists of the icons panel, <c>HeroesCastlesIcons</c> (<c>interface_icons.h:30-35</c>).
/// </summary>
[Flags]
public enum HeroesCastlesIcons
{
    /// <summary>No list at all.</summary>
    None = 0,

    /// <summary>The heroes of the current kingdom.</summary>
    Heroes = 1,

    /// <summary>The castles of the current kingdom.</summary>
    Castles = 2,

    /// <summary>Both lists, <c>ANY</c> in the reference.</summary>
    Any = Heroes | Castles,
}

/// <summary>
/// What the icons panel asks the adventure interface to do after an event. The reference calls
/// <c>Interface::AdventureMap</c> directly; this port reports the call to the host instead.
/// </summary>
public enum IconsPanelActionKind
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>The border was grabbed, <see cref="IconsPanel.Dragging"/> holds the drag.</summary>
    StartedDragging,

    /// <summary>Focus the hero, <c>SetFocus( hero, false )</c> plus <c>RedrawFocus()</c>.</summary>
    FocusHero,

    /// <summary>Focus the castle, <c>SetFocus( castle )</c> plus <c>RedrawFocus()</c>.</summary>
    FocusCastle,

    /// <summary>Open the hero dialog, <c>Game::OpenHeroesDialog()</c>.</summary>
    OpenHeroDialog,

    /// <summary>Open the castle dialog, <c>Game::OpenCastleDialog()</c>.</summary>
    OpenCastleDialog,

    /// <summary>Show the hero quick info, <c>Dialog::QuickInfo()</c>.</summary>
    ShowHeroQuickInfo,

    /// <summary>Show the castle quick info, <c>Dialog::QuickInfo()</c>.</summary>
    ShowCastleQuickInfo,
}

/// <summary>
/// The outcome of <see cref="IconsPanel.QueueEventProcessing"/>: a kind plus the hero or castle it is about.
/// </summary>
/// <param name="Kind">What the host should do.</param>
/// <param name="Hero">The hero of the event, for the hero kinds.</param>
/// <param name="Castle">The castle of the event, for the castle kinds.</param>
public readonly record struct IconsPanelAction(IconsPanelActionKind Kind, Hero? Hero, CastleTown? Castle)
{
    /// <summary>Nothing to do.</summary>
    public static IconsPanelAction None => default;

    /// <summary>The border drag started.</summary>
    public static IconsPanelAction StartedDragging => new(IconsPanelActionKind.StartedDragging, null, null);

    /// <summary>Focus the hero.</summary>
    /// <param name="hero">The hero to focus.</param>
    public static IconsPanelAction Focus(Hero hero) => new(IconsPanelActionKind.FocusHero, hero, null);

    /// <summary>Focus the castle.</summary>
    /// <param name="castle">The castle to focus.</param>
    public static IconsPanelAction Focus(CastleTown castle) => new(IconsPanelActionKind.FocusCastle, null, castle);

    /// <summary>Open the hero dialog.</summary>
    /// <param name="hero">The hero to open.</param>
    public static IconsPanelAction OpenHeroDialog(Hero hero) => new(IconsPanelActionKind.OpenHeroDialog, hero, null);

    /// <summary>Open the castle dialog.</summary>
    /// <param name="castle">The castle to open.</param>
    public static IconsPanelAction OpenCastleDialog(CastleTown castle) =>
        new(IconsPanelActionKind.OpenCastleDialog, null, castle);

    /// <summary>Show the hero quick info.</summary>
    /// <param name="hero">The hero to describe.</param>
    public static IconsPanelAction ShowHeroQuickInfo(Hero hero) =>
        new(IconsPanelActionKind.ShowHeroQuickInfo, hero, null);

    /// <summary>Show the castle quick info.</summary>
    /// <param name="castle">The castle to describe.</param>
    public static IconsPanelAction ShowCastleQuickInfo(CastleTown castle) =>
        new(IconsPanelActionKind.ShowCastleQuickInfo, null, castle);
}

/// <summary>
/// The column of hero and castle portraits on the right edge of the adventure screen, the port of
/// <c>Interface::IconsPanel</c> (<c>interface_icons.h:186-240</c>, <c>interface_icons.cpp:317-526</c>).
/// </summary>
/// <remarks>
/// <para>
/// The panel is two <see cref="ListBox{TItem}"/>s side by side: the left column shows the heroes of the current
/// kingdom, the right one its castles. Each column is 56 pixels wide (a 46x22 portrait plus its scroll bar) and its
/// slots are 32 pixels high; the castle column starts 72 pixels to the right of the hero column. Both are painted with
/// the <c>ADVBORD</c>/<c>ADVBORDE</c> artwork of the rest of the adventure interface, and the selected row is framed by
/// a runtime-painted cyan frame — the same trick as the radar cursor. A single click focuses the hero or castle, a
/// double click opens its dialog, the right button asks for the quick info, and holding
/// <c>WORLD_MOVE_HERO_PORTRAIT_UP_MODIFIER</c> while clicking a hero moves it to the front of the kingdom's hero list.
/// </para>
/// <para>
/// Deviations from the reference:
/// <list type="bullet">
/// <item>the world, the frame buffer and the sprite source are constructor parameters instead of the globals
/// <c>world</c>, <c>Display::instance()</c> and <c>Assets::getImage()</c>;</item>
/// <item><c>isEvilInterfaceEnabled()</c> and <c>ShowIcons()</c> are the <see cref="EvilInterface"/> and
/// <see cref="ShowIconsEnabled"/> properties, which the host keeps in sync with <see cref="Settings"/>, and
/// <c>isHideInterfaceEnabled()</c> is <see cref="BorderWindow.HideInterfaceEnabled"/>;</item>
/// <item>the portrait of a hero row is painted by <see cref="HeroIcons"/>, because <c>heroes.cpp</c> has no
/// <c>Heroes::PortraitRedraw( PORT_SMALL )</c> yet in this port;</item>
/// <item><c>SavePosition()</c> keeps the offset in <see cref="Settings.PosIcons"/> only: writing it to
/// <c>.fh2config</c> waits for the configuration writer;</item>
/// <item>the lists keep their own <see cref="List{T}"/> of rows, filled by <see cref="ResetIcons"/>, because
/// <see cref="Kingdom.Heroes"/> and <see cref="Kingdom.Castles"/> are read-only views.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class IconsPanel : BorderWindow
{
    /// <summary>Width of the panel, <c>144</c> (<c>interface_icons.cpp:384</c>).</summary>
    public const int PanelWidth = 144;

    /// <summary>Width of one slot, <c>iconsCursorWidth</c> (<c>interface_icons.cpp:54</c>).</summary>
    public const int CursorWidth = IconBar.SlotWidth;

    /// <summary>Height of one slot, <c>iconsCursorHeight</c> (<c>interface_icons.cpp:55</c>).</summary>
    public const int CursorHeight = IconBar.SlotHeight;

    /// <summary>Width of the portrait inside a slot, <c>IconsBar::getItemWidth()</c>.</summary>
    public const int ItemWidth = IconBar.IconWidth;

    /// <summary>Height of the portrait inside a slot, <c>IconsBar::getItemHeight()</c>.</summary>
    public const int ItemHeight = IconBar.IconHeight;

    /// <summary>Offset of the portrait inside its slot, <c>+ 5</c> in the reference.</summary>
    public const int ItemInset = IconBar.IconInset;

    /// <summary>Distance between two slots, <c>22 + 10</c> (<c>interface_icons.cpp:432</c>).</summary>
    public const int RowGap = 10;

    /// <summary>X offset of the scroll bar groove inside a column, <c>iconsCursorWidth + 3</c>.</summary>
    public const int ScrollBarOffsetX = IconBar.SlotWidth + 3;

    /// <summary>Y offset of the scroll bar groove inside a column, <c>19</c>.</summary>
    public const int ScrollBarOffsetY = 19;

    /// <summary>X offset of the two scroll buttons inside a column, <c>iconsCursorWidth + 1</c>.</summary>
    public const int ScrollBarButtonOffsetX = IconBar.SlotWidth + 1;

    /// <summary>Y offset of the upper scroll button inside a column, <c>1</c>.</summary>
    public const int ScrollBarTopButtonOffsetY = 1;

    /// <summary>Distance of the lower scroll button from the bottom of the column, <c>15</c>.</summary>
    public const int ScrollBarBottomOffset = 15;

    /// <summary>Width of the scroll bar groove, <c>10</c>.</summary>
    public const int ScrollBarWidth = 10;

    /// <summary>Height a scroll bar groove loses to its two margins, <c>38</c>.</summary>
    public const int ScrollBarHeightInset = 38;

    /// <summary>Distance between the two columns, <c>setPos( rect.x + 72, rect.y )</c>.</summary>
    public const int ColumnGap = IconBar.ColumnGap;

    /// <summary>Number of slots of a hidden adventure interface, <c>2</c>.</summary>
    public const int HiddenIconsCount = 2;

    /// <summary>Number of slots of an interface up to 640 pixels high, <c>4</c>.</summary>
    public const int ShortIconsCount = 4;

    /// <summary>Number of slots of an interface between 640 and 736 pixels high, <c>7</c>.</summary>
    public const int MediumIconsCount = 7;

    /// <summary>Number of slots of an interface 736 pixels high or higher, <c>8</c>.</summary>
    public const int TallIconsCount = 8;

    private readonly PixelBuffer _display;
    private readonly World _world;
    private readonly IViewWorldSpriteSource _sprites;
    private readonly Sprite _marker;
    private readonly HeroIcons _heroesIcons;
    private readonly CastleIcons _castleIcons;
    private IconsPanelAction _action;

    /// <summary>
    /// Builds the panel, <c>Interface::IconsPanel()</c> (<c>interface_icons.cpp:317-336</c>).
    /// </summary>
    /// <param name="interface">The adventure interface the redraw mask goes to.</param>
    /// <param name="display">The frame buffer the panel paints into.</param>
    /// <param name="world">The world the hero and castle lists come from.</param>
    /// <param name="sprites">The <c>ADVBORD</c> / <c>LOCATORS</c> / <c>SCROLL</c> artwork.</param>
    /// <param name="evilInterface">Whether the <c>*E</c> artwork is used.</param>
    public IconsPanel(BaseInterface @interface, PixelBuffer display, World world, IViewWorldSpriteSource sprites,
        bool evilInterface = false)
        : base(display, new Rect(0, 0, PanelWidth, ShortIconsCount * CursorHeight),
            GetBorderImage(sprites, evilInterface))
    {
        ArgumentNullException.ThrowIfNull(@interface);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sprites);

        Interface = @interface;
        _display = display;
        _world = world;
        _sprites = sprites;
        EvilInterface = evilInterface;
        ShowIconsEnabled = Settings.Get().IsShowIcons;

        _marker = new Sprite(CursorWidth, CursorHeight);
        DrawSelectionFrame(_marker, EmptyButtons.GetColorId(0xA0, 0xE0, 0xE0));

        _heroesIcons = new HeroIcons(this, world, sprites, _marker);
        _castleIcons = new CastleIcons(this, sprites, _marker);
    }

    /// <summary>The adventure interface the redraw mask goes to.</summary>
    public BaseInterface Interface { get; }

    /// <summary>Whether the <c>*E</c> artwork is used, <c>isEvilInterfaceEnabled()</c>.</summary>
    public bool EvilInterface { get; }

    /// <summary>Whether the panel is drawn while the adventure interface is hidden, <c>ShowIcons()</c>.</summary>
    public bool ShowIconsEnabled { get; set; }

    /// <summary>The border drag of the last event, <c>_border</c> in the reference.</summary>
    public BorderDrag? Dragging { get; private set; }

    /// <summary>The hero list of the panel.</summary>
    public HeroIcons HeroesIcons => _heroesIcons;

    /// <summary>The castle list of the panel.</summary>
    public CastleIcons CastleIcons => _castleIcons;

    /// <summary>Whether the panel is drawn at all, <c>IconsPanel::isVisible()</c>.</summary>
    public bool IsVisible() => !HideInterfaceEnabled || ShowIconsEnabled;

    /// <summary>
    /// Whether the panel is drawn at all for the given settings, <c>IconsPanel::isVisible()</c>.
    /// </summary>
    /// <param name="settings">The settings to ask.</param>
    public static bool IsVisible(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return !settings.IsHideInterface || settings.IsShowIcons;
    }

    /// <summary>
    /// Places the panel and lays out both lists, <c>IconsPanel::setPos()</c> (<c>interface_icons.cpp:338-400</c>).
    /// </summary>
    /// <param name="x">Left edge of the panel in screen coordinates.</param>
    /// <param name="y">Top edge of the panel in screen coordinates.</param>
    public override void SetPos(int x, int y)
    {
        int iconsCount;

        if (HideInterfaceEnabled)
        {
            iconsCount = HiddenIconsCount;
        }
        else
        {
            int countHeight = (_display.Height - 640) / CursorHeight;

            iconsCount = countHeight > 3 ? TallIconsCount : (countHeight < 3 ? ShortIconsCount : MediumIconsCount);
        }

        SetPosition(x, y, PanelWidth, iconsCount * CursorHeight);

        _heroesIcons.SetIconsCount(iconsCount);
        _castleIcons.SetIconsCount(iconsCount);

        Rect area = Area;
        Kingdom kingdom = _world.GetKingdom(Settings.Get().Players.CurrentColor);

        _heroesIcons.SetPos(area.X, area.Y, kingdom.Heroes);
        _castleIcons.SetPos(area.X + ColumnGap, area.Y, kingdom.Castles);
    }

    /// <inheritdoc />
    public override void SavePosition() => Settings.Get().SetPosIcons(GetRect.Position);

    /// <summary>
    /// Draws the whole panel, <c>IconsPanel::_redraw()</c> (<c>interface_icons.cpp:402-420</c>).
    /// </summary>
    public void RedrawPanel()
    {
        if (!IsVisible())
        {
            return;
        }

        if (HideInterfaceEnabled)
        {
            Redraw();
        }

        _heroesIcons.Redraw(_display);
        _castleIcons.Redraw(_display);
    }

    /// <summary>
    /// Draws one half of the panel, <c>IconsPanel::_redrawIcons()</c> (<c>interface_icons.cpp:422-447</c>).
    /// </summary>
    /// <param name="type">Which list to draw; both bits draw both lists.</param>
    /// <remarks>
    /// Unlike <see cref="RedrawPanel"/> this repaints no border: it exists for the redraw mask, whose
    /// <c>REDRAW_HEROES</c> / <c>REDRAW_CASTLES</c> bits mean "the rows of one list changed", not "the panel is dirty".
    /// </remarks>
    public void RedrawIcons(HeroesCastlesIcons type)
    {
        if (!IsVisible())
        {
            return;
        }

        if (type.HasFlag(HeroesCastlesIcons.Heroes))
        {
            _heroesIcons.Redraw(_display);
        }

        if (type.HasFlag(HeroesCastlesIcons.Castles))
        {
            _castleIcons.Redraw(_display);
        }
    }

    /// <summary>
    /// Refills the lists from the current kingdom, <c>IconsPanel::resetIcons()</c>
    /// (<c>interface_icons.cpp:449-484</c>).
    /// </summary>
    /// <remarks>
    /// The lists are left alone while a computer controls the kingdom and the human has not handed the slot to the
    /// automatic mode, exactly like the reference. Unlike the reference this method does not request a redraw; the
    /// reference callers repaint explicitly.
    /// </remarks>
    /// <param name="type">Which lists to refill.</param>
    public void ResetIcons(HeroesCastlesIcons type)
    {
        PlayerColor color = Settings.Get().Players.CurrentColor;
        Kingdom kingdom = _world.GetKingdom(color);
        Player? player = Players.Get(color);

        if (kingdom.IsControlAi && (player is null || !player.IsAiAutoControlMode))
        {
            return;
        }

        if ((type & HeroesCastlesIcons.Heroes) != 0)
        {
            _heroesIcons.SetRows(kingdom.Heroes);
            _heroesIcons.Unselect();
        }

        if ((type & HeroesCastlesIcons.Castles) != 0)
        {
            _castleIcons.SetRows(kingdom.Castles);
            _castleIcons.Unselect();
        }
    }

    /// <summary>Selects a hero in its list, <c>IconsPanel::select( const Heroes * )</c>.</summary>
    /// <remarks>The castle list loses its selection, as in the reference.</remarks>
    /// <param name="hero">The hero to select, or <see langword="null"/> to unselect.</param>
    public void Select(Hero? hero)
    {
        _castleIcons.Unselect();
        _heroesIcons.SelectRow(hero);
    }

    /// <summary>Selects a castle in its list, <c>IconsPanel::select( const Castle * )</c>.</summary>
    /// <remarks>The hero list loses its selection, as in the reference.</remarks>
    /// <param name="castle">The castle to select, or <see langword="null"/> to unselect.</param>
    public void Select(CastleTown? castle)
    {
        _heroesIcons.Unselect();
        _castleIcons.SelectRow(castle);
    }

    /// <summary>Whether one of the lists has a selection, <c>IconsPanel::isSelected()</c>.</summary>
    /// <param name="type">Which lists to ask.</param>
    public bool IsSelected(HeroesCastlesIcons type) =>
        ((type & HeroesCastlesIcons.Heroes) != 0 && _heroesIcons.IsSelected)
        || ((type & HeroesCastlesIcons.Castles) != 0 && _castleIcons.IsSelected);

    /// <summary>Hides the rows of the given lists, <c>IconsPanel::hideIcons()</c>.</summary>
    /// <param name="type">Which lists to hide.</param>
    public void HideIcons(HeroesCastlesIcons type) => ShowIconsOf(type, false);

    /// <summary>Shows the rows of the given lists, <c>IconsPanel::showIcons()</c>.</summary>
    /// <param name="type">Which lists to show.</param>
    public void ShowIcons(HeroesCastlesIcons type) => ShowIconsOf(type, true);

    /// <summary>
    /// Handles one event of the panel, <c>IconsPanel::QueueEventProcessing()</c>
    /// (<c>interface_icons.cpp:452-500</c>).
    /// </summary>
    /// <remarks>
    /// The border of the base class is handled first, like <c>ListBox::QueueEventProcessing()</c> is called first in
    /// the reference; the two overloads differ by the second parameter, so no hiding is involved.
    /// </remarks>
    /// <param name="events">The event to handle.</param>
    /// <param name="moveHeroPortraitPressed">
    /// Whether <c>WORLD_MOVE_HERO_PORTRAIT_UP_MODIFIER</c> is held down: a click on a hero then moves it to the front
    /// of the kingdom's hero list instead of focusing it.
    /// </param>
    /// <returns>What the host should do next.</returns>
    public IconsPanelAction QueueEventProcessing(LocalEvent events, bool moveHeroPortraitPressed = false)
    {
        ArgumentNullException.ThrowIfNull(events);

        _action = IconsPanelAction.None;
        MoveHeroPortraitPressed = moveHeroPortraitPressed;
        Dragging = null;

        CaptureMouse(events);

        if (ShowIconsEnabled && base.QueueEventProcessing(events) is { } drag)
        {
            Dragging = drag;
            SetRedraw(HeroesCastlesIcons.Any);

            return IconsPanelAction.StartedDragging;
        }

        if (_heroesIcons.QueueEventProcessing(events))
        {
            if (_heroesIcons.IsSelected)
            {
                _castleIcons.Unselect();
            }

            SetRedraw(HeroesCastlesIcons.Any);

            return _action;
        }

        if (_castleIcons.QueueEventProcessing(events))
        {
            if (_castleIcons.IsSelected)
            {
                _heroesIcons.Unselect();
            }

            SetRedraw(HeroesCastlesIcons.Any);

            return _action;
        }

        return IconsPanelAction.None;
    }

    /// <summary>Whether the modifier of the portrait reordering is held down.</summary>
    internal bool MoveHeroPortraitPressed { get; private set; }

    /// <summary>Records the action the list hooks produced.</summary>
    /// <param name="action">The action to report.</param>
    internal void Report(IconsPanelAction action) => _action = action;

    /// <summary>
    /// Moves a hero to the front of the kingdom's hero list, <c>IconsPanel::HeroesIcons::ActionListLongPress()</c>
    /// (<c>interface_icons.cpp:106-124</c>).
    /// </summary>
    /// <param name="hero">The hero to move.</param>
    internal void RotateHeroPortraits(Hero hero)
    {
        if (!_world.GetKingdom(Settings.Get().Players.CurrentColor).MoveHeroToFront(hero))
        {
            return;
        }

        _heroesIcons.MoveRowToFront(hero);
        _action = IconsPanelAction.Focus(hero);
    }

    private void ShowIconsOf(HeroesCastlesIcons type, bool show)
    {
        if ((type & HeroesCastlesIcons.Heroes) != 0)
        {
            _heroesIcons.ShowRows(show);
        }

        if ((type & HeroesCastlesIcons.Castles) != 0)
        {
            _castleIcons.ShowRows(show);
        }
    }

    /// <summary>Queues both icon lists for a repaint, <c>setRedraw()</c> (<c>interface_icons.h:189-192</c>).</summary>
    public void SetRedraw() => SetRedraw(HeroesCastlesIcons.Any);

    /// <summary>Queues one icon list for a repaint, <c>setRedraw( const HeroesCastlesIcons )</c>.</summary>
    /// <param name="type">The list to repaint.</param>
    /// <remarks>参考：<c>interface_icons.cpp:353-372</c>。</remarks>
    public void SetRedraw(HeroesCastlesIcons type)
    {
        if (!IsVisible())
        {
            return;
        }

        RedrawMask mask = type switch
        {
            HeroesCastlesIcons.Heroes => RedrawMask.Heroes,
            HeroesCastlesIcons.Castles => RedrawMask.Castles,
            _ => RedrawMask.Icons,
        };

        Interface.SetRedraw(mask);
    }

    /// <summary>
    /// Paints the cyan frame of the selected row, <c>fheroes2::DrawBorder( _sfMarker, color )</c>
    /// (<c>interface_icons.cpp:330-334</c>).
    /// </summary>
    private static void DrawSelectionFrame(Sprite marker, byte color) => ImageOps.DrawBorder(marker.Image, color);

    /// <summary>Reads the border artwork of the panel, called from the base constructor initializer.</summary>
    /// <param name="sprites">The artwork source.</param>
    /// <param name="evilInterface">Whether the <c>ADVBORDE</c> art is used.</param>
    /// <returns>The border image, or <see langword="null"/> when it is missing.</returns>
    private static Sprite? GetBorderImage(IViewWorldSpriteSource sprites, bool evilInterface)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        return sprites.GetSprite(evilInterface ? IcnType.Advborde : IcnType.Advbord, 0);
    }
}

/// <summary>
/// One of the two lists of <see cref="IconsPanel"/>, the port of <c>Interface::IconsBar</c>
/// (<c>interface_icons.h:38-140</c>) on top of <see cref="ListBox{TItem}"/>.
/// </summary>
/// <remarks>
/// The reference inherits from both <c>ListBox<T></c> and <c>IconsBar</c>; C# has no multiple inheritance, so this
/// class plays both roles.
/// </remarks>
/// <typeparam name="TRow">The hero or castle the list shows.</typeparam>
public abstract class IconsBar<TRow> : ListBox<TRow>
    where TRow : class
{
    private readonly List<TRow> _rows = new();
    private Sprite? _slider;
    private bool _show = true;

    /// <summary>
    /// Builds a list, <c>IconsBar::IconsBar()</c>.
    /// </summary>
    /// <param name="panel">The panel the list reports to.</param>
    /// <param name="spriteSource">The <c>ADVBORD</c> / <c>LOCATORS</c> artwork.</param>
    /// <param name="marker">The frame painted around the selected row.</param>
    /// <param name="topLeft">Where the list starts.</param>
    /// <param name="evilInterface">Whether the <c>*E</c> artwork is used.</param>
    protected IconsBar(IconsPanel panel, IViewWorldSpriteSource spriteSource, Sprite marker, Point topLeft,
        bool evilInterface)
        : base(topLeft)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(spriteSource);
        ArgumentNullException.ThrowIfNull(marker);

        Panel = panel;
        Sprites = spriteSource;
        Marker = marker;
        EvilInterface = evilInterface;
        IconsCount = IconsPanel.ShortIconsCount;
    }

    /// <summary>The panel the list belongs to.</summary>
    protected IconsPanel Panel { get; }

    /// <summary>The artwork of the list.</summary>
    protected IViewWorldSpriteSource Sprites { get; }

    /// <summary>The frame painted around the selected row.</summary>
    protected Sprite Marker { get; }

    /// <summary>Whether the <c>*E</c> artwork is used.</summary>
    protected bool EvilInterface { get; }

    /// <summary>Number of slots of the list, <c>_iconsCount</c>.</summary>
    public int IconsCount { get; private set; }

    /// <summary>Whether the rows of the list are drawn, <c>_show</c>.</summary>
    public bool Show => _show;

    /// <summary>Sets the number of visible slots, <c>setIconsCount()</c>.</summary>
    /// <param name="count">The number of slots.</param>
    /// <remarks>
    /// Only the counter is stored, like the reference: the visible item count reaches the base list through
    /// <see cref="SetPos(int,int,IReadOnlyList{TRow})"/>, which resets the list after saving the selection.
    /// </remarks>
    public void SetIconsCount(int count) => IconsCount = count;

    /// <summary>
    /// Places the list and points it at new rows, <c>HeroesIcons::setPos()</c> / <c>CastleIcons::setPos()</c>
    /// (<c>interface_icons.cpp:190-220</c> and <c>:303-333</c>).
    /// </summary>
    /// <param name="px">Left edge of the column.</param>
    /// <param name="py">Top edge of the column.</param>
    /// <param name="rows">The rows of the current kingdom.</param>
    /// <remarks>The selection is kept when the selected row is still in <paramref name="rows"/>.</remarks>
    public void SetPos(int px, int py, IReadOnlyList<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        TRow? selected = IsSelected ? GetCurrent() : null;
        int columnHeight = IconsCount * IconsPanel.CursorHeight;

        SetTopLeft(new Point(px, py));

        SetScrollBarArea(new Rect(px + IconsPanel.ScrollBarOffsetX, py + IconsPanel.ScrollBarOffsetY,
            IconsPanel.ScrollBarWidth, columnHeight - IconsPanel.ScrollBarHeightInset));

        IcnType scrollType = EvilInterface ? IcnType.Scrolle : IcnType.Scroll;

        SetScrollButtonUp(CreateButton(scrollType, 0, 1,
            new Point(px + IconsPanel.ScrollBarButtonOffsetX, py + IconsPanel.ScrollBarTopButtonOffsetY)));
        SetScrollButtonDown(CreateButton(scrollType, 2, 3,
            new Point(px + IconsPanel.ScrollBarButtonOffsetX, py + columnHeight - IconsPanel.ScrollBarBottomOffset)));

        SetAreaMaxItems(IconsCount);
        SetAreaItems(new Rect(px, py, IconsPanel.CursorWidth, columnHeight));
        DisableHotkeys(true);

        SetRows(rows);
        Reset();

        if (selected is not null)
        {
            SetCurrent(selected);
        }
    }

    /// <summary>Refills the list, the <c>SetListContent()</c> half of <c>IconsPanel::resetIcons()</c>.</summary>
    /// <param name="rows">The rows of the current kingdom.</param>
    public void SetRows(IReadOnlyList<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _rows.Clear();
        _rows.AddRange(rows);
        SetListContent(_rows);
        RebuildSlider();
    }

    /// <summary>Selects a row, or clears the selection when <paramref name="row"/> is <see langword="null"/>.</summary>
    /// <param name="row">The row to select.</param>
    public void SelectRow(TRow? row)
    {
        if (row is null)
        {
            Unselect();
        }
        else
        {
            SetCurrent(row);
        }
    }

    /// <summary>Moves a row to the front of the list, <c>std::rotate</c> in <c>ActionListLongPress()</c>.</summary>
    /// <param name="row">The row to move.</param>
    public void MoveRowToFront(TRow row)
    {
        if (!_rows.Remove(row))
        {
            return;
        }

        _rows.Insert(0, row);
        SetListContent(_rows);
        SetCurrent(row);
    }

    /// <summary>Shows or hides the rows of the list, <c>showIcons()</c> / <c>hideIcons()</c>.</summary>
    /// <param name="show">Whether the rows are drawn.</param>
    public void ShowRows(bool show)
    {
        _show = show;

        if (IconsPanel.IsVisible(Settings.Get()))
        {
            SetScrollBarImage(show ? _slider : null);
        }
    }

    /// <summary>
    /// Replaces the scrollbar thumb and remembers it, so that <see cref="ShowRows"/> can put it back.
    /// </summary>
    /// <param name="slider">The new thumb, or <see langword="null"/> to drop it.</param>
    public void SetSliderImage(Sprite? slider)
    {
        _slider = slider;
        SetScrollBarImage(_show ? slider : null);
    }

    /// <summary>
    /// Rebuilds the thumb for the current row count and groove, <c>generateScrollbarSlider()</c> of the reference.
    /// </summary>
    private void RebuildSlider()
    {
        IcnType scrollType = EvilInterface ? IcnType.Scrolle : IcnType.Scroll;

        if (Sprites.GetSprite(scrollType, 4) is not { } original)
        {
            SetSliderImage(null);

            return;
        }

        Rect startArea = new(0, 0, original.Width, 8);
        Rect middleArea = new(0, 7, original.Width, 8);

        SetSliderImage(ScrollSlider.Create(original, false, ScrollBar.Area.Height, IconsCount, _rows.Count, startArea,
            middleArea));
    }

    /// <summary>Builds one of the two step buttons of the scroll bar, <c>SetScrollButtonUp()</c> of the reference.</summary>
    /// <param name="scrollType">The <c>SCROLL</c> artwork.</param>
    /// <param name="idleFrame">Frame of the released button.</param>
    /// <param name="pressedFrame">Frame of the pressed button.</param>
    /// <param name="position">Top-left corner of the button.</param>
    /// <returns>The button, or <see langword="null"/> when the artwork is missing.</returns>
    private ButtonBase? CreateButton(IcnType scrollType, int idleFrame, int pressedFrame, Point position)
    {
        if (Sprites.GetSprite(scrollType, idleFrame) is not { } idle ||
            Sprites.GetSprite(scrollType, pressedFrame) is not { } pressed)
        {
            return null;
        }

        ButtonSprite button = new(idle, pressed);
        button.SetPosition(position.X, position.Y);

        return button;
    }

    /// <inheritdoc />
    public override void RedrawBackground(Point offset, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        IcnType boardType = EvilInterface ? IcnType.Advborde : IcnType.Advbord;

        if (Sprites.GetSprite(boardType, 0) is not { } board)
        {
            return;
        }

        Rect source = new(board.Width - IconsPanel.PanelWidth - 16, IconsPanel.PanelWidth + 32, IconsPanel.PanelWidth / 2,
            IconsPanel.CursorHeight);

        ImageOps.Blit(output, offset, board.Image, source);

        source = new(source.X, source.Y + source.Height, source.Width, source.Height);

        int internalOffsetY = offset.Y + source.Height;

        if (IconsCount > 2)
        {
            for (int i = 0; i < IconsCount - 2; ++i)
            {
                ImageOps.Blit(output, new Point(offset.X, internalOffsetY), board.Image, source);
                internalOffsetY += IconsPanel.CursorHeight;
            }
        }

        source = new(source.X, source.Y + 2 * IconsPanel.CursorHeight, source.Width, source.Height);

        ImageOps.Blit(output, new Point(offset.X, internalOffsetY), board.Image, source);

        int validItemCount = _show ? Size : 0;
        IcnType slotType = EvilInterface ? IcnType.Locatore : IcnType.Locators;

        for (int i = validItemCount; i < IconsCount; ++i)
        {
            if (Sprites.GetSprite(slotType, IconBar.EmptySlotFrame(i)) is not { } slot)
            {
                continue;
            }

            ImageOps.Blit(output, new Point(offset.X + IconsPanel.ItemInset,
                offset.Y + IconsPanel.ItemInset + i * (IconsPanel.ItemHeight + IconsPanel.RowGap)), slot.Image,
                new Rect(0, 0, slot.Width, slot.Height));
        }
    }

    /// <summary>Paints the frame of the selected row, <c>fheroes2::Blit( _sfMarker, ... )</c>.</summary>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="output">The frame buffer.</param>
    protected void DrawMarker(int x, int y, PixelBuffer output) =>
        ImageOps.BlitSprite(output, new Point(x, y), Marker);
}

/// <summary>
/// The hero half of the icons panel, the port of <c>Interface::IconsPanel::HeroesIcons</c>
/// (<c>interface_icons.cpp:59-157</c>).
/// </summary>
public sealed class HeroIcons : IconsBar<Hero>
{
    private readonly World _world;

    /// <summary>
    /// Builds the hero list, <c>HeroesIcons::HeroesIcons()</c>.
    /// </summary>
    /// <param name="panel">The panel the list reports to.</param>
    /// <param name="world">The world the movement points come from.</param>
    /// <param name="sprites">The artwork of the list.</param>
    /// <param name="marker">The frame painted around the selected row.</param>
    public HeroIcons(IconsPanel panel, World world, IViewWorldSpriteSource sprites, Sprite marker)
        : base(panel, sprites, marker, new Point(0, 0), panel?.EvilInterface ?? false)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    /// <inheritdoc />
    public override void RedrawItem(Hero item, int ox, int oy, bool current, PixelBuffer output)
    {
        if (!Show)
        {
            return;
        }

        DrawPortrait(item, ox + IconsPanel.ItemInset, oy + IconsPanel.ItemInset, output);

        if (current)
        {
            DrawMarker(ox, oy, output);
        }
    }

    /// <inheritdoc />
    public override void ActionCurrentUp()
    {
        if (IsSelected && GetCurrent() is { } hero)
        {
            Panel.Report(IconsPanelAction.Focus(hero));
        }
    }

    /// <inheritdoc />
    public override void ActionCurrentDn()
    {
        if (IsSelected && GetCurrent() is { } hero)
        {
            Panel.Report(IconsPanelAction.Focus(hero));
        }
    }

    /// <inheritdoc />
    public override void ActionListSingleClick(Hero hero)
    {
        if (Panel.MoveHeroPortraitPressed)
        {
            Panel.RotateHeroPortraits(hero);
        }
        else
        {
            Panel.Report(IconsPanelAction.Focus(hero));
        }
    }

    /// <inheritdoc />
    public override void ActionListDoubleClick(Hero hero)
    {
        if (Panel.MoveHeroPortraitPressed)
        {
            Panel.RotateHeroPortraits(hero);
        }
        else
        {
            Panel.Report(IconsPanelAction.OpenHeroDialog(hero));
        }
    }

    /// <inheritdoc />
    public override void ActionListPressRight(Hero hero) => Panel.Report(IconsPanelAction.ShowHeroQuickInfo(hero));

    /// <inheritdoc />
    public override void ActionListLongPress(Hero hero) => Panel.RotateHeroPortraits(hero);

    private void DrawPortrait(Hero hero, int px, int py, PixelBuffer output) =>
        HeroPortraitPainter.DrawSmall(hero, Sprites, _world, new Point(px, py), output);
}

/// <summary>
/// The castle half of the icons panel, the port of <c>Interface::IconsPanel::CastleIcons</c>
/// (<c>interface_icons.cpp:159-303</c>).
/// </summary>
public sealed class CastleIcons : IconsBar<CastleTown>
{
    /// <summary>
    /// Builds the castle list, <c>CastleIcons::CastleIcons()</c>.
    /// </summary>
    /// <param name="panel">The panel the list reports to.</param>
    /// <param name="sprites">The artwork of the list.</param>
    /// <param name="marker">The frame painted around the selected row.</param>
    public CastleIcons(IconsPanel panel, IViewWorldSpriteSource sprites, Sprite marker)
        : base(panel, sprites, marker, new Point(0, 0), panel?.EvilInterface ?? false)
    {
    }

    /// <inheritdoc />
    public override void RedrawItem(CastleTown item, int ox, int oy, bool current, PixelBuffer output)
    {
        if (!Show)
        {
            return;
        }

        DrawCastleIcon(item, ox + IconsPanel.ItemInset, oy + IconsPanel.ItemInset, output);

        if (current)
        {
            DrawMarker(ox, oy, output);
        }
    }

    /// <inheritdoc />
    public override void ActionCurrentUp()
    {
        if (IsSelected && GetCurrent() is { } castle)
        {
            Panel.Report(IconsPanelAction.Focus(castle));
        }
    }

    /// <inheritdoc />
    public override void ActionCurrentDn()
    {
        if (IsSelected && GetCurrent() is { } castle)
        {
            Panel.Report(IconsPanelAction.Focus(castle));
        }
    }

    /// <inheritdoc />
    public override void ActionListSingleClick(CastleTown castle) => Panel.Report(IconsPanelAction.Focus(castle));

    /// <inheritdoc />
    public override void ActionListDoubleClick(CastleTown castle) =>
        Panel.Report(IconsPanelAction.OpenCastleDialog(castle));

    /// <inheritdoc />
    public override void ActionListPressRight(CastleTown castle) =>
        Panel.Report(IconsPanelAction.ShowCastleQuickInfo(castle));

    /// <summary>
    /// Paints the icon of a castle, <c>Interface::redrawCastleIcon()</c> (<c>interface_icons.cpp:340-350</c>) on top of
    /// <c>fheroes2::drawCastleIcon()</c>.
    /// </summary>
    /// <param name="castle">The castle to paint.</param>
    /// <param name="x">Left edge of the icon.</param>
    /// <param name="y">Top edge of the icon.</param>
    /// <param name="output">The frame buffer.</param>
    public void DrawCastleIcon(CastleTown castle, int x, int y, PixelBuffer output) =>
        CastleIconPainter.Draw(castle, Sprites, new Point(x, y), output, EvilInterface);
}
