using FHeroes2.Core.Geometry;
using FHeroes2.Core.Timing;

namespace FHeroes2.UI.Input;

/// <summary>
/// Which mouse button an event is about, the reference's <c>LocalEvent::MouseButtonType</c>
/// (<c>src/engine/localevent.h:400-406</c>).
/// </summary>
public enum MouseButtonType
{
    Unknown = 0,
    Left,
    Middle,
    Right,
}

/// <summary>
/// Whether a key went down or up, the reference's <c>LocalEvent::KeyboardEventState</c>
/// (<c>src/engine/localevent.h:408-413</c>).
/// </summary>
public enum KeyboardEventState
{
    Unknown = 0,
    Down,
    Up,
}

/// <summary>
/// The state of the mouse, the wheel, the keyboard and the drag gesture between two frames, a port of
/// the state machine of <c>LocalEvent</c> (<c>src/engine/localevent.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>What is ported.</b> The event <i>state</i> and every query built on it: the eight action state
/// bits of <c>localevent.h:440-462</c>, the press/release positions of the three buttons, the wheel
/// offset, the long press rule of <c>MouseLongPressLeft</c> (850 ms, one hit only), the two hooks
/// (<c>setGlobalMouseMotionEventHook</c>, <c>setGlobalKeyDownEventHook</c>), the drag flag and the
/// quit hook. A dialog can therefore be driven exactly like the reference drives it: the scene pushes
/// what happened, <c>MouseClickLeft( area )</c> and friends answer what that means.
/// </para>
/// <para>
/// <b>How <c>HandleEvents</c> is ported.</b> The reference's <c>HandleEvents</c>
/// (<c>localevent.cpp:1243-1316</c>) is the frame: reset the one-shot states, let the platform engine
/// poll and translate the events, render the affected area and sleep. The state reset is
/// <see cref="BeginFrame"/>, the SDL half is <see cref="IEventEngine"/> and the frame is
/// <see cref="HandleEvents"/> itself — a dialog can therefore be written exactly like the reference,
/// <c>while ( le.HandleEvents() ) { ... }</c>. A scene that drives its own loop keeps translating SDL
/// events into <see cref="OnMouseMotion"/>, <see cref="OnMouseButton"/>, <see cref="OnKeyboard"/> and
/// <see cref="OnMouseWheel"/> directly. The gamepad (<c>onController*</c>), the two-finger touchpad
/// gesture (<c>onTouchFingerEvent</c>) and the emulated pointer are still not translated: they need
/// SDL devices and a render loop. <c>KeySymGetName</c> goes through <c>SDL_GetKeyName</c> and is not
/// here either; <see cref="KeyText"/> has the character side of the keyboard.
/// </para>
/// <para>
/// The reference has a process-wide instance behind <c>LocalEvent::Get()</c>; the port mirrors that
/// with <see cref="Current"/>. The constructor is public anyway so a test can own its instance and its
/// clock.
/// </para>
/// </remarks>
public sealed class LocalEvent
{
    /// <summary>
    /// How long the left button has to be held for <see cref="MouseLongPressLeft"/> to fire, the
    /// reference's <c>mouseButtonLongPressTimeout</c> (<c>localevent.cpp:74</c>).
    /// </summary>
    public const ulong LongPressTimeoutMs = 850;

    /// <summary>
    /// How long a frame sleeps after the events have been processed, the reference's
    /// <c>globalLoopSleepTime</c> (<c>localevent.cpp:1286-1288</c>).
    /// </summary>
    public const ulong LoopSleepTimeMs = 1;

    private readonly LongPressDelay _longPressDelay;

    private IEventEngine? _engine;

    private ActionStates _actionStates = ActionStates.None;
    private Key _currentKey = Key.None;
    private MouseButtonType _currentButton = MouseButtonType.Unknown;

    private Point _leftPressPosition;
    private Point _middlePressPosition;
    private Point _rightPressPosition;

    private Point _leftReleasePosition;
    private Point _middleReleasePosition;
    private Point _rightReleasePosition;

    private Point _cursorPosition;
    private Point _wheelMovementOffset;

    private Rect _cursorRenderArea;

    /// <summary>Creates the event state, the reference's private constructor.</summary>
    /// <param name="clockMs">
    /// Monotonic clock in milliseconds for the long press delay; <see langword="null"/> uses
    /// <see cref="TimeDelay"/>'s stopwatch.
    /// </param>
    public LocalEvent(Func<long>? clockMs = null)
    {
        _longPressDelay = new LongPressDelay(LongPressTimeoutMs, clockMs);
    }

    /// <summary>
    /// The process-wide instance, the reference's <c>LocalEvent::Get()</c>
    /// (<c>localevent.cpp:1236-1241</c>).
    /// </summary>
    public static LocalEvent Current { get; } = new();

    /// <summary>
    /// The platform side of <see cref="HandleEvents"/>, the reference's <c>_engine</c>
    /// (<c>localevent.h:481</c>). The application attaches its implementation, a test attaches a fake.
    /// </summary>
    public IEventEngine? Engine
    {
        get => _engine;
        set => _engine = value;
    }

    /// <summary>
    /// Area the caller has to present before drawing the next frame, the reference's
    /// <c>display.updateNextRenderRoi( roi )</c> deferred by <c>HandleEvents( false )</c>
    /// (<c>localevent.cpp:1302-1304</c>). Empty when nothing is pending.
    /// </summary>
    public Rect NextRenderArea { get; private set; }

    /// <summary>
    /// Called on every cursor movement with the new position; the rectangle it returns becomes
    /// <see cref="CursorRenderArea"/>. The reference's <c>setGlobalMouseMotionEventHook</c>
    /// (<c>localevent.h:207-210</c>), used by the cursor layer to ask for an incremental redraw.
    /// </summary>
    public Func<int, int, Rect>? MouseMotionHook { get; set; }

    /// <summary>
    /// Called on every key down with the key and its modifier bits, the reference's
    /// <c>setGlobalKeyDownEventHook</c> (<c>localevent.h:212-215</c>).
    /// </summary>
    public Action<Key, KeyModifier>? KeyDownHook { get; set; }

    /// <summary>
    /// Answers the window close request, the reference's <c>setQuitEventProcessingHook</c>
    /// (<c>localevent.h:344-347</c>): <see langword="true"/> asks the application to close.
    /// </summary>
    public Func<bool>? QuitEventHook { get; set; }

    /// <summary>
    /// Area the cursor layer asked to redraw for the last movement, the reference's
    /// <c>_mouseCursorRenderArea</c>. Empty when no movement happened this frame.
    /// </summary>
    public Rect CursorRenderArea => _cursorRenderArea;

    /// <summary>
    /// Clears every action state, the reference's <c>reset()</c> (<c>localevent.h:201-204</c>). It
    /// also forgets <see cref="CursorRenderArea"/>.
    /// </summary>
    public void Reset()
    {
        _actionStates = ActionStates.None;
        _cursorRenderArea = default;
    }

    /// <summary>
    /// Starts a frame: clears the one-shot states and keeps the ongoing ones, the top of the
    /// reference's <c>HandleEvents</c> (<c>localevent.cpp:1243-1262</c>).
    /// </summary>
    /// <remarks>
    /// A pressed button and a held key are ongoing, so they survive; a click, a movement, a release, a
    /// wheel turn and the touchpad flag do not. The touchpad flag is kept while a button is pressed,
    /// because the reference does not want the gesture to end in the middle of a drag
    /// (<c>localevent.cpp:1259-1261</c>).
    /// </remarks>
    public void BeginFrame()
    {
        // "Mouse area must be updated only once so we will use only the latest area for rendering."
        _cursorRenderArea = default;

        ResetStates(ActionStates.KeyPressed);
        ResetStates(ActionStates.MouseMotion);
        ResetStates(ActionStates.MouseReleased);
        ResetStates(ActionStates.MouseWheel);

        if ((_actionStates & ActionStates.MousePressed) == 0)
        {
            ResetStates(ActionStates.MouseTouch);
        }
    }

    /// <summary>
    /// Runs one frame: resets the one-shot states, polls the platform events through <see cref="Engine"/>,
    /// presents the affected area and sleeps, the reference's <c>HandleEvents</c>
    /// (<c>localevent.cpp:1243-1316</c>).
    /// </summary>
    /// <remarks>
    /// This is what lets a dialog be written exactly like the reference,
    /// <c>while ( le.HandleEvents() ) { ... }</c>, instead of being fed one event at a time by a scene.
    /// </remarks>
    /// <param name="sleepAfterEventProcessing">
    /// Whether to present and sleep, the reference's <c>sleepAfterEventProcessing</c>. Pass
    /// <see langword="false"/> when the caller draws right after the call: the area is then only
    /// recorded in <see cref="NextRenderArea"/>.
    /// </param>
    /// <param name="allowExit">Whether a window close request may end the loop.</param>
    /// <returns><see langword="false"/> when the application has to stop.</returns>
    /// <exception cref="InvalidOperationException">No <see cref="Engine"/> is attached.</exception>
    public bool HandleEvents(bool sleepAfterEventProcessing = true, bool allowExit = false)
    {
        IEventEngine engine = Engine
            ?? throw new InvalidOperationException(
                "No IEventEngine is attached to this LocalEvent; attach one before calling HandleEvents().");

        NextRenderArea = Rect.Empty;
        BeginFrame();

        if (!engine.HandleEvents(this, allowExit, out bool displayRefreshRequired))
        {
            return false;
        }

        // "To maintain color cycling animation we need to render the whole frame with an updated palette."
        Rect renderArea = displayRefreshRequired ? engine.ScreenArea : CursorRenderArea;

        if (sleepAfterEventProcessing)
        {
            if (!renderArea.IsEmpty)
            {
                engine.Render(renderArea);
            }

            // The reference subtracts the time the event processing took from its 1 ms budget
            // (localevent.cpp:1294-1297). The port sleeps the whole millisecond and leaves the real
            // frame rate to the presenter's vsync.
            engine.Sleep(LoopSleepTimeMs);
        }
        else if (!renderArea.IsEmpty)
        {
            NextRenderArea = renderArea;
        }

        return true;
    }

    /// <summary>Whether the cursor moved this frame, <c>hasMouseMoved()</c>.</summary>
    public bool HasMouseMoved => (_actionStates & ActionStates.MouseMotion) == ActionStates.MouseMotion;

    /// <summary>The cursor position, <c>getMouseCursorPos()</c>.</summary>
    public Point CursorPosition => _cursorPosition;

    /// <summary>Where the left button went down, <c>getMouseLeftButtonPressedPos()</c>.</summary>
    public Point LeftButtonPressPosition => _leftPressPosition;

    /// <summary>
    /// Pushes a cursor movement, the reference's <c>onMouseMotionEvent</c>
    /// (<c>localevent.cpp:1676-1686</c>).
    /// </summary>
    /// <param name="position">New cursor position in logical pixels.</param>
    public void OnMouseMotion(Point position)
    {
        SetStates(ActionStates.MouseMotion);
        _cursorPosition = position;

        if (MouseMotionHook is not null)
        {
            _cursorRenderArea = MouseMotionHook(position.X, position.Y);
        }
    }

    /// <summary>
    /// Pushes a mouse button press or release, the reference's <c>onMouseButtonEvent</c>
    /// (<c>localevent.cpp:1688-1747</c>).
    /// </summary>
    /// <param name="isPressed">Whether the button went down.</param>
    /// <param name="buttonType">Which button it was.</param>
    /// <param name="position">Cursor position of the event.</param>
    public void OnMouseButton(bool isPressed, MouseButtonType buttonType, Point position)
    {
        if (isPressed)
        {
            // A new press restarts the long press count, and it also clears the "already fired" flag
            // of the previous one: a second long press on the same gesture is a new long press.
            _longPressDelay.Reset();

            SetStates(ActionStates.MousePressed);
        }
        else
        {
            ResetStates(ActionStates.MousePressed);
            ResetStates(ActionStates.DragOngoing);

            SetStates(ActionStates.MouseReleased);
        }

        _currentButton = buttonType;

        _cursorPosition = position;

        if ((_actionStates & ActionStates.MousePressed) != 0)
        {
            switch (buttonType)
            {
                case MouseButtonType.Left:
                    _leftPressPosition = _cursorPosition;
                    break;

                case MouseButtonType.Middle:
                    _middlePressPosition = _cursorPosition;
                    break;

                case MouseButtonType.Right:
                    _rightPressPosition = _cursorPosition;
                    break;

                default:
                    // The reference asserts here; a release of an unknown button is simply ignored.
                    break;
            }
        }
        else
        {
            switch (buttonType)
            {
                case MouseButtonType.Left:
                    _leftReleasePosition = _cursorPosition;
                    break;

                case MouseButtonType.Middle:
                    _middleReleasePosition = _cursorPosition;
                    break;

                case MouseButtonType.Right:
                    _rightReleasePosition = _cursorPosition;
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Pushes a key down or up, the reference's <c>onKeyboardEvent</c>
    /// (<c>localevent.cpp:1658-1674</c>).
    /// </summary>
    /// <param name="key">Key that changed state.</param>
    /// <param name="modifier">Modifier bits held at that moment; only the hook sees them.</param>
    /// <param name="state">Whether the key went down or up.</param>
    public void OnKeyboard(Key key, KeyModifier modifier, KeyboardEventState state)
    {
        if (state == KeyboardEventState.Down)
        {
            SetStates(ActionStates.KeyPressed);
            SetStates(ActionStates.KeyHold);

            KeyDownHook?.Invoke(key, modifier);
        }
        else if (state == KeyboardEventState.Up)
        {
            ResetStates(ActionStates.KeyPressed);
            ResetStates(ActionStates.KeyHold);
        }

        _currentKey = key;
    }

    /// <summary>
    /// Pushes a wheel turn, the reference's <c>onMouseWheelEvent</c>
    /// (<c>localevent.cpp:1328-1333</c>).
    /// </summary>
    /// <remarks>
    /// The reference also stamps the middle release position with the cursor, an oddity that is kept
    /// as is: it makes a wheel turn look like a middle button release that ended where the cursor
    /// already was, which no click rule accepts.
    /// </remarks>
    /// <param name="movementOffset">Wheel delta; positive y scrolls up.</param>
    public void OnMouseWheel(Point movementOffset)
    {
        SetStates(ActionStates.MouseWheel);
        _middleReleasePosition = _cursorPosition;
        _wheelMovementOffset = movementOffset;
    }

    /// <summary>Whether the wheel turned up this frame, <c>isMouseWheelUp()</c>.</summary>
    public bool IsMouseWheelUp => (_actionStates & ActionStates.MouseWheel) != 0 && _wheelMovementOffset.Y > 0;

    /// <summary>Whether the wheel turned down this frame, <c>isMouseWheelDown()</c>.</summary>
    public bool IsMouseWheelDown => (_actionStates & ActionStates.MouseWheel) != 0 && _wheelMovementOffset.Y < 0;

    /// <summary>Whether the left button is held down, <c>isMouseLeftButtonPressed()</c>.</summary>
    public bool IsMouseLeftButtonPressed =>
        (_actionStates & ActionStates.MousePressed) != 0 && _currentButton == MouseButtonType.Left;

    /// <summary>
    /// Whether the left button went down inside <paramref name="area"/>,
    /// <c>isMouseLeftButtonPressedInArea()</c>.
    /// </summary>
    /// <param name="area">Area in logical pixels.</param>
    public bool IsMouseLeftButtonPressedInArea(Rect area) =>
        IsMouseLeftButtonPressed && area.Contains(_leftPressPosition);

    /// <summary>
    /// Whether the left button is held down and both the press and the cursor are inside
    /// <paramref name="area"/>, <c>isMouseLeftButtonPressedAndHeldInArea()</c>.
    /// </summary>
    /// <param name="area">Area in logical pixels.</param>
    public bool IsMouseLeftButtonPressedAndHeldInArea(Rect area) =>
        IsMouseLeftButtonPressedInArea(area) && IsMouseCursorPosInArea(area);

    /// <summary>Whether the right button is held down, <c>isMouseRightButtonPressed()</c>.</summary>
    public bool IsMouseRightButtonPressed =>
        (_actionStates & ActionStates.MousePressed) != 0 && _currentButton == MouseButtonType.Right;

    /// <summary>
    /// Whether the right button went down inside <paramref name="area"/>,
    /// <c>isMouseRightButtonPressedInArea()</c>.
    /// </summary>
    /// <param name="area">Area in logical pixels.</param>
    public bool IsMouseRightButtonPressedInArea(Rect area) =>
        IsMouseRightButtonPressed && area.Contains(_rightPressPosition);

    /// <summary>Whether the left button was released this frame, <c>isMouseLeftButtonReleased()</c>.</summary>
    public bool IsMouseLeftButtonReleased =>
        (_actionStates & ActionStates.MouseReleased) != 0 && _currentButton == MouseButtonType.Left;

    /// <summary>
    /// Whether the left button was released inside <paramref name="area"/>,
    /// <c>isMouseLeftButtonReleasedInArea()</c>.
    /// </summary>
    /// <param name="area">Area in logical pixels.</param>
    public bool IsMouseLeftButtonReleasedInArea(Rect area) =>
        IsMouseLeftButtonReleased && area.Contains(_leftReleasePosition);

    /// <summary>
    /// Whether the wheel turned up while the cursor was inside <paramref name="area"/>,
    /// <c>isMouseWheelUpInArea()</c>.
    /// </summary>
    /// <param name="area">Area in logical pixels.</param>
    public bool IsMouseWheelUpInArea(Rect area) => IsMouseWheelUp && area.Contains(_cursorPosition);

    /// <summary>
    /// Whether the wheel turned down while the cursor was inside <paramref name="area"/>,
    /// <c>isMouseWheelDownInArea()</c>.
    /// </summary>
    /// <param name="area">Area in logical pixels.</param>
    public bool IsMouseWheelDownInArea(Rect area) => IsMouseWheelDown && area.Contains(_cursorPosition);

    /// <summary>Whether the cursor is inside <paramref name="area"/>, <c>isMouseCursorPosInArea()</c>.</summary>
    /// <param name="area">Area in logical pixels.</param>
    public bool IsMouseCursorPosInArea(Rect area) => area.Contains(_cursorPosition);

    /// <summary>
    /// Whether the current mouse event came from a touchpad, <c>isMouseEventFromTouchpad()</c>.
    /// </summary>
    /// <remarks>
    /// Always <see langword="false"/> until the two-finger gesture is ported: nothing sets the flag.
    /// </remarks>
    public bool IsMouseEventFromTouchpad => (_actionStates & ActionStates.MouseTouch) != 0;

    /// <summary>Whether a key went down this frame, <c>isAnyKeyPressed()</c>.</summary>
    public bool IsAnyKeyPressed => (_actionStates & ActionStates.KeyPressed) != 0;

    /// <summary>Whether <paramref name="key"/> went down this frame, <c>isKeyPressed()</c>.</summary>
    /// <param name="key">Key to test.</param>
    public bool IsKeyPressed(Key key) => key == _currentKey && (_actionStates & ActionStates.KeyPressed) != 0;

    /// <summary>Whether a key is being held down, <c>isKeyBeingHold()</c>.</summary>
    public bool IsKeyBeingHold => (_actionStates & ActionStates.KeyHold) != 0;

    /// <summary>The last key that went down, <c>getPressedKeyValue()</c>.</summary>
    public Key PressedKeyValue => _currentKey;

    /// <summary>Whether a drag started this gesture, <c>isDragInProgress()</c>.</summary>
    public bool IsDragInProgress => (_actionStates & ActionStates.DragOngoing) != 0;

    /// <summary>
    /// Marks the current gesture as a drag, <c>registerDrag()</c>; the flag lives until the button is
    /// released.
    /// </summary>
    public void RegisterDrag() => SetStates(ActionStates.DragOngoing);

    /// <summary>
    /// Restarts the long press count without clearing the "already fired" flag, the reference's
    /// <c>resetLongPress()</c>, used by a control that wants a continuous press.
    /// </summary>
    public void ResetLongPress() => _longPressDelay.Reset();

    /// <summary>
    /// Asks the quit hook whether the application should close, the reference's
    /// <c>processQuitEvent()</c>; no hook means no.</summary>
    public bool ProcessQuitEvent() => QuitEventHook?.Invoke() ?? false;

    /// <summary>
    /// A left click whose press and release both happened inside <paramref name="area"/>, the
    /// reference's <c>MouseClickLeft( area )</c> (<c>localevent.cpp:1768-1789</c>).
    /// </summary>
    /// <remarks>
    /// The release state is consumed by the first call that answers <see langword="true"/>, so two
    /// controls that ask about the same area do not both react. A long press that already fired
    /// swallows the click: the press was not a click, it was a hold.
    /// </remarks>
    /// <param name="area">Area in logical pixels.</param>
    /// <returns>Whether the click happened.</returns>
    public bool MouseClickLeft(Rect area)
    {
        if ((_actionStates & ActionStates.MouseReleased) == 0)
        {
            return false;
        }

        if (_currentButton != MouseButtonType.Left)
        {
            return false;
        }

        if (!area.Contains(_leftPressPosition) || !area.Contains(_leftReleasePosition))
        {
            return false;
        }

        if (_longPressDelay.IsTriggered)
        {
            return false;
        }

        ResetStates(ActionStates.MouseReleased);

        return true;
    }

    /// <summary>
    /// A left click anywhere, the reference's <c>MouseClickLeft()</c>
    /// (<c>localevent.cpp:1749-1766</c>).
    /// </summary>
    /// <returns>Whether the click happened.</returns>
    public bool MouseClickLeft()
    {
        if ((_actionStates & ActionStates.MouseReleased) == 0)
        {
            return false;
        }

        if (_currentButton != MouseButtonType.Left)
        {
            return false;
        }

        if (_longPressDelay.IsTriggered)
        {
            return false;
        }

        ResetStates(ActionStates.MouseReleased);

        return true;
    }

    /// <summary>A middle click anywhere, the reference's <c>MouseClickMiddle()</c>.</summary>
    /// <returns>Whether the click happened.</returns>
    public bool MouseClickMiddle()
    {
        if ((_actionStates & ActionStates.MouseReleased) == 0)
        {
            return false;
        }

        if (_currentButton != MouseButtonType.Middle)
        {
            return false;
        }

        if (_longPressDelay.IsTriggered)
        {
            return false;
        }

        ResetStates(ActionStates.MouseReleased);

        return true;
    }

    /// <summary>A right click anywhere, the reference's <c>MouseClickRight()</c>.</summary>
    /// <returns>Whether the click happened.</returns>
    public bool MouseClickRight()
    {
        if ((_actionStates & ActionStates.MouseReleased) == 0)
        {
            return false;
        }

        if (_currentButton != MouseButtonType.Right)
        {
            return false;
        }

        if (_longPressDelay.IsTriggered)
        {
            return false;
        }

        ResetStates(ActionStates.MouseReleased);

        return true;
    }

    /// <summary>
    /// A right click whose press and release both happened inside <paramref name="area"/>, the
    /// reference's <c>MouseClickRight( area )</c>.
    /// </summary>
    /// <param name="area">Area in logical pixels.</param>
    /// <returns>Whether the click happened.</returns>
    public bool MouseClickRight(Rect area)
    {
        if ((_actionStates & ActionStates.MouseReleased) == 0)
        {
            return false;
        }

        if (_currentButton != MouseButtonType.Right)
        {
            return false;
        }

        if (!area.Contains(_rightPressPosition) || !area.Contains(_rightReleasePosition))
        {
            return false;
        }

        if (_longPressDelay.IsTriggered)
        {
            return false;
        }

        ResetStates(ActionStates.MouseReleased);

        return true;
    }

    /// <summary>
    /// The left button has been held inside <paramref name="area"/> for at least
    /// <see cref="LongPressTimeoutMs"/>, the reference's <c>MouseLongPressLeft( area )</c>
    /// (<c>localevent.cpp:1852-1877</c>).
    /// </summary>
    /// <remarks>
    /// It answers <see langword="true"/> at most once per press: the delay remembers that it fired,
    /// and the click rules refuse the release afterwards. The order of the last two checks is the
    /// reference's - "has enough time passed?" before "did it already fire?".
    /// </remarks>
    /// <param name="area">Area in logical pixels.</param>
    /// <returns>Whether this is the frame the long press fired.</returns>
    public bool MouseLongPressLeft(Rect area)
    {
        if ((_actionStates & ActionStates.MousePressed) == 0)
        {
            return false;
        }

        if (_currentButton != MouseButtonType.Left)
        {
            return false;
        }

        if (!area.Contains(_leftPressPosition))
        {
            return false;
        }

        if (!_longPressDelay.IsPassed())
        {
            return false;
        }

        if (_longPressDelay.IsTriggered)
        {
            return false;
        }

        _longPressDelay.SetTriggered();

        return true;
    }

    private void SetStates(ActionStates states) => _actionStates |= states;

    private void ResetStates(ActionStates states) => _actionStates &= ~states;

    /// <summary>The action state bits of <c>localevent.h:440-462</c>.</summary>
    [Flags]
    private enum ActionStates : uint
    {
        None = 0,

        /// <summary>A key went down this frame.</summary>
        KeyPressed = 1 << 0,

        /// <summary>The cursor moved this frame.</summary>
        MouseMotion = 1 << 1,

        /// <summary>A button is held down, an ongoing state.</summary>
        MousePressed = 1 << 2,

        /// <summary>A button was released this frame.</summary>
        MouseReleased = 1 << 3,

        /// <summary>The wheel turned this frame.</summary>
        MouseWheel = 1 << 4,

        /// <summary>The mouse event came from a touchpad.</summary>
        MouseTouch = 1 << 5,

        /// <summary>A key is held down, an ongoing state.</summary>
        KeyHold = 1 << 6,

        /// <summary>A control registered the start of a drag.</summary>
        DragOngoing = 1 << 7,
    }

    /// <summary>
    /// <see cref="TimeDelay"/> plus the "already fired" flag, the reference's nested
    /// <c>LongPressDelay</c> (<c>localevent.h:464-488</c>).
    /// </summary>
    private sealed class LongPressDelay
    {
        private readonly TimeDelay _delay;

        private bool _triggered;

        public LongPressDelay(ulong delayMs, Func<long>? clockMs)
        {
            _delay = new TimeDelay(delayMs, clockMs);
        }

        /// <summary>Whether the long press already fired for this gesture.</summary>
        public bool IsTriggered => _triggered;

        /// <summary>Restarts the count and forgets that the long press fired.</summary>
        public void Reset()
        {
            _delay.Reset();
            _triggered = false;
        }

        /// <summary>Whether the configured delay has passed since the last reset.</summary>
        public bool IsPassed() => _delay.IsPassed();

        /// <summary>Marks the long press as fired.</summary>
        public void SetTriggered() => _triggered = true;
    }
}
