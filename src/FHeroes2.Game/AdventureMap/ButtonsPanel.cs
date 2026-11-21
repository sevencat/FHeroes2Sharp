using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The eight buttons of the panel, in the order <c>Interface::ButtonsPanel::SetPos</c> lays them out:
/// two rows of four, 36x36 each.
/// </summary>
public enum AdventureButton
{
    /// <summary>Jump to the next hero that can still move.</summary>
    NextHero = 0,

    /// <summary>Walk the path of the current hero, visit the object he stands on or reset his path.</summary>
    HeroMovement = 1,

    /// <summary>Open the castle of the current hero (or the last one).</summary>
    Kingdom = 2,

    /// <summary>Cast an adventure map spell, needs a hero with a spell book.</summary>
    Spell = 3,

    /// <summary>End the turn of the current player.</summary>
    EndTurn = 4,

    /// <summary>Tear down the tents adventure map option / pass to the next player.</summary>
    Adventure = 5,

    /// <summary>Save, load and the other game commands.</summary>
    File = 6,

    /// <summary>Sound, music, options and the main menu.</summary>
    System = 7,
}

/// <summary>
/// The three meanings of the hero movement button, the frames
/// <c>Interface::ButtonsPanel::_setButtonStatus</c> switches between: walk the current path (2,3),
/// visit the object under the hero (16,17) or show that the path is idle (18,19).
/// </summary>
public enum HeroMovementIcon
{
    /// <summary>The hero has a path that can be walked, frames 2 and 3.</summary>
    Walk,

    /// <summary>The hero stands on an object he may visit again, frames 16 and 17.</summary>
    Action,

    /// <summary>No path, or a path that cannot be walked: frames 18 and 19, greyed out.</summary>
    Reset,
}

/// <summary>
/// Which of the buttons are enabled and which glyph the movement button shows. It is the data of
/// <c>Interface::ButtonsPanel::_setButtonStatus</c>, which turns the state of the focused hero and of
/// his kingdom into these five values.
/// </summary>
/// <param name="Movement">What the movement button offers.</param>
/// <param name="IsMovementEnabled">Whether the movement button reacts to a click.</param>
/// <param name="IsSpellEnabled">Whether the spell button reacts to a click.</param>
/// <param name="IsNextHeroEnabled">Whether the next hero button reacts to a click.</param>
/// <param name="HasValidPath">
/// Whether the focus hero has a path at all, <c>GetPath().isValidForMovement()</c>: the reference checks it a second
/// time before it accepts a long press on the movement button, to keep the long press delay from firing for a hero
/// without a path.
/// </param>
/// <remarks>
/// The other five buttons are never disabled by the reference, so they do not need a flag.
/// </remarks>
public readonly record struct AdventureButtonStatus(
    HeroMovementIcon Movement,
    bool IsMovementEnabled,
    bool IsSpellEnabled,
    bool IsNextHeroEnabled,
    bool HasValidPath = false)
{
    /// <summary>
    /// The status without a focused hero: every branch of <c>_setButtonStatus</c> that tests
    /// <c>currentHero</c> falls through, so only the five unconditional buttons stay enabled.
    /// </summary>
    public static AdventureButtonStatus WithoutHero { get; } =
        new(HeroMovementIcon.Reset, IsMovementEnabled: false, IsSpellEnabled: false, IsNextHeroEnabled: false);
}

/// <summary>
/// The eight buttons below the icon bar: the layout of <c>Interface::ButtonsPanel::SetPos</c> and its
/// <c>_redraw</c>, without the window border mover and without the click handling.
/// </summary>
/// <remarks>
/// <para>
/// The button area is 144x72, four 36x36 buttons per row and two rows, taken from
/// <c>ADVBTNS.ICN</c>: every button owns a released and a pressed frame, <c>2 * index</c> and
/// <c>2 * index + 1</c>. The movement button replaces that pair by 2/3, 16/17 or 18/19 depending on
/// the hero, so it can show three different glyphs in the same slot.
/// </para>
/// <para>
/// A disabled button is the released frame with <see cref="PaletteId.Darkening"/> applied, which is
/// what <c>ButtonBase::_getDisabled</c> does for every button of the engine.
/// </para>
/// <para>
/// The evil interface swaps the file (<c>ADVEBTNS.ICN</c>); this port draws the good one until the
/// evil artwork is wired up with the rest of the settings.
/// </para>
/// </remarks>
public sealed class ButtonsPanel
{
    /// <summary>File that holds the button frames, <c>ICN::ADVBTNS</c>.</summary>
    public const string AssetName = "ADVBTNS.ICN";

    /// <summary>File of the evil interface, <c>ICN::ADVEBTNS</c>.</summary>
    public const string EvilAssetName = "ADVEBTNS.ICN";

    /// <summary>Width and height of one button; the frames of <c>ADVBTNS.ICN</c> are square.</summary>
    public const int ButtonSize = 36;

    /// <summary>Buttons per row.</summary>
    public const int Columns = 4;

    /// <summary>Number of rows.</summary>
    public const int Rows = 2;

    /// <summary>Number of buttons in the panel.</summary>
    public const int ButtonCount = 8;

    /// <summary>Released frame of the walking hero movement button, <c>setICNInfo( icnbtn, 2, 3 )</c>.</summary>
    public const int WalkFrame = 2;

    /// <summary>Released frame of the "visit the object under the hero" button.</summary>
    public const int ActionFrame = 16;

    /// <summary>Released frame of the idle / too tired movement button.</summary>
    public const int ResetFrame = 18;

    /// <summary>
    /// Lays the panel out below the icon bar of <paramref name="panel"/>, the bay of
    /// <see cref="MapFrame"/> that runs from the bottom of the radar to the frame edge.
    /// </summary>
    /// <param name="panel">The panel bay of the interface frame.</param>
    public ButtonsPanel(Rect panel)
    {
        // The reference puts the panel one border below the icon list, which itself starts one border
        // below the radar; both strips are part of the brick background of the bay.
        int top = panel.Y + (MapFrame.BorderWidth * 2) + (IconBar.SlotCount * IconBar.SlotHeight);

        Area = new Rect(panel.X, top, Columns * ButtonSize, Rows * ButtonSize);
    }

    /// <summary>The 144x72 area the buttons are painted in.</summary>
    public Rect Area { get; }

    /// <summary>Released frame of <paramref name="button"/> in <c>ADVBTNS.ICN</c>.</summary>
    /// <param name="button">One of the eight buttons.</param>
    public static int ReleasedFrame(AdventureButton button) => (int)button * 2;

    /// <summary>Pressed frame of <paramref name="button"/>, the released one plus one.</summary>
    /// <param name="button">One of the eight buttons.</param>
    public static int PressedFrame(AdventureButton button) => ReleasedFrame(button) + 1;

    /// <summary>Released frame of a <see cref="HeroMovementIcon"/>.</summary>
    /// <param name="icon">What the movement button offers.</param>
    public static int ReleasedFrame(HeroMovementIcon icon) =>
        icon switch
        {
            HeroMovementIcon.Walk => WalkFrame,
            HeroMovementIcon.Action => ActionFrame,
            _ => ResetFrame,
        };

    /// <summary>The area of <paramref name="button"/> inside <see cref="Area"/>.</summary>
    /// <param name="button">One of the eight buttons.</param>
    public Rect AreaOf(AdventureButton button)
    {
        int column = (int)button % Columns;
        int row = (int)button / Columns;

        return new Rect(
            Area.X + (column * ButtonSize),
            Area.Y + (row * ButtonSize),
            ButtonSize,
            ButtonSize);
    }

    /// <summary>
    /// Paints the eight buttons, disabled ones darkened.
    /// </summary>
    /// <param name="frame">Logical frame to paint into.</param>
    /// <param name="sprites">Source of the button frames; <see langword="null"/> paints nothing.</param>
    /// <param name="status">Which buttons are enabled and what the movement button shows.</param>
    /// <remarks>
    /// A frame the file does not hold is skipped: this is what the reference does for the 16..19
    /// movement frames, which only the extended asset packs carry — the original <c>ADVBTNS.ICN</c>
    /// has the sixteen base frames.
    /// </remarks>
    public void Draw(PixelBuffer frame, IButtonSpriteSource? sprites, AdventureButtonStatus status)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (sprites is null)
        {
            return;
        }

        foreach (AdventureButton button in Enum.GetValues<AdventureButton>())
        {
            int released = button == AdventureButton.HeroMovement
                ? ReleasedFrame(status.Movement)
                : ReleasedFrame(button);

            Sprite? sprite = IsEnabled(button, status)
                ? sprites.GetReleased(released)
                : sprites.GetDisabled(released);

            if (sprite is null)
            {
                continue;
            }

            Rect area = AreaOf(button);
            ImageOps.BlitSprite(frame, new Point(area.X, area.Y), sprite);
        }
    }

    private static bool IsEnabled(AdventureButton button, AdventureButtonStatus status) =>
        button switch
        {
            AdventureButton.NextHero => status.IsNextHeroEnabled,
            AdventureButton.HeroMovement => status.IsMovementEnabled,
            AdventureButton.Spell => status.IsSpellEnabled,
            _ => true,
        };
}
