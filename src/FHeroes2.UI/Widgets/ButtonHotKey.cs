namespace FHeroes2.UI.Widgets;

/// <summary>
/// Hot keys a <see cref="ButtonGroup"/> understands, the <c>HotKeyEvent</c> entries the reference's
/// <c>processEvents()</c> looks at.
/// </summary>
/// <remarks>
/// The full hot key system (<c>Game::HotKeyPressEvent</c>, remappable keys, the settings file) is not
/// ported yet. A caller therefore translates its keys into one of these two and hands the result to
/// <see cref="ButtonGroup.ProcessHotKey"/>, which keeps the choice of what "accept" and "cancel"
/// mean in one place.
/// </remarks>
public enum ButtonHotKey
{
    /// <summary>Enter or space, <c>HotKeyEvent::DEFAULT_OKAY</c>; answers YES and OK.</summary>
    Okay = 0,

    /// <summary>Escape, <c>HotKeyEvent::DEFAULT_CANCEL</c>; answers NO and CANCEL.</summary>
    Cancel = 1,
}
