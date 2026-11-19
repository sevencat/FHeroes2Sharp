using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>The two buttons of the well window, i.e. frames of the two archives it draws.</summary>
public enum WellDialogButton
{
    /// <summary>The MAX button, <c>ICN::BUTTON_WELL_MAX</c>.</summary>
    Max,

    /// <summary>The EXIT button, <c>ICN::BUTTON_GUILDWELL_EXIT</c>.</summary>
    Exit,
}

/// <summary>What a right click on one of the two buttons says.</summary>
/// <param name="Title">The heading of the message box.</param>
/// <param name="Text">The body of the message box.</param>
public readonly record struct WellTooltip(string Title, string Text);

/// <summary>
/// What a frame of the well window asks the caller to do, the body of the <c>while</c> loop in
/// <c>Castle::_openWell()</c> (<c>castle_well.cpp:246-300</c>).
/// </summary>
public enum WellDialogAction
{
    /// <summary>Nothing happened: holds the zero value, so a default frame is a quiet frame.</summary>
    None,

    /// <summary>The EXIT button, or the close-window hot key.</summary>
    Exit,

    /// <summary>The MAX button, or its hot key: the caller plans and shows the purchase.</summary>
    Max,

    /// <summary>A dwelling was clicked, or its hot key: the caller opens the recruit dialog.</summary>
    Recruit,

    /// <summary>A right click on a dwelling: the caller shows <see cref="WellDialog.DescribeDwelling"/>.</summary>
    DwellingInfo,
}

/// <summary>
/// The hot keys the well window listens to besides the pointer, the three
/// <c>Game::HotKey…</c> lookups of the reference loop.
/// </summary>
/// <remarks>
/// The port has no hot key table yet, so the scene decodes the frame's key press into these three
/// answers and hands them over.
/// </remarks>
/// <param name="CloseWindow"><c>Game::HotKeyCloseWindow()</c>: Escape or Enter.</param>
/// <param name="BuyAll"><c>Game::HotKeyPressEvent( TOWN_WELL_BUY_ALL )</c>.</param>
/// <param name="Dwelling">The pressed dwelling hot key as a level, one to six, or zero.</param>
public readonly record struct WellHotKeys(bool CloseWindow = false, bool BuyAll = false, int Dwelling = 0);

/// <summary>
/// One frame of the well window: what it asks for, and what a right click on a button said.
/// </summary>
/// <param name="Action">What the caller should do next.</param>
/// <param name="DwellingLevel">The level of <see cref="WellDialogAction.Recruit"/> or
/// <see cref="WellDialogAction.DwellingInfo"/>, zero otherwise.</param>
/// <param name="Tooltip">The button a right click landed on, or <see langword="null"/>.</param>
public readonly record struct WellDialogFrame(
    WellDialogAction Action,
    int DwellingLevel = 0,
    WellDialogButton? Tooltip = null);

/// <summary>
/// The three faces of the text of the well window, the <c>FontType</c> combinations of
/// <c>castle_well.cpp:400-543</c>.
/// </summary>
/// <param name="SmallWhite">Statistics, names and the word "Available:".</param>
/// <param name="NormalWhite">The title of the window.</param>
/// <param name="NormalYellow">The population numbers.</param>
public sealed record WellFonts(IGlyphSource SmallWhite, IGlyphSource NormalWhite, IGlyphSource NormalYellow);

/// <summary>
/// The well window, <c>Castle::_openWell()</c>, <c>_wellRedrawBackground()</c> and
/// <c>_wellRedrawAvailableMonsters()</c> (<c>castle_well.cpp:203-548</c>).
/// </summary>
/// <remarks>
/// <para>
/// Six cells in two columns: the picture and the name of the dwelling, the creature, its statistics, its growth and
/// what is waiting in it. The window itself is static, and the reference keeps a copy of the composed background to
/// restore under the changing parts, so <see cref="Draw"/> composes the whole thing and
/// <see cref="DrawAvailable"/> repaints the one line that a recruitment changes.
/// </para>
/// <para>
/// The message loop is <see cref="ProcessEvents"/>: one call per frame with the frame's
/// <see cref="LocalEvent"/> answers what the caller should do next, in the order of the reference. The recruitment
/// itself stays outside — <see cref="CastleMaxRecruitment"/> and the dwelling recruit dialog are the caller's
/// business.
/// </para>
/// <para>
/// Not ported: the animated creature sprites between the cells (<c>_wellRedrawMonsterAnimation</c>, which needs the
/// battle animation archives). The reference also clips the title and shortens "Available:" to fit its column; with
/// the English strings both are already inside their boxes, and <see cref="GlyphText"/> has no clip, so the port draws
/// them as they are.
/// </para>
/// </remarks>
public sealed class WellDialog
{
    /// <summary>The heading of the window, drawn on the bottom bar.</summary>
    public const string Title = "Town Population Information and Statistics";

    /// <summary>The word in front of the population, drawn in the small font.</summary>
    public const string AvailableLabel = "Available:";

    private readonly CastleWellDwelling[] _dwellings;

    private WellDialog(CastleWellDwelling[] dwellings, MapRace race)
    {
        _dwellings = dwellings;
        Race = race;
    }

    /// <summary>The six rows of the window, first level first.</summary>
    public IReadOnlyList<CastleWellDwelling> Dwellings => _dwellings;

    /// <summary>The race of the town, which decides the dwelling pictures.</summary>
    public MapRace Race { get; }

    /// <summary>Takes the state of a town.</summary>
    /// <param name="castle">The town whose well is open.</param>
    /// <returns>The window model.</returns>
    public static WellDialog Create(Castle castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        return Create(castle.GetWellDwellings(), castle.Race);
    }

    /// <summary>Takes the six rows directly, which is how the tests build a window without a town.</summary>
    /// <param name="dwellings">Exactly six rows, first level first.</param>
    /// <param name="race">The race of the town.</param>
    /// <returns>The window model.</returns>
    /// <exception cref="ArgumentException">The count is not six.</exception>
    public static WellDialog Create(IReadOnlyList<CastleWellDwelling> dwellings, MapRace race)
    {
        ArgumentNullException.ThrowIfNull(dwellings);

        if (dwellings.Count != WellLayout.CellCount)
        {
            throw new ArgumentException($"A town has {WellLayout.CellCount} dwellings.", nameof(dwellings));
        }

        CastleWellDwelling[] rows = [.. dwellings];
        return new WellDialog(rows, race);
    }

    /// <summary>Places the window in the middle of <paramref name="screen"/>.</summary>
    /// <param name="screen">The screen the window is centred on.</param>
    /// <param name="artwork">The artwork, which decides the height of the bar and the button sizes.</param>
    /// <returns>The layout of every part of the window.</returns>
    public WellLayout CreateLayout(in Rect screen, WellArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        return new WellLayout(
            screen,
            new Size(artwork.Bar.Width, artwork.Bar.Height),
            new Size(artwork.Max.Released.Width, artwork.Max.Released.Height),
            new Size(artwork.Exit.Released.Width, artwork.Exit.Released.Height));
    }

    /// <summary>
    /// Composes the static part of the window: background, bar, title and every dwelling.
    /// </summary>
    /// <param name="target">The buffer to draw on, usually the display.</param>
    /// <param name="layout">Where the parts go.</param>
    /// <param name="artwork">The sprites.</param>
    /// <param name="fonts">The three text faces.</param>
    public void Draw(PixelBuffer target, WellLayout layout, WellArtwork artwork, WellFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(fonts);

        ImageOps.BlitSpriteClipped(
            target,
            layout.Dialog.Position,
            artwork.Background,
            new Rect(0, 0, artwork.Background.Width, WellLayout.BottomBarOffsetY),
            layout.Dialog);

        // The bar is drawn in two pieces so that the buttons keep their own corners of it.
        ImageOps.BlitSpriteClipped(
            target,
            layout.BarLeft.Position,
            artwork.Bar,
            new Rect(WellLayout.BarLeftSourceX, 0, layout.BarLeft.Width, layout.BarHeight),
            layout.BottomBar);

        ImageOps.BlitSpriteClipped(
            target,
            layout.BarRight.Position,
            artwork.Bar,
            new Rect(layout.BarRightSourceX, 0, layout.BarRight.Width, layout.BarHeight),
            layout.BottomBar);

        // The original background has a transparent column right of EXIT that nothing else covers.
        ImageOps.VLine(target, layout.FillColumn.X, layout.FillColumn.Y, layout.FillColumn.Height, 0);

        DrawCentred(
            fonts.NormalWhite,
            target,
            new Point(layout.Dialog.X + WellLayout.TitleCentreX, layout.Dialog.Y + WellLayout.BottomBarOffsetY + WellLayout.TitleOffsetY),
            Title);

        foreach (CastleWellDwelling dwelling in _dwellings)
        {
            DrawDwelling(target, layout, artwork, fonts, dwelling);
        }
    }

    /// <summary>
    /// Repaints what a recruitment changes, i.e. the "Available:" line of one dwelling
    /// (<c>_wellRedrawAvailableMonsters</c>, <c>castle_well.cpp:308-375</c>).
    /// </summary>
    /// <remarks>
    /// The reference copies the stored background under the line first; the port leaves that to the caller, which
    /// has the composed image anyway.
    /// </remarks>
    /// <param name="target">The buffer to draw on.</param>
    /// <param name="layout">Where the parts go.</param>
    /// <param name="fonts">The three text faces.</param>
    /// <param name="dwelling">The row to repaint; an unbuilt dwelling draws nothing.</param>
    public static void DrawAvailable(PixelBuffer target, WellLayout layout, WellFonts fonts, CastleWellDwelling dwelling)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fonts);

        if (!dwelling.IsBuilt)
        {
            return;
        }

        Point origin = layout.GetAvailableOrigin(dwelling.Level);

        DrawCentred(
            fonts.NormalYellow,
            target,
            new Point(origin.X + WellLayout.AvailablePopulationCentreX, origin.Y),
            DescribePopulation(dwelling.Available));

        DrawCentred(
            fonts.SmallWhite,
            target,
            new Point(origin.X + WellLayout.AvailableLabelCentreX, origin.Y + WellLayout.AvailableLabelOffsetY),
            AvailableLabel);
    }

    /// <summary>
    /// The statistics lines of a dwelling, in drawing order (<c>castle_well.cpp:461-543</c>).
    /// </summary>
    /// <remarks>
    /// A skipped line is an empty entry, so every entry moves the cursor down by one line height and the caller does
    /// not have to know where the gaps are. The growth lines are only there for a dwelling that stands, and
    /// "Speed:" really has no space after the colon in the reference.
    /// </remarks>
    /// <param name="dwelling">The row to describe.</param>
    /// <returns>Attack, defense, damage, hit points, speed, then growth when built.</returns>
    public static IReadOnlyList<string> DescribeStats(CastleWellDwelling dwelling)
    {
        MonsterBattleStats stats = dwelling.Stats;

        List<string> lines =
        [
            $"Attack: {Describe(stats.Attack)}",
            $"Defense: {Describe(stats.Defense)}",
            DescribeDamage(stats),
            $"HP: {Describe(stats.HitPoints)}",
            string.Empty,
            "Speed:",
            Speed.GetName((uint)stats.Speed),
            string.Empty,
        ];

        if (dwelling.IsBuilt)
        {
            lines.Add("Growth");
            lines.Add($"+ {Describe(dwelling.WeeklyGrowth)} / week");
        }

        return lines;
    }

    /// <summary>The name of the dwelling as built, <c>GetStringBuilding()</c>.</summary>
    /// <param name="dwelling">The row to name.</param>
    /// <returns>The name of the upgraded dwelling when one stands.</returns>
    public string DescribeDwelling(CastleWellDwelling dwelling) =>
        CastleBuildingInfo.GetName(Race, dwelling.ActualDwelling);

    /// <summary>The buttons of the window, in the reference's order: MAX, then EXIT.</summary>
    /// <param name="layout">Where the buttons go.</param>
    /// <param name="artwork">The button sprites.</param>
    /// <returns>The two buttons.</returns>
    public static IReadOnlyList<ButtonSprite> CreateButtons(WellLayout layout, WellArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);

        return
        [
            new ButtonSprite(
                artwork.Max.Released,
                artwork.Max.Pressed,
                offsetX: layout.Max.X,
                offsetY: layout.Max.Y),
            new ButtonSprite(
                artwork.Exit.Released,
                artwork.Exit.Pressed,
                offsetX: layout.Exit.X,
                offsetY: layout.Exit.Y),
        ];
    }

    /// <summary>The button behind an index of <see cref="CreateButtons"/>.</summary>
    /// <param name="index">Zero based button index.</param>
    /// <returns>MAX for the first button, EXIT for everything else.</returns>
    public static WellDialogButton FromButton(int index) => index == 0 ? WellDialogButton.Max : WellDialogButton.Exit;

    /// <summary>What a right click on a button says, <c>castle_well.cpp:298-304</c>.</summary>
    /// <param name="button">The button under the cursor.</param>
    /// <returns>The heading and the body of the message.</returns>
    public static WellTooltip GetTooltip(WellDialogButton button) => button switch
    {
        WellDialogButton.Max => new WellTooltip("Max", "Hire all creatures in the town."),
        _ => new WellTooltip("Exit", "Exit this menu."),
    };

    /// <summary>
    /// The dwelling whose creature was clicked, <c>castle_well.cpp:268-286</c>.
    /// </summary>
    /// <remarks>
    /// Only a built dwelling answers: the reference tests the click area and the building bit together, so an empty
    /// cell is not a target at all.
    /// </remarks>
    /// <param name="point">The point in window coordinates.</param>
    /// <param name="layout">Where the cells are.</param>
    /// <returns>The dwelling level, one to six, or zero when the point hits nothing.</returns>
    public int GetDwellingAt(in Point point, WellLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        foreach (CastleWellDwelling dwelling in _dwellings)
        {
            if (dwelling.IsBuilt && layout.GetMonsterArea(dwelling.Level).Contains(point))
            {
                return dwelling.Level;
            }
        }

        return 0;
    }

    /// <summary>
    /// One frame of the reference's message loop, <c>castle_well.cpp:246-300</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order of the reference is kept, because the branches consume the click: the two buttons first, then the six
    /// dwellings from the first level to the sixth, then the two right-click tooltips. A click belongs to exactly one
    /// of them — <see cref="LocalEvent.MouseClickLeft(Rect)"/> eats the release — so a dwelling click that follows a
    /// button click can never fire in the same frame.
    /// </para>
    /// <para>
    /// The reference walks the dwellings with a <c>break</c> after the first hit, so a frame answers at most one
    /// dwelling even if the cells overlapped. Only a built dwelling is a target, like
    /// <see cref="GetDwellingAt"/>.
    /// </para>
    /// <para>
    /// <b>Not here</b>: the purchase. The MAX branch of the reference calls <c>_recruitCastleMax</c> and its own
    /// confirmation dialog; the port answers <see cref="WellDialogAction.Max"/> and lets the caller plan with
    /// <see cref="CastleMaxRecruitment"/>. A <see cref="WellDialogAction.Recruit"/> is answered without opening the
    /// recruit dialog either.
    /// </para>
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="layout">Where the buttons and the cells are.</param>
    /// <param name="buttons">The two buttons of <see cref="CreateButtons"/>, MAX first.</param>
    /// <param name="hotKeys">The hot keys pressed this frame.</param>
    /// <returns>What the caller should do, plus the tooltip of the frame.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/>, <paramref name="layout"/> or
    /// <paramref name="buttons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="buttons"/> does not hold exactly two buttons.</exception>
    public WellDialogFrame ProcessEvents(
        LocalEvent events,
        WellLayout layout,
        IReadOnlyList<ButtonSprite> buttons,
        WellHotKeys hotKeys = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(buttons);

        if (buttons.Count != 2)
        {
            throw new ArgumentException("The well window has two buttons: MAX and EXIT.", nameof(buttons));
        }

        // drawOnState( isMouseLeftButtonPressedAndHeldInArea( area() ) ) of both buttons, castle_well.cpp:247-250.
        buttons[0].UpdatePressState(events);
        buttons[1].UpdatePressState(events);

        if (events.MouseClickLeft(buttons[1].Area) || hotKeys.CloseWindow)
        {
            return new WellDialogFrame(WellDialogAction.Exit);
        }

        if (events.MouseClickLeft(buttons[0].Area) || hotKeys.BuyAll)
        {
            return new WellDialogFrame(WellDialogAction.Max);
        }

        foreach (CastleWellDwelling dwelling in _dwellings)
        {
            if (!dwelling.IsBuilt)
            {
                continue;
            }

            Rect area = layout.GetMonsterArea(dwelling.Level);

            if (events.MouseClickLeft(area) || hotKeys.Dwelling == dwelling.Level)
            {
                return new WellDialogFrame(WellDialogAction.Recruit, dwelling.Level);
            }

            if (events.IsMouseRightButtonPressedInArea(area))
            {
                return new WellDialogFrame(WellDialogAction.DwellingInfo, dwelling.Level);
            }
        }

        // The two right-click tooltips come last in the reference and are not an else-if chain, but the
        // two areas are disjoint, so the first hit is the only hit.
        if (events.IsMouseRightButtonPressedInArea(buttons[1].Area))
        {
            return new WellDialogFrame(WellDialogAction.None, 0, WellDialogButton.Exit);
        }

        if (events.IsMouseRightButtonPressedInArea(buttons[0].Area))
        {
            return new WellDialogFrame(WellDialogAction.None, 0, WellDialogButton.Max);
        }

        return default;
    }

    /// <summary>The population of a dwelling as the window prints it.</summary>
    /// <param name="population">The number of creatures waiting.</param>
    /// <returns>The number in the invariant culture.</returns>
    public static string DescribePopulation(uint population) => population.ToString(CultureInfo.InvariantCulture);

    private static string Describe(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Describe(uint value) => value.ToString(CultureInfo.InvariantCulture);

    private static string DescribeDamage(MonsterBattleStats stats) =>
        stats.DamageMin == stats.DamageMax
            ? $"Damage: {Describe(stats.DamageMin)}"
            : $"Damage: {Describe(stats.DamageMin)}-{Describe(stats.DamageMax)}";

    private static void DrawCentred(IGlyphSource font, PixelBuffer target, Point anchor, string text)
    {
        Point position = WellLayout.Centre(anchor, GlyphText.Measure(font, text));
        GlyphText.DrawLine(font, target, position, text);
    }

    private void DrawDwelling(
        PixelBuffer target,
        WellLayout layout,
        WellArtwork artwork,
        WellFonts fonts,
        CastleWellDwelling dwelling)
    {
        Sprite picture = artwork.GetDwellingSprite(dwelling.Level, dwelling.ActualDwelling);
        Point pictureOrigin = layout.GetPictureOrigin(dwelling.Level);

        // A plain copy, as in the reference: the dwelling pictures carry no transparency.
        ImageOps.Blit(target, pictureOrigin, picture.Image, picture.Image.Bounds);

        DrawCentred(fonts.SmallWhite, target, layout.GetDwellingNameAnchor(dwelling.Level), DescribeDwelling(dwelling));
        DrawCentred(fonts.SmallWhite, target, layout.GetMonsterNameAnchor(dwelling.Level), dwelling.MonsterName);

        Point cursor = layout.GetStatsAnchor(dwelling.Level);
        foreach (string line in DescribeStats(dwelling))
        {
            if (line.Length != 0)
            {
                DrawCentred(fonts.SmallWhite, target, cursor, line);
            }

            cursor = new Point(cursor.X, cursor.Y + fonts.SmallWhite.LineHeight);
        }

        DrawAvailable(target, layout, fonts, dwelling);
    }
}
