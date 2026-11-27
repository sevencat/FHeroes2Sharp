using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The language window run on the host's frame pump, <c>openLanguageWindow()</c> of the reference
/// (<c>editor_language_window.cpp:133-306</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在 ADD 里阻塞调 <c>fheroes2::selectLanguage()</c>（<c>:253-269</c>）；端口把选择器摞在自己的栈上，选中之后
/// 才回喂 <see cref="EditorLanguageWindow.AddLanguage"/>，被拒（不是主语言就是已经在列表里）时照参考弹一句提示。
/// </para>
/// <para>
/// 写回交给 <see cref="EditorMapSpecsInput.ResolveLanguage"/>，它读窗口的 <see cref="EditorLanguageWindow.Result"/>
/// 与 <see cref="EditorLanguageWindow.ApplyTo(MapSpecsState)"/>；本类只负责把 <see cref="Outcome"/> 报出去。
/// </para>
/// </remarks>
internal sealed class EditorLanguageModal : IModalScreen
{
    private readonly EditorLanguageWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the window.</summary>
    /// <param name="window">The language window to run.</param>
    /// <param name="host">Where the language picker and the message box are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorLanguageModal(EditorLanguageWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run; its <see cref="EditorLanguageWindow.Result"/> is the answer.</summary>
    public EditorLanguageWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorListOutcome.None"/> until it reports one.</summary>
    public EditorListOutcome Outcome { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>Whether the language picker or a message box is stacked above the list.</summary>
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

        // 选择器或提示框还开着：这一帧只归它，语言列表既不读事件也不改。
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

        // 参考 :253-256：ADD 先弹选择器，选中之后才进列表；它排在右键说明之前。
        if (_window.LanguageAddRequested)
        {
            _inner.Push(new LanguagePickerModal(_window.CreateLanguagePicker(ScreenBuffer.DefaultSize)));

            return;
        }

        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Feeds the picked language back into the list, the body of <c>:258-269</c>.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        if (closed is not LanguagePickerModal picker || !picker.Accepted)
        {
            return;
        }

        if (_window.AddLanguage(picker.SelectedLanguage))
        {
            return;
        }

        // 已经在列表里：参考骂一句再回循环（:261）；选的是主语言时参考静默忽略，Popup 也就没有。
        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }
}

/// <summary>
/// The language picker of <see cref="EditorLanguageWindow.CreateLanguagePicker"/> on the host's frame pump,
/// <c>fheroes2::selectLanguage( ..., isGameLanguage = false )</c>.
/// </summary>
/// <remarks>
/// <see cref="LanguageSelectionDialog"/> 是 <c>SetCursor</c> / <c>SetLeftButton</c> 那一套（与主菜单的语言选择同一个类），
/// 不读 <see cref="LocalEvent"/>，所以摞在栈上当内层窗正合适。
/// </remarks>
internal sealed class LanguagePickerModal : IModalScreen
{
    private readonly LanguageSelectionDialog _dialog;
    private DialogButton _answer = DialogButton.None;

    /// <summary>Wraps the picker.</summary>
    /// <param name="dialog">The picker to run.</param>
    /// <exception cref="ArgumentNullException">The picker is <see langword="null"/>.</exception>
    public LanguagePickerModal(LanguageSelectionDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        _dialog = dialog;
    }

    /// <summary>The picker being run.</summary>
    public LanguageSelectionDialog Dialog => _dialog;

    /// <summary>Whether the author confirmed the pick instead of dropping it.</summary>
    public bool Accepted => _answer == DialogButton.Ok;

    /// <summary>
    /// The language the author picked; the index runs over <see cref="Enum.GetValues{TEnum}()"/> the same way
    /// <see cref="EditorLanguageWindow.CreateLanguagePicker"/> built the names.
    /// </summary>
    public SupportedLanguage SelectedLanguage => Enum.GetValues<SupportedLanguage>()[_dialog.SelectedIndex];

    /// <inheritdoc />
    public bool IsFinished => _answer is DialogButton.Ok or DialogButton.Cancel;

    /// <inheritdoc />
    public void SetCursor(Point? position) => _ = _dialog.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Answer(_dialog.SetLeftButton(down));

    /// <inheritdoc />
    public void SetRightButton(bool down) => _ = down;

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Answer(_dialog.ProcessHotKey(hotKey));

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _dialog.Draw(frame.Pixels);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _ = elapsedSeconds;

        // 选择器没有动画，重绘由宿主每帧整屏做。
    }

    /// <summary>Keeps the answer of the button row; OK and CANCEL close the picker.</summary>
    /// <param name="button">What the button row reported.</param>
    private void Answer(DialogButton button)
    {
        if (button is DialogButton.Ok or DialogButton.Cancel)
        {
            _answer = button;
        }
    }
}
