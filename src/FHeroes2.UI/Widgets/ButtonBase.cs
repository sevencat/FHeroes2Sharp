using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Base class of the dialog buttons, a port of <c>fheroes2::ButtonBase</c>
/// (<c>src/fheroes2/gui/ui_button.h</c>).
/// </summary>
/// <remarks>
/// <para>
/// A button owns two rectangles: the bounds of its pressed sprite and the bounds of its released
/// sprite, each moved by the button offset. <see cref="Area"/> is the one that is active, which is
/// what the mouse is tested against - the reference returns the same pair from <c>area()</c>. The
/// sprites bring the anchor stored in the ICN header along, so a button built from a table lands on
/// the pixels the artwork was drawn for and needs no layout of its own.
/// </para>
/// <para>
/// The three looks are resolved through <see cref="GetReleased"/>, <see cref="GetPressed"/> and
/// <see cref="GetDisabled"/>, so a subclass only has to answer those. A button without its own
/// disabled artwork is darkened through <see cref="PaletteId.Darkening"/>, the
/// <c>PAL::PaletteType::DARKENING</c> copy the reference caches per released sprite.
/// </para>
/// <para>
/// Unlike <see cref="Button"/> - the hover aware sign of the main menu - a dialog button has no
/// hover look: it is pressed while the left mouse button is held inside it and released otherwise,
/// which is what <see cref="ButtonGroup"/> drives. The reference also notifies its
/// <c>ActionObject</c> subscribers on every state change; the port has no observers yet, so the
/// methods only report the change to their caller.
/// </para>
/// </remarks>
public abstract class ButtonBase
{
    /// <summary>Direction of the drop shadow, <c>drawShadow()</c> of the reference.</summary>
    private static readonly Point ShadowOffset = new(-5, 5);

    /// <summary>The released sprite <see cref="_disabled"/> was built from.</summary>
    private Sprite? _disabledSource;

    /// <summary>Darkened copy of <see cref="_disabledSource"/>, cached like the reference does.</summary>
    private Sprite? _disabled;

    /// <summary>Creates a button whose sprites are drawn at their own anchor.</summary>
    protected ButtonBase()
        : this(0, 0)
    {
    }

    /// <summary>Creates a button that moves every sprite by <paramref name="offsetX"/>/<paramref name="offsetY"/>.</summary>
    protected ButtonBase(int offsetX, int offsetY)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        PressedArea = new Rect(offsetX, offsetY, 0, 0);
        ReleasedArea = new Rect(offsetX, offsetY, 0, 0);
    }

    /// <summary>Horizontal offset every sprite of the button is drawn at.</summary>
    public int OffsetX { get; private set; }

    /// <summary>Vertical offset every sprite of the button is drawn at.</summary>
    public int OffsetY { get; private set; }

    /// <summary>Whether the button is currently shown in its pressed look.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>Whether the button reacts to the mouse and draws its released look undarkened.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Whether <see cref="Draw"/> paints anything at all.</summary>
    public bool IsVisible { get; private set; } = true;

    /// <summary>Bounds of the pressed sprite, <c>_areaPressed</c> of the reference.</summary>
    public Rect PressedArea { get; private set; }

    /// <summary>
    /// Bounds of the released sprite, or of its darkened version while the button is disabled
    /// (<c>_updateReleasedArea()</c> of the reference).
    /// </summary>
    public Rect ReleasedArea { get; private set; }

    /// <summary>The area the mouse is tested against: the pressed one while the button is pressed.</summary>
    public Rect Area => IsPressed ? PressedArea : ReleasedArea;

    /// <summary>
    /// Switches the button to its pressed look. Does nothing when the button is disabled, which is
    /// how the reference refuses a press on an inactive button.
    /// </summary>
    /// <returns><see langword="true"/> when the button is pressed afterwards.</returns>
    public bool Press()
    {
        if (!IsEnabled)
        {
            return false;
        }

        IsPressed = true;
        return true;
    }

    /// <summary>Switches the button back to its released look; ignored while disabled.</summary>
    /// <returns><see langword="true"/> when the button is released afterwards.</returns>
    public bool Release()
    {
        if (!IsEnabled)
        {
            return false;
        }

        IsPressed = false;
        return true;
    }

    /// <summary>Enables the button and takes the (possibly larger) released sprite into account again.</summary>
    public void Enable()
    {
        IsEnabled = true;
        UpdateReleasedArea();
    }

    /// <summary>
    /// Disables the button. A disabled button is never pressed - <c>disable()</c> of the reference
    /// clears the pressed state - and its released area follows the darkened sprite.
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        IsPressed = false;
        UpdateReleasedArea();
    }

    /// <summary>Makes the button drawn again; the reference calls this <c>show()</c>.</summary>
    public void Show() => IsVisible = true;

    /// <summary>Stops drawing the button; it still reacts to nothing in <see cref="Draw"/>.</summary>
    public void Hide() => IsVisible = false;

    /// <summary>
    /// Moves the button, sprites and areas together, <c>setPosition()</c> of the reference.
    /// </summary>
    public void SetPosition(int offsetX, int offsetY)
    {
        int deltaX = offsetX - OffsetX;
        int deltaY = offsetY - OffsetY;

        PressedArea = PressedArea.Offset(deltaX, deltaY);
        ReleasedArea = ReleasedArea.Offset(deltaX, deltaY);
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>
    /// Draws the sprite that matches the current state at its area, <c>draw()</c> of the reference.
    /// </summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <returns><see langword="true"/> when something was drawn, i.e. the button is visible.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public bool Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!IsVisible)
        {
            return false;
        }

        if (IsPressed)
        {
            ImageOps.BlitSprite(target, PressedArea.Position, GetPressed());
        }
        else
        {
            ImageOps.BlitSprite(target, ReleasedArea.Position, IsEnabled ? GetReleased() : GetDisabled());
        }

        return true;
    }

    /// <summary>
    /// Draws the drop shadow of the released sprite, <c>drawShadow()</c> of the reference: five
    /// pixels to the left and five below the button, cast by the sprite silhouette.
    /// </summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void DrawShadow(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.AddGradientShadow(GetReleased(), target, Area.Position, ShadowOffset);
    }

    /// <summary>
    /// Presses the button and draws it, <c>drawOnPress()</c> of the reference. The port has no
    /// display to render into, so the caller uploads the frame itself.
    /// </summary>
    /// <returns><see langword="true"/> when the state changed.</returns>
    public bool DrawOnPress(PixelBuffer target)
    {
        if (IsPressed || !Press())
        {
            return false;
        }

        if (IsVisible)
        {
            Draw(target);
        }

        return true;
    }

    /// <summary>Releases the button and draws it, <c>drawOnRelease()</c> of the reference.</summary>
    /// <returns><see langword="true"/> when the state changed.</returns>
    public bool DrawOnRelease(PixelBuffer target)
    {
        if (!IsPressed || !Release())
        {
            return false;
        }

        if (IsVisible)
        {
            Draw(target);
        }

        return true;
    }

    /// <summary>Picks <see cref="DrawOnPress"/> or <see cref="DrawOnRelease"/>, <c>drawOnState()</c>.</summary>
    /// <param name="pressed">Whether the button should look pressed.</param>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <returns><see langword="true"/> when the state changed.</returns>
    public bool DrawOnState(bool pressed, PixelBuffer target) =>
        pressed ? DrawOnPress(target) : DrawOnRelease(target);

    /// <summary>The sprite drawn while the mouse button is held inside the button.</summary>
    protected abstract Sprite GetPressed();

    /// <summary>The sprite drawn in the normal state; a disabled button darkens it.</summary>
    protected abstract Sprite GetReleased();

    /// <summary>
    /// The darkened look of a disabled button. The reference caches the conversion per released
    /// sprite and recomputes it when a subclass returns a different one, which matters for buttons
    /// whose sprite set changes (an ICN index switch, for instance).
    /// </summary>
    protected virtual Sprite GetDisabled()
    {
        Sprite released = GetReleased();

        if (_disabled is null || !ReferenceEquals(_disabledSource, released))
        {
            _disabledSource = released;
            _disabled = released.Clone();
            ImageOps.ApplyPalette(_disabled.Image, PaletteId.Darkening);
        }

        return _disabled;
    }

    /// <summary>Recomputes both areas from the sprites, <c>_updateButtonAreas()</c>.</summary>
    protected void UpdateButtonAreas()
    {
        Sprite pressed = GetPressed();
        PressedArea = new Rect(
            OffsetX + pressed.OffsetX, OffsetY + pressed.OffsetY, pressed.Width, pressed.Height);

        UpdateReleasedArea();
    }

    /// <summary>
    /// Recomputes the released area from the sprite the current state draws: the darkened one while
    /// the button is disabled, which can be a different size than the released artwork.
    /// </summary>
    private void UpdateReleasedArea()
    {
        Sprite released = IsEnabled ? GetReleased() : GetDisabled();
        ReleasedArea = new Rect(
            OffsetX + released.OffsetX, OffsetY + released.OffsetY, released.Width, released.Height);
    }
}
