using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A plain on/off checkbox, the <c>renderCheckbox()</c> of the reference's auto playtest dialog
/// (<c>game_auto_playtest.cpp:360-381</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference builds it from two frames of <c>CELLWIN.ICN</c>: frame 1 is the empty box, frame 2 the tick,
/// and the tick is blitted at the box origin plus its own frame offset. The port keeps that split, so a caller
/// that already holds the frames (a dialog's artwork) pays for one blit per frame and not for a repaint.
/// </para>
/// <para>
/// <b>Two flags, not one.</b> <c>isEnabled</c> is the value of the box, <c>isActive</c> says whether the box can
/// be used at all: the reference draws the tick only when both are true and darkens the whole box when the box
/// is inactive, because the "Sound Effects" box is greyed out while animation is off. <see cref="Toggle"/> obeys
/// the same rule and leaves an inactive box alone.
/// </para>
/// <para>
/// The evil interface is a palette, not a different set of frames: the reference recolours the box and the tick
/// in place with <c>GOOD_TO_EVIL_INTERFACE</c> after drawing them, which is why <see cref="IsEvilInterface"/> is a
/// flag of the widget rather than a parameter of the artwork.
/// </para>
/// </remarks>
public sealed class CheckboxWidget
{
    private readonly Sprite _box;
    private readonly Sprite? _tick;

    /// <summary>
    /// Creates the checkbox, <c>renderCheckbox( offsetX, offsetY, isEnabled, output, isEvilInterface, isActive )</c>.
    /// </summary>
    /// <param name="box">Empty box, frame 1 of <c>CELLWIN.ICN</c>.</param>
    /// <param name="tick">
    /// The tick, frame 2 of <c>CELLWIN.ICN</c>; <see langword="null"/> when the archive has no such frame, in which
    /// case the box still works but never shows a mark.
    /// </param>
    /// <param name="position">Top left corner of the box in screen coordinates.</param>
    /// <param name="isChecked">Whether the tick starts visible.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
    public CheckboxWidget(Sprite box, Sprite? tick, Point position, bool isChecked = false)
    {
        ArgumentNullException.ThrowIfNull(box);

        _box = box;
        _tick = tick;

        Position = position;
        Area = new Rect(position.X, position.Y, box.Width, box.Height);
        IsChecked = isChecked;
    }

    /// <summary>Top left corner of the box.</summary>
    public Point Position { get; }

    /// <summary>The rectangle the box occupies, the return value of <c>renderCheckbox()</c>.</summary>
    public Rect Area { get; }

    /// <summary>The value of the box, <c>isEnabled</c> of the reference.</summary>
    public bool IsChecked { get; set; }

    /// <summary>
    /// Whether the box can be used, <c>isActive</c> of the reference. An inactive box is drawn darkened and
    /// <see cref="Toggle"/> ignores it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether the box is drawn for the evil interface.</summary>
    public bool IsEvilInterface { get; set; }

    /// <summary>
    /// Flips the value, the click the reference's dialog performs on the box area.
    /// </summary>
    /// <returns><see langword="true"/> when the value changed, i.e. when the box is active.</returns>
    public bool Toggle()
    {
        if (!IsActive)
        {
            return false;
        }

        IsChecked = !IsChecked;

        return true;
    }

    /// <summary>
    /// Paints the box, its tick and the two palette fix-ups, in the order the reference uses: box, tick, evil
    /// recolour, darkening. The order matters — the palettes have to reach the tick as well as the box.
    /// </summary>
    /// <param name="target">Indexed frame buffer of the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, Position, _box);

        if (IsChecked && IsActive && _tick is not null)
        {
            ImageOps.BlitSprite(target, new Point(Position.X + _tick.OffsetX, Position.Y + _tick.OffsetY), _tick);
        }

        if (IsEvilInterface)
        {
            ImageOps.ApplyPalette(target, Area, PaletteTable.Get(PaletteId.GoodToEvilInterface));
        }

        if (!IsActive)
        {
            ImageOps.ApplyPalette(target, Area, PaletteTable.Get(PaletteId.Darkening));
        }
    }
}
