using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A button made of two sprites, a port of <c>fheroes2::ButtonSprite</c>
/// (<c>src/fheroes2/gui/ui_button.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// This is the button of every dialog: the released and the pressed look come from the artwork the
/// dialog was composed with (<c>UNIFORM_GOOD_OKAY_BUTTON</c> and friends), and a third sprite can
/// carry a separate disabled look. Without one, <see cref="ButtonBase"/> darkens the released
/// sprite through <see cref="PaletteId.Darkening"/>, exactly like the reference.
/// </para>
/// <para>
/// The areas are recomputed whenever the sprite set changes, because the sprites of a dialog button
/// are not required to share a size or an anchor: <c>setSprite()</c> of the reference does the same
/// and it is what makes a button that swaps its artwork mid dialog keep a correct hit box.
/// </para>
/// </remarks>
public sealed class ButtonSprite : ButtonBase
{
    private Sprite _released;
    private Sprite _pressed;
    private Sprite? _disabled;

    /// <summary>
    /// Creates a sprite button.
    /// </summary>
    /// <param name="released">Sprite drawn in the normal state.</param>
    /// <param name="pressed">Sprite drawn while the button is held down.</param>
    /// <param name="disabled">
    /// Sprite drawn while the button is disabled, or <see langword="null"/> to darken
    /// <paramref name="released"/> instead.
    /// </param>
    /// <param name="offsetX">Horizontal offset added to both sprite anchors.</param>
    /// <param name="offsetY">Vertical offset added to both sprite anchors.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="released"/> or <paramref name="pressed"/> is <see langword="null"/>.
    /// </exception>
    public ButtonSprite(Sprite released, Sprite pressed, Sprite? disabled = null, int offsetX = 0,
        int offsetY = 0)
        : base(offsetX, offsetY)
    {
        ArgumentNullException.ThrowIfNull(released);
        ArgumentNullException.ThrowIfNull(pressed);

        _released = released;
        _pressed = pressed;
        _disabled = disabled;

        UpdateButtonAreas();
    }

    /// <summary>The sprite drawn in the normal state.</summary>
    public Sprite Released => _released;

    /// <summary>The sprite drawn while the button is held down.</summary>
    public Sprite Pressed => _pressed;

    /// <summary>
    /// Replaces the sprite set and recomputes the areas, <c>setSprite()</c> of the reference.
    /// </summary>
    /// <param name="released">Sprite drawn in the normal state.</param>
    /// <param name="pressed">Sprite drawn while the button is held down.</param>
    /// <param name="disabled">
    /// Sprite drawn while the button is disabled, or <see langword="null"/> to darken
    /// <paramref name="released"/> instead.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="released"/> or <paramref name="pressed"/> is <see langword="null"/>.
    /// </exception>
    public void SetSprite(Sprite released, Sprite pressed, Sprite? disabled = null)
    {
        ArgumentNullException.ThrowIfNull(released);
        ArgumentNullException.ThrowIfNull(pressed);

        _released = released;
        _pressed = pressed;
        _disabled = disabled;

        UpdateButtonAreas();
    }

    /// <inheritdoc/>
    protected override Sprite GetPressed() => _pressed;

    /// <inheritdoc/>
    protected override Sprite GetReleased() => _released;

    /// <summary>
    /// Returns the disabled artwork of the button, or the darkened released sprite when the caller
    /// did not provide one.
    /// </summary>
    protected override Sprite GetDisabled() => _disabled ?? base.GetDisabled();
}
