using FHeroes2.Core.Geometry;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="LocalEvent"/>: the event state machine of <c>src/engine/localevent.cpp</c>.
/// </summary>
public sealed class LocalEventTests
{
    private static readonly Rect Button = new(10, 10, 20, 10);

    [Fact]
    public void BeginFrame_ClearsTheOneShotStatesAndKeepsTheOngoingOne()
    {
        LocalEvent events = CreateEvents();

        events.OnMouseButton(true, MouseButtonType.Left, new Point(15, 15));

        Assert.True(events.IsMouseLeftButtonPressed);

        // A pressed button is an ongoing state: the next frame still sees it.
        events.BeginFrame();

        Assert.True(events.IsMouseLeftButtonPressed);

        events.OnMouseButton(false, MouseButtonType.Left, new Point(15, 15));

        Assert.True(events.IsMouseLeftButtonReleased);

        events.BeginFrame();

        Assert.False(events.IsMouseLeftButtonReleased);
        Assert.False(events.IsMouseLeftButtonPressed);
    }

    [Fact]
    public void BeginFrame_ClearsTheMotionAndTheWheel()
    {
        LocalEvent events = CreateEvents();

        events.OnMouseMotion(new Point(5, 5));
        events.OnMouseWheel(new Point(0, 1));

        Assert.True(events.HasMouseMoved);
        Assert.True(events.IsMouseWheelUp);

        events.BeginFrame();

        Assert.False(events.HasMouseMoved);
        Assert.False(events.IsMouseWheelUp);
    }

    [Fact]
    public void MouseClickLeft_RequiresBothEndsInsideTheArea()
    {
        LocalEvent events = CreateEvents();

        Click(events, new Point(15, 15), new Point(15, 15));
        Assert.True(events.MouseClickLeft(Button));

        // The release left the button: not a click, and the state is not consumed either.
        Click(events, new Point(15, 15), new Point(100, 100));
        Assert.False(events.MouseClickLeft(Button));

        // The press started elsewhere and was dragged in: not a click either.
        Click(events, new Point(100, 100), new Point(15, 15));
        Assert.False(events.MouseClickLeft(Button));
    }

    [Fact]
    public void MouseClickLeft_IsConsumedByTheFirstQuestion()
    {
        LocalEvent events = CreateEvents();

        Click(events, new Point(15, 15), new Point(15, 15));

        Assert.True(events.MouseClickLeft(Button));
        Assert.False(events.MouseClickLeft(Button));
    }

    [Fact]
    public void MouseClickLeft_WithoutAnArea_AcceptsAnyPosition()
    {
        LocalEvent events = CreateEvents();

        Click(events, new Point(-50, -50), new Point(900, 700));

        Assert.True(events.MouseClickLeft());
    }

    [Fact]
    public void MouseClickLeft_RejectsTheOtherButtons()
    {
        LocalEvent events = CreateEvents();

        Click(events, MouseButtonType.Right, new Point(15, 15), new Point(15, 15));

        Assert.False(events.IsMouseLeftButtonReleased);
        Assert.False(events.MouseClickLeft(Button));
        Assert.True(events.MouseClickRight(Button));
    }

    [Fact]
    public void MouseClickRight_RequiresBothEndsInsideTheArea()
    {
        LocalEvent events = CreateEvents();

        Click(events, MouseButtonType.Right, new Point(15, 15), new Point(15, 15));
        Assert.True(events.MouseClickRight(Button));

        Click(events, MouseButtonType.Right, new Point(15, 15), new Point(100, 100));
        Assert.False(events.MouseClickRight(Button));
    }

    [Fact]
    public void MouseClickMiddle_WorksWithoutAnArea()
    {
        LocalEvent events = CreateEvents();

        Click(events, MouseButtonType.Middle, new Point(1, 2), new Point(3, 4));

        Assert.True(events.MouseClickMiddle());
        Assert.False(events.MouseClickLeft());
    }

    [Fact]
    public void PressedAndHeldInArea_AlsoWantsTheCursorInside()
    {
        LocalEvent events = CreateEvents();

        events.OnMouseButton(true, MouseButtonType.Left, new Point(15, 15));
        events.OnMouseMotion(new Point(15, 15));

        Assert.True(events.IsMouseLeftButtonPressedInArea(Button));
        Assert.True(events.IsMouseLeftButtonPressedAndHeldInArea(Button));

        // The cursor left the button while the button is still down.
        events.OnMouseMotion(new Point(100, 100));

        Assert.True(events.IsMouseLeftButtonPressedInArea(Button));
        Assert.False(events.IsMouseLeftButtonPressedAndHeldInArea(Button));
        Assert.False(events.IsMouseCursorPosInArea(Button));
    }

    [Fact]
    public void RightButtonPress_IsTrackedAtItsOwnPosition()
    {
        LocalEvent events = CreateEvents();

        events.OnMouseButton(true, MouseButtonType.Right, new Point(15, 15));

        Assert.True(events.IsMouseRightButtonPressed);
        Assert.True(events.IsMouseRightButtonPressedInArea(Button));
        Assert.False(events.IsMouseLeftButtonPressed);

        events.OnMouseMotion(new Point(100, 100));

        Assert.True(events.IsMouseRightButtonPressedInArea(Button));
    }

    [Fact]
    public void RegisterDrag_LivesUntilTheButtonIsReleased()
    {
        LocalEvent events = CreateEvents();

        events.OnMouseButton(true, MouseButtonType.Left, new Point(15, 15));
        events.RegisterDrag();

        Assert.True(events.IsDragInProgress);

        events.BeginFrame();

        Assert.True(events.IsDragInProgress);

        events.OnMouseButton(false, MouseButtonType.Left, new Point(15, 15));

        Assert.False(events.IsDragInProgress);
    }

    [Fact]
    public void Wheel_ReportsItsDirectionAndTheCursorArea()
    {
        LocalEvent events = CreateEvents();

        events.OnMouseMotion(new Point(15, 15));
        events.OnMouseWheel(new Point(0, 1));

        Assert.True(events.IsMouseWheelUp);
        Assert.False(events.IsMouseWheelDown);
        Assert.True(events.IsMouseWheelUpInArea(Button));
        Assert.False(events.IsMouseWheelDownInArea(Button));

        events.BeginFrame();
        events.OnMouseWheel(new Point(0, -1));

        Assert.False(events.IsMouseWheelUp);
        Assert.True(events.IsMouseWheelDown);
        Assert.True(events.IsMouseWheelDownInArea(Button));

        // A horizontal turn is neither up nor down.
        events.OnMouseWheel(new Point(3, 0));

        Assert.False(events.IsMouseWheelUp);
        Assert.False(events.IsMouseWheelDown);
    }

    [Fact]
    public void Keyboard_ReportsOnlyTheLatestKeyOfTheFrame()
    {
        LocalEvent events = CreateEvents();

        events.OnKeyboard(Key.A, KeyModifier.None, KeyboardEventState.Down);

        Assert.True(events.IsAnyKeyPressed);
        Assert.True(events.IsKeyPressed(Key.A));
        Assert.False(events.IsKeyPressed(Key.B));
        Assert.Equal(Key.A, events.PressedKeyValue);

        Assert.True(events.IsKeyBeingHold);

        // The key-up clears both states...
        events.OnKeyboard(Key.A, KeyModifier.None, KeyboardEventState.Up);

        Assert.False(events.IsAnyKeyPressed);
        Assert.False(events.IsKeyBeingHold);

        // ...but the value of the last key stays.
        Assert.Equal(Key.A, events.PressedKeyValue);
    }

    [Fact]
    public void KeyHold_SurvivesBeginFrame_ButThePressDoesNot()
    {
        LocalEvent events = CreateEvents();

        events.OnKeyboard(Key.Space, KeyModifier.None, KeyboardEventState.Down);
        events.BeginFrame();

        Assert.False(events.IsAnyKeyPressed);
        Assert.True(events.IsKeyBeingHold);
    }

    [Fact]
    public void LongPress_FiresOnceAfterTheTimeoutAndSwallowsTheClick()
    {
        Clock clock = new();
        LocalEvent events = new(clock.Read);

        events.OnMouseButton(true, MouseButtonType.Left, new Point(15, 15));

        clock.Now = 849;
        Assert.False(events.MouseLongPressLeft(Button));

        clock.Now = 850;
        Assert.True(events.MouseLongPressLeft(Button));

        // Only the first call wins.
        clock.Now = 5000;
        Assert.False(events.MouseLongPressLeft(Button));

        events.OnMouseButton(false, MouseButtonType.Left, new Point(15, 15));

        // The press was a hold, so the release is not a click.
        Assert.False(events.MouseClickLeft(Button));

        // A new press starts a new long press.
        events.OnMouseButton(true, MouseButtonType.Left, new Point(15, 15));
        clock.Now = 10_000;

        Assert.True(events.MouseLongPressLeft(Button));
    }

    [Fact]
    public void LongPress_RequiresThePressInsideTheArea()
    {
        Clock clock = new();
        LocalEvent events = new(clock.Read);

        events.OnMouseButton(true, MouseButtonType.Left, new Point(100, 100));
        clock.Now = 5000;

        Assert.False(events.MouseLongPressLeft(Button));
    }

    [Fact]
    public void LongPress_RequiresTheLeftButton()
    {
        Clock clock = new();
        LocalEvent events = new(clock.Read);

        events.OnMouseButton(true, MouseButtonType.Right, new Point(15, 15));
        clock.Now = 5000;

        Assert.False(events.MouseLongPressLeft(Button));
    }

    [Fact]
    public void MotionHook_ProvidesTheCursorRenderArea()
    {
        LocalEvent events = CreateEvents();

        events.MouseMotionHook = (x, y) => new Rect(x - 1, y - 1, 30, 30);
        events.OnMouseMotion(new Point(50, 60));

        Assert.Equal(new Rect(49, 59, 30, 30), events.CursorRenderArea);

        // The reference keeps only the latest area of the frame.
        events.BeginFrame();

        Assert.Equal(default, events.CursorRenderArea);
        Assert.Equal(new Point(50, 60), events.CursorPosition);
    }

    [Fact]
    public void KeyDownHook_ReceivesTheKeyAndTheModifier()
    {
        LocalEvent events = CreateEvents();

        Key? seenKey = null;
        KeyModifier seenModifier = KeyModifier.None;

        events.KeyDownHook = (key, modifier) =>
        {
            seenKey = key;
            seenModifier = modifier;
        };

        events.OnKeyboard(Key.S, KeyModifier.Ctrl | KeyModifier.Shift, KeyboardEventState.Down);

        Assert.Equal(Key.S, seenKey);
        Assert.Equal(KeyModifier.Ctrl | KeyModifier.Shift, seenModifier);
    }

    [Fact]
    public void ProcessQuitEvent_AsksTheHook()
    {
        LocalEvent events = CreateEvents();

        Assert.False(events.ProcessQuitEvent());

        events.QuitEventHook = () => true;

        Assert.True(events.ProcessQuitEvent());
    }

    [Fact]
    public void Reset_ForgetsEverything()
    {
        LocalEvent events = CreateEvents();

        events.OnMouseButton(true, MouseButtonType.Left, new Point(15, 15));
        events.OnKeyboard(Key.A, KeyModifier.None, KeyboardEventState.Down);
        events.MouseMotionHook = (x, y) => new Rect(x, y, 1, 1);
        events.OnMouseMotion(new Point(5, 5));

        events.Reset();

        Assert.False(events.IsMouseLeftButtonPressed);
        Assert.False(events.IsAnyKeyPressed);
        Assert.False(events.IsKeyBeingHold);
        Assert.False(events.HasMouseMoved);
        Assert.Equal(default, events.CursorRenderArea);
    }

    [Fact]
    public void Current_IsOneInstanceForTheProcess()
    {
        Assert.Same(LocalEvent.Current, LocalEvent.Current);
        Assert.False(LocalEvent.Current.IsMouseLeftButtonPressed);
    }

    [Fact]
    public void KeyEnumeration_EndsWithLastKey()
    {
        // LAST_KEY of the reference sits one past the last key, so it also counts the members.
        Assert.Equal((int)Key.LastKey, Enum.GetValues<Key>().Length - 1);
        Assert.Equal(0, (int)Key.None);
    }

    [Fact]
    public void HandleEvents_ThrowsWhenNoEngineIsAttached()
    {
        LocalEvent events = CreateEvents();

        Assert.Throws<InvalidOperationException>(() => events.HandleEvents());
    }

    [Fact]
    public void HandleEvents_PresentsTheCursorAreaAndSleepsOneMillisecond()
    {
        LocalEvent events = CreateEvents();
        FakeEngine engine = new() { OnHandle = state => state.OnMouseMotion(new Point(5, 6)) };

        events.MouseMotionHook = (x, y) => new Rect(x, y, 3, 3);
        events.Engine = engine;

        Assert.True(events.HandleEvents());

        // The reference renders the area the cursor layer asked for and then waits one millisecond.
        Assert.Equal(new Rect(5, 6, 3, 3), Assert.Single(engine.Rendered));
        Assert.Equal(LocalEvent.LoopSleepTimeMs, Assert.Single(engine.Sleeps));
    }

    [Fact]
    public void HandleEvents_PresentsTheWholeFrameWhenARefreshIsRequired()
    {
        LocalEvent events = CreateEvents();
        FakeEngine engine = new() { Refresh = true };

        events.Engine = engine;

        Assert.True(events.HandleEvents());

        // A resize or a palette cycle redraws everything, even without a cursor movement.
        Assert.Equal(engine.ScreenArea, Assert.Single(engine.Rendered));
    }

    [Fact]
    public void HandleEvents_WithoutSleepOnlyRecordsWhatToPresent()
    {
        LocalEvent events = CreateEvents();
        FakeEngine engine = new() { OnHandle = state => state.OnMouseMotion(new Point(5, 6)) };

        events.MouseMotionHook = (x, y) => new Rect(x, y, 3, 3);
        events.Engine = engine;

        Assert.True(events.HandleEvents(sleepAfterEventProcessing: false));

        // The caller draws right after the call: nothing is presented yet, the area is only deferred
        // (display.updateNextRenderRoi).
        Assert.Empty(engine.Rendered);
        Assert.Empty(engine.Sleeps);
        Assert.Equal(new Rect(5, 6, 3, 3), events.NextRenderArea);
    }

    [Fact]
    public void HandleEvents_ReturnsFalseAndPresentsNothingWhenTheEngineStops()
    {
        LocalEvent events = CreateEvents();
        FakeEngine engine = new() { Stop = true, OnHandle = state => state.OnMouseMotion(new Point(5, 6)) };

        events.Engine = engine;

        Assert.False(events.HandleEvents());

        Assert.Empty(engine.Rendered);
        Assert.Empty(engine.Sleeps);
    }

    [Fact]
    public void Touch_FirstFingerDrivesTheLeftButtonAndTheCursor()
    {
        LocalEvent events = CreateEvents();

        Assert.Equal(
            LocalEvent.TouchEmulation.LeftPressed,
            events.OnTouchFinger(LocalEvent.TouchFinger.Down, 1, 0, new Point(15, 15)));

        Assert.True(events.IsMouseLeftButtonPressed);
        Assert.True(events.IsMouseEventFromTouchpad);
        Assert.Equal(new Point(15, 15), events.CursorPosition);

        events.BeginFrame();

        Assert.Equal(
            LocalEvent.TouchEmulation.LeftReleased,
            events.OnTouchFinger(LocalEvent.TouchFinger.Up, 1, 0, new Point(15, 15)));

        Assert.True(events.IsMouseLeftButtonReleased);
        Assert.False(events.IsMouseLeftButtonPressed);
    }

    [Fact]
    public void Touch_MotionOfTheFirstFingerMovesTheCursorWithoutReleasing()
    {
        LocalEvent events = CreateEvents();
        events.OnTouchFinger(LocalEvent.TouchFinger.Down, 1, 0, new Point(15, 15));
        events.BeginFrame();

        Assert.Equal(
            LocalEvent.TouchEmulation.Motion,
            events.OnTouchFinger(LocalEvent.TouchFinger.Motion, 1, 0, new Point(35, 45)));

        Assert.Equal(new Point(35, 45), events.CursorPosition);
        Assert.True(events.IsMouseLeftButtonPressed);
        Assert.True(events.HasMouseMoved);
    }

    [Fact]
    public void Touch_SecondFingerEmulatesTheRightButtonAndOutlivesTheFirstFinger()
    {
        LocalEvent events = CreateEvents();
        events.OnTouchFinger(LocalEvent.TouchFinger.Down, 1, 0, new Point(15, 15));
        events.BeginFrame();

        Assert.Equal(
            LocalEvent.TouchEmulation.RightPressed,
            events.OnTouchFinger(LocalEvent.TouchFinger.Down, 2, 1, new Point(50, 50)));

        Assert.True(events.IsMouseRightButtonPressed);

        // The second finger must not drag the cursor away from the item being inspected.
        Assert.Equal(new Point(15, 15), events.CursorPosition);

        // Lifting the first finger keeps the right button held, which is the whole point of the gesture.
        events.BeginFrame();

        Assert.Equal(
            LocalEvent.TouchEmulation.None,
            events.OnTouchFinger(LocalEvent.TouchFinger.Up, 1, 0, new Point(15, 15)));

        Assert.True(events.IsMouseRightButtonPressed);

        Assert.Equal(
            LocalEvent.TouchEmulation.None,
            events.OnTouchFinger(LocalEvent.TouchFinger.Motion, 2, 1, new Point(60, 60)));

        Assert.True(events.IsMouseRightButtonPressed);

        events.BeginFrame();

        Assert.Equal(
            LocalEvent.TouchEmulation.RightReleased,
            events.OnTouchFinger(LocalEvent.TouchFinger.Up, 2, 1, new Point(60, 60)));

        Assert.False(events.IsMouseRightButtonPressed);
    }

    [Fact]
    public void Touch_IgnoresAThirdFingerAndFingersThatAreNotOnTheScreen()
    {
        LocalEvent events = CreateEvents();
        events.OnTouchFinger(LocalEvent.TouchFinger.Down, 1, 0, new Point(10, 10));
        events.OnTouchFinger(LocalEvent.TouchFinger.Down, 2, 1, new Point(20, 20));

        Assert.Equal(
            LocalEvent.TouchEmulation.None,
            events.OnTouchFinger(LocalEvent.TouchFinger.Down, 3, 2, new Point(30, 30)));

        Assert.Equal(
            LocalEvent.TouchEmulation.None,
            events.OnTouchFinger(LocalEvent.TouchFinger.Motion, 9, 9, new Point(30, 30)));

        // The stray events did not disturb the gesture: the second finger still holds the right button.
        Assert.True(events.IsMouseRightButtonPressed);
        Assert.Equal(new Point(10, 10), events.CursorPosition);
    }

    private static LocalEvent CreateEvents() => new(() => 0);

    /// <summary>An event engine that records what <see cref="LocalEvent.HandleEvents"/> asked it to do.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public List<Rect> Rendered { get; } = [];

        public List<ulong> Sleeps { get; } = [];

        public bool Stop { get; set; }

        public bool Refresh { get; set; }

        public Action<LocalEvent>? OnHandle { get; set; }

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = Refresh;
            OnHandle?.Invoke(state);

            return !Stop;
        }

        public void Render(Rect area) => Rendered.Add(area);

        public void Sleep(ulong milliseconds) => Sleeps.Add(milliseconds);
    }

    private static void Click(LocalEvent events, Point press, Point release) =>
        Click(events, MouseButtonType.Left, press, release);

    private static void Click(LocalEvent events, MouseButtonType button, Point press, Point release)
    {
        events.OnMouseButton(true, button, press);
        events.OnMouseButton(false, button, release);
    }

    /// <summary>A clock the test moves by hand, in milliseconds.</summary>
    private sealed class Clock
    {
        public long Now { get; set; }

        public long Read() => Now;
    }
}
