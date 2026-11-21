using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using ArmyText = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Interface;

/// <summary>
/// Two state paintings of the stone panel under the buttons of the adventure screen, a by-product of
/// <c>Interface::StatusPanel</c> (<c>src/fheroes2/gui/interface_status.cpp</c>, <c>interface_status.h</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Transitional.</b> The complete port of the reference lives in <see cref="StatusPanel"/>; this painter is what
/// the adventure scene draws with until <c>Interface::AdventureMap</c> is ported. It is deleted together with the
/// scene's own panels (see the self-built deletion list in <c>docs/planv4.md</c>).
/// </para>
/// <para>
/// <b>Two of the five states are here.</b> The reference cycles between <c>STATUS_DAY</c>, <c>STATUS_FUNDS</c>,
/// <c>STATUS_ARMY</c>, a transient «you found some resource» state and an empty state, on a timer. The army state
/// needs <c>ArmyTroop</c>/<c>Hero</c> drawing that lives in the game layer, and the found-resource state needs the
/// <c>RESOURCE.ICN</c> index table; both are left for the castle batch, so this class exposes the two setters the
/// screen needs now: <see cref="DrawDate"/> and <see cref="DrawKingdom"/>.
/// </para>
/// <para>
/// <b>The background is a stretched frame, not a tiled one.</b> <c>STONBACK.ICN</c> is 144x72; a taller panel takes
/// its top 11 pixels, repeats its middle 46 pixel band as often as fits and then glues its bottom
/// <c>height - 11</c> pixels to the end (<c>interface_status.cpp:405-436</c>). That is why a 88 pixel panel looks
/// like the top of a 72 pixel one with a slice of the bottom under it.
/// </para>
/// <para>
/// <b>The day picture stands for the phase of the moon.</b> Days 2 and later show frame 0; the first day of the week
/// shows frame <c>( week - 1 ) % 4 + 1</c>, except the very first day of the game, which is frame 0 again
/// (<c>interface_status.cpp:263-291</c>).
/// </para>
/// </remarks>
public sealed class StatusPanelPainter
{
    /// <summary>Width of the panel, <c>radarWidthPx</c>/<c>statusPanelWidthPx</c> of the reference.</summary>
    public const int Width = 144;

    /// <summary>Height of one full state, the height of <c>STONBACK.ICN</c>.</summary>
    public const int SegmentHeight = 72;

    /// <summary>Height of the top band of the background artwork.</summary>
    public const int BackgroundHeadHeight = 11;

    /// <summary>Height of the middle band that repeats on a taller panel.</summary>
    public const int BackgroundChunkHeight = 46;

    /// <summary>Distance between the two rows of resource icons, 27 pixels.</summary>
    public const int KingdomRowStep = 27;

    /// <summary>Distance between the two resource rows, 30 pixels.</summary>
    public const int ResourceRowStep = 30;

    /// <summary>File of the stone frame, <c>ICN::STONBACK</c>.</summary>
    public const string BackgroundAssetName = "STONBACK.ICN";

    /// <summary>File of the moon phases, <c>ICN::SUNMOON</c>.</summary>
    public const string MoonAssetName = "SUNMOON.ICN";

    /// <summary>File of the small icon strip, <c>ICN::RESSMALL</c>.</summary>
    public const string SmallResourcesAssetName = "RESSMALL.ICN";

    /// <summary>
    /// How much taller than three states the panel has to be before the reference puts the army or the
    /// found-resource strip under the kingdom, <c>stonHeight * 3 + 15</c>.
    /// </summary>
    private const int ThreeStateSlack = 15;

    private readonly Sprite _background;
    private readonly IReadOnlyList<Sprite> _sunMoon;
    private readonly Sprite _smallResources;

    /// <summary>
    /// Where the panel goes on the adventure screen: under the buttons, down to the bottom border,
    /// the <c>_statusPanel.SetPosition( buttonsArea.x, buttonsArea.y + buttonsArea.height )</c> and the
    /// height <c>display.height - y - border</c> of <c>game_interface.cpp</c>.
    /// </summary>
    /// <param name="panelBay">The bay the buttons and the panel share, <c>MapFrame.Panel</c>.</param>
    /// <param name="buttonsArea">Where the button panel is drawn.</param>
    /// <param name="frameHeight">Height of the display, 480.</param>
    /// <param name="borderWidth">Width of the frame border, 16.</param>
    /// <remarks>
    /// With the reference layout (buttons ending at 392) this is a 144x72 area, exactly one
    /// <c>STONBACK</c> frame, so the panel shows one state at a time.
    /// </remarks>
    public static Rect AreaFor(Rect panelBay, Rect buttonsArea, int frameHeight, int borderWidth)
    {
        int top = buttonsArea.Bottom;
        int height = frameHeight - top - borderWidth;

        // A panel squeezed by a tiny window keeps the width of the bay so the artwork still fits.
        return new Rect(panelBay.X, top, Width, Math.Max(height, 0));
    }

    /// <summary>
    /// Reads the three artworks the panel needs, <c>Assets::getImage( ICN::STONBACK / SUNMOON / RESSMALL )</c>.
    /// </summary>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <returns>The panel, or <see langword="null"/> when the archive misses one of the three files.</returns>
    public static StatusPanelPainter? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        if (assets.TryReadIcn(BackgroundAssetName) is not { Sprites.Count: > 0 } stone
            || assets.TryReadIcn(MoonAssetName) is not { Sprites.Count: >= 5 } moon
            || assets.TryReadIcn(SmallResourcesAssetName) is not { Sprites.Count: > 0 } resources)
        {
            return null;
        }

        return new StatusPanelPainter(stone.Sprites[0], moon.Sprites.Take(5).ToArray(), resources.Sprites[0]);
    }

    /// <summary>Creates the panel from the three artworks it needs.</summary>
    /// <param name="background">Frame 0 of <c>STONBACK.ICN</c>, 144x72.</param>
    /// <param name="sunMoon">Frames 0 to 4 of <c>SUNMOON.ICN</c>, the moon phases.</param>
    /// <param name="smallResources">
    /// Frame 0 of <c>RESSMALL.ICN</c>, the strip with the castle, town and six resource icons.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sunMoon"/> has fewer than five frames.</exception>
    public StatusPanelPainter(Sprite background, IReadOnlyList<Sprite> sunMoon, Sprite smallResources)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(sunMoon);
        ArgumentNullException.ThrowIfNull(smallResources);

        if (sunMoon.Count < 5)
        {
            throw new ArgumentException("The moon has five phases, frames 0 to 4.", nameof(sunMoon));
        }

        _background = background;
        _sunMoon = sunMoon;
        _smallResources = smallResources;
    }

    /// <summary>
    /// Paints the state the panel is in, a cut-down <c>StatusPanel::_redraw()</c>.
    /// </summary>
    /// <param name="target">Frame buffer of the display.</param>
    /// <param name="area">Where the panel is, see <see cref="AreaFor"/>.</param>
    /// <param name="state">Which state to show.</param>
    /// <param name="date">Month, week and day of the calendar.</param>
    /// <param name="kingdom">Castle count, town count and the seven resources.</param>
    /// <param name="smallFont">Font of «Month: 1 Week: 1».</param>
    /// <param name="normalFont">Font of «Day: 1».</param>
    /// <exception cref="ArgumentNullException">Any reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is shorter than 26 pixels.</exception>
    /// <remarks>
    /// The reference decides by height: three states plus 15 pixels put the date, the kingdom and the army
    /// on top of each other; two states plus 15 put the date and one state; anything shorter centres the one
    /// state it has. The army and the found-resource strips need the treasure and <c>RESOURCE.ICN</c> tables
    /// and are added with the castle batch, so a taller panel simply leaves their half of the stone empty.
    /// </remarks>
    public void Draw(
        PixelBuffer target,
        Rect area,
        StatusPanelState state,
        StatusPanelDate date,
        StatusPanelKingdom kingdom,
        IGlyphSource smallFont,
        IGlyphSource normalFont)
    {
        DrawBackground(target, area.Position, area.Height);

        // Nothing but the stone before the first day has passed, and nothing at all while hidden.
        if (state == StatusPanelState.Unknown)
        {
            return;
        }

        if (area.Height >= (SegmentHeight * 3) + ThreeStateSlack)
        {
            DrawDate(target, area.Position, date, smallFont, normalFont);
            DrawKingdom(target, area.Position, kingdom, smallFont, SegmentHeight + 5);

            return;
        }

        if (area.Height >= (SegmentHeight * 2) + ThreeStateSlack)
        {
            DrawDate(target, area.Position, date, smallFont, normalFont);

            if (state == StatusPanelState.Funds)
            {
                DrawKingdom(target, area.Position, kingdom, smallFont, SegmentHeight + 5);
            }

            return;
        }

        if (state == StatusPanelState.Funds)
        {
            // A single state in a taller than one frame panel is centred in it.
            int offsetY = area.Height > SegmentHeight ? (area.Height - SegmentHeight) / 2 : 0;

            DrawKingdom(target, area.Position, kingdom, smallFont, offsetY);

            return;
        }

        DrawDate(target, area.Position, date, smallFont, normalFont);
    }

    /// <summary>
    /// Paints the stone background, <c>_drawBackground()</c>.
    /// </summary>
    /// <param name="target">Frame buffer of the display.</param>
    /// <param name="position">Top left corner of the panel.</param>
    /// <param name="height">Height the panel occupies, at least 26 pixels.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> is below 26; the reference asserts.</exception>
    public void DrawBackground(PixelBuffer target, Point position, int height)
    {
        ArgumentNullException.ThrowIfNull(target);

        int minimumHeight = _background.Height - BackgroundChunkHeight;

        if (height < minimumHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height), height, $"A stretched panel cannot be shorter than {minimumHeight} pixels.");
        }

        // Every band places the top left corner of its source rectangle on the destination point, which is
        // fheroes2::Copy( icnston, srcX, srcY, display, dstX, dstY, w, h ) and therefore BlitSpriteClipped.
        Rect clip = target.Bounds;

        // Top band.
        ImageOps.BlitSpriteClipped(
            target,
            position,
            _background,
            new Rect(0, 0, _background.Width, BackgroundHeadHeight),
            clip);

        // Repeating middle band.
        int count = (height - minimumHeight) / BackgroundChunkHeight;

        for (int i = 0; i < count; ++i)
        {
            ImageOps.BlitSpriteClipped(
                target,
                new Point(position.X, position.Y + (BackgroundChunkHeight * i) + BackgroundHeadHeight),
                _background,
                new Rect(0, BackgroundHeadHeight, _background.Width, BackgroundChunkHeight),
                clip);
        }

        // Bottom band, glued to the end of the requested height.
        int bottomHeight = _background.Height - BackgroundHeadHeight;

        ImageOps.BlitSpriteClipped(
            target,
            new Point(position.X, position.Y + height - bottomHeight),
            _background,
            new Rect(0, BackgroundHeadHeight, _background.Width, bottomHeight),
            clip);
    }

    /// <summary>
    /// Paints the date state, <c>_drawDayInfo()</c>.
    /// </summary>
    /// <param name="target">Frame buffer of the display.</param>
    /// <param name="position">Top left corner of the panel.</param>
    /// <param name="date">Month, week and day of the calendar.</param>
    /// <param name="smallFont">Font of «Month: 1 Week: 1».</param>
    /// <param name="normalFont">Font of «Day: 1».</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public void DrawDate(
        PixelBuffer target, Point position, StatusPanelDate date, IGlyphSource smallFont, IGlyphSource normalFont)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(smallFont);
        ArgumentNullException.ThrowIfNull(normalFont);

        int frame = date.Day > 1 ? 0 : (((date.Week - 1) % 4) + 1);

        // The very first day of the game shows the same moon as any other first day of a month: frame 0.
        if (date.Day == 1 && date.Week == 1 && date.Month == 1)
        {
            frame = 0;
        }

        ImageOps.BlitSprite(target, new Point(position.X, position.Y + 1), _sunMoon[frame]);

        string monthWeek = $"Month: {date.Month} Week: {date.Week}";
        int monthWeekWidth = GlyphText.Measure(smallFont, monthWeek);
        GlyphText.DrawLine(
            smallFont,
            target,
            new Point(position.X + ((Width - monthWeekWidth) / 2), position.Y + 32),
            monthWeek);

        string day = $"Day: {date.Day}";
        int dayWidth = GlyphText.Measure(normalFont, day);
        GlyphText.DrawLine(
            normalFont,
            target,
            new Point(position.X + ((Width - dayWidth) / 2), position.Y + 48),
            day);
    }

    /// <summary>
    /// Paints the resource state, <c>_drawKingdomInfo()</c>.
    /// </summary>
    /// <param name="target">Frame buffer of the display.</param>
    /// <param name="position">Top left corner of the panel.</param>
    /// <param name="kingdom">Castle count, town count and the seven resources.</param>
    /// <param name="font">Font of every number.</param>
    /// <param name="offsetY">How far down the state sits when it is centred in a taller panel.</param>
    /// <exception cref="ArgumentNullException">Any reference argument is <see langword="null"/>.</exception>
    public void DrawKingdom(
        PixelBuffer target, Point position, StatusPanelKingdom kingdom, IGlyphSource font, int offsetY = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(font);

        int x = position.X;
        int y = position.Y + offsetY + 3;

        // The icon strip of the whole kingdom, then the counts under the matching icons.
        ImageOps.BlitSprite(target, new Point(x + 6, y), _smallResources);

        y += KingdomRowStep;

        DrawCentered(target, font, kingdom.CastleCount.ToString(), x + 26, y);
        DrawCentered(target, font, kingdom.TownCount.ToString(), x + 78, y);
        DrawCentered(target, font, kingdom.Funds.Gold.ToString(), x + 122, y);

        y += ResourceRowStep;

        // Everything but gold is abbreviated as soon as it grows past three digits, fheroes2::abbreviateNumber.
        DrawCentered(target, font, Abbreviate(kingdom.Funds.Wood), x + 15, y);
        DrawCentered(target, font, Abbreviate(kingdom.Funds.Mercury), x + 37, y);
        DrawCentered(target, font, Abbreviate(kingdom.Funds.Ore), x + 60, y);
        DrawCentered(target, font, Abbreviate(kingdom.Funds.Sulfur), x + 84, y);
        DrawCentered(target, font, Abbreviate(kingdom.Funds.Crystal), x + 108, y);
        DrawCentered(target, font, Abbreviate(kingdom.Funds.Gems), x + 130, y);
    }

    /// <summary>The <c>fheroes2::abbreviateNumber( value )</c> of a resource pile; negative piles count as empty.</summary>
    private static string Abbreviate(int value) =>
        ArmyText.AbbreviateNumber((uint)Math.Max(value, 0));

    /// <summary>Paints a number centred on a column, the <c>draw( x - text.width() / 2, y )</c> of the reference.</summary>
    private static void DrawCentered(PixelBuffer target, IGlyphSource font, string text, int centerX, int y)
    {
        int width = GlyphText.Measure(font, text);

        GlyphText.DrawLine(font, target, new Point(centerX - (width / 2), y), text);
    }
}

/// <summary>
/// Which of the panel's states is on screen, the first three of <c>Interface::StatusType</c>.
/// </summary>
/// <remarks>
/// The reference has six: <c>STATUS_UNKNOWN</c>, <c>STATUS_DAY</c>, <c>STATUS_FUNDS</c>, <c>STATUS_ARMY</c>,
/// <c>STATUS_RESOURCE</c> and <c>STATUS_AITURN</c>. The army and the found-resource stripes are drawn by
/// <c>_drawArmyInfo</c>/<c>_drawResourceInfo</c>, which need the hero's troops and the <c>RESOURCE.ICN</c>
/// index table and arrive with the castle batch; the AI hourglass is a battle-of-the-turn animation that
/// arrives with the AI loop.
/// </remarks>
public enum StatusPanelState
{
    /// <summary>Nothing but the stone, <c>STATUS_UNKNOWN</c> and the state before the first day.</summary>
    Unknown,

    /// <summary>«Month: 1 Week: 1» and «Day: 1» over the moon, <c>STATUS_DAY</c>.</summary>
    Day,

    /// <summary>The castle, town and resource counts, <c>STATUS_FUNDS</c>.</summary>
    Funds,
}

/// <summary>The calendar as the panel shows it, <c>world.GetMonth()</c>/<c>GetWeek()</c>/<c>GetDay()</c>.</summary>
/// <param name="Month">Month of the campaign, starting at 1.</param>
/// <param name="Week">Week of the month, starting at 1.</param>
/// <param name="Day">Day of the week, starting at 1.</param>
public readonly record struct StatusPanelDate(int Month, int Week, int Day);

/// <summary>What the resource state shows.</summary>
/// <param name="CastleCount">Number of castles, <c>Kingdom::GetCountCastle()</c>.</param>
/// <param name="TownCount">Number of towns, <c>Kingdom::GetCountTown()</c>.</param>
/// <param name="Funds">The seven resource piles, <c>Kingdom::GetFunds()</c>.</param>
public readonly record struct StatusPanelKingdom(int CastleCount, int TownCount, Funds Funds);
