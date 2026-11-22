using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>The geometry the kingdom overview shares between its two lists and its frame.</summary>
/// <remarks>
/// The reference keeps these numbers in the constructors of the lists and in the main loop
/// (<c>kingdom_overview.cpp:216-237</c>, <c>:245</c>, <c>:498-526</c>, <c>:610-664</c>); the port names them once so the
/// frame and the rows cannot drift apart.
/// </remarks>
public static class KingdomOverviewLayout
{
    /// <summary>Horizontal distance from the window's left edge to the scroll bar, <c>scrollbarOffset</c> (<c>:216</c>).</summary>
    public const int ScrollbarOffset = 626;

    /// <summary>How many rows fit at once, <c>SetAreaMaxItems( 4 )</c>.</summary>
    public const int VisibleRows = 4;

    /// <summary>The height of one row: the visible area divided by the row count.</summary>
    public const int RowHeight = 86;

    /// <summary>The width of the rows' area, <c>SetAreaItems( { dstx + 30, dsty + 17, 594, 344 } )</c>.</summary>
    public const int RowsWidth = 594;

    /// <summary>The area the rows are painted in, <c>SetAreaItems()</c> of both constructors.</summary>
    /// <param name="offset">Top-left corner of the window's active area.</param>
    /// <returns>The area.</returns>
    public static Rect ItemsArea(Point offset) =>
        new(offset.X + 30, offset.Y + 17, RowsWidth, VisibleRows * RowHeight);
}

/// <summary>
/// The shared half of the kingdom overview's two lists: the scroll bar, the frame around the rows and the bag of
/// requests a frame may fill, <c>StatsHeroesList</c> / <c>StatsCastlesList</c> of <c>kingdom_overview.cpp:169-685</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference's lists open the dialogs from inside <c>ActionListSingleClick()</c> and <c>ActionListCursor()</c>; the
/// port fills <see cref="Requests"/> and lets the window hand it to the scene.
/// </para>
/// <para>
/// <see cref="ListBox{T}.ActionListCursor"/> does not receive the frame's <see cref="LocalEvent"/>, while an army bar
/// needs it, so <see cref="QueueEventProcessing"/> remembers the frame the list is working on. That is the only
/// addition the port makes to the reference's event flow.
/// </para>
/// </remarks>
/// <typeparam name="T">The row type.</typeparam>
public abstract class KingdomOverviewList<T> : ListBox<T>
    where T : class
{
    /// <summary>The frame the list is processing, or <see langword="null"/> outside of a frame.</summary>
    private LocalEvent? _frame;

    /// <summary>Creates the empty list, the constructors of <c>:216-237</c> and <c>:498-526</c>.</summary>
    /// <param name="offset">Top-left corner of the window's active area, <c>ptRedraw</c>.</param>
    protected KingdomOverviewList(Point offset)
        : base(offset)
    {
    }

    /// <summary>What the lists asked their owner to open.</summary>
    public KingdomOverviewRequests Requests { get; } = new();

    /// <summary>Whether the whole window has to fade in again, <c>needFadeIn</c>.</summary>
    public bool NeedFadeIn => Requests.NeedFadeIn;

    /// <summary>Consumes one frame, remembering it for <see cref="ListBox{T}.ActionListCursor"/>.</summary>
    /// <param name="events">The frame's input.</param>
    /// <returns><see langword="true"/> when the list has to be painted again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public override bool QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _frame = events;

        try
        {
            return base.QueueEventProcessing(events);
        }
        finally
        {
            _frame = null;
        }
    }

    /// <summary>The frame the click handlers may look at, or <see langword="null"/>.</summary>
    protected LocalEvent? Frame => _frame;

    /// <summary>
    /// Lays out the scroll bar, the identical code of both constructors (<c>:220-235</c>, <c>:502-517</c>).
    /// </summary>
    /// <param name="artwork">The artwork of the scroll bar.</param>
    /// <param name="offset">Top-left corner of the window's active area.</param>
    protected void SetUpScrollBar(KingdomOverviewArtwork artwork, Point offset)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        Sprite back = artwork.ScrollbarBackground;
        int backHeight = back.Height;
        int offsetX = offset.X + KingdomOverviewLayout.ScrollbarOffset;

        SetScrollBarArea(new Rect(offsetX + 2, offset.Y + 18, back.Width, backHeight - 2));

        // The original thumb; UpdateScrollBarImage() stretches it to the number of rows afterwards.
        SetScrollBarImage(artwork.ScrollSlider);
        SetScrollButtonUp(new ButtonSprite(artwork.ScrollUpReleased, artwork.ScrollUpPressed, null, offsetX, offset.Y));
        SetScrollButtonDown(
            new ButtonSprite(artwork.ScrollDownReleased, artwork.ScrollDownPressed, null, offsetX, offset.Y + 20 + backHeight));
        SetAreaMaxItems(KingdomOverviewLayout.VisibleRows);
        SetAreaItems(KingdomOverviewLayout.ItemsArea(offset));
    }

    /// <summary>
    /// Paints the frame and the yellow lines shared by both lists, <c>redrawCommonBackground()</c> (<c>:95-120</c>).
    /// </summary>
    /// <param name="destination">Top-left corner of the list.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="artwork">The artwork.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    protected static void RedrawCommonBackground(Point destination, PixelBuffer output, KingdomOverviewArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(output);

        // The scroll bar groove.
        ImageOps.BlitSprite(
            output,
            new Point(destination.X + KingdomOverviewLayout.ScrollbarOffset + 1, destination.Y + 17),
            artwork.ScrollbarBackground);

        // One plate per visible row, with a horizontal yellow line under it.
        Sprite itemsBack = artwork.ItemsBackground;
        int offsetY = destination.Y + 17;
        int stepY = itemsBack.Height + 4;
        int overbackOffsetX = destination.X + 29;
        int overbackOffsetY = destination.Y + 13;

        for (int i = 0; i < KingdomOverviewLayout.VisibleRows; ++i, offsetY += stepY, overbackOffsetY += stepY)
        {
            ImageOps.BlitSprite(output, new Point(destination.X + 30, offsetY), itemsBack);
            ImageOps.BlitSprite(
                output, new Point(overbackOffsetX, overbackOffsetY), artwork.Overback, new Rect(29, 13, 595, 4));
        }

        // One vertical line, kept from a previous army selection that left it behind.
        ImageOps.BlitSprite(output, new Point(overbackOffsetX, destination.Y + 12), artwork.Overback, new Rect(29, 12, 1, 357));
    }

    /// <summary>Whether the cursor is inside a 30x30 icon slot of a row, <c>IconsBar::getItemWidth/Height</c>.</summary>
    /// <param name="point">The cursor.</param>
    /// <param name="left">Left edge of the slot.</param>
    /// <param name="top">Top edge of the slot.</param>
    /// <returns><see langword="true"/> when the cursor is on the slot.</returns>
    protected static bool IsOverIcon(Point point, int left, int top) =>
        new Rect(left, top, IconsPanel.ItemWidth, IconsPanel.ItemHeight).Contains(point);
}

/// <summary>The list of the kingdom's heroes, <c>StatsHeroesList</c> (<c>kingdom_overview.cpp:169-394</c>).</summary>
public sealed class KingdomHeroesList : KingdomOverviewList<KingdomHeroRow>
{
    private readonly KingdomOverviewArtwork _artwork;
    private readonly IGlyphSource _smallWhite;
    private readonly World _world;
    private readonly List<KingdomHeroRow> _rows = [];

    /// <summary>Creates the list, <c>StatsHeroesList::StatsHeroesList()</c> (<c>:216-237</c>).</summary>
    /// <param name="artwork">The artwork.</param>
    /// <param name="smallWhite">The font of the header and of the primary skills.</param>
    /// <param name="world">The world the heroes stand in.</param>
    /// <param name="offset">Top-left corner of the window's active area.</param>
    /// <param name="heroes">The kingdom's heroes.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public KingdomHeroesList(KingdomOverviewArtwork artwork, IGlyphSource smallWhite, World world, Point offset,
        IReadOnlyList<Hero> heroes)
        : base(offset)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(smallWhite);
        ArgumentNullException.ThrowIfNull(world);

        _artwork = artwork;
        _smallWhite = smallWhite;
        _world = world;

        SetUpScrollBar(artwork, offset);
        Refresh(heroes);
    }

    /// <summary>
    /// Rebuilds the rows when the kingdom's heroes changed, <c>StatsHeroesList::Refresh()</c> (<c>:239-273</c>).
    /// </summary>
    /// <param name="heroes">The kingdom's heroes as they are now.</param>
    /// <returns><see langword="true"/> when the rows were rebuilt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="heroes"/> is <see langword="null"/>.</exception>
    public bool Refresh(IReadOnlyList<Hero> heroes)
    {
        ArgumentNullException.ThrowIfNull(heroes);

        bool same = heroes.Count == _rows.Count;

        for (int i = 0; same && i < _rows.Count; ++i)
        {
            same = ReferenceEquals(heroes[i], _rows[i].Hero);
        }

        if (same && _rows.Count > 0)
        {
            return false;
        }

        int top = TopId;

        _rows.Clear();

        foreach (Hero hero in heroes)
        {
            _rows.Add(new KingdomHeroRow(hero, _world, _artwork, _smallWhite));
        }

        SetListContent(_rows);
        UpdateScrollBarImage();

        if (_rows.Count > 0)
        {
            SetTopVisibleItem(top);
        }

        return true;
    }

    /// <summary>Paints the header, its three titles and the shared background, <c>:280-303</c>.</summary>
    /// <param name="destination">Top-left corner of the list.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, new Point(destination.X + 30, destination.Y), _artwork.HeroHeader);

        DrawCentred("Hero/Stats", destination.X + 130, destination.Y + 3, output);
        DrawCentred("Skills", destination.X + 300, destination.Y + 3, output);
        DrawCentred("Artifacts", destination.X + 500, destination.Y + 3, output);

        RedrawCommonBackground(destination, output, _artwork);
    }

    /// <inheritdoc/>
    public override void RedrawItem(KingdomHeroRow item, int x, int y, bool current, PixelBuffer output) =>
        item.Redraw(x, y, output);

    /// <inheritdoc/>
    public override void ActionCurrentUp()
    {
        // The reference does nothing here.
    }

    /// <inheritdoc/>
    public override void ActionCurrentDn()
    {
        // The reference does nothing here.
    }

    /// <inheritdoc/>
    public override void ActionListSingleClick(KingdomHeroRow item)
    {
        // The reference only implements the overload with the cursor.
    }

    /// <inheritdoc/>
    public override void ActionListDoubleClick(KingdomHeroRow item)
    {
        // The reference only implements the overload with the cursor.
    }

    /// <inheritdoc/>
    public override void ActionListPressRight(KingdomHeroRow item)
    {
        // The reference only implements the overload with the cursor.
    }

    /// <summary>Opens the hero dialog on the portrait, <c>:275-287</c>.</summary>
    /// <param name="item">The row.</param>
    /// <param name="mousePosition">The cursor.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public override void ActionListSingleClick(KingdomHeroRow item, Point mousePosition, int offsetX, int offsetY)
    {
        if (!IsOverIcon(mousePosition, offsetX + 5, offsetY + 4))
        {
            return;
        }

        Requests.HeroDialog = item.Hero;
        Requests.NeedFadeIn = true;
    }

    /// <summary>A double click on the portrait is a single one, <c>:275-278</c>.</summary>
    /// <param name="item">The row.</param>
    /// <param name="mousePosition">The cursor.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public override void ActionListDoubleClick(KingdomHeroRow item, Point mousePosition, int offsetX, int offsetY) =>
        ActionListSingleClick(item, mousePosition, offsetX, offsetY);

    /// <summary>Shows the quick info of the hero on a right click, <c>:289-294</c>.</summary>
    /// <param name="item">The row.</param>
    /// <param name="mousePosition">The cursor.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public override void ActionListPressRight(KingdomHeroRow item, Point mousePosition, int offsetX, int offsetY)
    {
        if (IsOverIcon(mousePosition, offsetX + 5, offsetY + 4))
        {
            Requests.HeroInfo = item.Hero;
        }
    }

    /// <summary>
    /// Handles the army bar under the cursor, <c>StatsHeroesList::ActionListCursor()</c> (<c>:296-323</c>).
    /// </summary>
    /// <remarks>
    /// The reference also lets the artifact bar take the frame; the port's artifact and skill bars are read-only
    /// widgets without event handling (they were ported as consumers of a hero, not as editors), so only the army bar
    /// can answer. What it answers is passed up to the scene through <see cref="KingdomOverviewRequests.Army"/>.
    /// </remarks>
    /// <param name="item">The row under the cursor.</param>
    /// <param name="cursor">The cursor.</param>
    /// <returns><see langword="true"/> when the row consumed the frame.</returns>
    public override bool ActionListCursor(KingdomHeroRow item, Point cursor)
    {
        if (Frame is null || item.ArmyBar.RenderingArea.Contains(cursor) is false)
        {
            return false;
        }

        ArmyBarEvent armyEvent = item.ArmyBar.ProcessEvents(Frame);

        if (!armyEvent.Handled)
        {
            return false;
        }

        Requests.Army.Add(new KingdomArmyRequest(item.ArmyBar, null, armyEvent));

        return true;
    }

    private void DrawCentred(string text, int centreX, int y, PixelBuffer output) =>
        GlyphText.DrawLine(_smallWhite, output, new Point(centreX - (GlyphText.Measure(_smallWhite, text) / 2), y), text);
}

/// <summary>The list of the kingdom's towns, <c>StatsCastlesList</c> (<c>kingdom_overview.cpp:448-685</c>).</summary>
public sealed class KingdomCastlesList : KingdomOverviewList<KingdomCastleRow>
{
    private readonly KingdomOverviewArtwork _artwork;
    private readonly IGlyphSource _smallWhite;
    private readonly IGlyphSource _smallYellow;
    private readonly World _world;
    private readonly List<KingdomCastleRow> _rows = [];

    /// <summary>Creates the list, <c>StatsCastlesList::StatsCastlesList()</c> (<c>:498-526</c>).</summary>
    /// <param name="artwork">The artwork.</param>
    /// <param name="smallWhite">The font of the header, of the names and of the dwelling stock.</param>
    /// <param name="smallYellow">The font of the weekly growth of a dwelling.</param>
    /// <param name="world">The world, asked who is home in each town.</param>
    /// <param name="offset">Top-left corner of the window's active area.</param>
    /// <param name="castles">The kingdom's towns.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public KingdomCastlesList(KingdomOverviewArtwork artwork, IGlyphSource smallWhite, IGlyphSource smallYellow,
        World world, Point offset, IReadOnlyList<CastleTown> castles)
        : base(offset)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(smallWhite);
        ArgumentNullException.ThrowIfNull(smallYellow);
        ArgumentNullException.ThrowIfNull(world);

        _artwork = artwork;
        _smallWhite = smallWhite;
        _smallYellow = smallYellow;
        _world = world;

        SetUpScrollBar(artwork, offset);
        Refresh(castles);
    }

    /// <summary>
    /// Rebuilds the rows when the kingdom's towns changed, <c>StatsCastlesList::Refresh()</c> (<c>:478-496</c>).
    /// </summary>
    /// <param name="castles">The kingdom's towns as they are now.</param>
    /// <returns><see langword="true"/> when the rows were rebuilt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="castles"/> is <see langword="null"/>.</exception>
    public bool Refresh(IReadOnlyList<CastleTown> castles)
    {
        ArgumentNullException.ThrowIfNull(castles);

        bool same = castles.Count == _rows.Count;

        for (int i = 0; same && i < _rows.Count; ++i)
        {
            same = ReferenceEquals(castles[i], _rows[i].Castle);
        }

        if (same && _rows.Count > 0)
        {
            return false;
        }

        int top = TopId;

        _rows.Clear();

        foreach (CastleTown castle in castles)
        {
            _rows.Add(new KingdomCastleRow(castle, _world, _artwork, _smallWhite, _smallYellow));
        }

        SetListContent(_rows);
        UpdateScrollBarImage();

        if (_rows.Count > 0)
        {
            SetTopVisibleItem(top);
        }

        return true;
    }

    /// <summary>Refreshes the hero army bar of every row, <c>updateHeroArmyBars()</c> (<c>:483-497</c>).</summary>
    public void UpdateHeroArmyBars()
    {
        foreach (KingdomCastleRow row in _rows)
        {
            row.UpdateHeroArmyBar();
        }
    }

    /// <summary>Paints the header, its titles and the shared background, <c>:600-608</c>.</summary>
    /// <param name="destination">Top-left corner of the list.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, new Point(destination.X + 30, destination.Y), _artwork.CastleHeader);

        DrawCentred("Town/Castle", destination.X + 105, destination.Y + 3, output);
        DrawCentred("Garrison", destination.X + 275, destination.Y + 3, output);
        DrawCentred("Available", destination.X + 500, destination.Y + 3, output);

        RedrawCommonBackground(destination, output, _artwork);
    }

    /// <inheritdoc/>
    public override void RedrawItem(KingdomCastleRow item, int x, int y, bool current, PixelBuffer output) =>
        item.Redraw(x, y, output);

    /// <inheritdoc/>
    public override void ActionCurrentUp()
    {
        // The reference does nothing here.
    }

    /// <inheritdoc/>
    public override void ActionCurrentDn()
    {
        // The reference does nothing here.
    }

    /// <inheritdoc/>
    public override void ActionListSingleClick(KingdomCastleRow item)
    {
        // The reference only implements the overload with the cursor.
    }

    /// <inheritdoc/>
    public override void ActionListDoubleClick(KingdomCastleRow item)
    {
        // The reference only implements the overload with the cursor.
    }

    /// <inheritdoc/>
    public override void ActionListPressRight(KingdomCastleRow item)
    {
        // The reference only implements the overload with the cursor.
    }

    /// <summary>
    /// Opens the town dialog on its icon, or the hero's dialog on the portrait, <c>:528-560</c>.
    /// </summary>
    /// <param name="item">The row.</param>
    /// <param name="mousePosition">The cursor.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public override void ActionListSingleClick(KingdomCastleRow item, Point mousePosition, int offsetX, int offsetY)
    {
        if (IsOverIcon(mousePosition, offsetX + 17, offsetY + 19))
        {
            Requests.CastleDialog = item.Castle;
            Requests.NeedFadeIn = true;

            return;
        }

        if (_world.GetHero(item.Castle) is { } hero && IsOverIcon(mousePosition, offsetX + 82, offsetY + 19))
        {
            Requests.HeroDialog = hero;
            Requests.NeedFadeIn = true;
        }
    }

    /// <summary>A double click is a single one, <c>:528-560</c>.</summary>
    /// <param name="item">The row.</param>
    /// <param name="mousePosition">The cursor.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public override void ActionListDoubleClick(KingdomCastleRow item, Point mousePosition, int offsetX, int offsetY) =>
        ActionListSingleClick(item, mousePosition, offsetX, offsetY);

    /// <summary>
    /// Shows the quick info of the town, of the hero at home or of the captain, <c>:562-576</c>.
    /// </summary>
    /// <param name="item">The row.</param>
    /// <param name="mousePosition">The cursor.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public override void ActionListPressRight(KingdomCastleRow item, Point mousePosition, int offsetX, int offsetY)
    {
        if (IsOverIcon(mousePosition, offsetX + 17, offsetY + 19))
        {
            Requests.CastleInfo = item.Castle;

            return;
        }

        if (!IsOverIcon(mousePosition, offsetX + 82, offsetY + 19))
        {
            return;
        }

        if (_world.GetHero(item.Castle) is { } hero)
        {
            Requests.HeroInfo = hero;
        }
        else if (item.Castle.Captain.IsValid)
        {
            Requests.CaptainInfo = item.Castle.Captain;
        }
    }

    /// <summary>
    /// Handles the two army bars and the dwellings under the cursor, <c>:578-598</c>.
    /// </summary>
    /// <remarks>
    /// The reference lets the garrison exchange troops with the hero at home; the port keeps that pairing in
    /// <see cref="KingdomArmyRequest.Other"/>. The dwellings bar has no event handling in the port yet (recruiting and
    /// building are castle-screen batches), so a click on a dwelling does nothing here.
    /// </remarks>
    /// <param name="item">The row under the cursor.</param>
    /// <param name="cursor">The cursor.</param>
    /// <returns><see langword="true"/> when the row consumed the frame.</returns>
    public override bool ActionListCursor(KingdomCastleRow item, Point cursor)
    {
        if (Frame is null)
        {
            return false;
        }

        ArmyBar? heroBar = item.HeroArmyBar;

        if (item.GarrisonArmyBar.RenderingArea.Contains(cursor))
        {
            ArmyBarEvent armyEvent = item.GarrisonArmyBar.ProcessEvents(Frame);

            if (armyEvent.Handled)
            {
                Requests.Army.Add(new KingdomArmyRequest(item.GarrisonArmyBar, heroBar, armyEvent));

                return true;
            }
        }

        if (heroBar is null || !heroBar.RenderingArea.Contains(cursor))
        {
            return false;
        }

        ArmyBarEvent heroArmyEvent = heroBar.ProcessEvents(Frame);

        if (!heroArmyEvent.Handled)
        {
            return false;
        }

        Requests.Army.Add(new KingdomArmyRequest(heroBar, item.GarrisonArmyBar, heroArmyEvent));

        return true;
    }

    private void DrawCentred(string text, int centreX, int y, PixelBuffer output) =>
        GlyphText.DrawLine(_smallWhite, output, new Point(centreX - (GlyphText.Measure(_smallWhite, text) / 2), y), text);
}
