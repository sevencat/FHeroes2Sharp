using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The row (or column) of buttons at the bottom of a dialog, a port of
/// <c>fheroes2::ButtonGroup</c> (<c>src/fheroes2/gui/ui_button.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// A group pairs buttons with the <see cref="DialogButton"/> value each of them stands for, and
/// turns the mouse and the two hot keys of the reference into one of those values:
/// <see cref="DialogButton.None"/> means "nothing happened yet". The input model of the port is the
/// one of the scenes - a cursor position and the state of the left mouse button arrive as events -
/// so a dialog drives the group with <see cref="SetCursor"/> and <see cref="SetLeftButton"/> instead
/// of the reference's <c>LocalEvent</c> polling.
/// </para>
/// <para>
/// The click rule is the one of <c>LocalEvent::MouseClickLeft( area )</c>: the press must have
/// happened inside the area and so must the release, which is why the group remembers where the
/// button went down. While the left button is held, the button under the cursor is drawn pressed -
/// only when the press itself happened inside it, so dragging the cursor over a neighbour does not
/// light it up. The long press rule of the reference is not ported: the port has no long press
/// feature yet, so every press that ends inside the button is a click.
/// </para>
/// <para>
/// <see cref="CreateStandard"/> ports the constructor that places the standard OK/CANCEL, YES/NO and
/// single button rows. The constructors that build a group from a list of texts or from a whole ICN
/// table are not ported yet: they need the text-button factories of <c>ui_button.cpp</c>, which are
/// only partly here (<see cref="ButtonArtwork"/> covers the sprite side, not the label).
/// </para>
/// </remarks>
public sealed class ButtonGroup
{
    private readonly List<ButtonBase> _buttons = [];
    private readonly List<DialogButton> _values = [];

    private Point? _cursor;
    private Point? _pressPosition;
    private bool _leftButtonHeld;

    /// <summary>Number of buttons in the group, <c>getButtonsCount()</c> of the reference.</summary>
    public int Count => _buttons.Count;

    /// <summary>The button at <paramref name="index"/>, in the order the buttons were added.</summary>
    /// <param name="index">Zero based index of the button.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not smaller than <see cref="Count"/>.
    /// </exception>
    public ButtonBase this[int index] => _buttons[index];

    /// <summary>The value a click on the button at <paramref name="index"/> returns.</summary>
    /// <param name="index">Zero based index of the button.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not smaller than <see cref="Count"/>.
    /// </exception>
    public DialogButton ValueOf(int index) => _values[index];

    /// <summary>Adds a button that is already built, <c>addButton()</c> of the reference.</summary>
    /// <param name="button">Button to add; the group drives its state from then on.</param>
    /// <param name="value">What a click on the button returns.</param>
    /// <exception cref="ArgumentNullException"><paramref name="button"/> is <see langword="null"/>.</exception>
    public void Add(ButtonBase button, DialogButton value)
    {
        ArgumentNullException.ThrowIfNull(button);

        _buttons.Add(button);
        _values.Add(value);
    }

    /// <summary>
    /// Creates a sprite button and adds it to the group, the
    /// <c>createButton( offsetX, offsetY, Sprite, Sprite, value )</c> overload of the reference.
    /// </summary>
    /// <param name="offsetX">Horizontal offset added to both sprite anchors.</param>
    /// <param name="offsetY">Vertical offset added to both sprite anchors.</param>
    /// <param name="released">Sprite drawn in the normal state.</param>
    /// <param name="pressed">Sprite drawn while the button is held down.</param>
    /// <param name="value">What a click on the button returns.</param>
    /// <returns>The button that was added, so a dialog can enable or disable it later.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="released"/> or <paramref name="pressed"/> is <see langword="null"/>.
    /// </exception>
    public ButtonSprite CreateButton(int offsetX, int offsetY, Sprite released, Sprite pressed,
        DialogButton value)
    {
        ButtonSprite button = new(released, pressed, null, offsetX, offsetY);
        Add(button, value);
        return button;
    }

    /// <summary>
    /// Builds the standard button row of a dialog, the
    /// <c>ButtonGroup( const Rect &amp; area, const int buttonTypes )</c> constructor of the
    /// reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="area"/> is the whole window content, so the buttons always sit on its bottom
    /// edge. A pair is spread with a quarter of the free width as padding on both sides of the pair;
    /// a single button is centred. Unsupported combinations produce an empty group, exactly like the
    /// <c>default:</c> of the reference switch.
    /// </para>
    /// <para>
    /// The ICN sprites of the original carry their own anchor, and so does <see cref="ButtonSprite"/>:
    /// the sizes below come from the sprites, the placement from <paramref name="area"/>, and the
    /// anchor is added on top by the button itself.
    /// </para>
    /// </remarks>
    /// <param name="area">Area the buttons have to be placed in, usually <see cref="Dialogs.StandardWindow.ActiveArea"/>.</param>
    /// <param name="buttons">Which buttons to place, e.g. <c>DialogButton.Ok | DialogButton.Cancel</c>.</param>
    /// <param name="artwork">Source of the released and pressed sprites.</param>
    /// <returns>The group with the placed buttons, in reading order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public static ButtonGroup CreateStandard(Rect area, DialogButton buttons, IDialogButtonArtworkSource artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        ButtonGroup group = new();

        switch (buttons)
        {
            case DialogButton.Yes | DialogButton.No:
                group.AddPair(area, DialogButton.Yes, DialogButton.No, artwork);
                break;

            case DialogButton.Ok | DialogButton.Cancel:
                group.AddPair(area, DialogButton.Ok, DialogButton.Cancel, artwork);
                break;

            case DialogButton.Ok:
                group.AddCentered(area, DialogButton.Ok, artwork);
                break;

            case DialogButton.Cancel:
                group.AddCentered(area, DialogButton.Cancel, artwork);
                break;

            default:
                break;
        }

        return group;
    }

    /// <summary>
    /// Places two buttons with a quarter of the free width as padding, the
    /// <c>Dialog::YES | Dialog::NO</c> and <c>Dialog::OK | Dialog::CANCEL</c> branches.
    /// </summary>
    private void AddPair(Rect area, DialogButton leftValue, DialogButton rightValue,
        IDialogButtonArtworkSource artwork)
    {
        DialogButtonArtwork left = artwork.Get(leftValue);
        DialogButtonArtwork right = artwork.Get(rightValue);

        int padding = (area.Width - left.Released.Width - right.Released.Width) / 4;

        CreateButton(
            area.X + padding,
            area.Y + area.Height - left.Released.Height,
            left.Released,
            left.Pressed,
            leftValue);

        CreateButton(
            area.X + area.Width - right.Released.Width - padding,
            area.Y + area.Height - right.Released.Height,
            right.Released,
            right.Pressed,
            rightValue);
    }

    /// <summary>Places one button in the horizontal middle of the bottom edge.</summary>
    private void AddCentered(Rect area, DialogButton value, IDialogButtonArtworkSource artwork)
    {
        DialogButtonArtwork button = artwork.Get(value);

        CreateButton(
            area.X + ((area.Width - button.Released.Width) / 2),
            area.Y + area.Height - button.Released.Height,
            button.Released,
            button.Pressed,
            value);
    }

    /// <summary>Draws the buttons in the order they were added.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (ButtonBase button in _buttons)
        {
            button.Draw(target);
        }
    }

    /// <summary>Draws the drop shadow of every button, <c>drawShadows()</c> of the reference.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void DrawShadows(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (ButtonBase button in _buttons)
        {
            button.DrawShadow(target);
        }
    }

    /// <summary>Enables every button of the group.</summary>
    public void Enable()
    {
        foreach (ButtonBase button in _buttons)
        {
            button.Enable();
        }
    }

    /// <summary>Disables every button of the group, e.g. while a dialog is busy.</summary>
    public void Disable()
    {
        foreach (ButtonBase button in _buttons)
        {
            button.Disable();
        }
    }

    /// <summary>
    /// Records the cursor position, or <see langword="null"/> when it left the window. The pressed
    /// look of the buttons follows immediately, even without a mouse button event.
    /// </summary>
    /// <param name="cursor">Cursor position in logical pixels.</param>
    public void SetCursor(Point? cursor)
    {
        if (_cursor == cursor)
        {
            return;
        }

        _cursor = cursor;
        UpdatePressedStates();
    }

    /// <summary>
    /// Records the left mouse button and returns the value of the button the press and the release
    /// both happened on, <see cref="DialogButton.None"/> otherwise.
    /// </summary>
    /// <param name="down">Whether the left mouse button is now held.</param>
    /// <returns>Value of the clicked button, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton SetLeftButton(bool down)
    {
        if (down == _leftButtonHeld)
        {
            return DialogButton.None;
        }

        _leftButtonHeld = down;

        if (down)
        {
            _pressPosition = _cursor;
            UpdatePressedStates();
            return DialogButton.None;
        }

        // The buttons are released before the click is resolved: the reference draws them in their
        // released look first and tests MouseClickLeft() against that area afterwards.
        UpdatePressedStates();

        DialogButton clicked = FindClicked();

        _pressPosition = null;
        return clicked;
    }

    /// <summary>
    /// Returns the value of the button a hot key stands for, <see cref="DialogButton.None"/> when the
    /// group has none: the <c>processEvents()</c> hot key block of the reference.
    /// </summary>
    /// <remarks>
    /// A group with exactly one OK or CANCEL button answers whichever of the two keys arrives
    /// (<c>Game::HotKeyCloseWindow()</c>). Note that the reference does not check whether that single
    /// button is enabled, so neither does the port.
    /// </remarks>
    /// <param name="hotKey">Hot key that was pressed.</param>
    /// <returns>Value of the button the key stands for, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton ProcessHotKey(ButtonHotKey hotKey)
    {
        if (_buttons.Count == 1 && (_values[0] == DialogButton.Ok || _values[0] == DialogButton.Cancel))
        {
            return _values[0];
        }

        for (int index = 0; index < _buttons.Count; ++index)
        {
            if (!_buttons[index].IsEnabled)
            {
                continue;
            }

            DialogButton value = _values[index];

            if (hotKey == ButtonHotKey.Okay && (value == DialogButton.Yes || value == DialogButton.Ok))
            {
                return value;
            }

            if (hotKey == ButtonHotKey.Cancel && (value == DialogButton.No || value == DialogButton.Cancel))
            {
                return value;
            }
        }

        return DialogButton.None;
    }

    /// <summary>
    /// Shows every enabled button in its pressed look while the left mouse button is held inside it,
    /// <c>drawOnState( le.isMouseLeftButtonPressedAndHeldInArea( area() ) )</c> of the reference.
    /// </summary>
    private void UpdatePressedStates()
    {
        foreach (ButtonBase button in _buttons)
        {
            if (!button.IsEnabled)
            {
                continue;
            }

            Rect area = button.Area;
            bool pressed = _leftButtonHeld
                && _pressPosition is { } pressPosition
                && _cursor is { } cursor
                && area.Contains(pressPosition)
                && area.Contains(cursor);

            if (pressed)
            {
                button.Press();
            }
            else
            {
                button.Release();
            }
        }
    }

    /// <summary>
    /// Finds the button the press and the release both landed on, the
    /// <c>le.MouseClickLeft( button-&gt;area() )</c> loop of the reference.
    /// </summary>
    private DialogButton FindClicked()
    {
        if (_pressPosition is not { } pressPosition || _cursor is not { } cursor)
        {
            return DialogButton.None;
        }

        for (int index = 0; index < _buttons.Count; ++index)
        {
            ButtonBase button = _buttons[index];
            if (!button.IsEnabled)
            {
                continue;
            }

            Rect area = button.Area;
            if (area.Contains(pressPosition) && area.Contains(cursor))
            {
                return _values[index];
            }
        }

        return DialogButton.None;
    }
}

