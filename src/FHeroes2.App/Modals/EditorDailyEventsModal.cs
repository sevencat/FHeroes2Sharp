using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The daily events window run on the host's frame pump, <c>openDailyEventsWindow()</c> of the reference
/// (<c>editor_daily_events_window.cpp:160-318</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在循环里对加一条与改一条都调 <c>editDailyEvent()</c>（<c>:252-281</c>），那个函数自己阻塞开 608×430 的
/// 事件窗（<c>editor_daily_event_spec_window.cpp:67-372</c>）。端口把事件窗摞在这个类自己的栈上，事件窗再把自己的
/// 消息输入框摞在它自己的栈上；两层都关掉才回到列表。
/// </para>
/// <para>
/// 写回只在两层都点头时发生：参考的 <c>if ( editDailyEvent( temp, ... ) ) { dailyEvents.emplace_back(...) }</c>
/// （<c>:254-261</c>、<c>:273-278</c>），所以事件窗取消就什么都不加、什么都不换。
/// </para>
/// </remarks>
internal sealed class EditorDailyEventsModal : IModalScreen
{
    private readonly EditorDailyEventsWindow _window;
    private readonly EditorSceneHost _host;
    private readonly PlayerColor _availableHumanColors;
    private readonly PlayerColor _availableComputerColors;
    private readonly Stack<IModalScreen> _inner = new();

    private bool _addPending;

    /// <summary>Wraps the window.</summary>
    /// <param name="window">The events window to run.</param>
    /// <param name="host">Where the event window, the input box and the message box are built from.</param>
    /// <param name="availableHumanColors">Colours the map lets humans play, <c>humanPlayerColors</c>.</param>
    /// <param name="availableComputerColors">Colours the map lets computers play, <c>computerPlayerColors</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorDailyEventsModal(EditorDailyEventsWindow window, EditorSceneHost host,
        PlayerColor availableHumanColors, PlayerColor availableComputerColors)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
        _availableHumanColors = availableHumanColors;
        _availableComputerColors = availableComputerColors;
    }

    /// <summary>The window being run.</summary>
    public EditorDailyEventsWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorListOutcome.None"/> until it reports one.</summary>
    public EditorListOutcome Outcome { get; private set; }

    /// <summary>
    /// The events to write back, or <see langword="null"/> when the author cancelled, the <c>return false</c> of
    /// <c>:317</c>.
    /// </summary>
    public IReadOnlyList<MapDailyEvent>? AcceptedEvents =>
        Outcome == EditorListOutcome.Accepted ? _window.Events : null;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>Whether the event window is stacked above the list.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The inner window on top of the stack; <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Top?.HotKey(hotKey);

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels, LocalEvent.Current);

        Top?.Draw(frame);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (IsFinished)
        {
            return;
        }

        // 事件窗（或它自己的输入框）还开着：这一帧只归它，列表既不读事件也不改。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
                Resolve(inner);
            }

            return;
        }

        EditorListOutcome outcome = _window.Process(LocalEvent.Current);

        if (outcome != EditorListOutcome.None)
        {
            Outcome = outcome;
            IsFinished = true;

            return;
        }

        // 加一条 / 改一条先于右键说明：参考的 :252-281 在 :294-305 之前。
        if (_window.AddRequested)
        {
            OpenSpec(new MapDailyEvent(), isAdd: true);

            return;
        }

        if (_window.EditRequested)
        {
            OpenSpec(_window.SelectedEvent?.Clone() ?? new MapDailyEvent(), isAdd: false);

            return;
        }

        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Opens the event window an add or an edit asks for, the <c>editDailyEvent</c> of <c>:252-281</c>.</summary>
    /// <param name="dailyEvent">The event to edit; the window keeps its own copy.</param>
    /// <param name="isAdd">Whether the answer appends an event instead of replacing the selected one.</param>
    private void OpenSpec(MapDailyEvent dailyEvent, bool isAdd)
    {
        if (_host.TryCreateDailyEventArtwork() is not { } artwork || !_host.TryGetFonts(out GameFonts? fonts))
        {
            return;
        }

        var window = EditorDailyEventSpecWindow.Create(dailyEvent, _availableHumanColors,
            _availableComputerColors, artwork, ScreenBuffer.DefaultSize, fonts.Yellow, fonts.White);

        _addPending = isAdd;

        _inner.Push(new EditorDailyEventSpecModal(window, _host));
    }

    /// <summary>Feeds the event the spec window produced back into the list, <c>:254-261</c> and <c>:273-278</c>.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        bool isAdd = _addPending;

        _addPending = false;

        // 消息框与取消的事件窗都没有事件可写。
        if (closed is not EditorDailyEventSpecModal { AcceptedEvent: { } dailyEvent })
        {
            return;
        }

        if (isAdd)
        {
            _window.AddEvent(dailyEvent);
        }
        else if (!_window.ReplaceSelected(dailyEvent))
        {
            GameLog.Warn("The edited daily event could not replace the selected one.");
        }
    }
}
