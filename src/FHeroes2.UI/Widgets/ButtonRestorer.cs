using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Shows a button darkened for the duration of a scope and repaints it afterwards, a port of
/// <c>fheroes2::ButtonRestorer</c> (<c>ui_button.cpp:643-665</c>). The castle dialogs wrap an
/// embedded window in it so that the exit button looks inactive while the window is open.
/// </summary>
/// <remarks>
/// The reference captures <c>button.isEnabled()</c> once, so a button that was already disabled is
/// left alone by <see cref="Dispose"/>. It repaints into <c>Display::instance()</c> and asks the
/// renderer to upload the button area; the port has no display of its own, so the frame buffer is
/// passed in and the caller uploads the frame - the same split as <see cref="ButtonBase.DrawOnPress"/>.
/// </remarks>
public sealed class ButtonRestorer : IDisposable
{
    private readonly PixelBuffer _target;
    private readonly ButtonBase _button;
    private readonly bool _wasEnabled;
    private bool _isDisposed;

    /// <summary>
    /// Darkens <paramref name="button"/> and draws it right away, the constructor of the reference.
    /// </summary>
    /// <param name="button">Button to show as disabled while the scope lasts.</param>
    /// <param name="target">Indexed frame buffer of the scene the button sits in.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="button"/> or <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public ButtonRestorer(ButtonBase button, PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(target);

        _button = button;
        _target = target;
        _wasEnabled = button.IsEnabled;

        if (_wasEnabled)
        {
            button.Disable();
            button.Draw(target);
        }
    }

    /// <summary>Re-enables the button and repaints it, the destructor of the reference.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_wasEnabled)
        {
            _button.Enable();
            _button.Draw(_target);
        }
    }
}
