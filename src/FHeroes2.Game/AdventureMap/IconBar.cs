using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The two columns of icons below the radar: the heroes of the kingdom on the left, its castles on
/// the right, four slots each. It is the layout and the painting of <c>Interface::IconsPanel</c>
/// without its list box, its scroll bars and its marker.
/// </summary>
/// <remarks>
/// <para>
/// The geometry is the one of <c>Interface::IconsPanel::SetPos</c> for a 640x480 display: the panel
/// sits one border below the radar (<c>176</c>), each column is 56 pixels wide and four slots high,
/// and the castle column starts 72 pixels to the right of the hero column. The icons themselves are
/// 46x22 at an inset of five pixels, which is what the sprites of <c>LOCATORS.ICN</c> are drawn at.
/// </para>
/// <para>
/// <c>LOCATORS.ICN</c> holds the empty slot frames (1…8, one per player color), the six fortified
/// castles (9…14) and the six towns (15…20), all of them 46x22. Frame 0 is the 56x32 background of one
/// slot, which the frame art of the panel already provides.
/// </para>
/// </remarks>
public sealed class IconBar
{
    /// <summary>File that holds the slot frames, <c>ICN::LOCATORS</c>.</summary>
    public const string AssetName = "LOCATORS.ICN";

    /// <summary>Width of one slot, <c>iconsCursorWidth</c> of the reference.</summary>
    public const int SlotWidth = 56;

    /// <summary>Height of one slot, <c>iconsCursorHeight</c> of the reference.</summary>
    public const int SlotHeight = 32;

    /// <summary>Distance between the two columns, <c>setPos( rect.x + 72, rect.y )</c>.</summary>
    public const int ColumnGap = 72;

    /// <summary>Inset of the icon inside its slot, <c>redrawItem( ox + 5, oy + 5 )</c>.</summary>
    public const int IconInset = 5;

    /// <summary>Width of an icon, the width of the <c>LOCATORS</c> frames.</summary>
    public const int IconWidth = 46;

    /// <summary>Height of an icon, the height of the <c>LOCATORS</c> frames.</summary>
    public const int IconHeight = 22;

    /// <summary>Number of slots per column on a 640x480 display.</summary>
    public const int SlotCount = 4;

    /// <summary>First frame of the empty slot sprites.</summary>
    public const int EmptySlotFirstFrame = 1;

    /// <summary>Number of empty slot sprites, one per player color.</summary>
    public const int EmptySlotFrameCount = 8;

    /// <summary>First frame of the fortified castle sprites, one per race.</summary>
    public const int CastleFirstFrame = 9;

    /// <summary>First frame of the town sprites, one per race.</summary>
    public const int TownFirstFrame = 15;

    /// <summary>
    /// Lays the bar out below <paramref name="panel"/>, the bay of <see cref="MapFrame"/> that runs
    /// from the bottom of the radar to the frame edge.
    /// </summary>
    /// <param name="panel">The panel bay of the interface frame.</param>
    public IconBar(Rect panel)
    {
        // The icons start one border below the radar, like the brick of the panel background
        // (MapFrame.DrawPanel), and not at the top of the bay: the reference leaves that strip free.
        int top = panel.Y + MapFrame.BorderWidth;

        Heroes = new Rect(panel.X, top, SlotWidth, SlotCount * SlotHeight);
        Castles = new Rect(Heroes.X + ColumnGap, top, SlotWidth, SlotCount * SlotHeight);
    }

    /// <summary>Column of the hero icons, the left one.</summary>
    public Rect Heroes { get; }

    /// <summary>Column of the castle icons, the right one.</summary>
    public Rect Castles { get; }

    /// <summary>The slot <paramref name="row"/> of a column, 56x32.</summary>
    /// <param name="column">Either <see cref="Heroes"/> or <see cref="Castles"/>.</param>
    /// <param name="row">Slot index, 0 at the top.</param>
    public static Rect SlotAt(Rect column, int row) =>
        new(column.X, column.Y + (row * SlotHeight), SlotWidth, SlotHeight);

    /// <summary>
    /// Frame of <c>LOCATORS.ICN</c> that draws a castle of <paramref name="race"/>,
    /// <c>fheroes2::getCastleIcnIndex</c> of the reference.
    /// </summary>
    /// <param name="race">Race of the castle.</param>
    /// <param name="hasFort">Whether the castle is fortified, i.e. a castle and not a town.</param>
    /// <returns>
    /// The frame index, or <see langword="null"/> for a race the data cannot hold. A random castle is frames 25 and
    /// 26, the editor's "random castle" icons; the reference asserts and answers 1 (an empty slot frame) for anything
    /// else, which the port turns into <see langword="null"/> rather than painting a wrong sprite.
    /// </returns>
    public static int? CastleFrame(MapRace race, bool hasFort) =>
        race switch
        {
            MapRace.Knight => hasFort ? CastleFirstFrame : TownFirstFrame,
            MapRace.Barbarian => (hasFort ? CastleFirstFrame : TownFirstFrame) + 1,
            MapRace.Sorceress => (hasFort ? CastleFirstFrame : TownFirstFrame) + 2,
            MapRace.Warlock => (hasFort ? CastleFirstFrame : TownFirstFrame) + 3,
            MapRace.Wizard => (hasFort ? CastleFirstFrame : TownFirstFrame) + 4,
            MapRace.Necromancer => (hasFort ? CastleFirstFrame : TownFirstFrame) + 5,

            // The editor's "random castle" icon lives at frames 25 and 26 (Race::RAND in the reference).
            MapRace.Random => hasFort ? 25 : 26,

            // The reference asserts and falls back to frame 1, which is an empty slot: answering "nothing" is the
            // port's way of not painting a wrong sprite for a race the data cannot have.
            _ => null,
        };

    /// <summary>
    /// Frame of <c>LOCATORS.ICN</c> that draws the empty slot of <paramref name="row"/>, the
    /// <c>1 + i % 8</c> of <c>Interface::IconsBar::redrawBackground</c> (one frame per player color).
    /// </summary>
    /// <param name="row">Slot index, 0 at the top.</param>
    public static int EmptySlotFrame(int row) => EmptySlotFirstFrame + (row % EmptySlotFrameCount);

    /// <summary>
    /// Paints the bar: the castles of <paramref name="castles"/> in the right column, empty slots for
    /// whatever is left. The left column is empty for now, because an MP2 hero has no identity yet.
    /// </summary>
    /// <param name="frame">Logical frame to paint into.</param>
    /// <param name="sprites">Source of the icons; <see langword="null"/> leaves the bar untouched.</param>
    /// <param name="castles">
    /// Castles to show, in map order; only the first <see cref="SlotCount"/> of them fit.
    /// </param>
    public void Draw(PixelBuffer frame, IIconSpriteSource? sprites, IReadOnlyList<Mp2CastlePosition> castles)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(castles);

        if (sprites is null)
        {
            return;
        }

        for (int row = 0; row < SlotCount; ++row)
        {
            // A hero tile of an MP2 file only carries the MINIHERO sprite of the hero, not its
            // identity, and the small portrait of the reference (MINIPORT) is indexed by that
            // identity. Until the map's hero information blocks are read the hero column stays empty.
            DrawIcon(frame, sprites.GetEmptySlot(row), SlotAt(Heroes, row));

            Sprite? castle = row < castles.Count
                ? sprites.GetCastle(castles[row].Race, castles[row].HasFort)
                : null;

            DrawIcon(frame, castle ?? sprites.GetEmptySlot(row), SlotAt(Castles, row));
        }
    }

    private static void DrawIcon(PixelBuffer frame, Sprite? sprite, Rect slot)
    {
        if (sprite is null)
        {
            return;
        }

        // The frames of LOCATORS are cut without their offset, so the icon is placed by the inset of
        // the slot and not by Sprite.OffsetX/Y (which is the 5,5 the file stores).
        ImageOps.BlitSprite(frame, new Point(slot.X + IconInset, slot.Y + IconInset), sprite);
    }
}
