using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// An icon button whose three looks are plain sprites, which is how the original engine draws
/// every menu and dialog button.
/// </summary>
/// <remarks>
/// <para>
/// The button owns no layout: its <see cref="Area"/> is the bounding box of the released sprite at
/// the anchor stored in the ICN header, so a widget built from <c>BTNSHNGL.ICN</c> lands on the
/// exact pixel the artwork was drawn for. Drawing is a single transform layer aware blit
/// (<see cref="ImageOps.BlitSprite"/>), therefore shadows and anti aliased edges behave like every
/// other sprite of the game.
/// </para>
/// <para>
/// <see cref="Update"/> keeps the state machine in one place: a button is
/// <see cref="ButtonState.Hovered"/> when the cursor is inside <see cref="Area"/> and
/// <see cref="ButtonState.Pressed"/> while the left mouse button is held there. Missing
/// hover/pressed sprites fall back to <see cref="Released"/>, so a table with only one frame still
/// works.
/// </para>
/// </remarks>
public sealed class Button
{
    public Button(Sprite released, Sprite? hovered = null, Sprite? pressed = null)
    {
        ArgumentNullException.ThrowIfNull(released);

        Released = released;
        Hovered = hovered;
        Pressed = pressed;
        Area = new Rect(released.OffsetX, released.OffsetY, released.Width, released.Height);
    }

    /// <summary>The sprite drawn when the cursor is elsewhere.</summary>
    public Sprite Released { get; }

    /// <summary>The sprite drawn while the cursor is over the button, or <see langword="null"/>.</summary>
    public Sprite? Hovered { get; }

    /// <summary>The sprite drawn while the button is held down, or <see langword="null"/>.</summary>
    public Sprite? Pressed { get; }

    /// <summary>Clickable area in logical pixels, taken from the released sprite.</summary>
    public Rect Area { get; }

    public ButtonState State { get; private set; } = ButtonState.Released;

    public bool IsPressed => State == ButtonState.Pressed;

    /// <summary>The sprite that matches <see cref="State"/>.</summary>
    public Sprite CurrentSprite => State switch
    {
        ButtonState.Pressed => Pressed ?? Released,
        ButtonState.Hovered => Hovered ?? Released,
        _ => Released,
    };

    /// <summary>Checks whether <paramref name="point"/> (logical pixels) is inside the button.</summary>
    public bool HitTest(Point point) => Area.Contains(point);

    /// <summary>
    /// Recomputes the visual state from the cursor and the mouse button.
    /// </summary>
    /// <param name="cursor">Cursor position in logical pixels, or <see langword="null"/> when it left the window.</param>
    /// <param name="leftButtonDown">Whether the left mouse button is currently held.</param>
    /// <returns><see langword="true"/> when the state changed, so the caller can skip a redraw.</returns>
    public bool Update(Point? cursor, bool leftButtonDown)
    {
        ButtonState state = cursor is { } position && HitTest(position)
            ? (leftButtonDown ? ButtonState.Pressed : ButtonState.Hovered)
            : ButtonState.Released;

        if (state == State)
        {
            return false;
        }

        State = state;
        return true;
    }

    /// <summary>Draws the sprite of the current state at its own anchor.</summary>
    public void Draw(PixelBuffer target) => ImageOps.BlitSprite(target, CurrentSprite);
}
