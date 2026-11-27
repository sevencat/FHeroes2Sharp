using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The map specifications window run on the host's frame pump, <c>openMapSpecificationsDialog()</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的这个窗口是个阻塞函数：里面再用 <c>Dialog::inputString</c>、<c>showStandardTextMessage</c> 开窗口，
/// <c>while ( le.HandleEvents() )</c> 一层套一层（<c>editor_map_specs_window.cpp:2212</c> 起）。端口只有一条帧循环，
/// 递归 <c>GameHost.RunModal</c> 会在已经开着窗口的时候再压一层，所以内层窗摞在这个类自己的栈上：
/// 栈上有窗口时，只更新栈顶、只把它收到的输入转给栈顶；栈空了才轮到规格页自己的
/// <see cref="EditorMapSpecsInput.Process"/>。每帧读的都是宿主那份 <see cref="LocalEvent.Current"/>——规格页与它的
/// 子窗用的都是参考的同一个 <c>LocalEvent</c>。
/// </para>
/// <para>
/// 关窗的两步照参考：确定就 <see cref="EditorMapSpecsInput.Commit"/> 把状态写回地图，写不进去就把原因留在
/// <see cref="Error"/> 上（宿主在模态结束后再弹错误框）；取消什么都不写。
/// </para>
/// </remarks>
internal sealed class EditorMapSpecsModal : IModalScreen
{
    private readonly EditorMapSpecsWindow _window;
    private readonly EditorMapSpecsInput _input;
    private readonly MapEditSession _session;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private EditorMapSpecsRequest _pending = EditorMapSpecsRequest.None;

    /// <summary>Wraps the page and its state machine.</summary>
    /// <param name="window">The page window.</param>
    /// <param name="input">The state machine fed by the page's events.</param>
    /// <param name="session">The session the accepted changes are committed to.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorMapSpecsModal(
        EditorMapSpecsWindow window, EditorMapSpecsInput input, MapEditSession session, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _input = input;
        _session = session;
        _host = host;
    }

    /// <summary>How the player left the page.</summary>
    public EditorMapSpecsOutcome Outcome { get; private set; }

    /// <summary>The page being run.</summary>
    public EditorMapSpecsWindow Window => _window;

    /// <summary>Why the accepted changes could not be written back; <see langword="null"/> when they were.</summary>
    public string? Error { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <summary>The inner window on top of the stack; <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

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

        _window.Paint(frame.Pixels, LocalEvent.Current);

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

        // 内层窗还开着：这一帧只归它，规格页既不读事件也不改状态，与参考的内层 while 一样。
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

        EditorMapSpecsOutcome outcome = _input.Process(LocalEvent.Current);

        if (outcome != EditorMapSpecsOutcome.None)
        {
            Finish(outcome);

            return;
        }

        if (!_input.Request.IsNone)
        {
            Open(_input.Request);
        }
    }

    /// <summary>Opens the window a request of the page asks for, <c>_queueEvent</c> of the reference.</summary>
    /// <param name="request">The request; already consumed by the page state machine.</param>
    private void Open(EditorMapSpecsRequest request)
    {
        IModalScreen? window = request.Kind switch
        {
            EditorMapSpecsRequestKind.ShowMessage => _host.TryCreateMessageBox(request.Title, request.Body),
            EditorMapSpecsRequestKind.EditMapName or EditorMapSpecsRequestKind.EditMapDescription or
                EditorMapSpecsRequestKind.EditCreatorNotes => request.Dialog is { } dialog
                    ? _host.TryCreateInput(dialog)
                    : null,
            EditorMapSpecsRequestKind.OpenRumors => TryCreateRumors(),
            EditorMapSpecsRequestKind.OpenDailyEvents => TryCreateDailyEvents(),
            EditorMapSpecsRequestKind.OpenLanguage => TryCreateLanguage(),
            EditorMapSpecsRequestKind.PickConditionTarget => TryCreateTargetPicker(),
            EditorMapSpecsRequestKind.EditValue => TryCreateValuePad(request.ValueField),
            _ => null,
        };

        if (window is null)
        {
            GameLog.Warn($"The map specifications page asked for {request.Kind}, and no window answers it yet.");

            return;
        }

        _pending = request;
        _inner.Push(window);
    }

    /// <summary>Feeds the answer of a closed inner window back into the page state machine.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        EditorMapSpecsRequest request = _pending;
        _pending = EditorMapSpecsRequest.None;

        // 消息框没有答案；文本输入的答案读窗口自己记下的那段字，取消时是 null。
        if (closed is InputStringModal input)
        {
            _ = _input.Resolve(input.Window.Result);
        }
        else if (closed is EditorRumorModal rumors)
        {
            // 取消时 AcceptedRumors 是 null，参考的 `if ( openRumorWindow( ... ) )` 也就不写（:2284-2286）。
            _ = _input.ResolveRumors(rumors.AcceptedRumors);
        }
        else if (closed is EditorDailyEventsModal events)
        {
            // :2292-2294 的同一个门槛：取消就不写。
            _ = _input.ResolveDailyEvents(events.AcceptedEvents);
        }
        else if (closed is EditorLanguageModal language)
        {
            // 参考 :2298-2311 把窗自己交回去，ResolveLanguage 读 Result 与 ApplyTo。
            _ = _input.ResolveLanguage(language.Window);
        }
        else if (closed is EditorMapSpecsTargetModal target)
        {
            // 参考 SelectMapObject 的写回：取消时 GetCenter() 那几个调用被 if 挡掉，端口传 null（:1252-1264）。
            _ = _input.ResolveTarget(target.SelectedPosition);
        }
        else if (closed is EditorMapSpecsValueModal value)
        {
            // openVirtualNumpad() 只有「校验通过才写回 output」一条路，没通过时窗还开着、走不到这里。
            _ = _input.ResolveValue(value.Value);
        }
        else if (request.Kind != EditorMapSpecsRequestKind.ShowMessage)
        {
            GameLog.Warn($"The answer of {request.Kind} dropped on the floor.");
        }
    }

    /// <summary>Builds the rumors window the page asks for, <c>:2282-2288</c>.</summary>
    /// <returns>The window, or <see langword="null"/> when the artwork, the fonts or the page state says no.</returns>
    private EditorRumorModal? TryCreateRumors()
    {
        if (_host.TryCreateListArtwork() is not { } artwork || !_host.TryGetFonts(out GameFonts? fonts) ||
            _input.CreateRumorWindow(artwork, fonts.Yellow, fonts.White) is not { } window)
        {
            return null;
        }

        return new EditorRumorModal(window, _host);
    }

    /// <summary>Builds the daily events window the page asks for, <c>:2290-2297</c>.</summary>
    /// <returns>The window, or <see langword="null"/> when the artwork, the fonts or the page state says no.</returns>
    private EditorDailyEventsModal? TryCreateDailyEvents()
    {
        if (_host.TryCreateListArtwork() is not { } artwork || !_host.TryGetFonts(out GameFonts? fonts) ||
            _input.CreateDailyEventsWindow(artwork, fonts.Yellow, fonts.White) is not { } window)
        {
            return null;
        }

        // 事件的颜色集要先与地图允许的求交，参考把它放在 editDailyEvent 开头（:71-72）；这里交给事件窗的
        // Clamp（EditorDailyEventSpecWindow.Create），列表只需要知道地图允许哪两个集合。
        return new EditorDailyEventsModal(window, _host, _window.State.HumanPlayers,
            _window.State.ComputerPlayers);
    }

    /// <summary>Builds the language window the page asks for, <c>:2298-2311</c>.</summary>
    /// <returns>The window, or <see langword="null"/> when the artwork, the fonts or the page state says no.</returns>
    private EditorLanguageModal? TryCreateLanguage()
    {
        if (_host.TryCreateListArtwork() is not { } artwork || !_host.TryGetFonts(out GameFonts? fonts) ||
            _input.CreateLanguageWindow(artwork, fonts.Yellow, fonts.White) is not { } window)
        {
            return null;
        }

        return new EditorLanguageModal(window, _host);
    }

    /// <summary>Builds the candidate list window the condition panels ask for, <c>:1252-1264</c>.</summary>
    /// <returns>The window, or <see langword="null"/> when the artwork, the fonts or the page state says no.</returns>
    private EditorMapSpecsTargetModal? TryCreateTargetPicker()
    {
        if (_host.TryCreateItemSelectionArtwork() is not { } artwork || !_host.TryGetFonts(out GameFonts? fonts) ||
            _input.CreateTargetPicker(artwork, fonts.Yellow, fonts.White) is not { } picker)
        {
            return null;
        }

        return new EditorMapSpecsTargetModal(picker);
    }

    /// <summary>
    /// Builds the number pad of one of the two value elements, the <c>openVirtualNumpad( _value, _minimum, _maximum )</c>
    /// of <c>:1465</c> and <c>:1885</c>.
    /// </summary>
    /// <param name="field">Which element was clicked; the pad reads its current value and its limits.</param>
    /// <returns>The pad, or <see langword="null"/> when the element is not on the page or the artwork is missing.</returns>
    private EditorMapSpecsValueModal? TryCreateValuePad(EditorMapSpecsValueField field)
    {
        if (_window.GetValueElement(field) is not { } element ||
            _host.TryCreateNumpad(element.Value, element.Minimum, element.Maximum) is not { } pad)
        {
            return null;
        }

        return new EditorMapSpecsValueModal(pad, element.Minimum, element.Maximum, _host);
    }

    /// <summary>Ends the page and writes the state back when the player accepted it.</summary>
    /// <param name="outcome">How the page was left.</param>
    private void Finish(EditorMapSpecsOutcome outcome)
    {
        Outcome = outcome;
        IsFinished = true;

        if (outcome != EditorMapSpecsOutcome.Accepted)
        {
            return;
        }

        // 写回只在地图尺寸对不上时抛；抛了就把原因交给宿主，规格页本身照常关掉。
        try
        {
            _input.Commit(_session);
        }
        catch (ArgumentException error)
        {
            Error = error.Message;
        }
    }
}
