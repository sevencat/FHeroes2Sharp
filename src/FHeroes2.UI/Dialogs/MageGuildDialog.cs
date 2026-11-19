using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What a press in the mage guild window means, <c>Castle::MageGuildDialogResult</c>
/// (<c>castle_mageguild.cpp:180-200</c>).
/// </summary>
public enum MageGuildDialogAction
{
    /// <summary>The exit button, or the close-window hot key.</summary>
    Exit,

    /// <summary>The arrow that walks to the previous town.</summary>
    PreviousTown,

    /// <summary>The arrow that walks to the next town.</summary>
    NextTown,
}

/// <summary>
/// The window that shows a town's mage guild, <c>Castle::_openMageGuild()</c>
/// (<c>castle_mageguild.cpp:56-212</c>).
/// </summary>
/// <remarks>
/// <para>
/// Five rows of spells, one per level, the highest on top; the guild's own picture on the left; the previous and next
/// town buttons at the bottom with a status line between them. The rows come from
/// <see cref="CastleTown.GetMageGuildSpellRows"/>, the geometry from <see cref="MageGuildLayout"/> and the sprites from
/// <see cref="MageGuildArtwork"/>.
/// </para>
/// <para>
/// The two town buttons are disabled when the kingdom owns a single town, which the reference decides by
/// <c>GetKingdom().GetCastles().size() &lt; 2</c> — the caller passes that count in, because the window has no kingdom
/// of its own.
/// </para>
/// <para>
/// <b>Not here</b>: the event loop. As with the other windows of the port, <see cref="Create"/>, <see cref="Draw"/>
/// and <see cref="CreateButtons"/> are called once per frame by the caller, whose loop turns a press into a
/// <see cref="MageGuildDialogAction"/> with <see cref="FromButton"/> and feeds the pointer to
/// <see cref="GetStatusText"/>.
/// </para>
/// </remarks>
public sealed class MageGuildDialog
{
    /// <summary>Screen x of the rows, the <c>dialogOffset.x + 250</c> of the reference.</summary>
    public const int RowsLeftOffset = 250;

    /// <summary>
    /// Screen y of the first level's row, the <c>dialogOffset.y + 365</c> of the reference. The rows go <b>up</b>
    /// from here, so the fifth level sits at <see cref="RowsTopOffset"/>.
    /// </summary>
    public const int FirstRowTopOffset = 365;

    /// <summary>Vertical distance between two rows, <c>90</c> in the reference.</summary>
    public const int RowStepY = 90;

    /// <summary>Screen y of the fifth level's row, <c>365 - 90 * 4</c>.</summary>
    public const int RowsTopOffset = FirstRowTopOffset - (RowStepY * 4);

    /// <summary>Screen y of the bottom button bar, <c>bottomBarOffsetY</c> of the reference.</summary>
    public const int BottomBarOffsetY = 461;

    /// <summary>How many spell levels the window has, <c>spellRows.size()</c>.</summary>
    public const int SpellLevels = 5;

    private MageGuildDialog(
        IReadOnlyList<IReadOnlyList<SpellType>> rows,
        bool heroHasSpellBook,
        bool canSwitchTowns,
        MapRace race,
        int guildLevel)
    {
        Rows = rows;
        HeroHasSpellBook = heroHasSpellBook;
        CanSwitchTowns = canSwitchTowns;
        Race = race;
        GuildLevel = guildLevel;
    }

    /// <summary>Builds the window for a town, <c>Castle::_openMageGuild( hero )</c>.</summary>
    /// <param name="castle">The town whose guild is shown.</param>
    /// <param name="heroHasSpellBook">Whether the visiting hero already owns a spell book.</param>
    /// <param name="castleCount">How many towns the kingdom owns, which decides whether the town buttons work.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> is <see langword="null"/>.</exception>
    public static MageGuildDialog Create(CastleTown castle, bool heroHasSpellBook, int castleCount)
    {
        ArgumentNullException.ThrowIfNull(castle);

        return Create(
            castle.GetMageGuildSpellRows(),
            heroHasSpellBook,
            castleCount >= 2,
            castle.Race,
            castle.GetLevelMageGuild());
    }

    /// <summary>
    /// Builds the window from its parts, without a town — the shape the caller has when it walks from town to town
    /// with the two arrows and only reloads the rows.
    /// </summary>
    /// <param name="rows">The spells of each level, slot <see cref="SpellType.None"/> for an empty slot.</param>
    /// <param name="heroHasSpellBook">Whether the visiting hero already owns a spell book.</param>
    /// <param name="canSwitchTowns">Whether the two arrows work.</param>
    /// <param name="race">The town's race, which picks the guild's picture.</param>
    /// <param name="guildLevel">The level of the guild that was built.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is <see langword="null"/>.</exception>
    public static MageGuildDialog Create(
        IReadOnlyList<IReadOnlyList<SpellType>> rows,
        bool heroHasSpellBook,
        bool canSwitchTowns,
        MapRace race,
        int guildLevel)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return new MageGuildDialog(rows, heroHasSpellBook, canSwitchTowns, race, guildLevel);
    }

    /// <summary>The spells of each level, row <c>0</c> being the first level and the bottom row.</summary>
    public IReadOnlyList<IReadOnlyList<SpellType>> Rows { get; }

    /// <summary>Whether the visiting hero already owns a spell book.</summary>
    public bool HeroHasSpellBook { get; }

    /// <summary>Whether the previous and next town buttons are enabled.</summary>
    public bool CanSwitchTowns { get; }

    /// <summary>The town's race, which picks the guild's picture.</summary>
    public MapRace Race { get; }

    /// <summary>The level of the guild that was built, <c>GetLevelMageGuild()</c>.</summary>
    public int GuildLevel { get; }

    /// <summary>
    /// The frame of the guild picture, <c>std::max( guildLevel - 1, 0 )</c>: frame <c>0</c> is the finished first
    /// level, and a town without a guild (which never opens this window) falls back to it.
    /// </summary>
    public int BuildingFrameIndex => Math.Max(GuildLevel - 1, 0);

    /// <summary>
    /// What the status bar says until the pointer reaches a button, the <c>defaultStatusBarText</c> of the reference.
    /// </summary>
    /// <remarks>
    /// A hero without a book is told the spells are on offer; one with a book has already learned them, since the
    /// window fills the book on the way in.
    /// </remarks>
    public string DefaultStatusText => HeroHasSpellBook
        ? "The spells the hero can learn have been added to their book."
        : "The above spells are available here.";

    /// <summary>
    /// Screen y of the row of a spell level, the <c>365 - 90 * levelIndex</c> of the reference.
    /// </summary>
    /// <param name="levelIndex">Zero based level, i.e. an index into <see cref="Rows"/>.</param>
    /// <returns>The offset from the top of the dialog, going up with the level.</returns>
    public static int GetRowTopOffset(int levelIndex) => FirstRowTopOffset - (RowStepY * levelIndex);

    /// <summary>
    /// Lays the window out, <c>castle_mageguild.cpp:63-101</c>.
    /// </summary>
    /// <param name="screen">The whole display.</param>
    /// <param name="artwork">The sprites, whose sizes drive the bottom strip.</param>
    /// <returns>The layout.</returns>
    public MageGuildLayout CreateLayout(in Rect screen, MageGuildArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        return MageGuildLayout.Create(
            screen,
            artwork.BarSize,
            new Size(artwork.Exit.Released.Width, artwork.Exit.Released.Height),
            new Size(artwork.PreviousTown.Released.Width, artwork.PreviousTown.Released.Height),
            new Size(artwork.NextTown.Released.Width, artwork.NextTown.Released.Height),
            CanSwitchTowns);
    }

    /// <summary>
    /// Builds the five scroll rows and positions them, <c>castle_mageguild.cpp:119-132</c>.
    /// </summary>
    /// <param name="layout">Where the rows go.</param>
    /// <param name="artwork">The scroll sprites, whose sizes shift a row's slots apart.</param>
    /// <returns>The rows, level 0 first.</returns>
    public IReadOnlyList<SpellScrollRow> CreateRows(MageGuildLayout layout, MageGuildArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);

        Size opened = new(artwork.OpenedScroll.Width, artwork.OpenedScroll.Height);
        Size folded = new(artwork.FoldedScroll.Width, artwork.FoldedScroll.Height);

        List<SpellScrollRow> rows = new(SpellLevels);
        for (int levelIndex = 0; levelIndex < SpellLevels; ++levelIndex)
        {
            SpellScrollRow row = new(
                levelIndex < Rows.Count ? Rows[levelIndex] : Array.Empty<SpellType>());

            row.SetPosition(layout.GetRowAnchor(levelIndex), opened, folded);
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Paints the window: plate, guild picture, the five rows and the bottom bar
    /// (<c>castle_mageguild.cpp:72</c>, <c>114</c>, <c>131</c> and <c>95-97</c>).
    /// </summary>
    /// <remarks>
    /// The three buttons are not painted here, for the same reason as everywhere else in the port: they repaint
    /// themselves on hover, so <see cref="CreateButtons"/> hands them to the caller's loop.
    /// </remarks>
    /// <param name="target">The display buffer.</param>
    /// <param name="layout">Where everything goes.</param>
    /// <param name="artwork">The sprites.</param>
    /// <param name="font">The small white font of the spell names.</param>
    /// <param name="guildBuilding">The guild picture, or <see langword="null"/> for a window without one.</param>
    /// <param name="rows">The rows from <see cref="CreateRows"/>.</param>
    public void Draw(
        PixelBuffer target,
        MageGuildLayout layout,
        MageGuildArtwork artwork,
        IGlyphSource font,
        Sprite? guildBuilding,
        IReadOnlyList<SpellScrollRow> rows)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(rows);

        ImageOps.BlitSprite(target, layout.Dialog.Position, artwork.Background);

        if (guildBuilding is { } building)
        {
            DrawGuildBuilding(target, layout, building, Race);
        }

        foreach (SpellScrollRow row in rows)
        {
            row.Draw(target, artwork.OpenedScroll, artwork.FoldedScroll, font, artwork.GetSpellIcon);
        }

        DrawBottomBar(target, layout, artwork);
    }

    /// <summary>
    /// Paints the guild's picture above the stone plate, <c>renderMageGuildBuilding</c>
    /// (<c>castle_mageguild.cpp:28-54</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The picture is centred on the left column's midpoint — <c>offset.x + 100 - area.x - area.width / 2</c> — using
    /// the <b>drawn</b> part of the sprite rather than its full grid, so that the transparent margin of a large frame
    /// does not push the building off centre. That is <see cref="ImageOps.GetActiveRoi"/>'s job. Vertically the whole
    /// sprite is stood on <c>offset.y + 290</c>: its bottom edge, not the bottom of the drawn box, touches that row.
    /// </para>
    /// <para>
    /// A random race gets the wizard's picture recoloured with the purple table, which is how the reference makes a
    /// "generic" guild; the sprite is copied first, so the caller's frame is not repainted in place.
    /// </para>
    /// <para>
    /// The whole sprite is blitted, clipped to the left 200 columns of the window: the picture of a high level guild
    /// is taller than the plate and would otherwise cover the spell rows. The clip is why the drawing goes through a
    /// source rectangle rather than a plain blit.
    /// </para>
    /// </remarks>
    /// <param name="target">The display buffer.</param>
    /// <param name="layout">The window, which supplies the clip column.</param>
    /// <param name="building">The frame of <see cref="MageGuildArtwork.GetGuildBuildingIcn"/>.</param>
    /// <param name="race">The town's race.</param>
    public static void DrawGuildBuilding(
        PixelBuffer target,
        MageGuildLayout layout,
        Sprite building,
        MapRace race)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(building);

        Sprite picture = building;
        if (race == MapRace.Random)
        {
            PixelBuffer recoloured = building.Image.Clone();
            ImageOps.ApplyPalette(recoloured, PaletteTable.Get(PaletteId.Purple));
            picture = new Sprite(recoloured, building.OffsetX, building.OffsetY, building.Transform);
        }

        // The drawn box decides the horizontal centring only; the sprite's own bottom edge sits on the baseline.
        Rect inked = ImageOps.GetActiveRoi(picture);

        Point destination = new(
            layout.Dialog.X + (MageGuildLayout.GuildColumnWidth / 2) - inked.X - (inked.Width / 2),
            layout.Dialog.Y + MageGuildLayout.GuildBaselineOffsetY - picture.Height);

        Rect clip = new(
            layout.Dialog.X,
            layout.Dialog.Y,
            MageGuildLayout.GuildColumnWidth,
            MageGuildLayout.DialogHeight);

        ImageOps.BlitSpriteClipped(target, destination, picture, picture.Image.Bounds, clip);
    }

    /// <summary>
    /// Paints the status bar's plate, <c>castle_mageguild.cpp:94-97</c>: two copies of <c>SMALLBAR</c> frame 0, the
    /// left one starting at source column 1 because column 0 is black.
    /// </summary>
    /// <param name="target">The display buffer.</param>
    /// <param name="layout">Where the bar goes.</param>
    /// <param name="artwork">The plate and the arrows.</param>
    public static void DrawBottomBar(PixelBuffer target, MageGuildLayout layout, MageGuildArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);

        Sprite bar = artwork.Bar;

        ImageOps.BlitSpriteClipped(
            target,
            layout.Bar.Position,
            bar,
            new Rect(1, 0, layout.BarLeftWidth, bar.Height),
            layout.Bar);

        ImageOps.BlitSpriteClipped(
            target,
            new Point(layout.Bar.X + layout.BarLeftWidth, layout.Bar.Y),
            bar,
            new Rect(layout.BarSourceRightX, 0, layout.BarRightWidth, bar.Height),
            layout.Bar);
    }

    /// <summary>
    /// The three buttons of the window in <see cref="FromButton"/> order: previous town, next town, exit
    /// (<c>castle_mageguild.cpp:75-86</c>).
    /// </summary>
    /// <remarks>
    /// The arrows are still returned when the kingdom has one town — the reference draws them and only blocks their
    /// input — and they are given the released look as their disabled look, which is what <c>Button::disable()</c>
    /// does when no greyscale image was set. They are disabled here, not merely flagged, so that a press is refused
    /// the same way as in the reference.
    /// </remarks>
    /// <param name="layout">Where the buttons go.</param>
    /// <param name="artwork">The button sprites.</param>
    /// <returns>The buttons, previous town first.</returns>
    public IReadOnlyList<ButtonSprite> CreateButtons(MageGuildLayout layout, MageGuildArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);

        Sprite? disabledPrevious = CanSwitchTowns ? null : artwork.PreviousTown.Released;
        Sprite? disabledNext = CanSwitchTowns ? null : artwork.NextTown.Released;

        ButtonSprite previous = new(
            artwork.PreviousTown.Released,
            artwork.PreviousTown.Pressed,
            disabledPrevious,
            layout.PreviousTown.X,
            layout.PreviousTown.Y);

        ButtonSprite next = new(
            artwork.NextTown.Released,
            artwork.NextTown.Pressed,
            disabledNext,
            layout.NextTown.X,
            layout.NextTown.Y);

        if (!CanSwitchTowns)
        {
            previous.Disable();
            next.Disable();
        }

        return
        [
            previous,
            next,
            new ButtonSprite(
                artwork.Exit.Released,
                artwork.Exit.Pressed,
                offsetX: layout.Exit.X,
                offsetY: layout.Exit.Y),
        ];
    }

    /// <summary>
    /// What the status bar says for a pointer position, the <c>updateStatusBar</c> lambda of the reference
    /// (<c>castle_mageguild.cpp:146-166</c>).
    /// </summary>
    /// <remarks>
    /// The order matters: the exit button wins over the arrows, and a disabled arrow says nothing at all, so the
    /// default text shows through.
    /// </remarks>
    /// <param name="cursor">Where the pointer is.</param>
    /// <param name="layout">Where the buttons are.</param>
    /// <returns>The line to print in the bar.</returns>
    public string GetStatusText(in Point cursor, MageGuildLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Exit.Contains(cursor))
        {
            return "Exit Mage Guild";
        }

        if (CanSwitchTowns && layout.PreviousTown.Contains(cursor))
        {
            return "Show previous town";
        }

        if (CanSwitchTowns && layout.NextTown.Contains(cursor))
        {
            return "Show next town";
        }

        return DefaultStatusText;
    }

    /// <summary>
    /// The meaning of a press, <c>return MageGuildDialogResult::...</c> of the reference.
    /// </summary>
    /// <param name="index">Index of the pressed button, i.e. into <see cref="CreateButtons"/>.</param>
    /// <returns>What the caller should do next.</returns>
    public static MageGuildDialogAction FromButton(int index) => index switch
    {
        0 => MageGuildDialogAction.PreviousTown,
        1 => MageGuildDialogAction.NextTown,
        _ => MageGuildDialogAction.Exit,
    };
}
