namespace FHeroes2.UI.Widgets;

/// <summary>Visual state of a <see cref="Button"/>.</summary>
public enum ButtonState
{
    /// <summary>Nothing is over the button.</summary>
    Released = 0,

    /// <summary>The cursor is over the button.</summary>
    Hovered = 1,

    /// <summary>The left mouse button is held down over the button.</summary>
    Pressed = 2,
}
